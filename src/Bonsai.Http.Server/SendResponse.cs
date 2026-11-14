using System.ComponentModel;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Xml.Serialization;
using Bonsai.Expressions;

namespace Bonsai.Http.Server;

/// <summary>Runs a response-body workflow separately for each incoming request.</summary>
[Description("Runs a nested workflow for each request and produces a paired response to return to HttpServer.")]
[WorkflowElementCategory(ElementCategory.Transform)]
public sealed class SendResponse : WorkflowExpressionBuilder
{
    public SendResponse() : this(new ExpressionBuilderGraph()) { }

    public SendResponse(ExpressionBuilderGraph workflow) : base(workflow) { }

    [Browsable(false)]
    [Description("Requires one sequence of incoming HTTP request contexts.")]
    public override Range<int> ArgumentRange => Bonsai.Range.Create(1, 1);

    [Description("The HTTP status code to send, from 100 to 599. Read when the workflow starts.")]
    public int StatusCode { get; set; } = 200;

    [Description("The response media type. When unset, strings use text/plain and byte arrays use application/octet-stream. String responses include the selected encoding as their charset. Read when the workflow starts.")]
    public string? ContentType { get; set; }

    [Description("The encoding for string output, such as utf-8 or utf-16. Leave empty to use UTF-8. Byte arrays are sent unchanged. Read when the workflow starts.")]
    [TypeConverter(typeof(ResponseEncodingConverter))]
    public string? Encoding { get; set; }

    [Description("The headers to include in each response. Read when the workflow starts.")]
    [XmlArrayItem("Header")]
    public List<HttpHeader> Headers { get; set; } = new();

    public override Expression Build(IEnumerable<Expression> arguments)
    {
        var source = arguments.Single();
        if (!typeof(IObservable<HttpServerRequestContext>).IsAssignableFrom(source.Type))
            throw new InvalidOperationException("SendResponse requires HTTP request contexts.");
        var request = Expression.Parameter(typeof(HttpServerRequestContext), "request");
        var input = Expression.Call(typeof(Observable), nameof(Observable.Return), new[] { request.Type }, request);
        return BuildWorkflow(arguments, input, body =>
        {
            var bodyType = body.Type.GetGenericArguments()[0];
            if (bodyType != typeof(string) && bodyType != typeof(byte[]))
                throw new InvalidOperationException("The SendResponse workflow output must be a string or byte array.");
            var selector = Expression.Lambda(body, request);
            var method = typeof(SendResponse).GetMethod(nameof(Process), BindingFlags.NonPublic | BindingFlags.Instance)!;
            return Expression.Call(Expression.Constant(this), method.MakeGenericMethod(bodyType), source, selector);
        });
    }

    IObservable<HttpServerResponse> Process<TBody>(IObservable<HttpServerRequestContext> source,
        Func<HttpServerRequestContext, IObservable<TBody>> selector) => Observable.Defer(() =>
    {
        var statusCode = StatusCode;
        if (statusCode is < 100 or > 599) throw new InvalidOperationException("StatusCode must be between 100 and 599.");
        var isText = typeof(TBody) == typeof(string);
        var encoding = isText
            ? string.IsNullOrWhiteSpace(Encoding) ? System.Text.Encoding.UTF8 : System.Text.Encoding.GetEncoding(Encoding)
            : null;
        var contentType = MediaTypeHeaderValue.Parse(ContentType ?? (isText ? "text/plain" : "application/octet-stream"));
        if (isText) contentType.CharSet = encoding!.WebName;
        var mediaType = contentType.ToString();
        var headers = Headers.ToDictionary(header => header.Name, header => header.Value, StringComparer.OrdinalIgnoreCase);

        return source.SelectMany(request => Observable.Defer(() => selector(request)
                .Take(1)
                .DefaultIfEmpty()
                .Select(body =>
                {
                    var bytes = isText ? encoding!.GetBytes((string?)(object?)body ?? string.Empty)
                        : (byte[]?)(object?)body ?? Array.Empty<byte>();
                    return new HttpServerResponse(request, statusCode, bytes, mediaType, headers);
                }))
            .TakeUntil(Observable.Create<Unit>(observer =>
                request.CancellationToken.Register(() => observer.OnNext(Unit.Default)))));
    });
}
