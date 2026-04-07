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
    private readonly object _connectionLock = new();
    private int _connectionVersion = 0;

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
        CloseConnection(); // Triggers reconnect; RunClientAsync loop picks up new peer
        Log.Information("Peer güncellendi: {Ip}:{Port}", ipAddress, port);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = new CancellationTokenSource();
        _ = RunListenerAsync(_cts.Token);
        _ = RunClientAsync(_cts.Token); // Single long-running loop; role is checked internally
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

    // --- Listener: accepts inbound connections when we are server role ---

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

                // Reject connections from unknown peers
                if (string.IsNullOrEmpty(_peerIp) || remoteIp != _peerIp)
                {
                    Log.Warning("Beklenmeyen IP'den bağlantı reddedildi: {Ip}", remoteIp);
                    inboundClient.Close();
                    continue;
                }

                // Only the server role (larger IP, or larger port on tie) accepts inbound
                var localIp = GetLocalIp(remoteIp);
                int cmp = string.Compare(localIp, remoteIp, StringComparison.Ordinal);
                bool weAreServer = cmp > 0 || (cmp == 0 && _listenPort > _peerPort);

                if (!weAreServer)
                {
                    Log.Debug("Biz istemci rolündeyiz, gelen bağlantı reddedildi ({RemoteIp})", remoteIp);
                    inboundClient.Close();
                    continue;
                }

                Log.Information("Gelen bağlantı kabul edildi: {Ip}", remoteIp);
                AcceptConnection(inboundClient);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Log.Error(ex, "Listener hatası");
            }
        }
    }

    // --- Single client loop: role check, outbound connect, reconnect ---
    // Started once in StartAsync and runs for the lifetime of the transport.
    // This is the ONLY place RunClientAsync is spawned — ReceiveLoopAsync does NOT re-spawn it.

    private async Task RunClientAsync(CancellationToken ct)
    {
        var delays = new[] { 2, 4, 8, 16, 30 };
        int attempt = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // No peer configured yet: idle
                if (string.IsNullOrEmpty(_peerIp))
                {
                    await Task.Delay(500, ct);
                    continue;
                }

                // Already connected: wait
                if (State == TransportState.Connected)
                {
                    await Task.Delay(500, ct);
                    attempt = 0;
                    continue;
                }

                // Determine our role for this peer
                var localIp = GetLocalIp(_peerIp);
                int cmp = string.Compare(localIp, _peerIp, StringComparison.Ordinal);
                bool weAreClient = cmp < 0 || (cmp == 0 && _listenPort < _peerPort);

                if (!weAreClient)
                {
                    // Server role: listener handles inbound; show "waiting" state
                    if (State == TransportState.Disconnected)
                        State = TransportState.Connecting;
                    await Task.Delay(1000, ct);
                    attempt = 0;
                    continue;
                }

                // Client role: attempt outbound connection
                State = attempt == 0 ? TransportState.Connecting : TransportState.Reconnecting;
                Log.Debug("Peer'e bağlanılıyor: {Ip}:{Port} (deneme {Attempt})", _peerIp, _peerPort, attempt + 1);

                var client = new TcpClient { NoDelay = true };
                bool connected = false;
                try
                {
                    using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    connectCts.CancelAfter(TimeSpan.FromSeconds(5));
                    await client.ConnectAsync(_peerIp, _peerPort, connectCts.Token);
                    connected = true;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    client.Close();
                    break;
                }
                catch (Exception ex)
                {
                    client.Close();
                    Log.Debug("Bağlantı denemesi başarısız (deneme {Attempt}): {Message}", attempt + 1, ex.Message);
                }

                if (connected)
                {
                    Log.Information("Peer'e bağlandı: {Ip}:{Port}", _peerIp, _peerPort);
                    attempt = 0;
                    AcceptConnection(client);

                    // Wait while connected; ReceiveLoopAsync will set State=Disconnected on drop
                    while (!ct.IsCancellationRequested && State == TransportState.Connected)
                        await Task.Delay(500, ct);

                    continue; // Skip backoff after successful connect cycle
                }

                int delay = delays[Math.Min(attempt, delays.Length - 1)];
                attempt++;
                Log.Debug("{Delay}s sonra yeniden denenecek", delay);
                try { await Task.Delay(TimeSpan.FromSeconds(delay), ct); }
                catch (OperationCanceledException) { break; }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Log.Error(ex, "RunClientAsync beklenmedik hata");
                try { await Task.Delay(2000, ct); } catch (OperationCanceledException) { break; }
            }
        }
    }

    // --- Connection management ---

    private void AcceptConnection(TcpClient client)
    {
        int version;
        lock (_connectionLock)
        {
            _connectionVersion++;
            version = _connectionVersion;

            // Close stale connection; any active ReceiveLoopAsync will detect version mismatch
            try { _stream?.Close(); } catch { }
            try { _connection?.Close(); } catch { }

            client.NoDelay = true;
            _connection = client;
            _stream = client.GetStream();
            State = TransportState.Connected;
        }
        _ = ReceiveLoopAsync(version, _cts.Token);
    }

    private async Task ReceiveLoopAsync(int version, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                NetworkStream? stream;
                lock (_connectionLock)
                {
                    if (_connectionVersion != version) break; // Superseded by a newer connection
                    stream = _stream;
                }
                if (stream is null) break;

                var frame = await MessageFramer.ReadFrameAsync(stream, ct);
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
            bool superseded;
            lock (_connectionLock) { superseded = _connectionVersion != version; }
            if (!superseded && !ct.IsCancellationRequested)
                Log.Warning(ex, "Receive loop hatası");
        }
        finally
        {
            // Only the current (non-superseded) receive loop should clean up state
            bool isOwner;
            lock (_connectionLock)
            {
                isOwner = _connectionVersion == version;
                if (isOwner)
                {
                    try { _stream?.Close(); } catch { }
                    try { _connection?.Close(); } catch { }
                    _stream = null;
                    _connection = null;
                }
            }
            if (isOwner && State == TransportState.Connected)
                State = TransportState.Disconnected;
        }
    }

    private void CloseConnection()
    {
        bool wasConnected;
        lock (_connectionLock)
        {
            _connectionVersion++; // Invalidates any active ReceiveLoopAsync
            try { _stream?.Close(); } catch { }
            try { _connection?.Close(); } catch { }
            _stream = null;
            _connection = null;
            wasConnected = _state == TransportState.Connected;
        }
        if (wasConnected)
            State = TransportState.Disconnected;
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
