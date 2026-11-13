using System.Reactive.Subjects;
using System.Reactive.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Bonsai.Http.Server;

/// <summary>A cross-platform Kestrel HTTP listener shared through a ResourceSubject.</summary>
public sealed class HttpServerResource : IDisposable
{
    readonly object gate = new();
    readonly List<Route> routes = new();
    readonly WebApplication application;
    readonly X509Certificate2? certificate;
    readonly long maxRequestBodyBytes;
    readonly TimeSpan requestDeadline;
    bool started;
    volatile bool disposed;

    sealed record Route(string Method, string Pattern, ISubject<HttpServerRequestContext> Requests);

    public HttpServerResource(string url, long maxRequestBodyBytes, TimeSpan requestDeadline,
        string? certificatePath = null, string? certificatePassword = null)
    {
        if (maxRequestBodyBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxRequestBodyBytes));
        if (requestDeadline <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestDeadline));
        this.maxRequestBodyBytes = maxRequestBodyBytes;
        this.requestDeadline = requestDeadline;
        var endpoint = new Uri(url, UriKind.Absolute);
        if (endpoint.Scheme is not ("http" or "https")) throw new ArgumentException("URL scheme must be HTTP or HTTPS.", nameof(url));
        if (endpoint.Scheme == "https")
        {
            if (string.IsNullOrWhiteSpace(certificatePath))
                throw new ArgumentException("A certificate path is required for HTTPS.", nameof(certificatePath));
            certificate = new X509Certificate2(certificatePath, certificatePassword);
        }
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(
            IPAddress.TryParse(endpoint.Host, out var address) ? address : IPAddress.Loopback,
            endpoint.Port,
            listen => { if (certificate is not null) listen.UseHttps(certificate); }));
        application = builder.Build();
        application.Run(HandleRequestAsync);
    }

    public IObservable<HttpServerRequestContext> RegisterRoute(string method, string pattern)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var subject = new Subject<HttpServerRequestContext>();
        var route = new Route(method.ToUpperInvariant(), pattern, subject);
        lock (gate)
        {
            if (routes.Any(item => item.Method == route.Method && item.Pattern == route.Pattern))
                throw new InvalidOperationException($"Duplicate HTTP route: {method} {pattern}");
            routes.Add(route);
        }
        return Observable.Create<HttpServerRequestContext>(observer =>
        {
            var subscription = subject.Subscribe(observer);
            return System.Reactive.Disposables.Disposable.Create(() =>
            {
                subscription.Dispose();
                lock (gate) routes.Remove(route);
                subject.Dispose();
            });
        });
    }

    public async Task StartAsync(CancellationToken token = default)
    {
        lock (gate)
        {
            if (started) return;
            started = true;
        }
        await application.StartAsync(token).ConfigureAwait(false);
    }

    async Task HandleRequestAsync(Microsoft.AspNetCore.Http.HttpContext context)
    {
        Route? selected = null;
        Dictionary<string, string>? values = null;
        var pathMatched = false;
        string[] allowed;
        lock (gate)
        {
            foreach (var route in routes)
            {
                if (!TryMatch(route.Pattern, context.Request.Path.Value ?? "/", out var candidate)) continue;
                pathMatched = true;
                if (route.Method == context.Request.Method)
                {
                    selected = route;
                    values = candidate;
                    break;
                }
            }
            allowed = routes.Where(route => TryMatch(route.Pattern, context.Request.Path.Value ?? "/", out _))
                .Select(route => route.Method).Distinct().ToArray();
        }
        if (selected is null)
        {
            context.Response.StatusCode = pathMatched ? 405 : 404;
            if (pathMatched) context.Response.Headers.Allow = string.Join(", ", allowed);
            return;
        }
        if (context.Request.ContentLength is { } length && length > maxRequestBodyBytes)
        {
            context.Response.StatusCode = 413;
            return;
        }
        using var buffer = new MemoryStream();
        var bytes = new byte[81920];
        while (true)
        {
            var count = await context.Request.Body.ReadAsync(bytes, context.RequestAborted).ConfigureAwait(false);
            if (count == 0) break;
            if (buffer.Length + count > maxRequestBodyBytes)
            {
                context.Response.StatusCode = 413;
                return;
            }
            buffer.Write(bytes, 0, count);
        }
        var headers = context.Request.Headers.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        var request = new HttpServerRequestContext(Guid.NewGuid(), context.Request.Method,
            new Uri($"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}"),
            headers, buffer.ToArray(), values!);
        selected.Requests.OnNext(request);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        deadline.CancelAfter(requestDeadline);
        try
        {
            var response = await request.ReplyTask.WaitAsync(deadline.Token).ConfigureAwait(false);
            context.Response.StatusCode = response.StatusCode;
            if (response.ContentType is { } contentType) context.Response.ContentType = contentType;
            foreach (var header in response.Headers) context.Response.Headers[header.Key] = header.Value;
            await context.Response.Body.WriteAsync(response.Body, context.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            request.Expire();
            context.Response.StatusCode = 500;
        }
        catch (OperationCanceledException) { request.Expire(); }
    }

    static bool TryMatch(string pattern, string path, out Dictionary<string, string> values)
    {
        values = new(StringComparer.OrdinalIgnoreCase);
        var left = pattern.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var right = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++)
        {
            if (left[i].StartsWith('{') && left[i].EndsWith('}')) values[left[i][1..^1]] = Uri.UnescapeDataString(right[i]);
            else if (!string.Equals(left[i], right[i], StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lock (gate)
        {
            foreach (var route in routes) route.Requests.OnCompleted();
            routes.Clear();
        }
        try
        {
            application.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            application.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        finally { certificate?.Dispose(); }
    }
}
