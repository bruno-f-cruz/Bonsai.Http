using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Http.Server;

/// <summary>Registers an HTTP route and emits matching requests.</summary>
[Description("Receives requests matching an HTTP method and route template.")]
public sealed class ReceiveRequest : Combinator<HttpServerResource, HttpServerRequestContext>
{
    [Description("The HTTP method to match, such as GET or POST. Read when the workflow starts.")]
    public string Method { get; set; } = "GET";
    [Description("The route path template to match, such as /items/{id}. Captured parameters appear in RouteValues. Read when the workflow starts.")]
    public string Pattern { get; set; } = "/";

    public override IObservable<HttpServerRequestContext> Process(IObservable<HttpServerResource> source) =>
        Observable.Defer(() =>
        {
            var method = Method;
            var pattern = Pattern;
            return source.Take(1).SelectMany(server => Observable.Create<HttpServerRequestContext>(observer =>
            {
                var requests = server.RegisterRoute(method, pattern);
                var subscription = requests.Subscribe(observer);
                _ = server.StartAsync().ContinueWith(task =>
                {
                    if (task.IsFaulted) observer.OnError(task.Exception!.GetBaseException());
                }, TaskScheduler.Default);
                return subscription;
            }));
        });
}
