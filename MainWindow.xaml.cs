using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using NetworkDownloadTray.Models;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray;

public partial class MainWindow : Window
{
    private readonly DownloadMonitorService _monitor;
    private readonly ITrayIconService _tray;
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly WindowsStartupService _startup;
    private readonly ObservableCollection<AdapterRow> _rows = new();
    private readonly ICollectionView _view;
    private DateTime _lastRefresh = DateTime.MinValue;
    private bool _loading = true;

    public MainWindow(DownloadMonitorService monitor, AppSettings settings, SettingsService settingsService,
        ITrayIconService tray, WindowsStartupService startup)
    {
        _monitor = monitor; _settings = settings; _settingsService = settingsService;
        _tray = tray; _startup = startup;
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = item => item is AdapterRow row && (AdapterFilterBox.SelectedIndex switch
        { 1 => row.Included, 2 => !row.Included, _ => true });
        AdapterGrid.ItemsSource = _view;
        AutoStartCheckBox.IsChecked = settings.AutoStartWithWindows;
        StartMinimizedCheckBox.IsChecked = settings.StartMinimizedToTray;
        DiagnosticsCheckBox.IsChecked = settings.ShowDiagnostics;
        DiagnosticsPanel.Visibility = settings.ShowDiagnostics ? Visibility.Visible : Visibility.Collapsed;
        var menu = new ContextMenu();
        foreach (var choice in new[] { ("Include", (bool?)true), ("Exclude", (bool?)false), ("Automatic", (bool?)null) })
        {
            var item = new MenuItem { Header = choice.Item1 };
            item.Click += (_, _) => SetAdapterOverride(choice.Item2);
            menu.Items.Add(item);
        }
        AdapterGrid.ContextMenu = menu;
        AdapterGrid.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(AdapterGrid, source) is DataGridRow row)
                AdapterGrid.SelectedItem = row.Item;
        };
        _monitor.SpeedUpdated += MonitorSpeedUpdated;
        _monitor.DiagnosticsUpdated += MonitorDiagnosticsUpdated;
        Loaded += (_, _) => RefreshVisibleState();
        IsVisibleChanged += (_, _) => { if (IsVisible) RefreshVisibleState(); };
        _loading = false;
        UpdateStartupStatus();
    }

    private void RefreshVisibleState()
    {
        UpdateTrayStatus();
        _lastRefresh = DateTime.MinValue;
        if (_monitor.Latest is { } latest)
        {
            MonitorSpeedUpdated(this, latest.Snapshot);
            MonitorDiagnosticsUpdated(this, latest.Diagnostics);
        }
    }

    private void MonitorDiagnosticsUpdated(object? sender, IReadOnlyList<NetworkAdapterDiagnostic> diagnostics)
    {
        if (!IsVisible || !_settings.ShowDiagnostics || AdapterGrid.ContextMenu?.IsOpen == true ||
            DateTime.UtcNow - _lastRefresh < TimeSpan.FromSeconds(5)) return;
        _lastRefresh = DateTime.UtcNow;
        var existing = _rows.ToDictionary(x => x.Id);
        var ids = diagnostics.Select(x => x.Id).ToHashSet();
        foreach (var row in _rows.Where(x => !ids.Contains(x.Id)).ToArray()) _rows.Remove(row);
        bool membershipChanged = false;
        foreach (var data in diagnostics)
        {
            if (existing.TryGetValue(data.Id, out var row))
            {
                membershipChanged |= row.Included != data.Included;
                row.Apply(data);
            }
            else _rows.Add(new(data));
        }
        if (membershipChanged) _view.Refresh();
        AdapterCountText.Text = $"{diagnostics.Count(x => x.Included)} included / {diagnostics.Count} adapters";
    }

    private bool SaveSettings()
    {
        try { _settingsService.Save(_settings); return true; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            AppLog.Error("Save settings", ex);
            MessageBox.Show(this, ex.Message, "Could not save settings");
            return false;
        }
    }

    private void SetAdapterOverride(bool? included)
    {
        if (AdapterGrid.SelectedItem is not AdapterRow row) return;
        bool hadOverride = _settings.AdapterOverrides.TryGetValue(row.Id, out bool previous);
        if (included.HasValue) _settings.AdapterOverrides[row.Id] = included.Value;
        else _settings.AdapterOverrides.Remove(row.Id);
        if (!SaveSettings())
        {
            if (hadOverride) _settings.AdapterOverrides[row.Id] = previous;
            else _settings.AdapterOverrides.Remove(row.Id);
            return;
        }
        _monitor.ConfigureAdapters(_settings.AdapterOverrides);
        _lastRefresh = DateTime.MinValue;
        _ = _monitor.RefreshAsync();
    }

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        try
        {
            _startup.SetEnabled(AutoStartCheckBox.IsChecked == true);
            _settings.AutoStartWithWindows = _startup.IsEnabled();
            SaveSettings();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show(this, ex.Message, "Could not change Windows startup");
            _loading = true;
            AutoStartCheckBox.IsChecked = _settings.AutoStartWithWindows;
            _loading = false;
        }
        UpdateStartupStatus();
    }

    private void UpdateStartupStatus()
    {
        try { StartupPathText.Text = _startup.RegisteredCommand ?? "Not registered for Windows sign-in"; }
        catch (Exception ex) { StartupPathText.Text = ex.Message; }
    }

    private void Minimized_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool previous = _settings.StartMinimizedToTray;
        _settings.StartMinimizedToTray = StartMinimizedCheckBox.IsChecked == true;
        if (!SaveSettings())
        {
            _settings.StartMinimizedToTray = previous;
            _loading = true; StartMinimizedCheckBox.IsChecked = previous; _loading = false;
        }
    }

    private void Diagnostics_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool previous = _settings.ShowDiagnostics;
        _settings.ShowDiagnostics = DiagnosticsCheckBox.IsChecked == true;
        if (!SaveSettings())
        {
            _settings.ShowDiagnostics = previous;
            _loading = true; DiagnosticsCheckBox.IsChecked = previous; _loading = false;
        }
        DiagnosticsPanel.Visibility = _settings.ShowDiagnostics ? Visibility.Visible : Visibility.Collapsed;
        RefreshVisibleState();
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) _view.Refresh();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _lastRefresh = DateTime.MinValue;
        await _monitor.RefreshAsync();
        RefreshVisibleState();
    }

    private void CopyAdapter_Click(object sender, RoutedEventArgs e)
    {
        if (AdapterGrid.SelectedItem is not AdapterRow row) return;
        try { Clipboard.SetText($"{row.Id}\n{row.Name}\n{row.Description}\n{row.Type} / {row.Status}\nMode: {row.SelectionMode}\n{row.ExclusionReason}"); }
        catch (System.Runtime.InteropServices.COMException ex) { MessageBox.Show(this, ex.Message, "Clipboard busy"); }
    }

    private void MonitorSpeedUpdated(object? sender, DownloadSpeedSnapshot snapshot)
    {
        if (!IsVisible) return;
        SpeedText.Text = snapshot.IsMeasuring ? "Measuring..." : snapshot.IsAvailable
            ? $"{DownloadSpeedCalculator.FormatMegabits(snapshot.MegabitsPerSecond)} Mbps" : "Unavailable";
        AdapterText.Text = snapshot.AdapterDescription;
        LiveText.Text = snapshot.IsAvailable ? "LIVE" : "OFFLINE";
        UpdateTrayStatus();
    }

    private void UpdateTrayStatus() => TrayStatusText.Text = _tray.IsCreated
        ? $"Tray ready · {_tray.PixelSize}px" : _tray.Error ?? "Waiting for tray";

    private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
    {
        if (_tray.IsCreated) Hide();
        else MessageBox.Show(this, "Tray is unavailable. The window will stay open so you can control the app.");
    }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Focus();
        RefreshVisibleState();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app) app.ShutdownApplication();
        else Application.Current?.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (Application.Current is App app && app.IsExiting) { base.OnClosing(e); return; }
        e.Cancel = true;
        if (_tray.IsCreated) Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _monitor.SpeedUpdated -= MonitorSpeedUpdated;
        _monitor.DiagnosticsUpdated -= MonitorDiagnosticsUpdated;
        base.OnClosed(e);
    }
}
