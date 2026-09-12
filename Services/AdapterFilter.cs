using System.Net.NetworkInformation;

namespace NetworkDownloadTray.Services;

public static class AdapterFilter
{
    private static readonly string[] Markers = ["Virtual", "WAN Miniport", "WFP", "Filter",
        "QoS Packet Scheduler", "Network Monitor", "Wi-Fi Direct", "Loopback", "Bluetooth",
        "Kernel Debug", "VMware", "VirtualBox", "Hyper-V", "TAP-Windows", "WireGuard"];

    public static string GetExclusionReason(string name, string description, NetworkInterfaceType type,
        OperationalStatus status, bool? userOverride = null)
    {
        if (status != OperationalStatus.Up) return "Status is not Up";
        if (userOverride.HasValue) return userOverride.Value ? "" : "Excluded by user";
        if (type is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.FastEthernetT
            or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.Wireless80211))
            return "Interface type excluded";
        string identity = name + " " + description;
        return Markers.Any(x => identity.Contains(x, StringComparison.OrdinalIgnoreCase))
            ? "Virtual/filter interface (heuristic)" : "";
    }
}
