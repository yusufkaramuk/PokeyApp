using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PokeyApp.ViewModels;

public partial class NotificationViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = string.Empty;

    public event EventHandler? DismissRequested;

    public NotificationViewModel(string fromPeer)
    {
        Message = $"{fromPeer} seni dürtükledi!";
    }

    [RelayCommand]
    private void Dismiss()
    {
        DismissRequested?.Invoke(this, EventArgs.Empty);
    }
}
