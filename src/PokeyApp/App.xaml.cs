using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PokeyApp.Infrastructure;
using PokeyApp.Services;
using PokeyApp.Transport;
using PokeyApp.ViewModels;
using PokeyApp.Views;
using Serilog;

namespace PokeyApp;

public partial class App : WinApp
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Hata yakalama — debug amacıyla masaüstüne log yazar
        var traceLog = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "pokey_trace.txt");

        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
            File.AppendAllText(traceLog, $"[AppDomain] {ev.ExceptionObject}\n");
        DispatcherUnhandledException += (_, ev) =>
        {
            File.AppendAllText(traceLog, $"[Dispatcher] {ev.Exception}\n");
            ev.Handled = true;
        };

        try
        {
            File.AppendAllText(traceLog, $"\n--- {DateTime.Now} ---\n");

            // Serilog
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PokeyApp", "logs");
            Directory.CreateDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(logDir, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7)
                .CreateLogger();

            Log.Information("PokeyApp başlatılıyor");

            // DI container
            _host = CreateHost();
            Services = _host.Services;

            // Background servisleri başlat (sync — async void kullanmıyoruz)
            _host.StartAsync().GetAwaiter().GetResult();

            // Peer ayarı varsa bağlan
            var config = Services.GetRequiredService<IConfigurationService>();
            var settings = config.Load();
            var transport = Services.GetRequiredService<ITransport>();

            if (!string.IsNullOrEmpty(settings.PeerIpAddress))
                transport.SetPeer(settings.PeerIpAddress, settings.TcpPort);

            // Poke alındığında bildirim + ses
            WirePokeEvents();

            // Ana pencereyi aç
            var mainWindow = Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;

            if (!settings.StartMinimized)
                mainWindow.Show();

            File.AppendAllText(traceLog, "Startup OK\n");
            Log.Information("PokeyApp başlatıldı");
        }
        catch (Exception ex)
        {
            File.AppendAllText(traceLog, $"STARTUP EXCEPTION: {ex}\n");
            Log.Fatal(ex, "Başlatma hatası");
            System.Windows.MessageBox.Show(
                $"PokeyApp başlatılamadı:\n\n{ex.Message}",
                "Hata",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("PokeyApp kapatılıyor");

        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void WirePokeEvents()
    {
        var pokeService = Services.GetRequiredService<IPokeService>();
        var notificationService = Services.GetRequiredService<INotificationService>();
        var audioService = Services.GetRequiredService<IAudioService>();
        var config = Services.GetRequiredService<IConfigurationService>();

        pokeService.PokeReceived += (_, msg) =>
        {
            notificationService.ShowPokeNotification(msg.FromUsername);

            var settings = config.Load();
            if (settings.SoundEnabled)
                audioService.PlayPokeSound();
        };
    }

    private static IHost CreateHost() => Host.CreateDefaultBuilder()
        .UseSerilog()
        .ConfigureServices(services =>
        {
            // Infrastructure
            services.AddSingleton<IConfigurationService, ConfigurationService>();

            // Transport
            services.AddSingleton<ITransport>(sp =>
            {
                var config = sp.GetRequiredService<IConfigurationService>();
                var settings = config.Load();
                return new TcpTransport(settings.TcpPort);
            });

            // Services
            services.AddSingleton<IPokeService, PokeService>();
            services.AddSingleton<IConnectionService, ConnectionService>();
            services.AddSingleton<IAudioService, AudioService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<IDiscoveryService, DiscoveryService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddSingleton<TrayViewModel>();

            // Views
            services.AddSingleton<MainWindow>();

            // Hosted services (arka plan)
            services.AddHostedService(sp => (TcpTransport)sp.GetRequiredService<ITransport>());
            services.AddHostedService(sp => (DiscoveryService)sp.GetRequiredService<IDiscoveryService>());
        })
        .Build();
}
