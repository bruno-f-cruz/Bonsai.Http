namespace Bonsai.Http.Server;

/// <summary>A request and its one-use reply capability.</summary>
public sealed class HttpServerRequestContext
{
    readonly TaskCompletionSource<HttpServerResponse> reply = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly byte[] body;
    int replyState;

    internal HttpServerRequestContext(Guid id, string method, Uri uri, IReadOnlyDictionary<string, string[]> headers,
        byte[] body, IReadOnlyDictionary<string, string> routeValues)
    {
        RequestId = id;
        Method = method;
        Uri = uri;
        Headers = new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            headers.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)Array.AsReadOnly((string[])pair.Value.Clone()), StringComparer.OrdinalIgnoreCase));
        this.body = (byte[])body.Clone();
        RouteValues = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(routeValues, StringComparer.OrdinalIgnoreCase));
    }

    public Guid RequestId { get; }
    public string Method { get; }
    public Uri Uri { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }
    public byte[] Body => (byte[])body.Clone();
    public IReadOnlyDictionary<string, string> RouteValues { get; }
    internal Task<HttpServerResponse> ReplyTask => reply.Task;

    public void Reply(HttpServerResponse response)
    {
        if (Interlocked.CompareExchange(ref replyState, 1, 0) != 0)
            throw new InvalidOperationException($"Request {RequestId} has already been answered.");
        reply.TrySetResult(response);
    }

    internal void Expire() => Interlocked.CompareExchange(ref replyState, 2, 0);
}
