using Cytrus.Assembly;

namespace Cytrus.Download;

public interface IDownloadProgressSink
{
    void ReportPlan(long totalDownloadBytes, int totalFiles, int fragmentCount);

    void BeginFragment(string name, int index, int total, long downloadBytes, int fileCount);

    void ReportDownloadedBytes(long deltaBytes);

    void ReportFileCompleted(FileAssemblyResult result);

    void EndFragment(string name);
}
