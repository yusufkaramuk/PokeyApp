namespace PokeyApp.Messages;

public record DiscoveryBeacon
{
    public string Type { get; init; } = "DiscoveryBeacon";
    public string Username { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public int TcpPort { get; init; }
}

public record DiscoveryResponse
{
    public string Type { get; init; } = "DiscoveryResponse";
    public string Username { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public int TcpPort { get; init; }
}

public record PeerInfo(string Username, string MachineName, string IpAddress, int TcpPort)
{
    public string DisplayName => $"{Username} ({MachineName})";
}
