using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Duzenleyicinin A2 muzik izi arayuzu (later 103): goruntusu olmayan ses dosyasi kaynak olarak reddedilmez, oynatma basindan
/// varsayilan -12 dB ile muzik izine konur; duzey, tasima ve kaldirma tek geri alma adimidir; cizelgede A2 satiri A1 ile seridin
/// arasinda durur ve govdesi surukleyince baslangic kayar. Canli kol ffmpeg <c>sine</c> ile 2 sn'lik m4a uretip gercek ffprobe
/// yolundan gecirir. Kanit <c>.calisma/muzik-izi/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciMuzikIziTests
{
    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "muzik-izi", ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Kapat(string klasor)
    {
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        var ust = Path.GetDirectoryName(klasor)!;
        if (Directory.Exists(ust) && !Directory.EnumerateFileSystemEntries(ust).Any()) Directory.Delete(ust);
    }

    private static string Sahte(string klasor, string ad)
    {
        var dosya = Path.Combine(klasor, ad);
        File.WriteAllBytes(dosya, new byte[16]);
        return dosya;
    }

    private static MediaInfo Video(string yol) => new()
    {
        FilePath = yol, FileSizeBytes = 16, DurationSeconds = 60, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264",
        AudioCodec = "aac", TotalBitrateBps = 1
    };

    private static MediaInfo Ses(string yol, double sure = 30) => new()
    {
        FilePath = yol, FileSizeBytes = 16, DurationSeconds = sure, Width = 0, Height = 0, Fps = 0, VideoCodec = "none",
        AudioCodec = "aac", TotalBitrateBps = 1, IsAudioOnly = true
    };

    private static EditorView Gorunum(Func<string, MediaInfo?> bilgi)
    {
        var view = new EditorView
        {
            KnownInfo = bilgi,
            Projects = null,
            PeakReader = (_, _) => Task.FromResult(AudioPeaks.Empty),
            KeyframeReader = (_, _) => Task.FromResult<IReadOnlyList<double>>(Array.Empty<double>()),
        };
        view.Player.EngineFactory = () => new KlipMotoru();
        return view;
    }

    private static T Bekle<T>(EditorView view, Task<T> gorev)
    {
        DenetimSurucu.Pump(view.Player, () => gorev.IsCompleted, 15);
        Assert.True(gorev.IsCompleted, "gorev 15 sn icinde bitmedi");
        return gorev.GetAwaiter().GetResult();
    }

    private static void Bekle(EditorView view, Task gorev)
    {
        DenetimSurucu.Pump(view.Player, () => gorev.IsCompleted, 15);
        Assert.True(gorev.IsCompleted, "gorev 15 sn icinde bitmedi");
        gorev.GetAwaiter().GetResult();
    }

    private static T Pencerede<T>(EditorView view, Func<T> is_)
    {
        var pencere = new Window { Width = 1000, Height = 640, Content = view };
        pencere.Show();
        try
        {
            return is_();
        }
        finally
        {
            view.Player.Close();
            pencere.Close();
        }
    }

    [Fact]
    public void SesDosyasiKaynakOlmazOynatmaBasindanMuzikIzineKonur()
    {
        var klasor = Klasor("koy");
        try
        {
            var video = Sahte(klasor, "video.mp4");
            var muzik = Sahte(klasor, "muzik.m4a");
            var sessiz = Sahte(klasor, "sessiz.bin");
            var s = AppHost.Run(() =>
            {
                var view = Gorunum(yol => yol == muzik ? Ses(yol) : yol == sessiz ? Ses(yol) with { AudioCodec = null } : Video(yol));
                return Pencerede(view, () =>
                {
                    Bekle(view, view.OpenSourceAsync(video));
                    view.Timeline.Playhead = S(5);
                    var konuldu = Bekle(view, view.AddSourceAsync(muzik));
                    var sonra = (Muzik: view.Model!.Music, Kaynak: view.SourcePaths.Count, Klip: view.Model.Clips.Count);
                    var sessizKondu = Bekle(view, view.AddSourceAsync(sessiz));
                    var sessizSonra = view.Model.Music;
                    var geri = view.Undo();
                    var geriSonra = view.Model.Music;
                    return (konuldu, sonra, sessizKondu, sessizSonra, geri, geriSonra);
                });
            });

            Assert.True(s.konuldu);
            Assert.NotNull(s.sonra.Muzik);
            Assert.Equal(muzik, s.sonra.Muzik!.Path);
            Assert.Equal(S(5), s.sonra.Muzik.Start);
            Assert.Equal(S(30), s.sonra.Muzik.Duration);
            Assert.Equal(EditorView.MusicDefaultLevelDb, s.sonra.Muzik.LevelDb);
            Assert.Equal(-12, EditorView.MusicDefaultLevelDb);
            Assert.Equal(1, s.sonra.Kaynak);
            Assert.Equal(1, s.sonra.Klip);
            Assert.False(s.sessizKondu);
            Assert.Same(s.sonra.Muzik, s.sessizSonra);
            Assert.True(s.geri);
            Assert.Null(s.geriSonra);
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void OynatmaBasiSondaysaMuzikBastanBaslarDegisimDuzeyiKorur()
    {
        var klasor = Klasor("son");
        try
        {
            var video = Sahte(klasor, "video.mp4");
            var a = Sahte(klasor, "a.m4a");
            var b = Sahte(klasor, "b.m4a");
            var s = AppHost.Run(() =>
            {
                var view = Gorunum(yol => yol == video ? Video(yol) : Ses(yol, yol == b ? 12 : 30));
                return Pencerede(view, () =>
                {
                    Bekle(view, view.OpenSourceAsync(video));
                    view.Timeline.Playhead = view.Model!.Duration;
                    Assert.True(Bekle(view, view.AddMusicAsync(a)));
                    var ilk = view.Model.Music!;
                    Assert.True(view.SetMusicLevel(-6));
                    view.Timeline.Playhead = S(10);
                    Assert.True(Bekle(view, view.AddMusicAsync(b)));
                    return (ilk, Ikinci: view.Model.Music!);
                });
            });

            Assert.Equal(0, s.ilk.Start);
            Assert.Equal(b, s.Ikinci.Path);
            Assert.Equal(S(12), s.Ikinci.Duration);
            Assert.Equal(S(10), s.Ikinci.Start);
            Assert.Equal(-6, s.Ikinci.LevelDb);
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void DuzeyTasimaVeKaldirmaTekAdimdirDuzeyAraligaKirpilir()
    {
        var klasor = Klasor("duzey");
        try
        {
            var video = Sahte(klasor, "video.mp4");
            var muzik = Sahte(klasor, "muzik.m4a");
            var s = AppHost.Run(() =>
            {
                var view = Gorunum(yol => yol == muzik ? Ses(yol) : Video(yol));
                return Pencerede(view, () =>
                {
                    Bekle(view, view.OpenSourceAsync(video));
                    var bosDuzey = view.SetMusicLevel(-6);
                    var bosTasima = view.MoveMusic(S(1));
                    var bosKaldirma = view.RemoveMusic();
                    view.Timeline.Playhead = S(3);
                    Assert.True(Bekle(view, view.AddSourceAsync(muzik)));
                    Assert.True(view.SetMusicLevel(-6));
                    var duzey = view.Model!.Music!;
                    Assert.True(view.SetMusicLevel(40));
                    var tavan = view.Model.Music!.LevelDb;
                    Assert.True(view.MoveMusic(S(8)));
                    var tasindi = view.Model.Music!;
                    Assert.True(view.MoveMusic(S(50000)));
                    var sinir = view.Model.Music!.Start;
                    Assert.True(view.RemoveMusic());
                    var kaldirildi = view.Model.Music;
                    Assert.True(view.Undo());
                    var geri = view.Model.Music;
                    return (bosDuzey, bosTasima, bosKaldirma, duzey, tavan, tasindi, sinir, Sure: view.Model.Duration, kaldirildi, geri);
                });
            });

            Assert.False(s.bosDuzey);
            Assert.False(s.bosTasima);
            Assert.False(s.bosKaldirma);
            Assert.Equal(-6, s.duzey.LevelDb);
            Assert.Equal(S(3), s.duzey.Start);
            Assert.Equal(EditMusic.MaxLevelDb, s.tavan);
            Assert.Equal(S(8), s.tasindi.Start);
            Assert.Equal(EditMusic.MaxLevelDb, s.tasindi.LevelDb);
            Assert.Equal(s.Sure - 1, s.sinir);
            Assert.Null(s.kaldirildi);
            Assert.NotNull(s.geri);
            Assert.Equal(s.Sure - 1, s.geri!.Start);
        }
        finally
        {
            Kapat(klasor);
        }
    }

    private static Pointer Isaretci() => new(Pointer.GetNextFreeId(), PointerType.Mouse, true);

    private static void Bas(EditorTimeline cizelge, IPointer isaretci, Point nokta) =>
        cizelge.RaiseEvent(new PointerPressedEventArgs(cizelge, isaretci, cizelge, nokta, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));

    private static void Kaydir(EditorTimeline cizelge, IPointer isaretci, Point nokta) =>
        cizelge.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, cizelge, isaretci, cizelge, nokta, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));

    private static void Birak(EditorTimeline cizelge, IPointer isaretci, Point nokta) =>
        cizelge.RaiseEvent(new PointerReleasedEventArgs(cizelge, isaretci, cizelge, nokta, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));

    [Fact]
    public void A2SatiriA1IleSeridinArasindaGovdesiSurukleyinceBaslangicKayar()
    {
        var s = AppHost.Run(() =>
        {
            var model = EditTimeline.FromSource(S(60));
            Assert.True(model.SetMusic(new EditMusic("muzik.m4a", S(20), S(10), -12)));
            var cizelge = new EditorTimeline();
            var pencere = new Window { Width = 1000, Height = 400, Content = cizelge };
            pencere.Show();
            try
            {
                cizelge.Fps = 30;
                cizelge.Show(model);
                Dispatcher.UIThread.RunJobs();
                long? tasinan = null;
                cizelge.MusicMoved += at => tasinan = at;
                var y = cizelge.MusicTop + cizelge.MusicHeight / 2;
                var ici = cizelge.MusicAt(cizelge.TimeToX(S(15)));
                var oncesi = cizelge.MusicAt(cizelge.TimeToX(S(9)));
                var sonrasi = cizelge.MusicAt(cizelge.TimeToX(S(31)));
                var bas = new Point(cizelge.TimeToX(S(15)), y);
                var hedef = new Point(cizelge.TimeToX(S(25)), y);
                var isaretci = Isaretci();
                Bas(cizelge, isaretci, bas);
                var basili = cizelge.MusicDragging;
                Kaydir(cizelge, isaretci, hedef);
                var hayalet = cizelge.MusicGhost;
                Birak(cizelge, isaretci, hedef);
                var birakti = cizelge.MusicDragging;

                long? bostaTasinan = null;
                cizelge.MusicMoved += at => bostaTasinan = at;
                var bosNokta = new Point(cizelge.TimeToX(S(45)), y);
                Bas(cizelge, Isaretci(), bosNokta);
                var bostaBasili = cizelge.MusicDragging;
                return (cizelge.TracksBottom, cizelge.MusicTop, cizelge.MusicBottom, cizelge.AudioHeight, cizelge.MusicHeight, cizelge.StripTop,
                    Aralik: EditorTokens.Size(cizelge, "EditorTrackGap"), ici, oncesi, sonrasi, basili, hayalet, birakti, tasinan, bostaBasili, bostaTasinan,
                    Satir: cizelge.OnMusicRow(new Point(cizelge.TimeToX(S(1)), y)), Ust: cizelge.OnMusicRow(new Point(cizelge.TimeToX(S(1)), cizelge.MusicTop - 1)));
            }
            finally
            {
                pencere.Close();
            }
        });

        Assert.Equal(s.TracksBottom + s.Aralik, s.MusicTop, 6);
        Assert.Equal(s.AudioHeight, s.MusicHeight, 6);
        Assert.Equal(s.MusicTop + s.MusicHeight, s.MusicBottom, 6);
        Assert.Equal(s.MusicBottom + s.Aralik, s.StripTop, 6);
        Assert.True(s.Satir);
        Assert.False(s.Ust);
        Assert.True(s.ici);
        Assert.False(s.oncesi);
        Assert.False(s.sonrasi);
        Assert.True(s.basili);
        Assert.NotNull(s.hayalet);
        Assert.InRange(s.hayalet!.Value, S(19.9), S(20.1));
        Assert.False(s.birakti);
        Assert.NotNull(s.tasinan);
        Assert.InRange(s.tasinan!.Value, S(19.9), S(20.1));
        Assert.False(s.bostaBasili);
        Assert.Null(s.bostaTasinan);
    }

    [FfmpegFact]
    public void GercekSesDosyasiFfprobeYolundanMuzikIzineKonur()
    {
        var klasor = Klasor("canli");
        try
        {
            var video = Sahte(klasor, "video.mp4");
            var muzik = Path.Combine(klasor, "muzik.m4a");
            Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", "sine=f=440:d=2", "-c:a", "aac", "-threads", "2", muzik);
            var s = AppHost.Run(() =>
            {
                var view = Gorunum(yol => yol == video ? Video(yol) : null);
                return Pencerede(view, () =>
                {
                    Bekle(view, view.OpenSourceAsync(video));
                    var kondu = Bekle(view, view.AddSourceAsync(muzik));
                    return (kondu, view.Model!.Music, Kaynak: view.SourcePaths.Count);
                });
            });

            Assert.True(s.kondu);
            Assert.NotNull(s.Music);
            Assert.InRange(s.Music!.Duration, S(1.9), S(2.1));
            Assert.Equal(1, s.Kaynak);
        }
        finally
        {
            Kapat(klasor);
        }
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
}
