using System.ComponentModel;
namespace Bonsai.Http;

public sealed class HttpStatusException : Exception
{
    public HttpStatusException(HttpResponse response) : base($"HTTP request returned {response.StatusCode}.") => Response = response;
    [Description("The HTTP response associated with this error, including its originating request and body.")]
    public HttpResponse Response { get; }
}

public sealed class HttpRequestTimeoutException : TimeoutException
{
    public HttpRequestTimeoutException(HttpRequest request, Guid requestId, TimeSpan timeout)
        : base($"HTTP request {requestId} exceeded timeout {timeout}.")
    {
        Request = request;
        RequestId = requestId;
        Timeout = timeout;
    }
    [Description("The request that exceeded the configured timeout.")]
    public HttpRequest Request { get; }
    [Description("The unique identifier of the request execution that failed.")]
    public Guid RequestId { get; }
    [Description("The configured request timeout that was exceeded.")]
    public TimeSpan Timeout { get; }
}

public sealed class HttpResponseSizeException : Exception
{
    public HttpResponseSizeException(Guid requestId, long limit)
        : base($"HTTP response for request {requestId} exceeded {limit} bytes.")
    {
        RequestId = requestId;
        Limit = limit;
    }
    [Description("The unique identifier of the request execution that failed.")]
    public Guid RequestId { get; }
    [Description("The maximum permitted response body size in bytes.")]
    public long Limit { get; }
}

public sealed class HttpResponseParseException : Exception
{
    public HttpResponseParseException(HttpResponse response, Exception inner)
        : base($"Could not parse HTTP response for request {response.RequestId}.", inner) => Response = response;
    [Description("The HTTP response associated with this error, including its originating request and body.")]
    public HttpResponse Response { get; }
}
