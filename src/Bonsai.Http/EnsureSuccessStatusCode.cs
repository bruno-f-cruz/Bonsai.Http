using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Http;

/// <summary>Raises a sequence error when an HTTP response has a non-success status.</summary>
[Description("Raises an error for non-success HTTP status codes.")]
public sealed class EnsureSuccessStatusCode : Combinator<HttpResponse, HttpResponse>
{
    public override IObservable<HttpResponse> Process(IObservable<HttpResponse> source) =>
        source.Select(response => response.IsSuccessStatusCode ? response : throw new HttpStatusException(response));
}
