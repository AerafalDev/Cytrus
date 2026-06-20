namespace Cytrus.Download;

public interface IGameDownloader
{
    Task<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default);
}
