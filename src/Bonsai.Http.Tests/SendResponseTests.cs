#if NET8_0
using Bonsai.Expressions;
using Bonsai.Http.Server;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using Xunit;
using NativeHttpClient = System.Net.Http.HttpClient;

namespace Bonsai.Http.Tests;

public class SendResponseTests
{
    static string Address()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return $"http://127.0.0.1:{port}/";
    }

    static SendResponse Response<T>(Func<HttpServerRequestContext, IObservable<T>> body)
    {
        var send = new SendResponse();
        send.Workflow.Add(new WorkflowInputBuilder());
        send.Workflow.Add(new CombinatorBuilder { Combinator = new Body<T>(body) });
        send.Workflow.Add(new WorkflowOutputBuilder());
        send.Workflow.AddEdge(send.Workflow[0], send.Workflow[1], new ExpressionBuilderArgument());
        send.Workflow.AddEdge(send.Workflow[1], send.Workflow[2], new ExpressionBuilderArgument());
        return send;
    }

    static IObservable<HttpServerRequestContext> Build(SendResponse send, IObservable<HttpServerRequestContext> source)
        => BuildPaired(send, source).Select(response =>
        {
            response.Request!.Reply(response);
            return response.Request;
        });

    static IObservable<HttpServerResponse> BuildPaired(SendResponse send, IObservable<HttpServerRequestContext> source)
    {
        var workflow = new WorkflowBuilder();
        workflow.Workflow.Add(new CombinatorBuilder { Combinator = new Requests(source) });
        workflow.Workflow.Add(send);
        workflow.Workflow.Add(new WorkflowOutputBuilder());
        workflow.Workflow.AddEdge(workflow.Workflow[0], workflow.Workflow[1], new ExpressionBuilderArgument());
        workflow.Workflow.AddEdge(workflow.Workflow[1], workflow.Workflow[2], new ExpressionBuilderArgument());
        return workflow.Workflow.BuildObservable<HttpServerResponse>();
    }

    [Fact]
    public async Task NestedOutputWaitsForTheServerToSendItsPairedResponse()
    {
        var address = Address();
        using var responses = new System.Reactive.Subjects.Subject<HttpServerResponse>();
        var ready = new TaskCompletionSource<HttpServerResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new HttpServer { Url = address };
        using var subscription = BuildPaired(Response(_ => Observable.Return("paired")), server.Generate(responses))
            .Subscribe(response => ready.TrySetResult(response));
        using var client = new NativeHttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var pending = Observable.Defer(() => Observable.FromAsync(() => client.GetStringAsync(address)))
            .Catch<string, System.Net.Http.HttpRequestException>(_ => Observable.Timer(TimeSpan.FromMilliseconds(20))
                .SelectMany(_ => Observable.FromAsync(() => client.GetStringAsync(address))))
            .ToTask();
        var response = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(response.Request);
        Assert.False(pending.IsCompleted);
        responses.OnNext(response);
        Assert.Equal("paired", await pending);
    }

    [Theory]
    [InlineData(null, "utf-8")]
    [InlineData("UTF-8", "utf-8")]
    [InlineData("utf-16", "utf-16")]
    [InlineData("iso-8859-1", "iso-8859-1")]
    public async Task StringsUseSelectedEncodingAndCaptureResponseProperties(string? name, string expectedEncoding)
    {
        const string text = "Ol\u00e1, caf\u00e9!";
        var address = Address();
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(3));
        var send = Response(_ => Observable.Return(text));
        send.Encoding = name;
        send.StatusCode = 201;
        send.ContentType = "text/plain";
        send.Headers.Add(new HttpHeader { Name = "X-Example", Value = "first" });
        using var subscription = Build(send, server.RegisterRoute("GET", "/")).Subscribe();
        send.StatusCode = 202;
        send.Encoding = "us-ascii";
        send.Headers[0].Value = "second";
        await server.StartAsync();
        using var client = new NativeHttpClient();
        using var response = await client.GetAsync(address);
        Assert.Equal(201, (int)response.StatusCode);
        Assert.Equal("first", response.Headers.GetValues("X-Example").Single());
        Assert.Equal(expectedEncoding, response.Content.Headers.ContentType!.CharSet);
        Assert.Equal(Encoding.GetEncoding(expectedEncoding).GetBytes(text), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ByteArraysAreSentUnchanged()
    {
        var bytes = new byte[] { 0, 255, 128, 42 };
        var address = Address();
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(3));
        var send = Response(_ => Observable.Return(bytes));
        send.Encoding = "not-an-encoding"; // Only string output uses Encoding.
        using var subscription = Build(send, server.RegisterRoute("GET", "/")).Subscribe();
        await server.StartAsync();
        using var client = new NativeHttpClient();
        using var response = await client.GetAsync(address);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task FirstOutputRepliesAndEmptyCompletionSendsEmptyBody()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(3));
        var send = Response(request => request.RouteValues["id"] == "empty" ? Observable.Empty<string>()
            : Observable.Return("first").Concat(Observable.Throw<string>(new Exception("The second output must not run."))));
        using var subscription = Build(send, server.RegisterRoute("GET", "/{id}")).Subscribe();
        await server.StartAsync();
        using var client = new NativeHttpClient();
        Assert.Equal("first", await client.GetStringAsync(address + "first"));
        Assert.Equal(string.Empty, await client.GetStringAsync(address + "empty"));
    }

    [Fact]
    public async Task ConcurrentNestedWorkflowsKeepTheirRequestContexts()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(3));
        var send = Response(request => Observable.Timer(TimeSpan.FromMilliseconds(request.RouteValues["id"] == "first" ? 80 : 5))
            .Select(_ => request.RouteValues["id"]));
        using var subscription = Build(send, server.RegisterRoute("GET", "/{id}")).Subscribe();
        await server.StartAsync();
        using var client = new NativeHttpClient();
        var results = await Task.WhenAll(client.GetStringAsync(address + "first"), client.GetStringAsync(address + "second"));
        Assert.Equal(new[] { "first", "second" }, results);
    }

    [Fact]
    public async Task DeadlineDisposesAnUnfinishedNestedWorkflow()
    {
        var disposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var address = Address();
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromMilliseconds(100));
        var send = Response(_ => Observable.Never<string>().Finally(() => disposed.TrySetResult(true)));
        using var subscription = Build(send, server.RegisterRoute("GET", "/")).Subscribe();
        await server.StartAsync();
        using var client = new NativeHttpClient();
        using var response = await client.GetAsync(address);
        Assert.Equal(500, (int)response.StatusCode);
        Assert.True(await disposed.Task.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task NestedErrorsFailTheRequestAndTheSequence()
    {
        var failure = new InvalidOperationException("Response failed.");
        var seen = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var address = Address();
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(3));
        var send = Response(_ => Observable.Throw<string>(failure));
        using var subscription = Build(send, server.RegisterRoute("GET", "/")).Subscribe(_ => { }, error => seen.TrySetResult(error));
        await server.StartAsync();
        using var client = new NativeHttpClient();
        using var response = await client.GetAsync(address);
        Assert.Equal(500, (int)response.StatusCode);
        Assert.Same(failure, await seen.Task.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void OtherBodyTypesAreRejectedWhenBuilding()
    {
        var error = Assert.ThrowsAny<Exception>(() => Build(Response(_ => Observable.Return(42)), Observable.Never<HttpServerRequestContext>()));
        Assert.Contains("string or byte array", error.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("UTF-8")]
    [InlineData("utf-16")]
    public void EncodingAndNestedWorkflowRoundTrip(string? encoding)
    {
        using var reader = System.Xml.XmlReader.Create(Path.Combine(AppContext.BaseDirectory, "examples", "10-http-server.bonsai"));
        var workflow = (WorkflowBuilder)WorkflowBuilder.Serializer.Deserialize(reader)!;
        workflow.Workflow.Select(node => node.Value).OfType<SendResponse>().Single().Encoding = encoding;
        using var writer = new StringWriter();
        WorkflowBuilder.Serializer.Serialize(writer, workflow);
        using var restoredReader = System.Xml.XmlReader.Create(new StringReader(writer.ToString()));
        var restored = (WorkflowBuilder)WorkflowBuilder.Serializer.Deserialize(restoredReader)!;
        Assert.Equal(encoding, restored.Workflow.Select(node => node.Value).OfType<SendResponse>().Single().Encoding);
        Assert.NotNull(restored.Workflow.BuildObservable<HttpServerResponse>());
    }

    [Fact]
    public void EncodingPropertyOffersStandardNamesWithoutRestrictingFutureValues()
    {
        var property = System.ComponentModel.TypeDescriptor.GetProperties(typeof(SendResponse))[nameof(SendResponse.Encoding)]!;
        Assert.True(property.Converter.GetStandardValuesSupported());
        Assert.False(property.Converter.GetStandardValuesExclusive());
        Assert.Contains("utf-8", property.Converter.GetStandardValues()!.Cast<string>());
    }

    public sealed class Requests(IObservable<HttpServerRequestContext> source) : Source<HttpServerRequestContext>
    {
        public override IObservable<HttpServerRequestContext> Generate() => source;
    }

    public sealed class Body<T>(Func<HttpServerRequestContext, IObservable<T>> body) : Combinator<HttpServerRequestContext, T>
    {
        public override IObservable<T> Process(IObservable<HttpServerRequestContext> source) => source.SelectMany(body);
    }
}
#endif
