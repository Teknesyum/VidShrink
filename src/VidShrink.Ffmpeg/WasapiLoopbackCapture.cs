using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

/// <summary>
/// Varsayilan cikis cihazini WASAPI loopback ile okur. COM nesneleri kendi is parcaciginda
/// acilir ve yalniz orada kullanilir: <c>IAudioClient</c>'in vekili yok, baska daireden
/// cagrilamaz. Okunan ornekler bir halkada birikir, <see cref="Read"/> halkayi bosaltir.
/// <para>
/// Bicimi Windows cevirir (<c>AUTOCONVERTPCM</c>), boylece cihazin kendi bicimi ne olursa olsun
/// buradan <see cref="LoopbackAudio"/> bicimi cikar. Cihaz saniyede bir yoklanir: varsayilan
/// cikis degistiyse yenisi acilir, hic cihaz yoksa acilis saniyede bir yeniden denenir.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WasapiLoopbackCapture : ISystemAudioCapture
{
    private const int RingBytes = LoopbackAudio.SampleRate * LoopbackAudio.BytesPerFrame;
    private const int PollMs = 10;
    private const int DeviceCheckMs = 1000;
    private const int FirstOpenWaitMs = 2000;
    private const long BufferDuration100Ns = 2_000_000;

    private const int RenderFlow = 0;
    private const int ConsoleRole = 0;
    private const int ClsCtxAll = 23;
    private const int SharedMode = 0;
    private const uint StreamFlags = 0x00020000 | 0x80000000 | 0x08000000;
    private const uint BufferSilent = 0x2;

    private static readonly Guid AudioClientId = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    private static readonly Guid CaptureClientId = new("C8ADBD64-E71E-48A0-A4DE-185C395CD317");

    private readonly object _gate = new();
    private readonly byte[] _ring = new byte[RingBytes];
    private readonly ManualResetEventSlim _stop = new(false);
    private readonly ManualResetEventSlim _opened = new(false);
    private readonly Thread _thread;
    private int _head;
    private int _count;
    private volatile int _state = (int)SystemAudioState.Unavailable;

    private IAudioClient? _client;
    private IAudioCaptureClient? _capture;
    private string? _deviceId;
    private string? _firstDeviceId;
    private byte[] _scratch = new byte[RingBytes / 5];

    private WasapiLoopbackCapture()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "VidShrink loopback" };
    }

    public static WasapiLoopbackCapture Start()
    {
        var capture = new WasapiLoopbackCapture();
        capture._thread.Start();
        capture._opened.Wait(FirstOpenWaitMs);
        return capture;
    }

    public SystemAudioState State => (SystemAudioState)_state;

    public int Read(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        lock (_gate)
        {
            var take = Math.Min(_count, buffer.Length);
            take -= take % LoopbackAudio.BytesPerFrame;
            var first = Math.Min(take, RingBytes - _head);
            Buffer.BlockCopy(_ring, _head, buffer, 0, first);
            Buffer.BlockCopy(_ring, 0, buffer, first, take - first);
            _head = (_head + take) % RingBytes;
            _count -= take;
            return take;
        }
    }

    public void Dispose()
    {
        _stop.Set();
        if (_thread.IsAlive && Thread.CurrentThread != _thread) _thread.Join(FirstOpenWaitMs);
    }

    private void Run()
    {
        IMMDeviceEnumerator? enumerator = null;
        try
        {
            try { enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorClass(); }
            catch (Exception ex) when (ex is COMException or InvalidCastException) { }

            if (enumerator is not null) Open(enumerator);
            _opened.Set();

            var check = Stopwatch.StartNew();
            while (!_stop.Wait(PollMs))
            {
                if (enumerator is null) continue;

                if (_capture is not null && !Drain(_capture)) Close();

                if (check.ElapsedMilliseconds < DeviceCheckMs) continue;
                check.Restart();

                if (_capture is null || !string.Equals(DefaultId(enumerator), _deviceId, StringComparison.Ordinal))
                {
                    Close();
                    Open(enumerator);
                }
            }
        }
        finally
        {
            _opened.Set();
            Close();
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }

    private static string? DefaultId(IMMDeviceEnumerator enumerator)
    {
        if (enumerator.GetDefaultAudioEndpoint(RenderFlow, ConsoleRole, out var device) < 0 || device is null) return null;
        try { return device.GetId(out var id) < 0 ? null : id; }
        finally { Marshal.ReleaseComObject(device); }
    }

    private void Open(IMMDeviceEnumerator enumerator)
    {
        if (enumerator.GetDefaultAudioEndpoint(RenderFlow, ConsoleRole, out var device) < 0 || device is null) return;

        IAudioClient? client = null;
        try
        {
            if (device.GetId(out var id) < 0 || id is null) return;

            var iid = AudioClientId;
            if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out var activated) < 0 || activated is not IAudioClient opened) return;
            client = opened;

            var format = new WaveFormat
            {
                FormatTag = 1,
                Channels = LoopbackAudio.Channels,
                SamplesPerSec = LoopbackAudio.SampleRate,
                AvgBytesPerSec = LoopbackAudio.SampleRate * LoopbackAudio.BytesPerFrame,
                BlockAlign = LoopbackAudio.BytesPerFrame,
                BitsPerSample = 16,
                Size = 0
            };

            if (client.Initialize(SharedMode, StreamFlags, BufferDuration100Ns, 0, ref format, IntPtr.Zero) < 0) return;

            var service = CaptureClientId;
            if (client.GetService(ref service, out var served) < 0 || served is not IAudioCaptureClient capture) return;
            if (client.Start() < 0)
            {
                Marshal.ReleaseComObject(capture);
                return;
            }

            _client = client;
            _capture = capture;
            _deviceId = id;
            _firstDeviceId ??= id;
            client = null;
            _state = (int)(string.Equals(id, _firstDeviceId, StringComparison.Ordinal)
                ? SystemAudioState.Capturing
                : SystemAudioState.Switched);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
        }
        finally
        {
            if (client is not null) Marshal.ReleaseComObject(client);
            Marshal.ReleaseComObject(device);
        }
    }

    private void Close()
    {
        _state = (int)SystemAudioState.Unavailable;
        var client = _client;
        var capture = _capture;
        _client = null;
        _capture = null;
        _deviceId = null;

        try
        {
            if (client is not null) _ = client.Stop();
        }
        catch (Exception ex) when (ex is COMException or InvalidComObjectException) { }

        if (capture is not null) Marshal.ReleaseComObject(capture);
        if (client is not null) Marshal.ReleaseComObject(client);
    }

    private bool Drain(IAudioCaptureClient capture)
    {
        try
        {
            while (true)
            {
                if (capture.GetNextPacketSize(out var pending) < 0) return false;
                if (pending == 0) return true;

                if (capture.GetBuffer(out var data, out var frames, out var flags, out _, out _) < 0) return false;
                var bytes = checked((int)frames * LoopbackAudio.BytesPerFrame);
                if (bytes > 0)
                {
                    if (_scratch.Length < bytes) _scratch = new byte[bytes];
                    if ((flags & BufferSilent) != 0 || data == IntPtr.Zero) Array.Clear(_scratch, 0, bytes);
                    else Marshal.Copy(data, _scratch, 0, bytes);
                    Push(_scratch, bytes);
                }

                if (capture.ReleaseBuffer(frames) < 0) return false;
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidComObjectException or OverflowException)
        {
            return false;
        }
    }

    private void Push(byte[] source, int bytes)
    {
        lock (_gate)
        {
            if (bytes > RingBytes)
            {
                Buffer.BlockCopy(source, bytes - RingBytes, _ring, 0, RingBytes);
                _head = 0;
                _count = RingBytes;
                return;
            }

            var overflow = _count + bytes - RingBytes;
            if (overflow > 0)
            {
                _head = (_head + overflow) % RingBytes;
                _count -= overflow;
            }

            var tail = (_head + _count) % RingBytes;
            var first = Math.Min(bytes, RingBytes - tail);
            Buffer.BlockCopy(source, 0, _ring, tail, first);
            Buffer.BlockCopy(source, first, _ring, 0, bytes - first);
            _count += bytes;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSec;
        public uint AvgBytesPerSec;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort Size;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorClass
    {
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? instance);

        [PreserveSig]
        int OpenPropertyStore(int access, out IntPtr store);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string? id);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig]
        int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, ref WaveFormat format, IntPtr sessionId);

        [PreserveSig]
        int GetBufferSize(out uint frames);

        [PreserveSig]
        int GetStreamLatency(out long latency);

        [PreserveSig]
        int GetCurrentPadding(out uint frames);

        [PreserveSig]
        int IsFormatSupported(int shareMode, ref WaveFormat format, out IntPtr closest);

        [PreserveSig]
        int GetMixFormat(out IntPtr format);

        [PreserveSig]
        int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);

        [PreserveSig]
        int Start();

        [PreserveSig]
        int Stop();

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int SetEventHandle(IntPtr handle);

        [PreserveSig]
        int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object? service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        [PreserveSig]
        int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);

        [PreserveSig]
        int ReleaseBuffer(uint frames);

        [PreserveSig]
        int GetNextPacketSize(out uint frames);
    }
}
