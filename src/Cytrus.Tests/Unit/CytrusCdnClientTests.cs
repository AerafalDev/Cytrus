using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Cytrus.Cdn;
using Cytrus.Exceptions;
using Cytrus.Hash;
using Cytrus.Models;
using Cytrus.Planning;
using Cytrus.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cytrus.Tests.Unit;

public sealed class CytrusCdnClientTests
{
    private static (CytrusCdnClient client, TestHttpMessageHandler handler) Create(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
    {
        var options = new CdnOptions { MaxAttempts = 3, RetryBaseDelay = TimeSpan.FromMilliseconds(1), RetryMaxDelay = TimeSpan.FromMilliseconds(3) };
        var handler = new TestHttpMessageHandler(responder);
        var http = new HttpClient(handler) { BaseAddress = options.BaseAddress, Timeout = Timeout.InfiniteTimeSpan };
        var policy = new RetryPolicy(options, NullLogger<RetryPolicy>.Instance);
        return (new CytrusCdnClient(http, options, policy, NullLogger<CytrusCdnClient>.Instance), handler);
    }

    private static HttpResponseMessage Ok(byte[] body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
    }

    [Fact]
    public async Task GetManifestBuildsExpectedUrlAndReturnsBytes()
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        var (client, handler) = Create((_, _) => Ok(payload));

        var bytes = await client.GetManifestAsync(new GameCoordinates("dofus", "windows", "dofus3", "6.0_3.5.17.26"));

        Assert.Equal(payload, bytes);
        Assert.Equal("/dofus/releases/dofus3/windows/6.0_3.5.17.26.manifest", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetIndexParsesJson()
    {
        const string json = """
        {"version":6,"name":"production","games":{"dofus":{"name":"Dofus","order":1,"gameId":1,
        "platforms":{"windows":{"dofus3":"6.0_3.5.17.26","main":"6.0_2.73.3.14"}}}}}
        """;

        var (client, _) = Create((_, _) => Ok(Encoding.UTF8.GetBytes(json)));

        var index = await client.GetIndexAsync();

        Assert.Equal("6.0_3.5.17.26", index.ResolveVersion("dofus", "windows", "dofus3"));
        Assert.Null(index.ResolveVersion("dofus", "windows", "beta"));
    }

    [Fact]
    public async Task DownloadRangeSendsRangeHeaderAndReportsPartialContent()
    {
        var slice = new byte[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };
        var (client, handler) = Create((request, _) =>
        {
            Assert.Equal("bytes=10-19", request.Headers.Range!.ToString());

            var resp = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(slice) };
            resp.Content.Headers.ContentRange = new ContentRangeHeaderValue(10, 19, 100);
            return resp;
        });

        DownloadedRange captured = default;
        byte[] body = [];

        await client.DownloadRangeAsync("dofus", HashId.Parse("3fa291"), new ByteRange(10, 20), async (dr, stream, ct) =>
        {
            captured = dr;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            body = ms.ToArray();
        });

        Assert.Equal(10, captured.Start);
        Assert.Equal(10, captured.Length);
        Assert.False(captured.WholeBundleReturned);
        Assert.Equal(slice, body);
        Assert.Equal("/dofus/bundles/3f/3fa291", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DownloadRangeHandlesFullBundleReturnedForRangeRequest()
    {
        var whole = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
        var (client, _) = Create((_, _) => Ok(whole));

        DownloadedRange captured = default;

        await client.DownloadRangeAsync("dofus", HashId.Parse("abcd"), new ByteRange(10, 20), (dr, _, _) =>
        {
            captured = dr;
            return Task.CompletedTask;
        });

        Assert.True(captured.WholeBundleReturned);
        Assert.Equal(0, captured.Start);
        Assert.Equal(100, captured.Length);
    }

    [Fact]
    public async Task NotFoundIsNotRetried()
    {
        var (client, handler) = Create((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));

        var ex = await Assert.ThrowsAsync<CdnRequestException>(() => client.GetManifestAsync(new GameCoordinates("dofus", "windows", "main", "9.9_9.9.9.9")));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ServerErrorIsRetriedThenSucceeds()
    {
        var payload = new byte[] { 7 };
        var (client, handler) = Create((_, attempt) => attempt is 1 ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Ok(payload));

        var bytes = await client.GetManifestAsync(new GameCoordinates("dofus", "windows", "main", "6.0_2.73.3.14"));

        Assert.Equal(payload, bytes);
        Assert.Equal(2, handler.Requests.Count);
    }
}
