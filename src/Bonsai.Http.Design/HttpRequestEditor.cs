using System.Text;
using Bonsai.Http;
using Hexa.NET.ImGui;

namespace Bonsai.Http.Design;
using ImGui = Hexa.NET.ImGui.ImGui;

/// <summary>A small Dear ImGui request editor that produces schema-agnostic request values.</summary>
public sealed class HttpRequestEditor
{
    public string Method { get; set; } = "GET";
    public string Uri { get; set; } = "/";
    public string ContentType { get; set; } = "application/json";
    public string HeaderLines { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;

    public void Draw()
    {
        var method = Method;
        var uri = Uri;
        var contentType = ContentType;
        var headerLines = HeaderLines;
        var bodyText = BodyText;
        ImGui.InputText("Method", ref method, 32);
        ImGui.InputText("URI", ref uri, 2048);
        ImGui.InputText("Content type", ref contentType, 256);
        ImGui.InputTextMultiline("Headers", ref headerLines, 8192, new System.Numerics.Vector2(-1, 100));
        ImGui.InputTextMultiline("Body", ref bodyText, 65536, new System.Numerics.Vector2(-1, 180));
        Method = method;
        Uri = uri;
        ContentType = contentType;
        HeaderLines = headerLines;
        BodyText = bodyText;
    }

    public HttpRequest BuildRequest()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in HeaderLines.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0) throw new FormatException($"Invalid header line: {line}");
            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
        var body = string.IsNullOrEmpty(BodyText) ? null : Encoding.UTF8.GetBytes(BodyText);
        return new HttpRequest(Method, Uri, headers, body, string.IsNullOrWhiteSpace(ContentType) ? null : ContentType);
    }
}
