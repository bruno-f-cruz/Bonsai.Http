using Xunit;

namespace Bonsai.Http.Tests;

public class DesignTests
{
#if NETFRAMEWORK
    [Fact]
    public void RequestComponentEditorIsRegistered()
    {
        Assert.IsType<Design.HttpRequestComponentEditor>(System.ComponentModel.TypeDescriptor.GetEditor(new CreateHttpRequest(), typeof(System.ComponentModel.ComponentEditor)));
        Assert.Contains(typeof(Design.HttpResponsePanel).Assembly.GetCustomAttributesData(), attribute => attribute.AttributeType.FullName == "Bonsai.TypeVisualizerAttribute");
    }

    [NativeUiFact]
    public void NativeImGuiFrameCanRenderWhenRequested()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new System.Windows.Forms.Form { Width = 800, Height = 600, Opacity = 0, ShowInTaskbar = false };
                var control = new Bonsai.ImGui.Design.ImGuiControl { Dock = System.Windows.Forms.DockStyle.Fill };
                form.Controls.Add(control);
                var frames = 0;
                var editor = new Design.HttpRequestEditor();
                control.Render += (_, _) =>
                {
                    Hexa.NET.ImGui.ImGui.Begin("HTTP rendering check");
                    editor.Draw();
                    Hexa.NET.ImGui.ImGui.End();
                    frames++;
                };
                form.Show();
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                while (frames == 0 && elapsed.Elapsed < TimeSpan.FromSeconds(5))
                {
                    control.Invalidate();
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Assert.True(frames > 0, "The Dear ImGui host did not render a frame.");
                Assert.Equal("GET", editor.BuildRequest().Method);
            }
            catch (Exception error) { failure = error; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The Dear ImGui check did not complete.");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
#endif
}

public sealed class NativeUiFactAttribute : FactAttribute
{
    public NativeUiFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("BONSAI_HTTP_NATIVE_UI") != "1")
            Skip = "Set BONSAI_HTTP_NATIVE_UI=1 on a Windows machine with an OpenGL display.";
    }
}
