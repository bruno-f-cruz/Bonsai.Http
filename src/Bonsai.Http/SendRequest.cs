using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Http;

/// <summary>Sends each request through a shared HTTP client resource.</summary>
[Combinator]
[Description("Sends HTTP requests using a shared client resource.")]
public sealed class SendRequest
{
    [Description("The maximum number of requests in flight at once. Responses are emitted as they complete and may arrive out of order. Read when the workflow starts.")]
    public int MaxConcurrency { get; set; } = 4;
    [Description("The maximum number of requests waiting for an available concurrency slot. Exceeding this limit raises a sequence error. Read when the workflow starts.")]
    public int MaxPendingRequests { get; set; } = 64;

    public IObservable<HttpResponse> Process(IObservable<HttpRequest> requests, IObservable<HttpClient> clients)
    {
        return Observable.Defer(() =>
        {
            var maxConcurrency = MaxConcurrency;
            var maxPending = MaxPendingRequests;
            if (maxConcurrency < 1) throw new InvalidOperationException("MaxConcurrency must be positive.");
            if (maxPending < 1) throw new InvalidOperationException("MaxPendingRequests must be positive.");
            return clients.Take(1).DefaultIfEmpty().Select(client => client ?? throw new InvalidOperationException("The client source completed without a resource.")).Replay(sharedClient =>
            {
                var queued = 0;
                var responses = requests.Select(request =>
                {
                    if (Interlocked.Increment(ref queued) > maxConcurrency + maxPending)
                    {
                        Interlocked.Decrement(ref queued);
                        throw new InvalidOperationException("HTTP request queue is full.");
                    }
                    return sharedClient.SelectMany(client => Observable.FromAsync(token => client.SendAsync(request, token)))
                        .Finally(() => Interlocked.Decrement(ref queued));
                }).Merge(maxConcurrency);
                return responses.Merge(sharedClient.SelectMany(_ => Observable.Empty<HttpResponse>()));
            }, 1);
        });
    }
}
