using System.ComponentModel;
using System.Reactive.Linq;
using System.Text;
using System.Xml.Serialization;

namespace Bonsai.Http.Server;

/// <summary>Pairs each incoming request with a configured server response.</summary>
[Description("Creates a response value for each incoming HTTP request.")]
public sealed class CreateHttpServerResponse : Combinator<HttpServerRequestContext, HttpServerExchange>
{
    public int StatusCode { get; set; } = 200;
    public string? ContentType { get; set; } = "text/plain; charset=utf-8";
    public string Body { get; set; } = string.Empty;
    [XmlArrayItem("Header")] public List<HttpHeader> Headers { get; set; } = new();

    public override IObservable<HttpServerExchange> Process(IObservable<HttpServerRequestContext> source) => Observable.Defer(() =>
    {
        var status = StatusCode;
        var contentType = ContentType;
        var body = Encoding.UTF8.GetBytes(Body);
        var headers = Headers.ToDictionary(header => header.Name, header => header.Value, StringComparer.OrdinalIgnoreCase);
        return source.Select(request => new HttpServerExchange(request,
            new HttpServerResponse(status, body, contentType, headers)));
    });
}
