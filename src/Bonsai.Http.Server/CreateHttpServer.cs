using System.ComponentModel;
using System.Reactive.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Bonsai.Http.Server;

/// <summary>Creates a disposable HTTP server resource.</summary>
[Description("Creates a shared HTTP server resource.")]
public sealed class CreateHttpServer : Source<HttpServerResource>
{
    public string Url { get; set; } = "http://127.0.0.1:8080";
    public long MaxRequestBodyBytes { get; set; } = 16 * 1024 * 1024;
    [XmlIgnore] public TimeSpan RequestDeadline { get; set; } = TimeSpan.FromSeconds(30);
    [Browsable(false), XmlElement("RequestDeadline")]
    public string RequestDeadlineXml { get => XmlConvert.ToString(RequestDeadline); set => RequestDeadline = XmlConvert.ToTimeSpan(value); }
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; }

    public override IObservable<HttpServerResource> Generate() => Observable.Defer(() =>
        Observable.Return(new HttpServerResource(Url, MaxRequestBodyBytes, RequestDeadline, CertificatePath, CertificatePassword)));
}
