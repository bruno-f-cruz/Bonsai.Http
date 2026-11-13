using System.ComponentModel;
using System.Reactive.Linq;
using System.Xml.Serialization;

namespace Bonsai.Http;

/// <summary>Creates a fixed request for each input notification.</summary>
[Description("Creates an HTTP request for each input notification.")]
[Editor("Bonsai.Http.Design.HttpRequestComponentEditor, Bonsai.Http.Design", typeof(ComponentEditor))]
public sealed class CreateHttpRequest : Combinator<HttpRequest>
{
    [Description("The HTTP method, such as GET, POST, PUT, or DELETE. Read when the workflow starts.")]
    public string Method { get; set; } = "GET";
    [Description("The absolute request URI or a URI relative to the shared client base address. Read when the workflow starts.")]
    public string Uri { get; set; } = "/";
    [Description("The media type of the request body, such as application/json. Leave empty to omit it. Read when the workflow starts.")]
    public string? ContentType { get; set; }
    [Description("The request body encoded as UTF-8. Leave unset to send no body. Read when the workflow starts.")]
    public string? Body { get; set; }
    [Description("Headers for this request. These override client defaults with the same name. Read when the workflow starts.")]
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
