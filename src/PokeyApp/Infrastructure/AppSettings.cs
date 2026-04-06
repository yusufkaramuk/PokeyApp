namespace PokeyApp.Infrastructure;

public record AppSettings
{
    public string LocalUsername { get; init; } = Environment.MachineName;
    public string PeerIpAddress { get; init; } = string.Empty;
    public int TcpPort { get; init; } = 14191;
    public int UdpPort { get; init; } = 14190;
    public bool SoundEnabled { get; init; } = true;
    public bool StartMinimized { get; init; } = false;
    public int NotificationDurationSeconds { get; init; } = 4;
}
