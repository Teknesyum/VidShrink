using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// DZA-85 ilk yarisi, kap ici iz isleri (<see cref="Remux"/>): <c>-c copy</c> ile yeniden paketleme, iz basina dil, ad ve
/// varsayilan bayragi, dosya basligi, iz kaydirma (ayni kaynagin <c>-itsoffset</c>'li ikinci girdisinden esleme) ve
/// donus bayragi (<c>-display_rotation</c>, saat yonu girilir, ffmpeg'e tersi yazilir). Canli kol iki ses izli 2 sn
/// 320x240 MKV'yi (<c>-threads 2</c>) yeniden paketleyip ffprobe ile basligi, dili, adi, bayragi, kaydirilan izin
/// baslangicini ve donus matrisini okur. Kanit <c>.calisma/worktree-agent-abc2ed49a4f3cc92a/kap-ici/</c>, test siler.
/// </summary>
public sealed class KapIciIzTests
{
    internal static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-abc2ed49a4f3cc92a", "kap-ici");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static readonly RemuxTrack[] Uc =
    {
        new(0, StreamKind.Video),
        new(1, StreamKind.Audio, "tur", "Ana", true),
        new(2, StreamKind.Audio, "eng", null, false)
    };

    private static List<string> Args(IReadOnlyList<RemuxTrack> izler, string? baslik = null)
        => Remux.Arguments(new RemuxRequest("k.mkv", "o.mkv", izler, baslik)).ToList();

    private static string Sonraki(List<string> args, string anahtar)
    {
        var i = args.IndexOf(anahtar);
        Assert.True(i >= 0 && i + 1 < args.Count, anahtar + " yok: " + string.Join(' ', args));
        return args[i + 1];
    }

    [Fact]
    public void DegisiksizIstekTekGirdiVeKopyaYazar()
    {
        var args = Args(Uc);
        Assert.Equal(new[] { "-hide_banner", "-nostdin", "-y", "-i", "k.mkv" }, args.Take(5));
        Assert.Equal(1, args.Count(a => a == "-i"));
        Assert.Equal(new[] { "0:0", "0:1", "0:2" }, args.Select((a, i) => (a, i)).Where(x => x.a == "-map").Select(x => args[x.i + 1]));
        Assert.Equal("copy", Sonraki(args, "-c"));
        Assert.Equal("o.mkv", args[^1]);
        Assert.DoesNotContain("-itsoffset", args);
        Assert.DoesNotContain(args, a => a.StartsWith("-display_rotation", StringComparison.Ordinal));
        Assert.DoesNotContain("-metadata", args);
    }

    [Fact]
    public void IzBasinaDilAdVeBayrakCiktiSirasinaYazilir()
    {
        var args = Args(Uc);
        Assert.Equal("language=tur", Sonraki(args, "-metadata:s:1"));
        Assert.Contains("title=Ana", args);
        Assert.Equal(args.IndexOf("title=Ana") - 1, args.LastIndexOf("-metadata:s:1"));
        Assert.Equal("language=eng", Sonraki(args, "-metadata:s:2"));
        Assert.DoesNotContain(args, a => a.StartsWith("title=", StringComparison.Ordinal) && a != "title=Ana");
        Assert.Equal("+default", Sonraki(args, "-disposition:1"));
        Assert.Equal("-default", Sonraki(args, "-disposition:2"));
        Assert.Equal("-default", Sonraki(args, "-disposition:0"));
        Assert.DoesNotContain("-metadata:s:0", args);
    }

    [Fact]
    public void BosAdIziTemizlerBasliktaDosyaUstVerisiOlur()
    {
        var izler = new[] { Uc[0], Uc[1] with { Title = "" } };
        var args = Args(izler, "Tatil");
        Assert.Equal("title=Tatil", Sonraki(args, "-metadata"));
        Assert.Contains("title=", args);
        Assert.Equal("title=", args[args.LastIndexOf("-metadata:s:1") + 1]);
    }

    [Fact]
    public void DilKoduKucukHarfeIner_GecersizKodReddedilir()
    {
        Assert.Equal("language=tur", Sonraki(Args(new[] { Uc[0], Uc[1] with { Language = " TUR " } }), "-metadata:s:1"));
        Assert.Throws<ArgumentException>(() => Args(new[] { Uc[0], Uc[1] with { Language = "turkce" } }));
        Assert.Throws<ArgumentException>(() => Args(new[] { Uc[0], Uc[1] with { Language = "t1r" } }));
        Assert.True(Remux.IsLanguage("eng"));
        Assert.False(Remux.IsLanguage("en"));
    }

    [Fact]
    public void KaydirilanIzAyniKaynaginItsoffsetliGirdisindenEslenir()
    {
        var izler = new[] { Uc[0], Uc[1] with { OffsetMs = 500 }, Uc[2] with { OffsetMs = -250 } };
        var args = Args(izler);
        Assert.Equal(3, args.Count(a => a == "-i"));
        var ilk = args.IndexOf("-itsoffset");
        Assert.Equal("0.5", args[ilk + 1]);
        Assert.Equal(new[] { "-i", "k.mkv" }, args.Skip(ilk + 2).Take(2));
        var ikinci = args.LastIndexOf("-itsoffset");
        Assert.Equal("-0.25", args[ikinci + 1]);
        Assert.Equal(new[] { "-i", "k.mkv" }, args.Skip(ikinci + 2).Take(2));
        Assert.Equal(new[] { "0:0", "1:1", "2:2" }, args.Select((a, i) => (a, i)).Where(x => x.a == "-map").Select(x => args[x.i + 1]));
        Assert.True(args.IndexOf("-map") > ikinci);
    }

    [Fact]
    public void AyniKaydirmaTekGirdiyiPaylasir()
    {
        var izler = new[] { Uc[0], Uc[1] with { OffsetMs = 1200 }, Uc[2] with { OffsetMs = 1200 } };
        var args = Args(izler);
        Assert.Equal(2, args.Count(a => a == "-i"));
        Assert.Equal("1.2", Sonraki(args, "-itsoffset"));
        Assert.Equal(new[] { "0:0", "1:1", "1:2" }, args.Select((a, i) => (a, i)).Where(x => x.a == "-map").Select(x => args[x.i + 1]));
    }

    [Theory]
    [InlineData(90, "270")]
    [InlineData(180, "180")]
    [InlineData(270, "90")]
    [InlineData(0, "0")]
    public void DonusSaatYonundenTersineCevrilipGirdidenOnceYazilir(int saat, string ters)
    {
        var args = Args(new[] { Uc[0] with { Rotation = saat }, Uc[1] });
        var i = args.IndexOf("-display_rotation:0");
        Assert.True(i >= 0, string.Join(' ', args));
        Assert.Equal(ters, args[i + 1]);
        Assert.Equal("-i", args[i + 2]);
    }

    [Fact]
    public void KaydirilanGoruntununDonusuKendiGirdisininOnundedir()
    {
        var args = Args(new[] { Uc[0] with { Rotation = 90, OffsetMs = 100 }, Uc[1] });
        var don = args.IndexOf("-display_rotation:0");
        Assert.True(don > args.IndexOf("-i"), string.Join(' ', args));
        Assert.True(don < args.LastIndexOf("-i"), string.Join(' ', args));
        Assert.Equal("1:0", Sonraki(args, "-map"));
    }

    [Fact]
    public void GecersizDonusVeSesteDonusReddedilir()
    {
        Assert.Throws<ArgumentException>(() => Args(new[] { Uc[0] with { Rotation = 45 } }));
        Assert.Throws<ArgumentException>(() => Args(new[] { Uc[0], Uc[1] with { Rotation = 90 } }));
        Assert.Throws<ArgumentException>(() => Args(Array.Empty<RemuxTrack>()));
    }

    [Fact]
    public void KaynakIzleriVeriAkisiDisindaAlinir()
    {
        var bilgi = new MediaInfo
        {
            FilePath = "k.mkv", FileSizeBytes = 1, DurationSeconds = 2, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1,
            Streams = new[]
            {
                new SourceStream(0, StreamKind.Video, "h264", null, true, false, 0, 0, 0),
                new SourceStream(1, StreamKind.Audio, "aac", "tur", true, false, 2, 0, 0, Title: "Ana"),
                new SourceStream(2, StreamKind.Data, "bin_data", null, false, false, 0, 0, 0),
                new SourceStream(3, StreamKind.Subtitle, "subrip", "eng", false, false, 0, 0, 0)
            }
        };
        var izler = Remux.TracksOf(bilgi);
        Assert.Equal(new[] { 0, 1, 3 }, izler.Select(t => t.Index));
        Assert.Equal(new RemuxTrack(1, StreamKind.Audio, "tur", "Ana", true), izler[1]);
        Assert.Null(izler[0].Rotation);
        Assert.Equal(0, izler[2].OffsetMs);
    }

    [FfmpegFact]
    public async Task CanliYenidenPaketlemeUstVeriyiYazar()
    {
        KanitKapanisi.Onceki(Kanit, "kaynak.mkv", "cikti.mkv", "olcu.txt");
        var kaynak = Path.Combine(Kanit, "kaynak.mkv");
        Ffmpeg("-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=2",
            "-f", "lavfi", "-i", "sine=f=440:d=2",
            "-f", "lavfi", "-i", "sine=f=880:d=2",
            "-map", "0:v", "-map", "1:a", "-map", "2:a",
            "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2",
            "-c:a", "aac", "-metadata:s:a:0", "language=eng", "-metadata:s:a:1", "language=eng", kaynak);
        var cikti = Path.Combine(Kanit, "cikti.mkv");
        var istek = new RemuxRequest(kaynak, cikti, new[]
        {
            new RemuxTrack(0, StreamKind.Video, Rotation: 90),
            new RemuxTrack(1, StreamKind.Audio, "tur", "Türkçe", false),
            new RemuxTrack(2, StreamKind.Audio, "jpn", "Yorum", true, 500)
        }, "Deneme Başlığı");

        await RemuxRunner.RunAsync(istek, 2, null);

        var dokum = Ffprobe("-show_entries", "format_tags=title:stream=index,start_time:stream_tags=language,title:stream_disposition=default:stream_side_data=rotation",
            "-of", "compact", cikti);
        File.WriteAllText(Path.Combine(Kanit, "olcu.txt"), dokum);
        var satirlar = dokum.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string Iz(int i) => satirlar.Single(s => s.StartsWith($"stream|index={i}|", StringComparison.Ordinal));
        Assert.Contains("tag:title=Deneme Başlığı", satirlar.Single(s => s.StartsWith("format|", StringComparison.Ordinal)));
        Assert.Contains("rotation=-90", Iz(0));
        Assert.Contains("tag:language=tur", Iz(1));
        Assert.Contains("tag:title=Türkçe", Iz(1));
        Assert.Contains("disposition:default=0", Iz(1));
        Assert.Contains("tag:language=jpn", Iz(2));
        Assert.Contains("disposition:default=1", Iz(2));
        Assert.Contains("start_time=0.500000", Iz(2));
        Assert.Contains("start_time=0.000000", Iz(1));
        Assert.Empty(Directory.GetFiles(Kanit, "vidshrink_partial_*"));
        KanitKapanisi.Kapat(Kanit, "kaynak.mkv", "cikti.mkv", "olcu.txt");
    }

    internal static void Ffmpeg(params string[] args)
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

    private static string Ffprobe(params string[] args)
    {
        var psi = new ProcessStartInfo("ffprobe") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8 };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var metin = surec.StandardOutput.ReadToEnd();
        Assert.True(surec.WaitForExit(15000));
        return metin;
    }
}

/// <summary>
/// DZA-85 arayuz kolu: teslim acilirindaki kap ici iz bolumu kaynagin izlerini sahte <c>SourceProber</c>'dan okur, iz
/// secimi dil, ad, bayrak, kaydirma ve donus kutularini doldurur, kutulara yazilan deger istege iner (donus yalniz
/// goruntu izinde etkin), gecersiz dil ve kaynagin ustune yazma reddedilir. Kanit
/// <c>.calisma/worktree-agent-abc2ed49a4f3cc92a/kap-ici-arayuz/</c>, test siler.
/// </summary>
public sealed class KapIciIzArayuzTests
{
    private static string Klasor()
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-abc2ed49a4f3cc92a", "kap-ici-arayuz");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static MediaInfo Bilgi(string yol) => new()
    {
        FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1,
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "h264", null, true, false, 0, 0, 0),
            new SourceStream(1, StreamKind.Audio, "aac", "eng", true, false, 2, 0, 0, Title: "Main"),
            new SourceStream(2, StreamKind.Audio, "aac", "eng", false, false, 2, 0, 0)
        }
    };

    [Fact]
    public void PanelIzleriOkurVeDuzenlemeyiIstegeIndirir()
    {
        var klasor = Klasor();
        var dosya = Path.Combine(klasor, "kaynak.mkv");
        File.WriteAllBytes(dosya, new byte[16]);
        AppHost.Run(() =>
        {
            var view = new EditorView
            {
                KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 },
                AutosaveDelay = TimeSpan.FromHours(1),
            };
            var okunan = 0;
            view.SourceProber = (yol, _) => { okunan++; return Task.FromResult(Bilgi(yol)); };
            view.Player.EngineFactory = () => new IzMotoru(2);
            view.OverlayRoot = Path.Combine(klasor, "katman");
            var pencere = new Window { Width = 900, Height = 600, Content = view };
            pencere.Show();
            try
            {
                var acilis = view.OpenSourceAsync(dosya);
                DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                acilis.GetAwaiter().GetResult();

                Assert.False(view.BtnRemux.IsEnabled);
                var yukle = view.LoadRemuxAsync();
                DenetimSurucu.Pump(view.Player, () => yukle.IsCompleted, 10);
                yukle.GetAwaiter().GetResult();
                Assert.Equal(1, okunan);
                Assert.True(view.BtnRemux.IsEnabled);
                Assert.Equal(3, view.CmbRemuxTrack.ItemCount);

                Assert.Equal(0, view.CmbRemuxTrack.SelectedIndex);
                Assert.True(view.CmbRemuxRotation.IsEnabled);
                view.CmbRemuxRotation.SelectedIndex = 2;

                view.CmbRemuxTrack.SelectedIndex = 1;
                Assert.False(view.CmbRemuxRotation.IsEnabled);
                Assert.Equal("eng", view.TxtRemuxLanguage.Text);
                Assert.Equal("Main", view.TxtRemuxTrackTitle.Text);
                Assert.True(view.ChkRemuxDefault.IsChecked);
                view.TxtRemuxLanguage.Text = "tur";
                view.TxtRemuxTrackTitle.Text = "Türkçe";
                view.ChkRemuxDefault.IsChecked = false;

                view.CmbRemuxTrack.SelectedIndex = 2;
                Assert.Equal(string.Empty, view.TxtRemuxTrackTitle.Text ?? string.Empty);
                view.ChkRemuxDefault.IsChecked = true;
                view.TxtRemuxOffset.Text = "-250";
                view.TxtRemuxFileTitle.Text = "Tatil";

                var istek = view.RemuxRequestFor(Path.Combine(klasor, "cikti.mkv"));
                Assert.NotNull(istek);
                Assert.Equal(dosya, istek!.Source);
                Assert.Equal("Tatil", istek.FileTitle);
                Assert.Equal(new RemuxTrack(0, StreamKind.Video, null, null, true, 0, 90), istek.Tracks[0]);
                Assert.Equal(new RemuxTrack(1, StreamKind.Audio, "tur", "Türkçe", false), istek.Tracks[1]);
                Assert.Equal(new RemuxTrack(2, StreamKind.Audio, "eng", null, true, -250), istek.Tracks[2]);

                view.CmbRemuxTrack.SelectedIndex = 1;
                Assert.Equal("tur", view.TxtRemuxLanguage.Text);
                view.TxtRemuxLanguage.Text = "turkce";
                Assert.Null(view.RemuxRequestFor(Path.Combine(klasor, "cikti.mkv")));
                Assert.False(string.IsNullOrEmpty(view.TxtExportStatus.Text));
                view.TxtRemuxLanguage.Text = "tur";
                Assert.Null(view.RemuxRequestFor(dosya));

                var tekrar = view.LoadRemuxAsync();
                DenetimSurucu.Pump(view.Player, () => tekrar.IsCompleted, 10);
                Assert.Equal(1, okunan);
            }
            finally
            {
                pencere.Close();
            }
        });
        KanitKapanisi.Kapat(Path.GetDirectoryName(klasor)!, Path.GetFileName(klasor));
    }
}
