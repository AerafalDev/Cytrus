using Cytrus.Assembly;

namespace Cytrus.Download;

public sealed class NullDownloadProgressSink : IDownloadProgressSink
{
    public static NullDownloadProgressSink Instance { get; } = new();

    public void ReportPlan(long totalDownloadBytes, int totalFiles, int fragmentCount) { }

    public void BeginFragment(string name, int index, int total, long downloadBytes, int fileCount) { }

    public void ReportDownloadedBytes(long deltaBytes) { }

    public void ReportFileCompleted(FileAssemblyResult result) { }

    public void EndFragment(string name) { }
}
