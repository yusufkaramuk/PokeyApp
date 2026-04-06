using System.IO;
using System.Text.Json;
using Serilog;

namespace PokeyApp.Infrastructure;

public interface IConfigurationService
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public class ConfigurationService : IConfigurationService
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PokeyApp");

    private static readonly string SettingsPath =
        Path.Combine(AppDataDir, "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Settings dosyası okunamadı, varsayılan ayarlar kullanılıyor");
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Settings dosyası kaydedilemedi");
        }
    }
}
