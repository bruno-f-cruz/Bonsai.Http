using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Http.Server;

/// <summary>A paired request context and response value.</summary>
public sealed record HttpServerExchange(
    [property: Description("The incoming request context to which the response will be sent.")] HttpServerRequestContext Request,
    [property: Description("The HTTP response to send to the associated request.")] HttpServerResponse Response);

/// <summary>Sends a response through the corresponding request context.</summary>
[Description("Sends a response to the request in each exchange.")]
public sealed class SendResponse : Combinator<HttpServerExchange, HttpServerExchange>
{
    public override IObservable<HttpServerExchange> Process(IObservable<HttpServerExchange> source) =>
        source.Do(exchange => exchange.Request.Reply(exchange.Response));
}
