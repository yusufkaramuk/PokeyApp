using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeyApp.Infrastructure;
using PokeyApp.Messages;
using PokeyApp.Services;
using PokeyApp.Transport;

namespace PokeyApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigurationService _config;
    private readonly ITransport _transport;
    private readonly IDiscoveryService _discovery;

    [ObservableProperty]
    private string _localUsername = string.Empty;

    [ObservableProperty]
    private string _peerIpAddress = string.Empty;

    [ObservableProperty]
    private int _tcpPort = 14191;

    [ObservableProperty]
    private bool _soundEnabled = true;

    [ObservableProperty]
    private PeerInfo? _selectedDiscoveredPeer;

    public ObservableCollection<PeerInfo> DiscoveredPeers { get; } = new();

    public event EventHandler? SettingsSaved;

    public SettingsViewModel(
        IConfigurationService config,
        ITransport transport,
        IDiscoveryService discovery)
    {
        _config = config;
        _transport = transport;
        _discovery = discovery;

        _discovery.PeerDiscovered += OnPeerDiscovered;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _config.Load();
        LocalUsername = settings.LocalUsername;
        PeerIpAddress = settings.PeerIpAddress;
        TcpPort = settings.TcpPort;
        SoundEnabled = settings.SoundEnabled;
    }

    private void OnPeerDiscovered(object? sender, PeerInfo peer)
    {
        // UI thread'inde güncelle
        WinApp.Current.Dispatcher.InvokeAsync(() =>
        {
            if (!DiscoveredPeers.Any(p => p.IpAddress == peer.IpAddress))
                DiscoveredPeers.Add(peer);
        });
    }

    partial void OnSelectedDiscoveredPeerChanged(PeerInfo? value)
    {
        if (value is null) return;
        PeerIpAddress = value.IpAddress;
        TcpPort = value.TcpPort;
    }

    [RelayCommand]
    private void Save()
    {
        var current = _config.Load();
        var updated = current with
        {
            LocalUsername = LocalUsername.Trim(),
            PeerIpAddress = PeerIpAddress.Trim(),
            TcpPort = TcpPort,
            SoundEnabled = SoundEnabled
        };

        _config.Save(updated);

        if (!string.IsNullOrEmpty(PeerIpAddress))
            _transport.SetPeer(PeerIpAddress, TcpPort);

        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        LoadSettings();
    }
}
