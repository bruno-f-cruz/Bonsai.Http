using System.ComponentModel;
using Hexa.NET.ImGui;

namespace Bonsai.Http.Design;

/// <summary>Edits a CreateHttpRequest operator in a native Dear ImGui window.</summary>
public sealed class HttpRequestComponentEditor : ComponentEditor
{
    public override bool EditComponent(ITypeDescriptorContext? context, object component)
    {
        if (component is not CreateHttpRequest node) return false;
        var editor = new HttpRequestEditor
        {
            Method = node.Method, Uri = node.Uri, ContentType = node.ContentType ?? string.Empty,
            BodyText = node.Body ?? string.Empty,
            HeaderLines = string.Join("\n", node.Headers.Select(pair => $"{pair.Name}: {pair.Value}"))
        };
        var accepted = false;
        string? error = null;
        using var window = new HttpImGuiWindow("HTTP Request");
        while (window.Pump(() =>
        {
            editor.Draw();
            if (error is not null) ImGui.TextUnformatted(error);
            if (ImGui.Button("Apply"))
            {
                try { editor.BuildRequest(); accepted = true; window.Close(); }
                catch (Exception exception) { error = exception.Message; }
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) window.Close();
        })) Thread.Sleep(10);
        if (!accepted || context?.OnComponentChanging() == false) return false;
        node.Method = editor.Method;
        node.Uri = editor.Uri;
        node.ContentType = string.IsNullOrWhiteSpace(editor.ContentType) ? null : editor.ContentType;
        node.Body = string.IsNullOrEmpty(editor.BodyText) ? null : editor.BodyText;
        node.Headers = editor.BuildRequest().Headers.Select(pair => new HttpHeader { Name = pair.Key, Value = pair.Value }).ToList();
        context?.OnComponentChanged();
        return true;
    }
}
