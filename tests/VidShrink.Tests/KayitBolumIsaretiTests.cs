using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kayıt sürerken kısayolla konan bölüm işaretleri (<see cref="RecorderChapters"/>,
/// <see cref="RecorderSession.Mark()"/>, <c>RecorderView.Bolum.cs</c>). Saf kol: işaretin çıktı
/// zamanındaki yeri (birleşen kayıtta baştan beri, bölünen kayıtta parçanın kendi başından),
/// yakın basışın tek işaret sayılması, ilk bölümün 0'dan başlaması, işaretsiz dosyaya
/// dokunulmaması, ffmetadata metni. <c>[FfmpegFact]</c> kolu 4 sn'lik lavfi klibine
/// (<c>-threads 2</c>) bölümleri kopyayla yazıp ffprobe'la geri okur ve kap tablosunu
/// (<see cref="RecorderChapters.Carries"/>) ölçümle karşılaştırır. <c>[KayitFact]</c> kolu gerçek
/// oturumda duraklatılan sürenin sayılmadığını, bölünen kayıtta işaretin kendi parçasına
/// düştüğünü ve GIF'te işaretin kayıp olarak bildirildiğini ölçer. Arayüz kolu sahte kancayla:
/// gerçek genel kısayol kaydedilmez, ayar her ölçünün kendi dosyasındadır. Kanıt
/// <c>.calisma/kayit-bolum-isareti/</c>, test siler.
/// </summary>
public sealed class KayitBolumIsaretiTests
{
    private sealed class SahteKanca : IGlobalHotkeys
    {
        private Action<HotkeyAction>? _basildi;

        public IReadOnlyList<HotkeyBinding>? Kayitli { get; private set; }

        public IReadOnlyList<HotkeyBinding> Register(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyAction> pressed)
        {
            _basildi = pressed;
            Kayitli = bindings;
            return Array.Empty<HotkeyBinding>();
        }

        public void Unregister() => Kayitli = null;

        public bool Bas(Key tus)
        {
            if (Kayitli?.FirstOrDefault(b => b.Key == tus && b.Modifiers == KeyModifiers.None) is not { } bag) return false;
            _basildi!(bag.Action);
            return true;
        }
    }

    private static readonly string[] YeniAnahtarlar =
    {
        "recorder.hotkeys.chapter", "recorder.chapter.marked", "recorder.chapter.title",
        "recorder.chapter.unsupported", "recorder.chapter.failed"
    };

    private static readonly TimeSpan Esik = TimeSpan.FromSeconds(RecorderChapters.MinGapSeconds);

    private static TimeSpan Sn(double saniye) => TimeSpan.FromSeconds(saniye);

    private static string Klasor()
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "kayit-bolum-isareti", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static void Sil(string klasor)
    {
        try { Directory.Delete(klasor, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static async Task<IReadOnlyList<ChapterMark>> Bolumler(string dosya)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffprobe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { "-v", "error", "-show_chapters", "-of", "json", dosya }) psi.ArgumentList.Add(arg);

        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var cikti = await surec.StandardOutput.ReadToEndAsync();
        await hata;
        await surec.WaitForExitAsync();
        Assert.True(surec.ExitCode == 0, hata.Result);

        using var belge = JsonDocument.Parse(cikti);
        var bolumler = new List<ChapterMark>();
        foreach (var bolum in belge.RootElement.GetProperty("chapters").EnumerateArray())
        {
            bolumler.Add(new ChapterMark(
                bolumler.Count + 1,
                double.Parse(bolum.GetProperty("start_time").GetString()!, CultureInfo.InvariantCulture),
                double.Parse(bolum.GetProperty("end_time").GetString()!, CultureInfo.InvariantCulture),
                bolum.TryGetProperty("tags", out var etiket) && etiket.TryGetProperty("title", out var baslik)
                    ? baslik.GetString() ?? string.Empty
                    : string.Empty));
        }

        return bolumler;
    }

    private static async Task<double> Sure(string dosya) => (await FfprobeClient.ProbeAsync(dosya)).DurationSeconds;

    [Fact]
    public void YakinIkiBasisTekIsaretSayilir()
    {
        var isaretler = new List<RecorderMark> { new(0, Sn(10)) };

        Assert.False(RecorderChapters.Accepts(isaretler, new RecorderMark(0, Sn(10) + Esik / 2)));
        Assert.True(RecorderChapters.Accepts(isaretler, new RecorderMark(0, Sn(10) + Esik)));
        Assert.True(RecorderChapters.Accepts(isaretler, new RecorderMark(0, Sn(10) + Esik * 3)));
    }

    [Fact]
    public void DosyaninBasinaYakinBasisIlkBolumeKarisir()
    {
        var bos = Array.Empty<RecorderMark>();

        Assert.False(RecorderChapters.Accepts(bos, new RecorderMark(0, Esik / 2)));
        Assert.True(RecorderChapters.Accepts(bos, new RecorderMark(0, Esik)));
    }

    [Fact]
    public void YakinlikYalnizAyniParcadaSayilir()
    {
        var isaretler = new List<RecorderMark> { new(0, Sn(10)) };

        Assert.True(RecorderChapters.Accepts(isaretler, new RecorderMark(1, Sn(10) + Esik / 2)));
        Assert.False(RecorderChapters.Accepts(isaretler, new RecorderMark(0, Sn(10) + Esik / 2)));
    }

    [Fact]
    public void BirlesenKayittaIsaretOncekiParcalarinSuresiniSayar()
    {
        var isaret = RecorderChapters.Position(split: false, segment: 2, Sn(12), Sn(3));

        Assert.Equal(new RecorderMark(0, Sn(15)), isaret);
    }

    [Fact]
    public void BolunenKayittaIsaretParcaninKendiBasindanSayilir()
    {
        var isaret = RecorderChapters.Position(split: true, segment: 2, Sn(12), Sn(3));

        Assert.Equal(new RecorderMark(2, Sn(3)), isaret);
    }

    [Fact]
    public void TeslimSureleriBolmeyeGoreToplanirYaDaAyriKalir()
    {
        var parcalar = new[] { Sn(4), Sn(6), Sn(2.5) };

        Assert.Equal(new[] { Sn(12.5) }, RecorderChapters.PartDurations(split: false, parcalar));
        Assert.Equal(parcalar, RecorderChapters.PartDurations(split: true, parcalar));
    }

    [Fact]
    public void IlkBolumSifirdanBaslarSonuncusuDosyaSonundaBiter()
    {
        var isaretler = new[] { new RecorderMark(0, Sn(7)), new RecorderMark(0, Sn(3)) };

        var bolumler = RecorderChapters.ForPart(isaretler, 0, Sn(10), "Bölüm {0}");

        Assert.Equal(
            new[]
            {
                new RecorderChapter(TimeSpan.Zero, Sn(3), "Bölüm 1"),
                new RecorderChapter(Sn(3), Sn(7), "Bölüm 2"),
                new RecorderChapter(Sn(7), Sn(10), "Bölüm 3")
            },
            bolumler);
    }

    [Fact]
    public void IsaretYoksaBolumListesiBos()
    {
        Assert.Empty(RecorderChapters.ForPart(Array.Empty<RecorderMark>(), 0, Sn(10), "Bölüm {0}"));
        Assert.Empty(RecorderChapters.ForPart(new[] { new RecorderMark(1, Sn(3)) }, 0, Sn(10), "Bölüm {0}"));
        Assert.Equal(2, RecorderChapters.ForPart(new[] { new RecorderMark(1, Sn(3)) }, 1, Sn(10), "Bölüm {0}").Count);
    }

    [Fact]
    public void SureyeSigmayanVeYakinIsaretAtilir()
    {
        var isaretler = new[]
        {
            new RecorderMark(0, Esik / 2),
            new RecorderMark(0, Sn(4)),
            new RecorderMark(0, Sn(4) + Esik / 2),
            new RecorderMark(0, Sn(10)),
            new RecorderMark(0, Sn(11))
        };

        var bolumler = RecorderChapters.ForPart(isaretler, 0, Sn(10), "{0}");

        Assert.Equal(new[] { TimeSpan.Zero, Sn(4) }, bolumler.Select(b => b.Start));
        Assert.Equal(Sn(10), bolumler[^1].End);
    }

    [Fact]
    public void HerIsaretKendiParcasinaDuser()
    {
        var isaretler = new[] { new RecorderMark(0, Sn(2)), new RecorderMark(1, Sn(1.5)), new RecorderMark(1, Sn(3)) };

        var ilk = RecorderChapters.ForPart(isaretler, 0, Sn(5), "{0}");
        var ikinci = RecorderChapters.ForPart(isaretler, 1, Sn(5), "{0}");

        Assert.Equal(new[] { TimeSpan.Zero, Sn(2) }, ilk.Select(b => b.Start));
        Assert.Equal(new[] { TimeSpan.Zero, Sn(1.5), Sn(3) }, ikinci.Select(b => b.Start));
    }

    [Fact]
    public void MetaVeriMetniMilisaniyeTabanliVeBasligiKacirir()
    {
        var metin = RecorderChapters.Metadata(new[]
        {
            new RecorderChapter(TimeSpan.Zero, Sn(1.2345), "Bölüm 1"),
            new RecorderChapter(Sn(1.2345), Sn(4), "a=b;c#d\\e\r\nf")
        });

        Assert.Equal(
            ";FFMETADATA1\n"
            + "[CHAPTER]\nTIMEBASE=1/1000\nSTART=0\nEND=1235\ntitle=Bölüm 1\n"
            + "[CHAPTER]\nTIMEBASE=1/1000\nSTART=1235\nEND=4000\ntitle=a\\=b\\;c\\#d\\\\e\\\nf\n",
            metin);
    }

    [Fact]
    public void YenidenPaketlemeKopyalarBolumuIkinciGirdidenAlir()
    {
        var mkv = RecorderChapters.BuildRemux("a.mkv", "a.txt", "b.mkv");
        var mp4 = RecorderChapters.BuildRemux("a.mp4", "a.txt", "b.mp4");

        Assert.Equal(
            new[]
            {
                "-hide_banner", "-y", "-nostdin", "-i", "a.mkv", "-f", "ffmetadata", "-i", "a.txt",
                "-map", "0", "-map_metadata", "0", "-map_chapters", "1", "-c", "copy", "b.mkv"
            },
            mkv);
        Assert.DoesNotContain("-movflags", mkv);
        Assert.Equal(new[] { "-movflags", "+faststart", "b.mp4" }, mp4.Skip(mp4.Count - 3));
        Assert.DoesNotContain(mp4, a => a.StartsWith("-c:", StringComparison.Ordinal) || a == "-crf" || a == "-b:v");
    }

    [FfmpegFact]
    public async Task BolumlerKopyaylaYazilirVeKapTablosuOlcumleAyni()
    {
        var klasor = Klasor();
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mkv");
            var uret = await FfmpegRunner.RunAsync(new[]
            {
                "-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "testsrc=size=160x90:rate=15:duration=4",
                "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", kaynak
            });
            Assert.True(uret.Ok, uret.StandardError);
            Assert.Empty(await Bolumler(kaynak));

            var isaretler = new[] { new RecorderMark(0, Sn(1.5)), new RecorderMark(0, Sn(3)) };
            var bolumler = RecorderChapters.ForPart(isaretler, 0, Sn(await Sure(kaynak)), "Bölüm {0}");
            var meta = Path.Combine(klasor, "bolumler.txt");
            File.WriteAllText(meta, RecorderChapters.Metadata(bolumler));

            foreach (var kap in new[] { RecorderContainer.Mkv, RecorderContainer.Mp4, RecorderContainer.Mov, RecorderContainer.Gif })
            {
                var hedef = Path.Combine(klasor, "cikti." + kap.ToString().ToLowerInvariant());
                var args = kap == RecorderContainer.Gif
                    ? new[]
                    {
                        "-hide_banner", "-y", "-nostdin", "-i", kaynak, "-f", "ffmetadata", "-i", meta,
                        "-map", "0:v", "-map_chapters", "1", "-threads", "2", "-t", "1", hedef
                    }
                    : RecorderChapters.BuildRemux(kaynak, meta, hedef);
                var yaz = await FfmpegRunner.RunAsync(args);
                Assert.True(yaz.Ok, kap + ": " + yaz.StandardError);

                var okunan = await Bolumler(hedef);
                Assert.Equal(RecorderChapters.Carries(kap), okunan.Count > 0);
                Assert.Equal(kap, RecorderArguments.ContainerOf(hedef));
                if (okunan.Count == 0) continue;

                Assert.Equal(3, okunan.Count);
                Assert.Equal(0, okunan[0].StartSeconds, 3);
                Assert.Equal(1.5, okunan[1].StartSeconds, 2);
                Assert.Equal(3, okunan[2].StartSeconds, 2);
                Assert.Equal(new[] { "Bölüm 1", "Bölüm 2", "Bölüm 3" }, okunan.Select(b => b.Title));
                Assert.InRange(await Sure(hedef), 3.8, 4.2);
            }

            Assert.False(RecorderChapters.Carries(null));
        }
        finally { Sil(klasor); }
    }

    private static RecorderRequest Istek(RecorderContainer kap) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Region = new RecorderRegion(0, 0, 320, 240),
        Fps = 15,
        Container = kap,
        Preset = "ultrafast"
    };

    private static async Task<int> IsaretKoy(RecorderSession oturum)
    {
        for (var i = 0; i < 100; i++)
        {
            if (oturum.Mark() is { } bolum) return bolum;
            await Task.Delay(150);
        }

        return 0;
    }

    private static string[] Artiklar(string klasor)
        => Directory.GetFiles(klasor).Where(d => d.Contains(".bolumler", StringComparison.Ordinal) || d.Contains(".isaretli", StringComparison.Ordinal)).ToArray();

    [KayitFact]
    public async Task DuraklatilanSureIsareteSayilmaz()
    {
        var klasor = Klasor();
        try
        {
            var oturum = await RecorderSession.StartAsync(Istek(RecorderContainer.Mkv), Path.Combine(klasor, "kayit.mkv"));
            oturum.ChapterTitle = "Kısım {0}";

            var ilk = await IsaretKoy(oturum);
            var hemen = oturum.Mark();
            await oturum.PauseAsync();
            var duraklamada = oturum.Mark();
            await Task.Delay(4000);
            await oturum.ResumeAsync();
            var ikinci = await IsaretKoy(oturum);
            await Task.Delay(1500);
            var sonuc = await oturum.StopAsync();

            Assert.True(sonuc.Ok, sonuc.StandardError);
            Assert.Equal(2, ilk);
            Assert.Null(hemen);
            Assert.Null(duraklamada);
            Assert.Equal(3, ikinci);
            Assert.Equal(2, sonuc.Segments);
            Assert.Equal(2, sonuc.MarksWritten);
            Assert.Equal(0, sonuc.MarksLost);
            Assert.False(sonuc.ChaptersUnsupported);

            var bolumler = await Bolumler(sonuc.OutputPath);
            var sure = await Sure(sonuc.OutputPath);
            Assert.Equal(new[] { "Kısım 1", "Kısım 2", "Kısım 3" }, bolumler.Select(b => b.Title));
            Assert.Equal(0, bolumler[0].StartSeconds, 3);
            Assert.InRange(bolumler[1].StartSeconds, RecorderChapters.MinGapSeconds, sure);
            Assert.InRange(bolumler[2].StartSeconds - bolumler[1].StartSeconds, RecorderChapters.MinGapSeconds, 3.5);
            Assert.InRange(bolumler[2].StartSeconds, bolumler[1].StartSeconds, sure);
            Assert.InRange(bolumler[2].EndSeconds, sure - 0.5, sure + 0.5);
            Assert.Empty(Artiklar(klasor));
        }
        finally { Sil(klasor); }
    }

    [KayitFact]
    public async Task BolunenKayittaIsaretKendiParcasinaYazilir()
    {
        var klasor = Klasor();
        try
        {
            var istek = Istek(RecorderContainer.Mkv) with { MaxDuration = Sn(5), Split = new RecorderSplit(Sn(2)) };
            var oturum = await RecorderSession.StartAsync(istek, Path.Combine(klasor, "kayit.mkv"));

            var konan = new List<int> { await IsaretKoy(oturum) };
            while (konan.Count(b => b == 2) < 2 && konan[^1] != 0) konan.Add(await IsaretKoy(oturum));

            var bitti = await Task.WhenAny(oturum.Ended, Task.Delay(20000));
            var sonuc = await oturum.StopAsync();

            Assert.True(bitti == oturum.Ended, "kayit 20 sn icinde kendiliginden bitmedi");
            Assert.True(sonuc.Ok, sonuc.StandardError);
            Assert.DoesNotContain(0, konan);
            Assert.True(sonuc.Files is { Count: >= 2 }, "kayit bolunmedi");
            Assert.Equal(konan.Count, sonuc.MarksWritten);
            Assert.Equal(0, sonuc.MarksLost);

            var ilk = await Bolumler(sonuc.Files![0]);
            var ikinci = await Bolumler(sonuc.Files[1]);
            Assert.Equal(konan.Count, ilk.Count);
            Assert.Equal(2, ikinci.Count);
            Assert.Equal(0, ikinci[0].StartSeconds, 3);
            Assert.InRange(ikinci[1].StartSeconds, RecorderChapters.MinGapSeconds, await Sure(sonuc.Files[1]));
            Assert.Equal("Chapter 2", ikinci[1].Title);
            Assert.Empty(Artiklar(klasor));
        }
        finally { Sil(klasor); }
    }

    [KayitFact]
    public async Task BolumTasimayanKaptaIsaretKayipDiyeBildirilir()
    {
        var klasor = Klasor();
        try
        {
            var oturum = await RecorderSession.StartAsync(Istek(RecorderContainer.Gif), Path.Combine(klasor, "kayit.gif"));
            var konan = await IsaretKoy(oturum);
            var sonuc = await oturum.StopAsync();

            Assert.Equal(2, konan);
            Assert.True(sonuc.Ok, sonuc.StandardError);
            Assert.EndsWith(".gif", sonuc.OutputPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, sonuc.MarksWritten);
            Assert.Equal(1, sonuc.MarksLost);
            Assert.True(sonuc.ChaptersUnsupported);
            Assert.Equal(new[] { sonuc.OutputPath }, Directory.GetFiles(klasor));
        }
        finally { Sil(klasor); }
    }

    [KayitFact]
    public async Task IsaretsizVeIsaretiBirakilanKayitYenidenPaketlenmez()
    {
        var klasor = Klasor();
        try
        {
            var oturum = await RecorderSession.StartAsync(Istek(RecorderContainer.Mkv), Path.Combine(klasor, "kayit.mkv"));
            var konan = await IsaretKoy(oturum);
            oturum.DropMarks();
            var sonuc = await oturum.StopAsync();

            Assert.Equal(2, konan);
            Assert.True(sonuc.Ok, sonuc.StandardError);
            Assert.Equal(0, sonuc.MarksWritten);
            Assert.Equal(0, sonuc.MarksLost);
            Assert.Empty(await Bolumler(sonuc.OutputPath));
        }
        finally { Sil(klasor); }
    }

    [Fact]
    public void KayitYokkenKisayolIsaretKoymaz()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var o = AppHost.Run(() =>
        {
            Strings.Use("en");
            var view = new RecorderView(ayar.Yol);
            var kanca = new SahteKanca();
            view.GlobalHotkeys = kanca;
            view.ActivateHotkeys();
            var kayitli = kanca.Bas(Key.F6);
            var kostu = view.RunHotkeyAsync(HotkeyAction.Chapter).GetAwaiter().GetResult();
            var elle = view.MarkChapter();
            var sonuc = (kayitli, kostu, elle, view.CanMarkChapter, view.NoticeText, view.ChapterText);
            view.DeactivateHotkeys();
            return sonuc;
        });

        Assert.True(o.kayitli);
        Assert.False(o.kostu);
        Assert.False(o.elle);
        Assert.False(o.CanMarkChapter);
        Assert.Equal(string.Empty, o.NoticeText);
        Assert.Equal(string.Empty, o.ChapterText);
    }

    [Theory]
    [InlineData("en", "Chapter 3 Marked")]
    [InlineData("tr", "Bölüm 3 İşaretlendi")]
    public void KonanIsaretDurumSatirindaSoylenir(string dil, string beklenen)
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var (konmadi, bos, kondu, metin) = AppHost.Run(() =>
        {
            Strings.Use(dil);
            try
            {
                var view = new RecorderView(ayar.Yol);
                var k = view.MarkChapter(() => null);
                var b = view.NoticeText;
                return (k, b, view.MarkChapter(() => 3), view.NoticeText);
            }
            finally { Strings.Use("en"); }
        });

        Assert.False(konmadi);
        Assert.Equal(string.Empty, bos);
        Assert.True(kondu);
        Assert.Equal(beklenen, metin);
    }

    [Fact]
    public void YazilamayanIsaretUyariSatirindaSoylenir()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var o = AppHost.Run(() =>
        {
            Strings.Use("en");
            var view = new RecorderView(ayar.Yol);
            string Goster(RecordResult sonuc)
            {
                view.ShowResult(sonuc);
                return view.WarningText;
            }

            var temiz = new RecordResult(true, "kayit.mkv", 1, false, 0, string.Empty, 1);
            return (
                yok: Goster(temiz with { MarksWritten = 2 }),
                tasimaz: Goster(temiz with { OutputPath = "kayit.gif", MarksLost = 2, ChaptersUnsupported = true }),
                dustu: Goster(temiz with { MarksLost = 1 }),
                yarim: Goster(temiz with { Ok = false, Partial = true, Playable = true, MarksLost = 1 }),
                yarimIsaretsiz: Goster(temiz with { Ok = false, Partial = true, Playable = true }),
                gifsiz: Goster(temiz with { MissingGif = "kayit.gif" }),
                gifsizIsaretli: Goster(temiz with { MissingGif = "kayit.gif", MarksLost = 1 }));
        });

        const string tasimaz = "This format does not carry chapters; the chapter marks were not written.";
        const string dustu = "The chapter marks could not be written to the file.";
        Assert.Equal(string.Empty, o.yok);
        Assert.Equal(tasimaz, o.tasimaz);
        Assert.Equal(dustu, o.dustu);
        Assert.NotEqual(string.Empty, o.yarimIsaretsiz);
        Assert.Equal(o.yarimIsaretsiz + Environment.NewLine + dustu, o.yarim);
        Assert.NotEqual(string.Empty, o.gifsiz);
        Assert.Equal(o.gifsiz + Environment.NewLine + dustu, o.gifsizIsaretli);
    }

    [Fact]
    public void MiniSeritIsaretiSayacinYanindaGosterir()
    {
        var (once, sonra, silindi) = AppHost.Run(() =>
        {
            var mini = new RecorderMini();
            var o = (mini.ChapterText, mini.TxtChapter.IsVisible);
            mini.ShowChapter("Chapter 2 Marked");
            var s = (mini.ChapterText, mini.TxtChapter.IsVisible);
            mini.ShowChapter(string.Empty);
            return (o, s, (mini.ChapterText, mini.TxtChapter.IsVisible));
        });

        Assert.Equal((string.Empty, false), once);
        Assert.Equal(("Chapter 2 Marked", true), sonra);
        Assert.Equal((string.Empty, false), silindi);
    }

    [Fact]
    public void KisayolSatirindaBolumIsaretiVarsayilanTusuyla()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var (ad, etiket, eylem) = AppHost.Run(() =>
        {
            Strings.Use("en");
            var view = new RecorderView(ayar.Yol);
            var satir = (Grid)view.HotkeyBox(HotkeyAction.Chapter).Parent!;
            return (
                view.HotkeyName(HotkeyAction.Chapter),
                satir.Children.OfType<TextBlock>().First().Text,
                RecorderHotkeys.ActionOf(Key.F6, KeyModifiers.None));
        });

        Assert.Equal("F6", ad);
        Assert.Equal("Mark a chapter", etiket);
        Assert.Equal(HotkeyAction.Chapter, eylem);
    }

    [Fact]
    public void EskiAyarAtamalariKorurBolumIsaretiVarsayilaniAlir()
    {
        var eski = RecorderHotkeys.Read("0:F1,0:F2,0:F3,0:F4,0:F5")!;

        Assert.Equal(RecorderHotkeys.All.Count, eski.Count);
        Assert.Equal(Key.F1, RecorderHotkeys.Of(eski, HotkeyAction.Toggle).Key);
        Assert.Equal(Key.F6, RecorderHotkeys.Of(eski, HotkeyAction.Chapter).Key);
        Assert.Null(RecorderHotkeys.Read("0:F1,0:F2,0:F3,0:F4,0:F6"));
        Assert.Null(RecorderHotkeys.Read("0:F1,0:F2,0:F3,0:F4"));
    }

    [Fact]
    public void BolumMetinleriButunDillerde()
    {
        foreach (var dil in Locales.Languages)
        {
            var metin = Locales.Values(dil);
            foreach (var anahtar in YeniAnahtarlar)
                Assert.False(string.IsNullOrWhiteSpace(metin.GetValueOrDefault(anahtar)), dil + " " + anahtar);

            Assert.Contains("{0}", metin["recorder.chapter.marked"]);
            Assert.Contains("{0}", metin["recorder.chapter.title"]);
            Assert.DoesNotContain("F6", metin["recorder.hotkeys.chapter"]);
        }
    }
}
