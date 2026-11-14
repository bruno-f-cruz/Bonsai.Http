using Bonsai.Expressions;
using System.Xml;
using System.Reactive.Linq;
using System.Net;
using System.Net.Sockets;
using Bonsai.Http.Server;
using Xunit;

namespace Bonsai.Http.Tests;

public class WorkflowTests
{
    [Theory]
    [InlineData(ResponseFormat.Text)]
    [InlineData(ResponseFormat.Json)]
    public void DecoderFormatRoundTripsAndBuildsWithCorrectOutputType(ResponseFormat format)
    {
        var workflow = Load("08-response-text.bonsai");
        var decoder = workflow.Workflow.Select(node => node.Value).OfType<DecodeResponse>().Single();
        decoder.Format = format;
        using var writer = new StringWriter();
        WorkflowBuilder.Serializer.Serialize(writer, workflow);
        using var reader = XmlReader.Create(new StringReader(writer.ToString()));
        var restored = (WorkflowBuilder)WorkflowBuilder.Serializer.Deserialize(reader)!;
        Assert.Equal(format, restored.Workflow.Select(node => node.Value).OfType<DecodeResponse>().Single().Format);
        if (format == ResponseFormat.Text) Assert.NotNull(restored.Workflow.BuildObservable<string>());
        else Assert.NotNull(restored.Workflow.BuildObservable<System.Text.Json.Nodes.JsonNode>());
    }

    [Fact]
    public async Task RequestGeneratorWithoutInputEmitsOnceAndCompletesInBonsai()
    {
        var workflow = new WorkflowBuilder();
        workflow.Workflow.Add(new CombinatorBuilder { Combinator = new CreateHttpRequest { Uri = "https://example.com/" } });
        workflow.Workflow.Add(new WorkflowOutputBuilder());
        workflow.Workflow.AddEdge(workflow.Workflow[0], workflow.Workflow[1], new ExpressionBuilderArgument());
        var requests = await workflow.Workflow.BuildObservable<HttpRequest>().Timeout(TimeSpan.FromSeconds(2)).ToList();
        Assert.Equal("https://example.com/", Assert.Single(requests).Uri);
    }

    [Fact]
    public void TriggeredRequestGeneratorCapturesPropertiesOncePerSubscription()
    {
        var generator = new CreateHttpRequest { Uri = "/first", Body = "first" };
        using var ticks = new System.Reactive.Subjects.Subject<int>();
        var requests = new List<HttpRequest>();
        var completed = false;
        var sequence = generator.Generate(ticks);
        using var subscription = sequence.Subscribe(requests.Add, () => completed = true);
        Assert.Empty(requests);
        ticks.OnNext(0);
        generator.Uri = "/second";
        generator.Body = "second";
        ticks.OnNext(1);
        ticks.OnCompleted();
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request =>
        {
            Assert.Equal("/first", request.Uri);
            Assert.Equal("first", System.Text.Encoding.UTF8.GetString(request.Body!));
        });
        Assert.True(completed);
        Assert.Equal("/second", generator.Generate().SingleAsync().Wait().Uri);
    }

    [Fact]
    public void ClientPropertiesRoundTripThroughBonsaiSerializer()
    {
        var workflow = new WorkflowBuilder();
        workflow.Workflow.Add(new CombinatorBuilder { Combinator = new CreateHttpClient { BaseAddress = "https://example.com/", Timeout = TimeSpan.FromSeconds(7), DefaultHeaders = new List<HttpHeader> { new HttpHeader { Name = "Accept", Value = "application/json" } } } });
        workflow.Workflow.Add(new Bonsai.Reactive.ResourceSubject { Name = "Api" });
        workflow.Workflow.Add(new SubscribeSubject { Name = "Api" });
        workflow.Workflow.Add(new CombinatorBuilder { Combinator = new Bonsai.Reactive.Timer { DueTime = TimeSpan.FromSeconds(1), Period = TimeSpan.FromSeconds(15) } });
        using var writer = new StringWriter();
        WorkflowBuilder.Serializer.Serialize(writer, workflow);
        using var reader = XmlReader.Create(new StringReader(writer.ToString()));
        var restored = (WorkflowBuilder)WorkflowBuilder.Serializer.Deserialize(reader)!;
        var node = Assert.IsType<CreateHttpClient>(((CombinatorBuilder)restored.Workflow[0].Value).Combinator);
        Assert.Equal(TimeSpan.FromSeconds(7), node.Timeout);
        Assert.Equal("Accept", node.DefaultHeaders[0].Name);
    }

    [Fact]
    public async Task ClientWorkflowExecutesWithOwnedClient()
    {
        var lease = new TcpListener(IPAddress.Loopback, 0);
        lease.Start();
        var port = ((IPEndPoint)lease.LocalEndpoint).Port;
        lease.Stop();
        var address = $"http://127.0.0.1:{port}/";
        using var server = new HttpServerResource(address, 1024, TimeSpan.FromSeconds(3));
        using var route = server.RegisterRoute("GET", "/").Subscribe(request => request.Reply(new HttpServerResponse(200)));
        await server.StartAsync();
        var workflow = Load("01-public-get.bonsai");
        foreach (var node in workflow.Workflow)
        {
            var value = (node.Value as CombinatorBuilder)?.Combinator;
            if (value is SendRequest client) client.BaseAddress = address;
            if (value is CreateHttpRequest request) request.Uri = "/";
            if (value is Bonsai.Reactive.Timer timer) { timer.DueTime = TimeSpan.Zero; timer.Period = null; }
        }
        var response = await workflow.Workflow.BuildObservable<HttpResponse>().Timeout(TimeSpan.FromSeconds(5)).FirstAsync();
        Assert.Equal(200, response.StatusCode);
    }


    static WorkflowBuilder Load(string file)
    {
        using var reader = XmlReader.Create(Path.Combine(AppContext.BaseDirectory, "examples", file));
        return (WorkflowBuilder)WorkflowBuilder.Serializer.Deserialize(reader)!;
    }

    [PublicApiTheory]
    [InlineData("01-public-get.bonsai", 200)]
    [InlineData("02-public-post.bonsai", 201)]
    [InlineData("03-status-as-data.bonsai", 404)]
    [InlineData("06-current-weather.bonsai", 200)]
    [InlineData("07-earthquakes.bonsai", 200)]
    public async Task PublicApiExamplesReturnExpectedStatuses(string file, int status)
    {
        var workflow = Load(file);
        var response = await workflow.Workflow.BuildObservable<HttpResponse>().Timeout(TimeSpan.FromSeconds(30)).FirstAsync();
        Assert.Equal(status, response.StatusCode);
        if (file == "06-current-weather.bonsai")
        {
            using var json = System.Text.Json.JsonDocument.Parse(response.Body);
            var current = json.RootElement.GetProperty("current");
            Assert.False(string.IsNullOrEmpty(current.GetProperty("time").GetString()));
            Assert.True(current.TryGetProperty("temperature_2m", out _));
            Assert.True(current.TryGetProperty("relative_humidity_2m", out _));
            Assert.True(current.TryGetProperty("wind_speed_10m", out _));
            Assert.True(json.RootElement.TryGetProperty("current_units", out _));
        }
        if (file == "07-earthquakes.bonsai")
        {
            using var json = System.Text.Json.JsonDocument.Parse(response.Body);
            Assert.Equal("FeatureCollection", json.RootElement.GetProperty("type").GetString());
            Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.GetProperty("features").ValueKind);
            Assert.True(json.RootElement.GetProperty("metadata").GetProperty("count").GetInt32() >= 0);
        }
    }
    [PublicApiTheory]
    [InlineData(ResponseFormat.Text)]
    [InlineData(ResponseFormat.Json)]
    public async Task PublicDecoderExampleEmitsWeather(ResponseFormat format)
    {
        var workflow = Load("08-response-text.bonsai");
        workflow.Workflow.Select(node => node.Value).OfType<DecodeResponse>().Single().Format = format;
        if (format == ResponseFormat.Text)
        {
            var text = await workflow.Workflow.BuildObservable<string>().Timeout(TimeSpan.FromSeconds(30)).FirstAsync();
            using var json = System.Text.Json.JsonDocument.Parse(text);
            Assert.True(json.RootElement.GetProperty("current").TryGetProperty("temperature_2m", out _));
        }
        else
        {
            var json = await workflow.Workflow.BuildObservable<System.Text.Json.Nodes.JsonNode>().Timeout(TimeSpan.FromSeconds(30)).FirstAsync();
            Assert.NotNull(json["current"]!["temperature_2m"]);
        }
    }

    [Theory]
    [InlineData("01-public-get.bonsai")]
    [InlineData("02-public-post.bonsai")]
    [InlineData("03-status-as-data.bonsai")]
    [InlineData("04-timeout.bonsai")]
    [InlineData("05-local-server.bonsai")]
    [InlineData("06-current-weather.bonsai")]
    [InlineData("07-earthquakes.bonsai")]
    [InlineData("08-response-text.bonsai")]
    public void PublicExamplesLoadAndCompileInBonsai(string file)
    {
        using var reader = XmlReader.Create(Path.Combine(AppContext.BaseDirectory, "examples", file));
        var workflow = (WorkflowBuilder)WorkflowBuilder.Serializer.Deserialize(reader)!;
        Assert.NotNull(workflow.Workflow.Build());
        using var writer = new StringWriter();
        WorkflowBuilder.Serializer.Serialize(writer, workflow);
        using var roundTrip = XmlReader.Create(new StringReader(writer.ToString()));
        var restored = (WorkflowBuilder)WorkflowBuilder.Serializer.Deserialize(roundTrip)!;
        Assert.NotNull(restored.Workflow.Build());
    }
}

public sealed class PublicApiTheoryAttribute : TheoryAttribute
{
    public PublicApiTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("BONSAI_HTTP_PUBLIC_API") != "1") Skip = "Set BONSAI_HTTP_PUBLIC_API=1 to contact the public example services.";
    }
}
