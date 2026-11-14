using System.ComponentModel;

namespace Bonsai.Http;

/// <summary>Produces asynchronous HTTP responses to an optional sequence of requests.</summary>
[Description("Produces asynchronous HTTP responses paired with their originating requests.")]
[WorkflowElementIcon(typeof(HttpRequest), "Resources.Network")]
public class HttpClient : HttpClientOperator<HttpResponse>
{
    public IObservable<HttpResponse> Generate(IObservable<HttpRequest> requests) =>
        ProcessRequests(requests, request => request, (_, response) => response);

    public IObservable<HttpResponse> Generate(IObservable<HttpRequest> requests, IObservable<HttpClientResource> clients) =>
        ProcessRequests(requests, request => request, (_, response) => response, clients);
}
