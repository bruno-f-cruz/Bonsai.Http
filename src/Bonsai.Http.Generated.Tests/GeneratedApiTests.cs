using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
#if NET8_0
using Bonsai.Http.Server;
#endif
using SampleApi;
using Xunit;

namespace Bonsai.Http.Generated.Tests;

public class GeneratedApiTests
{
    [Fact]
    public void GeneratedDiscriminatorModelsRoundTripOnBothRuntimes()
    {
        Creature original = new Cat { Name = "Milo", Lives = 9 };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(original);
        Assert.Contains("\"kind\":\"cat\"", json);
        var result = Assert.IsType<Cat>(Newtonsoft.Json.JsonConvert.DeserializeObject<Creature>(json));
        Assert.Equal("Milo", result.Name);
        Assert.Equal(9, result.Lives);
    }

    [Fact]
    public async Task GeneratedRequestSourceCopiesConfiguredValues()
    {
        var source = new CreateGetAnimalRequest { Value = new GetAnimalRequest { AnimalId = "otter" } };
        var request = await source.Generate().SingleAsync();
        Assert.Equal("otter", request.AnimalId);
        Assert.NotSame(source.Value, request);
    }

    [Fact]
    public void GeneratedPropertiesExposeDescriptionsToThePropertyGrid()
    {
        foreach (var type in typeof(Animal).Assembly.GetExportedTypes().Where(type => type.Namespace == "SampleApi"))
        foreach (System.ComponentModel.PropertyDescriptor property in System.ComponentModel.TypeDescriptor.GetProperties(type))
        {
            if (property.ComponentType.Assembly != type.Assembly) continue;
            Assert.True(!string.IsNullOrWhiteSpace(property.Description), $"{type.FullName}.{property.Name} has no property-grid description.");
        }

        Assert.Equal("The animal name.", System.ComponentModel.TypeDescriptor.GetProperties(typeof(Animal))["Name"]!.Description);
        Assert.Equal("The animal identifier.", System.ComponentModel.TypeDescriptor.GetProperties(typeof(GetAnimalRequest))["AnimalId"]!.Description);
    }

    static string Address()
    {
        var lease = new TcpListener(IPAddress.Loopback, 0);
        lease.Start();
        var port = ((IPEndPoint)lease.LocalEndpoint).Port;
        lease.Stop();
        return $"http://127.0.0.1:{port}/";
    }

#if NET8_0
    [Theory]
    [InlineData(200, "text/plain")]
    [InlineData(418, "application/json")]
    public async Task ApiClientKeepsUnknownResponsesAndOriginalTypedRequests(int status, string mediaType)
    {
        var address = Address();
        using var server = new HttpServerResource(address, 4096, TimeSpan.FromSeconds(5));
        using var route = server.RegisterRoute("GET", "/creatures/{id}").Subscribe(context => context.Reply(
            new HttpServerResponse(status, Encoding.UTF8.GetBytes("unexpected response"), mediaType)));
        await server.StartAsync();
        var request = new GetCreatureRequest { Id = "unknown" };
        var result = await new SampleApi.Client { BaseAddress = address }.Process(Observable.Return(request)).SingleAsync();
        Assert.IsType<GetCreatureUnknown>(result);
        Assert.Same(request, result.Request);
        Assert.Equal(status, result.Response.StatusCode);
        Assert.Equal("unexpected response", result.Response.ReadText());
    }

    [Fact]
    public async Task GeneratedClientBuildsAndRoundTripsAsATypedBonsaiNode()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 4096, TimeSpan.FromSeconds(5));
        using var route = server.RegisterRoute("GET", "/creatures/{id}").Subscribe(request => request.Reply(
            new HttpServerResponse(200, Encoding.UTF8.GetBytes("{\"kind\":\"cat\",\"name\":\"Milo\",\"lives\":9}"), "application/json")));
        await server.StartAsync();
        var workflow = new Bonsai.WorkflowBuilder();
        workflow.Workflow.Add(new Bonsai.Expressions.CombinatorBuilder { Combinator = new CreateGetCreatureRequest { Value = new GetCreatureRequest { Id = "milo" } } });
        workflow.Workflow.Add(new Bonsai.Expressions.CombinatorBuilder { Combinator = new SampleApi.Client { BaseAddress = address } });
        workflow.Workflow.Add(new Bonsai.Expressions.WorkflowOutputBuilder());
        workflow.Workflow.AddEdge(workflow.Workflow[0], workflow.Workflow[1], new Bonsai.Expressions.ExpressionBuilderArgument());
        workflow.Workflow.AddEdge(workflow.Workflow[1], workflow.Workflow[2], new Bonsai.Expressions.ExpressionBuilderArgument());
        Assert.Equal(typeof(IObservable<GetCreatureResult>), Bonsai.Expressions.ExpressionBuilderGraphExtensions.Build(workflow.Workflow).Type);
        using var writer = new StringWriter();
        Bonsai.WorkflowBuilder.Serializer.Serialize(writer, workflow);
        using var reader = System.Xml.XmlReader.Create(new StringReader(writer.ToString()));
        var restored = (Bonsai.WorkflowBuilder)Bonsai.WorkflowBuilder.Serializer.Deserialize(reader)!;
        var result = await Bonsai.Expressions.ExpressionBuilderGraphExtensions.BuildObservable<GetCreatureResult>(restored.Workflow).SingleAsync();
        Assert.IsType<Cat>(Assert.IsType<GetCreatureStatus200>(result).Value);
        Assert.Equal("milo", result.Request.Id);
    }

    [Fact]
    public async Task ApiClientDispatchesMixedRequestsAndDiscriminatorModels()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 4096, TimeSpan.FromSeconds(5));
        using var replies = new System.Reactive.Disposables.CompositeDisposable();
        using var animals = server.RegisterRoute("GET", "/animals/{animalId}").Subscribe(request =>
            replies.Add(Observable.Timer(TimeSpan.FromMilliseconds(80)).Subscribe(_ => request.Reply(
                new HttpServerResponse(200, Encoding.UTF8.GetBytes("{\"name\":\"otter\",\"age\":5}"), "application/json")))));
        using var creatures = server.RegisterRoute("GET", "/creatures/{id}").Subscribe(request => request.Reply(
            new HttpServerResponse(200, Encoding.UTF8.GetBytes("{\"kind\":\"cat\",\"name\":\"Milo\",\"lives\":9}"), "application/json")));
        await server.StartAsync();
        var animal = new GetAnimalRequest { AnimalId = "otter", Count = 1, XTrace = "trace" };
        var creature = new GetCreatureRequest { Id = "milo" };
        var client = new SampleApi.Client { BaseAddress = address, Timeout = TimeSpan.FromSeconds(3) };
        var results = await client.Process(new ApiRequest[] { animal, creature }.ToObservable()).ToArray();
        Assert.Equal(2, results.Length);
        Assert.IsType<GetCreatureStatus200>(results[0]);
        var animalResult = Assert.IsType<GetAnimalStatus200>(results.Single(result => result.Request == animal));
        Assert.Same(animal, animalResult.Request);
        var creatureResult = Assert.IsType<GetCreatureStatus200>(results.Single(result => result.Request == creature));
        Assert.Same(creature, creatureResult.Request);
        var cat = Assert.IsType<Cat>(creatureResult.Value);
        Assert.Equal("Milo", cat.Name);
        Assert.Equal(9, cat.Lives);
        Assert.NotEqual(animalResult.Response.RequestId, creatureResult.Response.RequestId);
    }

    [Fact]
    public async Task ApiClientTypedOverloadRetainsRawParseFailure()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 4096, TimeSpan.FromSeconds(5));
        using var route = server.RegisterRoute("GET", "/creatures/{id}").Subscribe(request => request.Reply(
            new HttpServerResponse(200, Encoding.UTF8.GetBytes("invalid json"), "application/json")));
        await server.StartAsync();
        var client = new SampleApi.Client { BaseAddress = address };
        IObservable<GetCreatureResult> results = client.Process(Observable.Return(new GetCreatureRequest { Id = "bad" }));
        var error = await Assert.ThrowsAsync<HttpResponseParseException>(async () => await results.SingleAsync());
        Assert.Equal("invalid json", error.Response.ReadText());
    }

    [Fact]
    public async Task GeneratedOperationSendsInheritedTypedParametersAndParsesModel()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 4096, TimeSpan.FromSeconds(5));
        string? seenQuery = null;
        string? seenHeader = null;
        using var route = server.RegisterRoute("GET", "/animals/{animalId}").Subscribe(request =>
        {
            seenQuery = request.Uri.Query;
            seenHeader = request.Headers["X-Trace"][0];
            request.Reply(new HttpServerResponse(200, Encoding.UTF8.GetBytes("{\"name\":\"otter\",\"age\":5}"), "application/json"));
        });
        await server.StartAsync();
        using var client = new HttpClient(new Uri(address), new Dictionary<string, string>(), TimeSpan.FromSeconds(3), TimeSpan.FromMinutes(1), 4096);
        var result = await new GetAnimal().Process(Observable.Return(new GetAnimalRequest { AnimalId = "a b", Count = 3, XTrace = "trace-1" }), Observable.Return(client)).FirstAsync();
        var success = Assert.IsType<GetAnimalStatus200>(result);
        Assert.Equal("otter", success.Value.Name);
        Assert.Contains("count=3", seenQuery);
        Assert.Contains("verbose=true", seenQuery);
        Assert.Equal("trace-1", seenHeader);
        Assert.Equal(200, success.Response.StatusCode);
    }

#endif
    [Fact]
    public async Task MissingRequiredParameterErrorsBeforeNetworkSend()
    {
        using var client = new HttpClient(new Uri("http://127.0.0.1:1/"), new Dictionary<string, string>(), null, TimeSpan.FromMinutes(1), 4096);
        await Assert.ThrowsAsync<ArgumentException>(async () => await new GetAnimal()
            .Process(Observable.Return(new GetAnimalRequest { AnimalId = "a", XTrace = "trace" }), Observable.Return(client)).FirstAsync());
    }

#if NET8_0
    [Fact]
    public async Task ErrorStatusAndParseFailureKeepRawResponse()
    {
        var address = Address();
        using var server = new HttpServerResource(address, 4096, TimeSpan.FromSeconds(5));
        using var route = server.RegisterRoute("GET", "/animals/{animalId}").Subscribe(request =>
            request.Reply(new HttpServerResponse(404, Encoding.UTF8.GetBytes(request.RouteValues["animalId"] == "bad" ? "invalid json" : "{\"title\":\"missing\"}"), "application/problem+json")));
        await server.StartAsync();
        using var client = new HttpClient(new Uri(address), new Dictionary<string, string>(), TimeSpan.FromSeconds(3), TimeSpan.FromMinutes(1), 4096);
        IObservable<GetAnimalResult> Run(string id) => new GetAnimal().Process(Observable.Return(new GetAnimalRequest { AnimalId = id, Count = 1, XTrace = "trace" }), Observable.Return(client));
        var missing = Assert.IsType<GetAnimalStatus404>(await Run("missing").FirstAsync());
        Assert.Equal("missing", missing.Value.Title);
        var error = await Assert.ThrowsAsync<HttpResponseParseException>(async () => await Run("bad").FirstAsync());
        Assert.Equal(404, error.Response.StatusCode);
        Assert.Equal("invalid json", error.Response.ReadText());
    }
#endif
}
