using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Http;

/// <summary>Decodes each buffered HTTP response body into a string.</summary>
[Description("Decodes each HTTP response body into a string using its declared charset, or UTF-8 when the charset is absent or unsupported.")]
public sealed class ReadResponseText : Combinator<HttpResponse, string>
{
    public override IObservable<string> Process(IObservable<HttpResponse> source) =>
        source.Select(response => response.ReadText());
}
