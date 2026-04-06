using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeyApp.Services;
using PokeyApp.Transport;

namespace PokeyApp.ViewModels;

public partial class TrayViewModel : ObservableObject
{
    private readonly IPokeService _pokeService;
    private readonly IConnectionService _connectionService;

    [ObservableProperty]
    private string _tooltipText = "PokeyApp - Bağlantı yok";

    [ObservableProperty]
    private bool _isConnected;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? OpenSettingsRequested;

    public TrayViewModel(IPokeService pokeService, IConnectionService connectionService)
    {
        _pokeService = pokeService;
        _connectionService = connectionService;

        _connectionService.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, TransportState state)
    {
        IsConnected = state == TransportState.Connected;
        TooltipText = state switch
        {
            TransportState.Connected => "PokeyApp - Bağlı",
            TransportState.Connecting => "PokeyApp - Bağlanıyor...",
            TransportState.Reconnecting => "PokeyApp - Yeniden bağlanıyor...",
            _ => "PokeyApp - Bağlantı yok"
        };
    }

    [RelayCommand]
    private void ShowWindow() => ShowWindowRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenSettings() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task PokeFromTray()
    {
        try { await _pokeService.SendPokeAsync(); }
        catch { /* sessiz hata */ }
    }

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}
