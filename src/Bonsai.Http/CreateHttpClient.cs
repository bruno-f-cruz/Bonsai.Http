using System.ComponentModel;
using System.Reactive.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Bonsai.Http;

/// <summary>Creates a reusable HTTP client resource for a ResourceSubject.</summary>
[Description("Creates a reusable HTTP client resource.")]
public sealed class CreateHttpClient : Source<HttpClientResource>
{
    public string? BaseAddress { get; set; }
    [XmlIgnore] public TimeSpan? Timeout { get; set; }
    [XmlIgnore] public TimeSpan ConnectionLifetime { get; set; } = TimeSpan.FromMinutes(5);
    [Browsable(false), XmlElement("Timeout")]
    public string? TimeoutXml { get => Timeout.HasValue ? XmlConvert.ToString(Timeout.Value) : null; set => Timeout = string.IsNullOrEmpty(value) ? null : (TimeSpan?)XmlConvert.ToTimeSpan(value); }
    [Browsable(false), XmlElement("ConnectionLifetime")]
    public string ConnectionLifetimeXml { get => XmlConvert.ToString(ConnectionLifetime); set => ConnectionLifetime = XmlConvert.ToTimeSpan(value); }
    public long MaxResponseBodyBytes { get; set; } = 16 * 1024 * 1024;
    [XmlArrayItem("Header")] public List<HttpHeader> DefaultHeaders { get; set; } = new();

    public override IObservable<HttpClientResource> Generate() => Observable.Defer(() =>
    {
        var baseAddress = string.IsNullOrWhiteSpace(BaseAddress) ? null : new Uri(BaseAddress, UriKind.Absolute);
        var headers = DefaultHeaders.ToDictionary(header => header.Name, header => header.Value, StringComparer.OrdinalIgnoreCase);
        return Observable.Return(new HttpClientResource(baseAddress, headers, Timeout, ConnectionLifetime, MaxResponseBodyBytes));
    });
}
