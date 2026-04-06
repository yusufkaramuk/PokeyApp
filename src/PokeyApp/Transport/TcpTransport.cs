using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace PokeyApp.Transport;

/// <summary>
/// Simetrik TCP transport: Her instance hem listener (server) hem de outbound client.
/// Küçük IP adresi her zaman client rolünü üstlenir (race condition önlemi).
/// Bağlantı koptuğunda exponential backoff ile yeniden bağlanır.
/// </summary>
public class TcpTransport : ITransport, IHostedService
{
    private readonly int _listenPort;
    private string _peerIp = string.Empty;
    private int _peerPort;

    private TcpListener? _listener;
    private TcpClient? _connection;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private CancellationTokenSource _cts = new();
    private TransportState _state = TransportState.Disconnected;

    public TransportState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(this, value);
        }
    }

    public event EventHandler<TransportState>? StateChanged;
    public event EventHandler<byte[]>? FrameReceived;

    public TcpTransport(int listenPort)
    {
        _listenPort = listenPort;
    }

    public void SetPeer(string ipAddress, int port)
    {
        _peerIp = ipAddress;
        _peerPort = port;

        // Yeni peer ayarlandığında mevcut bağlantıyı sıfırla
        _ = ResetConnectionAsync();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = new CancellationTokenSource();
        _ = RunListenerAsync(_cts.Token);
        Log.Information("TCP Transport başlatıldı, port {Port}", _listenPort);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _cts.CancelAsync();
        CloseConnection();
        _listener?.Stop();
        Log.Information("TCP Transport durduruldu");
    }

    public async Task SendAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        if (_stream is null || State != TransportState.Connected)
            throw new InvalidOperationException("Bağlantı yok");

        var frame = MessageFramer.Encode(payload);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _stream.WriteAsync(frame, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    // --- Listener (server role) ---

    private async Task RunListenerAsync(CancellationToken ct)
    {
        _listener = new TcpListener(IPAddress.Any, _listenPort);
        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Start();
        Log.Debug("TCP Listener başlatıldı: port {Port}", _listenPort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var inboundClient = await _listener.AcceptTcpClientAsync(ct);
                var remoteIp = ((IPEndPoint)inboundClient.Client.RemoteEndPoint!).Address.ToString();

                // Yalnızca kayıtlı peer IP'sinden gelen bağlantıları kabul et
                if (!string.IsNullOrEmpty(_peerIp) && remoteIp != _peerIp)
                {
                    Log.Warning("Bilinmeyen IP'den bağlantı reddedildi: {Ip}", remoteIp);
                    inboundClient.Close();
                    continue;
                }

                // Client role belirleme: küçük IP addresi client olur
                var localIp = GetLocalIp(remoteIp);
                int cmp = string.Compare(localIp, remoteIp, StringComparison.Ordinal);
                bool weAreClient = cmp < 0 || (cmp == 0 && _listenPort < _peerPort);

                if (weAreClient)
                {
                    // If we are supposed to be the client, we shouldn't accept inbound connections!
                    Log.Debug("Biz istemci (Client) rolündeyiz, gelen sunucu bağlantısı reddedildi.");
                    inboundClient.Close();
                    continue;
                }

                Log.Information("Gelen bağlantı kabul edildi: {Ip}", remoteIp);
                AcceptConnection(inboundClient);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Log.Error(ex, "Listener hatası");
            }
        }
    }

    // --- Client role: outbound bağlantı + reconnect ---

    private async Task RunClientAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_peerIp)) return;

        var localIp = GetLocalIp(_peerIp);
        // Deterministic role: smaller IP = client; equal IPs use port as tiebreaker
        int cmp = string.Compare(localIp, _peerIp, StringComparison.Ordinal);
        bool weAreClient = cmp < 0 || (cmp == 0 && _listenPort < _peerPort);

        if (!weAreClient)
        {
            Log.Debug("Biz sunucu (Server) olarak atandık, Karşı tarafın bağlanmasını bekliyoruz.");
            return; // Wait as a server
        }

        var delays = new[] { 2, 4, 8, 16, 30 };
        int attempt = 0;

        while (!ct.IsCancellationRequested)
        {
            if (State == TransportState.Connected)
            {
                await Task.Delay(1000, ct);
                continue;
            }

            State = attempt == 0 ? TransportState.Connecting : TransportState.Reconnecting;
            Log.Debug("Peer'e bağlanılıyor: {Ip}:{Port} (deneme {Attempt})", _peerIp, _peerPort, attempt + 1);

            try
            {
                var client = new TcpClient();
                client.NoDelay = true;

                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connectCts.CancelAfter(TimeSpan.FromSeconds(5));

                await client.ConnectAsync(_peerIp, _peerPort, connectCts.Token);
                Log.Information("Peer'e bağlandı: {Ip}:{Port}", _peerIp, _peerPort);
                attempt = 0;
                AcceptConnection(client);

                // Bağlantı kopana kadar bekle
                while (!ct.IsCancellationRequested && State == TransportState.Connected)
                    await Task.Delay(500, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Bağlantı denemesi başarısız");
            }

            int delay = delays[Math.Min(attempt, delays.Length - 1)];
            attempt++;
            Log.Debug("{Delay}s sonra yeniden denenecek", delay);

            try { await Task.Delay(TimeSpan.FromSeconds(delay), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    // --- Connection management ---

    private void AcceptConnection(TcpClient client)
    {
        CloseConnection();
        client.NoDelay = true;

        _connection = client;
        _stream = client.GetStream();
        State = TransportState.Connected;

        _ = ReceiveLoopAsync(_cts.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var frame = await MessageFramer.ReadFrameAsync(_stream!, ct);
                if (frame is null)
                {
                    Log.Information("Bağlantı karşı tarafından kapatıldı");
                    break;
                }
                FrameReceived?.Invoke(this, frame);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                Log.Warning(ex, "Receive loop hatası");
        }
        finally
        {
            CloseConnection();
            if (!ct.IsCancellationRequested && !string.IsNullOrEmpty(_peerIp))
                _ = RunClientAsync(ct);
        }
    }

    private void CloseConnection()
    {
        try { _stream?.Close(); } catch { }
        try { _connection?.Close(); } catch { }
        _stream = null;
        _connection = null;
        if (State == TransportState.Connected)
            State = TransportState.Disconnected;
    }

    private async Task ResetConnectionAsync()
    {
        CloseConnection();
        if (!_cts.IsCancellationRequested)
            _ = RunClientAsync(_cts.Token);
        await Task.CompletedTask;
    }

    private static string GetLocalIp(string destinationIp)
    {
        try
        {
            if (string.IsNullOrEmpty(destinationIp)) destinationIp = "8.8.8.8";
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(destinationIp, 80);
            return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}
