using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Bonsai.Http;

/// <summary>A shared HTTP client owned by a Bonsai ResourceSubject.</summary>
public sealed class HttpClientResource : IDisposable
{
    readonly System.Net.Http.HttpClient client;
    readonly TimeSpan connectionLifetime;
    volatile bool disposed;

    public HttpClientResource(Uri? baseAddress, IReadOnlyDictionary<string, string> defaultHeaders,
        TimeSpan? timeout, TimeSpan connectionLifetime, long maxResponseBodyBytes)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (connectionLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(connectionLifetime));
        if (maxResponseBodyBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxResponseBodyBytes));
        Timeout = timeout;
        MaxResponseBodyBytes = maxResponseBodyBytes;
        this.connectionLifetime = connectionLifetime;
        DefaultHeaders = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            defaultHeaders.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
#if NETFRAMEWORK
        var handler = new HttpClientHandler();
#else
        var handler = new SocketsHttpHandler { PooledConnectionLifetime = connectionLifetime };
#endif
        client = new System.Net.Http.HttpClient(handler)
        {
            BaseAddress = baseAddress,
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };
    }

    [Description("The request timeout, including reading the response body; null means no timeout.")]
    public TimeSpan? Timeout { get; }
    [Description("The maximum buffered response body size in bytes.")]
    public long MaxResponseBodyBytes { get; }
    [Description("The default headers applied unless overridden by a request.")]
    public IReadOnlyDictionary<string, string> DefaultHeaders { get; }
    [Description("The absolute base URI used to resolve relative request URIs.")]
    public Uri? BaseAddress => client.BaseAddress;

    public async Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        if (disposed) throw new ObjectDisposedException(nameof(HttpClientResource));
        var id = Guid.NewGuid();
        using var timeoutSource = Timeout is { } duration ? new CancellationTokenSource(duration) : null;
        using var linkedSource = timeoutSource is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        var token = linkedSource.Token;
        var watch = Stopwatch.StartNew();
        try
        {
            var requestUri = new Uri(request.Uri, UriKind.RelativeOrAbsolute);
            var resolvedUri = requestUri.IsAbsoluteUri ? requestUri : new Uri(client.BaseAddress ?? throw new InvalidOperationException("Relative requests require BaseAddress."), requestUri);
            using var message = new HttpRequestMessage(new HttpMethod(request.Method), resolvedUri);
#if NETFRAMEWORK
            System.Net.ServicePointManager.FindServicePoint(resolvedUri).ConnectionLeaseTimeout =
                (int)Math.Min(int.MaxValue, connectionLifetime.TotalMilliseconds);
#endif
            if (request.Body is { } body) message.Content = new ByteArrayContent(body);
            if (request.ContentType is { } contentType)
            {
                message.Content ??= new ByteArrayContent(Array.Empty<byte>());
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
            foreach (var pair in DefaultHeaders)
            {
                if (request.Headers.ContainsKey(pair.Key)) continue;
                AddHeader(message, pair.Key, pair.Value);
            }
            foreach (var pair in request.Headers)
            {
                AddHeader(message, pair.Key, pair.Value);
            }
            using var nativeResponse = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            var effectiveUri = nativeResponse.RequestMessage?.RequestUri ?? message.RequestUri!;
            if (nativeResponse.Content.Headers.ContentLength is { } length && length > MaxResponseBodyBytes)
                throw new HttpResponseSizeException(id, MaxResponseBodyBytes);
#if NETFRAMEWORK
            using var source = await nativeResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
#else
            using var source = await nativeResponse.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
#endif
            using var destination = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var count = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (count == 0) break;
                if (destination.Length + count > MaxResponseBodyBytes)
                    throw new HttpResponseSizeException(id, MaxResponseBodyBytes);
                destination.Write(buffer, 0, count);
            }
            var responseHeaders = nativeResponse.Headers.Concat(nativeResponse.Content.Headers)
                .ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            var effectiveHeaders = DefaultHeaders.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers) effectiveHeaders[header.Key] = header.Value;
            var effectiveContentType = message.Content?.Headers.ContentType?.ToString();
            var effectiveRequest = new HttpRequest(request.Method, resolvedUri.ToString(), effectiveHeaders,
                request.Body, effectiveContentType);
            watch.Stop();
            return new HttpResponse(effectiveRequest, id, effectiveUri, (int)nativeResponse.StatusCode,
                nativeResponse.ReasonPhrase, responseHeaders, destination.ToArray(), watch.Elapsed, request);
        }
        catch (OperationCanceledException) when (timeoutSource?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestTimeoutException(request, id, Timeout!.Value);
        }
    }

    static void AddHeader(HttpRequestMessage message, string name, string value)
    {
        const string separators = "()<>@,;:\\\"/[]?={} \t";
        if (string.IsNullOrEmpty(name) || name.Any(character => character <= 32 || character >= 127 || separators.IndexOf(character) >= 0))
            throw new ArgumentException("Invalid HTTP header name.", nameof(name));
        if (value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0) throw new ArgumentException("HTTP header values cannot contain line breaks or NUL.", nameof(value));
        if (message.Headers.TryAddWithoutValidation(name, value)) return;
        message.Content ??= new ByteArrayContent(Array.Empty<byte>());
        if (!message.Content.Headers.TryAddWithoutValidation(name, value))
            throw new ArgumentException($"Invalid HTTP header: {name}", nameof(name));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        client.Dispose();
    }
}
