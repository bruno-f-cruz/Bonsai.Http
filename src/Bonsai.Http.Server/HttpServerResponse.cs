namespace Bonsai.Http.Server;

/// <summary>An HTTP response to send to an incoming request.</summary>
public sealed class HttpServerResponse
{
    readonly byte[] body;
    public HttpServerResponse(int statusCode = 200, byte[]? body = null, string? contentType = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        if (statusCode is < 100 or > 599) throw new ArgumentOutOfRangeException(nameof(statusCode));
        StatusCode = statusCode;
        this.body = body is null ? Array.Empty<byte>() : (byte[])body.Clone();
        ContentType = contentType;
        Headers = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            (headers ?? new Dictionary<string, string>()).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
    }
    public int StatusCode { get; }
    public byte[] Body => (byte[])body.Clone();
    public string? ContentType { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }
}
