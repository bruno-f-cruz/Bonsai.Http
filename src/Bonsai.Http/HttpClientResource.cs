using System.Diagnostics;
using System.Net.Http.Headers;

namespace Bonsai.Http;

/// <summary>A shared HTTP client owned by a Bonsai ResourceSubject.</summary>
public sealed class HttpClientResource : IDisposable
{
    readonly HttpClient client;
    volatile bool disposed;

    public HttpClientResource(Uri? baseAddress, IReadOnlyDictionary<string, string> defaultHeaders,
        TimeSpan? timeout, TimeSpan connectionLifetime, long maxResponseBodyBytes)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (connectionLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(connectionLifetime));
        if (maxResponseBodyBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxResponseBodyBytes));
        Timeout = timeout;
        MaxResponseBodyBytes = maxResponseBodyBytes;
        DefaultHeaders = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(defaultHeaders, StringComparer.OrdinalIgnoreCase));
        client = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = connectionLifetime })
        {
            BaseAddress = baseAddress,
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };
    }

    public TimeSpan? Timeout { get; }
    public long MaxResponseBodyBytes { get; }
    public IReadOnlyDictionary<string, string> DefaultHeaders { get; }
    public Uri? BaseAddress => client.BaseAddress;

    public async Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var id = Guid.NewGuid();
        using var timeoutSource = Timeout is { } duration ? new CancellationTokenSource(duration) : null;
        using var linkedSource = timeoutSource is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        var token = linkedSource.Token;
        var watch = Stopwatch.StartNew();
        try
        {
            using var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Uri);
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
            await using var source = await nativeResponse.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var destination = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var count = await source.ReadAsync(buffer, token).ConfigureAwait(false);
                if (count == 0) break;
                if (destination.Length + count > MaxResponseBodyBytes)
                    throw new HttpResponseSizeException(id, MaxResponseBodyBytes);
                destination.Write(buffer, 0, count);
            }
            var responseHeaders = nativeResponse.Headers.Concat(nativeResponse.Content.Headers)
                .ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            var effectiveHeaders = new Dictionary<string, string>(DefaultHeaders, StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers) effectiveHeaders[header.Key] = header.Value;
            var effectiveContentType = message.Content?.Headers.ContentType?.ToString();
            var effectiveRequest = new HttpRequest(request.Method, effectiveUri.ToString(), effectiveHeaders,
                request.Body, effectiveContentType);
            watch.Stop();
            return new HttpResponse(effectiveRequest, id, effectiveUri, (int)nativeResponse.StatusCode,
                nativeResponse.ReasonPhrase, responseHeaders, destination.ToArray(), watch.Elapsed);
        }
        catch (OperationCanceledException) when (timeoutSource?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestTimeoutException(request, id, Timeout!.Value);
        }
    }

    static void AddHeader(HttpRequestMessage message, string name, string value)
    {
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
