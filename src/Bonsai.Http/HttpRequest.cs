using System.ComponentModel;
using System.Collections.ObjectModel;

namespace Bonsai.Http;

/// <summary>An immutable, repeatable description of an HTTP request.</summary>
public sealed class HttpRequest
{
    readonly byte[]? body;
    public HttpRequest(string method, string uri, IReadOnlyDictionary<string, string>? headers = null,
        byte[]? body = null, string? contentType = null)
    {
        Method = !string.IsNullOrWhiteSpace(method) ? method : throw new ArgumentException("Method is required.", nameof(method));
        Uri = !string.IsNullOrWhiteSpace(uri) ? uri : throw new ArgumentException("URI is required.", nameof(uri));
        Headers = new ReadOnlyDictionary<string, string>((headers ?? new Dictionary<string, string>()).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
        this.body = body is null ? null : (byte[])body.Clone();
        ContentType = contentType;
    }

    [Description("The HTTP request method.")]
    public string Method { get; }
    [Description("The absolute or relative request URI.")]
    public string Uri { get; }
    [Description("The request headers, which override client defaults with the same name.")]
    public IReadOnlyDictionary<string, string> Headers { get; }
    [Description("A copy of the request body bytes, or null when no body is present.")]
    public byte[]? Body => body is null ? null : (byte[])body.Clone();
    [Description("The media type of the request body, or null when unspecified.")]
    public string? ContentType { get; }

    public override string ToString() => $"{Method} {Uri}";
}
