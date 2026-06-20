using System.Net;

namespace Cytrus.Exceptions;

public sealed class CdnRequestException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
