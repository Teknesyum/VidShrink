using System.Diagnostics;
using System.Globalization;
using System.Text;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

internal static class SesKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "dalga8c");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));
}

/// <summary>
/// Cihaz listesi ciktilarinin sabit metinleri. Sayim pimleri canli ciktidan degil buradan
/// okunur: bu depoda bir olcerin surum ofseti, sürüm sinirini gecen kiyasi gecersiz
/// kilmisti. <see cref="Ffmpeg9Dshow"/> bu makinedeki gercek ffmpeg 9.0 ciktisidir
/// (<c>.calisma/dalga8c/ham-dshow-listesi.txt</c>), surucu gunluk satirlari dahil oldugu
/// gibi; <see cref="Ffmpeg6Dshow"/> bolum baslikli eski bicim.
/// </summary>
internal static class CihazCiktilari
{
    internal const string Ffmpeg9Dshow = """
        I2026-09-12 15:17:48.340735 ( 4304) [INFO] [VCAMDS] ffmpeg.exe
        I2026-09-12 15:17:48.341748 ( 4304) [INFO] [VCAMDS] Creating WndMsg Listener Window
        I2026-09-12 15:17:48.345875 ( 4304) [INFO] [VCAMDS] Unregistered window class
        [in#0 @ 000002efbe997f40] "Camera (NVIDIA Broadcast)" (video)
        [in#0 @ 000002efbe997f40]   Alternative name "@device_sw_{860BB310-5D01-11D0-BD3B-00A0C911CE86}\{7BBFF097-B3FB-4B26-B685-7A998DE7CEAC}"
        [in#0 @ 000002efbe997f40] "OBS Virtual Camera" (video)
        [in#0 @ 000002efbe997f40]   Alternative name "@device_sw_{860BB310-5D01-11D0-BD3B-00A0C911CE86}\{A3FCE0F5-3493-419F-958A-ABA1250EC20B}"
        [in#0 @ 000002efbe997f40] "SteelSeries Sonar - Microphone (SteelSeries Sonar Virtual Audio Device)" (audio)
        [in#0 @ 000002efbe997f40]   Alternative name "@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}\wave_{90D863D0-0974-4E3F-8FD5-1919988FF283}"
        [in#0 @ 000002efbe997f40] "Mikrofon (Arctis Nova Pro Wireless)" (audio)
        [in#0 @ 000002efbe997f40]   Alternative name "@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}\wave_{33B98ADF-B333-4074-9973-E52442136578}"
        Error opening input file dummy.
        """;

    internal const string Ffmpeg6Dshow = """
        [dshow @ 0000018e3f2c1040] DirectShow video devices (some may be both video and audio devices)
        [dshow @ 0000018e3f2c1040]  "Integrated Camera"
        [dshow @ 0000018e3f2c1040]     Alternative name "@device_pnp_\\?\usb#vid_04f2"
        [dshow @ 0000018e3f2c1040] DirectShow audio devices
        [dshow @ 0000018e3f2c1040]  "Microphone (Realtek High Definition Audio)"
        [dshow @ 0000018e3f2c1040]     Alternative name "@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}\wave_{AAAA}"
        [dshow @ 0000018e3f2c1040]  "Stereo Mix (Realtek High Definition Audio)"
        [dshow @ 0000018e3f2c1040]     Alternative name "@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}\wave_{BBBB}"
        dummy: Immediate exit requested
        """;

    internal const string Avfoundation = """
        [AVFoundation indev @ 0x7f8e5b504600] AVFoundation video devices:
        [AVFoundation indev @ 0x7f8e5b504600] [0] FaceTime HD Camera
        [AVFoundation indev @ 0x7f8e5b504600] [1] Capture screen 0
        [AVFoundation indev @ 0x7f8e5b504600] AVFoundation audio devices:
        [AVFoundation indev @ 0x7f8e5b504600] [0] Built-in Microphone
        [AVFoundation indev @ 0x7f8e5b504600] [1] BlackHole 2ch Loopback
        : Input/output error
        """;

    internal const string Pactl =
        "0\talsa_output.pci-0000_00_1f.3.analog-stereo.monitor\tPipeWire\ts16le 2ch 48000Hz\tIDLE\n" +
        "1\talsa_input.pci-0000_00_1f.3.analog-stereo\tPipeWire\ts16le 2ch 48000Hz\tSUSPENDED\n" +
        "2\tbluez_input.AC_80_0A.headset\tPipeWire\ts16le 1ch 16000Hz\tSUSPENDED\n";
}

/// <summary>
/// Canli cihaz olcusu yalniz makinede listelenmis bir mikrofon varsa kurulur. Erken
/// donen bir <c>[Fact]</c> yerine gecit: bu depoda sifir teste denk gelen bir filtre kolu
/// yesil donmus ve iki muhurlu sozlesmede fark edilmemisti; atlanan olcu sebebiyle
/// gorunur, sessizce gecen olcu gorunmez.
/// </summary>
public sealed class SesCihaziFactAttribute : FactAttribute
{
    public SesCihaziFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
        {
            Skip = $"{missing} bulunamadi, canli ses cihazi olcusu kosturulmadi.";
            return;
        }

        var liste = CaptureDevices.Instance;
        if (!liste.Loaded)
        {
            Skip = "cihaz listesi okunamadi (surec donmedi), canli olcu kurulmadi.";
            return;
        }

        if (!liste.Audio.Any(d => d.Role == AudioSourceRole.Microphone))
            Skip = $"makinede listelenmis mikrofon yok ({liste.Audio.Count} ses cihazi), " +
                   "canli yakalama olcusu kurulmadi.";
    }
}

public sealed class SesGirisiTests
{
    [Fact]
    public void Ffmpeg9Ciktisindan_IkiSesIkiVideoCihaziAyristirilir()
    {
        var liste = CaptureDevices.ParseDirectShow(CihazCiktilari.Ffmpeg9Dshow);

        var rapor = new StringBuilder();
        rapor.AppendLine("kaynak: bu makinedeki gercek ffmpeg 9.0 -list_devices ciktisi (16 satir)");
        rapor.AppendLine($"video cihaz: {liste.Video.Count}");
        foreach (var v in liste.Video) rapor.AppendLine($"  {v}");
        rapor.AppendLine($"ses cihaz: {liste.Audio.Count}");
        foreach (var a in liste.Audio)
            rapor.AppendLine($"  {a.Name} | rol={a.Role} | referans={a.Reference} | takma={a.Alternative}");
        SesKanit.Write("k1-ffmpeg9-dshow-ayristirma.txt", rapor.ToString());

        Assert.Equal(2, liste.Video.Count);
        Assert.Equal(2, liste.Audio.Count);
        Assert.Equal(new[] { "Camera (NVIDIA Broadcast)", "OBS Virtual Camera" }, liste.Video);
        Assert.Equal(
            "SteelSeries Sonar - Microphone (SteelSeries Sonar Virtual Audio Device)",
            liste.Audio[0].Name);
        Assert.Equal("Mikrofon (Arctis Nova Pro Wireless)", liste.Audio[1].Name);
        Assert.All(liste.Audio, d => Assert.Equal(AudioSourceRole.Microphone, d.Role));
        Assert.All(liste.Audio, d => Assert.StartsWith("@device_cm_", d.Alternative));
        Assert.True(liste.Loaded);
    }

    [Fact]
    public void EskiBicimBolumBaslikliListedeAyniCihazlarOkunur()
    {
        var liste = CaptureDevices.ParseDirectShow(CihazCiktilari.Ffmpeg6Dshow);

        SesKanit.Write("k2-ffmpeg6-dshow-ayristirma.txt",
            $"video: {string.Join(" | ", liste.Video)}\n" +
            $"ses: {string.Join(" | ", liste.Audio.Select(a => $"{a.Name} [{a.Role}]"))}\n");

        Assert.Equal(new[] { "Integrated Camera" }, liste.Video);
        Assert.Equal(2, liste.Audio.Count);
        Assert.Equal(AudioSourceRole.Microphone, liste.Audio[0].Role);
        Assert.Equal(AudioSourceRole.SystemAudio, liste.Audio[1].Role);
        Assert.Equal("@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}\\wave_{BBBB}", liste.Audio[1].Alternative);
    }

    [Fact]
    public void SurucuGunluguVeHataSatiriCihazSayilmaz()
    {
        const string yalnizGurultu = """
            I2026-09-12 15:17:48.340735 ( 4304) [INFO] [VCAMDS] ffmpeg.exe
            [in#0 @ 000002efbe997f40] Could not enumerate audio only devices (or none found).
            Error opening input file dummy.
            Error opening input files: Immediate exit requested
            """;

        var liste = CaptureDevices.ParseDirectShow(yalnizGurultu);

        Assert.Empty(liste.Audio);
        Assert.Empty(liste.Video);
        Assert.True(liste.Loaded);
    }

    [Fact]
    public void AvfoundationVePulseListeleriAyristirilir()
    {
        var mac = CaptureDevices.ParseAvFoundation(CihazCiktilari.Avfoundation);
        var linux = CaptureDevices.ParsePulse(CihazCiktilari.Pactl);

        SesKanit.Write("k3-mac-linux-ayristirma.txt",
            $"avfoundation video: {string.Join(" | ", mac.Video)}\n" +
            $"avfoundation ses: {string.Join(" | ", mac.Audio.Select(a => $"{a.Name} [{a.Role}] -> {a.Reference}"))}\n" +
            $"pulse ses: {string.Join(" | ", linux.Audio.Select(a => $"{a.Name} [{a.Role}]"))}\n");

        Assert.Equal(new[] { "FaceTime HD Camera", "Capture screen 0" }, mac.Video);
        Assert.Equal(2, mac.Audio.Count);
        Assert.Equal("Built-in Microphone", mac.Audio[0].Name);
        Assert.Equal("0", mac.Audio[0].Reference);
        Assert.Equal("1", mac.Audio[1].Reference);
        Assert.Equal(AudioSourceRole.SystemAudio, mac.Audio[1].Role);

        Assert.Equal(3, linux.Audio.Count);
        Assert.Equal(AudioSourceRole.SystemAudio, linux.Audio[0].Role);
        Assert.Equal("alsa_input.pci-0000_00_1f.3.analog-stereo", linux.Audio[1].Reference);
        Assert.Equal(AudioSourceRole.Microphone, linux.Audio[2].Role);
    }

    [Fact]
    public void ListelenenHerCihazIcinArgumanUretilir()
    {
        var listeler = new[]
        {
            ("ffmpeg9-dshow", CaptureDevices.ParseDirectShow(CihazCiktilari.Ffmpeg9Dshow)),
            ("ffmpeg6-dshow", CaptureDevices.ParseDirectShow(CihazCiktilari.Ffmpeg6Dshow)),
            ("avfoundation", CaptureDevices.ParseAvFoundation(CihazCiktilari.Avfoundation)),
            ("pulse", CaptureDevices.ParsePulse(CihazCiktilari.Pactl))
        };

        var rapor = new StringBuilder();
        var sayi = 0;
        foreach (var (etiket, liste) in listeler)
            foreach (var cihaz in liste.Audio)
            {
                var secim = cihaz.Role == AudioSourceRole.Microphone
                    ? new AudioCaptureSelection(Microphone: cihaz)
                    : new AudioCaptureSelection(SystemAudio: cihaz);
                var plan = AudioCaptureArguments.Build(secim, liste.Audio, firstInputIndex: 1);

                sayi++;
                rapor.AppendLine($"{etiket} | {cihaz.Name} [{cihaz.Role}]");
                rapor.AppendLine($"  girdi: {string.Join(" ", plan.Inputs)}");
                rapor.AppendLine($"  eslem: {string.Join(" ", plan.Maps)} | filtre: {plan.FilterComplex ?? "yok"}");

                Assert.Equal(4, plan.Inputs.Count);
                Assert.Equal("-f", plan.Inputs[0]);
                Assert.Equal("-i", plan.Inputs[2]);
                Assert.Equal(1, plan.InputCount);
                Assert.Equal(new[] { "1:a" }, plan.Maps);
                Assert.Null(plan.FilterComplex);

                switch (cihaz.Backend)
                {
                    case CaptureBackend.DirectShow:
                        Assert.Equal("dshow", plan.Inputs[1]);
                        Assert.Equal($"audio={AudioCaptureArguments.EscapeDirectShow(cihaz.Name)}", plan.Inputs[3]);
                        break;
                    case CaptureBackend.AvFoundation:
                        Assert.Equal("avfoundation", plan.Inputs[1]);
                        Assert.Equal($":{cihaz.Reference}", plan.Inputs[3]);
                        break;
                    default:
                        Assert.Equal("pulse", plan.Inputs[1]);
                        Assert.Equal(cihaz.Reference, plan.Inputs[3]);
                        break;
                }
            }

        rapor.AppendLine($"arguman uretilen cihaz: {sayi}");
        SesKanit.Write("k4-her-cihaz-arguman.txt", rapor.ToString());

        Assert.Equal(9, sayi);
    }

    [Fact]
    public void MikrofonVeSistemSesiBirlikteAmixIleBirlesir()
    {
        var liste = CaptureDevices.ParseDirectShow(CihazCiktilari.Ffmpeg6Dshow).Audio;
        var plan = AudioCaptureArguments.Build(
            new AudioCaptureSelection(liste[0], liste[1]), liste, firstInputIndex: 1);

        SesKanit.Write("k5-amix.txt",
            $"girdi: {string.Join(" ", plan.Inputs)}\n" +
            $"filtre: {plan.FilterComplex}\n" +
            $"eslem: {string.Join(" ", plan.Maps)}\n" +
            $"girdi sayisi: {plan.InputCount}\n");

        Assert.Equal(2, plan.InputCount);
        Assert.Equal(8, plan.Inputs.Count);
        Assert.Equal(
            "[1:a][2:a]amix=inputs=2:duration=longest:dropout_transition=0[aout]",
            plan.FilterComplex);
        Assert.Equal(new[] { "[aout]" }, plan.Maps);
        Assert.Equal("audio=Microphone (Realtek High Definition Audio)", plan.Inputs[3]);
        Assert.Equal("audio=Stereo Mix (Realtek High Definition Audio)", plan.Inputs[7]);
    }

    [Fact]
    public void GirdiSirasiVideodanSonraKaydirilir()
    {
        var liste = CaptureDevices.ParseDirectShow(CihazCiktilari.Ffmpeg6Dshow).Audio;

        var tek = AudioCaptureArguments.Build(new AudioCaptureSelection(liste[0]), liste, firstInputIndex: 3);
        var cift = AudioCaptureArguments.Build(new AudioCaptureSelection(liste[0], liste[1]), liste, firstInputIndex: 3);

        Assert.Equal(new[] { "3:a" }, tek.Maps);
        Assert.StartsWith("[3:a][4:a]amix=", cift.FilterComplex);
    }

    [Fact]
    public void SessizSecimSifirGirdiUretir()
    {
        var liste = CaptureDevices.ParseDirectShow(CihazCiktilari.Ffmpeg6Dshow).Audio;
        var plan = AudioCaptureArguments.Build(new AudioCaptureSelection(), liste, firstInputIndex: 1);

        Assert.Equal(0, plan.InputCount);
        Assert.Empty(plan.Inputs);
        Assert.Empty(plan.Maps);
        Assert.Null(plan.FilterComplex);
    }

    [Fact]
    public void BilinmeyenCihazAdiSessizceYutulmaz()
    {
        var liste = CaptureDevices.ParseDirectShow(CihazCiktilari.Ffmpeg9Dshow).Audio;
        var uydurma = new AudioCaptureDevice(
            "Uydurma Mikrofon 9000", CaptureBackend.DirectShow, AudioSourceRole.Microphone);

        var atilan = Assert.Throws<UnknownCaptureDeviceException>(() =>
            AudioCaptureArguments.Build(new AudioCaptureSelection(uydurma), liste, 1));

        var kabul = AudioCaptureArguments.TryBuild(
            new AudioCaptureSelection(uydurma), liste, 1, out var plan, out var sebep);

        SesKanit.Write("k6-negatif-kontrol.txt",
            $"listelenen cihaz: {liste.Count}\n" +
            $"uydurma ad: {uydurma.Name}\n" +
            $"atilan: {atilan.GetType().Name}: {atilan.Message}\n" +
            $"TryBuild kabul: {kabul.ToString(CultureInfo.InvariantCulture)}\n" +
            $"sebep: {sebep}\n" +
            $"plan girdi sayisi: {plan.InputCount}\n");

        Assert.Equal("Uydurma Mikrofon 9000", atilan.DeviceName);
        Assert.Contains("Uydurma Mikrofon 9000", atilan.Message);
        Assert.False(kabul);
        Assert.NotNull(sebep);
        Assert.Equal(0, plan.InputCount);
        Assert.Empty(plan.Inputs);
    }

    [Fact]
    public void RolUymayanSecimArgumanUretmez()
    {
        var liste = CaptureDevices.ParseDirectShow(CihazCiktilari.Ffmpeg6Dshow).Audio;
        var mikrofon = liste[0];
        Assert.Equal(AudioSourceRole.Microphone, mikrofon.Role);

        var atilan = Assert.Throws<UnknownCaptureDeviceException>(() =>
            AudioCaptureArguments.Build(new AudioCaptureSelection(SystemAudio: mikrofon), liste, 1));

        Assert.Contains("Microphone", atilan.Message, StringComparison.Ordinal);
        Assert.Contains("SystemAudio", atilan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DshowDegerindeIkiNoktaVeTersBoluKacirilir()
    {
        Assert.Equal("a\\\\b", AudioCaptureArguments.EscapeDirectShow("a\\b"));
        Assert.Equal("Mic\\: 2", AudioCaptureArguments.EscapeDirectShow("Mic: 2"));
        Assert.Equal("Mikrofon (Arctis Nova Pro Wireless)",
            AudioCaptureArguments.EscapeDirectShow("Mikrofon (Arctis Nova Pro Wireless)"));
    }
}

public sealed class SesGirisiCanliTests
{
    private static string Klasor
    {
        get
        {
            var path = Path.Combine(SesKanit.Folder, "gecici");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    [Fact]
    public void CanliCihazListesiKilitlenmedenOkunur()
    {
        if (!ToolLocator.IsAvailable(out var missing))
        {
            SesKanit.Write("k7-canli-liste.txt", $"{missing} yok, liste okunmadi.\n");
            Assert.Fail($"{missing} bulunamadi; cihaz listesi olcusu ffmpeg olmadan kurulamaz.");
        }

        CaptureDevices.Invalidate();
        var saat = Stopwatch.StartNew();
        var liste = CaptureDevices.Instance;
        saat.Stop();

        var rapor = new StringBuilder();
        rapor.AppendLine($"ffmpeg: {ToolLocator.GetFfmpegVersion()}");
        rapor.AppendLine($"katman: {liste.Backend}");
        rapor.AppendLine($"okundu (Loaded): {liste.Loaded}");
        rapor.AppendLine($"sure: {saat.ElapsedMilliseconds} ms (oldurme siniri {CaptureDevices.CaptureKillMs} ms)");
        rapor.AppendLine($"video cihaz: {liste.Video.Count}");
        foreach (var v in liste.Video) rapor.AppendLine($"  {v}");
        rapor.AppendLine($"ses cihaz: {liste.Audio.Count}");
        foreach (var a in liste.Audio) rapor.AppendLine($"  {a.Name} [{a.Role}] -> {a.Reference}");
        SesKanit.Write("k7-canli-liste.txt", rapor.ToString());

        Assert.True(liste.Loaded, "cihaz listeleyen surec donmedi");
        Assert.True(saat.ElapsedMilliseconds < CaptureDevices.CaptureKillMs,
            $"liste {saat.ElapsedMilliseconds} ms surdu, sinir {CaptureDevices.CaptureKillMs} ms");
        Assert.Equal(CaptureDevices.Backend, liste.Backend);
    }

    /// <summary>
    /// Uretilen argumanin ffmpeg'de gercekten calistigini ve <b>uydurma cihaz adinin
    /// ffmpeg tarafindan da yutulmadigini</b> olcer. Kayit ffprobe'a tek ses akisi
    /// gosterir; sesli kayitta iki akis (video + ses) olcumu 8a'nin motoruna bagli
    /// oldugu icin bu kolda arguman duzeyinde kalir.
    /// </summary>
    [SesCihaziFact]
    public void UretilenArgumanFfmpegdeCalisir_UydurmaAdReddedilir()
    {
        var liste = CaptureDevices.Instance.Audio;
        var mikrofon = liste.First(d => d.Role == AudioSourceRole.Microphone);
        var plan = AudioCaptureArguments.Build(new AudioCaptureSelection(mikrofon), liste, firstInputIndex: 0);

        var cikti = Path.Combine(Klasor, "mikrofon-1sn.wav");
        if (File.Exists(cikti)) File.Delete(cikti);

        var args = new List<string> { "-hide_banner", "-y" };
        args.AddRange(plan.Inputs);
        args.AddRange(new[] { "-map" });
        args.AddRange(plan.Maps);
        args.AddRange(new[] { "-t", "1", "-c:a", "pcm_s16le", cikti });
        var kayit = Kostur(ToolLocator.Ffmpeg, args);

        var uydurma = new List<string> { "-hide_banner", "-y" };
        uydurma.AddRange(AudioCaptureArguments.InputArguments(
            mikrofon with { Name = "Uydurma Mikrofon 9000", Alternative = null }));
        uydurma.AddRange(new[] { "-map", "0:a", "-t", "1", "-c:a", "pcm_s16le", Path.Combine(Klasor, "uydurma.wav") });
        var red = Kostur(ToolLocator.Ffmpeg, uydurma);

        var akislar = Kostur(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-show_entries", "stream=codec_type",
            "-of", "csv=p=0", cikti
        });
        var sesAkisi = akislar.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(l => l.Trim() == "audio");

        var rapor = new StringBuilder();
        rapor.AppendLine($"cihaz: {mikrofon.Name} [{mikrofon.Role}]");
        rapor.AppendLine($"kayit argumani: {string.Join(" ", args)}");
        rapor.AppendLine($"kayit cikis kodu: {kayit.ExitCode}");
        rapor.AppendLine($"dosya boyutu: {(File.Exists(cikti) ? new FileInfo(cikti).Length : 0)} bayt");
        rapor.AppendLine($"ffprobe ses akisi: {sesAkisi}");
        rapor.AppendLine($"uydurma ad argumani: {string.Join(" ", uydurma)}");
        rapor.AppendLine($"uydurma ad cikis kodu: {red.ExitCode}");
        rapor.AppendLine("uydurma ad stderr kuyrugu:");
        foreach (var satir in red.Error.Split('\n').TakeLast(6)) rapor.AppendLine($"  {satir.TrimEnd()}");
        SesKanit.Write("k8-canli-yakalama.txt", rapor.ToString());

        Assert.Equal(0, kayit.ExitCode);
        Assert.True(File.Exists(cikti) && new FileInfo(cikti).Length > 1000,
            "kayit dosyasi olusmadi ya da bos");
        Assert.Equal(1, sesAkisi);
        Assert.NotEqual(0, red.ExitCode);
    }

    private static (int ExitCode, string Output, string Error) Kostur(string tool, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = tool,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException($"{tool} 30 sn icinde donmedi.");
        }
        Task.WaitAll(new Task[] { output, error }, 2000);
        return (process.ExitCode, output.Result, error.Result);
    }
}
