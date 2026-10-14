using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// ARC-01 ve ARC-05: medya bilgisi dökümü (<c>Core/MediaReport</c>), denetim listesi
/// (<c>Core/Editing/AuditReport</c>), medya bilgisi penceresi ve düzenleyicinin "listeyi kaydet"
/// düğmesi. Döküm kolu StreamMappingTests'in çok izli, bölümlü 3 sn dosyasını (1 görüntü, 2 ses,
/// 2 altyazı, "Bir" ve "Iki" bölümleri) ffprobe ile okur; liste kolları sahte tarayıcıyla, listenin
/// taramanın bulduğunun aynısı olduğunu ölçer. Kanıt <c>.calisma/worktree-agent-a208ae8e6f7ba5c65/</c>,
/// test siler.
/// </summary>
public sealed class MedyaBilgisiTests
{
    private const string Dal = "worktree-agent-a208ae8e6f7ba5c65";

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", Dal);
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static readonly SilenceScan SahteTarama = new(
        new[] { new IdleSpan(12.5, 14), new IdleSpan(2, 3.25) },
        new[] { new IdleSpan(40, 41.5) },
        new SilenceCutOptions { Silence = true, Black = true });

    private static readonly IReadOnlyList<double> SahteSahneler = new[] { 30.04, 2 };

    private static readonly AuditEntry[] Beklenen =
    {
        new(AuditKind.Silence, 2, 3.25),
        new(AuditKind.Scene, 2, null),
        new(AuditKind.Silence, 12.5, 14),
        new(AuditKind.Scene, 30.04, null),
        new(AuditKind.Black, 40, 41.5),
    };

    private static string Ad(AuditKind tur) => tur.ToString();

    private static readonly string[] BeklenenSatirlar =
    {
        "00:00:02.000 - 00:00:03.250  Silence",
        "00:00:02.000  Scene",
        "00:00:12.500 - 00:00:14.000  Silence",
        "00:00:30.040  Scene",
        "00:00:40.000 - 00:00:41.500  Black",
    };

    [FfmpegAvailableFact]
    public async Task DokumHerIziVeBolumuIcerir()
    {
        var girdi = await AkisGirdisi.CokIzliAsync();
        var bilgi = await FfprobeClient.ProbeAsync(girdi);

        Assert.Equal(5, bilgi.Streams.Count);
        Assert.Equal(2, bilgi.Chapters.Count);

        var bolumler = MediaReport.Build(bilgi, CultureInfo.InvariantCulture);
        var izler = bolumler.Where(b => b.Key is "video" or "audio" or "subtitle").ToList();
        Assert.Equal(bilgi.Streams.Select(s => (s.Kind.ToString().ToLowerInvariant(), s.Index)), izler.Select(b => (b.Key, b.Number)));
        Assert.Equal(new[] { "video", "audio", "audio", "subtitle", "subtitle" }, izler.Select(b => b.Key));
        foreach (var (iz, bolum) in bilgi.Streams.Zip(izler))
            Assert.Contains(bolum.Fields, f => f.Key == "codec" && f.Value == iz.Codec);

        var isaretler = bolumler.Where(b => b.Key == "chapter").ToList();
        Assert.Equal(new[] { "Bir", "Iki" }, isaretler.Select(b => b.Fields.Single(f => f.Key == "title").Value));
        Assert.Equal(new[] { "00:00:00.000", "00:00:01.500" }, isaretler.Select(b => b.Fields.Single(f => f.Key == "start").Value));

        var genel = bolumler[0];
        Assert.Equal("general", genel.Key);
        Assert.Equal("5", genel.Fields.Single(f => f.Key == "streams").Value);
        Assert.Equal("2", genel.Fields.Single(f => f.Key == "chapters").Value);

        var metin = MediaInfoWindow.DetailsOf(bilgi);
        AkisGirdisi.Write("medya-bilgisi.txt", metin);
        foreach (var bolum in bolumler)
            Assert.Contains(MediaInfoWindow.Heading(bolum), metin);
        foreach (var iz in bilgi.Streams)
            Assert.Contains(iz.Codec, metin);
        Assert.Contains("Bir", metin);
        Assert.Contains("Iki", metin);

        AkisGirdisi.Kapat("medya-bilgisi.txt");
    }

    [Fact]
    public void DenetimListesiTaramaninAynisiSirali()
    {
        var liste = AuditReport.Build(SahteTarama, SahteSahneler);
        Assert.Equal(Beklenen, liste);
        Assert.Equal(BeklenenSatirlar, AuditReport.Text(liste, Ad).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal(Beklenen.Where(e => e.Kind != AuditKind.Scene), AuditReport.Build(SahteTarama, null));
        Assert.Equal(Beklenen.Where(e => e.Kind == AuditKind.Scene), AuditReport.Build(null, SahteSahneler));
        Assert.Empty(AuditReport.Build(null, null));
    }

    [Fact]
    public void PencereListesiSahteTaramayaEsitVeKaydedilir()
    {
        var kayit = Path.Combine(Kanit, "pencere-denetim.txt");
        try
        {
            AppHost.Run(() =>
            {
                var bilgi = new MediaInfo { FilePath = Path.Combine(Kanit, "kaynak.mp4"), FileSizeBytes = 16, DurationSeconds = 60, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", AudioCodec = "aac", TotalBitrateBps = 1 };
                var pencere = new MediaInfoWindow(bilgi.FilePath, bilgi);
                SilenceCutOptions? ayar = null;
                var sahneSuresi = 0.0;
                pencere.SilenceScanner = (_, a, _, ilerleme, _) =>
                {
                    ayar = a;
                    ilerleme.Report(1);
                    return Task.FromResult(SahteTarama);
                };
                pencere.SceneScanner = (_, sure, _, _) =>
                {
                    sahneSuresi = sure;
                    return Task.FromResult(SahteSahneler);
                };
                pencere.SavePicker = _ => Task.FromResult<string?>(kayit);
                pencere.Show();
                try
                {
                    Assert.Null(pencere.Audit);
                    var tarama = pencere.ScanAsync();
                    Pompala(() => tarama.IsCompleted);
                    Assert.True(tarama.Result);
                    Assert.True(ayar!.Silence);
                    Assert.True(ayar.Black);
                    Assert.Equal(60, sahneSuresi);

                    var liste = AuditReport.Build(SahteTarama, SahteSahneler);
                    Assert.Equal(Beklenen, pencere.Audit);
                    Assert.Equal(AuditReport.Text(liste, MediaInfoWindow.KindName).TrimEnd(), pencere.AuditText);
                    Assert.Equal(Beklenen.Length, pencere.AuditText.Split(Environment.NewLine).Length);

                    var kaydet = pencere.SaveAsync();
                    Pompala(() => kaydet.IsCompleted);
                    Assert.True(kaydet.Result);
                    var yazilan = File.ReadAllText(kayit);
                    Assert.Equal(pencere.ReportText(), yazilan);
                    Assert.StartsWith(pencere.DetailsText, yazilan);
                    Assert.EndsWith(pencere.AuditText + Environment.NewLine, yazilan);
                }
                finally
                {
                    pencere.Close();
                }
            });
        }
        finally
        {
            KanitKapanisi.Kapat(Kanit, "pencere-denetim.txt");
        }
    }

    [Fact]
    public void DuzenleyiciListeyiTaramaninAynisiOlarakKaydeder()
    {
        var klasor = Path.Combine(Kanit, "duzenleyici-denetim");
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        var kayit = Path.Combine(klasor, "liste.txt");
        File.WriteAllBytes(dosya, new byte[16]);
        try
        {
            AppHost.Run(() =>
            {
                var motor = new KlipMotoru();
                var view = new EditorView
                {
                    KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 60, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", AudioCodec = "aac", TotalBitrateBps = 1 }
                };
                view.Player.EngineFactory = () => motor;
                view.OverlayRoot = Path.Combine(klasor, "katman");
                view.SilenceScanner = (_, _, _, ilerleme, _) =>
                {
                    ilerleme.Report(1);
                    return Task.FromResult(SahteTarama);
                };
                view.SceneScanner = (_, _, _, _, _) => Task.FromResult(SahteSahneler);
                view.AuditSavePicker = _ => Task.FromResult<string?>(kayit);
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    view.BtnSilence.IsChecked = true;
                    view.BtnSilence.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.False(view.BtnSilenceSaveList.IsEnabled);

                    var tarama = view.ScanSilenceAsync();
                    DenetimSurucu.Pump(view.Player, () => tarama.IsCompleted, 10);
                    Assert.True(tarama.Result);
                    Assert.True(view.BtnSilenceSaveList.IsEnabled);
                    Assert.Equal(Beklenen.Where(e => e.Kind != AuditKind.Scene), view.AuditEntries);

                    var sahne = view.SplitScenesAsync();
                    DenetimSurucu.Pump(view.Player, () => sahne.IsCompleted, 10);
                    sahne.GetAwaiter().GetResult();
                    Assert.Equal(Beklenen, view.AuditEntries);
                    Assert.True(view.BtnSilenceSaveList.IsEnabled);

                    var kaydet = view.SaveAuditListAsync();
                    DenetimSurucu.Pump(view.Player, () => kaydet.IsCompleted, 10);
                    Assert.True(kaydet.Result);
                    Assert.Equal(AuditReport.Text(Beklenen, MediaInfoWindow.KindName), File.ReadAllText(kayit));
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }
            });
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
            KanitKapanisi.Kapat(Kanit);
        }
    }

    private static void Pompala(Func<bool> bitti)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < 10)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
    }
}
