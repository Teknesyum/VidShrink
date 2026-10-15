using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// DZB-62 altyazi alisverisi (<c>Core/Editing/SubtitleFile</c>, <c>EditorView.Metin</c>): SRT, VTT ve ASS ffmpeg'siz
/// okunur (zaman damgasi, cok satirli metin, BOM, CRLF, bicim etiketleri), metin izi SRT ve ASS olarak geri yazilir ve
/// geri okununca ayni satirlar doner. Arayuz kolu sahte <c>KlipMotoru</c> ile. Kanit
/// <c>.calisma/worktree-agent-aa4e76d89d1f90592/</c>, test siler.
/// </summary>
public sealed class AltyaziAktarimTests
{
    private const string Dal = "worktree-agent-aa4e76d89d1f90592";

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static long Ms(long ms) => ms * AssWriter.TicksPerMillisecond;

    private static IEnumerable<SubtitleCue> Santisaniye(IEnumerable<SubtitleCue> satirlar)
        => satirlar.Select(c => c with { Start = Yuvarla(c.Start), End = Yuvarla(c.End) });

    private static long Yuvarla(long tick) => (tick + AssWriter.TicksPerCentisecond / 2) / AssWriter.TicksPerCentisecond * AssWriter.TicksPerCentisecond;

    private static string Kanit => Path.Combine(GirdiKanit.Root, ".calisma", Dal);

    private const string Srt = "1\n00:00:01,500 --> 00:00:03,250\nİlk satır\nikinci satır\n\n2\n00:01:02,007 --> 01:00:00,000\n<i>Eğik</i> ve <b>kalın</b>\n";

    [Theory]
    [InlineData("a.srt", SubtitleFormat.Srt)]
    [InlineData("a.VTT", SubtitleFormat.Vtt)]
    [InlineData("a.ass", SubtitleFormat.Ass)]
    [InlineData("a.ssa", SubtitleFormat.Ass)]
    [InlineData("a.txt", null)]
    public void BicimUzantidanGelir(string yol, SubtitleFormat? beklenen) => Assert.Equal(beklenen, SubtitleFile.FormatOf(yol));

    [Fact]
    public void SrtZamanVeCokSatirliMetinOkunur()
    {
        var okunan = SubtitleFile.Read(SubtitleFormat.Srt, Srt);
        Assert.NotNull(okunan);
        Assert.Equal(new[]
        {
            new SubtitleCue(Ms(1500), Ms(3250), "İlk satır\nikinci satır"),
            new SubtitleCue(Ms(62007), Ms(3600000), "Eğik ve kalın")
        }, okunan);
    }

    [Fact]
    public void BomVeCrlfAyniSatirlariVerir()
    {
        var duz = SubtitleFile.Read(SubtitleFormat.Srt, Srt);
        var windows = SubtitleFile.Read(SubtitleFormat.Srt, "\uFEFF" + Srt.Replace("\n", "\r\n", StringComparison.Ordinal));
        Assert.Equal(duz, windows);
        Assert.DoesNotContain(windows!, c => c.Text.Contains('\r', StringComparison.Ordinal) || c.Text.Contains('\uFEFF', StringComparison.Ordinal));
        var numarasiz = SubtitleFile.Read(SubtitleFormat.Vtt, "\uFEFF00:00:01.500 --> 00:00:03.250\r\n\u0130lk sat\u0131r\r\nikinci sat\u0131r\r\n");
        Assert.Equal(new[] { duz![0] }, numarasiz!);
        Assert.Equal(duz, SubtitleFile.Read(SubtitleFormat.Srt, Srt.Replace('\n', '\r')));

        var yol = Path.Combine(Kanit, "bom");
        Directory.CreateDirectory(yol);
        var dosya = Path.Combine(yol, "bom.srt");
        File.WriteAllText(dosya, Srt.Replace("\n", "\r\n", StringComparison.Ordinal), new UTF8Encoding(true));
        Assert.Equal(0xEF, File.ReadAllBytes(dosya)[0]);
        Assert.Equal(duz, SubtitleFile.Read(SubtitleFormat.Srt, File.ReadAllText(dosya)));
        KanitKapanisi.Kapat(Kanit, "bom");
    }

    [Fact]
    public void VttBaslikNotAyarVeEtiketleriAyiklanir()
    {
        const string vtt = "WEBVTT - deneme\n\nNOTE bu bir not\nikinci not satırı\n\nSTYLE\n::cue { color: red }\n\nkimlik-1\n00:05.250 --> 00:07.000 align:start position:10%\n<v Ayşe>Merhaba</v> &amp; <c.sari>selam</c>\n&lt;3 <00:06.000>son\n\n01:00:00.000 --> 01:00:01.000\nİkinci\n";
        var okunan = SubtitleFile.Read(SubtitleFormat.Vtt, vtt);
        Assert.Equal(new[]
        {
            new SubtitleCue(Ms(5250), Ms(7000), "Merhaba & selam\n<3 son"),
            new SubtitleCue(Ms(3600000), Ms(3601000), "İkinci")
        }, okunan);
    }

    [Fact]
    public void AssOlaylariAlanSirasiylaOkunurEtiketlerAtilir()
    {
        const string ass = "[Script Info]\nScriptType: v4.00+\n\n[V4+ Styles]\nFormat: Name, Fontname\nStyle: Default,Arial\n\n[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n"
                           + "Comment: 0,0:00:00.00,0:00:01.00,Default,,0,0,0,,yorum\n"
                           + "Dialogue: 0,0:00:01.50,0:00:02.75,Default,,0,0,0,,{\\an8\\b1}Virgül, ve\\Nikinci\\nsatır\n"
                           + "Dialogue: 0,1:02:03.04,1:02:05.00,Default,,0,0,0,,a\\{b\\} c\\\u2060N\n";
        var okunan = SubtitleFile.Read(SubtitleFormat.Ass, ass);
        Assert.Equal(new[]
        {
            new SubtitleCue(Ms(1500), Ms(2750), "Virgül, ve\nikinci\nsatır"),
            new SubtitleCue(Ms(3723040), Ms(3725000), "a{b} c\\N")
        }, okunan);
    }

    [Fact]
    public void AssYazicisininBoldugurMetinTekSatiraBirlesir()
    {
        var metin = new TextLayer("Kayan {yazı}\nalt", S(2), S(6))
        {
            MotionIn = TextMotion.Typewriter, MotionInLength = S(1),
            Keyframes = new[] { new TextKeyframe(0, 0.2, 0.5), new TextKeyframe(S(2), 0.8, 0.5) }
        };
        var belge = AssWriter.Write(new[] { metin, new TextLayer("İkinci", S(6), S(7)) }, 1920, 1080);
        Assert.True(belge.Split('\n').Count(s => s.StartsWith("Dialogue: 0,", StringComparison.Ordinal)) > 2);

        var okunan = SubtitleFile.Read(SubtitleFormat.Ass, belge);
        Assert.Equal(new[] { new SubtitleCue(S(2), S(6), "Kayan {yazı}\nalt"), new SubtitleCue(S(6), S(7), "İkinci") }, okunan);
    }

    [Fact]
    public void OkunamayanDosyaBosDoner()
    {
        Assert.Null(SubtitleFile.Read(SubtitleFormat.Srt, "bu bir altyazı değil\n"));
        Assert.Null(SubtitleFile.Read(SubtitleFormat.Vtt, "WEBVTT\n\n00:01.000 --> 00:00.500\nters\n"));
        Assert.Null(SubtitleFile.Read(SubtitleFormat.Ass, "[Events]\nFormat: Start, End, Text\n"));
    }

    [Fact]
    public void SrtGidisDonusuAyniSatirlariVerir()
    {
        var ilk = SubtitleFile.Read(SubtitleFormat.Srt, Srt)!;
        var katmanlar = SubtitleFile.ToLayers(ilk, S(7200));
        Assert.All(katmanlar, k => Assert.Equal(new[] { new TextKeyframe(0, 0.5, SubtitleFile.LineY) }, k.Keyframes));

        var yazilan = SubtitleFile.WriteSrt(katmanlar);
        Assert.StartsWith("1\r\n00:00:01,500 --> 00:00:03,250\r\nİlk satır\r\nikinci satır\r\n\r\n2\r\n", yazilan, StringComparison.Ordinal);
        Assert.Equal(ilk, SubtitleFile.Read(SubtitleFormat.Srt, yazilan));
    }

    [Fact]
    public void AssGidisDonusuAyniSatirlariVerir()
    {
        var ilk = SubtitleFile.Read(SubtitleFormat.Srt, Srt.Replace("Eğik", "Eğik \\ {ters}", StringComparison.Ordinal))!;
        var katmanlar = SubtitleFile.ToLayers(ilk, S(7200));
        var belge = Encoding.UTF8.GetString(SubtitleFile.Bytes(SubtitleFormat.Ass, katmanlar, 1280, 720));
        Assert.Equal(Santisaniye(ilk), SubtitleFile.Read(SubtitleFormat.Ass, belge)!);
        Assert.Throws<ArgumentException>(() => SubtitleFile.Bytes(SubtitleFormat.Vtt, katmanlar, 1280, 720));
    }

    [Fact]
    public void KatmanlarCizelgeyeSigarBosMetinAtlanir()
    {
        var katmanlar = SubtitleFile.ToLayers(new[]
        {
            new SubtitleCue(S(1), S(2), "  \n "),
            new SubtitleCue(S(8), S(12), "taşan"),
            new SubtitleCue(S(10), S(11), "dışarıda"),
            new SubtitleCue(S(3), S(4), "içeride")
        }, S(10));
        Assert.Equal(new[] { ("taşan", S(8), S(10)), ("içeride", S(3), S(4)) }, katmanlar.Select(k => (k.Text, k.Start, k.End)));
    }

    [Fact]
    public void DuzenleyiciAltyaziyiAlirVeGeriYazar()
    {
        var klasor = Path.Combine(Kanit, "altyazi");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        var girdi = Path.Combine(klasor, "girdi.srt");
        File.WriteAllText(girdi, Srt.Replace("01:00:00,000", "00:01:05,000", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal), new UTF8Encoding(true));
        AppHost.Run(() =>
        {
            using var sahne = new Sahne(klasor);
            var view = sahne.View;
            Assert.False(view.ImportSubtitlesFrom(girdi));
            Assert.False(view.ExportSubtitlesTo(Path.Combine(klasor, "bos.srt")));

            sahne.Ac(dosya);
            Assert.False(view.ExportSubtitlesTo(Path.Combine(klasor, "bos.srt")));
            Assert.True(view.AddText());
            var onceki = view.Model!.Texts.ToArray();

            Assert.True(view.ImportSubtitlesFrom(girdi));
            sahne.Bekle();
            Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.subtitle.imported"), 2), view.ExportStatusText);
            Assert.Equal(3, view.Model!.Texts.Count);
            Assert.Equal(1, view.TimelineView.SelectedText);

            var cikti = Path.Combine(klasor, "cikti.srt");
            Assert.True(view.ExportSubtitlesTo(cikti));
            Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.subtitle.exported"), cikti), view.ExportStatusText);
            var beklenen = new[] { new SubtitleCue(onceki[0].Start, onceki[0].End, onceki[0].Text) }.Concat(SubtitleFile.Read(SubtitleFormat.Srt, File.ReadAllText(girdi))!);
            Assert.Equal(beklenen, SubtitleFile.Read(SubtitleFormat.Srt, File.ReadAllText(cikti)));

            var ass = Path.Combine(klasor, "cikti.ass");
            Assert.True(view.ExportSubtitlesTo(ass));
            Assert.Contains("PlayResX: 320", File.ReadAllText(ass), StringComparison.Ordinal);
            Assert.Equal(Santisaniye(beklenen), SubtitleFile.Read(SubtitleFormat.Ass, File.ReadAllText(ass))!);
            Assert.False(view.ExportSubtitlesTo(Path.Combine(klasor, "cikti.vtt")));
            Assert.False(view.ExportSubtitlesTo(Path.Combine(klasor, "yok", "cikti.srt")));
            Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.subtitle.failed"), Path.Combine(klasor, "yok", "cikti.srt")), view.ExportStatusText);

            Assert.True(view.Undo());
            Assert.Equal(onceki, view.Model!.Texts);

            var bozuk = Path.Combine(klasor, "bozuk.vtt");
            File.WriteAllText(bozuk, "WEBVTT\n\nbos\n");
            Assert.False(view.ImportSubtitlesFrom(bozuk));
            Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.subtitle.unreadable"), bozuk), view.ExportStatusText);
            Assert.Equal(onceki, view.Model!.Texts);
        });
        KanitKapanisi.Kapat(Kanit, "altyazi");
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
}
