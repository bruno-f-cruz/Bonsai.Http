using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using Bonsai.Http.Server;
using SampleApi;
using Xunit;

namespace Bonsai.Http.Generated.Tests;

public class GeneratedApiTests
{
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

    [Fact]
    public async Task MissingRequiredParameterErrorsBeforeNetworkSend()
    {
        using var client = new HttpClient(new Uri("http://127.0.0.1:1/"), new Dictionary<string, string>(), null, TimeSpan.FromMinutes(1), 4096);
        await Assert.ThrowsAsync<ArgumentException>(async () => await new GetAnimal()
            .Process(Observable.Return(new GetAnimalRequest { AnimalId = "a", XTrace = "trace" }), Observable.Return(client)).FirstAsync());
    }

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
}
