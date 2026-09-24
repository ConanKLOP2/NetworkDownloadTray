# AGENTS.md — NetworkDownloadTray

Instructions for any coding agent working in this repo. `README.md` (Vietnamese) is the user-facing
description; this file covers what an agent needs to change the code safely.

## Project

Windows-only WPF tray app (.NET 10, `net10.0-windows`) that shows **download Mbps** as pixel digits in
the system tray, sampled once per second. Single project + one xUnit test project.

- Dependencies: `H.NotifyIcon.Wpf` 2.4.1 (tray), `CommunityToolkit.Mvvm` (`ObservableObject`).
- Communication: the user writes English or Vietnamese — reply in the language they used.
  Project docs (`README.md`, `PLAN.md`, `IMPLEMENTATION_HISTORY.md`, `RELEASE_CHECKLIST.md`) are Vietnamese; keep them so.

## Commands

```powershell
dotnet build NetworkDownloadTray.sln -c Release          # must be 0 warnings, 0 errors
dotnet test  NetworkDownloadTray.sln -c Release          # expect 0 failed, exactly 1 skipped (NativeTrayTests)
dotnet test  NetworkDownloadTray.sln -c Release --filter "FullyQualifiedName~RendererTests"
```

- Native tray test needs an interactive desktop with Explorer: set `NDT_DESKTOP_TESTS=1`, run, then unset.
- Publish (never trim, never `InvariantGlobalization`):
  - `-p:PublishProfile=portable` → framework-dependent, `outputs\portable` (copy the whole folder).
  - `-p:PublishProfile=win-x64` → self-contained single file + ReadyToRun, `outputs\win-x64`.
  - To publish elsewhere add `"-p:PublishDir=<dir>\"`. If `NetworkDownloadTray.exe` is running from that
    folder, its files are locked (single-instance app): stop the process first, relaunch it afterwards, and say so.

## Architecture (data flow)

```
NetworkStatisticsProvider  (cached NetworkInterface list + one GetIfTable2 per tick; managed fallback)
  → NetworkSpeedReader     (AdapterFilter + user overrides → included counters; lock-free override swap)
  → AdapterCounterTracker  (delta → Mbps; allocation-free double buffer)
  → DownloadMonitorService (1 s DispatcherTimer, Task.Run sampling, results raised on the UI dispatcher)
  → App.xaml.cs            (wires TrayIconService + lazily created MainWindow)
```

- `Services/Native/IpHelperApi.cs` — `GetIfTable2`/`FreeMibTable` interop, `MIB_IF_ROW2` offsets.
- `Services/TrayIconRenderer.cs` — pixel glyphs → 32bpp ICO bytes in memory; `TrayIconService` owns the `TaskbarIcon`.
- `SettingsService` (`%AppData%\NetworkDownloadTray\settings.json`, atomic temp-file replace),
  `WindowsStartupService` + `IStartupStore` (HKCU Run), `AppLog` (`%LocalAppData%\NetworkDownloadTray\app.log`).

## Invariants — do not break

- **Adapter ID = `NetworkInterface.Id`** (`{GUID}` string). It is the persisted key of user overrides.
  Join native rows by parsed `Guid`, never by string compare.
- **Native interop:** `MIB_IF_TABLE2` rows start at offset 8, stride 1352; `MIB_IF_ROW2` InterfaceGuid @12,
  Type @1128, OperStatus @1156, InOctets @1208. Always `FreeMibTable` in `finally`. No `unsafe`
  (`AllowUnsafeBlocks` is off) — use `Marshal.Read*`, following the existing `DllImport` style.
- **Tray icon ownership:** assigning `TaskbarIcon.Icon` hands the `Icon` to H.NotifyIcon, which disposes the
  previous one. Never dispose the icon currently assigned. Never use `Bitmap.GetHicon` or write temp ICO files.
- **Icon content:** max 4 characters, clamp at 9999, 1–3 digits use 5×7 glyphs and 4 digits use 3×7;
  `...` = measuring, `-` = unavailable. No Gbps, no upload. Tooltip/window show the full value.
- **ICO output is golden-hashed** (`RendererTests.EncodedIcoMatchesGoldenHash`). Encoder refactors must keep
  bytes identical. Intentional glyph changes: re-capture only the affected hashes and say why in the comment.
- **Sampling:** interval stays 1 s. Session lock/disconnect pauses the timer; unlock resumes with a reset.
  **Suspend must only reset, never stop the timer** — `SystemEvents` does not report `PBT_APMRESUMEAUTOMATIC`,
  so a stopped timer might never restart. After any reset/override change the in-flight read is discarded
  (no speed spike, one "Measuring" tick is expected).
- **Threading:** UI objects are touched only on the dispatcher. `ConfigureAdapters`/`Reset` run on the UI thread
  and must never wait on OS I/O. `SystemEvents` fire on their own thread — marshal via the dispatcher.
- **Lifecycle:** `ShutdownMode=OnExplicitShutdown`; the X button hides to tray, Exit shuts down.
  `MainWindow` may be `null` until first Open (start-minimized path) — always go through `GetOrCreateWindow()`.
- Frozen public surfaces unless the task says otherwise: `INetworkStatisticsProvider`, `ITrayIconService`,
  `MainWindow` ctor + `ShowFromTray()`, `DownloadMonitorService` events.

## Safety rules for agents

- **Do not launch the real app** to test or benchmark changes: it reads/writes the user's real
  `%AppData%` settings and can touch HKCU Run. Launch it only when the user asks (e.g. after a publish).
- Tests must use a temp-path `SettingsService` and a fake `IStartupStore` (see `DesktopIntegrationTests`).
- Put benchmark harnesses and scratch projects outside the repo. Measure CPU time and allocations
  (`GC.GetAllocatedBytesForCurrentThread`) against a baseline build; wall time is noisy under parallel builds.

## Git workflow

- **Never commit directly to `master` or `main`**, and never push to them, even when asked to "commit this".
  First create a branch from the up-to-date default branch, then commit there:
  `feat/<topic>`, `fix/<topic>`, `perf/<topic>`, `docs/<topic>`, `test/<topic>` (short kebab-case).
- Changes reach `master` only through a pull request to `origin` (GitHub `ConanKLOP2/NetworkDownloadTray`)
  that the user merges. Do not merge, rebase onto, or fast-forward `master` locally on the user's behalf.
- Commit only when the user asks; push the branch / open the PR only when the user asks.
- If you find yourself on `master` with uncommitted changes, create the branch before committing
  (`git switch -c <branch>` keeps the working-tree changes).
- Never force-push, rewrite published history, or skip hooks unless the user explicitly asks.

## Tests — gotchas

- xUnit parallelization is disabled assembly-wide (`TestConfiguration.cs`).
- WPF allows **one `Application` per process**: add new window/UI assertions to the existing
  `WindowResumeHideReopenAndExitSmoke` flow instead of a second test that creates an `Application`.
- Dispatcher-based tests run on a dedicated STA thread (see the `RunOnDispatcher` helper in `MonitorTests`).
- The main csproj excludes `tests\**`; test files belong only to `tests/NetworkDownloadTray.Tests`.
- `ProviderTests` cross-check against real adapters and must pass on machines with zero adapters.

## Performance budget (Release, ~36 adapters)

| Path | Budget | Measured 2026-09-23 |
|---|---|---|
| Per-second tick (`ReadWithDiagnostics`) | ≤ 2 ms CPU, ≤ 15 KB alloc | ~0.9–1.0 ms, 11 KB |
| `AdapterCounterTracker.Sample` | 0 B alloc | 0 B |
| Icon rebuild 32 px (on text change) | ≤ 0.8 ms | 0.45–0.61 ms |

Rejected on purpose (don't re-propose without new data): caching `TrayIconSize.Read()` (2 µs),
`InvariantGlobalization`, H.NotifyIcon EfficiencyMode, changing the 1 s interval. Details: `OPTIMIZATION_PLAN.md`.

## Code style

Terse C#: file-scoped namespaces, `sealed` classes, records for immutable data, few comments (only for
non-obvious *why*), nullable enabled, 0 warnings. Match surrounding code; files use CRLF line endings.

## Docs to update

- `IMPLEMENTATION_HISTORY.md`: after a substantive step, append a dated entry using its template (Vietnamese).
- `OPTIMIZATION_PLAN.md` / `PLAN.md`: plans and open release gates. `RELEASE_CHECKLIST.md`: manual desktop checks.

## Multi-agent work

Split tasks by **file ownership** (each agent edits only the files it owns), freeze shared interfaces up
front, give each agent its own git worktree, then apply the branches' diffs in dependency order, run the full
build + tests, and have an independent reviewer check the combined diff. `OPTIMIZATION_PLAN.md` §4 is the template.
