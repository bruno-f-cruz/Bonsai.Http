using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Http.Server;

/// <summary>Registers an HTTP route and emits matching requests.</summary>
[Description("Receives requests matching an HTTP method and route template.")]
public sealed class ReceiveRequest : Combinator<HttpServerResource, HttpServerRequestContext>
{
    public string Method { get; set; } = "GET";
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
