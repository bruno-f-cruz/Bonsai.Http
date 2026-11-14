using System.ComponentModel;
namespace Bonsai.Http.Server;

/// <summary>An HTTP response to send to an incoming request.</summary>
public sealed class HttpServerResponse
{
    readonly byte[] body;
    public HttpServerResponse(int statusCode = 200, byte[]? body = null, string? contentType = null,
        IReadOnlyDictionary<string, string>? headers = null)
        : this(null, statusCode, body, contentType, headers) { }

    public HttpServerResponse(HttpServerRequestContext? request, int statusCode = 200, byte[]? body = null,
        string? contentType = null, IReadOnlyDictionary<string, string>? headers = null)
    {
        if (statusCode is < 100 or > 599) throw new ArgumentOutOfRangeException(nameof(statusCode));
        StatusCode = statusCode;
        Request = request;
        this.body = body is null ? Array.Empty<byte>() : (byte[])body.Clone();
        ContentType = contentType;
        Headers = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            (headers ?? new Dictionary<string, string>()).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
    }
    [Description("The originating request context. Required when sending this response through an HttpServer input.")]
    public HttpServerRequestContext? Request { get; }
    [Description("The HTTP status code to send.")]
    public int StatusCode { get; }
    [Description("A copy of the response body bytes.")]
    public byte[] Body => (byte[])body.Clone();
    [Description("The media type of the response body, or null when unspecified.")]
    public string? ContentType { get; }
    [Description("The HTTP headers to include in the response.")]
    public IReadOnlyDictionary<string, string> Headers { get; }
}
