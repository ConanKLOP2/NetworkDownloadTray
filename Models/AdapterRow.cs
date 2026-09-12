using CommunityToolkit.Mvvm.ComponentModel;

namespace NetworkDownloadTray.Models;

public sealed class AdapterRow : ObservableObject
{
    private NetworkAdapterDiagnostic _data;
    public AdapterRow(NetworkAdapterDiagnostic data) => _data = data;
    public string Id => _data.Id;
    public string Name => _data.Name;
    public string Description => _data.Description;
    public string Type => _data.Type.ToString();
    public string Status => _data.Status.ToString();
    public bool Included => _data.Included;
    public long? BytesReceived => _data.BytesReceived;
    public string ExclusionReason => _data.ExclusionReason;
    public string SelectionMode => _data.SelectionMode;
    public void Apply(NetworkAdapterDiagnostic data)
    {
        if (_data == data) return;
        _data = data;
        OnPropertyChanged(string.Empty);
    }
}
