using System.IO;
using System.Media;
using System.Reflection;
using Serilog;

namespace PokeyApp.Services;

public interface IAudioService
{
    void PlayPokeSound();
}

public class AudioService : IAudioService
{
    private SoundPlayer? _player;

    public AudioService()
    {
        LoadSound();
    }

    private void LoadSound()
    {
        try
        {
            // Önce embed edilmiş resource'u dene
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("poke.wav", StringComparison.OrdinalIgnoreCase));

            if (resourceName is not null)
            {
                var stream = assembly.GetManifestResourceStream(resourceName)!;
                _player = new SoundPlayer(stream);
                _player.Load();
                Log.Debug("Ses dosyası embedded resource'dan yüklendi");
                return;
            }

            // Fallback: yanındaki dosyadan yükle
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "poke.wav");
            if (File.Exists(path))
            {
                _player = new SoundPlayer(path);
                _player.Load();
                Log.Debug("Ses dosyası diskten yüklendi");
                return;
            }

            Log.Warning("poke.wav bulunamadı, ses devre dışı");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ses dosyası yüklenemedi");
        }
    }

    public void PlayPokeSound()
    {
        if (_player is null) return;

        // UI thread'ini bloklamadan arka planda çal
        Task.Run(() =>
        {
            try
            {
                _player.PlaySync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ses çalınamadı");
            }
        });
    }
}
