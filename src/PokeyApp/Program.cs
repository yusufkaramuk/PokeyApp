using System;
using System.IO;
using System.Windows;

namespace PokeyApp;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var traceLog = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "pokey_trace.txt");

        try
        {
            File.AppendAllText(traceLog, $"\n=== PROGRAM START {DateTime.Now} ===\n");

            var app = new App();
            File.AppendAllText(traceLog, "App instance created\n");

            app.InitializeComponent();
            File.AppendAllText(traceLog, "InitializeComponent done\n");

            app.Run();
            File.AppendAllText(traceLog, "App.Run() completed\n");
        }
        catch (Exception ex)
        {
            File.AppendAllText(traceLog, $"FATAL: {ex}\n");
            System.Windows.MessageBox.Show(
                $"PokeyApp başlatılamadı:\n\n{ex}",
                "Kritik Hata",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
