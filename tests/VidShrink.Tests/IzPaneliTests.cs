using VidShrink.App;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// C1-6: B1'de yalnız CLI'dan açılan ses ve altyazı kolları arayüzde. Ses yüksekliği ve kazanç
/// plana geçer; dış altyazı ve yakma seçimi o videoya aittir — yeni kaynakta sıfırlanır,
/// kuyruk penceresine taşınmaz.
/// </summary>
public sealed class IzPaneliTests : IDisposable
{
    private static readonly string Klasor = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".calisma", "iz-paneli");

    private static MediaInfo Kaynak(string yol = "C:\\ornek\\film.mkv") => new()
    {
        FilePath = yol,
        FileSizeBytes = 50_000_000L,
        DurationSeconds = 60,
        Width = 1280,
        Height = 720,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 6_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p",
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "h264"),
            new SourceStream(1, StreamKind.Audio, "aac", "tur", Channels: 2),
            new SourceStream(2, StreamKind.Subtitle, "hdmv_pgs_subtitle", "eng"),
            new SourceStream(3, StreamKind.Subtitle, "subrip", "tur", Title: "Türkçe"),
            new SourceStream(4, StreamKind.Subtitle, "ass", "eng")
        }
    };

    private static T Pencerede<T>(Func<MainWindow, T> is_)
        => AppHost.Run(() =>
        {
            var window = new MainWindow();
            try { return is_(window); }
            finally { window.Close(); }
        });

    private readonly List<string> _klasorler = new();

    public void Dispose()
    {
        foreach (var klasor in _klasorler)
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
    }

    private string[] Dosyalar(params string[] adlar)
    {
        var klasor = Path.GetFullPath(Path.Combine(Klasor, Guid.NewGuid().ToString("N")[..8]));
        Directory.CreateDirectory(klasor);
        _klasorler.Add(klasor);
        return adlar.Select(ad =>
        {
            var yol = Path.Combine(klasor, ad);
            File.WriteAllText(yol, "1\n00:00:01,000 --> 00:00:02,000\nmerhaba\n");
            return yol;
        }).ToArray();
    }

    /// <summary>Ses yüksekliği ve kazanç plana geçer; 0 dB kazanç yazılmaz (olumsuz kontrol).</summary>
    [Fact]
    public void SesKollariPlanaGecer()
    {
        var (bos, dolu, sifir) = Pencerede(window =>
        {
            var ilk = window.PlanOptionsForTest();
            window.ChkAudioLoudnorm.IsChecked = true;
            window.CmbAudioGain.SelectedIndex = Array.IndexOf(MainWindow.GainCandidates, -6);
            var ikinci = window.PlanOptionsForTest();
            window.CmbAudioGain.SelectedIndex = Array.IndexOf(MainWindow.GainCandidates, 0);
            return (ilk, ikinci, window.PlanOptionsForTest());
        });

        Assert.False(bos.AudioLoudnorm);
        Assert.Null(bos.AudioGainDb);
        Assert.True(dolu.AudioLoudnorm);
        Assert.Equal(-6, dolu.AudioGainDb);
        Assert.Null(sifir.AudioGainDb);
    }

    /// <summary>Kazanç adayları motorun sınırı içinde ve sıfırı içerir.</summary>
    [Fact]
    public void KazancAdaylariSinirda()
    {
        Assert.Contains(0, MainWindow.GainCandidates);
        Assert.All(MainWindow.GainCandidates, db => Assert.InRange(db, StreamMapping.MinGainDb, StreamMapping.MaxGainDb));
    }

    /// <summary>Dış altyazı bir kez eklenir, desteklenmeyen uzantı ve olmayan dosya atlanır, çıkarma listeden düşer.</summary>
    [Fact]
    public void DisAltyaziEklenirVeCikarilir()
    {
        var yollar = Dosyalar("film.tr.srt", "film.en.vtt", "notlar.txt");
        var (eklenen, ikinci, plandaki, dil, kalan, gorunur) = Pencerede(window =>
        {
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", Kaynak());
            var n = window.AddSubtitleFiles(yollar.Append(yollar[0].ToUpperInvariant()).Append(yollar[0] + ".yok.srt"));
            var m = window.AddSubtitleFiles(new[] { yollar[1] });
            var plan = window.PlanOptionsForTest().ExternalSubtitles.Select(s => s.Path).ToList();
            var tr = window.ExternalSubtitles[0].Language;
            window.RemoveSubtitle(0);
            return (n, m, plan, tr, window.PlanOptionsForTest().ExternalSubtitles.Select(s => s.Path).ToList(), window.SubtitleList.IsVisible);
        });

        Assert.Equal(2, eklenen);
        Assert.Equal(0, ikinci);
        Assert.Equal(new[] { yollar[0], yollar[1] }, plandaki);
        Assert.Equal("tur", dil);
        Assert.Equal(new[] { yollar[1] }, kalan);
        Assert.True(gorunur);
    }

    private static string? Sonraki(IReadOnlyList<string> args, string bayrak)
        => args.ToList().IndexOf(bayrak) is var i and >= 0 ? args[i + 1] : null;

    /// <summary>
    /// Yakma listesi metin ve resim altyazıları sunar; seçim altyazılar içindeki sıraya iner. Resim izi
    /// (PGS) seçilince argüman CLI'daki <c>--yak N</c> ile aynı yoldan, <c>-filter_complex</c> bindirmesiyle
    /// kurulur; metin izi <c>-vf subtitles</c> yolunda kalır (olumsuz kontrol). Yakılamayan kodek listede yok.
    /// </summary>
    [Fact]
    public void YakmaListesiMetinVeResimAltyazisi()
    {
        var kaynak = Kaynak();
        kaynak = kaynak with { Streams = kaynak.Streams.Append(new SourceStream(5, StreamKind.Subtitle, "eia_608", "eng")).ToArray() };
        var (adet, acik, resimSecim, resim, metinSecim, metin, secimsiz) = Pencerede(window =>
        {
            var basta = window.PlanOptionsForTest().Filters.BurnSubtitle;
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", kaynak);
            var n = window.CmbBurnSubtitle.ItemCount;
            var a = window.CmbBurnSubtitle.IsEnabled;
            window.CmbBurnSubtitle.SelectedIndex = 1;
            var rs = window.BurnChoice;
            var r = window.PlanOptionsForTest();
            window.CmbBurnSubtitle.SelectedIndex = 2;
            return (n, a, rs, r, window.BurnChoice, window.PlanOptionsForTest(), basta);
        });

        Assert.Equal(4, adet);
        Assert.True(acik);
        Assert.Equal(0, resimSecim);
        Assert.Equal(0, resim.Filters.BurnSubtitle);
        Assert.Equal(1, metinSecim);
        Assert.Equal(1, metin.Filters.BurnSubtitle);
        Assert.Null(secimsiz);

        var resimArg = FfmpegArguments.Build(kaynak, PlanCalculator.Build(kaynak, resim), "cikti.mp4", 0, null);
        var graf = Sonraki(resimArg, "-filter_complex");
        Assert.NotNull(graf);
        Assert.StartsWith("[0:2]scale=1280:-1[s];", graf, StringComparison.Ordinal);
        Assert.Contains("[s]overlay=x=(W-w)/2:y=H-h:eof_action=pass", graf, StringComparison.Ordinal);
        Assert.Contains(VideoFilterChain.OverlayOutput, Enumerable.Range(0, resimArg.Count - 1).Where(i => resimArg[i] == "-map").Select(i => resimArg[i + 1]));
        Assert.DoesNotContain("-vf", resimArg);

        var metinArg = FfmpegArguments.Build(kaynak, PlanCalculator.Build(kaynak, metin), "cikti.mp4", 0, null);
        Assert.DoesNotContain("-filter_complex", metinArg);
        Assert.Contains(VideoFilterChain.BurnFilterName + "=", Sonraki(metinArg, "-vf") ?? "", StringComparison.Ordinal);
    }

    /// <summary>
    /// Süzgeç dökümü resim altyazı yakmada bindirme adımını gösterir: <c>-vf</c> zinciri onu taşımıyor,
    /// grafiğe geçiyor. Metin izde dökümde <c>subtitles</c> var, bindirme yok (olumsuz kontrol).
    /// </summary>
    [Fact]
    public void SuzgecDokumuBindirmeyiGosterir()
    {
        var (bos, resim, metin) = Pencerede(window =>
        {
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", Kaynak());
            window.RecalculateForTest();
            var b = window.TxtAdvFiltersNow.Text;
            window.CmbBurnSubtitle.SelectedIndex = 1;
            window.RecalculateForTest();
            var r = window.TxtAdvFiltersNow.Text;
            window.CmbBurnSubtitle.SelectedIndex = 2;
            window.RecalculateForTest();
            return (b, r, window.TxtAdvFiltersNow.Text);
        });

        Assert.False(string.IsNullOrEmpty(bos));
        Assert.DoesNotContain("overlay=", bos, StringComparison.Ordinal);
        Assert.Contains("overlay=x=(W-w)/2:y=H-h:eof_action=pass", resim, StringComparison.Ordinal);
        Assert.DoesNotContain(VideoFilterChain.BurnFilterName + "=", resim, StringComparison.Ordinal);
        Assert.Contains(VideoFilterChain.BurnFilterName + "=", metin, StringComparison.Ordinal);
        Assert.DoesNotContain("overlay=", metin, StringComparison.Ordinal);
    }

    /// <summary>
    /// Döküm grafikteki sırayı verir: bindirme kırpmadan sonra, ölçeklemeden önce; 10 bit kaynakta
    /// <c>format</c> eki dökümde de var. Yakma yokken döküm <c>-vf</c> zinciriyle aynı (olumsuz kontrol).
    /// </summary>
    [Fact]
    public void DokumGrafiginSirasiniTasir()
    {
        var kaynak = Kaynak() with { Width = 1920, Height = 1080 };
        var kirp = new VideoFilterOptions { BurnSubtitle = 0, Crop = new CropRect(1920, 800, 0, 140) };
        var plan = PlanCalculator.Build(kaynak, new PlanOptions { TargetMb = 2, Filters = kirp });
        var dokum = VideoFilterChain.Steps(kaynak, plan).ToList();
        var zincir = VideoFilterChain.Filters(kaynak, plan).ToList();
        var bindirme = dokum.FindIndex(adim => adim.StartsWith("overlay=", StringComparison.Ordinal));

        Assert.Equal(zincir.Count + 1, dokum.Count);
        Assert.Equal(dokum.IndexOf("crop=1920:800:0:140") + 1, bindirme);
        Assert.True(bindirme < dokum.FindIndex(adim => adim.StartsWith("scale=", StringComparison.Ordinal)));
        Assert.Equal(zincir, dokum.Where((_, i) => i != bindirme));
        Assert.Contains("[s]" + string.Join(',', dokum.Skip(bindirme)) + "[v]", VideoFilterChain.OverlayGraph(kaynak, plan, "0:0"), StringComparison.Ordinal);

        var onBit = kaynak with { PixelFormat = "yuv420p10le", BitDepth = 10 };
        var onBitPlan = PlanCalculator.Build(onBit, new PlanOptions { TargetMb = 2, Filters = kirp });
        Assert.Contains(VideoFilterChain.Steps(onBit, onBitPlan), adim => adim.EndsWith(":format=yuv420p10", StringComparison.Ordinal));

        var yakmasiz = PlanCalculator.Build(kaynak, new PlanOptions { TargetMb = 2, Filters = kirp with { BurnSubtitle = null } });
        Assert.Equal(VideoFilterChain.Filters(kaynak, yakmasiz), VideoFilterChain.Steps(kaynak, yakmasiz));
    }

    /// <summary>
    /// Düşürülen resim altyazı notu 42 dilde yakma seçeneğini o dilin kendi etiketiyle
    /// (<c>main.subtitles.burn</c>) gösterir; komşu ses notu göstermez (olumsuz kontrol).
    /// </summary>
    [Fact]
    public void DusurmeNotuYakmaSeceneginiGosterir()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            var etiket = values["main.subtitles.burn"];
            Assert.True(etiket.Length > 0, language);
            foreach (var key in new[] { "main.reason.stream.image-subtitle-dropped", "main.reason.stream.image-subtitle-dropped-by-container" })
                Assert.True(values[key].Contains(etiket, StringComparison.Ordinal), $"{language}: {key}");
            Assert.False(values["main.reason.stream.extra-audio-dropped"].Contains(etiket, StringComparison.Ordinal), language);
        }
    }

    /// <summary>Altyazısız kaynakta yakma kutusu kapalı ve tek "kapalı" satırı var.</summary>
    [Fact]
    public void AltyazisizKaynaktaYakmaKapali()
    {
        var (adet, acik) = Pencerede(window =>
        {
            window.LoadWithoutProbing("C:\\ornek\\duz.mp4", Kaynak("C:\\ornek\\duz.mp4") with { AudioCodec = null, Streams = new[] { new SourceStream(0, StreamKind.Video, "h264") } });
            return (window.CmbBurnSubtitle.ItemCount, window.CmbBurnSubtitle.IsEnabled);
        });

        Assert.Equal(1, adet);
        Assert.False(acik);
    }

    /// <summary>Yeni kaynak önceki videonun dış altyazısını ve yakma seçimini taşımaz; ses kolları kalır.</summary>
    [Fact]
    public void YeniKaynakIzleriSifirlar()
    {
        var yollar = Dosyalar("film.srt");
        var (once, sonra) = Pencerede(window =>
        {
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", Kaynak());
            window.AddSubtitleFiles(yollar);
            window.CmbBurnSubtitle.SelectedIndex = 3;
            window.ChkAudioLoudnorm.IsChecked = true;
            var ilk = window.PlanOptionsForTest();
            window.LoadWithoutProbing("C:\\ornek\\ikinci.mkv", Kaynak("C:\\ornek\\ikinci.mkv"));
            return (ilk, window.PlanOptionsForTest());
        });

        Assert.Single(once.ExternalSubtitles);
        Assert.Equal(2, once.Filters.BurnSubtitle);
        Assert.Empty(sonra.ExternalSubtitles);
        Assert.Null(sonra.Filters.BurnSubtitle);
        Assert.True(sonra.AudioLoudnorm);
    }

    /// <summary>Kuyruk penceresi dış altyazıyı ve yakmayı almaz; ses kolları geçer (olumsuz kontrol).</summary>
    [Fact]
    public void KuyrukIzleriTasimaz()
    {
        var yollar = Dosyalar("film.srt");
        var (pencere, kuyruk) = Pencerede(window =>
        {
            ShrinkJobWindow? batch = null;
            try
            {
                window.LoadWithoutProbing("C:\\ornek\\film.mkv", Kaynak());
                window.AddSubtitleFiles(yollar);
                window.CmbBurnSubtitle.SelectedIndex = 1;
                window.ChkAudioLoudnorm.IsChecked = true;
                var tek = window.PlanOptionsForTest();
                batch = window.OpenBatch(new[] { "a.mp4", "b.mp4" });
                return (tek, batch.OptionsFor(new ShrinkRequest(0, "a.mp4"), Kaynak("a.mp4")).Options);
            }
            finally { batch?.Close(); }
        });

        Assert.Single(pencere.ExternalSubtitles);
        Assert.NotNull(pencere.Filters.BurnSubtitle);
        Assert.Empty(kuyruk.ExternalSubtitles);
        Assert.Null(kuyruk.Filters.BurnSubtitle);
        Assert.True(kuyruk.AudioLoudnorm);
    }

    /// <summary>Altyazı dosyası tanıma bırakma kolunun kapısıdır; video altyazı sayılmaz.</summary>
    [Theory]
    [InlineData("a.srt", true)]
    [InlineData("a.ASS", true)]
    [InlineData("a.vtt", true)]
    [InlineData("a.mkv", false)]
    [InlineData("a.txt", false)]
    public void AltyaziDosyasiTanima(string yol, bool beklenen) => Assert.Equal(beklenen, MainWindow.IsSubtitleFile(yol));

    /// <summary>On anahtar 42 dilde dolu.</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        var anahtarlar = new[]
        {
            "main.audio.level.label", "main.audio.loudnorm", "main.audio.gain", "main.audio.gain.none",
            "main.subtitles.label", "main.subtitles.add", "main.subtitles.burn", "main.subtitles.burn.off",
            "main.subtitles.remove", "main.subtitles.file-type"
        };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
        }
    }
}
