using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Bonsai.Http.Server;

/// <summary>A listener resource with shared routing, bounded bodies, and serialized route notifications.</summary>
public sealed class HttpServerResource : IDisposable
{
    readonly object gate = new();
    readonly List<Route> routes = new();
    readonly CancellationTokenSource shutdown = new();
    readonly IServerBackend backend;
    readonly long maxRequestBodyBytes;
    readonly TimeSpan requestDeadline;
    Task? startTask;
    bool disposed;

    sealed class Route
    {
        readonly object notifications = new();
        readonly Subject<HttpServerRequestContext> subject = new();
        readonly HashSet<HttpServerRequestContext> pending = new();
        bool closed;
        public Route(string method, string pattern) { Method = method; Pattern = pattern; }
        public string Method { get; }
        public string Pattern { get; }
        public IDisposable Subscribe(IObserver<HttpServerRequestContext> observer) => subject.Subscribe(observer);
        public bool Publish(HttpServerRequestContext request)
        {
            lock (notifications)
            {
                if (closed) return false;
                pending.Add(request);
                subject.OnNext(request);
                return true;
            }
        }
        public void Release(HttpServerRequestContext request) { lock (notifications) pending.Remove(request); }
        public void Complete()
        {
            lock (notifications)
            {
                if (closed) return;
                closed = true;
                foreach (var request in pending) request.Fail();
                pending.Clear();
                subject.OnCompleted();
            }
        }
    }

    public HttpServerResource(string url, long maxRequestBodyBytes, TimeSpan requestDeadline,
        string? certificatePath = null, string? certificatePassword = null)
    {
        if (maxRequestBodyBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxRequestBodyBytes));
        if (requestDeadline <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestDeadline));
        this.maxRequestBodyBytes = maxRequestBodyBytes;
        this.requestDeadline = requestDeadline;
        var endpoint = new Uri(url, UriKind.Absolute);
        if (endpoint.Scheme != "http" && endpoint.Scheme != "https") throw new ArgumentException("URL must use HTTP or HTTPS.", nameof(url));
        if (endpoint.AbsolutePath != "/" || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
            throw new ArgumentException("The server URL must specify an origin without a path, query, or fragment.", nameof(url));
#if NETFRAMEWORK
        backend = new FrameworkServerBackend(endpoint, HandleRequestAsync, certificatePath);
#else
        backend = new KestrelServerBackend(endpoint, HandleRequestAsync, certificatePath, certificatePassword);
#endif
    }

    public IObservable<HttpServerRequestContext> RegisterRoute(string method, string pattern)
    {
        if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("An HTTP method is required.", nameof(method));
        ValidatePattern(pattern);
        return Observable.Create<HttpServerRequestContext>(observer =>
        {
            var route = new Route(method.ToUpperInvariant(), pattern);
            var subscription = route.Subscribe(observer);
            lock (gate)
            {
                if (disposed) { subscription.Dispose(); throw new ObjectDisposedException(nameof(HttpServerResource)); }
                if (routes.Any(item => item.Method == route.Method && item.Pattern == route.Pattern))
                { subscription.Dispose(); throw new InvalidOperationException($"Duplicate HTTP route: {method} {pattern}"); }
                routes.Add(route);
            }
            return Disposable.Create(() =>
            {
                lock (gate) routes.Remove(route);
                subscription.Dispose();
                route.Complete();
            });
        });
    }

    public Task StartAsync(CancellationToken token = default)
    {
        lock (gate)
        {
            if (disposed) throw new ObjectDisposedException(nameof(HttpServerResource));
            return startTask ??= backend.StartAsync(token);
        }
    }

    async Task<HttpServerResponse> HandleRequestAsync(ServerRequest incoming)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(incoming.Cancellation, shutdown.Token);
        deadline.CancelAfter(requestDeadline);
        HttpServerRequestContext? request = null;
        Route? selected = null;
        try
        {
            Dictionary<string, string>? values = null;
            Route[] snapshot;
            lock (gate) snapshot = routes.ToArray();
            var matches = snapshot.Where(route => TryMatch(route.Pattern, incoming.Uri.AbsolutePath, out _)).ToArray();
            selected = matches.FirstOrDefault(route => route.Method == incoming.Method);
            if (selected is null)
                return new HttpServerResponse(matches.Length == 0 ? 404 : 405, headers:
                    matches.Length == 0 ? null : new Dictionary<string, string> { ["Allow"] = string.Join(", ", matches.Select(route => route.Method).Distinct()) });
            TryMatch(selected.Pattern, incoming.Uri.AbsolutePath, out values);
            if (incoming.ContentLength > maxRequestBodyBytes) return new HttpServerResponse(413);
            using var buffer = new MemoryStream();
            var bytes = new byte[81920];
            while (true)
            {
                var count = await incoming.Body.ReadAsync(bytes, 0, bytes.Length, deadline.Token).ConfigureAwait(false);
                if (count == 0) break;
                if (buffer.Length + count > maxRequestBodyBytes) return new HttpServerResponse(413);
                buffer.Write(bytes, 0, count);
            }
            request = new HttpServerRequestContext(Guid.NewGuid(), incoming.Method, incoming.Uri,
                incoming.Headers, buffer.ToArray(), values!);
            // Serialize observer calls per route, but allow replies to complete independently.
            if (!selected.Publish(request)) return new HttpServerResponse(404);
            return await AwaitReplyAsync(request.ReplyTask, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return new HttpServerResponse(500); }
        catch (Exception) { return new HttpServerResponse(500); }
        finally
        {
            if (request is not null) { request.Expire(); selected?.Release(request); }
        }
    }

    static async Task<HttpServerResponse> AwaitReplyAsync(Task<HttpServerResponse> reply, CancellationToken token)
    {
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (token.Register(() => canceled.TrySetResult(true)))
        {
            if (await Task.WhenAny(reply, canceled.Task).ConfigureAwait(false) != reply) token.ThrowIfCancellationRequested();
            return await reply.ConfigureAwait(false);
        }
    }

    static void ValidatePattern(string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || !pattern.StartsWith("/") || pattern.Contains("?") || pattern.Contains("#"))
            throw new ArgumentException("A route must be an absolute path template.", nameof(pattern));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in pattern.Split('/'))
        {
            if (!segment.Contains("{") && !segment.Contains("}")) continue;
            if (!segment.StartsWith("{") || !segment.EndsWith("}") || segment.Length < 3 ||
                segment.Substring(1, segment.Length - 2).IndexOfAny(new[] { '{', '}' }) >= 0 ||
                !names.Add(segment.Substring(1, segment.Length - 2)))
                throw new ArgumentException("Route parameters must be unique whole path segments.", nameof(pattern));
        }
    }

    static bool TryMatch(string pattern, string path, out Dictionary<string, string> values)
    {
        values = new(StringComparer.OrdinalIgnoreCase);
        var left = pattern.Trim('/').Split('/');
        var right = path.Trim('/').Split('/');
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++)
        {
            if (left[i].StartsWith("{") && left[i].EndsWith("}")) values[left[i].Substring(1, left[i].Length - 2)] = Uri.UnescapeDataString(right[i]);
            else if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
        }
        return true;
    }

    public void Dispose()
    {
        Route[] snapshot;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            snapshot = routes.ToArray();
            routes.Clear();
        }
        shutdown.Cancel();
        try { foreach (var route in snapshot) route.Complete(); }
        finally { backend.Dispose(); }
    }
}

internal interface IServerBackend : IDisposable { Task StartAsync(CancellationToken token); }
internal sealed class ServerRequest
{
    public string Method = null!;
    public Uri Uri = null!;
    public IReadOnlyDictionary<string, string[]> Headers = null!;
    public Stream Body = null!;
    public long? ContentLength;
    public CancellationToken Cancellation;
}
