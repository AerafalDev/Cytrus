using Cytrus.Models;

namespace Cytrus.Cdn;

public sealed class GameVersionResolver(ICytrusCdnClient cdnClient) : IGameVersionResolver
{
    public async Task<string> ResolveVersionAsync(GameCoordinates coordinates, CancellationToken cancellationToken = default)
    {
        if (coordinates.Version is { } explicitVersion)
            return explicitVersion;

        var index = await cdnClient.GetIndexAsync(cancellationToken).ConfigureAwait(false);
        var version = index.ResolveVersion(coordinates.Game, coordinates.Platform, coordinates.Release);

        if (string.IsNullOrEmpty(version))
            throw new InvalidOperationException($"No version is advertised for '{coordinates.Game}' / '{coordinates.Platform}' / '{coordinates.Release}' in cytrus.json.");

        return version;
    }
}
