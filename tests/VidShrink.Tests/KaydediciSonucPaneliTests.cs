using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class KaydediciSonucPaneliTests
{
    private static readonly DateTime An = new(2026, 10, 18, 10, 20, 30);

    private static string Klasor()
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "yan-sonuc-paneli", "sonuc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static RecordResult Tam(string yol) => new(true, yol, 1, false, 0, string.Empty, 1);

    [Fact]
    public void VarsayilanDesenEskiKayitAdiniVerir()
    {
        var dolu = new HashSet<string> { Path.Combine("d", "kayit_2026-10-18_10-20-30.mp4") };

        Assert.Equal(Path.Combine("d", "kayit_2026-10-18_10-20-30.mp4"), AdlandirmaDeseni.KayitYolu("d", null, ".mp4", An, null, _ => false));
        Assert.Equal(Path.Combine("d", "kayit_2026-10-18_10-20-30_2.mp4"), AdlandirmaDeseni.KayitYolu("d", null, ".mp4", An, null, dolu.Contains));
        Assert.Equal(Path.Combine("d", "kayit_2026-10-18_10-20-30.mkv"), AdlandirmaDeseni.KayitYolu("d", "{ad}", ".mkv", An, null, _ => false));
    }

    [Fact]
    public void PencereAdiTemizlenirSayacBosNumarayiAlir()
    {
        var dolu = new HashSet<string> { Path.Combine("d", "Not Defteri ab-001.mp4"), Path.Combine("d", "Not Defteri ab-002.mp4") };
        var uzun = new string('p', 80);

        Assert.Equal(Path.Combine("d", "Not Defteri ab-003.mp4"), AdlandirmaDeseni.KayitYolu("d", "{pencere}-{sayac}", ".mp4", An, "Not Defteri: a*b", dolu.Contains));
        Assert.Equal(Path.Combine("d", "001.mp4"), AdlandirmaDeseni.KayitYolu("d", "{pencere}-{sayac}", ".mp4", An, null, _ => false));
        Assert.Equal(Path.Combine("d", "kayit_2026-10-18_10-20-30.mp4"), AdlandirmaDeseni.KayitYolu("d", "{pencere}", ".mp4", An, null, _ => false));
        Assert.Equal(Path.Combine("d", "rec_2026-10-18_10-20.gif"), AdlandirmaDeseni.KayitYolu("d", "rec_{tarih}_{saat}", ".gif", An, "x", _ => false));
        Assert.Equal(AdlandirmaDeseni.PencereSiniri + 4, Path.GetFileName(AdlandirmaDeseni.KayitYolu("d", "{pencere}", ".mp4", An, uzun, _ => false)).Length);
    }

    [Fact]
    public void KayitDeseniKendiKumesiyleDenetlenir()
    {
        Assert.Null(AdlandirmaDeseni.KayitHata("{damga}_{pencere}_{sayac}"));
        Assert.Equal("settings-tab.output-name.bad-placeholder", AdlandirmaDeseni.KayitHata("{ad}"));
        Assert.Equal("settings-tab.output-name.bad-char", AdlandirmaDeseni.KayitHata("a:b"));
        Assert.Equal("settings-tab.output-name.empty", AdlandirmaDeseni.KayitHata(" "));
        Assert.Equal("settings-tab.output-name.bad-placeholder", AdlandirmaDeseni.Hata("{pencere}"));
    }

    [Fact]
    public void GifArgumanlariGenisligiKaynaktanBuyutmez()
    {
        var argumanlar = GifPalette.BuildFitted("k.mp4", "k.gif", 10, 640).ToList();

        Assert.Equal("k.mp4", argumanlar[argumanlar.IndexOf("-i") + 1]);
        Assert.Contains("-an", argumanlar);
        Assert.Equal("fps=10,scale='min(iw,640)':-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse", argumanlar[argumanlar.IndexOf("-vf") + 1]);
        Assert.Equal("k.gif", argumanlar[^1]);
        Assert.Equal(Path.Combine("d", "k_2.gif"), RecorderArguments.SiblingTarget(Path.Combine("d", "k.mp4"), ".gif", p => p == Path.Combine("d", "k.gif")));
    }

    [Fact]
    public void SonucPanelindeKopyalaVeGifCalisir()
    {
        var klasor = Klasor();
        try
        {
            var dosya = Path.Combine(klasor, "kayit.mp4");
            File.WriteAllBytes(dosya, new byte[] { 1 });
            var gif = Path.Combine(klasor, "eski.gif");
            File.WriteAllBytes(gif, new byte[] { 1 });
            var ayar = Path.Combine(klasor, "recorder-settings.json");

            var olcu = AppHost.Run(() =>
            {
                var pano = new SahtePano();
                IReadOnlyList<string>? verilen = null;
                var view = new RecorderView(ayar)
                {
                    FinishClipboard = pano,
                    ResultGifRun = args => { verilen = args; File.WriteAllBytes(args[^1], new byte[] { 1 }); return Task.FromResult(true); }
                };

                view.ShowResult(Tam(gif));
                var giftenGif = view.GifVisible;
                view.ShowResult(new RecordResult(true, dosya, 1, true, 0, string.Empty, 1) { Playable = false });
                var yarimGif = view.GifVisible;
                var yarimKopya = view.CopyFileVisible;
                view.ShowResult(Tam(dosya));
                var tamGif = view.GifVisible;
                var tamKopya = view.CopyFileVisible;

                var kopyalandi = view.CopyResultFileAsync();
                var kopyaDurumu = view.FinishStatusText;
                pano.Atacak = new InvalidOperationException("dolu");
                _ = view.CopyResultFileAsync();
                var kopyaHatasi = (view.FinishStatusText, view.FinishStatusIsError);

                var cevrilen = view.SaveAsGifAsync();
                var bildirim = view.NoticeText;
                view.ResultGifRun = args => { File.WriteAllBytes(args[^1], new byte[] { 1 }); return Task.FromResult(false); };
                var dusen = view.SaveAsGifAsync();
                var hata = view.ErrorText;
                return (giftenGif, yarimGif, yarimKopya, tamGif, tamKopya, kopyalandi: kopyalandi.Result, dosyalar: pano.Dosyalar.ToList(), kopyaDurumu, kopyaHatasi,
                    cevrilen: cevrilen.Result, verilen, bildirim, dusen: dusen.Result, hata);
            });

            var hedef = Path.Combine(klasor, "kayit.gif");
            Assert.False(olcu.giftenGif);
            Assert.False(olcu.yarimGif);
            Assert.False(olcu.yarimKopya);
            Assert.True(olcu.tamGif);
            Assert.True(olcu.tamKopya);
            Assert.True(olcu.kopyalandi);
            Assert.Equal(new[] { dosya }, olcu.dosyalar);
            Assert.Equal(LanguageCatalogText("recorder.finish.copied"), olcu.kopyaDurumu);
            Assert.True(olcu.kopyaHatasi.FinishStatusIsError);
            Assert.Contains("dolu", olcu.kopyaHatasi.FinishStatusText);
            Assert.Equal(hedef, olcu.cevrilen);
            Assert.NotNull(olcu.verilen);
            Assert.Equal(dosya, olcu.verilen![olcu.verilen.ToList().IndexOf("-i") + 1]);
            Assert.Equal(hedef, olcu.verilen[^1]);
            Assert.Contains(hedef, olcu.bildirim);
            Assert.Null(olcu.dusen);
            Assert.Equal(LanguageCatalogText("recorder.output.gif-convert-failed"), olcu.hata);
            Assert.False(File.Exists(Path.Combine(klasor, "kayit_2.gif")));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void DigerMenusuDonusumEylemlerinideTasirVeCalistirir()
    {
        var klasor = Klasor();
        try
        {
            var dosya = Path.Combine(klasor, "kayit.mp4");
            File.WriteAllBytes(dosya, new byte[] { 1 });
            var mkv = Path.Combine(klasor, "kayit.mkv");
            File.WriteAllBytes(mkv, new byte[] { 1 });
            var gif = Path.Combine(klasor, "eski.gif");
            File.WriteAllBytes(gif, new byte[] { 1 });
            var ayar = Path.Combine(klasor, "recorder-settings.json");

            var olcu = AppHost.Run(() =>
            {
                IReadOnlyList<string>? verilen = null;
                var view = new RecorderView(ayar)
                {
                    ResultGifRun = args => { verilen = args; File.WriteAllBytes(args[^1], new byte[] { 1 }); return Task.FromResult(true); }
                };

                view.ShowResult(Tam(gif));
                var gifdenDiger = view.MoreVisible;
                view.ShowResult(Tam(dosya));
                var mp4tenDiger = view.MoreVisible;
                var mp4tenOgeler = view.MoreItems.Select(m => (m.Name, m.IsVisible)).ToList();
                var duzDugmeler = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view)
                    .OfType<Avalonia.Controls.Button>().Select(b => b.Name).ToList();
                view.ShowResult(Tam(mkv));
                var mkvOgeler = view.MoreItems.Select(m => (m.Name, m.IsVisible)).ToList();

                var gifOgesi = view.MoreItems.Single(m => m.Name == "BtnToGif");
                var oge = (gifOgesi.Header, Avalonia.Automation.AutomationProperties.GetName(gifOgesi));
                view.ShowResult(Tam(dosya));
                gifOgesi.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.MenuItem.ClickEvent));
                var bildirim = view.NoticeText;
                var dugme = Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.Button>(view, "BtnResultMore")!;
                return (gifdenDiger, mp4tenDiger, mp4tenOgeler, duzDugmeler, mkvOgeler, oge, verilen, bildirim,
                    ad: Avalonia.Automation.AutomationProperties.GetName(dugme), icerik: dugme.Content, bayrak: dugme.Flyout is Avalonia.Controls.MenuFlyout);
            });

            Assert.False(olcu.gifdenDiger);
            Assert.True(olcu.mp4tenDiger);
            Assert.Contains(("BtnToGif", true), olcu.mp4tenOgeler);
            Assert.Contains(("BtnToMp4", false), olcu.mp4tenOgeler);
            Assert.DoesNotContain("BtnToGif", olcu.duzDugmeler);
            Assert.DoesNotContain("BtnToMp4", olcu.duzDugmeler);
            Assert.Contains(("BtnToMp4", true), olcu.mkvOgeler);
            Assert.Equal(LanguageCatalogText("recorder.output.to-gif"), olcu.oge.Header);
            Assert.Equal(LanguageCatalogText("recorder.output.to-gif"), olcu.oge.Item2);
            Assert.NotNull(olcu.verilen);
            Assert.Equal(Path.Combine(klasor, "kayit.gif"), olcu.verilen![^1]);
            Assert.Contains("kayit.gif", olcu.bildirim);
            Assert.Equal(LanguageCatalogText("recorder.output.more"), olcu.ad);
            Assert.Equal(LanguageCatalogText("recorder.output.more"), olcu.icerik);
            Assert.True(olcu.bayrak);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void DigerAnahtariButunDillerdeVar()
    {
        foreach (var dil in Locales.Languages)
            Assert.False(string.IsNullOrWhiteSpace(Locales.Values(dil).GetValueOrDefault("recorder.output.more")), $"{dil} dilinde recorder.output.more yok.");
    }

    [Fact]
    public void AdDeseniKutusuOnizlerVeAyaraYazar()
    {
        var klasor = Klasor();
        try
        {
            var ayar = Path.Combine(klasor, "recorder-settings.json");

            var olcu = AppHost.Run(() =>
            {
                var view = new RecorderView(ayar);
                var ilk = (view.NamePatternText, view.NamePreviewText);
                view.NamePatternText = "{ad}"; Dispatcher.UIThread.RunJobs();
                var bozuk = (view.NamePreviewText, view.NamePreviewIsError, RecorderSettings.Load(ayar).NamePattern);
                view.NamePatternText = "rec_{sayac}"; Dispatcher.UIThread.RunJobs();
                var iyi = (view.NamePreviewText, view.NamePreviewIsError, RecorderSettings.Load(ayar).NamePattern);
                var ayarli = RecorderSettings.Load(ayar);
                ayarli.OutputFolder = klasor;
                var yol = ayarli.OutputPath(An);
                view.NamePatternText = AdlandirmaDeseni.KayitVarsayilan; Dispatcher.UIThread.RunJobs();
                var geri = RecorderSettings.Load(ayar).NamePattern;
                return (ilk, bozuk, iyi, yol, geri);
            });

            Assert.Equal(AdlandirmaDeseni.KayitVarsayilan, olcu.ilk.NamePatternText);
            Assert.Contains("kayit_", olcu.ilk.NamePreviewText);
            Assert.True(olcu.bozuk.NamePreviewIsError);
            Assert.Equal(LanguageCatalogText("recorder.name.bad-placeholder"), olcu.bozuk.NamePreviewText);
            Assert.Null(olcu.bozuk.NamePattern);
            Assert.False(olcu.iyi.NamePreviewIsError);
            Assert.Contains("rec_001.", olcu.iyi.NamePreviewText);
            Assert.Equal("rec_{sayac}", olcu.iyi.NamePattern);
            Assert.Equal(Path.Combine(klasor, "rec_001.mp4"), olcu.yol);
            Assert.Null(olcu.geri);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void KomutSatiriAyniDeseniOkur()
    {
        var klasor = Klasor();
        try
        {
            var ayar = Path.Combine(klasor, "recorder-settings.json");
            File.WriteAllText(ayar, "{\"outputFolder\":" + System.Text.Json.JsonSerializer.Serialize(klasor)
                                    + ",\"target\":\"Window\",\"windowTitle\":\"Tarayici\",\"namePattern\":\"{pencere}_{sayac}\"}");
            File.WriteAllText(Path.Combine(klasor, "Tarayici_001.mp4"), "x");

            var cli = CliRecordSettings.Load(ayar);
            Assert.Equal("{pencere}_{sayac}", cli.NamePattern);
            Assert.Equal(Path.Combine(klasor, "Tarayici_002.mp4"), cli.OutputPath(An, RecorderContainer.Mp4));
            Assert.Equal(Path.Combine(klasor, "Tarayici_002.mp4"), RecorderSettings.Load(ayar).OutputPath(An));

            File.WriteAllText(ayar, "{\"outputFolder\":" + System.Text.Json.JsonSerializer.Serialize(klasor) + ",\"namePattern\":\"{ad}\"}");
            Assert.Null(CliRecordSettings.Load(ayar).NamePattern);
            Assert.Null(RecorderSettings.Load(ayar).NamePattern);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task GercekKayitGifeCevrilirGenislikSinirdaKalir()
    {
        var klasor = Klasor();
        try
        {
            var kaynak = Path.Combine(klasor, "kayit.mp4");
            var uret = await FfmpegRunner.RunAsync(new[]
            {
                "-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "testsrc=size=960x540:rate=10:duration=1",
                "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2", kaynak
            });
            Assert.True(uret.Ok, uret.StandardError);
            var kucuk = Path.Combine(klasor, "kucuk.mp4");
            var uretKucuk = await FfmpegRunner.RunAsync(new[]
            {
                "-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "testsrc=size=320x180:rate=10:duration=1",
                "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2", kucuk
            });
            Assert.True(uretKucuk.Ok, uretKucuk.StandardError);

            var gif = Path.Combine(klasor, "kayit.gif");
            var run = await FfmpegRunner.RunAsync(GifPalette.BuildFitted(kaynak, gif, RecorderView.ResultGifFps, RecorderView.ResultGifWidth));
            Assert.True(run.Ok, run.StandardError);
            var kucukGif = Path.Combine(klasor, "kucuk.gif");
            var runKucuk = await FfmpegRunner.RunAsync(GifPalette.BuildFitted(kucuk, kucukGif, RecorderView.ResultGifFps, RecorderView.ResultGifWidth));
            Assert.True(runKucuk.Ok, runKucuk.StandardError);

            var bilgi = await FfprobeClient.ProbeAsync(gif);
            var kucukBilgi = await FfprobeClient.ProbeAsync(kucukGif);
            Assert.Equal(640, bilgi.Width);
            Assert.Equal(360, bilgi.Height);
            Assert.Equal(320, kucukBilgi.Width);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    private static string LanguageCatalogText(string key) => VidShrink.App.LanguageCatalog.Display(Strings.Get(key));

    private sealed class SahtePano : IRecordingClipboard
    {
        public List<string> Dosyalar { get; } = new();

        public Exception? Atacak { get; set; }

        public Task SetFileAsync(string path)
        {
            if (Atacak is { } hata) throw hata;
            Dosyalar.Add(path);
            return Task.CompletedTask;
        }

        public Task SetTextAsync(string text) => Task.CompletedTask;
    }
}
