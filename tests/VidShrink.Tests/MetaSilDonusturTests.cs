using System.Text.Json;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Dönüştür sekmesindeki "Meta veriyi sil" kutusu: argümanlar küçültmeyle aynı fonksiyondan
/// (<see cref="StreamMapping.MetadataArguments"/>) gelir, çıktı yolundan hemen önce yazılır ve seçim
/// <c>convertDropMetadata</c> olarak saklanır. Kapalı planın komutu kutu eklenmeden önceki komuttur.
/// Ölçüm <c>docs/olcumler/meta-sil-donustur-duzenleyici.md</c>.
/// </summary>
public sealed class MetaSilDonusturTests
{
    private static readonly string Klasor = Path.Combine(TestPaths.OutputRoot, "meta-sil-donustur");

    private static string AyarDosyasi()
    {
        Directory.CreateDirectory(Klasor);
        foreach (var eski in Directory.GetFiles(Klasor, "settings-*.json")) File.Delete(eski);
        return Path.Combine(Klasor, "settings-" + Guid.NewGuid().ToString("N") + ".json");
    }

    private static void Sil(string dosya)
    {
        if (File.Exists(dosya)) File.Delete(dosya);
    }

    private static readonly SourceStream Tur = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);

    private static MediaInfo Ornek(params SourceStream[] streams)
        => Kaynak(62, streams.Length == 0 ? new[] { Video, Tur } : streams) with { FilePath = @"C:\Kayitlar\gezi.mp4" };

    private static ConversionPlan Plan(string kap, string? video, string? ses, bool sil, int crf = 23)
        => new() { Container = kap, VideoCodec = video ?? "libx264", AudioCodec = ses, Crf = crf, DropMetadata = sil };

    private static List<string> Kur(MediaInfo info, ConversionPlan plan, string cikti)
        => ConversionArguments.Build(info, plan, cikti, plan.Gif ? "palet.png" : null).ToList();

    /// <summary>
    /// Açık planın komutu kapalı planın komutuna, çıktı yolundan hemen önce, küçültmenin kullandığı
    /// dizinin eklenmiş halidir; kapalı plan <c>-map_metadata</c> yazmaz. Video, yalnız ses ve hareketli
    /// görsel kollarının üçü de aynı kurala uyar.
    /// </summary>
    [Theory]
    [InlineData("mp4", "libx264", "aac", "cikti.mp4", true)]
    [InlineData("mkv", "libx265", "copy", "cikti.mkv", true)]
    [InlineData("mp3", null, "libmp3lame", "cikti.mp3", true)]
    [InlineData("webp", "libwebp_anim", null, "cikti.webp", false)]
    public void AcikPlanKucultmeninDizisiniCiktiYolundanOnceYaziyor(string kap, string? video, string? ses, string cikti, bool dilYazilir)
    {
        var info = Ornek();
        var once = Kur(info, Plan(kap, video, ses, false), cikti);
        var sonra = Kur(info, Plan(kap, video, ses, true), cikti);

        var dizi = StreamMapping.MetadataArguments(true, dilYazilir ? new[] { "tur" } : Array.Empty<string?>(), Array.Empty<string?>());
        Assert.DoesNotContain("-map_metadata", once);
        Assert.Equal(once.Take(once.Count - 1).Concat(dizi).Append(cikti), sonra);
        Assert.Equal(dilYazilir, sonra.Contains("language=tur"));
        Assert.Equal("-1", Sonraki(sonra, "-map_metadata"));
    }

    /// <summary>GIF etiket taşımaz; kutu açıkken de iki geçişin komutu değişmez.</summary>
    [Fact]
    public void GifKomutuDegismiyor()
    {
        Assert.Equal(Kur(Ornek(), Plan("gif", null, null, false), "cikti.gif"), Kur(Ornek(), Plan("gif", null, null, true), "cikti.gif"));
    }

    /// <summary>
    /// Altyazı dili yalnız kaynakta tek altyazı izi varken yazılır; iki izde ve altyazısız kaynakta
    /// yazılmaz. Dili olmayan ses izi için de satır çıkmaz.
    /// </summary>
    [Fact]
    public void AltyaziDiliYalnizTekIzdeYaziliyor()
    {
        var plan = new ConversionPlan { Container = "mkv", VideoCodec = "libx264", AudioCodec = "aac", DropMetadata = true };
        var deu = new SourceStream(2, StreamKind.Subtitle, "subrip", "deu");
        var fra = new SourceStream(3, StreamKind.Subtitle, "subrip", "fra");

        var tek = Kur(Ornek(Video, Tur, deu), plan, "c.mkv");
        var iki = Kur(Ornek(Video, Tur, deu, fra), plan, "c.mkv");
        var dilsiz = Kur(Ornek(Video, Tur with { Language = null }), plan, "c.mkv");

        Assert.Equal("language=deu", Sonraki(tek, "-metadata:s:s:0"));
        Assert.DoesNotContain("-metadata:s:s:0", iki);
        Assert.Equal("language=tur", Sonraki(iki, "-metadata:s:a:0"));
        Assert.DoesNotContain("-metadata:s:a:0", dilsiz);
        Assert.Equal("-1", Sonraki(dilsiz, "-map_metadata"));
    }

    /// <summary>
    /// <c>-map</c> yokken ffmpeg'in seçtiği ses: varsayılan işaretli iz, yoksa en çok kanallı, eşitlikte ilk.
    /// Dil o izden yazılır.
    /// </summary>
    [Fact]
    public void DilFfmpeginSectigiSestenYaziliyor()
    {
        var tek = new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 1);
        var cift = new SourceStream(2, StreamKind.Audio, "aac", "tur", Channels: 2);
        var esit = new SourceStream(3, StreamKind.Audio, "aac", "deu", Channels: 2);

        Assert.Equal("tur", ConversionArguments.AutoAudio(Ornek(Video, tek, cift))!.Language);
        Assert.Equal("eng", ConversionArguments.AutoAudio(Ornek(Video, tek with { IsDefault = true }, cift))!.Language);
        Assert.Equal("tur", ConversionArguments.AutoAudio(Ornek(Video, cift, esit))!.Language);
        Assert.Null(ConversionArguments.AutoAudio(Ornek(Video)));

        var plan = new ConversionPlan { Container = "mp4", VideoCodec = "libx264", AudioCodec = "aac", DropMetadata = true };
        Assert.Equal("language=eng", Sonraki(Kur(Ornek(Video, tek with { IsDefault = true }, cift), plan, "c.mp4"), "-metadata:s:a:0"));
    }

    /// <summary>
    /// Pencere kolu: kutu varsayılan kapalıdır ve komut etiket argümanı taşımaz; açılınca komuta küçültmenin
    /// dizisi girer, kapanınca komut ilk haline döner.
    /// </summary>
    [Fact]
    public void KutuPencereninKomutunaIniyor()
    {
        var dosya = AyarDosyasi();
        try
        {
            var (varsayilan, once, acik, kapali) = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = dosya };
                try
                {
                    var info = Ornek();
                    window.LoadWithoutProbing(info.FilePath, info);
                    var ilk = window.ChkConvertDropMetadata.IsChecked == true;
                    window.ChkConvertDropMetadata.IsChecked = true;
                    window.ChkConvertDropMetadata.IsChecked = false;
                    var bos = window.TxtConvertCommand.Text ?? "";
                    window.ChkConvertDropMetadata.IsChecked = true;
                    var dolu = window.TxtConvertCommand.Text ?? "";
                    window.ChkConvertDropMetadata.IsChecked = false;
                    return (ilk, bos, dolu, window.TxtConvertCommand.Text ?? "");
                }
                finally { window.Close(); }
            });

            Assert.False(varsayilan);
            Assert.NotEqual("", once);
            Assert.DoesNotContain("-map_metadata", once, StringComparison.Ordinal);
            Assert.Contains("-metadata:s:a:0 language=tur -map_metadata -1 ", acik, StringComparison.Ordinal);
            Assert.Equal(once, kapali);
        }
        finally { Sil(dosya); }
    }

    /// <summary>
    /// Seçim ayar dosyasına yazılır ve yeni pencerede geri gelir; anahtarı taşımayan eski dosya kutuyu
    /// kapalı açar, "Ayarları sıfırla" kutuyu kapatır. Küçült'ün kutusu bu seçimden bağımsızdır.
    /// </summary>
    [Fact]
    public void SecimSaklanipGeriGeliyorSifirlamaKapatiyor()
    {
        var dosya = AyarDosyasi();
        try
        {
            var (yazilan, once, sonra, eskiDosya, sifir) = AppHost.Run(() =>
            {
                var ilk = new MainWindow { SettingsPathOverride = dosya };
                try { ilk.ChkConvertDropMetadata.IsChecked = true; }
                finally { ilk.Close(); }
                var metin = File.ReadAllText(dosya);

                var ikinci = new MainWindow { SettingsPathOverride = dosya };
                try
                {
                    var bas = ikinci.ChkConvertDropMetadata.IsChecked == true;
                    ikinci.RestoreAppSettingsForTest(AppSettings.Load(dosya));
                    var geri = (Donustur: ikinci.ChkConvertDropMetadata.IsChecked == true, Kucult: ikinci.ChkAdvDropMetadata.IsChecked == true);

                    File.WriteAllText(dosya, "{\"advDropMetadata\":true}");
                    ikinci.RestoreAppSettingsForTest(AppSettings.Load(dosya));
                    var eski = (Donustur: ikinci.ChkConvertDropMetadata.IsChecked == true, Kucult: ikinci.ChkAdvDropMetadata.IsChecked == true);

                    ikinci.ChkConvertDropMetadata.IsChecked = true;
                    var dolu = ikinci.CaptureAppSettingsForTest().ConvertDropMetadata;
                    ikinci.ConfirmResetSettingsForTest();
                    return (metin, bas, geri, eski, (Once: dolu, Sonra: ikinci.CaptureAppSettingsForTest().ConvertDropMetadata));
                }
                finally { ikinci.Close(); }
            });

            using var doc = JsonDocument.Parse(yazilan);
            Assert.True(doc.RootElement.GetProperty("convertDropMetadata").GetBoolean());
            Assert.False(doc.RootElement.GetProperty("advDropMetadata").GetBoolean());
            Assert.False(once);
            Assert.Equal((true, false), sonra);
            Assert.Equal((false, true), eskiDosya);
            Assert.Equal((true, false), sifir);
        }
        finally { Sil(dosya); }
    }

    /// <summary>
    /// Ortak ipucu 42 dilde dolu ve her dilde Küçült ipucunun ilk iki maddesidir (silinen, kalan);
    /// "yeniden kodlanır" maddesi Dönüştür ve düzenleyici için doğru olmadığından taşınmaz.
    /// </summary>
    [Fact]
    public void IpucuButunDillerdeIkiMadde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            Assert.True(values.TryGetValue("main.convert.drop-metadata.tip", out var ipucu) && ipucu.Length > 0, language);
            var kucult = values["main.advanced.drop-metadata.tip"].Split('\n');
            Assert.Equal(3, kucult.Length);
            Assert.Equal(string.Join('\n', kucult.Take(2)), ipucu);
            Assert.All(ipucu!.Split('\n'), satir => Assert.StartsWith("• ", satir, StringComparison.Ordinal));
        }
    }

    private sealed record Okuma(string Baslik, string VideoBasligi, string[] SesDilleri, int Bolum, string[] BolumAdlari);

    private static async Task<Okuma> OkuAsync(string yol)
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-show_entries",
            "format_tags=title:stream=codec_type:stream_tags=language,title:chapter=start_time:chapter_tags=title", "-of", "json", yol
        });
        Assert.Equal(0, sonuc.Code);
        using var doc = JsonDocument.Parse(sonuc.Out);
        var kok = doc.RootElement;
        var baslik = kok.GetProperty("format").TryGetProperty("tags", out var tags) && tags.TryGetProperty("title", out var deger)
            ? deger.GetString() ?? "" : "";
        var akislar = kok.GetProperty("streams").EnumerateArray().ToList();
        var bolumler = kok.TryGetProperty("chapters", out var liste) ? liste.EnumerateArray().ToList() : new List<JsonElement>();
        return new Okuma(baslik,
            Etiket(akislar.First(a => a.GetProperty("codec_type").GetString() == "video"), "title"),
            akislar.Where(a => a.GetProperty("codec_type").GetString() == "audio").Select(a => Etiket(a, "language")).ToArray(),
            bolumler.Count,
            bolumler.Select(b => Etiket(b, "title")).Where(ad => ad.Length > 0).ToArray());
    }

    internal static async Task<string> EtiketliKaynakAsync(string ad, bool varsayilanTekKanal)
    {
        var kaynak = Yol(ad);
        var bolum = Yol(Path.GetFileNameWithoutExtension(ad) + "-bolum.txt");
        await File.WriteAllTextAsync(bolum,
            ";FFMETADATA1\ntitle=Gizli Baslik\n[CHAPTER]\nTIMEBASE=1/1000\nSTART=0\nEND=1000\ntitle=Bir\n[CHAPTER]\nTIMEBASE=1/1000\nSTART=1000\nEND=2000\ntitle=Iki\n");
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-threads", "2",
            "-f", "lavfi", "-i", "testsrc=size=320x240:rate=24:duration=2",
            "-f", "lavfi", "-i", "sine=frequency=440:duration=2",
            "-f", "lavfi", "-i", "sine=frequency=880:duration=2",
            "-i", bolum,
            "-map", "0:v", "-map", "1:a", "-map", "2:a", "-map_metadata", "3", "-map_chapters", "3",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "12", "-pix_fmt", "yuv420p", "-c:a", "aac", "-ac:a:0", "1", "-ac:a:1", "2",
            "-metadata:s:a:0", "language=eng", "-metadata:s:a:1", "language=tur", "-metadata:s:v:0", "title=VidTitle",
            "-disposition:a:0", varsayilanTekKanal ? "default" : "0", "-disposition:a:1", "0", kaynak
        });
        return kaynak;
    }

    /// <summary>
    /// Canlı kol, 2 sn'lik başlıklı, iki sesli (tek kanal İngilizce, çift kanal Türkçe), iki bölümlü kaynak:
    /// kutu açıkken başlık, video izi başlığı ve bölüm adları düşer; bölüm işaretleri ve çıktıya giren sesin
    /// dili kalır. Kapalı eşi başlığı ve bölüm adlarını taşır. Tek kanallı iz varsayılan işaretliyken ffmpeg
    /// onu seçer ve yazılan dil ona uyar: <see cref="ConversionArguments.AutoAudio"/> ffmpeg'in seçimini izliyor.
    /// </summary>
    [FfmpegTheory]
    [InlineData(false, "tur")]
    [InlineData(true, "eng")]
    public async Task CanliBaslikDusuyorSecilenSesinDiliKaliyor(bool varsayilanTekKanal, string beklenenDil)
    {
        var ek = varsayilanTekKanal ? "v" : "k";
        var adlar = new[] { $"meta-donustur-{ek}-kaynak.mkv", $"meta-donustur-{ek}-kaynak-bolum.txt", $"meta-donustur-{ek}-temiz.mp4", $"meta-donustur-{ek}-tasinan.mp4" };
        Kapat(adlar);
        try
        {
            var kaynak = await EtiketliKaynakAsync(adlar[0], varsayilanTekKanal);
            var asil = await OkuAsync(kaynak);
            Assert.Equal("Gizli Baslik", asil.Baslik);
            Assert.Equal(new[] { "eng", "tur" }, asil.SesDilleri);
            Assert.Equal(new[] { "Bir", "Iki" }, asil.BolumAdlari);

            var info = await FfprobeClient.ProbeAsync(kaynak);
            Assert.Equal(beklenenDil, ConversionArguments.AutoAudio(info)!.Language);

            var temiz = Yol(adlar[2]);
            var tasinan = Yol(adlar[3]);
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, Dar(ConversionArguments.Build(info, Plan("mp4", "libx264", "aac", true, 35), temiz, null)));
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, Dar(ConversionArguments.Build(info, Plan("mp4", "libx264", "aac", false, 35), tasinan, null)));

            var silinen = await OkuAsync(temiz);
            Assert.Equal("", silinen.Baslik);
            Assert.Equal("", silinen.VideoBasligi);
            Assert.Equal(new[] { beklenenDil }, silinen.SesDilleri);
            Assert.Equal(2, silinen.Bolum);
            Assert.Empty(silinen.BolumAdlari);

            var kalan = await OkuAsync(tasinan);
            Assert.Equal("Gizli Baslik", kalan.Baslik);
            Assert.Equal(new[] { beklenenDil }, kalan.SesDilleri);
            Assert.Equal(new[] { "Bir", "Iki" }, kalan.BolumAdlari);
        }
        finally { Kapat(adlar); }
    }

    private static IReadOnlyList<string> Dar(IReadOnlyList<string> args)
        => args.Contains("-threads") ? args : args.Take(args.Count - 1).Concat(new[] { "-threads", "2", args[^1] }).ToArray();
}
