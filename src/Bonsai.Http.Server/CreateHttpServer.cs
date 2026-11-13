using System.ComponentModel;
using System.Reactive.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Bonsai.Http.Server;

/// <summary>Creates a disposable HTTP server resource.</summary>
[Description("Creates a shared HTTP server resource.")]
public sealed class CreateHttpServer : Source<HttpServerResource>
{
    [Description("The absolute URL on which the server listens, such as http://127.0.0.1:8080/. Read when the workflow starts.")]
    public string Url { get; set; } = "http://127.0.0.1:8080";
    [Description("The maximum buffered incoming request body size in bytes. Larger bodies are rejected. Defaults to 16 MiB. Read when the workflow starts.")]
    public long MaxRequestBodyBytes { get; set; } = 16 * 1024 * 1024;
    [Description("The maximum time allowed for the workflow to reply to an incoming request. Must be positive. Read when the workflow starts.")]
    [XmlIgnore] public TimeSpan RequestDeadline { get; set; } = TimeSpan.FromSeconds(30);
    [Browsable(false), XmlElement("RequestDeadline")]
    [Description("The XML duration used to serialize RequestDeadline.")]
    public string RequestDeadlineXml { get => XmlConvert.ToString(RequestDeadline); set => RequestDeadline = XmlConvert.ToTimeSpan(value); }
    [Description("The TLS certificate file for HTTPS on .NET 8. Framework HTTPS uses an operating-system certificate binding. Read when the workflow starts.")]
    public string? CertificatePath { get; set; }
    [Description("The password for the TLS certificate file, if required. Read when the workflow starts.")]
    public string? CertificatePassword { get; set; }

    public override IObservable<HttpServerResource> Generate() => Observable.Defer(() =>
        Observable.Return(new HttpServerResource(Url, MaxRequestBodyBytes, RequestDeadline, CertificatePath, CertificatePassword)));
}
