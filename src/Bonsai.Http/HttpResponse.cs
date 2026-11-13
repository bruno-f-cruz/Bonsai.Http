namespace Bonsai.Http;

/// <summary>A fully materialized HTTP response that owns no native HTTP resources.</summary>
public sealed class HttpResponse
{
    readonly byte[] body;
    public HttpResponse(HttpRequest request, Guid requestId, Uri effectiveUri, int statusCode,
        string? reasonPhrase, IReadOnlyDictionary<string, string[]> headers, byte[] body, TimeSpan duration)
    {
        Request = request;
        RequestId = requestId;
        EffectiveUri = effectiveUri;
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        Headers = new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            headers.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)Array.AsReadOnly((string[])pair.Value.Clone()), StringComparer.OrdinalIgnoreCase));
        this.body = (byte[])body.Clone();
        Duration = duration;
    }

    public HttpRequest Request { get; }
    public Guid RequestId { get; }
    public Uri EffectiveUri { get; }
    public int StatusCode { get; }
    public string? ReasonPhrase { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }
    public byte[] Body => (byte[])body.Clone();
    public TimeSpan Duration { get; }
    public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;
    public override string ToString() => $"{StatusCode} {Request.Method} {EffectiveUri}";
}
