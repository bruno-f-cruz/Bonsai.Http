using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Http.Server;

/// <summary>Creates a disposable HTTP server resource for explicit sharing.</summary>
[Description("Creates a shared HTTP server resource.")]
public sealed class CreateHttpServer : HttpServerConfiguration<HttpServerResource>
{
    public override IObservable<HttpServerResource> Generate() => Observable.Defer(() => Observable.Return(CreateServer()));
}
