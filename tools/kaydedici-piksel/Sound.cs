using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using VidShrink.App.Recorder;

namespace VidShrink.KaydediciPiksel;

internal static class Sound
{
    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;
    private const uint SndFileName = 0x00020000;

    internal static void Run()
    {
        var thread = new Thread(Measure);
        if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        thread.Join();
    }

    private static void Measure()
    {
        Program.Say($"ses\twaveOutGetNumDevs\t{waveOutGetNumDevs()}");
        IAudioMeterInformation meter;
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            var hr = enumerator.GetDefaultAudioEndpoint(0, 1, out var device);
            device.GetId(out var id);
            var iid = typeof(IAudioMeterInformation).GUID;
            device.Activate(ref iid, 1, IntPtr.Zero, out var activated);
            meter = (IAudioMeterInformation)activated;
            Program.Say($"ses\tvarsayilan cikis\t{id}\thr\t{hr}");
        }
        catch (Exception e)
        {
            Program.Say($"ses\tolcer acilamadi\t{e.GetType().Name}\t{e.Message}");
            return;
        }

        var tone = ClickTone.Wave();
        var pinned = GCHandle.Alloc(tone, GCHandleType.Pinned);
        try
        {
            Window(meter, "sessizlik-1", () => null);
            Window(meter, "PlaySoundW bellek async", () => PlaySoundW(pinned.AddrOfPinnedObject(), 0, SndAsync | SndNoDefault | SndMemory));
            Window(meter, "sessizlik-2", () => null);
            Window(meter, "Win32ClickSound.Play", () =>
            {
                new Win32ClickSound().Play();
                return null;
            });
            Window(meter, "NoClickSound.Play", () =>
            {
                new NoClickSound().Play();
                return null;
            });
            var missing = Path.Combine(Program.Out, "olmayan-" + Guid.NewGuid().ToString("N") + ".wav");
            Window(meter, "PlaySoundW olmayan WAV", () => PlaySoundWFile(missing, 0, SndAsync | SndNoDefault | SndFileName));
            Window(meter, "PlaySoundW olmayan WAV senkron", () => PlaySoundWFile(missing, 0, SndNoDefault | SndFileName));
            Window(meter, "sessizlik-3", () => null);
        }
        finally
        {
            PlaySoundW(0, 0, 0);
            pinned.Free();
        }
    }

    private static void Window(IAudioMeterInformation meter, string name, Func<bool?> act)
    {
        var baseline = Peak(meter, 150);
        var clock = Stopwatch.StartNew();
        var returned = act();
        var peaks = new List<(long Ms, float Peak)>();
        while (clock.ElapsedMilliseconds < 500)
        {
            meter.GetPeakValue(out var peak);
            peaks.Add((clock.ElapsedMilliseconds, peak));
            Thread.Sleep(2);
        }

        var max = peaks.Max(p => p.Peak);
        var first = peaks.FirstOrDefault(p => p.Peak > 0.01f);
        var above = peaks.Count(p => p.Peak > 0.01f);
        Program.Say(string.Create(CultureInfo.InvariantCulture,
            $"ses\t{name}\tdonus\t{(returned is null ? "-" : returned.ToString())}\tonceki tepe\t{baseline:0.0000}\ttepe\t{max:0.0000}\t>0.01 ornek\t{above}/{peaks.Count}\tilk ms\t{(above > 0 ? first.Ms : -1)}"));
    }

    private static float Peak(IAudioMeterInformation meter, int ms)
    {
        var clock = Stopwatch.StartNew();
        var max = 0f;
        while (clock.ElapsedMilliseconds < ms)
        {
            meter.GetPeakValue(out var peak);
            max = Math.Max(max, peak);
            Thread.Sleep(2);
        }

        return max;
    }

    [DllImport("winmm.dll")]
    private static extern bool PlaySoundW(nint sound, nint module, uint flags);

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode)]
    private static extern bool PlaySoundWFile(string sound, nint module, uint flags);

    [DllImport("winmm.dll")]
    private static extern uint waveOutGetNumDevs();

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator
    {
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object value);

        int OpenPropertyStore(int access, out IntPtr store);

        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        int GetState(out int state);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        int GetPeakValue(out float peak);

        int GetMeteringChannelCount(out int count);

        int GetChannelsPeakValues(int count, [Out] float[] peaks);

        int QueryHardwareSupport(out int mask);
    }
}
