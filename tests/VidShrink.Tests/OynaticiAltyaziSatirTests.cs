using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class SatirKanit
{
    internal static string Srt => string.Concat(
        "1\r\n00:00:00,500 --> 00:00:01,500\r\nbir\r\n\r\n",
        "2\r\n00:00:02,500 --> 00:00:03,500\r\niki\r\n\r\n",
        "3\r\n00:00:04,500 --> 00:00:05,500\r\nuc\r\n\r\n");

    internal static string Klasor(string ad)
    {
        var yol = Path.Combine(ParcaKanit.Folder, ad);
        if (Directory.Exists(yol)) Directory.Delete(yol, true);
        Directory.CreateDirectory(yol);
        return yol;
    }

    internal static async Task<double> KonumBekle(IPlaybackEngine engine, Func<double, bool> tamam, double seconds = 5)
    {
        var saat = Stopwatch.StartNew();
        var konum = engine.PositionSeconds;
        while (saat.Elapsed.TotalSeconds < seconds)
        {
            konum = engine.PositionSeconds;
            if (tamam(konum)) return konum;
            await Task.Delay(20);
        }

        return konum;
    }
}

public sealed class OynaticiAltyaziSatirTests
{
    [Fact]
    public void AltKlasordekiAltyazilarBulunurIdxEsiSubAtlanir()
    {
        var kok = Path.Combine(TestPaths.OutputRoot, "altyazi-alt-klasor");
        if (Directory.Exists(kok)) Directory.Delete(kok, true);
        Directory.CreateDirectory(Path.Combine(kok, "Subs"));
        Directory.CreateDirectory(Path.Combine(kok, "subtitles", "film"));
        Directory.CreateDirectory(Path.Combine(kok, "Extras"));
        foreach (var ad in new[]
        {
            "film.mkv", "film.srt", "film.idx", "film.sub", "film.sup", "baska.srt",
            Path.Combine("Subs", "film.tr.srt"), Path.Combine("Subs", "baska.srt"),
            Path.Combine("subtitles", "film", "2_English.srt"), Path.Combine("subtitles", "film", "notlar.txt"),
            Path.Combine("Extras", "film.srt")
        }) File.WriteAllText(Path.Combine(kok, ad), "x");

        var bulunan = SubtitleOptions.Sidecars(Path.Combine(kok, "film.mkv"))
            .Select(yol => Path.GetRelativePath(kok, yol).Replace('\\', '/'))
            .ToList();

        Assert.Equal(new[] { "Subs/film.tr.srt", "subtitles/film/2_English.srt", "film.idx", "film.sup", "film.srt" }, bulunan);
        Assert.True(SubtitleOptions.IsSubtitleFile("a.SUP"));
        Assert.True(SubtitleOptions.IsSubtitleFile("a.idx"));
        Directory.Delete(kok, true);
    }

    [Fact]
    public async Task SatirAtlamaKonumuDegistirirGizlemeIziDegistirmezKaydirmaGecikmeyiYazar()
    {
        var klasor = SatirKanit.Klasor("altyazi-satir-motor");
        var srt = Path.Combine(klasor, "satir.srt");
        File.WriteAllText(srt, SatirKanit.Srt, new UTF8Encoding(false));
        using var engine = await ParcaKanit.AcAsync();
        Assert.True(engine.AddSubtitle(srt), "sub-add basarisiz");
        var iz = engine.SubtitleTrack;
        Assert.True(iz > 0);

        await engine.SeekAsync(1.0, SeekPrecision.Exact);
        Assert.Equal("bir", await ParcaKanit.AltyaziBekle(engine, "bir"));
        Assert.Equal("bir", engine.SubtitleText);

        Assert.True(engine.SeekSubtitle(false), "sub-seek ileri basarisiz");
        var ileri = await SatirKanit.KonumBekle(engine, k => k > 2.4 && k < 2.7);
        Assert.InRange(ileri, 2.4, 2.7);
        Assert.Equal("iki", await ParcaKanit.AltyaziBekle(engine, "iki"));

        Assert.True(engine.SeekSubtitle(true), "sub-seek geri basarisiz");
        var geri = await SatirKanit.KonumBekle(engine, k => k > 0.4 && k < 0.7);
        Assert.InRange(geri, 0.4, 0.7);

        engine.SetSubtitleVisible(false);
        Assert.False(engine.SubtitleVisible);
        Assert.Equal("no", engine.GetProperty("sub-visibility"));
        Assert.Equal(iz, engine.SubtitleTrack);
        engine.SetSubtitleVisible(true);
        Assert.True(engine.SubtitleVisible);
        Assert.Equal(iz, engine.SubtitleTrack);

        await engine.SeekAsync(1.0, SeekPrecision.Exact);
        Assert.Equal(0, engine.SubtitleDelaySeconds, 6);
        Assert.True(engine.StepSubtitle(false), "sub-step basarisiz");
        Assert.Equal(-1.5, engine.SubtitleDelaySeconds, 1);
        Assert.Equal("iki", await ParcaKanit.AltyaziBekle(engine, "iki"));

        engine.Dispose();
        Directory.Delete(klasor, true);
    }

    [Fact]
    public void TuslarBaskaKomutlaCakismazEskiTuslarYerinde()
    {
        var satirlar = new (Key Key, KeyModifiers Mod, PlayerAction Eylem)[]
        {
            (Key.H, KeyModifiers.Alt, SubtitleOptions.SubtitleVisibility),
            (Key.PageUp, KeyModifiers.Alt, SubtitleOptions.SubtitlePreviousLine),
            (Key.PageDown, KeyModifiers.Alt, SubtitleOptions.SubtitleNextLine),
            (Key.Left, KeyModifiers.Control | KeyModifiers.Shift, SubtitleOptions.SubtitleStepBack),
            (Key.Right, KeyModifiers.Control | KeyModifiers.Shift, SubtitleOptions.SubtitleStepForward),
            (Key.C, KeyModifiers.Control, SubtitleOptions.SubtitleCopy)
        };
        foreach (var (key, mod, eylem) in satirlar)
        {
            var satir = Assert.Single(Keymap.DefaultRows, row => ReferenceEquals(row.Action, eylem));
            Assert.Equal((key, mod), (satir.Input.Key, satir.Input.Modifiers));
            Assert.Single(Keymap.DefaultRows, row => Keymap.SlotId(row) == Keymap.SlotId(satir));
            Assert.Equal(eylem.ToCommand(), Keymap.ForKey(key, mod, null));
        }

        Assert.Equal(PlayerCommandKind.Seek, Keymap.ForKey(Key.Right, KeyModifiers.Control, null).Kind);
        Assert.Equal(PlayerCommandKind.Seek, Keymap.ForKey(Key.Right, KeyModifiers.Alt, null).Kind);
        Assert.Equal(PlayerCommandKind.ChapterStep, Keymap.ForKey(Key.PageDown, KeyModifiers.Control, null).Kind);
        Assert.Equal(PlayerCommandKind.SubtitleCycle, Keymap.ForKey(Key.S, KeyModifiers.None, null).Kind);
    }

    [Fact]
    public void TuslarGorunumdenMotoraUlasirAltKlasorAltyazisiYuklenir()
    {
        var klasor = SatirKanit.Klasor("altyazi-satir-gorunum");
        var klip = Path.Combine(klasor, "klip.mkv");
        File.Copy(ParcaKanit.Klip, klip);
        Directory.CreateDirectory(Path.Combine(klasor, "Subs"));
        File.WriteAllText(Path.Combine(klasor, "Subs", "klip.srt"), SatirKanit.Srt, new UTF8Encoding(false));

        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(klip, out var window);
            var motor = DenetimSurucu.Motor(view);
            var kopyalanan = new List<string>();
            view.SubtitleCopier = (_, metin) =>
            {
                kopyalanan.Add(metin);
                return Task.CompletedTask;
            };
            var dis = motor.Tracks.Where(t => t is { Kind: PlaybackTrackKind.Subtitle, External: true }).ToList();
            var iz = motor.SubtitleTrack;
            DenetimSurucu.Duraklat(view);
            DenetimSurucu.Git(view, 1.0);
            DenetimSurucu.Pump(view, () => motor.GetProperty("sub-text") == "bir", 5);

            GirdiSurucu.Key(view, Key.C, KeyModifiers.Control);
            var kopyaIz = view.Trace[^1];

            GirdiSurucu.Key(view, Key.H, KeyModifiers.Alt);
            var gizli = (Gorunur: motor.GetProperty("sub-visibility"), Iz: motor.SubtitleTrack, IzSatiri: view.Trace[^1]);
            GirdiSurucu.Key(view, Key.H, KeyModifiers.Alt);
            var acik = motor.GetProperty("sub-visibility");

            GirdiSurucu.Key(view, Key.PageDown, KeyModifiers.Alt);
            var atlaIz = view.Trace[^1];
            DenetimSurucu.Pump(view, () => motor.PositionSeconds > 2.4, 5);
            var atlanan = motor.PositionSeconds;

            DenetimSurucu.Git(view, 1.0);
            GirdiSurucu.Key(view, Key.Right, KeyModifiers.Control | KeyModifiers.Shift);
            var kaydirIz = view.Trace[^1];
            var gecikme = (Motor: motor.SubtitleDelaySeconds, Gorunum: view.SubtitleDelay);

            view.Close();
            window.Close();
            return (dis.Count, iz, kopyalanan, kopyaIz, gizli, acik, atlaIz, atlanan, kaydirIz, gecikme);
        });

        Assert.Equal(1, rapor.Count);
        Assert.True(rapor.iz > 0);
        Assert.Equal(new[] { "bir" }, rapor.kopyalanan);
        Assert.Equal("subcopy -> ok", rapor.kopyaIz);
        Assert.Equal("no", rapor.gizli.Gorunur);
        Assert.Equal(rapor.iz, rapor.gizli.Iz);
        Assert.Equal("subvisible -> False", rapor.gizli.IzSatiri);
        Assert.Equal("yes", rapor.acik);
        Assert.Equal("subline 1 -> ok", rapor.atlaIz);
        Assert.InRange(rapor.atlanan, 2.4, 2.7);
        Assert.StartsWith("substep 1 -> ", rapor.kaydirIz, StringComparison.Ordinal);
        Assert.Equal(-1.5, rapor.gecikme.Motor, 1);
        Assert.Equal(rapor.gecikme.Motor, rapor.gecikme.Gorunum, 3);
        Directory.Delete(klasor, true);
    }
}
