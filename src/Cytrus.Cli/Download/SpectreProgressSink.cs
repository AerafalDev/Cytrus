using Cytrus.Assembly;
using Cytrus.Download;
using Spectre.Console;

namespace Cytrus.Cli.Download;

public sealed class SpectreProgressSink(ProgressContext context) : IDownloadProgressSink
{
    private ProgressTask? _bytes;
    private ProgressTask? _files;

    public void ReportPlan(long totalDownloadBytes, int totalFiles, int fragmentCount)
    {
        _bytes = context.AddTask("[green]Downloading[/]", maxValue: Math.Max(1, totalDownloadBytes));
        _files = context.AddTask("[blue]Assembling files[/]", maxValue: Math.Max(1, totalFiles));
    }

    public void BeginFragment(string name, int index, int total, long downloadBytes, int fileCount)
    {
        _bytes?.Description = $"[green]Downloading[/] [grey]({index}/{total} {Markup.Escape(name)})[/]";
    }

    public void ReportDownloadedBytes(long deltaBytes)
    {
        _bytes?.Increment(deltaBytes);
    }

    public void ReportFileCompleted(FileAssemblyResult result)
    {
        _files?.Increment(1);
    }

    public void EndFragment(string name) { }
}
