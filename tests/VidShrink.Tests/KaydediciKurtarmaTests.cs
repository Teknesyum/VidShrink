using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// EKA-P1 (later 63): kaydedicide dusuk disk uyarisi ve yarim kalan kaydin kurtarilmasi.
/// Disk sahte bos alan kapisiyla, klasor <c>.calisma/</c> altindaki gercek ama kucuk bir
/// klasorle olculur; gercek ekran ya da ses yakalanmaz. Tek canli kol <see cref="FfmpegFactAttribute"/>
/// ile 1 sn'lik <c>testsrc</c> Matroska'sini <c>-threads 2</c> ile uretip MP4'e aktarir.
/// </summary>
public sealed class KaydediciKurtarmaTests
{
    private const long Mb = 1_000_000L;

    private static string CalismaKlasoru()
    {
        var kok = new DirectoryInfo(AppContext.BaseDirectory);
        while (kok is not null && !File.Exists(Path.Combine(kok.FullName, "VidShrink.sln"))) kok = kok.Parent;
        var klasor = Path.Combine(kok!.FullName, ".calisma", "kayit-kurtarma-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    [Theory]
    [InlineData(999, true)]
    [InlineData(1000, false)]
    [InlineData(5000, false)]
    public void KaydediciEsigiBirGigabaytinAltindaUyarir(long bosMb, bool beklenen)
    {
        Assert.Equal(beklenen, DiskSpaceGuard.IsLowForRecording(bosMb * Mb));
    }

    [Fact]
    public void KayitBaslamadanDusukDiskUyarisiCikar()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var (dusuk, metin, bol, bosMetin) = AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol) { FreeSpace = _ => 300 * Mb };
            var d = view.CheckDisk("C:/kayit");
            var m = view.DiskWarningText;
            view.FreeSpace = _ => 50_000 * Mb;
            var b = view.CheckDisk("C:/kayit");
            return (d, m, b, view.DiskWarningText);
        });

        Assert.True(dusuk);
        Assert.Contains(Bicim.Boyut.Mb(300, VidShrink.App.Localization.Strings.Culture), metin);
        Assert.False(bol);
        Assert.Equal(string.Empty, bosMetin);
    }

    [Fact]
    public void KayitSurerkenDiskAralikliOkunur()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var (okuma, metin) = AppHost.Run(() =>
        {
            var saat = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
            var sayac = 0;
            long bos = 50_000 * Mb;
            var view = new RecorderView(ayar.Yol)
            {
                DiskClock = () => saat,
                FreeSpace = _ => { sayac++; return bos; }
            };
            view.BeginDiskWatch("C:/kayit");
            view.CheckDiskWhileRecording();
            saat += TimeSpan.FromSeconds(3);
            view.CheckDiskWhileRecording();
            bos = 200 * Mb;
            saat += DiskSpaceGuard.RecorderCheckInterval;
            view.CheckDiskWhileRecording();
            return (sayac, view.DiskWarningText);
        });

        Assert.Equal(2, okuma);
        Assert.NotEqual(string.Empty, metin);
    }

    [Fact]
    public void SahipsizYakalamaDosyasiBulunurKilitliOlanAtlanir()
    {
        var klasor = CalismaKlasoru();
        try
        {
            var sahipsiz = Path.Combine(klasor, "Kayit 2026-10-15.yakalama.mkv");
            var kilitli = Path.Combine(klasor, "Kayit 2026-10-16.yakalama.mkv");
            File.WriteAllText(sahipsiz, "x");
            File.WriteAllText(kilitli, "x");
            File.WriteAllText(Path.Combine(klasor, "Kayit.mkv"), "x");
            File.WriteAllText(Path.Combine(klasor, "Kayit.gif-kayit.mkv"), "x");

            IReadOnlyList<string> bulunan;
            using (new FileStream(kilitli, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                bulunan = RecorderSession.FindOrphanCaptures(klasor);

            Assert.Equal(new[] { sahipsiz }, bulunan);
            Assert.Empty(RecorderSession.FindOrphanCaptures(Path.Combine(klasor, "yok")));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Theory]
    [InlineData(RecorderContainer.Mp4, "a.mp4")]
    [InlineData(RecorderContainer.Mov, "a.mov")]
    [InlineData(RecorderContainer.Mkv, "a.mp4")]
    [InlineData(RecorderContainer.Gif, "a.mp4")]
    public void KurtarmaHedefiIstenenBicimdeVeCakismasiz(RecorderContainer kap, string beklenen)
    {
        var kok = Path.GetFullPath("kurtarma-hedefi");
        var kaynak = Path.Combine(kok, "a.yakalama.mkv");
        Assert.Equal(Path.Combine(kok, beklenen), RecorderArguments.RecoveryTarget(kaynak, kap, _ => false));

        var dolu = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(kok, beklenen) };
        var ikinci = RecorderArguments.RecoveryTarget(kaynak, kap, dolu.Contains);
        Assert.Equal(Path.Combine(kok, "a_2" + Path.GetExtension(beklenen)), ikinci);
    }

    [Fact]
    public void KaydediciAcilincaYarimKayitOnerilirVeKurtarilir()
    {
        var klasor = CalismaKlasoru();
        try
        {
            var yakalama = Path.Combine(klasor, "Kayit.yakalama.mkv");
            var bozuk = Path.Combine(klasor, "Bozuk.yakalama.mkv");
            File.WriteAllText(yakalama, "x");
            File.WriteAllText(bozuk, "x");

            using var ayar = new KaydediciAyarTests.OzelAyar();
            var (gorunur, oneri, kurtarilan, sonraGorunur, hata, bildirim, argumanlar) = AppHost.Run(() =>
            {
                var view = new RecorderView(ayar.Yol) { OrphanSource = _ => new[] { yakalama, bozuk } };
                view.Settings.Container = RecorderContainer.Mov;
                view.ScanOrphans(klasor);
                var g = view.RecoverVisible;
                var o = view.RecoverText;
                var yazilan = new List<IReadOnlyList<string>>();
                var k = view.RecoverAsync(a =>
                {
                    yazilan.Add(a);
                    if (a[a.ToList().IndexOf("-i") + 1] != yakalama) return Task.FromResult(false);
                    File.WriteAllText(a[^1], "mov");
                    return Task.FromResult(true);
                }).GetAwaiter().GetResult();
                return (g, o, k, view.RecoverVisible, view.ErrorText, view.NoticeText, yazilan);
            });

            var hedef = Path.Combine(klasor, "Kayit.mov");
            Assert.True(gorunur);
            Assert.Contains("Kayit.yakalama.mkv", oneri);
            Assert.Equal(new[] { hedef }, kurtarilan);
            Assert.True(File.Exists(hedef));
            Assert.False(File.Exists(yakalama));
            Assert.True(File.Exists(bozuk));
            Assert.Contains(hedef, bildirim);
            Assert.Contains("Bozuk.yakalama.mkv", hata);
            Assert.True(sonraGorunur);
            Assert.Equal(2, argumanlar.Count);
            Assert.Contains("copy", argumanlar[0]);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void SahipsizKayitYoksaOneriGorunmez()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var gorunur = AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol) { OrphanSource = _ => Array.Empty<string>() };
            view.ScanOrphans("C:/kayit");
            return view.RecoverVisible;
        });
        Assert.False(gorunur);
    }

    [FfmpegFact]
    public async Task UretilmisYarimKayitMp4OlarakKurtarilir()
    {
        var klasor = CalismaKlasoru();
        try
        {
            var yakalama = Path.Combine(klasor, "Canli.yakalama.mkv");
            var uret = await FfmpegRunner.RunAsync(new[]
            {
                "-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "testsrc=size=160x120:rate=10:duration=1",
                "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2", yakalama
            });
            Assert.True(uret.Ok, uret.StandardError);

            var hedef = RecorderArguments.RecoveryTarget(yakalama, RecorderContainer.Mp4, File.Exists);
            var ok = await RecorderSession.RecoverAsync(yakalama, hedef, async a => (await FfmpegRunner.RunAsync(a)).Ok);

            Assert.True(ok);
            Assert.True(File.Exists(hedef));
            Assert.True(new FileInfo(hedef).Length > 0);
            Assert.False(File.Exists(yakalama));
            Assert.True(await RecorderSession.HasPacketAsync(hedef));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }
}
