using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kesim listesi ve kurgu aktarimi (<c>Core/Editing/CutList*</c>, <c>FcpXml</c>, <c>EditorView.Proje</c>): CSV, EDL ve
/// CUE gidis-donusu ya da okunusu, YouTube bolum metni, Final Cut XML'inin (xmeml) kare ve dosya baglari. XML'in
/// Premiere Pro ya da DaVinci Resolve'da acildigi olculmedi; sahip gozle dener. Arayuz kolu sahte <c>KlipMotoru</c> ile:
/// disa yazilan liste geri okununca cizelge ayni kesimlere doner. Kanit
/// <c>.calisma/worktree-agent-a6ab701c8c66473f9/</c>, test siler.
/// </summary>
public sealed class KesimListesiAktarimTests
{
    private const string Dal = "worktree-agent-a6ab701c8c66473f9";

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string Kanit => Path.Combine(GirdiKanit.Root, ".calisma", Dal);

    private static EditTimeline Cizelge() => new(new[]
    {
        new EditClip(S(10), S(40)) { Label = "Giriş, \"ilk\" söz" },
        new EditClip(S(100), S(160), 1.5m, true),
        new EditClip(S(300), S(330)) { Label = "Son" },
    }, S(600));

    private static CutListContext Baglam(double fps = 25) => new(new[] { @"C:\videolar\çekim 1.mp4" }, fps) { Width = 1920, Height = 1080 };

    [Theory]
    [InlineData("a.csv", CutListFormat.Csv)]
    [InlineData("a.EDL", CutListFormat.Edl)]
    [InlineData("a.txt", CutListFormat.YouTube)]
    [InlineData("a.xml", CutListFormat.FcpXml)]
    [InlineData("a.cue", CutListFormat.Cue)]
    [InlineData("a.mp4", null)]
    public void BicimUzantidanGelir(string yol, CutListFormat? beklenen) => Assert.Equal(beklenen, CutList.FormatOf(yol));

    [Fact]
    public void CsvGidisDonusuAraligiVeEtiketiKorur()
    {
        var model = Cizelge();
        var metin = CutList.Write(CutListFormat.Csv, model, Baglam());
        Assert.StartsWith("10,40,\"Giriş, \"\"ilk\"\" söz\"", metin, StringComparison.Ordinal);

        var okunan = CutList.Read(CutListFormat.Csv, metin, 25, S(600));
        Assert.NotNull(okunan);
        Assert.Equal(model.Clips.Select(c => (c.SourceStart, c.SourceEnd, c.Label)), okunan!.Select(e => (e.Start, e.End, e.Label)));
    }

    [Fact]
    public void CsvBaslikBosSonVeSaatBiciminiOkur()
    {
        var metin = "start,end,name\n0.5,00:01:02.250,A\n\n70,,Kalan\n";
        var okunan = CutList.Read(CutListFormat.Csv, metin, 25, S(600));
        Assert.Equal(new (long, long, string?)[] { (S(0.5), S(62.25), "A"), (S(70), S(600), "Kalan") }, okunan!.Select(e => (e.Start, e.End, e.Label)));

        Assert.Null(CutList.Read(CutListFormat.Csv, "0,10\nbozuk,satir\n", 25, S(600)));
        Assert.Null(CutList.Read(CutListFormat.Csv, "700,800\n", 25, S(600)));
        Assert.Equal(new[] { (S(590), S(600)) }, CutList.Read(CutListFormat.Csv, "590,900\n", 25, S(600))!.Select(e => (e.Start, e.End)));
    }

    [Fact]
    public void EdlGidisDonusuHiziYonuVeEtiketiKorur()
    {
        var model = Cizelge();
        var metin = CutList.Write(CutListFormat.Edl, model, Baglam());
        var satirlar = metin.Split("\r\n");
        Assert.Contains("FCM: NON-DROP FRAME", satirlar);
        Assert.Contains("001  AX       AA/V  C        00:00:10:00 00:00:40:00 00:00:00:00 00:00:30:00", satirlar);
        Assert.Contains("002  AX       AA/V  C        00:01:40:00 00:02:40:00 00:00:30:00 00:01:10:00", satirlar);
        Assert.Contains("M2   AX       -37.5    00:01:40:00", satirlar);
        Assert.Contains("* FROM CLIP NAME: çekim 1.mp4", satirlar);
        Assert.Contains("* COMMENT: Son", satirlar);

        var okunan = CutList.Read(CutListFormat.Edl, metin, 25, S(600));
        Assert.NotNull(okunan);
        Assert.Equal(model.Clips.Select(c => (c.SourceStart, c.SourceEnd, c.Label, c.Speed, c.Reversed)),
            okunan!.Select(e => (e.Start, e.End, e.Label, e.Speed, e.Reversed)));
    }

    [Fact]
    public void EdlDusenKareVeYalnizSesOlayiniAyirir()
    {
        var model = new EditTimeline(new[] { new EditClip(S(60.06), S(70)) }, S(600));
        var metin = CutList.Write(CutListFormat.Edl, model, Baglam(30000.0 / 1001));
        Assert.Contains("FCM: DROP FRAME", metin, StringComparison.Ordinal);
        Assert.Contains("00:01:00;02", metin, StringComparison.Ordinal);
        var okunan = CutList.Read(CutListFormat.Edl, metin, 30000.0 / 1001, S(600));
        Assert.True(Math.Abs(okunan![0].Start - S(60.06)) < S(1.0 / 29.97));

        var karisik = "TITLE: x\n001  AX  V  C  00:00:01:00 00:00:02:00 00:00:00:00 00:00:01:00\n002  AX  A  C  00:00:05:00 00:00:06:00 00:00:00:00 00:00:01:00\n003  AX  V  D 010 00:00:03:00 00:00:04:00 00:00:01:00 00:00:02:00\n";
        Assert.Equal(new[] { (S(1), S(2)), (S(3), S(4)) }, CutList.Read(CutListFormat.Edl, karisik, 25, S(600))!.Select(e => (e.Start, e.End)));
        Assert.Null(CutList.Read(CutListFormat.Edl, "TITLE: bos\n", 25, S(600)));
    }

    [Fact]
    public void CueIzleriBirSonrakineKadarParcaOlur()
    {
        var metin = "PERFORMER \"X\"\nTITLE \"Albüm\"\nFILE \"a.wav\" WAVE\n  TRACK 01 AUDIO\n    TITLE \"Bir\"\n    INDEX 01 00:00:00\n  TRACK 02 AUDIO\n    TITLE \"İki\"\n    INDEX 00 01:29:00\n    INDEX 01 01:30:37\n";
        var okunan = CutList.Read(CutListFormat.Cue, metin, 25, S(600));
        Assert.Equal(new (long, long, string?)[] { (0L, S(90) + 37 * 3200, "Bir"), (S(90) + 37 * 3200, S(600), "İki") },
            okunan!.Select(e => (e.Start, e.End, e.Label)));
        Assert.Null(CutList.Read(CutListFormat.Cue, "TITLE \"yalniz baslik\"\n", 25, S(600)));
    }

    [Fact]
    public void YoutubeBolumleriTeslimZamaniniVerir()
    {
        var model = Cizelge();
        var metin = CutList.Write(CutListFormat.YouTube, model, Baglam() with { ChapterTitle = "Bölüm {0}" });
        Assert.Equal("00:00 Giriş, \"ilk\" söz\n00:30 Bölüm 2\n01:10 Son\n", metin);

        Assert.True(model.SetTransition(new[] { 2 }, new ClipTransition(TransitionKind.Dissolve, S(2))));
        Assert.Contains("01:08 Son", CutList.Write(CutListFormat.YouTube, model, Baglam()), StringComparison.Ordinal);

        var uzun = new EditTimeline(new[] { new EditClip(0, S(3700)), new EditClip(S(3700), S(3800)) }, S(4000));
        Assert.Equal("0:00:00 Chapter 1\n1:01:40 Chapter 2\n", CutList.Write(CutListFormat.YouTube, uzun, Baglam()));
    }

    [Fact]
    public void FcpXmlKareleriDosyaBaginiVeHiziYazar()
    {
        var model = new EditTimeline(new[]
        {
            new EditClip(S(10), S(40)) { Label = "Giriş" },
            new EditClip(S(100), S(160), 1.5m, true),
            new EditClip(0, S(5)) { Source = 1 },
        }, S(600), extraSources: new[] { S(20) });
        var baglam = new CutListContext(new[] { @"C:\videolar\çekim 1.mp4", "/home/x/b&c.mov" }, 30000.0 / 1001) { Width = 1280, Height = 720, Title = "Kurgu" };
        var xml = CutList.Write(CutListFormat.FcpXml, model, baglam);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml, StringComparison.Ordinal);
        var belge = XDocument.Parse(xml);

        var sira = belge.Root!.Element("sequence")!;
        Assert.Equal("Kurgu", sira.Element("name")!.Value);
        Assert.Equal("30", sira.Element("rate")!.Element("timebase")!.Value);
        Assert.Equal("TRUE", sira.Element("rate")!.Element("ntsc")!.Value);
        var goruntu = sira.Element("media")!.Element("video")!;
        Assert.Equal("1280", goruntu.Element("format")!.Descendants("width").Single().Value);
        var klipler = goruntu.Element("track")!.Elements("clipitem").ToArray();
        Assert.Equal(3, klipler.Length);

        int Sayi(XElement e, string ad) => int.Parse(e.Element(ad)!.Value);
        Assert.Equal(new[] { (0, 899, 300, 1199), (899, 2098, 2997, 4795), (2098, 2248, 0, 150) },
            klipler.Select(k => (Sayi(k, "start"), Sayi(k, "end"), Sayi(k, "in"), Sayi(k, "out"))));
        Assert.Equal(2248, Sayi(sira, "duration"));
        Assert.Equal("Giriş", klipler[0].Element("name")!.Value);

        var dosyalar = belge.Descendants("file").ToArray();
        Assert.Equal("file://localhost/C:/videolar/%C3%A7ekim%201.mp4", dosyalar.First(f => (string?)f.Attribute("id") == "file-1" && f.HasElements).Element("pathurl")!.Value);
        Assert.Equal("file://localhost/home/x/b%26c.mov", dosyalar.First(f => (string?)f.Attribute("id") == "file-2" && f.HasElements).Element("pathurl")!.Value);
        Assert.Equal(2, dosyalar.Count(f => f.HasElements));
        Assert.Equal("17982", dosyalar.First(f => (string?)f.Attribute("id") == "file-1" && f.HasElements).Element("duration")!.Value);

        var hiz = klipler[1].Descendants("parameter").ToDictionary(p => p.Element("parameterid")!.Value, p => p.Element("value")!.Value);
        Assert.Equal("150", hiz["speed"]);
        Assert.Equal("TRUE", hiz["reverse"]);
        Assert.Empty(klipler[0].Descendants("filter"));

        var ses = sira.Element("media")!.Element("audio")!.Element("track")!.Elements("clipitem").ToArray();
        Assert.Equal(klipler.Select(k => Sayi(k, "start")), ses.Select(k => Sayi(k, "start")));
        Assert.DoesNotContain(XDocument.Parse(CutList.Write(CutListFormat.FcpXml, model, baglam with { HasAudio = false })).Descendants("audio"), a => a.Parent?.Name == "media" && a.Parent.Parent?.Name == "sequence");
    }

    [Fact]
    public void CueYazilmazXmlOkunmaz()
    {
        Assert.Throws<ArgumentException>(() => CutList.Write(CutListFormat.Cue, Cizelge(), Baglam()));
        Assert.Null(CutList.Read(CutListFormat.FcpXml, "<xmeml/>", 25, S(600)));
        Assert.Null(CutList.Read(CutListFormat.YouTube, "00:00 a", 25, S(600)));
    }

    private sealed class Sahne : IDisposable
    {
        private readonly Window _pencere;

        internal Sahne(string klasor)
        {
            View = new EditorView
            {
                KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 25, VideoCodec = "h264", TotalBitrateBps = 1 },
                Projects = null,
            };
            View.Player.EngineFactory = () => new KlipMotoru();
            View.OverlayRoot = Path.Combine(klasor, "katman");
            _pencere = new Window { Width = 900, Height = 600, Content = View };
            _pencere.Show();
        }

        internal EditorView View { get; }

        internal void Ac(string dosya)
        {
            var acilis = View.OpenSourceAsync(dosya);
            DenetimSurucu.Pump(View.Player, () => acilis.IsCompleted, 10);
            acilis.GetAwaiter().GetResult();
            Assert.NotNull(View.Model);
        }

        internal void Bekle()
        {
            var saat = Stopwatch.StartNew();
            while (saat.Elapsed.TotalMilliseconds < 200)
            {
                using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
                Dispatcher.UIThread.MainLoop(dilim.Token);
                View.Player.RenderLatest();
            }
        }

        public void Dispose() => _pencere.Close();
    }

    [Fact]
    public void DuzenleyiciListeyiYazarVeGeriOkur()
    {
        var klasor = Path.Combine(Kanit, "kesim-listesi");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        AppHost.Run(() =>
        {
            using var sahne = new Sahne(klasor);
            var view = sahne.View;
            Assert.False(view.ExportCutListTo(Path.Combine(klasor, "bos.csv")));

            sahne.Ac(dosya);
            view.TimelineView.Playhead = S(100);
            Assert.True(view.Split());
            view.TimelineView.Playhead = S(400);
            Assert.True(view.Split());
            var beklenen = view.Model!.Clips.Select(c => (c.SourceStart, c.SourceEnd)).ToArray();
            Assert.Equal(3, beklenen.Length);

            foreach (var uzanti in new[] { ".csv", ".edl", ".txt", ".xml" })
            {
                var yol = Path.Combine(klasor, "liste" + uzanti);
                Assert.True(view.ExportCutListTo(yol));
                Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.cutlist.exported"), yol), view.ExportStatusText);
            }

            Assert.Contains("01:40 " + string.Format(Strings.Culture, Strings.Get("mediainfo.section.chapter"), 2), File.ReadAllText(Path.Combine(klasor, "liste.txt")));
            Assert.False(view.ExportCutListTo(Path.Combine(klasor, "liste.mp4")));
            Assert.False(view.ExportCutListTo(Path.Combine(klasor, "yok", "liste.csv")));

            Assert.True(view.Undo());
            Assert.True(view.Undo());
            Assert.Single(view.Model!.Clips);
            foreach (var uzanti in new[] { ".csv", ".edl" })
            {
                Assert.True(view.ImportCutListFrom(Path.Combine(klasor, "liste" + uzanti)));
                sahne.Bekle();
                Assert.Equal(beklenen, view.Model!.Clips.Select(c => (c.SourceStart, c.SourceEnd)));
                Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.cutlist.imported"), 3), view.ExportStatusText);
                Assert.True(view.Undo());
                Assert.Single(view.Model!.Clips);
            }

            var bozuk = Path.Combine(klasor, "bozuk.csv");
            File.WriteAllText(bozuk, "bu,bir,liste,degil\nx,y\n");
            Assert.False(view.ImportCutListFrom(bozuk));
            Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.cutlist.unreadable"), bozuk), view.ExportStatusText);
            Assert.Single(view.Model!.Clips);
        });
        KanitKapanisi.Kapat(Kanit, "kesim-listesi");
    }
}
