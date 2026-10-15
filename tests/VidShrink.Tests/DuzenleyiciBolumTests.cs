using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// DZA-P8, duzenleyicide bolum yazma ve koruma (<see cref="EditChapters"/>, <see cref="EditExport.ChaptersFor"/>):
/// kaynagin bolumleri kesimle kayar (silinen aralik duser, hiz kisaltir, ters parca sirayi cevirir, gecis bindirir,
/// Hizli kipin anahtar kare kaymasi geciktirir), parca sinirlari istenirse bolum olur, liste teslimin son adimina
/// Kaydedici'nin ffmetadata yoluyla girer. Bolumsuz kaynakta arguman degismez; kaynakta bolum varken liste bossa
/// <c>-map_chapters -1</c> yazilir. Canli kol 6 sn 320x240 lavfi (<c>-threads 2</c>) uzerinde Tam kipte bolumlerin
/// kaydigini ffprobe ile okur; olumsuz kontrol ayni adimin bolum argumansiz hali (ffmpeg bolumleri kaymamis kopyalar).
/// Kanit <c>.calisma/worktree-agent-a7e2cff5570b98e27/bolum/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciBolumTests
{
    private const long Gb = 1_000_000_000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static readonly double[] Kareler = { 0.0, 2, 4, 6, 8 };

    private static readonly ChapterMark[] Uclu =
    {
        new(1, 0, 10, "A"),
        new(2, 10, 20, "B"),
        new(3, 20, 30, "C")
    };

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-a7e2cff5570b98e27", "bolum");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static MediaInfo Bilgi(params ChapterMark[] bolumler) => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1,
        AudioCodec = "aac",
        AudioChannels = 2,
        PixelFormat = "yuv420p",
        BitDepth = 8,
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "h264"),
            new SourceStream(1, StreamKind.Audio, "aac", "tur", IsDefault: true, Channels: 2)
        },
        Chapters = bolumler
    };

    private static Dictionary<int, ExportSource> Kaynak(MediaInfo bilgi)
        => new() { [0] = new ExportSource(bilgi, Kareler, 0, DuzenleyiciAkilliKodekTests.Sinirlar(Kareler)) };

    private static EditTimeline Cizelge(params EditClip[] klipler) => new(klipler);

    private static IReadOnlyDictionary<int, IReadOnlyList<ChapterMark>> Tek(IReadOnlyList<ChapterMark> bolumler)
        => new Dictionary<int, IReadOnlyList<ChapterMark>> { [0] = bolumler };

    private static string Yaz(IEnumerable<RecorderChapter> bolumler)
        => string.Join(" | ", bolumler.Select(b => string.Create(CultureInfo.InvariantCulture,
            $"{b.Title} {b.Start.TotalSeconds:0.###}-{b.End.TotalSeconds:0.###}")));

    private static string Shifted(EditTimeline cizelge, IReadOnlyList<double>? kayma = null)
        => Yaz(EditChapters.Shifted(cizelge, Tek(Uclu), EditChapters.DefaultTitle, kayma));

    [Fact]
    public void KesilenAralikBolumleriSolaKaydirir()
        => Assert.Equal("A 0-5 | B 5-10 | C 10-20", Shifted(Cizelge(new EditClip(S(0), S(5)), new EditClip(S(15), S(30)))));

    [Fact]
    public void TamamenSilinenBolumDuser()
        => Assert.Equal("A 0-10 | C 10-20", Shifted(Cizelge(new EditClip(S(0), S(10)), new EditClip(S(20), S(30)))));

    [Fact]
    public void TersParcadaBolumSirasiDoner()
        => Assert.Equal("B 0-10 | A 10-20", Shifted(Cizelge(new EditClip(S(0), S(20), reversed: true))));

    [Fact]
    public void HizBolumuKisaltir()
        => Assert.Equal("A 0-5 | B 5-10", Shifted(Cizelge(new EditClip(S(0), S(20), 2m))));

    [Fact]
    public void GecisSonrakiBolumuBindirir()
    {
        var cizelge = Cizelge(new EditClip(S(0), S(10)),
            new EditClip(S(10), S(20)) { Transition = new ClipTransition(TransitionKind.Dissolve, S(1)) });
        Assert.Equal("A 0-9 | B 9-19", Shifted(cizelge));
    }

    [Fact]
    public void BolunenBolumTekBolumKalir()
        => Assert.Equal("A 0-10", Shifted(Cizelge(new EditClip(S(0), S(5)), new EditClip(S(5), S(10)))));

    [Fact]
    public void IkinciKaynagınBolumleriKendiParcasindanSayilir()
    {
        var bolumler = new Dictionary<int, IReadOnlyList<ChapterMark>>
        {
            [0] = Uclu,
            [1] = new ChapterMark[] { new(1, 0, 4, "X"), new(2, 4, 8, "Y") }
        };
        var cizelge = Cizelge(new EditClip(S(0), S(10)), new EditClip(S(0), S(8)) { Source = 1 });
        Assert.Equal("A 0-10 | X 10-14 | Y 14-18", Yaz(EditChapters.Shifted(cizelge, bolumler, EditChapters.DefaultTitle)));
    }

    [Fact]
    public void HizliKipKaymasiBolumuGeciktirir()
    {
        var cizelge = Cizelge(new EditClip(S(0), S(5)), new EditClip(S(15), S(30)));
        Assert.Equal("A 0-5 | B 6.2-11.2 | C 11.2-21.2", Shifted(cizelge, new[] { 0, 1.2 }));
    }

    [Fact]
    public void EsiktenKisaParcaAtilir()
        => Assert.Equal("B 0.5-10.5", Shifted(Cizelge(new EditClip(S(9.5), S(20)))));

    [Fact]
    public void AdsizBolumNumarasiylaAdlanir()
    {
        var bolumler = Tek(new ChapterMark[] { new(1, 0, 5, null), new(2, 5, 10, " ") });
        var sonuc = EditChapters.Shifted(Cizelge(new EditClip(S(0), S(10))), bolumler, "Bölüm {0}");
        Assert.Equal("Bölüm 1 0-5 | Bölüm 2 5-10", Yaz(sonuc));
    }

    [Fact]
    public void BaslangicZamaniKadarGeriCekilir()
    {
        var bolumler = new ChapterMark[] { new(1, 0, 0.8, "Once"), new(2, 0.8, 3, "Bir"), new(3, 3, 5, "Iki") };
        var sonuc = EditChapters.Relative(bolumler, 1);
        Assert.Equal(new[] { (0.0, 2.0, "Bir"), (2.0, 4.0, "Iki") }, sonuc.Select(b => (b.StartSeconds, b.EndSeconds, b.Title!)));
        Assert.Same(bolumler, EditChapters.Relative(bolumler, 0));
    }

    [Fact]
    public void ParcaSinirlariEtiketleBolumOlur()
    {
        var cizelge = Cizelge(new EditClip(S(0), S(5)) { Label = "Giriş" }, new EditClip(S(15), S(30)));
        Assert.Equal("Giriş 0-5 | Bölüm 2 5-20", Yaz(EditChapters.FromClips(cizelge, "Bölüm {0}")));
        Assert.Empty(EditChapters.FromClips(Cizelge(new EditClip(S(0), S(5))), "Bölüm {0}"));
    }

    [Fact]
    public void AcilistaBolumBaslariIsaretciOlur()
    {
        var bolumler = new ChapterMark[] { new(1, 1, 3, "Giriş"), new(2, 3, 5, null) };
        var isaretler = EditChapters.Markers(bolumler, 1);
        Assert.Equal(new[] { (0L, (string?)"Giriş"), (S(2), (string?)null) }, isaretler.Select(m => (m.Time, m.Name)));
    }

    private static ExportStep Son(ExportPlan plan) => plan.Steps[^1];

    private static string? Metin(ExportStep adim)
        => adim.ExtraLists.FirstOrDefault(l => Path.GetFileName(l.Path) == "chapters.ffmeta")?.Content;

    private static ExportPlan Plan(ExportMode kip, EditTimeline cizelge, MediaInfo bilgi, string cikti = "c.mp4", bool parca = false, string ad = EditChapters.DefaultTitle)
        => EditExport.Build(cizelge, Kaynak(bilgi), kip, cikti, "is", 8 * Gb, _ => true, clipChapters: parca, chapterTitle: ad);

    [Fact]
    public void TamKipteBolumlerFfmetadataGirdisindenEslenir()
    {
        var plan = Plan(ExportMode.Full, Cizelge(new EditClip(S(0), S(5)), new EditClip(S(15), S(30))), Bilgi(Uclu));
        var args = Son(plan).Args;
        var yol = Path.Combine("is", "chapters.ffmeta");
        var i = args.ToList().IndexOf(yol);
        Assert.True(i > 3, string.Join(" ", args));
        Assert.Equal(new[] { "-f", "ffmetadata", "-i" }, args.Skip(i - 3).Take(3));
        Assert.Equal(new[] { "-map_chapters", "1", "c.mp4" }, args.TakeLast(3));
        Assert.Equal(";FFMETADATA1\n[CHAPTER]\nTIMEBASE=1/1000\nSTART=0\nEND=5000\ntitle=A\n"
            + "[CHAPTER]\nTIMEBASE=1/1000\nSTART=5000\nEND=10000\ntitle=B\n"
            + "[CHAPTER]\nTIMEBASE=1/1000\nSTART=10000\nEND=20000\ntitle=C\n", Metin(Son(plan)));
    }

    [Fact]
    public void BolumsuzKaynaktaArgumanDegismez()
    {
        var cizelge = Cizelge(new EditClip(S(0), S(5)), new EditClip(S(15), S(30)));
        var plan = Plan(ExportMode.Full, cizelge, Bilgi());
        var eski = EditExport.Build(cizelge, Bilgi(), Kareler, 0, ExportMode.Full, "c.mp4", "is", 8 * Gb,
            DuzenleyiciAkilliKodekTests.Sinirlar(Kareler), _ => true);
        Assert.Equal(eski.Steps.Select(s => s.Args), plan.Steps.Select(s => s.Args));
        Assert.DoesNotContain("-map_chapters", Son(plan).Args);
        Assert.Null(Metin(Son(plan)));
    }

    [Fact]
    public void ButunBolumlerKesilinceKaynakBolumuDusurulur()
    {
        var plan = Plan(ExportMode.Full, Cizelge(new EditClip(S(40), S(50))), Bilgi(Uclu));
        Assert.Equal(new[] { "-map_chapters", "-1", "c.mp4" }, Son(plan).Args.TakeLast(3));
        Assert.Null(Metin(Son(plan)));
        Assert.DoesNotContain("ffmetadata", Son(plan).Args);
    }

    [Fact]
    public void BolumTasimayanKaptaListeYazilmaz()
    {
        var plan = Plan(ExportMode.Full, Cizelge(new EditClip(S(0), S(5))), Bilgi(Uclu), "c.webm");
        Assert.Null(Metin(Son(plan)));
        Assert.Equal(new[] { "-map_chapters", "-1", "c.webm" }, Son(plan).Args.TakeLast(3));
    }

    [Fact]
    public void ParcaBolumleriKutusuEtiketleriYazar()
    {
        var cizelge = Cizelge(new EditClip(S(0), S(5)) { Label = "Giriş" }, new EditClip(S(15), S(30)));
        var plan = Plan(ExportMode.Full, cizelge, Bilgi(), parca: true, ad: "Bölüm {0}");
        var metin = Metin(Son(plan));
        Assert.NotNull(metin);
        Assert.Contains("START=0\nEND=5000\ntitle=Giriş\n", metin);
        Assert.Contains("START=5000\nEND=20000\ntitle=Bölüm 2\n", metin);
        Assert.Equal(new[] { "-map_chapters", "1", "c.mp4" }, Son(plan).Args.TakeLast(3));
        var kapali = Plan(ExportMode.Full, cizelge, Bilgi(), parca: false);
        Assert.Null(Metin(Son(kapali)));
    }

    [Fact]
    public void AyriParcalarKendiBolumleriniTasir()
    {
        var cizelge = Cizelge(new EditClip(S(0), S(5)), new EditClip(S(15), S(30)));
        var planlar = EditExport.BuildSegments(cizelge, Kaynak(Bilgi(Uclu)), ExportMode.Full,
            new[] { "a.mp4", "b.mp4" }, new[] { "is1", "is2" }, 8 * Gb, _ => true);
        Assert.Equal(2, planlar.Count);
        Assert.Equal(";FFMETADATA1\n[CHAPTER]\nTIMEBASE=1/1000\nSTART=0\nEND=5000\ntitle=A\n", Metin(Son(planlar[0])));
        Assert.Equal(";FFMETADATA1\n[CHAPTER]\nTIMEBASE=1/1000\nSTART=0\nEND=5000\ntitle=B\n"
            + "[CHAPTER]\nTIMEBASE=1/1000\nSTART=5000\nEND=15000\ntitle=C\n", Metin(Son(planlar[1])));
        Assert.Contains(Path.Combine("is2", "chapters.ffmeta"), Son(planlar[1]).Args);
    }

    [Fact]
    public void HizliKipteBolumAnahtarKareKaymasiylaYazilir()
    {
        var bolumler = new ChapterMark[] { new(1, 0, 3.5, "A"), new(2, 3.5, 10, "B") };
        var plan = Plan(ExportMode.Fast, Cizelge(new EditClip(S(1), S(3)), new EditClip(S(4), S(6))), Bilgi(bolumler));
        Assert.Equal(ExportMode.Fast, plan.Effective);
        Assert.Equal(new[] { 1.0, 0 }, plan.KeyframeLeads);
        Assert.Equal(";FFMETADATA1\n[CHAPTER]\nTIMEBASE=1/1000\nSTART=1000\nEND=3000\ntitle=A\n"
            + "[CHAPTER]\nTIMEBASE=1/1000\nSTART=3000\nEND=5000\ntitle=B\n", Metin(Son(plan)));
    }

    [Fact]
    public void AkilliKipteYalnizSonAdimDegisir()
    {
        var cizelge = Cizelge(new EditClip(S(1), S(3)), new EditClip(S(4), S(7)));
        var bolumlu = Plan(ExportMode.Smart, cizelge, Bilgi(Uclu));
        var bolumsuz = Plan(ExportMode.Smart, cizelge, Bilgi());
        Assert.Equal(ExportMode.Smart, bolumlu.Effective);
        Assert.True(bolumlu.Steps.Count > 1);
        Assert.Equal(bolumsuz.Steps.SkipLast(1).Select(s => s.Args), bolumlu.Steps.SkipLast(1).Select(s => s.Args));
        Assert.NotEqual(Son(bolumsuz).Args, Son(bolumlu).Args);
        Assert.Equal(new[] { "-map_chapters", "2" }, Son(bolumlu).Args.SkipLast(1).TakeLast(2));
    }

    [FfmpegFact]
    public async Task CanliTeslimdeBolumlerKesimleKayar()
    {
        string[] dosyalar = { "kaynak.mp4", "bolumler.ffmeta", "kayik.mp4", "eski.mp4", "olcu.txt" };
        KanitKapanisi.Onceki(Kanit, dosyalar);
        var kaynak = Path.Combine(Kanit, "kaynak.mp4");
        var meta = Path.Combine(Kanit, "bolumler.ffmeta");
        File.WriteAllText(meta, RecorderChapters.Metadata(new[]
        {
            new RecorderChapter(TimeSpan.Zero, TimeSpan.FromSeconds(2), "Giriş"),
            new RecorderChapter(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), "Orta Bölüm"),
            new RecorderChapter(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6), "Kapanış")
        }), new UTF8Encoding(false));
        Ffmpeg("-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=6",
            "-f", "lavfi", "-i", "sine=f=440:d=6",
            "-f", "ffmetadata", "-i", meta, "-map", "0", "-map", "1", "-map_chapters", "2",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-threads", "2", "-c:a", "aac", "-shortest", kaynak);
        Assert.Equal(new[] { ("Giriş", 0.0, 2.0), ("Orta Bölüm", 2.0, 4.0), ("Kapanış", 4.0, 6.0) }, Bolumler(kaynak));

        var cizelge = Cizelge(new EditClip(S(0), S(1.5)), new EditClip(S(3), S(6)));
        var cikti = Path.Combine(Kanit, "kayik.mp4");
        var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, cizelge, ExportMode.Full, cikti, 8 * Gb);
        Assert.Equal(ExportMode.Full, plan.Effective);
        var adim = Son(plan);
        await EditExportRunner.RunAsync(plan, new Bos());
        var kayik = Bolumler(cikti);

        var eskiCikti = Path.Combine(Kanit, "eski.mp4");
        Directory.CreateDirectory(plan.WorkDirectory);
        var eskiArgs = Bolumsuz(adim.Args, eskiCikti);
        Ffmpeg(eskiArgs.ToArray());
        var eski = Bolumler(eskiCikti);
        if (Directory.Exists(plan.WorkDirectory)) Directory.Delete(plan.WorkDirectory, true);

        File.WriteAllLines(Path.Combine(Kanit, "olcu.txt"), new[]
        {
            "kayik: " + string.Join(" | ", kayik.Select(b => string.Create(CultureInfo.InvariantCulture, $"{b.Ad} {b.Bas:0.###}-{b.Son:0.###}"))),
            "eski: " + string.Join(" | ", eski.Select(b => string.Create(CultureInfo.InvariantCulture, $"{b.Ad} {b.Bas:0.###}-{b.Son:0.###}")))
        });
        Assert.Equal(new[] { "Giriş", "Orta Bölüm", "Kapanış" }, kayik.Select(b => b.Ad));
        var beklenen = new[] { (0.0, 1.5), (1.5, 2.5), (2.5, 4.5) };
        for (var i = 0; i < 3; i++)
        {
            Assert.InRange(kayik[i].Bas, beklenen[i].Item1 - 0.01, beklenen[i].Item1 + 0.01);
            Assert.InRange(kayik[i].Son, beklenen[i].Item2 - 0.01, beklenen[i].Item2 + 0.01);
        }

        Assert.Equal(new[] { 0.0, 2.0, 4.0 }, eski.Select(b => Math.Round(b.Bas, 2)));
        KanitKapanisi.Kapat(Kanit, dosyalar);
    }

    private static MediaInfo Acilis(string yol, params ChapterMark[] bolumler) => new()
    {
        FilePath = yol,
        FileSizeBytes = 16,
        DurationSeconds = 600,
        Width = 320,
        Height = 240,
        Fps = 30,
        VideoCodec = "h264",
        AudioCodec = "aac",
        TotalBitrateBps = 1,
        Chapters = bolumler
    };

    private static EditorView Gorunum(Func<string, MediaInfo?> bilgi, EditProjectStore? depo)
    {
        var view = new EditorView
        {
            KnownInfo = bilgi,
            Projects = depo,
            PeakReader = (_, _) => Task.FromResult(AudioPeaks.Empty),
            KeyframeReader = (_, _) => Task.FromResult<IReadOnlyList<double>>(Array.Empty<double>()),
        };
        view.Player.EngineFactory = () => new KlipMotoru();
        return view;
    }

    private static void Ac(EditorView view, string yol, double saniye = 10)
    {
        var gorev = view.OpenSourceAsync(yol);
        DenetimSurucu.Pump(view.Player, () => gorev.IsCompleted, saniye);
        Assert.True(gorev.IsCompleted, $"{saniye} sn icinde acilmadi");
        gorev.GetAwaiter().GetResult();
    }

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(Kanit, ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    /// <summary>
    /// DZA-14: kaynak acilinca bolum baslari adlariyla isaretci olur; bolumsuz kaynak isaretci getirmez; isaretcili
    /// kayitli proje varsa projenin isaretcileri kazanir, oneri onlari ezmez.
    /// </summary>
    [Fact]
    public void AcilistaKaynakBolumleriIsaretciOlurProjeKazanir()
    {
        var klasor = Klasor("acilis");
        try
        {
            string Dosya(string ad)
            {
                var yol = Path.Combine(klasor, ad);
                File.WriteAllBytes(yol, new byte[16]);
                return yol;
            }

            var bolumlu = Dosya("bolumlu.mp4");
            var bolumsuz = Dosya("bolumsuz.mp4");
            var projeli = Dosya("projeli.mp4");
            var depo = new EditProjectStore(Path.Combine(klasor, "kayit"));
            Assert.True(depo.Save(EditProject.From(SourceStamp.Of(projeli)!, EditTimeline.FromSource(S(600)),
                markers: new[] { new EditMarker(S(42), "Kendi") })));

            var bolumler = new[] { new ChapterMark(1, 0, 10, "Giriş"), new ChapterMark(2, 10, 20, null) };
            var sonuc = AppHost.Run(() =>
            {
                var view = Gorunum(yol => yol == bolumsuz ? Acilis(yol) : Acilis(yol, bolumler), depo);
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    Ac(view, bolumlu);
                    var ilk = view.TimelineView.MarkerList;
                    Ac(view, bolumsuz);
                    var bos = view.TimelineView.MarkerList;
                    Ac(view, projeli);
                    return (Ilk: ilk, Bos: bos, Proje: view.TimelineView.MarkerList);
                }
                finally
                {
                    view.Player.Close();
                    pencere.Close();
                }
            });

            Assert.Equal(new[] { new EditMarker(0, "Giriş"), new EditMarker(S(10), null) }, sonuc.Ilk);
            Assert.Empty(sonuc.Bos);
            Assert.Equal(new[] { new EditMarker(S(42), "Kendi") }, sonuc.Proje);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    /// <summary>
    /// Kutu teslimin kayit anahtarini degistirir (ayni duzenleme kutu degisince yeniden yazilir) ve degisimi
    /// bildirir; varsayilan kapali.
    /// </summary>
    [Fact]
    public void KutuKayitAnahtariniDegistirirVeBildirir()
    {
        var klasor = Klasor("kutu");
        try
        {
            var dosya = Path.Combine(klasor, "kaynak.mp4");
            File.WriteAllBytes(dosya, new byte[16]);
            var sonuc = AppHost.Run(() =>
            {
                var view = Gorunum(yol => Acilis(yol), null);
                var bildirilen = new List<bool>();
                view.ClipChaptersChanged = bildirilen.Add;
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    Ac(view, dosya);
                    var bas = view.ClipChapters;
                    var kapali = Anahtar(view);
                    view.ClipChapters = true;
                    var acik = Anahtar(view);
                    view.ClipChapters = false;
                    return (Bas: bas, Kapali: kapali, Acik: acik, Geri: Anahtar(view), Bildirilen: bildirilen.ToArray());
                }
                finally
                {
                    view.Player.Close();
                    pencere.Close();
                }
            });

            Assert.False(sonuc.Bas);
            Assert.NotEqual(sonuc.Kapali, sonuc.Acik);
            Assert.Contains("|chap", sonuc.Acik, StringComparison.Ordinal);
            Assert.Equal(sonuc.Kapali, sonuc.Geri);
            Assert.Equal(new[] { true, false }, sonuc.Bildirilen);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    private static string Anahtar(EditorView view)
    {
        var yontem = typeof(EditorView).GetMethod("SaveKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)yontem.Invoke(view, new object[] { view.Model! })!;
    }

    /// <summary>
    /// Secim ana pencerenin ayarina <c>editorClipChapters</c> olarak yazilir, yeni pencerede kutuya geri iner;
    /// anahtarsiz eski dosya ve sifirlama kutuyu kapatir.
    /// </summary>
    [Fact]
    public void SecimAyaraYaziliyorGeriGeliyor()
    {
        var klasor = Klasor("ayar");
        var dosya = Path.Combine(klasor, "settings.json");
        try
        {
            var (metin, geri, sifir) = AppHost.Run(() =>
            {
                var ilk = new MainWindow { SettingsPathOverride = dosya };
                try { ilk.EditorPaneForTest.ClipChapters = true; }
                finally { ilk.Close(); }
                var yazilan = File.ReadAllText(dosya);

                var ikinci = new MainWindow { SettingsPathOverride = dosya };
                try
                {
                    ikinci.RestoreAppSettingsForTest(AppSettings.Load(dosya));
                    var pane = ikinci.EditorPaneForTest;
                    var gelen = (Kutu: pane.ClipChapters, Ayar: ikinci.CaptureAppSettingsForTest().EditorClipChapters);
                    ikinci.ConfirmResetSettingsForTest();
                    return (yazilan, gelen, pane.ClipChapters);
                }
                finally { ikinci.Close(); }
            });

            using var belge = JsonDocument.Parse(metin);
            Assert.True(belge.RootElement.GetProperty("editorClipChapters").GetBoolean());
            Assert.Equal((true, true), geri);
            Assert.False(sifir);

            File.WriteAllText(dosya, "{\"editorDropMetadata\":true}");
            Assert.False(AppSettings.Load(dosya).EditorClipChapters);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    /// <summary>Kutunun iki metni 42 dilin hepsinde var ve bos degil.</summary>
    [Fact]
    public void KutuMetinleriHerDildeVar()
    {
        var diller = Locales.Languages;
        Assert.Equal(42, diller.Count);
        foreach (var dil in diller)
        {
            var sozluk = Locales.Domain(dil, "editor");
            foreach (var anahtar in new[] { "editor.export.chapters", "editor.export.chapters.tip" })
                Assert.False(string.IsNullOrWhiteSpace(sozluk.GetValueOrDefault(anahtar)), dil + " " + anahtar);
        }
    }

    /// <summary>
    /// Canli arayuz kolu: 6 sn kaynak 3. saniyede bolunur, kutu acikken Tam teslim iki bolum yazar (sinir 3 sn),
    /// kutu kapali ve kaynak bolumsuzken son adimda <c>-map_chapters</c> yoktur ve cikti bolumsuzdur.
    /// </summary>
    [FfmpegFact]
    public void CanliArayuzdeKutuParcalariBolumYazar()
    {
        var klasor = Klasor("arayuz");
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            Ffmpeg("-hide_banner", "-y", "-threads", "2", "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=6",
                "-c:v", "libx264", "-preset", "ultrafast", "-g", "30", "-pix_fmt", "yuv420p", kaynak);

            var s = AppHost.Run(() =>
            {
                var view = new EditorView { RevealFolder = _ => { }, Projects = null };
                view.Player.EngineFactory = () => new YolMotoru(sure: 6);
                var pencere = new Window { Width = 1100, Height = 700, Content = view };
                pencere.Show();
                try
                {
                    Ac(view, kaynak);
                    view.TimelineView.Playhead = S(3);
                    Assert.True(view.Split());
                    view.SelectedExportMode = ExportMode.Full;

                    (bool Ok, string? Yol, string[] Args) Kaydet(bool kutu)
                    {
                        view.ClipChapters = kutu;
                        var gorev = view.SaveAsync();
                        DenetimSurucu.Pump(view.Player, () => gorev.IsCompleted, 90);
                        Assert.True(gorev.IsCompleted, view.ExportStatusText);
                        return (gorev.Result, view.ExportedPath, view.LastPlan!.Steps[^1].Args.ToArray());
                    }

                    return (Acik: Kaydet(true), Kapali: Kaydet(false), Durum: view.ExportStatusText);
                }
                finally
                {
                    view.Player.Close();
                    pencere.Close();
                }
            });

            Assert.True(s.Acik.Ok, s.Durum);
            Assert.True(s.Kapali.Ok, s.Durum);
            Assert.Contains("-map_chapters", s.Acik.Args);
            Assert.DoesNotContain("-map_chapters", s.Kapali.Args);

            var acik = Bolumler(s.Acik.Yol!);
            Assert.Equal(2, acik.Count);
            Assert.InRange(acik[0].Son, 2.99, 3.01);
            Assert.InRange(acik[1].Bas, 2.99, 3.01);
            Assert.Empty(Bolumler(s.Kapali.Yol!));
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    private static List<string> Bolumsuz(IReadOnlyList<string> args, string cikti)
    {
        var liste = args.ToList();
        var m = liste.IndexOf("-map_chapters");
        liste.RemoveRange(m, 2);
        var f = liste.IndexOf("ffmetadata");
        liste.RemoveRange(f - 1, 4);
        liste[^1] = cikti;
        return liste;
    }

    private static List<(string Ad, double Bas, double Son)> Bolumler(string dosya)
    {
        var psi = new ProcessStartInfo("ffprobe")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        foreach (var a in new[] { "-v", "error", "-show_chapters", "-of", "json", dosya }) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var metin = surec.StandardOutput.ReadToEnd();
        Assert.True(surec.WaitForExit(15000));
        using var belge = JsonDocument.Parse(metin);
        return belge.RootElement.GetProperty("chapters").EnumerateArray()
            .Select(c => (
                c.TryGetProperty("tags", out var t) && t.TryGetProperty("title", out var ad) ? ad.GetString() ?? "" : "",
                double.Parse(c.GetProperty("start_time").GetString()!, CultureInfo.InvariantCulture),
                double.Parse(c.GetProperty("end_time").GetString()!, CultureInfo.InvariantCulture)))
            .ToList();
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

        var metin = hata.GetAwaiter().GetResult();
        Assert.True(surec.ExitCode == 0, metin);
    }

    private sealed class Bos : IProgress<EncodeProgress>
    {
        public void Report(EncodeProgress value)
        {
        }
    }
}
