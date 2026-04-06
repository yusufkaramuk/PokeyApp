namespace PokeyApp.Transport;

public interface ITransport
{
    TransportState State { get; }
    event EventHandler<TransportState>? StateChanged;
    event EventHandler<byte[]>? FrameReceived;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SendAsync(byte[] frame, CancellationToken cancellationToken = default);

    void SetPeer(string ipAddress, int port);
}
