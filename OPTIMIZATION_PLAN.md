# NetworkDownloadTray — Performance Optimization Plan (Round 2, multi-agent)

Date: 2026-09-23 · Baseline commit: `7dc41c6` · Baseline tests: 33 passed / 0 failed / 1 skipped (opt-in native tray)

## 1. Goal

A tray app runs 24/7, so the cost that matters is the **steady-state per-second tick**:
CPU time, allocations (GC pressure), wake-ups, and resident memory. Round 1 (`PLAN.md`)
fixed correctness/lifecycle. This round targets measured hot-path cost **without changing
user-visible behavior** (same adapters, same inclusion rules, same override IDs, same Mbps).

## 2. Baseline measurements (this machine, 36 network interfaces, Release, .NET 10.0.401)

| Hot path (runs every 1 s)                                   | Time/op      | Alloc/op   |
|-------------------------------------------------------------|--------------|------------|
| `GetAllNetworkInterfaces()` only                            | 30.9 ms      | 48.7 KB    |
| **Current provider**: enumerate + `GetIPStatistics()` × 36  | **34.2 ms**  | **98.2 KB**|
| Cached `NetworkInterface[]` + `GetIPStatistics()` × 36      | 2.9 ms       | 49.2 KB    |
| **`GetIfTable2` (one native call, all rows)**               | **0.94 ms**  | **0 B**    |
| `TrayIconSize.Read()` (3 user32 calls)                      | 0.002 ms     | 0 B        |

| Icon path (runs when displayed text changes)                | Time/op      | Alloc/op   |
|-------------------------------------------------------------|--------------|------------|
| `EncodeIco` 16 px / 32 px                                   | 63 / 173 µs  | 6.4 / 23 KB|
| `TrayIconRenderer.Create` 32 px (current, with `Clone()`)   | **1490 µs**  | 27.6 KB    |
| `new Icon(stream)` without `Clone()`                        | **690 µs**   | 27.5 KB    |

`MIB_IF_ROW2` offsets were verified on this machine: 36/36 rows matched `NetworkInterface`
on Name(Alias), Description, Type, OperationalStatus and InOctets≈BytesReceived.

**Conclusion:** ~97 % of steady-state cost is adapter enumeration. It is the #1 target.
DPI lookup is negligible — explicitly **not** worth caching (rejected).

## 3. Findings → optimization backlog

| ID  | Finding (file)                                                                                       | Fix                                                                                  | Expected gain                     | Risk |
|-----|------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------|-----------------------------------|------|
| A1  | Full `GetAdaptersAddresses` + N× `GetIfEntry2` every second (`NetworkStatisticsProvider.cs`)         | Hybrid provider: cached adapter membership + one `GetIfTable2` per tick               | 34 ms → ~1 ms, 98 KB → ~2 KB/tick  | Med  |
| A2  | No fallback if native call fails                                                                     | Keep managed path as fallback provider                                               | Robustness                        | Low  |
| B1  | `Icon.Clone()` re-parses and creates a 2nd HICON (`TrayIconRenderer.Create`)                          | Construct `Icon` from stream once; `Icon` copies the bytes internally                | −54 % icon build time             | Low  |
| B2  | `EncodeIco` uses `MemoryStream`+`BinaryWriter`+`ToArray` and `bool[16,16]`+`bool[size,size]`          | Write into one exact-size `byte[]` with `BinaryPrimitives`; single pixel buffer       | fewer allocations/copies          | Low  |
| B3  | `ToolTipText` re-assigned every tick → `Shell_NotifyIcon(NIM_MODIFY)` cross-process call each second  | Skip when tooltip string unchanged                                                    | 0 shell IPC calls at steady state  | Low  |
| B4  | Build config: background GC thread for a ~MB heap; all satellite languages; no ReadyToRun             | `ConcurrentGarbageCollection=false`, `SatelliteResourceLanguages=en`, R2R in win-x64   | lower RSS, faster cold start       | Low  |
| C1  | `AdapterCounterTracker.Sample` allocates a new `Dictionary` each tick                                 | Double-buffer two dictionaries, swap + clear                                          | 0 dictionary alloc/tick            | Low  |
| C2  | `NetworkSpeedReader` holds `_gate` during provider I/O → UI thread blocks on `ConfigureAdapters`      | Snapshot overrides via immutable reference; lock only the tracker math                | no UI stall on override change     | Med  |
| C3  | Reader does 2 dictionary lookups per adapter for the same override                                    | Single lookup                                                                         | micro                              | Low  |
| C4  | Sampling continues at 1 Hz while the session is locked (tray invisible)                              | `SystemEvents.SessionSwitch`: stop timer on lock; reset + restart on unlock (suspend only resets — see §8) | 0 wake-ups while locked            | Med  |
| D1  | `MainWindow` (DataGrid, styles, templates) is always built, even with "Start minimized to tray"       | Create window lazily on first Open / when tray unavailable                            | lower startup time & RSS           | Med  |
| D2  | `AdapterRow.Apply` raises `PropertyChanged("")` → every binding of every row re-evaluates              | Raise only changed properties                                                         | fewer binding updates every 5 s    | Low  |
| D3  | `SettingsService.Save` allocates new `JsonSerializerOptions` each save (defeats metadata cache)       | `static readonly` options                                                             | micro                              | Low  |

Explicitly rejected (documented so no agent re-proposes them):
- Caching `TrayIconSize.Read()` — 2 µs; caching adds DPI-change invalidation bugs for no gain.
- `InvariantGlobalization` — changes `N0` formatting in the DataGrid for non-English locales.
- Enabling H.NotifyIcon EfficiencyMode — visible behavior change (Task Manager leaf / EcoQoS); out of scope.
- Changing the 1 s sampling interval — user-visible behavior.

## 4. Multi-agent execution model

### 4.1 Topology

```
Wave 0 (lead)      Baseline + benchmarks + this plan                              [DONE]
                              │
Wave 1 (parallel)  ┌──────────┼───────────┬────────────┐
                   A          B           C            D        4 agents, 4 git worktrees
               native      tray icon   sampling     UI /
               provider    + build     core         lifecycle
                   └──────────┴─────┬─────┴────────────┘
Wave 2 (lead)      Merge A→C→B→D, full build + tests, re-run benchmarks
                              │
Wave 3 (agent)     Independent adversarial review of the combined diff
                              │
Wave 4 (lead)      Fix review findings, update IMPLEMENTATION_HISTORY.md (left uncommitted for user review)
```

### 4.2 File ownership (the contract that makes parallel work conflict-free)

Each agent may edit **only** the files it owns. Anything else is read-only. If an agent
believes another file must change, it stops and reports instead of editing.

| Agent | Owns (may edit / create)                                                                                                  |
|-------|---------------------------------------------------------------------------------------------------------------------------|
| A     | `Services/NetworkStatisticsProvider.cs`, new `Services/Native/IpHelperApi.cs`, new `tests/.../ProviderTests.cs`             |
| B     | `Services/TrayIconRenderer.cs`, `Services/TrayIconService.cs`, `tests/.../RendererTests.cs`, `NetworkDownloadTray.csproj`, `Properties/PublishProfiles/*.pubxml` |
| C     | `Services/AdapterCounterTracker.cs`, `Services/NetworkSpeedReader.cs`, `Services/DownloadMonitorService.cs`, `tests/.../AdapterTests.cs`, `tests/.../ReaderTests.cs`, new `tests/.../MonitorTests.cs` |
| D     | `App.xaml.cs`, `MainWindow.xaml.cs`, `Models/AdapterRow.cs`, `Services/SettingsService.cs`, `tests/.../DesktopIntegrationTests.cs`, new `tests/.../AdapterRowTests.cs` |
| Lead  | `OPTIMIZATION_PLAN.md`, `IMPLEMENTATION_HISTORY.md`, `README.md`, merges                                                     |

### 4.3 Frozen interfaces (must not change in Wave 1)

- `INetworkStatisticsProvider.Read()` and `AdapterStatistics` record shape.
- Class name `NetworkStatisticsProvider` stays the parameterless default used by `NetworkSpeedReader`.
- `NetworkSpeedReader` public API: ctor(provider?, clock?), `ConfigureAdapters`, `Reset`, `ReadWithDiagnostics`.
- `DownloadMonitorService` public API: ctor, `ConfigureAdapters`, `Start`, `RefreshAsync`, `Latest`,
  events `SampleUpdated` / `SpeedUpdated` / `DiagnosticsUpdated`, internal `ResetMeasurements`, `Dispose`.
- `ITrayIconService`, `TrayIconRenderer` public static API (`DisplayText`, `Create`, `RenderPixels`, `EncodeIco`) and byte-exact ICO output.
- `MainWindow` constructor signature and `ShowFromTray()`.
- Adapter ID format = `NetworkInterface.Id` (`{GUID}` uppercase, braces) — persisted in settings.json.

### 4.4 Definition of done (every agent)

1. `dotnet build NetworkDownloadTray.sln -c Release` → 0 warnings, 0 errors.
2. `dotnet test NetworkDownloadTray.sln -c Release` → 0 failed; skipped count unchanged (1).
3. Before/after numbers for the agent's own hot path, measured the same way as §2.
4. One focused commit on the worktree branch; message `perf(<area>): ...`.
5. Final report: files changed, measurements, any deviation from the task spec, open risks.

## 5. Task details

### Task A — Native one-call adapter sampling (A1, A2)

**Why:** 34 ms / 98 KB per second spent re-enumerating 36 adapters.

**Design (hybrid, behavior-preserving):**
- **Membership + metadata** (which adapters exist, their `Id`, `Name`, `Description`, `NetworkInterfaceType`)
  come from `NetworkInterface.GetAllNetworkInterfaces()`, **cached**. The cache is invalidated when:
  `NetworkChange.NetworkAddressChanged` fires, or `NetworkChange.NetworkAvailabilityChanged` fires, or it is older
  than 30 s, or a cached adapter's GUID is absent from the current `GetIfTable2` result (adapter removed).
  Invalidation sets a `volatile bool`; re-enumeration happens lazily on the next `Read()` on the sampling thread.
- **Live values** (`OperationalStatus`, `BytesReceived`) come from a single `GetIfTable2` call, joined by
  `Guid` (parse `NetworkInterface.Id` with `Guid.TryParse`; never compare strings).
- `AdapterStatistics.Id` must remain the original `NetworkInterface.Id` string (persisted override key).
- An adapter in the cache without a table row is treated as removed: trigger invalidation and use the managed
  path for this one tick, so the output is still complete and correct.
- **Fallback:** if `GetIfTable2` returns non-zero, log once via `AppLog.Error` and use the existing managed path
  (`GetIPStatistics()` per adapter) — keep that code as `private IReadOnlyList<AdapterStatistics> ReadManaged()`.
- Unsubscribe `NetworkChange` handlers: make the provider `IDisposable` **only if** that does not change the frozen
  interface; otherwise use static handlers with a weak/volatile flag. (Provider lives for the app lifetime.)

**Native interop (`Services/Native/IpHelperApi.cs`, `internal static`):**
- `[DllImport("iphlpapi.dll")] static extern int GetIfTable2(out IntPtr table);`
- `[DllImport("iphlpapi.dll")] static extern void FreeMibTable(IntPtr memory);` — always in `finally`.
- No `unsafe` (csproj is owned by B). Read with `Marshal.ReadInt32/ReadInt64` and `Marshal.PtrToStructure<Guid>`.
- Verified layout (x64 and x86 identical — all fields fixed-size):
  - `MIB_IF_TABLE2`: `NumEntries` (uint) at 0; rows start at **8**; row size **1352**.
  - `MIB_IF_ROW2`: `InterfaceGuid` @ **12**, `Alias` (WCHAR[257]) @ 28, `Description` @ 542,
    `Type` (uint) @ **1128**, `OperStatus` (uint) @ **1156**, `InOctets` (ulong) @ **1208**.
  - `OperStatus` values equal `OperationalStatus` enum values; `Type` values equal `NetworkInterfaceType` values.
- Add `const` offsets with a comment citing `netioapi.h`, plus a `Debug.Assert` that row count × 1352 fits.
- `InOctets` is `ulong`: clamp to `long.MaxValue` when converting.

**Tests (`ProviderTests.cs`):**
- Cross-check test on the real machine: every `NetworkInterface` whose `Id` parses as a Guid has a table row with
  equal Description, Type, OperStatus, and `InOctets` within 10 MB of `GetIPStatistics().BytesReceived`.
- `NetworkStatisticsProvider.Read()` returns the same Id set, in the same order, as `GetAllNetworkInterfaces()`.
- Two consecutive `Read()` calls: second call does **not** re-enumerate (expose an `internal int EnumerationCount`).
- Invalidate → next `Read()` re-enumerates.

**Acceptance:** `Read()` steady-state ≤ 2 ms and ≤ 5 KB allocated per call on this machine (measure 200 iterations
after warm-up with `Stopwatch` + `GC.GetAllocatedBytesForCurrentThread`), all existing tests green.

### Task B — Tray icon path + build configuration (B1–B4)

**B1:** `TrayIconRenderer.Create`: `using var stream = new MemoryStream(EncodeIco(text, size), writable: false); return new Icon(stream, size, size);`
(`System.Drawing.Icon(Stream)` copies the bytes into its own buffer, so disposing the stream is safe.)
`RepeatedIconCreationReleasesNativeResources` must still pass (GDI+USER handle delta within ±10 over 2000 icons).

**B2:** `EncodeIco`: compute the exact length (`22 + 40 + size*size*4 + maskStride*size`), allocate one `byte[]`,
write header fields with `BinaryPrimitives.WriteUInt16LittleEndian/WriteInt32LittleEndian/WriteUInt32LittleEndian`.
Output must be **byte-for-byte identical** to the current implementation. Add a golden test: before changing the
code, capture SHA-256 of the current `EncodeIco` output for every `InlineData` case in `RendererTests` and assert on it.
`RenderPixels`: keep the public signature; internally avoid the intermediate copy when `size == 16`.

**B3:** `TrayIconService.Update`: store `_lastTooltip`; assign `_icon.ToolTipText` only when it differs.
Reset `_lastTooltip = null` in the `catch` (same as `_lastText`), so a failed update is retried.

**B4 (`NetworkDownloadTray.csproj`):** add `<ConcurrentGarbageCollection>false</ConcurrentGarbageCollection>`,
`<SatelliteResourceLanguages>en</SatelliteResourceLanguages>`, `<TieredPGO>true</TieredPGO>`.
`win-x64.pubxml`: `<PublishReadyToRun>true</PublishReadyToRun>`. Do **not** enable trimming or `InvariantGlobalization`.
Measure: publish the portable profile before/after (to a scratch directory, not `outputs/`) and report folder
size. Do **not** launch the real app for measurement — it reads/writes the user's real `%APPDATA%` settings.

**Acceptance:** `Create` 32 px ≤ 800 µs; golden hashes identical; tooltip not re-set when unchanged
(unit-test via a counter is not possible on `TaskbarIcon` — verify by code review + the opt-in native test).

### Task C — Sampling core (C1–C4)

**C1 `AdapterCounterTracker`:** keep `_previous` and `_scratch` dictionaries; copy `counters` into `_scratch`,
compute, then swap references and `Clear()` the old one. `Reset()` clears both. Semantics identical
(including the "new adapter adds no historical bytes" and "elapsed > 10 s → measuring" rules).

**C2 `NetworkSpeedReader`:**
- `_overrides` becomes a `volatile IReadOnlyDictionary<string,bool>` replaced atomically in `ConfigureAdapters`.
- `ConfigureAdapters`/`Reset` must never wait for `_provider.Read()`. Use an `int _resetGeneration` incremented
  by `Reset`/`ConfigureAdapters` (Interlocked); `ReadWithDiagnostics` calls the provider **outside** the lock, then
  takes the lock only to (a) apply a pending reset if the generation changed and (b) run `_tracker.Sample`.
- Keep the `NetworkInformationException` handling and message text exactly.

**C3:** one `TryGetValue` per adapter; reuse the result for both `GetExclusionReason` and `SelectionMode`.
Replace `diagnostics.First(x => x.Included)` with the description captured during the loop.

**C4 `DownloadMonitorService`:** subscribe to `SystemEvents.SessionSwitch` in `Start()`.
On `SessionLock`/`ConsoleDisconnect`/`RemoteDisconnect`: stop the timer and `ResetMeasurements()`.
On `SessionUnlock`/`ConsoleConnect`/`RemoteConnect`: `ResetMeasurements()`, start the timer, `_ = RefreshAsync()`.
Marshal to the dispatcher exactly like `OnPowerModeChanged`. Unsubscribe in `Dispose`. Suspend (`PowerModes.Suspend`)
should also stop the timer; `Resume` restarts it.

**Tests:** extend `AdapterTests`/`ReaderTests`; new `MonitorTests.cs` covering: override change while a slow provider
read is in flight does not block (provider blocks on a `ManualResetEventSlim`; `ConfigureAdapters` returns within
100 ms) and the in-flight result is discarded; tracker allocates 0 bytes per `Sample` after warm-up
(`GC.GetAllocatedBytesForCurrentThread`); lock/unlock handlers stop/start sampling (make handlers `internal` for tests).

**Acceptance:** all tests green; `Sample` 0 B/op; reader overhead excluding provider ≤ 50 µs/op with 36 adapters.

### Task D — UI & lifecycle (D1–D3)

**D1 `App.xaml.cs`:** do not construct `MainWindow` at startup when `StartMinimizedToTray && _tray.IsCreated`.
Introduce `private MainWindow GetOrCreateWindow()` used by the tray "Open" callback and by the startup path
when the window must be shown. `MainWindow = window` assignment happens on creation. The tray's first `Update`
must happen before the decision (as today) so `IsCreated` is accurate. If the tray later fails, nothing changes
(existing behavior). Measure: working set 10 s after launch with a window vs. without — do this in a test harness,
not by editing the real `%APPDATA%` settings (e.g. an `internal` static factory the test calls on an STA thread).

**D2 `AdapterRow.Apply`:** compare old vs new record field-by-field and raise `OnPropertyChanged(nameof(X))`
only for changed properties (`Included`, `Status`, `BytesReceived`, `ExclusionReason`, `SelectionMode`, `Name`,
`Description`, `Type`). New `AdapterRowTests.cs`: only `BytesReceived` changes → exactly one notification.

**D3 `SettingsService`:** `private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };`

**Acceptance:** `WindowResumeHideReopenAndExitSmoke` green unchanged; new tests green; startup working-set delta reported.

## 6. Wave 2–4 integration checklist (lead)

1. Apply branch diffs to the main working tree in order **A → C → B → D** (`git diff <base>..<branch> | git apply -3`).
   No commits on `master` without the user's approval.
2. `dotnet build -c Release` 0/0; `dotnet test -c Release` 0 failed, 1 skipped.
3. Re-run §2 benchmarks against the merged build; record in the results table below.
4. User smoke check on a real desktop: tray digits, tooltip, Open, Exit, lock/unlock resumes updates.
5. Spawn one review agent over `git diff 7dc41c6..HEAD` with focus: interop memory safety (`FreeMibTable` in
   `finally`, bounds), thread-safety of reader generations, event unsubscription, lazy-window null paths.
6. Fix confirmed findings; append a dated entry to `IMPLEMENTATION_HISTORY.md` using its template.

## 7. Risks & rollback

| Risk                                                         | Mitigation                                                         |
|--------------------------------------------------------------|--------------------------------------------------------------------|
| `GetIfTable2` row set/fields differ on other Windows builds  | Membership from `NetworkInterface`; join by Guid; managed fallback |
| Struct offset error → garbage counters                       | Offsets verified 36/36; cross-check test runs on every test pass   |
| Session lock handling leaves the timer stopped               | Unlock/Resume always restart; smoke test; `Start()` idempotent     |
| Lazy window → `MainWindow` null in some path                 | Single `GetOrCreateWindow()`; smoke test covers Open/Exit          |
| Each task is one commit                                      | `git revert <sha>` per area                                        |

## 8. Results (2026-09-23)

All four Wave 1 branches applied cleanly (A → C → B → D, no conflicts). Build 0 warnings / 0 errors.
Tests: **59 passed / 0 failed / 1 skipped** (baseline 33 / 0 / 1).

End-to-end harness, same machine, real adapters, separate processes per build, 3 alternating runs.
CPU time is reported because wall time of the baseline was inflated by concurrent builds.

| Per-second tick (`NetworkSpeedReader.ReadWithDiagnostics`) | Before            | After                     |
|------------------------------------------------------------|-------------------|---------------------------|
| CPU per tick                                               | 15.6–17.6 ms      | **0.86–1.0 ms** (1 noisy run 3.2 ms) |
| Allocated per tick                                         | 111 KB            | **11 KB**                 |

| Icon rebuild on text change (32 px)                        | Before            | After                     |
|------------------------------------------------------------|-------------------|---------------------------|
| CPU                                                        | 0.95–1.34 ms      | **0.45–0.61 ms**          |
| Allocated                                                  | 27.6 KB           | **10.2 KB**               |

Other measured effects (agent reports):
- Tracker `Sample`: 1,224 B → **0 B** per tick; override changes never wait on OS I/O.
- Start-minimized launch skips `MainWindow`: **−26 MB private bytes / −39 MB working set**; trade-off: first
  Open builds the window (~1–2 s cold).
- Self-contained win-x64 publish now succeeds here; ReadyToRun grows it 131.5 → 147.8 MB (+12 %) for faster cold start.
- Portable publish size unchanged (no satellite assemblies to drop).

Wave 3 review findings and resolution:
- **MED (fixed):** stopping the timer on `PowerModes.Suspend` could leave sampling stopped forever, because
  `SystemEvents` does not report `PBT_APMRESUMEAUTOMATIC`. Suspend now only resets the baseline (old behavior);
  lock/unlock pausing is kept.
- **LOW (fixed):** provider marked its adapter cache fresh before enumeration; a throwing enumeration now retries next tick.
- **LOW (accepted):** adapters without a `GetIfTable2` row (none on this machine) report status up to 30 s stale; bytes stay live.
- Possible flakiness noted: `ProviderTests.NativeTableMatchesNetworkInterface` if adapters change mid-test;
  `LockPausesSamplingAndUnlockResumes` depends on 1 s timer timing with generous margins.

Open (needs a real desktop, not done by agents): native tray opt-in test, tooltip after Explorer restart,
lock/unlock and sleep/wake resume, start-minimized first-Open latency.
