using System.Net.NetworkInformation;

namespace NetworkDownloadTray.Services;

public sealed record AdapterStatistics(string Id, string Name, string Description,
    NetworkInterfaceType Type, OperationalStatus Status, long? BytesReceived);

public interface INetworkStatisticsProvider
{
    IReadOnlyList<AdapterStatistics> Read();
}

public sealed class NetworkStatisticsProvider : INetworkStatisticsProvider
{
    public IReadOnlyList<AdapterStatistics> Read()
    {
        var result = new List<AdapterStatistics>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            long? bytes = null;
            try { bytes = adapter.GetIPStatistics().BytesReceived; }
            catch (NetworkInformationException) { }
            result.Add(new(adapter.Id, adapter.Name, adapter.Description,
                adapter.NetworkInterfaceType, adapter.OperationalStatus, bytes));
        }
        return result;
    }
}
