using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeyApp.Infrastructure;
using PokeyApp.Services;
using PokeyApp.Transport;
using Serilog;

namespace PokeyApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPokeService _pokeService;
    private readonly IConnectionService _connectionService;
    private readonly IConfigurationService _config;

    [ObservableProperty]
    private string _peerDisplayName = "Bağlantı yok";

    [ObservableProperty]
    private string _connectionStatusText = "Bağlanıyor...";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PokeCommand))]
    private TransportState _connectionState = TransportState.Disconnected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PokeCommand))]
    private bool _isPoking;

    [ObservableProperty]
    private string _localUsername = Environment.MachineName;

    public MainViewModel(
        IPokeService pokeService,
        IConnectionService connectionService,
        IConfigurationService config)
    {
        _pokeService = pokeService;
        _connectionService = connectionService;
        _config = config;

        _connectionService.StateChanged += OnConnectionStateChanged;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _config.Load();
        LocalUsername = settings.LocalUsername;
        PeerDisplayName = string.IsNullOrEmpty(settings.PeerIpAddress)
            ? "Peer ayarlanmadı"
            : settings.PeerIpAddress;
    }

    private void OnConnectionStateChanged(object? sender, TransportState state)
    {
        WinApp.Current.Dispatcher.InvokeAsync(() =>
        {
            ConnectionState = state;
            ConnectionStatusText = state switch
            {
                TransportState.Connected => "Bağlı",
                TransportState.Connecting => "Bağlanıyor...",
                TransportState.Reconnecting => "Yeniden bağlanıyor...",
                TransportState.Disconnected => "Bağlantı yok",
                _ => "Bilinmiyor"
            };
        });
    }

    [RelayCommand(CanExecute = nameof(CanPoke))]
    private async Task PokeAsync()
    {
        IsPoking = true;
        try
        {
            await _pokeService.SendPokeAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Dürt gönderilemedi");
        }
        finally
        {
            // Kısa süre sonra butonu tekrar etkinleştir
            await Task.Delay(1000);
            IsPoking = false;
        }
    }

    private bool CanPoke() =>
        !IsPoking && ConnectionState == TransportState.Connected;

    public void RefreshSettings()
    {
        LoadSettings();
    }
}
