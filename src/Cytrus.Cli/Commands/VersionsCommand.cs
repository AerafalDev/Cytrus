using System.ComponentModel;
using Cytrus.Cdn;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cytrus.Cli.Commands;

public sealed class VersionsCommand(ICytrusCdnClient cdn) : AsyncCommand<VersionsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-g|--game <GAME>")]
        [Description("Only show this game.")]
        public string? Game { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var index = await cdn.GetIndexAsync(cancellationToken).ConfigureAwait(false);
        var table = new Table().Border(TableBorder.Rounded);

        table.AddColumn("Game");
        table.AddColumn("Platform");
        table.AddColumn("Release");
        table.AddColumn("Version");

        foreach (var (gameName, game) in index.Games.OrderBy(static g => g.Value.Order))
        {
            if (settings.Game is { } filter && !string.Equals(filter, gameName, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var (platform, releases) in game.Platforms.OrderBy(static p => p.Key))
                foreach (var (release, version) in releases.OrderBy(static r => r.Key))
                    table.AddRow(
                        Markup.Escape(gameName),
                        Markup.Escape(platform),
                        Markup.Escape(release),
                        Markup.Escape(version));
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
