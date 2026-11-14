using System.ComponentModel;

namespace Bonsai.Http;

/// <summary>Compatibility sender for workflows created before the source-based HttpClient.</summary>
[Combinator(MethodName = nameof(Process))]
[Description("Sends HTTP requests and emits their responses. Use HttpClient for new workflows.")]
public sealed class SendRequest : HttpClient
{
    public IObservable<HttpResponse> Process(IObservable<HttpRequest> requests) => Generate(requests);

    public IObservable<HttpResponse> Process(IObservable<HttpRequest> requests, IObservable<HttpClientResource> clients) =>
        Generate(requests, clients);
}
