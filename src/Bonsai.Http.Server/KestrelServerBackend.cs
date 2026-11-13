#if !NETFRAMEWORK
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

namespace Bonsai.Http.Server;

internal sealed class KestrelServerBackend : IServerBackend
{
    readonly WebApplication application;
    readonly X509Certificate2? certificate;

    public KestrelServerBackend(Uri endpoint, Func<ServerRequest, Task<HttpServerResponse>> handle,
        string? certificatePath, string? certificatePassword)
    {
        if (endpoint.Scheme == "https")
        {
            if (string.IsNullOrWhiteSpace(certificatePath)) throw new ArgumentException("HTTPS requires a certificate path.", nameof(certificatePath));
            certificate = new X509Certificate2(certificatePath, certificatePassword);
        }
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = Array.Empty<string>() });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(endpoint.ToString());
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = null; // The shared handler enforces the configured body limit.
            if (certificate is not null) options.ConfigureHttpsDefaults(https => https.ServerCertificate = certificate);
        });
        application = builder.Build();
        application.Run(async context =>
        {
            var response = await handle(new ServerRequest
            {
                Method = context.Request.Method,
                Uri = new Uri($"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}"),
                Headers = context.Request.Headers.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
                Body = context.Request.Body,
                ContentLength = context.Request.ContentLength,
                Cancellation = context.RequestAborted
            }).ConfigureAwait(false);
            context.Response.StatusCode = response.StatusCode;
            if (response.ContentType is not null) context.Response.ContentType = response.ContentType;
            foreach (var header in response.Headers) context.Response.Headers[header.Key] = header.Value;
            var body = response.Body;
            if (context.Request.Method != "HEAD" && response.StatusCode != 204 && response.StatusCode != 304)
                await context.Response.Body.WriteAsync(body, context.RequestAborted).ConfigureAwait(false);
        });
    }

    public Task StartAsync(CancellationToken token) => application.StartAsync(token);
    public void Dispose()
    {
        try
        {
            application.StopAsync(new CancellationToken(true)).GetAwaiter().GetResult();
            application.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        finally { certificate?.Dispose(); }
    }
}
#endif
