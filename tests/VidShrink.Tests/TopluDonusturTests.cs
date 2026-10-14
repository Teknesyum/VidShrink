using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// DON-P6: Dönüştür'ün toplu kuyruğu. Her dosya kendi planıyla dönüşür; "hepsine aynı ayar" kuyruğun
/// ortak <see cref="ConversionPreset"/>'i, "bu dosyaya ayrı ayar" öğenin kendi ön ayarı. Düşen dosya
/// kuyruğu durdurmaz, sonda tek özet. Sahte koşturucu ölçümleri ffmpeg'siz; tek canlı ölçüm üç kısa
/// lavfi klibini sırayla, tek süreçte ve <c>-threads 2</c> ile dönüştürür.
/// </summary>
public sealed class TopluDonusturTests : IDisposable
{
    private readonly string _klasor = Path.Combine(TestPaths.OutputRoot, "toplu-donustur", Guid.NewGuid().ToString("N")[..8]);

    public TopluDonusturTests() => Directory.CreateDirectory(_klasor);

    public void Dispose()
    {
        try { Directory.Delete(_klasor, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly ConversionPreset Ortak = new() { Id = "ortak", Name = "Ortak", Container = "mp4", VideoCodec = "libx264" };

    private static readonly ConversionPreset Ayri = new()
    {
        Id = "arsiv-mkv",
        Name = "Arşiv MKV",
        Container = "mkv",
        VideoCodec = "libx265",
        QualityMode = ConversionQualityMode.Bitrate,
        Quality = 3000,
        Resolution = ConversionPreset.Custom,
        CustomResolution = "1920x800",
        Fps = "24",
        AudioCodec = ConversionPreset.NoAudio,
        AudioBitrateK = 192,
        DropMetadata = true
    };

    /// <summary>Ortadaki dosya düşer; ilk ve son dönüşür, hata iletisi öğede kalır, özet 2/1/0.</summary>
    [Fact]
    public async Task DusenDosyaKuyruguDurdurmaz()
    {
        var kuyruk = new ConversionQueue(new[] { "a.mp4", "b.mp4", "c.mp4" }, Ortak);
        var cagrilan = new List<string>();

        var ozet = await kuyruk.RunAsync((oge, _, _) =>
        {
            cagrilan.Add(oge.Path);
            return oge.Path == "b.mp4"
                ? Task.FromException<string>(new IOException("bozuk kaynak"))
                : Task.FromResult(oge.Path + ".out");
        }, null, CancellationToken.None);

        Assert.Equal(new[] { "a.mp4", "b.mp4", "c.mp4" }, cagrilan);
        Assert.Equal(new[] { ConversionItemState.Done, ConversionItemState.Failed, ConversionItemState.Done }, kuyruk.Items.Select(i => i.State));
        Assert.Equal("bozuk kaynak", kuyruk.Items[1].Error);
        Assert.Null(kuyruk.Items[1].Output);
        Assert.Equal("c.mp4.out", kuyruk.Items[2].Output);
        Assert.Equal(new ConversionQueueSummary(2, 1, 0), ozet);
    }

    /// <summary>Kendi ayarı verilen dosya onu alır, diğerleri ortak ayarı; ayrı ayar silinince ortak ayara döner.</summary>
    [Fact]
    public async Task DosyaBasinaAyarOrtakAyardanAyri()
    {
        var kuyruk = new ConversionQueue(new[] { "a.mp4", "b.mp4", "c.mp4" }, Ortak);
        kuyruk.Items[1].Own = Ayri;
        var gelen = new List<ConversionPreset>();

        await kuyruk.RunAsync((oge, ayar, _) => { gelen.Add(ayar); return Task.FromResult(oge.Path); }, null, CancellationToken.None);

        Assert.Equal(new[] { Ortak, Ayri, Ortak }, gelen);
        kuyruk.Items[1].Own = null;
        Assert.Same(Ortak, kuyruk.PresetFor(kuyruk.Items[1]));
    }

    /// <summary>İlk dosya koşarken iptal: o ve sıradakiler iptal sayılır, koşturucu bir daha çağrılmaz.</summary>
    [Fact]
    public async Task IptalKalanlariIptalSayar()
    {
        var kuyruk = new ConversionQueue(new[] { "a.mp4", "b.mp4", "c.mp4" }, Ortak);
        using var cts = new CancellationTokenSource();
        var cagri = 0;
        var degisim = new List<(string, ConversionItemState)>();

        var ozet = await kuyruk.RunAsync((_, _, ct) =>
        {
            cagri++;
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult("x");
        }, oge => degisim.Add((oge.Path, oge.State)), cts.Token);

        Assert.Equal(1, cagri);
        Assert.All(kuyruk.Items, i => Assert.Equal(ConversionItemState.Cancelled, i.State));
        Assert.Equal(new ConversionQueueSummary(0, 0, 3), ozet);
        Assert.Equal(("a.mp4", ConversionItemState.Running), degisim[0]);
        Assert.Equal(4, degisim.Count);
    }

    /// <summary>
    /// Ön ayardan kurulan plan, aynı ön ayar panele yazıldığında panelin kurduğu planla alan alan aynı.
    /// Kesit kuyrukta yok; panelde de boş.
    /// </summary>
    [Fact]
    public void OnAyarPlaniPanelinPlaniylaAyni()
    {
        var klasor = _klasor;
        var (panel, kuyruk) = AppHost.Run(() =>
        {
            var window = new MainWindow
            {
                SettingsPathOverride = Path.Combine(klasor, "settings.json"),
                PresetPathOverride = Path.Combine(klasor, "presets.json"),
                ConvertPresetPathOverride = Path.Combine(klasor, "convert-presets.json")
            };
            try
            {
                window.ApplyConvertPreset(Ayri);
                return (window.CurrentConversionPlan(), Ayri.ToPlan());
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal("mkv", kuyruk.Container);
        Assert.Equal((1920, 800), (kuyruk.Width, kuyruk.Height));
        Assert.Equal(24, kuyruk.Fps);
        Assert.Null(kuyruk.AudioCodec);
        Assert.Equal(3000, kuyruk.VideoBitrateK);
        Assert.Equal(
            (panel.Container, panel.VideoCodec, panel.VideoProfile, panel.QualityMode, panel.Crf, panel.VideoBitrateK, panel.Width, panel.Height,
                panel.Fps, panel.AudioCodec, panel.AudioBitrateK, panel.HdrPolicy, panel.DropMetadata, panel.Start, panel.End),
            (kuyruk.Container, kuyruk.VideoCodec, kuyruk.VideoProfile, kuyruk.QualityMode, kuyruk.Crf, kuyruk.VideoBitrateK, kuyruk.Width, kuyruk.Height,
                kuyruk.Fps, kuyruk.AudioCodec, kuyruk.AudioBitrateK, kuyruk.HdrPolicy, kuyruk.DropMetadata, kuyruk.Start, kuyruk.End));
    }

    private static MediaInfo Bilgi(string yol) => new()
    {
        FilePath = yol,
        FileSizeBytes = 1_000_000,
        DurationSeconds = 10,
        Width = 1280,
        Height = 720,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 800_000
    };

    /// <summary>Tek boyutlu özel çözünürlük doğrulamadan düşer: istisna kuyruğun o dosyaya "hata" demesidir.</summary>
    [Fact]
    public void HazirlikGecersizAyardaDuser()
    {
        var kaynak = Path.Combine(_klasor, "kaynak.mp4");
        var tek = Ayri with { CustomResolution = "1921x800" };

        Assert.Throws<InvalidOperationException>(() => ConvertQueueWindow.Prepare(Bilgi(kaynak), tek, HdrPolicy.Preserve, null));
        var (plan, cikti) = ConvertQueueWindow.Prepare(Bilgi(kaynak), Ayri, HdrPolicy.TonemapToSdr, _klasor);
        Assert.Equal(HdrPolicy.TonemapToSdr, plan.HdrPolicy);
        Assert.Equal(".mkv", Path.GetExtension(cikti));
        Assert.Equal(_klasor, Path.GetDirectoryName(cikti));
    }

    private static void Dongu(Func<bool> bitti, double saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < saniye)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }

    /// <summary>
    /// Pencerede üç dosya: üçüncüsüne açılır kutudan ayrı ayar seçilir, ortadaki düşer. Satırlar
    /// Bitti/Hata/Bitti, özet satırı 2/1/0, koşturucuya ortak-ortak-ayrı gider; "klasörde göster" açılır.
    /// </summary>
    [Fact]
    public void PencereUcDosyaOrtadakiDuser()
    {
        var ayar = Path.Combine(_klasor, "ayar", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ayar)!);
        var sonuc = AppHost.Run(() =>
        {
            var kultur = CultureInfo.CurrentUICulture;
            var eskiAyar = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", ayar);
            ConvertQueueWindow pencere;
            try
            {
                pencere = new ConvertQueueWindow(new[] { "a.mp4", "b.mp4", "c.mp4" }, Ortak, new[] { Ayri }, HdrPolicy.Preserve, null);
            }
            finally
            {
                Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", eskiAyar);
                CultureInfo.CurrentUICulture = kultur;
            }

            try
            {
                var gelen = new List<ConversionPreset>();
                pencere.Kosturucu = (yol, onAyar, _, _) =>
                {
                    gelen.Add(onAyar);
                    return yol == "b.mp4" ? Task.FromException<string>(new IOException("bozuk")) : Task.FromResult(yol + ".out");
                };
                pencere.ChoiceFor(2).SelectedIndex = 1;
                var is_ = pencere.StartAsync();
                Dongu(() => is_.IsCompleted, 30);
                is_.GetAwaiter().GetResult();
                return (Dil: pencere.Language, Gelen: gelen.ToArray(), Durum: Enumerable.Range(0, 3).Select(pencere.StateTextFor).ToArray(),
                    Ozet: pencere.TxtSummary.Text, Goster: pencere.BtnReveal.IsVisible, Basla: pencere.BtnStart.IsEnabled,
                    Kutu: pencere.ChoiceFor(0).IsEnabled);
            }
            finally
            {
                pencere.Close();
                Strings.Use("en");
            }
        });

        string Say(string anahtar, params object?[] args) => LanguageCatalog.Title(Strings.GetIn(sonuc.Dil, anahtar, args), sonuc.Dil);
        Assert.Equal("en", sonuc.Dil);
        Assert.Equal(new[] { Ortak, Ortak, Ayri }, sonuc.Gelen);
        Assert.Equal(new[] { Say("main.convert-queue.done"), Say("main.convert-queue.failed"), Say("main.convert-queue.done") }, sonuc.Durum);
        Assert.Equal(Say("main.convert-queue.summary", 2, 1, 0), sonuc.Ozet);
        Assert.True(sonuc.Goster);
        Assert.False(sonuc.Basla);
        Assert.False(sonuc.Kutu);
    }

    private static async Task Ffmpeg(params string[] args) => await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, args);

    /// <summary>
    /// Üç dosyalık deneme: üç kısa lavfi klip (1 sn, 160x90) ve araya bir video olmayan dosya. İlk klip ortak
    /// ayarla mp4'e, ikincisi ayrı ayarla sessiz 80x44 mkv'ye, üçüncüsü ayrı ayarla mp3'e döner; bozuk dosya
    /// düşer ve kuyruk durmaz. Çıktılar ffprobe ile okunur. Kodlama tek süreç, <c>-threads 2</c>.
    /// </summary>
    [FfmpegFact]
    public async Task UcDosyaGercekFfmpeglaDonusur()
    {
        var klipler = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var yol = Path.Combine(_klasor, $"klip{i}.mp4");
            await Ffmpeg("-hide_banner", "-v", "error", "-y",
                "-f", "lavfi", "-i", "testsrc=size=160x90:rate=10:duration=1",
                "-f", "lavfi", "-i", "sine=frequency=440:duration=1",
                "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac", "-shortest", "-threads", "2", yol);
            klipler.Add(yol);
        }
        var bozuk = Path.Combine(_klasor, "bozuk.mp4");
        File.WriteAllText(bozuk, "video degil");

        var cikis = Path.Combine(_klasor, "cikti");
        Directory.CreateDirectory(cikis);
        var ortak = new ConversionPreset { Id = "ortak", Container = "mp4", VideoCodec = "libx264", AudioCodec = "aac" };
        var sessizMkv = new ConversionPreset
        {
            Id = "sessiz",
            Container = "mkv",
            VideoCodec = "libx264",
            Resolution = ConversionPreset.Custom,
            CustomResolution = "80x44",
            AudioCodec = ConversionPreset.NoAudio
        };
        var mp3 = new ConversionPreset { Id = "ses", Container = "mp3", AudioCodec = "libmp3lame" };

        var kuyruk = new ConversionQueue(new[] { klipler[0], bozuk, klipler[1], klipler[2] }, ortak);
        kuyruk.Items[2].Own = sessizMkv;
        kuyruk.Items[3].Own = mp3;

        var ozet = await kuyruk.RunAsync(async (oge, ayar, ct) =>
        {
            var bilgi = await FfprobeClient.ProbeAsync(oge.Path, ct);
            var (plan, cikti) = ConvertQueueWindow.Prepare(bilgi, ayar, HdrPolicy.Preserve, cikis);
            var args = ConversionArguments.Build(bilgi, plan, cikti).ToList();
            args.InsertRange(args.LastIndexOf(cikti), new[] { "-threads", "2" });
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, args);
            return cikti;
        }, null, CancellationToken.None);

        Assert.Equal(new ConversionQueueSummary(3, 1, 0), ozet);
        Assert.Equal(ConversionItemState.Failed, kuyruk.Items[1].State);

        var mp4 = await FfprobeClient.ProbeAsync(kuyruk.Items[0].Output!);
        Assert.Equal(".mp4", Path.GetExtension(mp4.FilePath));
        Assert.Equal("h264", mp4.VideoCodec);
        Assert.Equal((160, 90), (mp4.Width, mp4.Height));
        Assert.True(mp4.HasAudio);

        var mkv = await FfprobeClient.ProbeAsync(kuyruk.Items[2].Output!);
        Assert.Equal(".mkv", Path.GetExtension(mkv.FilePath));
        Assert.Equal((80, 44), (mkv.Width, mkv.Height));
        Assert.False(mkv.HasAudio);

        var ses = kuyruk.Items[3].Output!;
        Assert.Equal(".mp3", Path.GetExtension(ses));
        Assert.True(new FileInfo(ses).Length > 0);
    }
}
