using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Paket 2b T13, boşluk kırpma: <c>freezedetect</c> çıktısının ayrıştırılması, her donuk aralığın
/// <see cref="IdleTrim.DefaultKeepSeconds"/>'e kısaltılması, <c>trim/atrim + concat</c> argümanı ve
/// sonuç panelindeki "Boşlukları kırp" düğmesi. Canlı kol 6 sn'lik lavfi kaydında (2 sn hareket,
/// 3 sn donuk, 1 sn hareket, sesli) çıktının süresini ffprobe'la okur; donuksuz kayıt negatif kontrol.
/// Kanıt <c>.calisma/paket-2b/kirpma/</c>.
/// </summary>
public sealed class BoslukKirpmaTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "paket-2b", "kirpma");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    /// <summary>Son asertten sonra çağrılır; kuralı <see cref="KanitKapanisi"/> anlatıyor.</summary>
    private static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Kanit, adlar);

    private static void Onceki(params string[] adlar) => KanitKapanisi.Onceki(Kanit, adlar);

    private const string OrnekCikti = """
        [freezedetect @ 000001] lavfi.freezedetect.freeze_start: 2.1
        [freezedetect @ 000001] lavfi.freezedetect.freeze_duration: 2.9
        [freezedetect @ 000001] lavfi.freezedetect.freeze_end: 5
        [silencedetect @ 000002] silence_start: 1.5
        frame=  60 fps=0.0 q=-0.0 size=N/A time=00:00:06.00
        [freezedetect @ 000001] lavfi.freezedetect.freeze_start: 7.25
        """;

    [Fact]
    public void DonukAraliklarAyrisirSondaAcikKalanSureyeKapanir()
    {
        var araliklar = IdleTrim.ParseFreezes(OrnekCikti, 9.5);

        Assert.Equal(new[] { new IdleSpan(2.1, 5), new IdleSpan(7.25, 9.5) }, araliklar);
        Assert.Empty(IdleTrim.ParseFreezes("[silencedetect @ 1] silence_start: 1.5", 9.5));
        Assert.Empty(IdleTrim.ParseFreezes("lavfi.freezedetect.freeze_end: 4", 9.5));
    }

    [Fact]
    public void HerDonukAralikSaklanacakPayaKisalir()
    {
        var tutulan = IdleTrim.KeptRanges(new[] { new IdleSpan(2, 5), new IdleSpan(7, 7.3), new IdleSpan(8, 10) }, 10, 0.5);

        Assert.Equal(new[] { new IdleSpan(0, 2.5), new IdleSpan(5, 8.5) }, tutulan);
        Assert.Equal(4, IdleTrim.RemovedSeconds(tutulan, 10), 3);

        var payBuyuk = IdleTrim.KeptRanges(new[] { new IdleSpan(2, 5) }, 10, 5);
        Assert.Equal(new[] { new IdleSpan(0, 10) }, payBuyuk);
        Assert.Equal(0, IdleTrim.RemovedSeconds(payBuyuk, 10), 3);
    }

    [Fact]
    public void KirpmaArgumaniParcalariBirlestirirSessizdeSesDaliKurmaz()
    {
        var tutulan = new[] { new IdleSpan(0, 2.5), new IdleSpan(5, 6) };
        var sesli = IdleTrim.BuildTrim("a.mkv", "a-trimmed.mkv", tutulan, true);
        var sessiz = IdleTrim.BuildTrim("a.mkv", "a-trimmed.mp4", tutulan, false);

        Assert.Equal(
            "[0:v]trim=start=0:end=2.5,setpts=PTS-STARTPTS[v0];[0:a]atrim=start=0:end=2.5,asetpts=PTS-STARTPTS[a0];"
            + "[0:v]trim=start=5:end=6,setpts=PTS-STARTPTS[v1];[0:a]atrim=start=5:end=6,asetpts=PTS-STARTPTS[a1];"
            + "[v0][a0][v1][a1]concat=n=2:v=1:a=1[vout][aout]",
            sesli[sesli.ToList().IndexOf("-filter_complex") + 1]);
        Assert.Contains("[aout]", sesli);
        Assert.DoesNotContain("-movflags", sesli);

        var graf = sessiz[sessiz.ToList().IndexOf("-filter_complex") + 1];
        Assert.DoesNotContain("atrim", graf);
        Assert.EndsWith("concat=n=2:v=1:a=0[vout]", graf);
        Assert.DoesNotContain("[aout]", sessiz);
        Assert.Contains("+faststart", sessiz);
        Assert.Throws<ArgumentException>(() => IdleTrim.BuildTrim("a.mkv", "b.mkv", Array.Empty<IdleSpan>(), true));
    }

    [Fact]
    public void HedefAdiKaynagiEzmez()
    {
        var kaynak = Path.Combine("k", "kayit.mkv");
        Assert.Equal(Path.Combine("k", "kayit-trimmed.mkv"), IdleTrim.TrimTarget(kaynak, _ => false));
        Assert.Equal(Path.Combine("k", "kayit-trimmed_2.mkv"), IdleTrim.TrimTarget(kaynak, p => p.EndsWith("kayit-trimmed.mkv", StringComparison.Ordinal)));
        Assert.Equal(Path.Combine("k", "kayit-trimmed.mkv"), IdleTrim.TrimTarget(Path.Combine("k", "kayit.gif"), _ => false));
    }

    private static void Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(30000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 30 sn icinde bitmedi.");
        }

        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
    }

    private static double Sure(string dosya)
    {
        var psi = new ProcessStartInfo("ffprobe", $"-v error -show_entries format=duration -of csv=p=0 \"{dosya}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var surec = Process.Start(psi)!;
        var metin = surec.StandardOutput.ReadToEnd();
        Assert.True(surec.WaitForExit(15000));
        return double.Parse(metin.Trim(), CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task CanliKayittaDonukAralikKisalirDonuksuzKayitDokunulmaz()
    {
        Onceki("donuklu.mkv", "donuksuz.mkv");
        foreach (var eski in Directory.GetFiles(Kanit, "donu*-trimmed*.mkv")) File.Delete(eski);

        var donuklu = Path.Combine(Kanit, "donuklu.mkv");
        Ffmpeg("-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=160x120:r=10:d=2",
            "-f", "lavfi", "-i", "testsrc2=s=160x120:r=10:d=1",
            "-f", "lavfi", "-i", "sine=f=440:d=6",
            "-filter_complex", "[0:v]tpad=stop_mode=clone:stop_duration=3[a];[a][1:v]concat=n=2:v=1:a=0[v]",
            "-map", "[v]", "-map", "2:a", "-t", "6",
            "-c:v", "libx264", "-preset", "ultrafast", "-threads", "1", "-c:a", "aac", donuklu);

        var donuksuz = Path.Combine(Kanit, "donuksuz.mkv");
        Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=s=160x120:r=10:d=4",
            "-c:v", "libx264", "-preset", "ultrafast", "-threads", "1", donuksuz);

        var sonuc = await IdleTrimmer.RunAsync(donuklu, 1, 0.5);
        var bos = await IdleTrimmer.RunAsync(donuksuz, 1, 0.5);
        var yeniSure = sonuc.Target is null ? double.NaN : Sure(sonuc.Target);

        File.WriteAllLines(Path.Combine(Kanit, "olcu.txt"), new[]
        {
            $"donuklu kaynak sure={Sure(donuklu).ToString(CultureInfo.InvariantCulture)} sonuc={sonuc}",
            $"kirpilmis sure={yeniSure.ToString(CultureInfo.InvariantCulture)}",
            $"donuksuz sonuc={bos}"
        });

        Assert.True(sonuc.Ok, sonuc.Error);
        Assert.NotNull(sonuc.Target);
        Assert.InRange(sonuc.RemovedSeconds, 2.0, 2.8);
        Assert.InRange(yeniSure, 3.4, 4.1);
        Assert.True(bos.Ok, bos.Error);
        Assert.Null(bos.Target);
        Assert.Single(Directory.GetFiles(Kanit, "donu*-trimmed*.mkv"));

        Kapat("olcu.txt", "donuklu.mkv", "donuksuz.mkv", Path.GetFileName(sonuc.Target!));
    }

    [Fact]
    public void SonucPanelindekiDugmeSonucuKendiSatirindaSoyler()
    {
        var dosya = Path.Combine(Kanit, "panel.mkv");
        File.WriteAllBytes(dosya, new byte[] { 1 });
        var gif = Path.Combine(Kanit, "panel.gif");
        File.WriteAllBytes(gif, new byte[] { 1 });

        var olcu = AppHost.Run(() =>
        {
            var view = new RecorderView { RevealFolder = _ => { } };
            view.ShowResult(new RecordResult(true, gif, 1, false, 0, string.Empty, 1));
            var gifte = view.TrimIdleVisible;
            view.ShowResult(new RecordResult(true, dosya, 1, true, 0, string.Empty, 1));
            var yarimda = view.TrimIdleVisible;
            view.ShowResult(new RecordResult(true, dosya, 1, false, 0, string.Empty, 1));
            var tamda = view.TrimIdleVisible;

            string? verilen = null;
            var bitti = view.TrimIdleAsync(p => { verilen = p; return Task.FromResult(new IdleTrimResult(true, "x-trimmed.mkv", 12.5, string.Empty)); });
            var kirpildi = view.NoticeText;
            _ = view.TrimIdleAsync(_ => Task.FromResult(new IdleTrimResult(true, null, 0, string.Empty)));
            var yok = view.NoticeText;
            _ = view.TrimIdleAsync(_ => Task.FromResult(new IdleTrimResult(false, null, 0, "bozuk")));
            var hataGorunur = view.NoticeText.Length == 0;
            _ = view.TrimIdleAsync(_ => throw new InvalidOperationException("atti"));
            return (gifte, yarimda, tamda, verilen, bitti.IsCompleted, kirpildi, yok, hataGorunur);
        });

        Assert.False(olcu.gifte);
        Assert.False(olcu.yarimda);
        Assert.True(olcu.tamda);
        Assert.Equal(dosya, olcu.verilen);
        Assert.True(olcu.IsCompleted);
        Assert.Contains("x-trimmed.mkv", olcu.kirpildi);
        Assert.DoesNotContain("x-trimmed.mkv", olcu.yok);
        Assert.NotEmpty(olcu.yok);
        Assert.True(olcu.hataGorunur);

        Kapat("panel.mkv", "panel.gif");
    }
}
