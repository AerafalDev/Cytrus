using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Cytrus.Exceptions;
using Cytrus.Hash;
using Cytrus.Models;
using Cytrus.Planning;
using Microsoft.Extensions.Logging;

namespace Cytrus.Cdn;

public sealed partial class CytrusCdnClient(
    HttpClient httpClient,
    CdnOptions options,
    RetryPolicy retryPolicy,
    ILogger<CytrusCdnClient> logger) : ICytrusCdnClient
{
    public const string HttpClientName = "cytrus-cdn";

    public async Task<CytrusIndex> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        return await retryPolicy.ExecuteAsync(async (_, ct) =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, options.IndexPath);
            using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

            EnsureSuccess(response, options.IndexPath);

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

            var index = await JsonSerializer.DeserializeAsync(stream, CytrusJsonContext.Default.CytrusIndex, ct).ConfigureAwait(false);

            return index ?? throw new InvalidDataException("cytrus.json deserialized to null.");
        }, "GET cytrus.json", cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> GetManifestAsync(GameCoordinates coordinates, CancellationToken cancellationToken = default)
    {
        if (coordinates.Version is null)
            throw new ArgumentException("A specific version is required to fetch a manifest.", nameof(coordinates));

        var path = $"{coordinates.Game}/releases/{coordinates.Release}/{coordinates.Platform}/{coordinates.Version}.manifest";

        return await retryPolicy.ExecuteAsync(async (_, ct) =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

            EnsureSuccess(response, path);

            return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }, $"GET {path}", cancellationToken).ConfigureAwait(false);
    }

    public async Task<DownloadedRange> DownloadRangeAsync(
        string game,
        HashId bundleHash,
        ByteRange range,
        Func<DownloadedRange, Stream, CancellationToken, Task> onContent,
        CancellationToken cancellationToken = default)
    {
        if (bundleHash.IsEmpty)
            throw new ArgumentException("Bundle hash must not be empty.", nameof(bundleHash));

        if (range.Length <= 0)
            throw new ArgumentException("Range length must be positive.", nameof(range));

        var path = $"{game}/bundles/{bundleHash.ShardPrefix}/{bundleHash.Hex}";

        return await retryPolicy.ExecuteAsync(async (_, ct) =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);

            request.Headers.Range = new RangeHeaderValue(range.Start, range.InclusiveEnd);

            using var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            EnsureSuccess(response, path);

            var downloaded = ResolveReturnedRange(response, range);

            await using var content = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await onContent(downloaded, content, ct).ConfigureAwait(false);

            return downloaded;
        }, $"GET {path} [{range.Start}-{range.InclusiveEnd}]", cancellationToken).ConfigureAwait(false);
    }

    private static DownloadedRange ResolveReturnedRange(HttpResponseMessage response, ByteRange requested)
    {
        if (response.StatusCode is HttpStatusCode.PartialContent)
        {
            var cr = response.Content.Headers.ContentRange;

            if (cr is { HasRange: true, From: { } from, To: { } to })
                return new DownloadedRange(from, to - from + 1, WholeBundleReturned: false);

            return new DownloadedRange(requested.Start, requested.Length, WholeBundleReturned: false);
        }

        var length = response.Content.Headers.ContentLength ?? -1L;

        return new DownloadedRange(0, length, WholeBundleReturned: true);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        HttpCompletionOption completion,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeoutCts.CancelAfter(options.ResponseHeadersTimeout);

        try
        {
            return await httpClient.SendAsync(request, completion, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TransientCdnException($"Request to '{request.RequestUri}' timed out waiting for response headers.");
        }
    }

    private void EnsureSuccess(HttpResponseMessage response, string path)
    {
        if (response.IsSuccessStatusCode)
            return;

        var code = (int)response.StatusCode;
        var message = $"CDN request '{path}' failed with HTTP {code} ({response.ReasonPhrase}).";

        if (code is 408 or 429 or >= 500)
        {
            LogRetryableHttpStatusCodeForPath(code, path);
            throw new TransientCdnException(message);
        }

        throw new CdnRequestException(message, response.StatusCode);
    }

    [LoggerMessage(LogLevel.Debug, "Retryable HTTP status {Code} for {Path}.")]
    partial void LogRetryableHttpStatusCodeForPath(int code, string path);
}
