using System.Text;
using System.Text.Json;
using PokeyApp.Infrastructure;
using PokeyApp.Messages;
using PokeyApp.Transport;
using Serilog;

namespace PokeyApp.Services;

public interface IPokeService
{
    event EventHandler<PokeMessage>? PokeReceived;
    Task SendPokeAsync(CancellationToken cancellationToken = default);
}

public class PokeService : IPokeService
{
    private readonly ITransport _transport;
    private readonly IConfigurationService _config;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public event EventHandler<PokeMessage>? PokeReceived;

    public PokeService(ITransport transport, IConfigurationService config)
    {
        _transport = transport;
        _config = config;
        _transport.FrameReceived += OnFrameReceived;
    }

    public async Task SendPokeAsync(CancellationToken cancellationToken = default)
    {
        var settings = _config.Load();
        var message = new PokeMessage
        {
            FromUsername = settings.LocalUsername,
            Timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var payload = Encoding.UTF8.GetBytes(json);

        await _transport.SendAsync(payload, cancellationToken);
        Log.Information("Dürt gönderildi");
    }

    private void OnFrameReceived(object? sender, byte[] frame)
    {
        try
        {
            var json = Encoding.UTF8.GetString(frame);
            var message = JsonSerializer.Deserialize<PokeMessage>(json, JsonOptions);

            if (message is null || message.Type != "Poke")
                return;

            Log.Information("Dürt alındı: {From}", message.FromUsername);
            PokeReceived?.Invoke(this, message);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "Geçersiz JSON frame alındı, göz ardı edildi");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Frame işlenirken hata");
        }
    }
}
