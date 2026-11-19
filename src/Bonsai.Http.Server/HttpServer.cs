using System.ComponentModel;
using System.Collections.Concurrent;
using System.Reactive.Linq;

namespace Bonsai.Http.Server;

/// <summary>Owns an HTTP listener and produces incoming request contexts.</summary>
[Description("Produces incoming HTTP requests using a listener owned by the workflow.")]
[WorkflowElementIcon(typeof(HttpRequest), "Resources.Network")]
public sealed class HttpServer : HttpServerConfiguration<HttpServerRequestContext>
{
    [Description("The HTTP method to match, such as GET or POST. Read when the workflow starts.")]
    public string Method { get; set; } = "GET";

    [Description("The route path template to match, such as /items/{id}. Captured parameters appear in RouteValues. Read when the workflow starts.")]
    public string Pattern { get; set; } = "/";

    public override IObservable<HttpServerRequestContext> Generate() => Generate(Observable.Never<HttpServerResponse>());

    public IObservable<HttpServerRequestContext> Generate(IObservable<HttpServerResponse> responses) => Observable.Defer(() =>
    {
        var receive = new ReceiveRequest { Method = Method, Pattern = Pattern };
        return Observable.Using(CreateServer, server =>
        {
            var pending = new ConcurrentDictionary<Guid, HttpServerRequestContext>();
            var replies = responses.Select(response =>
            {
                var request = response.Request;
                if (request is null || !pending.TryRemove(request.RequestId, out var original) || !ReferenceEquals(request, original))
                    throw new InvalidOperationException("A server response must reference an unanswered request from this subscription.");
                request.Reply(response);
                return request;
            }).IgnoreElements();
            var requests = receive.Process(Observable.Return(server)).Do(request =>
            {
                pending.TryAdd(request.RequestId, request);
                request.CancellationToken.Register(() => pending.TryRemove(request.RequestId, out _));
            });
            // Subscribe to responses first so synchronous request handlers can reply immediately.
            return replies.Merge(requests);
        });
    });
}
