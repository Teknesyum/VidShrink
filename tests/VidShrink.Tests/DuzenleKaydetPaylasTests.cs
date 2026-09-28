using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.App;
using VidShrink.App.Editing;
using VidShrink.App.Playback;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Core.Share;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleKaydetPaylasTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "duzenle-kaydet-paylas");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    [Fact]
    public void CiktiAdiKaynaginYanindaVeCakismadaSayiAlir()
    {
        var klasor = Path.Combine(Path.GetTempPath(), "yok-klasor");
        var kaynak = Path.Combine(klasor, "tatil.mp4");
        var dolu = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(Path.Combine(klasor, "tatil-duzenlenmis.mp4"), EditOutputName.For(kaynak, dolu.Contains));

        dolu.Add(Path.Combine(klasor, "tatil-duzenlenmis.mp4"));
        Assert.Equal(Path.Combine(klasor, "tatil-duzenlenmis-2.mp4"), EditOutputName.For(kaynak, dolu.Contains));

        dolu.Add(Path.Combine(klasor, "tatil-duzenlenmis-2.mp4"));
        Assert.Equal(Path.Combine(klasor, "tatil-duzenlenmis-3.mp4"), EditOutputName.For(kaynak, dolu.Contains));

        Assert.Equal(Path.Combine(klasor, "a-duzenlenmis.MOV"), EditOutputName.For(Path.Combine(klasor, "a.MOV"), _ => false));
        Assert.Equal(Path.Combine(klasor, "b-duzenlenmis.mkv"), EditOutputName.For(Path.Combine(klasor, "b.webm"), _ => false));
        Assert.Equal(Path.Combine(klasor, "c-duzenlenmis.mkv"), EditOutputName.For(Path.Combine(klasor, "c.avi"), _ => false));
    }

    [Fact]
    public void DuzenleKomutuDuzenleyiciyeDosyayiVeKonumuVerir()
    {
        Onceki("pencere");
        var kok = Path.Combine(Kanit, "pencere");
        Directory.CreateDirectory(kok);
        var klip = Path.Combine(kok, "sahte.mp4");
        var kayit = Path.Combine(kok, "kayit.mp4");
        File.WriteAllBytes(klip, new byte[16]);
        File.WriteAllBytes(kayit, new byte[16]);

        var s = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = Path.Combine(kok, "settings.json"), Width = 1280, Height = 800, WindowState = WindowState.Normal };
            var view = window.PlayerTab;
            view.EngineFactory = () => new YolMotoru();
            var editor = window.EditorPaneForTest;
            editor.Player.EngineFactory = () => new YolMotoru();
            window.Show();
            try
            {
                DenetimSurucu.Wait(view, 0.2);
                var komut = Keymap.ForKey(Key.E, KeyModifiers.None, null);
                view.Apply(komut);
                DenetimSurucu.Wait(view, 0.1);
                var bosIz = view.Trace.LastOrDefault();
                var bosSekme = window.Tabs.SelectedIndex;
                var bosDugme = view.BtnSeritEdit.IsEnabled;

                var ac = window.OpenInPlayerAsync(klip);
                DenetimSurucu.Pump(view, () => ac.IsCompleted, 10);
                if (view.IsPlaying) view.Apply(Keymap.PlayPause.ToCommand());
                view.Apply(new PlayerCommand(PlayerCommandKind.Seek, 42));
                DenetimSurucu.Pump(view, () => Math.Abs(view.CurrentPosition() - 42) < 0.01, 5);
                var dugme = (view.BtnSeritEdit.IsVisible, view.BtnSeritEdit.IsEnabled);

                view.Apply(komut);
                DenetimSurucu.Pump(view, () => editor.Model is not null && editor.TimelineView.Playhead == EditTime.FromSeconds(42), 10);
                var oynatici = (Sekme: window.Tabs.SelectedIndex, Kaynak: editor.SourcePath, Bas: editor.TimelineView.Playhead, Iz: view.Trace.LastOrDefault());

                var kapi = window.RecorderPaneForTest.OpenInEditor;
                var git = kapi?.Invoke(kayit) ?? Task.CompletedTask;
                DenetimSurucu.Pump(view, () => git.IsCompleted && editor.Model is not null && CurrentMedia.SamePath(editor.SourcePath, kayit), 10);
                var kaydedici = (Sekme: window.Tabs.SelectedIndex, Kaynak: editor.SourcePath, Bas: editor.TimelineView.Playhead);

                return (komut.Kind, bosIz, bosSekme, bosDugme, dugme, oynatici, KapiVar: kapi is not null, kaydedici, Hedef: window.EditorTabIndex);
            }
            finally
            {
                editor.Player.Close();
                view.Close();
                window.Close();
            }
        });

        Assert.Equal(PlayerCommandKind.Edit, s.Kind);
        Assert.Equal("edit -> none", s.bosIz);
        Assert.NotEqual(s.Hedef, s.bosSekme);
        Assert.False(s.bosDugme);
        Assert.Equal((true, true), s.dugme);

        Assert.Equal(s.Hedef, s.oynatici.Sekme);
        Assert.Equal(Path.GetFullPath(klip), s.oynatici.Kaynak);
        Assert.Equal(EditTime.FromSeconds(42), s.oynatici.Bas);
        Assert.StartsWith("edit -> sahte.mp4 @ 42", s.oynatici.Iz, StringComparison.Ordinal);

        Assert.True(s.KapiVar);
        Assert.Equal(s.Hedef, s.kaydedici.Sekme);
        Assert.Equal(kayit, s.kaydedici.Kaynak);
        Assert.Equal(0L, s.kaydedici.Bas);

        Kapat("pencere");
    }

    [Fact]
    public void KaydediciDuzenleDugmesiTeslimEdilenKaydiVerir()
    {
        Onceki("kaydedici.mp4");
        var dosya = Path.Combine(Kanit, "kaydedici.mp4");
        File.WriteAllBytes(dosya, new byte[16]);

        var (once, sonra, xaml) = AppHost.Run(() =>
        {
            var view = new RecorderView { SkipAutoMeasure = true, RevealFolder = _ => { } };
            var window = new Window { Width = 1100, Height = 900, Content = view };
            window.Show();
            try
            {
                var gelen = new List<string>();
                view.OpenInEditor = yol => { gelen.Add(yol); return Task.CompletedTask; };
                view.BtnToEditor.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var bos = gelen.Count;
                view.Deliver(new RecordResult(true, dosya, 1, false, 0, string.Empty, 1));
                view.BtnToEditor.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return (bos, gelen.ToList(), view.BtnToEditor.IsEnabled);
            }
            finally { window.Close(); }
        });

        Assert.Equal(0, once);
        Assert.Equal(new[] { dosya }, sonra);
        Assert.True(xaml);

        Kapat("kaydedici.mp4");
    }

    [Fact]
    public void KaydetAkilliKiplaKaynaginYaninaYazarDuzenlemedenSonraYeniAdAlir()
    {
        Onceki("kaydet");
        var kok = Path.Combine(Kanit, "kaydet");
        Directory.CreateDirectory(kok);
        var kaynak = Kaynak(kok);

        var s = AppHost.Run(() =>
        {
            var (view, window) = Ac();
            try
            {
                Bekle(view, view.OpenSourceAsync(kaynak), 10);
                var ilk = view.SaveAsync();
                Bekle(view, ilk, 60);
                var birinci = (Ok: ilk.Result, Plan: view.LastPlan, Kayitli: view.SavedPath, Sayi: view.Exports, Durum: view.ExportStatusText);

                var tekrar = view.SaveAsync();
                Bekle(view, tekrar, 10);
                var tekrarSayi = view.Exports;

                view.TimelineView.Playhead = EditTime.FromSeconds(3);
                view.Split();
                view.TimelineView.SelectedIndex = 1;
                view.DeleteSelected();
                var duzenlenince = view.SavedPath;

                var ikinci = view.SaveAsync();
                Bekle(view, ikinci, 60);
                return (birinci, tekrarSayi, duzenlenince, Ikinci: (Ok: ikinci.Result, Plan: view.LastPlan, Sayi: view.Exports, Kayitli: view.SavedPath));
            }
            finally
            {
                view.Player.Close();
                window.Close();
            }
        });

        var ilkCikti = Path.Combine(kok, "kaynak-duzenlenmis.mp4");
        var ikinciCikti = Path.Combine(kok, "kaynak-duzenlenmis-2.mp4");

        Assert.True(s.birinci.Ok, s.birinci.Durum);
        Assert.Equal(ExportMode.Smart, s.birinci.Plan!.Requested);
        Assert.Equal(ilkCikti, s.birinci.Plan.OutputPath);
        Assert.Equal(ilkCikti, s.birinci.Kayitli);
        Assert.Contains(ilkCikti, s.birinci.Durum, StringComparison.Ordinal);
        Assert.Equal(1, s.birinci.Sayi);
        Assert.InRange(Sure(ilkCikti), 5.8, 6.2);

        Assert.Equal(1, s.tekrarSayi);
        Assert.Null(s.duzenlenince);

        Assert.True(s.Ikinci.Ok);
        Assert.Equal(ExportMode.Smart, s.Ikinci.Plan!.Requested);
        Assert.Equal(ikinciCikti, s.Ikinci.Plan.OutputPath);
        Assert.Equal(ikinciCikti, s.Ikinci.Kayitli);
        Assert.Equal(2, s.Ikinci.Sayi);
        Assert.InRange(Sure(ikinciCikti), 2.8, 3.2);

        Kapat("kaydet");
    }

    [Fact]
    public void PaylasKaydedilmemisiOnceKaydederSonraYenidenKodlamadanYukler()
    {
        Onceki("paylas");
        var kok = Path.Combine(Kanit, "paylas");
        Directory.CreateDirectory(kok);
        var kaynak = Kaynak(kok);
        var ledger = Path.Combine(kok, "paylasimlar.json");

        var s = AppHost.Run(() =>
        {
            var (view, window) = Ac();
            SahteSaglayici? saglayici = null;
            view.CreateShareFlow = () => new ShareFlow(t => saglayici ??= new SahteSaglayici(t), new ShareLedger(ledger));
            try
            {
                Bekle(view, view.OpenSourceAsync(kaynak), 10);
                var ilk = view.ShareAsync();
                Bekle(view, ilk, 60);
                var birinci = (Ok: ilk.Result, Sayi: view.Exports, Baglanti: view.ShareLinkText, Kayitli: view.SavedPath);

                var ikinci = view.ShareAsync();
                Bekle(view, ikinci, 10);
                return (birinci, IkinciOk: ikinci.Result, IkinciSayi: view.Exports, Yuklenen: saglayici?.Yuklenen.ToList() ?? new List<string>());
            }
            finally
            {
                view.Player.Close();
                window.Close();
            }
        });

        var cikti = Path.Combine(kok, "kaynak-duzenlenmis.mp4");
        Assert.True(s.birinci.Ok);
        Assert.Equal(1, s.birinci.Sayi);
        Assert.Equal(cikti, s.birinci.Kayitli);
        Assert.Equal("https://ornek.test/f1", s.birinci.Baglanti);
        Assert.True(s.IkinciOk);
        Assert.Equal(1, s.IkinciSayi);
        Assert.Equal(new[] { cikti, cikti }, s.Yuklenen);

        Kapat("paylas");
    }

    [Theory]
    [InlineData("player.menu.edit")]
    [InlineData("recorder.output.to-editor")]
    [InlineData("editor.save")]
    [InlineData("editor.save-as")]
    public void YeniAnahtarlarButunDillerde(string key)
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var keys = VidShrink.App.Localization.Strings.KeysOf(language).ToHashSet(StringComparer.Ordinal);
            Assert.True(keys.Contains(key), $"{language} dilinde {key} yok.");
            Assert.False(string.IsNullOrWhiteSpace(VidShrink.App.Localization.Strings.GetIn(language, key)), $"{language} dilinde {key} boş.");
            Assert.DoesNotContain("editor.export", keys);
        }
    }

    private static (EditorView View, Window Window) Ac()
    {
        var view = new EditorView { RevealFolder = _ => { } };
        view.Player.EngineFactory = () => new YolMotoru(sure: 6);
        var window = new Window { Width = 1100, Height = 700, Content = view };
        window.Show();
        return (view, window);
    }

    private static void Bekle(EditorView view, Task is_, double saniye)
    {
        DenetimSurucu.Pump(view.Player, () => is_.IsCompleted, saniye);
        Assert.True(is_.IsCompleted, $"{saniye} sn icinde bitmedi: {view.ExportStatusText}");
    }

    private static string Kaynak(string klasor)
    {
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[]
                 {
                     "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=6", "-f", "lavfi", "-i", "sine=f=440:d=6",
                     "-c:v", "libx264", "-preset", "ultrafast", "-g", "30", "-threads", "1", "-c:a", "aac", "-shortest", kaynak
                 })
            psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(30000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 30 sn icinde bitmedi.");
        }

        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
        return kaynak;
    }

    private static double Sure(string dosya)
    {
        var psi = new ProcessStartInfo("ffprobe", $"-v error -show_entries format=duration -of csv=p=0 \"{dosya}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var surec = Process.Start(psi)!;
        var metin = surec.StandardOutput.ReadToEnd();
        Assert.True(surec.WaitForExit(15000));
        return double.Parse(metin.Trim(), CultureInfo.InvariantCulture);
    }

    private static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Kanit, adlar);

    private static void Onceki(params string[] adlar) => KanitKapanisi.Onceki(Kanit, adlar);

    private sealed class SahteSaglayici : IShareProvider
    {
        public SahteSaglayici(ShareTarget target) => Target = target;

        public List<string> Yuklenen { get; } = new();

        public ShareTarget Target { get; }

        public bool CanDelete => false;

        public Task<ShareResult> UploadAsync(string filePath, int? retentionDays = null, IProgress<UploadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Yuklenen.Add(filePath);
            return Task.FromResult(ShareResult.Success(new ShareLink(Target.Id, "f1", "https://ornek.test/f1", Path.GetFileName(filePath), DateTimeOffset.UtcNow)));
        }

        public Task<ShareResult> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ShareResult.Success(new ShareLink(Target.Id, "h", "https://ornek.test/h", "h", DateTimeOffset.UtcNow)));

        public Task<ShareResult> DeleteAsync(ShareLink link, CancellationToken cancellationToken = default)
            => Task.FromResult(ShareResult.Success(link));
    }
}
