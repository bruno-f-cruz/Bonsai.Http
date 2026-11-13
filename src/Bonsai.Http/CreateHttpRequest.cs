using System.ComponentModel;
using System.Reactive.Linq;
using System.Xml.Serialization;

namespace Bonsai.Http;

/// <summary>Creates a fixed request for each input notification.</summary>
[Description("Creates an HTTP request for each input notification.")]
[Editor("Bonsai.Http.Design.HttpRequestComponentEditor, Bonsai.Http.Design", typeof(ComponentEditor))]
public sealed class CreateHttpRequest : Combinator<HttpRequest>
{
    public string Method { get; set; } = "GET";
    public string Uri { get; set; } = "/";
    public string? ContentType { get; set; }
    public string? Body { get; set; }
    [XmlArrayItem("Header")] public List<HttpHeader> Headers { get; set; } = new();

    public override IObservable<HttpRequest> Process<TSource>(IObservable<TSource> source) => Observable.Defer(() =>
    {
        var method = Method;
        var uri = Uri;
        var contentType = ContentType;
        var body = Body is null ? null : System.Text.Encoding.UTF8.GetBytes(Body);
        var headers = Headers.ToDictionary(header => header.Name, header => header.Value, StringComparer.OrdinalIgnoreCase);
        return source.Select(_ => new HttpRequest(method, uri, headers, body, contentType));
    });
}
