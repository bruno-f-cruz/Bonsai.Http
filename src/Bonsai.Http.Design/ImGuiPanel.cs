using Hexa.NET.ImGui;

namespace Bonsai.Http.Design;
using ImGui = Hexa.NET.ImGui.ImGui;

internal static class ImGuiPanel
{
    public static void Draw(string title, Action content)
    {
        ImGui.SetNextWindowPos(System.Numerics.Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        ImGui.Begin(title, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
        try { content(); }
        finally { ImGui.End(); }
    }
}
