using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// later.md #44: Dönüştür'de bölüm listesi. Hesap saf ölçülür: kırpma başı kadar kayma, kırpma başını içeren
/// bölümün 0'a çekilmesi, sona sığmayanın ve 1 sn'den yakın düşenin atılması. Argüman kolu <c>null</c> listede
/// komutu değiştirmez, boş listede <c>-map_chapters -1</c>, dolu listede son girdiyi ffmetadata olarak ekler ve
/// <c>-t</c>'den önce yazar. Canlı kol <see cref="EncodeRunner.ConvertAsync"/> ile koşar ve bölümleri ffprobe'dan okur.
/// </summary>
public sealed class BolumDuzenleyiciTests
{
    private static ChapterEntry B(double saniye, string ad) => new(TimeSpan.FromSeconds(saniye), ad);

    private static (double Bas, double Son, string Ad)[] Oku(IReadOnlyList<RecorderChapter> liste)
        => liste.Select(c => (c.Start.TotalSeconds, c.End.TotalSeconds, c.Title)).ToArray();

    [Fact]
    public void KirpmayaGoreKayarSigmayaniAtar()
    {
        var giris = new[] { B(30, "Üç"), B(0, "Bir"), B(10, "İki"), B(30.5, "Yakın"), B(59.5, "Son") };
        Assert.Equal(new[] { (0d, 10d, "Bir"), (10d, 30d, "İki"), (30d, 60d, "Üç") },
            Oku(ConversionChapters.ForOutput(giris, TimeSpan.FromSeconds(60), null, null)));

        Assert.Equal(new[] { (0d, 18d, "İki"), (18d, 25d, "Üç") },
            Oku(ConversionChapters.ForOutput(giris, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(37))));

        Assert.Equal(new[] { (0d, 5d, "Yakın") },
            Oku(ConversionChapters.ForOutput(giris, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(31), TimeSpan.FromSeconds(36))));

        Assert.Empty(ConversionChapters.ForOutput(giris, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(40)));
        Assert.Empty(ConversionChapters.ForOutput(Array.Empty<ChapterEntry>(), TimeSpan.FromSeconds(60), null, null));
    }

    private static MediaInfo Ornek() => Kaynak(60, Video) with { FilePath = @"C:\Kayitlar\gezi.mp4" };

    [Theory]
    [InlineData("mp4")]
    [InlineData("mkv")]
    [InlineData("mov")]
    public void ArgumanKoluUcHal(string kap)
    {
        var info = Ornek();
        var cikti = "cikti." + kap;
        var duz = new ConversionPlan { Container = kap, End = TimeSpan.FromSeconds(20) };
        var once = ConversionArguments.Build(info, duz, cikti).ToList();
        Assert.DoesNotContain("-map_chapters", once);

        var bos = ConversionArguments.Build(info, duz with { Chapters = Array.Empty<ChapterEntry>() }, cikti).ToList();
        Assert.DoesNotContain("ffmetadata", bos);
        Assert.Equal("-1", bos[bos.IndexOf("-map_chapters") + 1]);
        Assert.Equal(once.Count + 2, bos.Count);

        var dolu = ConversionArguments.Build(info, duz with { Chapters = new[] { B(0, "Bir"), B(25, "Dışarıda") } }, cikti).ToList();
        var girdi = dolu.IndexOf(ConversionChapters.MetadataPath(cikti));
        Assert.Equal(new[] { "-f", "ffmetadata", "-i" }, dolu.Skip(girdi - 3).Take(3));
        Assert.True(girdi < dolu.IndexOf("-t"));
        Assert.Equal("1", dolu[dolu.IndexOf("-map_chapters") + 1]);
        Assert.Equal(cikti, dolu[^1]);
    }

    [Theory]
    [InlineData("gif")]
    [InlineData("mp3")]
    [InlineData("webm")]
    public void BolumTasimayanKapDokunmaz(string kap)
    {
        var info = kap == "mp3" ? Kaynak(60, Video, new SourceStream(1, StreamKind.Audio, "aac")) : Ornek();
        var duz = new ConversionPlan { Container = kap, VideoCodec = kap == "webm" ? "libvpx-vp9" : "libx264", AudioCodec = kap == "mp3" ? "libmp3lame" : null };
        var palet = kap == "gif" ? "palet.png" : null;
        Assert.Equal(ConversionArguments.Build(info, duz, "c." + kap, palet),
            ConversionArguments.Build(info, duz with { Chapters = new[] { B(0, "Bir") } }, "c." + kap, palet));
        Assert.Null(ConversionChapters.Metadata(info, duz with { Chapters = new[] { B(0, "Bir") } }));
    }

    private static async Task<(double Bas, string Ad)[]> BolumlerAsync(string yol)
        => (await FfprobeClient.ProbeAsync(yol)).Chapters.Select(c => (Math.Round(c.StartSeconds, 1), c.Title ?? "")).ToArray();

    /// <summary>
    /// Canlı kol: iki bölümlü 6 sn'lik kaynak 1 sn'den kırpılır, liste üç bölüme düzenlenir. Çıktıda kaymış üç
    /// bölüm okunur, metin dosyası koşumdan sonra kalmaz; boş liste bölümsüz, <c>null</c> liste kaynağın iki bölümünü verir.
    /// </summary>
    [FfmpegFact]
    public async Task CanliBolumlerYaziliyorDosyaKalmiyor()
    {
        var adlar = new[] { "bolum-duz-kaynak.mkv", "bolum-duz-kaynak-bolum.txt", "bolum-duz-yeni.mp4", "bolum-duz-bos.mp4", "bolum-duz-ayni.mkv" };
        Kapat(adlar);
        try
        {
            var kaynak = Yol(adlar[0]);
            var bolum = Yol(adlar[1]);
            await File.WriteAllTextAsync(bolum, RecorderChapters.Metadata(new[]
            {
                new RecorderChapter(TimeSpan.Zero, TimeSpan.FromSeconds(3), "Bir"),
                new RecorderChapter(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(6), "Iki"),
            }));
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y", "-threads", "2",
                "-f", "lavfi", "-i", "testsrc=size=160x120:rate=12:duration=6",
                "-f", "lavfi", "-i", "sine=frequency=440:duration=6",
                "-i", bolum, "-map", "0:v", "-map", "1:a", "-map_chapters", "2",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", kaynak
            });
            var info = await FfprobeClient.ProbeAsync(kaynak);
            var temel = new ConversionPlan { Container = "mp4", VideoCodec = "libx264", AudioCodec = "aac", Crf = 35, ExtraArgs = new[] { "-threads", "2" } };
            var runner = new EncodeRunner();

            var yeni = Yol(adlar[2]);
            await runner.ConvertAsync(info, temel with { Start = TimeSpan.FromSeconds(1), Chapters = new[] { B(0, "Açılış"), B(1.5, "Yakın"), B(3, "Orta"), B(5.5, "Son") } }, yeni, null);
            Assert.Equal(new[] { (0d, "Açılış"), (2d, "Orta") }, await BolumlerAsync(yeni));
            Assert.False(File.Exists(ConversionChapters.MetadataPath(yeni)));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(yeni)!, "*.ffmeta"));

            var bos = Yol(adlar[3]);
            await runner.ConvertAsync(info, temel with { Chapters = Array.Empty<ChapterEntry>() }, bos, null);
            Assert.Empty(await BolumlerAsync(bos));

            var ayni = Yol(adlar[4]);
            await runner.ConvertAsync(info, temel with { Container = "mkv" }, ayni, null);
            Assert.Equal(new[] { (0d, "Bir"), (3d, "Iki") }, await BolumlerAsync(ayni));
        }
        finally { Kapat(adlar); }
    }

    /// <summary>
    /// Kaynağın başlangıcı 10 sn'de: ffmpeg bölümleri kendisi taşırken o kadar geri çekip hepsini düşürüyordu.
    /// El değmemiş liste artık ffmetadata'dan yazılır; başlangıcı sıfır olan kaynakta komut eskisi gibi bölüme dokunmaz.
    /// </summary>
    [FfmpegFact]
    public async Task BaslangiciKaymisKaynaginBolumleriDusmez()
    {
        var adlar = new[] { "bolum-kayik-kaynak.mkv", "bolum-kayik-bolum.txt", "bolum-kayik-cikti.mp4" };
        Kapat(adlar);
        try
        {
            var kaynak = Yol(adlar[0]);
            var bolum = Yol(adlar[1]);
            await File.WriteAllTextAsync(bolum, RecorderChapters.Metadata(new[]
            {
                new RecorderChapter(TimeSpan.Zero, TimeSpan.FromSeconds(3), "Bir"),
                new RecorderChapter(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(6), "Iki"),
            }));
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y", "-threads", "2",
                "-f", "lavfi", "-i", "testsrc=size=160x120:rate=12:duration=6",
                "-i", bolum, "-map", "0:v", "-map_chapters", "1", "-output_ts_offset", "10",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", kaynak
            });
            var info = await FfprobeClient.ProbeAsync(kaynak);
            Assert.Equal(10, info.StartSeconds, 1);
            var plan = new ConversionPlan { Container = "mp4", VideoCodec = "libx264", Crf = 35, ExtraArgs = new[] { "-threads", "2" } };
            Assert.Contains("ffmetadata", ConversionArguments.Build(info, plan, "c.mp4"));
            Assert.DoesNotContain("-map_chapters", ConversionArguments.Build(info with { StartSeconds = 0 }, plan, "c.mp4"));

            var cikti = Yol(adlar[2]);
            await new EncodeRunner().ConvertAsync(info, plan, cikti, null);
            Assert.Equal(new[] { (0d, "Bir"), (3d, "Iki") }, await BolumlerAsync(cikti));
            Assert.False(File.Exists(ConversionChapters.MetadataPath(cikti)));
        }
        finally { Kapat(adlar); }
    }

    /// <summary>
    /// Pencere kolu: kaynağın iki bölümü satırlara dolar ve dokunulmadıkça komut <c>-map_chapters</c> taşımaz.
    /// Ad değişince komut ffmetadata girdisini alır, okunamayan zaman doğrulama hatası olur, satırların hepsi
    /// silinince <c>-map_chapters -1</c> yazılır. GIF'te açılır gizlenir ve komut bölüme dokunmaz; yeni kaynak
    /// yüklenince düzenleme izi silinir.
    /// </summary>
    [Fact]
    public void AcilirKaynakBolumleriyleDolarDuzenlemeKomutaIner()
    {
        var olcu = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = Path.Combine(TestPaths.OutputRoot, "bolum-duz-" + Guid.NewGuid().ToString("N") + ".json") };
            try
            {
                var info = Ornek() with
                {
                    Chapters = new[] { new ChapterMark(1, 0, 30, "Giriş"), new ChapterMark(2, 30, 60, "Gezi") }
                };
                window.LoadWithoutProbing(info.FilePath, info);
                var satirlar = window.ConvertChapterFields.Select(f => (f.Time.Text ?? "", f.Name.Text ?? "")).ToArray();
                var ilk = (Komut: window.TxtConvertCommand.Text ?? "", window.ConvertChaptersEdited, Gorunur: window.BtnConvertChapters.IsVisible);

                window.ConvertChapterFields[1].Name.Text = "Dönüş";
                var adli = window.TxtConvertCommand.Text ?? "";
                var plan = window.CurrentConversionPlan().Chapters!.Select(c => (c.Start.TotalSeconds, c.Title)).ToArray();

                window.ConvertChapterFields[0].Time.Text = "yarım";
                var hata = (window.TxtConvertValidation.Text ?? "", window.BtnConvert.IsEnabled);
                window.ConvertChapterFields[0].Time.Text = "00:00:00";

                window.AddConvertChapter();
                var eklenen = (Saat: window.ConvertChapterFields[2].Time.Text ?? "", Ad: window.ConvertChapterFields[2].Name.Text ?? "");
                while (window.ConvertChapterFields.Count > 0) window.RemoveConvertChapter(0);
                var bos = (Komut: window.TxtConvertCommand.Text ?? "", Bos: window.TxtConvertChaptersEmpty.IsVisible, Etiket: window.BtnConvertChapters.Content as string);

                window.CmbContainer.SelectedItem = window.CmbContainer.Items.OfType<ComboBoxItem>().First(i => i.Tag as string == "gif");
                var gif = (window.BtnConvertChapters.IsVisible, Komut: window.TxtConvertCommand.Text ?? "");

                window.CmbContainer.SelectedItem = window.CmbContainer.Items.OfType<ComboBoxItem>().First(i => i.Tag as string == "mp4");
                window.LoadWithoutProbing(info.FilePath, info);
                var yeniden = (window.ConvertChaptersEdited, window.ConvertChapterFields.Count, window.CurrentConversionPlan().Chapters is null);
                return (satirlar, ilk, adli, plan, hata, eklenen, bos, gif, yeniden);
            }
            finally { window.Close(); }
        });

        Assert.Equal(new[] { ("00:00:00", "Giriş"), ("00:00:30", "Gezi") }, olcu.satirlar);
        Assert.DoesNotContain("-map_chapters", olcu.ilk.Komut, StringComparison.Ordinal);
        Assert.Equal((false, true), (olcu.ilk.ConvertChaptersEdited, olcu.ilk.Gorunur));
        Assert.Contains("-f ffmetadata -i", olcu.adli, StringComparison.Ordinal);
        Assert.Contains("-map_chapters 1", olcu.adli, StringComparison.Ordinal);
        Assert.Equal(new[] { (0d, "Giriş"), (30d, "Dönüş") }, olcu.plan);
        Assert.Contains(VidShrink.App.Localization.Strings.Get("main.convert.chapters.bad-time"), olcu.hata.Item1, StringComparison.Ordinal);
        Assert.False(olcu.hata.IsEnabled);
        Assert.Equal("00:00:31", olcu.eklenen.Saat);
        Assert.Contains("-map_chapters -1", olcu.bos.Komut, StringComparison.Ordinal);
        Assert.DoesNotContain("ffmetadata", olcu.bos.Komut, StringComparison.Ordinal);
        Assert.True(olcu.bos.Bos);
        Assert.False(olcu.gif.IsVisible);
        Assert.DoesNotContain("-map_chapters", olcu.gif.Komut, StringComparison.Ordinal);
        Assert.Equal((false, 2, true), olcu.yeniden);
    }
}
