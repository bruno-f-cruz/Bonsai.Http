using Bonsai.Design;
using Bonsai.ImGui.Design;
using System.Windows.Forms;

[assembly: Bonsai.TypeVisualizer(typeof(Bonsai.Http.Design.HttpResponseVisualizer), Target = typeof(Bonsai.Http.HttpResponse))]
[assembly: Bonsai.TypeVisualizer(typeof(Bonsai.Http.Design.HttpResponseVisualizer), Target = typeof(Bonsai.Http.IHttpResponseResult))]

namespace Bonsai.Http.Design;

/// <summary>Displays HTTP responses with Dear ImGui in the Bonsai Windows visualizer host.</summary>
public sealed class HttpResponseVisualizer : DialogTypeVisualizer
{
    ImGuiControl? control;
    System.Windows.Forms.Timer? timer;
    readonly HttpResponsePanel panel = new();

    public override void Load(IServiceProvider provider)
    {
        var service = provider.GetService(typeof(IDialogTypeVisualizerService)) as IDialogTypeVisualizerService
            ?? throw new InvalidOperationException("The Bonsai visualizer service is required.");
        control = new ImGuiControl { Dock = DockStyle.Fill };
        control.Render += (_, _) => ImGuiPanel.Draw("HTTP Response", panel.Draw);
        service.AddControl(control);
        timer = new System.Windows.Forms.Timer { Interval = 20 };
        timer.Tick += (_, _) => control?.Invalidate();
        timer.Start();
    }

    public override void Show(object value) => panel.Response = value is IHttpResponseResult result ? result.Response : (HttpResponse)value;

    public override void Unload()
    {
        timer?.Stop();
        timer?.Dispose();
        timer = null;
        control?.Dispose();
        control = null;
        panel.Response = null;
    }
}
