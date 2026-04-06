using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using PokeyApp.Native;
using PokeyApp.ViewModels;

namespace PokeyApp.Views;

public partial class NotificationWindow : Window
{
    private readonly NotificationViewModel _viewModel;
    private readonly int _durationSeconds;

    public NotificationWindow(NotificationViewModel viewModel, int durationSeconds = 4)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _durationSeconds = durationSeconds;
        DataContext = viewModel;

        viewModel.DismissRequested += (_, _) => AnimateAndClose();

        Loaded += OnLoaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Focus çalmayı engelle
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        ShowWithoutActivating();
        StartFadeIn();
        StartAutoDismissTimer();
    }

    private void PositionWindow(int stackOffset = 0)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 12;
        Top = workArea.Bottom - ActualHeight - 12 - stackOffset;
    }

    /// <summary>Offset ile konumlandırma — stacked bildirimler için.</summary>
    public void SetStackOffset(double offset)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 12;
        Top = workArea.Bottom - ActualHeight - 12 - offset;
    }

    private void ShowWithoutActivating()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);

        // Topmost, focus vermeden
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOACTIVATE);
    }

    private void StartFadeIn()
    {
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        BeginAnimation(OpacityProperty, anim);
    }

    private void StartAutoDismissTimer()
    {
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_durationSeconds)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            AnimateAndClose();
        };
        timer.Start();
    }

    private void AnimateAndClose()
    {
        var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
        anim.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, anim);
    }
}
