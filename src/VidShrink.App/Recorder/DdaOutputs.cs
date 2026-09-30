using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

/// <summary>
/// ffmpeg'in <c>ddagrab</c> icin actigi varsayilan D3D11 aygitinin (DXGI fabrikasinin ilk
/// ekran karti) cikislari. <c>output_idx</c> bu kartin <c>EnumOutputs</c> sirasidir; sira
/// Avalonia'nin ekran sirasini izlemiyor, eslesme masaustu koordinatindan yapiliyor.
/// Dondurulmus cikis listeye girmiyor, sirasi yine sayiliyor. Okunamazsa bos liste doner ve
/// kayit <c>gdigrab</c>'a duser.
/// </summary>
internal static class DdaOutputs
{
    private const int RotationUnspecified = 0;
    private const int RotationIdentity = 1;
    private const int MaxOutputs = 16;

    internal static IReadOnlyList<DdaOutput> Enumerate()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<DdaOutput>();

        try { return Read(); }
        catch (Exception ex) when (ex is COMException or DllNotFoundException or EntryPointNotFoundException or InvalidCastException)
        {
            return Array.Empty<DdaOutput>();
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static IReadOnlyList<DdaOutput> Read()
    {
        var found = new List<DdaOutput>();
        var iid = typeof(IDxgiFactory1).GUID;
        if (CreateDXGIFactory1(ref iid, out var factoryObject) < 0 || factoryObject is not IDxgiFactory1 factory) return found;

        try
        {
            if (factory.EnumAdapters(0, out var adapter) < 0 || adapter is null) return found;
            try
            {
                for (uint index = 0; index < MaxOutputs; index++)
                {
                    if (adapter.EnumOutputs(index, out var output) < 0 || output is null) break;
                    try
                    {
                        if (output.GetDesc(out var desc) < 0 || desc.AttachedToDesktop == 0) continue;
                        if (desc.Rotation != RotationUnspecified && desc.Rotation != RotationIdentity) continue;
                        var width = desc.Right - desc.Left;
                        var height = desc.Bottom - desc.Top;
                        if (width > 1 && height > 1) found.Add(new DdaOutput((int)index, desc.Left, desc.Top, width, height));
                    }
                    finally { Marshal.ReleaseComObject(output); }
                }
            }
            finally { Marshal.ReleaseComObject(adapter); }
        }
        finally { Marshal.ReleaseComObject(factory); }

        return found;
    }

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object factory);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OutputDesc
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int AttachedToDesktop;
        public int Rotation;
        public IntPtr Monitor;
    }

    [ComImport, Guid("770aae78-f26f-4dba-a829-253c83d1b387"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDxgiFactory1
    {
        void SetPrivateData();
        void SetPrivateDataInterface();
        void GetPrivateData();
        void GetParent();

        [PreserveSig]
        int EnumAdapters(uint index, out IDxgiAdapter? adapter);
    }

    [ComImport, Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDxgiAdapter
    {
        void SetPrivateData();
        void SetPrivateDataInterface();
        void GetPrivateData();
        void GetParent();

        [PreserveSig]
        int EnumOutputs(uint index, out IDxgiOutput? output);
    }

    [ComImport, Guid("ae02eedb-c735-4690-8d52-5a8dc20213aa"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDxgiOutput
    {
        void SetPrivateData();
        void SetPrivateDataInterface();
        void GetPrivateData();
        void GetParent();

        [PreserveSig]
        int GetDesc(out OutputDesc desc);
    }
}
