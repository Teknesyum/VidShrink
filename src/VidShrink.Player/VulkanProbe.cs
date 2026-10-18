using System.Runtime.InteropServices;

namespace VidShrink.Player;

/// <summary>
/// <c>vf gpu</c> takılmadan önce Vulkan aygıtı var mı diye bakar. GPU'suz makinede süzgeç
/// takılırken libmpv süreci erişim ihlaliyle düşürüyor (CI koşucusu, 18 Ekim 2026); aygıt
/// yoksa halka takılmaz. Sonuç süreç başına bir kez okunur.
/// </summary>
internal static class VulkanProbe
{
    private static readonly Lazy<bool> Result = new(Probe, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static Func<bool>? Override { get; set; }

    internal static bool HasDevice => Override?.Invoke() ?? Result.Value;

    internal static IEnumerable<string> LoaderNames()
    {
        if (OperatingSystem.IsWindows()) return new[] { "vulkan-1.dll" };
        if (OperatingSystem.IsMacOS()) return new[] { "libvulkan.1.dylib", "libMoltenVK.dylib" };
        return new[] { "libvulkan.so.1", "libvulkan.so" };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InstanceCreateInfo
    {
        public int SType;
        public IntPtr Next;
        public uint Flags;
        public IntPtr ApplicationInfo;
        public uint LayerCount;
        public IntPtr Layers;
        public uint ExtensionCount;
        public IntPtr Extensions;
    }

    private static unsafe bool Probe()
    {
        var loader = IntPtr.Zero;
        using (LoaderErrorMode.Suppress())
        {
            foreach (var name in LoaderNames())
                if (NativeLibrary.TryLoad(name, out loader)) break;
        }

        if (loader == IntPtr.Zero) return OperatingSystem.IsMacOS();

        if (!NativeLibrary.TryGetExport(loader, "vkCreateInstance", out var create)
            || !NativeLibrary.TryGetExport(loader, "vkEnumeratePhysicalDevices", out var enumerate)
            || !NativeLibrary.TryGetExport(loader, "vkDestroyInstance", out var destroy))
            return false;

        var info = new InstanceCreateInfo { SType = 1 };
        var instance = IntPtr.Zero;
        var created = ((delegate* unmanaged[Cdecl]<InstanceCreateInfo*, IntPtr, IntPtr*, int>)create)(&info, IntPtr.Zero, &instance);
        if (created != 0 || instance == IntPtr.Zero) return false;

        try
        {
            uint count = 0;
            var listed = ((delegate* unmanaged[Cdecl]<IntPtr, uint*, IntPtr, int>)enumerate)(instance, &count, IntPtr.Zero);
            return listed >= 0 && count > 0;
        }
        finally
        {
            ((delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void>)destroy)(instance, IntPtr.Zero);
        }
    }
}
