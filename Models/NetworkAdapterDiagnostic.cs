using System.Net.NetworkInformation;

namespace NetworkDownloadTray.Models;

public sealed record NetworkAdapterDiagnostic(string Name, string Description, NetworkInterfaceType Type, OperationalStatus Status, bool Included, long? BytesReceived, string ExclusionReason)
{
    public string DisplayText => $"{(Included ? "[INCLUDE]" : "[EXCLUDE]")}  {Name} | Type: {Type} | Status: {Status}\n    {Description} | BytesReceived: {BytesReceived?.ToString() ?? "N/A"}" + (string.IsNullOrEmpty(ExclusionReason) ? string.Empty : $" | Reason: {ExclusionReason}");
}
