using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using PokeyApp.Transport;
using PokeyApp.ViewModels;
using Serilog;

namespace PokeyApp.Tray;

public class TrayIconManager : IDisposable
{
    private readonly TrayViewModel _viewModel;
    private readonly NotifyIcon _notifyIcon;
    private bool _disposed;

    public TrayIconManager(TrayViewModel viewModel)
    {
        _viewModel = viewModel;
        _notifyIcon = new NotifyIcon();

        SetupIcon();
        SetupContextMenu();
        WireViewModelEvents();

        _notifyIcon.Visible = true;
        // TrayViewModel'deki IsConnected/TooltipText değişince ikonu güncelle
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(TrayViewModel.IsConnected) or nameof(TrayViewModel.TooltipText))
                UpdateIcon();
        };
    }

    private void SetupIcon()
    {
        _notifyIcon.Text = "PokeyApp";
        _notifyIcon.Icon = LoadIcon("tray-disconnected.ico");
        _notifyIcon.DoubleClick += (_, _) => _viewModel.ShowWindowCommand.Execute(null);
    }

    private void SetupContextMenu()
    {
        var menu = new ContextMenuStrip();

        var titleItem = new ToolStripMenuItem("PokeyApp") { Enabled = false };
        titleItem.Font = new Font(titleItem.Font, System.Drawing.FontStyle.Bold);

        var separator1 = new ToolStripSeparator();

        var showItem = new ToolStripMenuItem("Pencereyi Göster");
        showItem.Click += (_, _) => _viewModel.ShowWindowCommand.Execute(null);

        var pokeItem = new ToolStripMenuItem("Dürt!");
        pokeItem.Click += async (_, _) =>
        {
            if (_viewModel.PokeFromTrayCommand.CanExecute(null))
                await _viewModel.PokeFromTrayCommand.ExecuteAsync(null);
        };

        var settingsItem = new ToolStripMenuItem("Ayarlar");
        settingsItem.Click += (_, _) => _viewModel.OpenSettingsCommand.Execute(null);

        var separator2 = new ToolStripSeparator();

        var exitItem = new ToolStripMenuItem("Çıkış");
        exitItem.Click += (_, _) => WinApp.Current.Shutdown();

        menu.Items.AddRange(new ToolStripItem[]
        {
            titleItem, separator1, showItem, pokeItem, settingsItem, separator2, exitItem
        });

        _notifyIcon.ContextMenuStrip = menu;
    }

    private void WireViewModelEvents()
    {
        _viewModel.ShowWindowRequested += (_, _) =>
        {
            WinApp.Current.Dispatcher.InvokeAsync(() =>
            {
                if (WinApp.Current.MainWindow is System.Windows.Window w)
                {
                    w.Show();
                    w.Activate();
                }
            });
        };

        _viewModel.OpenSettingsRequested += (_, _) =>
        {
            WinApp.Current.Dispatcher.InvokeAsync(() =>
            {
                if (WinApp.Current.MainWindow is Views.MainWindow mw)
                {
                    mw.Show();
                    mw.Activate();
                }
            });
        };
    }

    private void UpdateIcon()
    {
        var iconFile = _viewModel.IsConnected ? "tray-connected.ico" : "tray-disconnected.ico";
        _notifyIcon.Icon = LoadIcon(iconFile);
        _notifyIcon.Text = _viewModel.TooltipText.Length > 63
            ? _viewModel.TooltipText[..60] + "..."
            : _viewModel.TooltipText;
    }

    private static Icon LoadIcon(string filename)
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", filename);
            if (File.Exists(path))
                return new Icon(path);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Tray icon yüklenemedi: {File}", filename);
        }

        // Fallback: sistem ikonu
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
