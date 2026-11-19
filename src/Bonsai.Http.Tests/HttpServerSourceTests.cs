#if NET8_0
using Bonsai.Http.Server;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Xunit;

namespace Bonsai.Http.Tests;

public class HttpServerSourceTests
{
    [Fact]
    public async Task SourceExampleBuildsAndRespondsThroughItsNestedWorkflow()
    {
        using var reader = System.Xml.XmlReader.Create(Path.Combine(AppContext.BaseDirectory, "examples", "10-http-server.bonsai"));
        var workflow = (WorkflowBuilder)WorkflowBuilder.Serializer.Deserialize(reader)!;
        var address = Address();
        var source = workflow.Workflow.Select(node => (node.Value as Bonsai.Expressions.CombinatorBuilder)?.Combinator).OfType<HttpServer>().Single();
        source.Url = address;
        var completed = new TaskCompletionSource<HttpServerResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = Bonsai.Expressions.ExpressionBuilderGraphExtensions.BuildObservable<HttpServerResponse>(workflow.Workflow)
            .Subscribe(response => completed.TrySetResult(response));
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        Assert.Equal("{\"message\":\"Hello from Bonsai\"}", await GetAsync(client, address + "hello"));
        Assert.Equal("/hello", (await completed.Task.WaitAsync(TimeSpan.FromSeconds(5))).Request!.Uri.AbsolutePath);
    }

    static string Address()
    {
        var lease = new TcpListener(IPAddress.Loopback, 0);
        lease.Start();
        var port = ((IPEndPoint)lease.LocalEndpoint).Port;
        lease.Stop();
        return $"http://127.0.0.1:{port}/";
    }

    static async Task<string> GetAsync(System.Net.Http.HttpClient client, string uri)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await client.GetStringAsync(uri); }
            catch (System.Net.Http.HttpRequestException) when (attempt < 100) { await Task.Delay(20); }
        }
    }

    [Fact]
    public async Task SourceOwnsListenerAndCapturesConfigurationUntilDisposed()
    {
        var address = Address();
        var source = new HttpServer { Url = address, Method = "GET", Pattern = "/items/{id}" };
        Assert.IsAssignableFrom<Source<HttpServerRequestContext>>(source);
        using (source.Generate().Subscribe(request => request.Reply(new HttpServerResponse(body: Encoding.UTF8.GetBytes(request.RouteValues["id"])))) )
        {
            source.Url = "invalid";
            source.Pattern = "/changed";
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            Assert.Equal("42", await GetAsync(client, address + "items/42"));
            Assert.Equal("43", await GetAsync(client, address + "items/43"));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(address + "changed")).StatusCode);
        }
        // The same endpoint can be bound again after the source is disposed.
        using var replacement = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(2));
        await replacement.StartAsync();
    }

    [Fact]
    public async Task ResponseInputPairsConcurrentRepliesAndCompletionKeepsListening()
    {
        var address = Address();
        using var responses = new Subject<HttpServerResponse>();
        using var handlers = new System.Reactive.Disposables.CompositeDisposable();
        var contexts = new List<HttpServerRequestContext>();
        var source = new HttpServer { Url = address, Pattern = "/items/{id}" };
        using var subscription = source.Generate(responses).Subscribe(request =>
        {
            contexts.Add(request);
            handlers.Add(Observable.Timer(TimeSpan.FromMilliseconds(request.RouteValues["id"] == "first" ? 100 : 5))
                .Subscribe(_ => responses.OnNext(new HttpServerResponse(request, body: Encoding.UTF8.GetBytes(request.RouteValues["id"])) )));
        });
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var first = GetAsync(client, address + "items/first");
        var second = GetAsync(client, address + "items/second");
        Assert.Equal(new[] { "first", "second" }, await Task.WhenAll(first, second));
        Assert.Equal(2, contexts.Count);
        Assert.NotEqual(contexts[0].RequestId, contexts[1].RequestId);
        responses.OnCompleted();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(address + "missing")).StatusCode);
    }

    [Fact]
    public async Task ResponseWithoutAnOriginRaisesAnErrorAndStopsListener()
    {
        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var address = Address();
        using var responses = new Subject<HttpServerResponse>();
        using var subscription = new HttpServer { Url = address }.Generate(responses).Subscribe(_ => { }, e => error.TrySetResult(e));
        responses.OnNext(new HttpServerResponse());
        Assert.IsType<InvalidOperationException>(await error.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        using var replacement = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(2));
        await replacement.StartAsync();
    }
}
#endif
