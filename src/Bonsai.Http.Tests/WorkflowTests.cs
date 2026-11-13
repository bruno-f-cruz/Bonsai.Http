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
    public async Task ClientWorkflowExecutesWithResourceSubject()
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
            if (value is CreateHttpClient client) client.BaseAddress = address;
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
    public async Task PublicApiExamplesReturnExpectedStatuses(string file, int status)
    {
        var workflow = Load(file);
        var response = await workflow.Workflow.BuildObservable<HttpResponse>().Timeout(TimeSpan.FromSeconds(30)).FirstAsync();
        Assert.Equal(status, response.StatusCode);
    }
    [Theory]
    [InlineData("01-public-get.bonsai")]
    [InlineData("02-public-post.bonsai")]
    [InlineData("03-status-as-data.bonsai")]
    [InlineData("04-timeout.bonsai")]
    [InlineData("05-local-server.bonsai")]
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
