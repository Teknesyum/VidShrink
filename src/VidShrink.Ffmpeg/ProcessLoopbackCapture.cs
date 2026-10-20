using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

[SupportedOSPlatform("windows")]
internal sealed class ProcessLoopbackCapture : ISystemAudioCapture
{
    private const string VirtualDevice = @"VAD\Process_Loopback";
    private const int PollMs = 10;
    private const int RetryMs = 1000;
    private const int FirstOpenWaitMs = 2000;
    private const int ActivateWaitMs = 2000;
    private const long BufferDuration100Ns = 2_000_000;
    private const int SharedMode = 0;
    private const uint StreamFlags = 0x00020000 | 0x00040000 | 0x80000000 | 0x08000000;
    private const uint BufferSilent = 0x2;
    private const ushort VtBlob = 65;

    private static readonly Guid AudioClientId = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    private static readonly Guid CaptureClientId = new("C8ADBD64-E71E-48A0-A4DE-185C395CD317");

    private readonly Func<int?> _resolve;
    private readonly PcmRing _ring = new(LoopbackAudio.SampleRate * LoopbackAudio.BytesPerFrame);
    private readonly ManualResetEventSlim _stop = new(false);
    private readonly ManualResetEventSlim _opened = new(false);
    private readonly AutoResetEvent _ready = new(false);
    private readonly Thread _thread;
    private volatile int _state = (int)SystemAudioState.Unavailable;

    private IAudioClient? _client;
    private IAudioCaptureClient? _capture;
    private byte[] _scratch = new byte[LoopbackAudio.SampleRate * LoopbackAudio.BytesPerFrame / 5];

    private ProcessLoopbackCapture(Func<int?> resolve)
    {
        _resolve = resolve;
        _thread = new Thread(Run) { IsBackground = true, Name = "VidShrink app loopback" };
    }

    public static ProcessLoopbackCapture Start(Func<int?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        var capture = new ProcessLoopbackCapture(resolve);
        capture._thread.Start();
        capture._opened.Wait(FirstOpenWaitMs);
        return capture;
    }

    public SystemAudioState State => (SystemAudioState)_state;

    public int Read(byte[] buffer) => _ring.Read(buffer);

    public void Dispose()
    {
        _stop.Set();
        if (_thread.IsAlive && Thread.CurrentThread != _thread) _thread.Join(FirstOpenWaitMs);
    }

    private void Run()
    {
        try
        {
            Open();
            _opened.Set();

            var retry = Stopwatch.StartNew();
            var handles = new WaitHandle[] { _ready, _stop.WaitHandle };
            while (!_stop.IsSet)
            {
                WaitHandle.WaitAny(handles, PollMs);
                if (_stop.IsSet) break;

                if (_capture is not null)
                {
                    if (!Drain(_capture)) Close();
                    continue;
                }

                if (retry.ElapsedMilliseconds < RetryMs) continue;
                retry.Restart();
                Open();
            }
        }
        finally
        {
            _opened.Set();
            Close();
        }
    }

    private void Open()
    {
        int? processId;
        try { processId = _resolve(); }
        catch (Exception ex) when (ex is InvalidOperationException or COMException or UnauthorizedAccessException) { processId = null; }
        if (processId is not { } pid || pid <= 0) return;

        IAudioClient? client = null;
        var parameters = IntPtr.Zero;
        var variant = IntPtr.Zero;
        try
        {
            var blob = AppAudio.ActivationParams(pid);
            parameters = Marshal.AllocHGlobal(blob.Length);
            Marshal.Copy(blob, 0, parameters, blob.Length);
            variant = Marshal.AllocHGlobal(Marshal.SizeOf<PropVariantBlob>());
            Marshal.StructureToPtr(new PropVariantBlob { Type = VtBlob, Size = (uint)blob.Length, Data = parameters }, variant, false);

            var handler = new Completion();
            var iid = AudioClientId;
            if (ActivateAudioInterfaceAsync(VirtualDevice, ref iid, variant, handler, out var operation) < 0 || operation is null) return;
            try
            {
                if (!handler.Done.Wait(ActivateWaitMs)) return;
                if (operation.GetActivateResult(out var result, out var activated) < 0 || result < 0 || activated is not IAudioClient opened) return;
                client = opened;
            }
            finally
            {
                Marshal.ReleaseComObject(operation);
            }

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
            if (client.SetEventHandle(_ready.SafeWaitHandle.DangerousGetHandle()) < 0) return;

            var service = CaptureClientId;
            if (client.GetService(ref service, out var served) < 0 || served is not IAudioCaptureClient capture) return;
            if (client.Start() < 0)
            {
                Marshal.ReleaseComObject(capture);
                return;
            }

            _client = client;
            _capture = capture;
            client = null;
            _state = (int)SystemAudioState.Capturing;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException)
        {
        }
        finally
        {
            if (client is not null) Marshal.ReleaseComObject(client);
            if (variant != IntPtr.Zero) Marshal.FreeHGlobal(variant);
            if (parameters != IntPtr.Zero) Marshal.FreeHGlobal(parameters);
        }
    }

    private void Close()
    {
        _state = (int)SystemAudioState.Unavailable;
        var client = _client;
        var capture = _capture;
        _client = null;
        _capture = null;

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
                    _ring.Push(_scratch, bytes);
                }

                if (capture.ReleaseBuffer(frames) < 0) return false;
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidComObjectException or OverflowException)
        {
            return false;
        }
    }

    [DllImport("Mmdevapi.dll", ExactSpelling = true)]
    private static extern int ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        ref Guid riid,
        IntPtr activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation? activationOperation);

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariantBlob
    {
        public ushort Type;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public uint Size;
        public IntPtr Data;
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

    [ComVisible(true)]
    private sealed class Completion : IActivateAudioInterfaceCompletionHandler, IAgileObject
    {
        public ManualResetEventSlim Done { get; } = new(false);

        public int ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation)
        {
            Done.Set();
            return 0;
        }
    }

    [ComImport, Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAgileObject
    {
    }

    [ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceCompletionHandler
    {
        [PreserveSig]
        int ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation);
    }

    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceAsyncOperation
    {
        [PreserveSig]
        int GetActivateResult(out int activateResult, [MarshalAs(UnmanagedType.IUnknown)] out object? activatedInterface);
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

internal sealed class PcmRing
{
    private readonly object _gate = new();
    private readonly byte[] _ring;
    private int _head;
    private int _count;

    public PcmRing(int bytes)
    {
        if (bytes <= 0 || bytes % LoopbackAudio.BytesPerFrame != 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        _ring = new byte[bytes];
    }

    public int Read(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        lock (_gate)
        {
            var take = Math.Min(_count, buffer.Length);
            take -= take % LoopbackAudio.BytesPerFrame;
            var first = Math.Min(take, _ring.Length - _head);
            Buffer.BlockCopy(_ring, _head, buffer, 0, first);
            Buffer.BlockCopy(_ring, 0, buffer, first, take - first);
            _head = (_head + take) % _ring.Length;
            _count -= take;
            return take;
        }
    }

    public void Push(byte[] source, int bytes)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (_gate)
        {
            if (bytes > _ring.Length)
            {
                Buffer.BlockCopy(source, bytes - _ring.Length, _ring, 0, _ring.Length);
                _head = 0;
                _count = _ring.Length;
                return;
            }

            var overflow = _count + bytes - _ring.Length;
            if (overflow > 0)
            {
                _head = (_head + overflow) % _ring.Length;
                _count -= overflow;
            }

            var tail = (_head + _count) % _ring.Length;
            var first = Math.Min(bytes, _ring.Length - tail);
            Buffer.BlockCopy(source, 0, _ring, tail, first);
            Buffer.BlockCopy(source, first, _ring, 0, bytes - first);
            _count += bytes;
        }
    }
}
