using System.Text.Json;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Düzenleyici teslimindeki "Meta veriyi sil" kutusu (teslim seçenekleri açılırında): argümanlar küçültmeyle
/// aynı fonksiyondan (<see cref="StreamMapping.MetadataArguments"/>) gelir, yalnız teslim dosyasını yazan son
/// adıma, çıktı yolundan hemen önce eklenir; seçim ana pencerenin ayarında <c>editorDropMetadata</c> olarak
/// saklanır. Düzenleyici kendi başına etiket ya da bölüm yazmaz, silinen yalnız kaynaktan taşınandır.
/// Ölçüm <c>docs/olcumler/meta-sil-donustur-duzenleyici.md</c>.
/// </summary>
public sealed class MetaSilDuzenleyiciTests
{
    private const long Gb = 1024L * 1024 * 1024;

    private static readonly string Kok = Path.Combine(GirdiKanit.Root, ".calisma", "meta-sil-duzenleyici");

    private static readonly string AyarKlasoru = Path.Combine(TestPaths.OutputRoot, "meta-sil-duzenleyici");

    private static readonly double[] Kareler = { 0.0, 2, 4, 6, 8 };

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string AyarDosyasi()
    {
        Directory.CreateDirectory(AyarKlasoru);
        foreach (var eski in Directory.GetFiles(AyarKlasoru, "settings-*.json")) File.Delete(eski);
        return Path.Combine(AyarKlasoru, "settings-" + Guid.NewGuid().ToString("N") + ".json");
    }

    private static MediaInfo Bilgi(bool sesli = true)
    {
        var akislar = sesli
            ? new[] { Video, new SourceStream(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000) }
            : new[] { Video };
        return Kaynak(8, akislar) with { FilePath = @"C:\Kayitlar\gezi.mp4", Fps = 30, PixelFormat = "yuv420p", FormatName = "mov,mp4,m4a,3gp,3g2,mj2" };
    }

    private static ExportPlan Plan(ExportMode kip, bool sil, EditTimeline? cizelge = null, MediaInfo? bilgi = null)
        => EditExport.Build(cizelge ?? new EditTimeline(new[] { new EditClip(S(2), S(6)) }), bilgi ?? Bilgi(), Kareler, 0, kip,
            "cikti.mp4", "is", 8 * Gb, null, null, sil);

    /// <summary>
    /// Üç kipte de açık kutu yalnız son adımı değiştirir: son adımın komutu, çıktı yolundan hemen önce
    /// küçültmenin dizisi eklenmiş halidir; ara adımlar ve seçilen kip aynı kalır. Kapalı planın hiçbir
    /// adımında <c>-map_metadata -1</c> yoktur (olumsuz kontrol).
    /// </summary>
    [Theory]
    [InlineData(ExportMode.Fast)]
    [InlineData(ExportMode.Smart)]
    [InlineData(ExportMode.Full)]
    public void AcikKutuYalnizSonAdimaKucultmeninDizisiniYaziyor(ExportMode kip)
    {
        var kapali = Plan(kip, false);
        var acik = Plan(kip, true);

        Assert.Equal(kapali.Effective, acik.Effective);
        Assert.Equal(kapali.Steps.Count, acik.Steps.Count);
        for (var i = 0; i < kapali.Steps.Count - 1; i++) Assert.Equal(kapali.Steps[i].Args, acik.Steps[i].Args);
        Assert.All(kapali.Steps, adim => Assert.DoesNotContain("-1", adim.Args.SkipWhile(a => a != "-map_metadata").Skip(1).Take(1)));

        var son = kapali.Steps[^1].Args;
        Assert.Equal("cikti.mp4", son[^1]);
        var dizi = StreamMapping.MetadataArguments(true, new[] { "tur" }, Array.Empty<string?>());
        Assert.Equal(son.Take(son.Count - 1).Concat(dizi).Append("cikti.mp4"), acik.Steps[^1].Args);
        Assert.Equal("-1", Sonraki(acik.Steps[^1].Args, "-map_metadata"));
        Assert.Equal("language=tur", Sonraki(acik.Steps[^1].Args, "-metadata:s:a:0"));
    }

    /// <summary>Sessiz kaynakta dil satırı yazılmaz; parçalara ayrılan teslimde her parçanın son adımı diziyi taşır.</summary>
    [Fact]
    public void SessizKaynakDilYazmazParcalarinHepsiTasir()
    {
        var sessiz = Plan(ExportMode.Full, true, bilgi: Bilgi(false) with { AudioCodec = null });
        Assert.DoesNotContain(sessiz.Steps[^1].Args, arg => arg.StartsWith("-metadata:s:a", StringComparison.Ordinal));
        Assert.Equal("-1", Sonraki(sessiz.Steps[^1].Args, "-map_metadata"));

        var cizelge = new EditTimeline(new[] { new EditClip(S(0), S(2)), new EditClip(S(4), S(6)) });
        IReadOnlyList<ExportPlan> Parcalar(bool sil) => EditExport.BuildSegments(cizelge, Bilgi(), Kareler, 0, ExportMode.Full,
            new[] { "a-01.mp4", "a-02.mp4" }, new[] { "is1", "is2" }, 8 * Gb, null, null, sil);

        var acik = Parcalar(true);
        Assert.Equal(2, acik.Count);
        Assert.All(acik, plan => Assert.Equal("-1", Sonraki(plan.Steps[^1].Args, "-map_metadata")));
        Assert.All(Parcalar(false), plan => Assert.DoesNotContain("-map_metadata", plan.Steps[^1].Args));
    }

    /// <summary>
    /// Kutu ana pencerenin ayarına köprülenir: düzenleyicide açılınca <c>editorDropMetadata</c> yazılır; yeni
    /// pencerede ayar hem düzenleyici kurulmadan önce (tembel sekme) hem kurulduktan sonra kutuya iner.
    /// Anahtarsız eski dosya kutuyu kapatır, sıfırlama da.
    /// </summary>
    [Fact]
    public void SecimAnaPencereninAyarinaYaziliyorGeriGeliyor()
    {
        var dosya = AyarDosyasi();
        try
        {
            var (yazilan, varsayilan, tembel, kurulu, eski, sifir) = AppHost.Run(() =>
            {
                var ilk = new MainWindow { SettingsPathOverride = dosya };
                bool bas;
                try
                {
                    bas = ilk.EditorPaneForTest.DropMetadata;
                    ilk.EditorPaneForTest.DropMetadata = true;
                }
                finally { ilk.Close(); }
                var metin = File.ReadAllText(dosya);

                bool once;
                var ikinci = new MainWindow { SettingsPathOverride = dosya };
                try
                {
                    ikinci.RestoreAppSettingsForTest(AppSettings.Load(dosya));
                    once = ikinci.EditorPaneForTest.DropMetadata;
                }
                finally { ikinci.Close(); }

                var ucuncu = new MainWindow { SettingsPathOverride = dosya };
                try
                {
                    var pane = ucuncu.EditorPaneForTest;
                    var ilkHal = pane.DropMetadata;
                    ucuncu.RestoreAppSettingsForTest(AppSettings.Load(dosya));
                    var sonra = (Once: ilkHal, Sonra: pane.DropMetadata, Ayar: ucuncu.CaptureAppSettingsForTest().EditorDropMetadata);

                    ucuncu.RestoreAppSettingsForTest(new AppSettings { AdvDropMetadata = true, ConvertDropMetadata = true });
                    var eskiHal = pane.DropMetadata;

                    pane.DropMetadata = true;
                    var dolu = ucuncu.CaptureAppSettingsForTest().EditorDropMetadata;
                    ucuncu.ConfirmResetSettingsForTest();
                    return (metin, bas, once, sonra, eskiHal, (Once: dolu, Sonra: pane.DropMetadata));
                }
                finally { ucuncu.Close(); }
            });

            using var doc = JsonDocument.Parse(yazilan);
            Assert.True(doc.RootElement.GetProperty("editorDropMetadata").GetBoolean());
            Assert.False(doc.RootElement.GetProperty("convertDropMetadata").GetBoolean());
            Assert.False(varsayilan);
            Assert.True(tembel);
            Assert.Equal((false, true, true), kurulu);
            Assert.False(eski);
            Assert.Equal((true, false), sifir);

            File.WriteAllText(dosya, "{\"advDropMetadata\":true}");
            Assert.False(AppSettings.Load(dosya).EditorDropMetadata);
            new AppSettings { EditorDropMetadata = true }.Save(dosya);
            Assert.True(AppSettings.Load(dosya).EditorDropMetadata);
        }
        finally
        {
            if (File.Exists(dosya)) File.Delete(dosya);
        }
    }

    private static async Task<(string Baslik, string Dil)> EtiketlerAsync(string yol)
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe,
            new[] { "-v", "error", "-show_entries", "format_tags=title:stream=codec_type:stream_tags=language", "-of", "json", yol });
        Assert.Equal(0, sonuc.Code);
        using var doc = JsonDocument.Parse(sonuc.Out);
        var baslik = doc.RootElement.GetProperty("format").TryGetProperty("tags", out var tags)
            && tags.TryGetProperty("title", out var deger) ? deger.GetString() ?? "" : "";
        var ses = doc.RootElement.GetProperty("streams").EnumerateArray()
            .First(stream => stream.GetProperty("codec_type").GetString() == "audio");
        return (baslik, Etiket(ses, "language"));
    }

    private static void Bekle(EditorView view, Task gorev, double saniye)
    {
        DenetimSurucu.Pump(view.Player, () => gorev.IsCompleted, saniye);
        Assert.True(gorev.IsCompleted, $"{saniye} sn icinde bitmedi: {view.ExportStatusText}");
    }

    /// <summary>
    /// Canlı kol, başlıklı ve Türkçe sesli 4 sn'lik kaynak, iki kipte kutu kapalı ve açık dört teslim.
    /// Ölçülen: Akıllı kipte son adım concat listesinden okur, başlık kutu kapalıyken de taşınmaz ve dil kalır;
    /// Tam kipte kaynak doğrudan girdidir, kutu kapalıyken başlığı taşır ama süzgeç grafından çıkan sesin dili
    /// "und" olur, kutu açıkken başlık düşer ve dil açıkça yazıldığı için "tur" okunur.
    /// Kutu değişince aynı düzenleme yeniden yazılır (kayıt anahtarı değişir), değişmeyince yazılmaz. Kutu tek
    /// parçada da kullanılabilir, "ayrı dosyalar" kutusu tek parçada pasif kalır.
    /// </summary>
    [FfmpegFact]
    public async Task CanliKutuAcilincaYenidenYazilirBaslikDuserDilKalir()
    {
        KanitKapanisi.Kapat(Kok, "canli");
        var kok = Path.Combine(Kok, "canli");
        Directory.CreateDirectory(kok);
        try
        {
            var kaynak = Path.Combine(kok, "kaynak.mp4");
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y", "-threads", "2",
                "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=4", "-f", "lavfi", "-i", "sine=f=440:d=4",
                "-c:v", "libx264", "-preset", "ultrafast", "-g", "30", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest",
                "-metadata", "title=Gizli Baslik", "-metadata:s:a:0", "language=tur", kaynak
            });
            Assert.Equal(("Gizli Baslik", "tur"), await EtiketlerAsync(kaynak));

            var s = AppHost.Run(() =>
            {
                var view = new EditorView { RevealFolder = _ => { } };
                view.Player.EngineFactory = () => new YolMotoru(sure: 4);
                var bildirilen = new List<bool>();
                view.DropMetadataChanged = bildirilen.Add;
                var window = new Window { Width = 1100, Height = 700, Content = view };
                window.Show();
                try
                {
                    Bekle(view, view.OpenSourceAsync(kaynak), 10);
                    var secenekler = (Dugme: view.BtnExportOptions.IsEnabled, Ayri: view.ExportSeparatelyEnabled);

                    (bool Ok, string? Yol, int Sayi, ExportMode Kip, string[] Args) Kaydet(ExportMode kip, bool sil)
                    {
                        view.SelectedExportMode = kip;
                        view.DropMetadata = sil;
                        var gorev = view.SaveAsync();
                        Bekle(view, gorev, 90);
                        return (gorev.Result, view.ExportedPath, view.Exports, view.LastPlan!.Effective, view.LastPlan!.Steps[^1].Args.ToArray());
                    }

                    var akilliKapali = Kaydet(ExportMode.Smart, false);
                    var akilliAcik = Kaydet(ExportMode.Smart, true);
                    var tamAcik = Kaydet(ExportMode.Full, true);
                    var tamKapali = Kaydet(ExportMode.Full, false);
                    var tekrar = Kaydet(ExportMode.Full, false);
                    return (secenekler, akilliKapali, akilliAcik, tamAcik, tamKapali, Tekrar: tekrar.Sayi,
                        Bildirilen: bildirilen.ToArray(), Durum: view.ExportStatusText);
                }
                finally
                {
                    view.Player.Close();
                    window.Close();
                }
            });

            Assert.Equal((true, false), s.secenekler);
            var teslimler = new[] { s.akilliKapali, s.akilliAcik, s.tamAcik, s.tamKapali };
            Assert.All(teslimler, teslim => Assert.True(teslim.Ok, s.Durum));
            Assert.Equal(new[] { 1, 2, 3, 4 }, teslimler.Select(teslim => teslim.Sayi));
            Assert.Equal(4, s.Tekrar);
            Assert.Equal(4, teslimler.Select(teslim => teslim.Yol).Distinct().Count());
            Assert.Equal(new[] { true, false }, s.Bildirilen);
            Assert.NotEqual(ExportMode.Full, s.akilliKapali.Kip);
            Assert.Equal(ExportMode.Full, s.tamKapali.Kip);

            foreach (var kapali in new[] { s.akilliKapali, s.tamKapali }) Assert.DoesNotContain("-map_metadata", kapali.Args);
            foreach (var acik in new[] { s.akilliAcik, s.tamAcik })
            {
                Assert.Equal("-1", Sonraki(acik.Args, "-map_metadata"));
                Assert.Equal("language=tur", Sonraki(acik.Args, "-metadata:s:a:0"));
            }

            var okunan = new List<(string Baslik, string Dil)>();
            foreach (var teslim in teslimler) okunan.Add(await EtiketlerAsync(teslim.Yol!));
            Assert.Equal(new[] { ("", "tur"), ("", "tur"), ("", "tur"), ("Gizli Baslik", "und") }, okunan);
        }
        finally { KanitKapanisi.Kapat(Kok, "canli"); }
    }
}
