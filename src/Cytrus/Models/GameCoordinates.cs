using System.Text.RegularExpressions;

namespace Cytrus.Models;

public sealed partial class GameCoordinates
{
    public string Game { get; }

    public string Platform { get; }

    public string Release { get; }

    public string? Version { get; }

    public GameCoordinates(string game, string platform, string release, string? version = null)
    {
        Game = Validate(game, nameof(game));
        Platform = Validate(platform, nameof(platform));
        Release = Validate(release, nameof(release));
        Version = version is null ? null : Validate(version, nameof(version));
    }

    public GameCoordinates WithVersion(string version)
    {
        return new GameCoordinates(Game, Platform, Release, version);
    }

    public override string ToString()
    {
        return Version is null ? $"{Game}/{Release}/{Platform}" : $"{Game}/{Release}/{Platform}@{Version}";
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._\-]{0,127}$")]
    private static partial Regex TokenRegex { get; }

    private static string Validate(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"'{name}' must not be empty.", name);

        if (!TokenRegex.IsMatch(value))
            throw new ArgumentException($"'{name}' value '{value}' contains characters that are not allowed in a CDN identifier.", name);

        return value;
    }
}
