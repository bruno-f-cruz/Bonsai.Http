#if NET8_0
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text;
using Bonsai.Http.Server;
using Xunit;

namespace Bonsai.Http.Tests;

public class HttpTests
{
    static string Address()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return $"http://127.0.0.1:{port}/";
    }

    static HttpClient Client(string address, TimeSpan? timeout = null, long limit = 1024) =>
        new(new Uri(address), new Dictionary<string, string>(), timeout, TimeSpan.FromMinutes(5), limit);

    [Fact]
    public async Task RequestResponseRetainsCorrelationAndStatusIsExplicit()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(2));
        using var route = server.RegisterRoute("GET", "/animals/{id}").Subscribe(request =>
        {
            Assert.Equal("otter", request.RouteValues["id"]);
            request.Reply(new HttpServerResponse(200, Encoding.UTF8.GetBytes("hello"), "text/plain"));
        });
        await server.StartAsync();
        using var client = Client(address);
        var response = await client.SendAsync(new HttpRequest("GET", "animals/otter"));
        Assert.Equal("hello", response.ReadText());
        Assert.Equal(address + "animals/otter", response.Request.Uri);
        Assert.NotEqual(Guid.Empty, response.RequestId);
        var missing = await client.SendAsync(new HttpRequest("GET", "missing"));
        Assert.Equal(404, missing.StatusCode);
        var error = await Assert.ThrowsAsync<HttpStatusException>(async () =>
            await new EnsureSuccessStatusCode().Process(Observable.Return(missing)).FirstAsync());
        Assert.Same(missing, error.Response);
        var wrongMethod = await client.SendAsync(new HttpRequest("POST", "animals/otter"));
        Assert.Equal(405, wrongMethod.StatusCode);
        Assert.Contains("GET", wrongMethod.Headers["Allow"]);
    }

    [Fact]
    public async Task ClientTimeoutAndCancellationHaveDifferentErrors()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(5));
        using var route = server.RegisterRoute("GET", "/").Subscribe(_ => { });
        await server.StartAsync();
        using var timed = Client(address, TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAsync<HttpRequestTimeoutException>(() => timed.SendAsync(new HttpRequest("GET", "/")));
        using var unlimited = Client(address);
        using var cancel = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => unlimited.SendAsync(new HttpRequest("GET", "/"), cancel.Token));
    }

    [Fact]
    public async Task BodyLimitsAreEnforcedOnBothSides()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 4, TimeSpan.FromSeconds(2));
        using var route = server.RegisterRoute("POST", "/").Subscribe(request => request.Reply(new HttpServerResponse(200, new byte[32])));
        await server.StartAsync();
        using var client = Client(address, limit: 16);
        var oversizedRequest = await client.SendAsync(new HttpRequest("POST", "/", body: new byte[8]));
        Assert.Equal(413, oversizedRequest.StatusCode);
        await Assert.ThrowsAsync<HttpResponseSizeException>(() => client.SendAsync(new HttpRequest("POST", "/", body: new byte[1])));
    }

    [Fact]
    public async Task OverflowErrorsImmediatelyWhileActiveRequestNeverReplies()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(5));
        using var route = server.RegisterRoute("GET", "/").Subscribe(_ => { });
        await server.StartAsync();
        using var client = Client(address);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new SendRequest { MaxConcurrency = 1, MaxPendingRequests = 1 }
                .Process(Observable.Range(0, 4).Select(_ => new HttpRequest("GET", "/")), Observable.Return(client))
                .Timeout(TimeSpan.FromSeconds(2)).LastAsync());
        Assert.Equal("HTTP request queue is full.", error.Message);
    }

    [Fact]
    public async Task ConcurrentRequestsSerializeNotificationsAndPreserveReplies()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(5));
        var entered = 0;
        var overlap = 0;
        using var route = server.RegisterRoute("GET", "/{id}").Subscribe(request =>
        {
            if (Interlocked.Increment(ref entered) != 1) Interlocked.Increment(ref overlap);
            Thread.Sleep(2);
            request.Reply(new HttpServerResponse(200, Encoding.UTF8.GetBytes(request.RouteValues["id"])));
            Interlocked.Decrement(ref entered);
        });
        await Task.WhenAll(server.StartAsync(), server.StartAsync());
        using var client = Client(address);
        var responses = await Task.WhenAll(Enumerable.Range(0, 24).Select(i => client.SendAsync(new HttpRequest("GET", i.ToString()))));
        Assert.Equal(0, overlap);
        for (var i = 0; i < responses.Length; i++) Assert.Equal(i.ToString(), responses[i].ReadText());
    }

    [Fact]
    public async Task ShutdownAllowsCompletionCallbacksToUnregisterRoutes()
    {
        var server = new HttpServerResource(Address(), 1024, TimeSpan.FromSeconds(2));
        IDisposable? first = null;
        IDisposable? second = null;
        first = server.RegisterRoute("GET", "/first").Subscribe(_ => { }, () => first?.Dispose());
        second = server.RegisterRoute("GET", "/second").Subscribe(_ => { }, () => second?.Dispose());
        await server.StartAsync();
        server.Dispose();
        server.Dispose();
    }

    [Fact]
    public async Task RequestsWaitForDelayedClientWithoutDroppingHotEvents()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(2));
        using var route = server.RegisterRoute("GET", "/").Subscribe(request => request.Reply(new HttpServerResponse(200)));
        await server.StartAsync();
        using var client = Client(address);
        using var requests = new Subject<HttpRequest>();
        using var clients = new Subject<HttpClient>();
        var result = new SendRequest().Process(requests, clients).Timeout(TimeSpan.FromSeconds(2)).FirstAsync().ToTask();
        requests.OnNext(new HttpRequest("GET", "/"));
        clients.OnNext(client);
        Assert.Equal(200, (await result).StatusCode);
    }

    [Fact]
    public async Task DisposingHandlerCompletesPendingRequestWithServerError()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(30));
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var route = server.RegisterRoute("GET", "/").Subscribe(_ => received.TrySetResult(true));
        await server.StartAsync();
        using var client = Client(address, TimeSpan.FromSeconds(3));
        var response = client.SendAsync(new HttpRequest("GET", "/"));
        await received.Task;
        route.Dispose();
        Assert.Equal(500, (await response).StatusCode);
    }

}
#endif
