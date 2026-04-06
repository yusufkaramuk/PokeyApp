using PokeyApp.Infrastructure;
using PokeyApp.ViewModels;
using PokeyApp.Views;
using Serilog;

namespace PokeyApp.Services;

public interface INotificationService
{
    void ShowPokeNotification(string fromPeer);
}

public class NotificationService : INotificationService
{
    private readonly IConfigurationService _config;
    private readonly List<NotificationWindow> _activeWindows = new();
    private readonly object _lock = new();

    private const double WindowHeight = 70;
    private const double WindowMargin = 8;

    public NotificationService(IConfigurationService config)
    {
        _config = config;
    }

    public void ShowPokeNotification(string fromPeer)
    {
        // Mutlaka UI thread'inde çalıştır
        WinApp.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var settings = _config.Load();
                var vm = new NotificationViewModel(fromPeer);
                var window = new NotificationWindow(vm, settings.NotificationDurationSeconds);

                lock (_lock)
                {
                    _activeWindows.Add(window);
                    window.Closed += (_, _) => RemoveWindow(window);
                }

                window.Show();
                RepositionWindows();

                Log.Debug("Bildirim gösterildi: {Peer}", fromPeer);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Bildirim gösterilemedi");
            }
        });
    }

    private void RemoveWindow(NotificationWindow window)
    {
        lock (_lock)
        {
            _activeWindows.Remove(window);
        }
        WinApp.Current.Dispatcher.InvokeAsync(RepositionWindows);
    }

    private void RepositionWindows()
    {
        lock (_lock)
        {
            for (int i = 0; i < _activeWindows.Count; i++)
            {
                double offset = i * (WindowHeight + WindowMargin);
                _activeWindows[i].SetStackOffset(offset);
            }
        }
    }
}
