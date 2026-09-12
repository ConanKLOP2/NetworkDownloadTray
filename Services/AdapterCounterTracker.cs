namespace NetworkDownloadTray.Services;

public sealed class AdapterCounterTracker
{
    private Dictionary<string, long> _previous = new();
    private double? _timestamp;

    public void Reset() { _previous.Clear(); _timestamp = null; }

    public (double Speed, bool Measuring) Sample(IReadOnlyDictionary<string, long> counters, double timestamp)
    {
        double elapsed = timestamp - (_timestamp ?? timestamp);
        double bytes = 0;
        bool comparable = false;
        if (elapsed > 0 && elapsed <= 10)
            foreach (var pair in counters)
                if (_previous.TryGetValue(pair.Key, out long before) && pair.Value >= before)
                {
                    bytes += pair.Value - before;
                    comparable = true;
                }
        _previous = new(counters);
        _timestamp = timestamp;
        return (comparable ? bytes / elapsed * 8 / 1_000_000 : 0, counters.Count > 0 && !comparable);
    }
}
