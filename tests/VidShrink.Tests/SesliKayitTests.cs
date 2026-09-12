using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Canli sesli kayit olcusu. ffmpeg yoksa, makine Windows degilse ya da bu makinede
/// listelenmis bir mikrofon yoksa atlanir. Cihaz yoklugu mesru bir cevap: arguman
/// duzeyindeki olculer cihazsiz makinede de kosuyor, yalniz gercek kayit atlaniyor.
/// </summary>
public sealed class SesliKayitFactAttribute : FactAttribute
{
    public SesliKayitFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
            Skip = $"{missing} bulunamadi, sesli kayit olcusu kosturulmadi.";
        else if (!OperatingSystem.IsWindows())
            Skip = "sesli kayit olcusu gdigrab ve dshow ile alindi; bu makine Windows degil.";
        else if (!CaptureDevices.Instance.Audio.Any(d => d.Role == AudioSourceRole.Microphone))
            Skip = "bu makinede listelenmis mikrofon yok, sesli kayit olcusu kosturulmadi.";
    }
}

/// <summary>
/// 8d kolu: ses girdisinin motora baglanmasi. Plani <see cref="AudioCaptureArguments"/>
/// uretiyor, argumana <see cref="RecorderArguments"/> yaziyor; olculen sey ikisinin
/// birlesimi.
///
/// <para>Bu kol acilmadan once <c>AudioCapturePlan</c>in yalniz <c>Inputs</c> parcasi
/// argumana giriyordu: <c>amix</c> grafigi ve <c>-map</c> satirlari hic yazilmadigi icin
/// iki cihaz secilse bile ffmpeg varsayilan eslemesine dusup tek akis aliyordu. Asagidaki
/// olculer tam olarak o dusmeyi tutuyor.</para>
/// </summary>
public sealed class SesliKayitTests
{
    private static string Klasor
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "dalga8d");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    private static readonly AudioCaptureDevice Mikrofon =
        new("Mikrofon Dizisi", CaptureBackend.DirectShow, AudioSourceRole.Microphone);

    private static readonly AudioCaptureDevice SistemSesi =
        new("Stereo Mix", CaptureBackend.DirectShow, AudioSourceRole.SystemAudio);

    private static readonly AudioCaptureDevice[] Liste = { Mikrofon, SistemSesi };

    private static readonly AudioCaptureDevice MacMikrofon =
        new("MacBook Mikrofonu", CaptureBackend.AvFoundation, AudioSourceRole.Microphone, Address: "0");

    private static readonly AudioCaptureDevice MacSistemSesi =
        new("Loopback Audio", CaptureBackend.AvFoundation, AudioSourceRole.SystemAudio, Address: "1");

    private static readonly AudioCaptureDevice[] MacListe = { MacMikrofon, MacSistemSesi };

    private static RecorderRequest Ekran(AudioCapturePlan? ses = null) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Screen,
        Audio = ses
    };

    private static string Arg(IReadOnlyList<string> args) => string.Join(" ", args);

    private static AudioCapturePlan Plan(
        AudioCaptureSelection secim,
        IReadOnlyCollection<AudioCaptureDevice>? liste = null)
        => AudioCaptureArguments.Build(secim, liste ?? Liste, RecorderArguments.AudioFirstInputIndex);

    /// <summary>
    /// Iki cihaz secilince argumanda grafik <b>ve</b> eslem duruyor. Eslemin ikinci yarisi
    /// kritik: <c>-map 0:v</c> yazilmazsa karmasik grafigin oldugu bir cagirimda ffmpeg
    /// video akisini kendisi eslemiyor.
    /// </summary>
    [Fact]
    public void IkiCihazSecilinceGrafikVeEslemArgumandaDurur()
    {
        var args = RecorderArguments.Build(
            Ekran(Plan(new AudioCaptureSelection(Mikrofon, SistemSesi))), @"C:\kayit\s.mp4");
        var metin = Arg(args);

        Assert.Contains("-filter_complex", args);
        Assert.Contains("amix=inputs=2", metin);
        Assert.Contains("-map 0:v", metin);
        Assert.Contains($"-map [{AudioCaptureArguments.MixOutputLabel}]", metin);
        Assert.Contains("-c:a aac", metin);
        Assert.DoesNotContain("-an", args);
        Assert.True(
            metin.IndexOf("-i desktop", StringComparison.Ordinal) < metin.IndexOf("audio=Mikrofon", StringComparison.Ordinal),
            "ses girdisi yakalama girdisinden sonra gelmeli");
    }

    /// <summary>Tek cihazda karistirma yok: filtre kurulmuyor, akis dogrudan esleniyor.</summary>
    [Fact]
    public void TekCihazdaFiltreKurulmaz()
    {
        var args = RecorderArguments.Build(
            Ekran(Plan(new AudioCaptureSelection(Mikrofon))), @"C:\kayit\t.mp4");

        Assert.DoesNotContain("-filter_complex", args);
        Assert.Contains("-map 0:v -map 1:a", Arg(args));
    }

    /// <summary>
    /// Sessiz kayitta <c>-an</c> duruyor ve <c>-map</c> <b>hic</b> yazilmiyor. Iki yol da
    /// olculuyor: ses kolu hic verilmemis istek ve <see cref="AudioCapturePlan.Silent"/>.
    /// </summary>
    [Fact]
    public void SessizKayitEslemYazmaz()
    {
        var bos = RecorderArguments.Build(Ekran(), @"C:\kayit\b.mp4");
        var sessiz = RecorderArguments.Build(Ekran(AudioCapturePlan.Silent), @"C:\kayit\c.mp4");

        Assert.Contains("-an", bos);
        Assert.Contains("-an", sessiz);
        Assert.DoesNotContain("-map", bos);
        Assert.DoesNotContain("-map", sessiz);
        Assert.DoesNotContain("-filter_complex", sessiz);
    }

    /// <summary>
    /// Ses girdilerinin numarasi yakalama girdisinden sonra basliyor. Sabit karsilastirmak
    /// davranis olcmezdi; olculen sey grafigin hangi girdiye baktigi: <c>[0:a]</c> ciksa
    /// grafik video girdisinin olmayan ses akisina bakiyor olurdu.
    /// </summary>
    [Fact]
    public void SesGrafigiVideoGirdisineBakmaz()
    {
        var metin = Arg(RecorderArguments.Build(
            Ekran(Plan(new AudioCaptureSelection(Mikrofon, SistemSesi))), @"C:\kayit\g.mp4"));

        Assert.Contains("[1:a][2:a]amix", metin);
        Assert.DoesNotContain("[0:a]", metin);
    }

    /// <summary>
    /// Bolge kirpmasi ses grafigiyle birlikte durabiliyor: <c>-vf</c> video akisina,
    /// <c>-filter_complex</c> ses akisina bakiyor. macOS'ta bolge girdi secenegi degil
    /// kirpma filtresi oldugu icin ikisi ayni cagirimda bulusuyor.
    /// </summary>
    [Fact]
    public void BolgeKirpmasiSesGrafigiyleBirlikteDurur()
    {
        var istek = Ekran(Plan(new AudioCaptureSelection(MacMikrofon, MacSistemSesi), MacListe)) with
        {
            Platform = RecorderPlatform.MacOs,
            Target = RecorderTargetKind.Region,
            Region = new RecorderRegion(10, 30, 320, 240),
            Container = RecorderContainer.Mov
        };

        var metin = Arg(RecorderArguments.Build(istek, "/tmp/k.mov"));

        Assert.Contains("-vf crop=320:240:10:30", metin);
        Assert.Contains("amix=inputs=2", metin);
        Assert.Contains("-map 0:v", metin);
    }

    /// <summary>
    /// Negatif kontrol: listede olmayan cihaz argumana giremiyor, dolayisiyla istek de
    /// kurulamiyor. Sessiz kayda dusmek hatayi yutmak olurdu.
    /// </summary>
    [Fact]
    public void ListedeOlmayanCihazIstegeGiremez()
    {
        var uydurma = new AudioCaptureDevice("Uydurma Mikrofon", CaptureBackend.DirectShow, AudioSourceRole.Microphone);

        var hata = Assert.Throws<UnknownCaptureDeviceException>(
            () => Plan(new AudioCaptureSelection(uydurma)));

        Assert.Equal("Uydurma Mikrofon", hata.DeviceName);
        Assert.False(
            AudioCaptureArguments.TryBuild(
                new AudioCaptureSelection(uydurma), Liste, RecorderArguments.AudioFirstInputIndex,
                out var plan, out var sebep));
        Assert.Same(AudioCapturePlan.Silent, plan);
        Assert.NotNull(sebep);
    }

    /// <summary>
    /// 8c'nin kapanmamis kabul olcutu: sesli kayitta <c>ffprobe</c> iki akis goruyor. Olcu
    /// gercek bir mikrofonla 4 sn kaydediyor, ham ffprobe ciktisi
    /// <c>.calisma/dalga8d/</c>ye yaziliyor.
    /// </summary>
    [SesliKayitFact]
    public async Task SesliKayitIkiAkisUretir()
    {
        var listelenen = CaptureDevices.Instance.Audio;
        var cihaz = listelenen.First(d => d.Role == AudioSourceRole.Microphone);

        var istek = new RecorderRequest
        {
            Platform = RecorderPlatform.Windows,
            Target = RecorderTargetKind.Region,
            Fps = 15,
            Region = new RecorderRegion(0, 0, 640, 480),
            Audio = AudioCaptureArguments.Build(
                new AudioCaptureSelection(cihaz), listelenen, RecorderArguments.AudioFirstInputIndex)
        };

        var cikti = Path.Combine(Klasor, "sesli.mp4");
        File.WriteAllLines(
            Path.Combine(Klasor, "sesli.args.txt"), RecorderArguments.Build(istek, cikti));

        var oturum = await RecorderSession.StartAsync(istek, cikti);
        await Task.Delay(4000);
        var sonuc = await oturum.StopAsync();

        var (kod, metin) = Ffprobe(cikti, "sesli.ffprobe.txt");
        var akislar = metin
            .Split('\n')
            .Count(line => line.Trim().StartsWith("codec_type=", StringComparison.Ordinal));

        Assert.True(sonuc.Ok, $"sesli kayit 0 ile kapanmali; stderr: {sonuc.StandardError}");
        Assert.False(sonuc.Partial, "nazik durdurma yarim dosya birakmaz");
        Assert.Equal(0, kod);
        Assert.Contains("codec_type=video", metin);
        Assert.Contains("codec_type=audio", metin);
        Assert.Equal(2, akislar);
    }

    private static (int ExitCode, string Text) Ffprobe(string file, string evidenceName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffprobe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "-hide_banner", "-v", "error",
            "-show_entries", "format=duration",
            "-show_entries", "stream=codec_type,codec_name",
            "-of", "default=nw=1", file
        }) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(20_000);
        File.WriteAllText(
            Path.Combine(Klasor, evidenceName),
            $"# {file}{Environment.NewLine}# cikis kodu {process.ExitCode}{Environment.NewLine}{text}");
        return (process.ExitCode, text);
    }
}
