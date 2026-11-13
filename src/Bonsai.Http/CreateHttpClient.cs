using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Http;

/// <summary>Creates a reusable HTTP client resource for a ResourceSubject.</summary>
[Description("Creates a reusable HTTP client resource.")]
public sealed class CreateHttpClient : Source<HttpClientResource>
{
    public string? BaseAddress { get; set; }
    public TimeSpan? Timeout { get; set; }
    public TimeSpan ConnectionLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public long MaxResponseBodyBytes { get; set; } = 16 * 1024 * 1024;
    public Dictionary<string, string> DefaultHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public override IObservable<HttpClientResource> Generate() => Observable.Defer(() =>
    {
        var baseAddress = string.IsNullOrWhiteSpace(BaseAddress) ? null : new Uri(BaseAddress, UriKind.Absolute);
        var headers = new Dictionary<string, string>(DefaultHeaders, StringComparer.OrdinalIgnoreCase);
        return Observable.Return(new HttpClientResource(baseAddress, headers, Timeout, ConnectionLifetime, MaxResponseBodyBytes));
    });
}
