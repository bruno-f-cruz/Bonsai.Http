using System.Runtime.InteropServices;

namespace Bonsai.Http.Design;

internal static class Glfw
{
    static readonly IntPtr library = Load();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int InitDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void HintDelegate(int hint, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr CreateDelegate(int width, int height, [MarshalAs(UnmanagedType.LPStr)] string title, IntPtr monitor, IntPtr share);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void WindowDelegate(IntPtr window);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void VoidDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void IntDelegate(int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ShouldCloseDelegate(IntPtr window);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void SizeDelegate(IntPtr window, out int width, out int height);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr ProcDelegate([MarshalAs(UnmanagedType.LPStr)] string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void ClearColorDelegate(float r, float g, float b, float a);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void ClearDelegate(uint mask);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void ViewportDelegate(int x, int y, int width, int height);

    internal static readonly InitDelegate Init = Get<InitDelegate>("glfwInit");
    internal static readonly HintDelegate WindowHint = Get<HintDelegate>("glfwWindowHint");
    internal static readonly CreateDelegate CreateWindow = Get<CreateDelegate>("glfwCreateWindow");
    internal static readonly WindowDelegate MakeContextCurrent = Get<WindowDelegate>("glfwMakeContextCurrent");
    internal static readonly WindowDelegate DestroyWindow = Get<WindowDelegate>("glfwDestroyWindow");
    internal static readonly WindowDelegate SwapBuffers = Get<WindowDelegate>("glfwSwapBuffers");
    internal static readonly VoidDelegate PollEvents = Get<VoidDelegate>("glfwPollEvents");
    internal static readonly IntDelegate SwapInterval = Get<IntDelegate>("glfwSwapInterval");
    internal static readonly ShouldCloseDelegate WindowShouldClose = Get<ShouldCloseDelegate>("glfwWindowShouldClose");
    internal static readonly SizeDelegate GetFramebufferSize = Get<SizeDelegate>("glfwGetFramebufferSize");
    internal static readonly ProcDelegate GetProcAddress = Get<ProcDelegate>("glfwGetProcAddress");

    internal static T GetGl<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(GetProcAddress(name));
    static T Get<T>(string name) where T : Delegate
    {
#if NETFRAMEWORK
        var address = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GetProcAddressWindows(library, name) :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? GetProcAddressMac(library, name) : GetProcAddressLinux(library, name);
#else
        var address = NativeLibrary.GetExport(library, name);
#endif
        if (address == IntPtr.Zero) throw new EntryPointNotFoundException(name);
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    static IntPtr Load()
    {
        var name = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "glfw3.dll" :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libglfw.3.dylib" : "libglfw.so.3";
#if NETFRAMEWORK
        var handle = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? LoadLibraryWindows(name) :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? LoadLibraryMac(name, 2) : LoadLibraryLinux(name, 2);
        if (handle == IntPtr.Zero) throw new DllNotFoundException(name);
        return handle;
#else
        return NativeLibrary.Load(name, typeof(Glfw).Assembly, null);
#endif
    }

#if NETFRAMEWORK
    [DllImport("kernel32", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode)] static extern IntPtr LoadLibraryWindows(string name);
    [DllImport("kernel32", EntryPoint = "GetProcAddress", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddressWindows(IntPtr handle, string name);
    [DllImport("libdl.so.2", EntryPoint = "dlopen")] static extern IntPtr LoadLibraryLinux(string name, int mode);
    [DllImport("libdl.so.2", EntryPoint = "dlsym")] static extern IntPtr GetProcAddressLinux(IntPtr handle, string name);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlopen")] static extern IntPtr LoadLibraryMac(string name, int mode);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlsym")] static extern IntPtr GetProcAddressMac(IntPtr handle, string name);
#endif
}
