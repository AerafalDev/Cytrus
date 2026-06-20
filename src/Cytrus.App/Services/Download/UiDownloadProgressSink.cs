using Cytrus.Assembly;
using Cytrus.Download;

namespace Cytrus.App.Services.Download;

public sealed class UiDownloadProgressSink : IDownloadProgressSink
{
    private long _totalDownloadBytes;
    private long _downloadedBytes;
    private int _totalFiles;
    private int _completedFiles;
    private int _fragmentCount;

    private volatile string _fragment = string.Empty;

    public long TotalDownloadBytes =>
        Interlocked.Read(ref _totalDownloadBytes);

    public long DownloadedBytes =>
        Interlocked.Read(ref _downloadedBytes);

    public int TotalFiles =>
        Volatile.Read(ref _totalFiles);

    public int CompletedFiles =>
        Volatile.Read(ref _completedFiles);

    public int FragmentCount =>
        Volatile.Read(ref _fragmentCount);

    public string CurrentFragment =>
        _fragment;

    public void ReportPlan(long totalDownloadBytes, int totalFiles, int fragmentCount)
    {
        Interlocked.Exchange(ref _totalDownloadBytes, totalDownloadBytes);
        Volatile.Write(ref _totalFiles, totalFiles);
        Volatile.Write(ref _fragmentCount, fragmentCount);
    }

    public void BeginFragment(string name, int index, int total, long downloadBytes, int fileCount)
    {
        _fragment = $"{name} ({index}/{total})";
    }

    public void ReportDownloadedBytes(long deltaBytes)
    {
        Interlocked.Add(ref _downloadedBytes, deltaBytes);
    }

    public void ReportFileCompleted(FileAssemblyResult result)
    {
        Interlocked.Increment(ref _completedFiles);
    }

    public void EndFragment(string name) { }
}
