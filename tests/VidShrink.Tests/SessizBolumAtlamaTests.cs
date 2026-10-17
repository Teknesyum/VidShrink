using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// OYA-37: sessiz bolumleri hizlandirma. Ortasinda 1,6 sn sessizlik olan 3 sn'lik uretilmis tonda
/// libmpv <c>silencedetect</c> halkasiyla oynatma suresi kisalir, sesli kisimda hiz 1'de kalir ve
/// sessizlik bitince eski hiza doner. <c>ao=null</c> gercek zamanli calar, hoparlore ses gitmez.
/// </summary>
public sealed class SessizBolumAtlamaTests
{
    private static readonly string Kanit = Path.Combine(GirdiKanit.Root, ".calisma", "yan-sessiz-atla");

    [Fact]
    public void AyarSinirlariniKirpar()
    {
        var kirpik = new SilenceSkip(true, -200, 99).Clamped();
        Assert.Equal(SilenceSkip.MinimumThresholdDb, kirpik.ThresholdDb);
        Assert.Equal(SilenceSkip.LongestSeconds, kirpik.MinimumSeconds);
        var bozuk = new SilenceSkip(true, double.NaN, double.PositiveInfinity).Clamped();
        Assert.Equal(SilenceSkip.DefaultThresholdDb, bozuk.ThresholdDb);
        Assert.Equal(SilenceSkip.DefaultMinimumSeconds, bozuk.MinimumSeconds);
        Assert.False(SilenceSkip.Off.On);
    }

    [Fact]
    public void AyarDosyasindaGidipGelir()
    {
        var klasor = Path.Combine(Kanit, "ayar");
        Directory.CreateDirectory(klasor);
        try
        {
            var dosya = Path.Combine(klasor, PlayerSettings.FileName);
            Assert.Equal(SilenceSkip.Off, PlayerSettings.Load(dosya).Silence);
            new PlayerSettings { SilenceSkip = true, SilenceThresholdDb = -45, SilenceMinimumSeconds = 1.5 }.Save(dosya);
            Assert.Equal(new SilenceSkip(true, -45, 1.5), PlayerSettings.Load(dosya).Silence);

            File.WriteAllText(dosya, "{\"silenceSkip\": true, \"silenceThresholdDb\": 5, \"silenceMinimumSeconds\": 0}");
            var kirpik = PlayerSettings.Load(dosya);
            Assert.Equal(new SilenceSkip(true, SilenceSkip.MaximumThresholdDb, SilenceSkip.ShortestSeconds), kirpik.Silence);
            Assert.Equal(SilenceSkip.MaximumThresholdDb, kirpik.SilenceThresholdDb);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task SessizlikteHizlanirSesteHizDegismez()
    {
        var klasor = Path.Combine(Kanit, "motor");
        Directory.CreateDirectory(klasor);
        try
        {
            var ton = Path.Combine(klasor, "ton.wav");
            Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i",
                "sine=frequency=440:sample_rate=48000:duration=3,volume='if(between(t,0.8,2.4),0,0.5)':eval=frame",
                "-threads", "2", ton);

            var duz = await Oynat(ton, false);
            var hizli = await Oynat(ton, true);
            var kayit = $"duz {duz.Sure:0.00} sn, hizli {hizli.Sure:0.00} sn; ornekler: {string.Join(" ", hizli.Ornekler.Select(o => $"{o.Konum:0.00}@{o.Hiz:0.##}"))}; ham: {string.Join(" | ", hizli.Ham.Distinct())}";
            File.WriteAllText(Path.Combine(Kanit, "motor-olcum.txt"), kayit);

            Assert.True(duz.Ornekler.All(o => o.Hiz == 1), kayit);
            Assert.True(hizli.Sure < duz.Sure - 0.5, kayit);
            Assert.Contains(hizli.Ornekler, o => o.Hiz == SilenceSkip.RushSpeed);
            var ilk = hizli.Ornekler.FindIndex(o => o.Hiz != 1);
            Assert.True(hizli.Ornekler[ilk].Konum >= 0.79, kayit);
            Assert.True(hizli.Ornekler.Take(ilk).Count(o => o.Konum < 0.7) > 10, kayit);
            Assert.True(hizli.Ornekler.TakeLast(5).All(o => o.Hiz == 1), kayit);
            Assert.True(hizli.Bildirilen.All(h => h == 1), kayit);
            Assert.Equal(1, hizli.Son);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    private sealed record Ornek(double Konum, double Hiz);

    private sealed record Kosum(double Sure, List<Ornek> Ornekler, List<double> Bildirilen, double Son, List<string> Ham);

    private static async Task<Kosum> Oynat(string dosya, bool acik)
    {
        using var motor = new MpvEngine();
        motor.SetProperty("ao", "null");
        await motor.OpenAsync(dosya);
        motor.SetSilenceSkip(new SilenceSkip(acik, -40, 0.3));
        Assert.Equal(acik, (motor.GetProperty("af") ?? "").Contains("vssilence", StringComparison.Ordinal));
        var ornekler = new List<Ornek>();
        var bildirilen = new List<double>();
        var ham = new List<string>();
        var saat = Stopwatch.StartNew();
        motor.Play();
        while (!motor.EndReached && saat.Elapsed < TimeSpan.FromSeconds(8))
        {
            await Task.Delay(20);
            var hiz = double.Parse(motor.GetProperty("speed") ?? "NaN", NumberStyles.Float, CultureInfo.InvariantCulture);
            ornekler.Add(new Ornek(motor.PositionSeconds, hiz));
            bildirilen.Add(motor.Speed);
            ham.Add(motor.GetProperty("af-metadata/vssilence") ?? "-");
        }

        var sure = saat.Elapsed.TotalSeconds;
        Assert.True(motor.EndReached, "8 sn icinde bitmedi");
        var son = double.Parse(motor.GetProperty("speed") ?? "NaN", NumberStyles.Float, CultureInfo.InvariantCulture);
        return new Kosum(sure, ornekler, bildirilen, son, ham);
    }

    private static void Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var standart = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(60_000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 60 sn icinde bitmedi");
        }

        surec.WaitForExit();
        Assert.True(surec.ExitCode == 0, hata.Result);
    }
}
