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
        var old = _data;
        if (old == data) return;
        _data = data;
        if (old.Included != data.Included) OnPropertyChanged(nameof(Included));
        if (old.Status != data.Status) OnPropertyChanged(nameof(Status));
        if (old.BytesReceived != data.BytesReceived) OnPropertyChanged(nameof(BytesReceived));
        if (old.ExclusionReason != data.ExclusionReason) OnPropertyChanged(nameof(ExclusionReason));
        if (old.SelectionMode != data.SelectionMode) OnPropertyChanged(nameof(SelectionMode));
        if (old.Name != data.Name) OnPropertyChanged(nameof(Name));
        if (old.Description != data.Description) OnPropertyChanged(nameof(Description));
        if (old.Type != data.Type) OnPropertyChanged(nameof(Type));
    }
}
