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
        Headers = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(headers ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase));
        this.body = body is null ? null : (byte[])body.Clone();
        ContentType = contentType;
    }

    public string Method { get; }
    public string Uri { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }
    public byte[]? Body => body is null ? null : (byte[])body.Clone();
    public string? ContentType { get; }

    public override string ToString() => $"{Method} {Uri}";
}
