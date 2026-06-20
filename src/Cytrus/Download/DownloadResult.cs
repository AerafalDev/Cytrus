namespace Cytrus.Download;

public sealed record DownloadResult(string Version, int FilesWritten, int FilesSkipped, int Symlinks, long BytesDownloaded);
