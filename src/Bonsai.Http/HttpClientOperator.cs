using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Http;

/// <summary>Reusable execution and lifetime management for raw and generated HTTP client operators.</summary>
public abstract class HttpClientOperator<TResponse> : HttpClientConfiguration<TResponse>
{
    /// <summary>Opens the client without sending requests; remains active until disposed.</summary>
    public override IObservable<TResponse> Generate() =>
        Observable.Using(CreateClient, _ => Observable.Never<TResponse>());

    [Description("The maximum number of requests in flight. Responses can arrive out of order. Read when the workflow starts.")]
    public int MaxConcurrency { get; set; } = 4;
    [Description("The maximum number of requests waiting for a slot. Exceeding this limit raises an error. Read when the workflow starts.")]
    public int MaxPendingRequests { get; set; } = 64;

    protected IObservable<TResult> ProcessRequests<TRequest, TResult>(IObservable<TRequest> requests,
        Func<TRequest, HttpRequest> serialize, Func<TRequest, HttpResponse, TResult> decode,
        IObservable<HttpClientResource>? clients = null) => Observable.Defer(() =>
    {
        var concurrency = MaxConcurrency;
        var pending = MaxPendingRequests;
        if (concurrency < 1) throw new InvalidOperationException("MaxConcurrency must be positive.");
        if (pending < 1) throw new InvalidOperationException("MaxPendingRequests must be positive.");
        IObservable<TResult> Execute(IObservable<HttpClientResource> resources) => resources.Take(1).DefaultIfEmpty()
            .Select(client => client ?? throw new InvalidOperationException("The client source completed without a resource."))
            .Replay(shared =>
            {
                var queued = 0;
                var responses = requests.Select(request =>
                {
                    if (Interlocked.Increment(ref queued) > concurrency + pending)
                    {
                        Interlocked.Decrement(ref queued);
                        throw new InvalidOperationException("HTTP request queue is full.");
                    }
                    var message = serialize(request);
                    return shared.SelectMany(client => Observable.FromAsync(token => client.SendAsync(message, token)))
                        .Select(response => decode(request, response))
                        .Finally(() => Interlocked.Decrement(ref queued));
                }).Merge(concurrency);
                return responses.Merge(shared.SelectMany(_ => Observable.Empty<TResult>()));
            }, 1);
        return clients is null ? Observable.Using(CreateClient, client => Execute(Observable.Return(client))) : Execute(clients);
    });
}
