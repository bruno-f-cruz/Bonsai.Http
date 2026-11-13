using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Http.Server;

/// <summary>A paired request context and response value.</summary>
public sealed record HttpServerExchange(HttpServerRequestContext Request, HttpServerResponse Response);

/// <summary>Sends a response through the corresponding request context.</summary>
[Description("Sends a response to the request in each exchange.")]
public sealed class SendResponse : Combinator<HttpServerExchange, HttpServerExchange>
{
    public override IObservable<HttpServerExchange> Process(IObservable<HttpServerExchange> source) =>
        source.Do(exchange => exchange.Request.Reply(exchange.Response));
}
