namespace Cytrus.Tests.Support;

public sealed class TestHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(responder(request, Requests.Count));
    }
}
