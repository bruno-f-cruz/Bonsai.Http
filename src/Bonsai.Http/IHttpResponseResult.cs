using System.Reactive.Linq;

namespace Bonsai.Http;

/// <summary>A typed operation result that retains the transport response.</summary>
public interface IHttpResponseResult
{
    HttpResponse Response { get; }
}

/// <summary>Extracts the raw response while preserving the originating request and diagnostics.</summary>
[Combinator]
public sealed class GetHttpResponse
{
    public IObservable<HttpResponse> Process<T>(IObservable<T> source) where T : IHttpResponseResult => source.Select(value => value.Response);
}
