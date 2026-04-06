using System.Windows;
using PokeyApp.ViewModels;

namespace PokeyApp.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.SettingsSaved += (_, _) => Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Command üzerinden zaten handle ediliyor
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
