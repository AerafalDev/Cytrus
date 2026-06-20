using Cytrus.Models;

namespace Cytrus.Cdn;

public interface IGameVersionResolver
{
    Task<string> ResolveVersionAsync(GameCoordinates coordinates, CancellationToken cancellationToken = default);
}
