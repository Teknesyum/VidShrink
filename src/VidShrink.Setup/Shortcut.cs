using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using VidShrink.Core.Setup;

namespace VidShrink.Setup;

[GeneratedComInterface]
[Guid("000214F9-0000-0000-C000-000000000046")]
internal partial interface IShellLinkW
{
    void GetPath(IntPtr file, int size, IntPtr data, uint flags);
    void GetIDList(out IntPtr list);
    void SetIDList(IntPtr list);
    void GetDescription(IntPtr name, int size);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
    void GetWorkingDirectory(IntPtr directory, int size);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
    void GetArguments(IntPtr arguments, int size);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
    void GetHotkey(out short hotkey);
    void SetHotkey(short hotkey);
    void GetShowCmd(out int showCommand);
    void SetShowCmd(int showCommand);
    void GetIconLocation(IntPtr iconPath, int size, out int icon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int icon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
    void Resolve(IntPtr window, uint flags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
}

[GeneratedComInterface]
[Guid("0000010B-0000-0000-C000-000000000046")]
internal partial interface IPersistFile
{
    void GetClassID(out Guid classId);
    [PreserveSig]
    int IsDirty();
    void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
    void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
    void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
    void GetCurFile(out IntPtr fileName);
}

internal sealed partial class ShellShortcuts : IShortcutWriter
{
    private static readonly Guid ShellLinkClass = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid ShellLinkInterface = new("000214F9-0000-0000-C000-000000000046");

    public void Write(string shortcutPath, string target, string workingDirectory, string icon)
    {
        OnSta(() =>
        {
            var link = Create();
            link.SetPath(target);
            link.SetWorkingDirectory(workingDirectory);
            var comma = icon.LastIndexOf(',');
            var index = comma > 0 && int.TryParse(icon[(comma + 1)..], out var parsed) ? parsed : 0;
            link.SetIconLocation(comma > 0 ? icon[..comma] : icon, index);
            ((IPersistFile)link).Save(shortcutPath, true);
            return 0;
        });
    }

    public string? ReadTarget(string shortcutPath) => OnSta(() =>
    {
        var link = Create();
        ((IPersistFile)link).Load(shortcutPath, 0);
        var buffer = Marshal.AllocHGlobal(32768 * sizeof(char));
        try
        {
            Marshal.WriteInt16(buffer, 0);
            link.GetPath(buffer, 32768, IntPtr.Zero, 0);
            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    });

    private static IShellLinkW Create()
    {
        var classId = ShellLinkClass;
        var interfaceId = ShellLinkInterface;
        Marshal.ThrowExceptionForHR(CoCreateInstance(ref classId, IntPtr.Zero, 1, ref interfaceId, out var pointer));
        try
        {
            return (IShellLinkW)ComWrappersFor.Instance.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.UniqueInstance);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    private static T OnSta<T>(Func<T> work)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new SetupException(SetupText.Get("setup.shortcut.failed", failure.Message), failure);
        return result;
    }

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid classId, IntPtr outer, uint context, ref Guid interfaceId, out IntPtr pointer);

    private static class ComWrappersFor
    {
        public static readonly StrategyBasedComWrappers Instance = new();
    }
}
