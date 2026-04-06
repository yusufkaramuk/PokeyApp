using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using PokeyApp.Transport;
using Xunit;

namespace PokeyApp.Tests.Transport;

/// <summary>
/// Gerçek TCP soketleri ile loopback entegrasyon testleri.
/// Her test farklı portlar kullanır (OS tarafından dinamik atanır).
/// </summary>
public class TcpTransportIntegrationTests : IAsyncDisposable
{
    private readonly TcpTransport _server;
    private readonly TcpTransport _client;
    private readonly int _serverPort;
    private readonly int _clientPort;

    public TcpTransportIntegrationTests()
    {
        // OS'tan boş port al (0 = OS'a bırak)
        _serverPort = GetFreePort();
        _clientPort = GetFreePort();
        _server = new TcpTransport(_serverPort);
        _client = new TcpTransport(_clientPort);
    }

    [Fact]
    public async Task SendPoke_FromClient_ReceivedByServer()
    {
        var received = new TaskCompletionSource<byte[]>();

        // Listen on both sides — whichever receives the frame will set the result
        _server.FrameReceived += (_, frame) => received.TrySetResult(frame);
        _client.FrameReceived += (_, frame) => received.TrySetResult(frame);

        await _server.StartAsync();
        await _client.StartAsync();
        // Both sides must know each other for loopback role determination
        _server.SetPeer("127.0.0.1", _clientPort);
        _client.SetPeer("127.0.0.1", _serverPort);

        // HEM client HEM server'ın bağlı olmasını bekle
        await Task.WhenAll(
            WaitForState(_client, TransportState.Connected, TimeSpan.FromSeconds(8)),
            WaitForState(_server, TransportState.Connected, TimeSpan.FromSeconds(8))
        );

        // Determine which side is client (smaller port = client on loopback)
        var sender = _clientPort < _serverPort ? _client : _server;
        var payload = Encoding.UTF8.GetBytes("{\"Type\":\"Poke\",\"FromUsername\":\"Test\"}");
        await sender.SendAsync(payload);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Should().Equal(payload);
    }

    [Fact]
    public async Task State_InitiallyDisconnected()
    {
        _client.State.Should().Be(TransportState.Disconnected);
    }

    [Fact]
    public async Task State_BecomeConnected_WhenPeerAvailable()
    {
        await _server.StartAsync();
        await _client.StartAsync();
        // Both sides must know each other for loopback role determination
        _server.SetPeer("127.0.0.1", _clientPort);
        _client.SetPeer("127.0.0.1", _serverPort);

        await Task.WhenAll(
            WaitForState(_client, TransportState.Connected, TimeSpan.FromSeconds(8)),
            WaitForState(_server, TransportState.Connected, TimeSpan.FromSeconds(8))
        );
        _client.State.Should().Be(TransportState.Connected);
        _server.State.Should().Be(TransportState.Connected);
    }

    /// <summary>Polling ile state bekler — event race condition yok.</summary>
    private static async Task WaitForState(
        ITransport transport,
        TransportState targetState,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (transport.State != targetState)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException(
                    $"Transport {targetState} durumuna {timeout.TotalSeconds}s içinde ulaşamadı. " +
                    $"Mevcut durum: {transport.State}");
            await Task.Delay(50);
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        await _server.StopAsync();
        await _client.StopAsync();
    }
}
