using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.GLFW;
using Hexa.NET.ImGui.Backends.OpenGL3;

namespace Bonsai.Http.Design;

/// <summary>A native GLFW/OpenGL Dear ImGui window. Create, pump, and dispose on the UI thread.</summary>
public sealed class HttpImGuiWindow : IDisposable
{
    readonly int thread = Environment.CurrentManagedThreadId;
    readonly string title;
    IntPtr window;
    ImGuiContextPtr context;
    Glfw.ClearColorDelegate? clearColor;
    Glfw.ClearDelegate? clear;
    Glfw.ViewportDelegate? viewport;
    bool glfwBackend;
    bool rendererBackend;
    bool closeRequested;

    public unsafe HttpImGuiWindow(string title, bool visible = true)
    {
        this.title = title;
        try
        {
            if (Glfw.Init() == 0) throw new InvalidOperationException("GLFW could not initialize a display.");
            Glfw.WindowHint(0x00022002, 3); // context major
            Glfw.WindowHint(0x00022003, 3); // context minor
            Glfw.WindowHint(0x00022008, 0x00032001); // core profile
            Glfw.WindowHint(0x00022006, 1); // forward compatible (macOS)
            Glfw.WindowHint(0x00020004, visible ? 1 : 0);
            window = Glfw.CreateWindow(800, 600, title, IntPtr.Zero, IntPtr.Zero);
            if (window == IntPtr.Zero) throw new InvalidOperationException("Could not create an OpenGL 3.3 window.");
            Glfw.MakeContextCurrent(window);
            Glfw.SwapInterval(1);
            context = ImGui.CreateContext();
            Activate();
            var io = ImGui.GetIO();
            io.IniFilename = (byte*)null;
            glfwBackend = ImGuiImplGLFW.InitForOpenGL(new GLFWwindowPtr((GLFWwindow*)window), true);
            rendererBackend = ImGuiImplOpenGL3.Init("#version 330 core");
            if (!glfwBackend || !rendererBackend) throw new InvalidOperationException("Could not initialize Dear ImGui backends.");
            clearColor = Glfw.GetGl<Glfw.ClearColorDelegate>("glClearColor");
            clear = Glfw.GetGl<Glfw.ClearDelegate>("glClear");
            viewport = Glfw.GetGl<Glfw.ViewportDelegate>("glViewport");
        }
        catch { Dispose(); throw; }
    }

    void CheckThread()
    {
        if (thread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Dear ImGui windows must be used on their creating UI thread.");
    }

    void Activate()
    {
        ImGui.SetCurrentContext(context);
        ImGuiImplGLFW.SetCurrentContext(context);
        ImGuiImplOpenGL3.SetCurrentContext(context);
    }

    public void Close() { CheckThread(); closeRequested = true; }

    /// <summary>Draws one frame; returns false after the native window is closed.</summary>
    public bool Pump(Action draw)
    {
        CheckThread();
        if (window == IntPtr.Zero || closeRequested || Glfw.WindowShouldClose(window) != 0) return false;
        Glfw.MakeContextCurrent(window);
        Activate();
        Glfw.PollEvents();
        ImGuiImplOpenGL3.NewFrame();
        ImGuiImplGLFW.NewFrame();
        ImGui.NewFrame();
        ImGui.SetNextWindowPos(System.Numerics.Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        ImGui.Begin(title, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
        try { draw(); }
        finally { ImGui.End(); ImGui.Render(); }
        Glfw.GetFramebufferSize(window, out var width, out var height);
        viewport!(0, 0, width, height);
        clearColor!(0.1f, 0.1f, 0.1f, 1);
        clear!(0x00004000);
        ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());
        Glfw.SwapBuffers(window);
        return true;
    }

    public void Dispose()
    {
        CheckThread();
        if (window == IntPtr.Zero) return;
        Glfw.MakeContextCurrent(window);
        if (!context.IsNull)
        {
            Activate();
            if (rendererBackend) ImGuiImplOpenGL3.Shutdown();
            if (glfwBackend) ImGuiImplGLFW.Shutdown();
            ImGui.DestroyContext(context);
        }
        Glfw.DestroyWindow(window);
        window = IntPtr.Zero;
    }
}
