using System.Globalization;
using Cytrus.Cli.Commands;
using Cytrus.Cli.Services;
using Cytrus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss}] {Level:u3}: {Message:lj}{NewLine}{Exception}", formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

var services = new ServiceCollection();

services.AddLogging(static builder => builder.ClearProviders().AddSerilog());

services.AddCytrus();

var app = new CommandApp(new TypeRegistrar(services));

app.Configure(static config =>
{
    config.SetApplicationName("cytrus");
    config.AddCommand<VersionCommand>("version").WithDescription("Show the latest version of a channel.");
    config.AddCommand<VersionsCommand>("versions").WithDescription("List all advertised game versions.");
    config.AddCommand<DownloadCommand>("download").WithDescription("Download (part of) a game version.");
    #if DEBUG
    config.PropagateExceptions();
    config.ValidateExamples();
    #endif
});

try
{
    return await app.RunAsync(args);
}
catch (Exception ex)
{
    AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
    return 1;
}
