using Cytrus.Cdn;
using Cytrus.Models;
using Spectre.Console.Cli;

namespace Cytrus.Cli.Commands;

public sealed class VersionCommand(IGameVersionResolver resolver) : AsyncCommand<TargetSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, TargetSettings settings, CancellationToken cancellationToken)
    {
        var coordinates = new GameCoordinates(settings.Game, settings.Platform, settings.Release);
        var version = await resolver.ResolveVersionAsync(coordinates, cancellationToken).ConfigureAwait(false);

        Console.WriteLine(version);
        return 0;
    }
}
