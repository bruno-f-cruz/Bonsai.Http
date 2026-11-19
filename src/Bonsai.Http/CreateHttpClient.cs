using System.ComponentModel;
using System.Reactive.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Bonsai.Http;

/// <summary>Creates a reusable HTTP client resource for a ResourceSubject.</summary>
[Description("Creates a reusable HTTP client resource.")]
public sealed class CreateHttpClient : Source<HttpClientResource>
{
    [Description("The absolute base URI used to resolve relative request URIs. Leave empty when all requests use absolute URIs. Read when the workflow starts.")]
    public string? BaseAddress { get; set; }
    [Description("The maximum time for each request, including reading its response body. Leave empty for no timeout. A timeout raises a sequence error. Read when the workflow starts.")]
    [XmlIgnore] public TimeSpan? Timeout { get; set; }
    [Description("The lifetime of pooled connections before they are renewed to refresh DNS. Must be positive. Read when the workflow starts.")]
    [XmlIgnore] public TimeSpan ConnectionLifetime { get; set; } = TimeSpan.FromMinutes(5);
    [Browsable(false), XmlElement("Timeout")]
    [Description("The XML duration used to serialize Timeout.")]
    public string? TimeoutXml { get => Timeout.HasValue ? XmlConvert.ToString(Timeout.Value) : null; set => Timeout = string.IsNullOrEmpty(value) ? null : (TimeSpan?)XmlConvert.ToTimeSpan(value); }
    [Browsable(false), XmlElement("ConnectionLifetime")]
    [Description("The XML duration used to serialize ConnectionLifetime.")]
    public string ConnectionLifetimeXml { get => XmlConvert.ToString(ConnectionLifetime); set => ConnectionLifetime = XmlConvert.ToTimeSpan(value); }
    [Description("The maximum buffered response body size in bytes. Larger responses raise a sequence error. Defaults to 16 MiB. Read when the workflow starts.")]
    public long MaxResponseBodyBytes { get; set; } = 16 * 1024 * 1024;
    [Description("Headers applied to every request unless overridden by that request. Read when the workflow starts.")]
    [XmlArrayItem("Header")] public List<HttpHeader> DefaultHeaders { get; set; } = new();

    public override IObservable<HttpClientResource> Generate() => Observable.Defer(() =>
    {
        var baseAddress = string.IsNullOrWhiteSpace(BaseAddress) ? null : new Uri(BaseAddress, UriKind.Absolute);
        var headers = DefaultHeaders.ToDictionary(header => header.Name, header => header.Value, StringComparer.OrdinalIgnoreCase);
        return Observable.Return(new HttpClientResource(baseAddress, headers, Timeout, ConnectionLifetime, MaxResponseBodyBytes));
    });
}
