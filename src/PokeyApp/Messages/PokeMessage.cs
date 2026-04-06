namespace PokeyApp.Messages;

public record PokeMessage
{
    public string Type { get; init; } = "Poke";
    public string FromUsername { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
