#if NETFRAMEWORK
using System.Net;

namespace Bonsai.Http.Server;

/// <summary>Framework host adapter; HTTPS uses the operating system's configured certificate binding.</summary>
internal sealed class FrameworkServerBackend : IServerBackend
{
    readonly HttpListener listener = new();
    readonly CancellationTokenSource shutdown = new();
    readonly Func<ServerRequest, Task<HttpServerResponse>> handle;

    public FrameworkServerBackend(Uri endpoint, Func<ServerRequest, Task<HttpServerResponse>> handle, string? certificatePath)
    {
        if (!string.IsNullOrEmpty(certificatePath))
            throw new PlatformNotSupportedException("On .NET Framework, configure the HTTPS certificate binding in the operating system and leave CertificatePath empty.");
        listener.Prefixes.Add(endpoint.ToString());
        this.handle = handle;
    }

    public Task StartAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        listener.Start();
        _ = AcceptAsync();
        return Task.CompletedTask;
    }

    async Task AcceptAsync()
    {
        while (!shutdown.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync().ConfigureAwait(false); }
            catch (Exception) when (shutdown.IsCancellationRequested) { return; }
            _ = Task.Run(() => HandleAsync(context));
        }
    }

    async Task HandleAsync(HttpListenerContext context)
    {
        using (shutdown.Token.Register(() => { try { context.Response.Abort(); } catch (ObjectDisposedException) { } }))
        {
            try
            {
                var request = context.Request;
                var response = await handle(new ServerRequest
                {
                    Method = request.HttpMethod,
                    Uri = request.Url,
                    Headers = request.Headers.AllKeys.ToDictionary(key => key, key => request.Headers.GetValues(key), StringComparer.OrdinalIgnoreCase),
                    Body = request.InputStream,
                    ContentLength = request.ContentLength64 < 0 ? null : (long?)request.ContentLength64,
                    Cancellation = shutdown.Token
                }).ConfigureAwait(false);
                context.Response.StatusCode = response.StatusCode;
                if (response.ContentType is not null) context.Response.ContentType = response.ContentType;
                foreach (var header in response.Headers) context.Response.Headers[header.Key] = header.Value;
                var body = response.Body;
                if (request.HttpMethod != "HEAD" && response.StatusCode != 204 && response.StatusCode != 304)
                {
                    context.Response.ContentLength64 = body.Length;
                    await context.Response.OutputStream.WriteAsync(body, 0, body.Length, shutdown.Token).ConfigureAwait(false);
                }
            }
            catch (Exception) { try { context.Response.StatusCode = 500; } catch (ObjectDisposedException) { } }
            finally { try { context.Response.Close(); } catch (ObjectDisposedException) { } }
        }
    }

    public void Dispose() { shutdown.Cancel(); listener.Close(); }
}
#endif
