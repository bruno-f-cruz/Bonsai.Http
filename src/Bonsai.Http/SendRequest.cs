using System.ComponentModel;

namespace Bonsai.Http;

/// <summary>Sends requests using a client owned by the subscription, or an explicitly shared client.</summary>
[Combinator]
[Description("Sends HTTP requests and emits their responses using a reusable client.")]
public sealed class SendRequest : HttpClientOperator
{
    public IObservable<HttpResponse> Process(IObservable<HttpRequest> requests) =>
        ProcessRequests(requests, request => request, (_, response) => response);

    public IObservable<HttpResponse> Process(IObservable<HttpRequest> requests, IObservable<HttpClient> clients) =>
        ProcessRequests(requests, request => request, (_, response) => response, clients);
}
