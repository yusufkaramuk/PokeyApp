using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using PokeyApp.Infrastructure;
using PokeyApp.Messages;
using Serilog;

namespace PokeyApp.Services;

public interface IDiscoveryService
{
    event EventHandler<PeerInfo>? PeerDiscovered;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public class DiscoveryService : IDiscoveryService, IHostedService
{
    private readonly IConfigurationService _config;
    private UdpClient? _udpClient;
    private CancellationTokenSource _cts = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public event EventHandler<PeerInfo>? PeerDiscovered;

    public DiscoveryService(IConfigurationService config)
    {
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = new CancellationTokenSource();
        var settings = _config.Load();

        try
        {
            _udpClient = new UdpClient(settings.UdpPort);
            _udpClient.EnableBroadcast = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "UDP socket başlatılamadı (port {Port})", settings.UdpPort);
            return;
        }

        _ = ListenLoopAsync(_cts.Token);
        _ = BroadcastLoopAsync(_cts.Token);

        Log.Information("Discovery Service başlatıldı, UDP port {Port}", settings.UdpPort);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _cts.CancelAsync();
        _udpClient?.Close();
        Log.Information("Discovery Service durduruldu");
    }

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        // İlk broadcast hemen
        await SendBroadcastAsync(ct);

        while (await timer.WaitForNextTickAsync(ct))
        {
            await SendBroadcastAsync(ct);
        }
    }

    private async Task SendBroadcastAsync(CancellationToken ct)
    {
        try
        {
            var settings = _config.Load();
            var beacon = new DiscoveryBeacon
            {
                Username = settings.LocalUsername,
                MachineName = Environment.MachineName,
                TcpPort = settings.TcpPort
            };

            var json = JsonSerializer.Serialize(beacon, JsonOptions);
            var data = Encoding.UTF8.GetBytes(json);

            var endpoint = new IPEndPoint(IPAddress.Broadcast, settings.UdpPort);
            await _udpClient!.SendAsync(data, endpoint, ct);
            Log.Debug("Discovery beacon broadcast yapıldı");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Warning(ex, "Broadcast başarısız");
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient!.ReceiveAsync(ct);
                var json = Encoding.UTF8.GetString(result.Buffer);
                var senderIp = result.RemoteEndPoint.Address.ToString();

                // Kendi broadcast'imizi görmezden gel
                if (IsLocalIp(senderIp)) continue;

                HandleUdpMessage(json, senderIp);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Log.Warning(ex, "UDP receive hatası");
            }
        }
    }

    private void HandleUdpMessage(string json, string senderIp)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var type = doc.RootElement.GetProperty("Type").GetString();

            if (type == "DiscoveryBeacon")
            {
                var beacon = JsonSerializer.Deserialize<DiscoveryBeacon>(json, JsonOptions)!;
                var peer = new PeerInfo(beacon.Username, beacon.MachineName, senderIp, beacon.TcpPort);
                Log.Information("Peer keşfedildi (beacon): {DisplayName} ({Ip})", peer.DisplayName, senderIp);
                PeerDiscovered?.Invoke(this, peer);

                // Beacon'a yanıt ver
                _ = SendResponseAsync(senderIp, beacon.TcpPort);
            }
            else if (type == "DiscoveryResponse")
            {
                var response = JsonSerializer.Deserialize<DiscoveryResponse>(json, JsonOptions)!;
                var peer = new PeerInfo(response.Username, response.MachineName, senderIp, response.TcpPort);
                Log.Information("Peer keşfedildi (response): {DisplayName} ({Ip})", peer.DisplayName, senderIp);
                PeerDiscovered?.Invoke(this, peer);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "UDP mesajı işlenemedi");
        }
    }

    private async Task SendResponseAsync(string targetIp, int _)
    {
        try
        {
            var settings = _config.Load();
            var response = new DiscoveryResponse
            {
                Username = settings.LocalUsername,
                MachineName = Environment.MachineName,
                TcpPort = settings.TcpPort
            };

            var json = JsonSerializer.Serialize(response, JsonOptions);
            var data = Encoding.UTF8.GetBytes(json);
            var endpoint = new IPEndPoint(IPAddress.Parse(targetIp), settings.UdpPort);
            await _udpClient!.SendAsync(data, endpoint);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Discovery yanıtı gönderilemedi");
        }
    }

    private static bool IsLocalIp(string ip)
    {
        var localAddresses = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Select(ua => ua.Address.ToString());
        return localAddresses.Contains(ip);
    }
}
