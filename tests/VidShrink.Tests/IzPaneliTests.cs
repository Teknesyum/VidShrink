using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
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

    private static MediaInfo TaramaKaynagi(string yol, params SourceStream[] altyazilar) => Kaynak(yol) with
    {
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "h264"),
            new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 2)
        }.Concat(altyazilar).ToArray()
    };

    private static SourceStream Metin(int index, string dil = "eng", bool zorunlu = false)
        => new(index, StreamKind.Subtitle, "subrip", dil, IsForced: zorunlu);

    private static void Tikla(Button dugme) => dugme.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// Zorunlu bayraklı iz paket sayılmadan seçilir ve plana iner: düğme CLI'daki <c>--altyazi-tara</c>
    /// ile aynı ilk adımı koşar. Sayaç hiç çağrılmaz (olumsuz kontrol: bayraksız eşinde çağrılır).
    /// </summary>
    [Fact]
    public void TaramaBayrakliIziSayimsizSecer()
    {
        var (secim, plandaki, sayim, durum, bayraksizSayim) = Pencerede(window =>
        {
            var sayac = 0;
            window.CountSubtitlePackets = (_, _) =>
            {
                sayac++;
                return Task.FromResult<IReadOnlyDictionary<int, int>?>(null);
            };
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", TaramaKaynagi("C:\\ornek\\film.mkv", Metin(2), Metin(3, "tur"), Metin(4, zorunlu: true)));
            Tikla(window.BtnScanSubtitle);
            var sonuc = (window.BurnChoice, window.PlanOptionsForTest().Filters.BurnSubtitle, sayac, window.SubtitleScanStatus);
            window.LoadWithoutProbing("C:\\ornek\\duz.mkv", TaramaKaynagi("C:\\ornek\\duz.mkv", Metin(2), Metin(3, "tur"), Metin(4)));
            Tikla(window.BtnScanSubtitle);
            return (sonuc.Item1, sonuc.Item2, sonuc.Item3, sonuc.Item4, sayac);
        });

        Assert.Equal(2, secim);
        Assert.Equal(2, plandaki);
        Assert.Equal(0, sayim);
        Assert.Equal("Picked #3: it carries the forced flag.", durum);
        Assert.Equal(1, bayraksizSayim);
    }

    /// <summary>
    /// Bayrak yokken sayım koşar; aynı dilde en dolu izin onda birini geçmeyen iz listede seçilir.
    /// Sayaca yüklü kaynak gider; resim altyazı listede yer tuttuğu için seçim sırası kaymaz.
    /// </summary>
    [Fact]
    public void TaramaSeyrekIziSayimlaSecer()
    {
        var kaynak = TaramaKaynagi("C:\\ornek\\film.mkv",
            new SourceStream(2, StreamKind.Subtitle, "eia_608", "eng"), Metin(3), Metin(4), new SourceStream(5, StreamKind.Subtitle, "hdmv_pgs_subtitle", "eng"));
        var (secim, satir, plandaki, sayilan, durum, calisiyor, dugme) = Pencerede(window =>
        {
            string? yol = null;
            window.CountSubtitlePackets = (info, _) =>
            {
                yol = info.FilePath;
                return Task.FromResult<IReadOnlyDictionary<int, int>?>(new Dictionary<int, int> { [3] = 100, [4] = 5, [5] = 40 });
            };
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", kaynak);
            Tikla(window.BtnScanSubtitle);
            return (window.BurnChoice, window.CmbBurnSubtitle.SelectedIndex, window.PlanOptionsForTest().Filters.BurnSubtitle, yol,
                window.SubtitleScanStatus, window.SubtitleScanRunning, window.BtnScanSubtitle.Content as string);
        });

        Assert.Equal(2, secim);
        Assert.Equal(2, satir);
        Assert.Equal(2, plandaki);
        Assert.Equal("C:\\ornek\\film.mkv", sayilan);
        Assert.Equal("Picked #3: 5 packets against 100 in the fullest track of the same language.", durum);
        Assert.False(calisiyor);
        Assert.Equal("Find Foreign-language Subtitle", dugme);
    }

    /// <summary>
    /// İz seçilmeyen dört sonuç kendi cümlesini yazar ve elle yapılmış seçime dokunmaz: sayılamadı
    /// (sayaç <c>null</c> döner ya da atar), aday yok, belirsiz.
    /// </summary>
    [Theory]
    [InlineData("null", "The subtitle packets could not be counted; no track is picked.")]
    [InlineData("hata", "The subtitle packets could not be counted; no track is picked.")]
    [InlineData("100,100,0", "No sparse track stands next to a full one in the same language; no track is picked.")]
    [InlineData("100,5,5", "More than one track looks like a foreign-language subtitle; no track is picked. Choose one from the list.")]
    public void TaramaSecemeyinceSoyler(string sayim, string beklenen)
    {
        var (secim, durum) = Pencerede(window =>
        {
            window.CountSubtitlePackets = (_, _) => sayim switch
            {
                "null" => Task.FromResult<IReadOnlyDictionary<int, int>?>(null),
                "hata" => Task.FromException<IReadOnlyDictionary<int, int>?>(new InvalidOperationException("ffprobe yok")),
                _ => Task.FromResult<IReadOnlyDictionary<int, int>?>(sayim.Split(',').Select(int.Parse).Select((adet, i) => (i, adet))
                    .Where(x => x.adet > 0).ToDictionary(x => x.i + 2, x => x.adet))
            };
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", TaramaKaynagi("C:\\ornek\\film.mkv", Metin(2), Metin(3), Metin(4)));
            window.CmbBurnSubtitle.SelectedIndex = 1;
            Tikla(window.BtnScanSubtitle);
            return (window.BurnChoice, window.SubtitleScanStatus);
        });

        Assert.Equal(0, secim);
        Assert.Equal(beklenen, durum);
    }

    /// <summary>
    /// Yakılabilir altyazı yoksa düğme kapalıdır ve karar yolu sayım açmadan "altyazı yok" der;
    /// altyazılı kaynakta düğme açık ve erişilebilir adı taşır (olumsuz kontrol).
    /// </summary>
    [Fact]
    public void TaramaDugmesiAltyazisizKaynaktaKapali()
    {
        var (bosta, kapali, sayim, durum, acik, ad) = Pencerede(window =>
        {
            var sayac = 0;
            window.CountSubtitlePackets = (_, _) =>
            {
                sayac++;
                return Task.FromResult<IReadOnlyDictionary<int, int>?>(null);
            };
            var ilk = window.BtnScanSubtitle.IsEnabled;
            window.LoadWithoutProbing("C:\\ornek\\duz.mkv", TaramaKaynagi("C:\\ornek\\duz.mkv", new SourceStream(2, StreamKind.Subtitle, "eia_608", "eng")));
            var k = window.BtnScanSubtitle.IsEnabled;
            window.ScanSubtitlesAsync().GetAwaiter().GetResult();
            var d = window.SubtitleScanStatus;
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", Kaynak());
            return (ilk, k, sayac, d, window.BtnScanSubtitle.IsEnabled, AutomationProperties.GetName(window.BtnScanSubtitle));
        });

        Assert.False(bosta);
        Assert.False(kapali);
        Assert.Equal(0, sayim);
        Assert.Equal("The source has no subtitle that can be burned.", durum);
        Assert.True(acik);
        Assert.Equal("Find Foreign-language Subtitle", ad);
    }

    /// <summary>
    /// Sayım sürerken düğme iptale döner ve arayüz beklemez; ikinci basış sayacın jetonunu iptal eder.
    /// Geç gelen sayım sonucu seçimi de durumu da yazmaz.
    /// </summary>
    [Fact]
    public void TaramaIptalEdilir()
    {
        var (surerken, sonra, gec) = Pencerede(window =>
        {
            var is_ = new TaskCompletionSource<IReadOnlyDictionary<int, int>?>();
            var jeton = CancellationToken.None;
            window.CountSubtitlePackets = (_, ct) =>
            {
                jeton = ct;
                return is_.Task;
            };
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", TaramaKaynagi("C:\\ornek\\film.mkv", Metin(2), Metin(3)));
            Tikla(window.BtnScanSubtitle);
            var a = (window.SubtitleScanRunning, window.SubtitleScanStatus, window.BtnScanSubtitle.Content as string,
                AutomationProperties.GetName(window.BtnScanSubtitle), jeton.IsCancellationRequested);
            Tikla(window.BtnScanSubtitle);
            var b = (window.SubtitleScanRunning, window.SubtitleScanStatus, window.BtnScanSubtitle.Content as string, jeton.IsCancellationRequested);
            is_.SetResult(new Dictionary<int, int> { [2] = 100, [3] = 5 });
            Dispatcher.UIThread.RunJobs();
            return (a, b, (window.BurnChoice, window.SubtitleScanStatus, window.SubtitleScanRunning));
        });

        Assert.Equal((true, "Counting subtitle packets...", "Cancel", "Cancel", false), surerken);
        Assert.Equal((false, null, "Find Foreign-language Subtitle", true), sonra);
        Assert.Equal((null, null, false), gec);
    }

    /// <summary>
    /// Yeni kaynak süren taramayı düşürür ve biten taramanın durumunu siler; eski kaynağın geç gelen
    /// sayımı yeni kaynakta iz seçmez. Elle seçim de durumu siler.
    /// </summary>
    [Fact]
    public void YeniKaynakTaramayiSifirlar()
    {
        var (iptal, gec, bitince, yeniKaynakta, elleSecince) = Pencerede(window =>
        {
            var is_ = new TaskCompletionSource<IReadOnlyDictionary<int, int>?>();
            var jeton = CancellationToken.None;
            window.CountSubtitlePackets = (_, ct) =>
            {
                jeton = ct;
                return is_.Task;
            };
            var ilk = TaramaKaynagi("C:\\ornek\\film.mkv", Metin(2), Metin(3));
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", ilk);
            Tikla(window.BtnScanSubtitle);
            window.LoadWithoutProbing("C:\\ornek\\ikinci.mkv", TaramaKaynagi("C:\\ornek\\ikinci.mkv", Metin(2), Metin(3)));
            var i = (jeton.IsCancellationRequested, window.SubtitleScanRunning, window.SubtitleScanStatus);
            is_.SetResult(new Dictionary<int, int> { [2] = 100, [3] = 5 });
            Dispatcher.UIThread.RunJobs();
            var g = (window.BurnChoice, window.SubtitleScanStatus);

            window.CountSubtitlePackets = (_, _) => Task.FromResult<IReadOnlyDictionary<int, int>?>(new Dictionary<int, int> { [2] = 100, [3] = 5 });
            Tikla(window.BtnScanSubtitle);
            var b = (window.BurnChoice, window.SubtitleScanStatus is not null);
            window.LoadWithoutProbing("C:\\ornek\\film.mkv", ilk);
            var y = (window.BurnChoice, window.SubtitleScanStatus);
            Tikla(window.BtnScanSubtitle);
            window.CmbBurnSubtitle.SelectedIndex = 1;
            return (i, g, b, y, (window.BurnChoice, window.SubtitleScanStatus));
        });

        Assert.Equal((true, false, null), iptal);
        Assert.Equal((null, null), gec);
        Assert.Equal((1, true), bitince);
        Assert.Equal((null, null), yeniKaynakta);
        Assert.Equal((0, null), elleSecince);
    }

    /// <summary>
    /// Taramanın sekiz anahtarı 42 dilde dolu; sayı taşıyan iki cümle yer tutucularını korur, öbürleri
    /// yer tutucu taşımaz. CLI'ın altı sonucunun her birinin arayüzde bir cümlesi var.
    /// </summary>
    [Fact]
    public void TaramaAnahtarlariButunDillerde()
    {
        var sonuclar = Enum.GetValues<ForeignAudioOutcome>().Select(sonuc => "main.subtitles.scan." + new ForeignAudioPick(sonuc).Slug).ToList();
        Assert.Equal(6, sonuclar.Distinct().Count());
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in sonuclar.Append("main.subtitles.scan").Append("main.subtitles.scan.running"))
            {
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
                var beklenen = key.EndsWith(".flagged", StringComparison.Ordinal) ? 1 : key.EndsWith(".sparse", StringComparison.Ordinal) ? 3 : 0;
                for (var i = 0; i < 3; i++)
                    Assert.True(metin.Contains("{" + i + "}", StringComparison.Ordinal) == i < beklenen, $"{language}: {key} {{{i}}}");
            }
            Assert.Equal(8, values.Keys.Count(key => key.StartsWith("main.subtitles.scan", StringComparison.Ordinal)));
        }
    }

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
