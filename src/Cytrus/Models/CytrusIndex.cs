using System.Text.Json.Serialization;

namespace Cytrus.Models;

public sealed class CytrusIndex
{
    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("games")]
    public Dictionary<string, CytrusGame> Games { get; init; } = new();

    public string? ResolveVersion(string game, string platform, string release)
    {
        if (!Games.TryGetValue(game, out var extractedGame))
            return null;

        if (!extractedGame.Platforms.TryGetValue(platform, out var releases))
            return null;

        return releases.TryGetValue(release, out var version) ? version : null;
    }
}
