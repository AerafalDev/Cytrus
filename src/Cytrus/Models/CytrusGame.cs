using System.Text.Json.Serialization;

namespace Cytrus.Models;

public sealed class CytrusGame
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("gameId")]
    public int GameId { get; init; }

    [JsonPropertyName("platforms")]
    public Dictionary<string, Dictionary<string, string>> Platforms { get; init; } = new();
}
