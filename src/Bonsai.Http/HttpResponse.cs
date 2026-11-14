using System.ComponentModel;
namespace Bonsai.Http;

/// <summary>A fully materialized HTTP response that owns no native HTTP resources.</summary>
public sealed class HttpResponse
{
    readonly byte[] body;
    public HttpResponse(HttpRequest request, Guid requestId, Uri effectiveUri, int statusCode,
        string? reasonPhrase, IReadOnlyDictionary<string, string[]> headers, byte[] body, TimeSpan duration)
        : this(request, requestId, effectiveUri, statusCode, reasonPhrase, headers, body, duration, request) { }

    public HttpResponse(HttpRequest request, Guid requestId, Uri effectiveUri, int statusCode,
        string? reasonPhrase, IReadOnlyDictionary<string, string[]> headers, byte[] body, TimeSpan duration, HttpRequest? originalRequest)
    {
        Request = request;
        OriginalRequest = originalRequest ?? request;
        RequestId = requestId;
        EffectiveUri = effectiveUri;
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        Headers = new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            headers.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)Array.AsReadOnly((string[])pair.Value.Clone()), StringComparer.OrdinalIgnoreCase));
        this.body = (byte[])body.Clone();
        Duration = duration;
    }

    [Description("The originating request with its resolved URI and effective headers.")]
    public HttpRequest Request { get; }
    [Description("The original request object supplied to this execution.")]
    public HttpRequest OriginalRequest { get; }
    [Description("The unique identifier assigned to this request execution.")]
    public Guid RequestId { get; }
    [Description("The final response URI after any redirects.")]
    public Uri EffectiveUri { get; }
    [Description("The numeric HTTP response status code. Non-success statuses are emitted as values.")]
    public int StatusCode { get; }
    [Description("The status reason phrase returned by the server, when available.")]
    public string? ReasonPhrase { get; }
    [Description("The response headers, including content headers and all values for each name.")]
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }
    [Description("A copy of the fully buffered response body bytes.")]
    public byte[] Body => (byte[])body.Clone();
    [Description("The elapsed time to send the request and fully read its response body.")]
    public TimeSpan Duration { get; }
    [Description("Indicates whether the HTTP status code is between 200 and 299.")]
    public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;
    public override string ToString() => $"{StatusCode} {Request.Method} {EffectiveUri}";
}
