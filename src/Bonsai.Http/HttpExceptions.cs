namespace Bonsai.Http;

public sealed class HttpStatusException : Exception
{
    public HttpStatusException(HttpResponse response) : base($"HTTP request returned {response.StatusCode}.") => Response = response;
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
    public HttpRequest Request { get; }
    public Guid RequestId { get; }
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
    public Guid RequestId { get; }
    public long Limit { get; }
}

public sealed class HttpResponseParseException : Exception
{
    public HttpResponseParseException(HttpResponse response, Exception inner)
        : base($"Could not parse HTTP response for request {response.RequestId}.", inner) => Response = response;
    public HttpResponse Response { get; }
}
