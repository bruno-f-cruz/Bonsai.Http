using System.Text;
using Hexa.NET.ImGui;

namespace Bonsai.Http.Design;

/// <summary>Renders a materialized HTTP response in an existing Dear ImGui frame.</summary>
public sealed class HttpResponsePanel
{
    HttpResponse? previewResponse;
    int previewLimit;
    string preview = string.Empty;
    public int MaxPreviewBytes { get; set; } = 64 * 1024;
    public HttpResponse? Response { get; set; }

    public void Draw()
    {
        var response = Response;
        if (response is null)
        {
            ImGui.TextUnformatted("No HTTP response yet.");
            return;
        }
        ImGui.TextUnformatted($"{response.StatusCode} {response.ReasonPhrase}");
        ImGui.TextUnformatted($"{response.Request.Method} {response.EffectiveUri}");
        ImGui.TextUnformatted($"Request ID: {response.RequestId}");
        ImGui.TextUnformatted($"Duration: {response.Duration.TotalMilliseconds:F1} ms");
        if (ImGui.CollapsingHeader("Request headers"))
        {
            foreach (var header in response.Request.Headers)
                ImGui.TextUnformatted($"{header.Key}: {(IsSensitive(header.Key) ? "[redacted]" : header.Value)}");
        }
        if (ImGui.CollapsingHeader("Headers"))
        {
            foreach (var header in response.Headers)
            {
                var value = IsSensitive(header.Key) ? "[redacted]" : string.Join(", ", header.Value);
                ImGui.TextUnformatted($"{header.Key}: {value}");
            }
        }
        if (ImGui.CollapsingHeader("Body"))
        {
            if (!ReferenceEquals(previewResponse, response) || previewLimit != MaxPreviewBytes)
            {
                var body = response.Body;
                var count = Math.Min(Math.Max(0, MaxPreviewBytes), body.Length);
                var mediaType = response.Headers.TryGetValue("Content-Type", out var types) ? types.FirstOrDefault() : null;
                var isText = mediaType?.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             mediaType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true;
                preview = isText ? Encoding.UTF8.GetString(body, 0, count) : BitConverter.ToString(body, 0, count);
                if (count < body.Length) preview += "\nPreview truncated.";
                previewResponse = response;
                previewLimit = MaxPreviewBytes;
            }
            ImGui.TextUnformatted(preview);
        }
    }

    static bool IsSensitive(string name) => name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase);
}
