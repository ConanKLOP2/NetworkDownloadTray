namespace NetworkDownloadTray.Models;

public sealed record NetworkReadResult(
    DownloadSpeedSnapshot Snapshot,
    IReadOnlyList<NetworkAdapterDiagnostic> Diagnostics);
