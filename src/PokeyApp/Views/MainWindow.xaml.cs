using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using PokeyApp.ViewModels;

namespace PokeyApp.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionBottomRight();
    }

    private void PositionBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 12;
        Top = workArea.Bottom - Height - 12;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            WindowState = WindowState.Normal;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsVm = App.Services.GetRequiredService<SettingsViewModel>();
        var settingsWindow = new SettingsWindow(settingsVm);
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
        _viewModel.RefreshSettings();
    }

    private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        WinApp.Current.Shutdown();
    }
}
