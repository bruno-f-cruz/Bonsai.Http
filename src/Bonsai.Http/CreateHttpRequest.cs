using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Http;

/// <summary>Creates a fixed request for each input notification.</summary>
[Description("Creates an HTTP request for each input notification.")]
public sealed class CreateHttpRequest : Combinator<HttpRequest>
{
    public string Method { get; set; } = "GET";
    public string Uri { get; set; } = "/";
    public string? ContentType { get; set; }
    public string? Body { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public override IObservable<HttpRequest> Process<TSource>(IObservable<TSource> source) => Observable.Defer(() =>
    {
        var method = Method;
        var uri = Uri;
        var contentType = ContentType;
        var body = Body is null ? null : System.Text.Encoding.UTF8.GetBytes(Body);
        var headers = new Dictionary<string, string>(Headers, StringComparer.OrdinalIgnoreCase);
        return source.Select(_ => new HttpRequest(method, uri, headers, body, contentType));
    });
}
