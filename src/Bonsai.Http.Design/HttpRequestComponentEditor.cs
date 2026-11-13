using System.ComponentModel;
using System.Windows.Forms;
using Bonsai.ImGui.Design;
using Hexa.NET.ImGui;

namespace Bonsai.Http.Design;
using ImGui = Hexa.NET.ImGui.ImGui;

/// <summary>Edits request properties with Dear ImGui in a Windows modal host.</summary>
public sealed class HttpRequestComponentEditor : ComponentEditor
{
    public override bool EditComponent(ITypeDescriptorContext? context, object component)
    {
        if (component is not CreateHttpRequest node) return false;
        var editor = new HttpRequestEditor
        {
            Method = node.Method, Uri = node.Uri, ContentType = node.ContentType ?? string.Empty,
            BodyText = node.Body ?? string.Empty,
            HeaderLines = string.Join("\n", node.Headers.Select(header => $"{header.Name}: {header.Value}"))
        };
        using var form = new Form { Text = "HTTP Request", Width = 800, Height = 600, StartPosition = FormStartPosition.CenterParent };
        var control = new ImGuiControl { Dock = DockStyle.Fill };
        form.Controls.Add(control);
        string? error = null;
        control.Render += (_, _) => ImGuiPanel.Draw("HTTP Request", () =>
        {
            editor.Draw();
            if (error is not null) ImGui.TextUnformatted(error);
            if (ImGui.Button("Apply"))
            {
                try { editor.BuildRequest(); form.DialogResult = DialogResult.OK; }
                catch (Exception exception) { error = exception.Message; }
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) form.DialogResult = DialogResult.Cancel;
        });
        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        timer.Tick += (_, _) => control.Invalidate();
        form.Shown += (_, _) => timer.Start();
        if (form.ShowDialog() != DialogResult.OK || context?.OnComponentChanging() == false) return false;
        node.Method = editor.Method;
        node.Uri = editor.Uri;
        node.ContentType = string.IsNullOrWhiteSpace(editor.ContentType) ? null : editor.ContentType;
        node.Body = string.IsNullOrEmpty(editor.BodyText) ? null : editor.BodyText;
        node.Headers = editor.BuildRequest().Headers.Select(pair => new HttpHeader { Name = pair.Key, Value = pair.Value }).ToList();
        context?.OnComponentChanged();
        return true;
    }
}
