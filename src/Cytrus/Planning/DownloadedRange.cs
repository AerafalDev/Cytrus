namespace Cytrus.Planning;

public readonly record struct DownloadedRange(long Start, long Length, bool WholeBundleReturned);
