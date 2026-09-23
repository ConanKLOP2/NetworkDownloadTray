using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray.Tests;

public class MonitorTests
{
    private sealed class BlockingProvider : INetworkStatisticsProvider
    {
        public readonly ManualResetEventSlim Entered = new(), Release = new(true);
        public long Bytes = 100;
        public int Calls, MaxActive;
        private int _active;
        public IReadOnlyList<AdapterStatistics> Read()
        {
            long bytes = Interlocked.Read(ref Bytes);
            int active = Interlocked.Increment(ref _active);
            if (active > MaxActive) MaxActive = active;
            Interlocked.Increment(ref Calls);
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(10));
            Interlocked.Decrement(ref _active);
            return [new("wifi", "Wi-Fi", "Intel wireless", NetworkInterfaceType.Wireless80211, OperationalStatus.Up, bytes)];
        }
    }

    [Fact]
    public async Task ConfigureAdaptersDoesNotWaitForInFlightReadAndDiscardsIt()
    {
        var provider = new BlockingProvider();
        double clock = 1;
        var reader = new NetworkSpeedReader(provider, () => clock);
        Assert.True(reader.ReadWithDiagnostics().Snapshot.IsMeasuring);
        clock = 2;
        provider.Bytes = 1_000_100;
        provider.Release.Reset();
        provider.Entered.Reset();
        var inFlight = Task.Run(reader.ReadWithDiagnostics);
        Assert.True(provider.Entered.Wait(TimeSpan.FromSeconds(5)));
        var watch = Stopwatch.StartNew();
        reader.ConfigureAdapters(new Dictionary<string, bool> { ["wifi"] = true });
        watch.Stop();
        Assert.True(watch.ElapsedMilliseconds < 100, $"ConfigureAdapters took {watch.ElapsedMilliseconds} ms");
        Assert.False(inFlight.IsCompleted);
        provider.Release.Set();
        var result = (await inFlight).Snapshot;
        Assert.Equal(0d, result.MegabitsPerSecond);
        Assert.True(result.IsMeasuring);
        // The pre-reconfigure counters must not become the baseline, or this read would report 8 Mbps.
        clock = 3; provider.Bytes = 2_000_100;
        Assert.True(reader.ReadWithDiagnostics().Snapshot.IsMeasuring);
        clock = 4; provider.Bytes = 3_000_100;
        var next = reader.ReadWithDiagnostics();
        Assert.Equal(8d, next.Snapshot.MegabitsPerSecond);
        Assert.Equal("Include", next.Diagnostics[0].SelectionMode);
    }

    [Fact]
    public async Task ResetDuringInFlightReadDoesNotBlockOrSpike()
    {
        var provider = new BlockingProvider();
        double clock = 1;
        var reader = new NetworkSpeedReader(provider, () => clock);
        reader.ReadWithDiagnostics();
        clock = 2; provider.Bytes = 1_000_100;
        provider.Release.Reset(); provider.Entered.Reset();
        var inFlight = Task.Run(reader.ReadWithDiagnostics);
        Assert.True(provider.Entered.Wait(TimeSpan.FromSeconds(5)));
        var watch = Stopwatch.StartNew();
        reader.Reset();
        Assert.True(watch.ElapsedMilliseconds < 100);
        provider.Release.Set();
        Assert.True((await inFlight).Snapshot.IsMeasuring);
        clock = 3; provider.Bytes = 2_000_100;
        Assert.True(reader.ReadWithDiagnostics().Snapshot.IsMeasuring);
    }

    [Fact]
    public async Task OverlappingReadsAreSerialized()
    {
        var provider = new BlockingProvider();
        double clock = 1;
        var reader = new NetworkSpeedReader(provider, () => clock);
        provider.Release.Reset();
        var first = Task.Run(reader.ReadWithDiagnostics);
        var second = Task.Run(reader.ReadWithDiagnostics);
        Assert.True(provider.Entered.Wait(TimeSpan.FromSeconds(5)));
        await Task.Delay(50);
        provider.Release.Set();
        await Task.WhenAll(first, second);
        Assert.Equal(2, provider.Calls);
        Assert.Equal(1, provider.MaxActive);
    }

    [Fact]
    public void TrackerSampleAllocatesNothingAfterWarmUp()
    {
        var tracker = new AdapterCounterTracker();
        var counters = new Dictionary<string, long>();
        for (int i = 0; i < 36; i++) counters["adapter-" + i] = i;
        double time = 0;
        for (int i = 0; i < 100; i++) { time++; tracker.Sample(counters, time); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { time++; tracker.Sample(counters, time); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void LockPausesSamplingAndUnlockResumes() => RunOnDispatcher(async () =>
    {
        var provider = new BlockingProvider();
        double clock = 1;
        int samples = 0;
        using var monitor = new DownloadMonitorService(new NetworkSpeedReader(provider, () => clock));
        monitor.SampleUpdated += (_, _) => samples++;
        monitor.OnSessionUnlocked();
        Assert.False(monitor.IsSampling);
        Assert.Equal(0, Volatile.Read(ref provider.Calls));
        monitor.Start();
        Assert.True(monitor.IsSampling);
        await WaitFor(() => samples == 1);

        monitor.OnSessionLocked();
        Assert.False(monitor.IsSampling);
        int calls = Volatile.Read(ref provider.Calls);
        await Task.Delay(1300);
        Assert.Equal(calls, Volatile.Read(ref provider.Calls));
        monitor.OnSuspended();
        monitor.OnResumed();
        Assert.False(monitor.IsSampling);

        clock = 5;
        monitor.OnSessionUnlocked();
        Assert.True(monitor.IsSampling);
        await WaitFor(() => samples == 2);
        Assert.True(monitor.Latest!.Snapshot.IsMeasuring);

        // Suspend must not stop the timer: an unreported automatic resume would leave it stopped.
        monitor.OnSuspended();
        Assert.True(monitor.IsSampling);
        monitor.OnResumed();
        Assert.True(monitor.IsSampling);

        monitor.Dispose();
        monitor.OnSessionUnlocked();
        monitor.OnResumed();
        Assert.False(monitor.IsSampling);
    });

    private static async Task WaitFor(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), "Condition not reached.");
            await Task.Delay(10);
        }
    }

    private static void RunOnDispatcher(Func<Task> body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await body(); }
                catch (Exception ex) { failure = ex; }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Dispatcher test did not exit.");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
