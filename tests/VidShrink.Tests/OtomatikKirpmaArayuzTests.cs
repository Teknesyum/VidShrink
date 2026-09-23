using System.IO;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// HB #36: Gelişmiş'teki "Siyah bantları kırp" kutusu. Yoklama sahte bir
/// <see cref="OtomatikKirpma.Tespit"/>'le değiştirilir, gerçek ffmpeg koşmaz; ölçü planın ürettiği
/// ffmpeg argümanındaki <c>-vf</c> metnidir.
/// </summary>
public sealed class OtomatikKirpmaArayuzTests : IDisposable
{
    private const string KaynakYolu = @"C:\Kayitlar\sinemaskop-2160p.mkv";
    private static readonly CropRect Bulunan = new(3840, 1600, 0, 280);
    private readonly string _ayar = Path.Combine(GirdiKanit.Root, ".calisma", "otomatik-kirpma", Guid.NewGuid().ToString("N")[..8], "settings.json");
    private int _yoklama;

    public OtomatikKirpmaArayuzTests()
    {
        OtomatikKirpma.Tespit = (info, _) =>
        {
            _yoklama++;
            return Task.FromResult(new CropDetection(info.FilePath == KaynakYolu ? Bulunan : new CropRect(1920, 800, 0, 140),
                Array.Empty<CropRect>(), TimeSpan.Zero));
        };
    }

    public void Dispose() => OtomatikKirpma.Tespit = CropProbe.RunAsync;

    private static MediaInfo Kaynak(string yol = KaynakYolu, int genislik = 3840, int yukseklik = 2160) => new()
    {
        FilePath = yol,
        FileSizeBytes = 420_000_000L,
        DurationSeconds = 187.5,
        Width = genislik,
        Height = yukseklik,
        Fps = 23.976,
        VideoCodec = "hevc",
        TotalBitrateBps = 18_800_000,
        AudioCodec = "aac",
        AudioBitrateBps = 192_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    private sealed record Okuma(string Vf, CropRect? Kirpma, IReadOnlyList<string> Gerekce);

    private Okuma Pencerede(bool kutu, string? belirtim = null)
        => AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = _ayar };
            try
            {
                if (belirtim is not null) window.TxtAdvFilters.Text = belirtim;
                window.ChkFltAutoCrop.IsChecked = kutu;
                window.LoadWithoutProbing(KaynakYolu, Kaynak());
                window.RecalculateForTest();
                window.RecalculateForTest();
                var plan = window.ActivePlanForTest!;
                var args = FfmpegArguments.Build(Kaynak(), plan, "cikti.mp4", 0, null);
                var vf = args.Contains("-vf") ? args[args.ToList().IndexOf("-vf") + 1] : "";
                return new Okuma(vf, plan.Filters?.Crop, window.ReasonLinesForTest(plan));
            }
            finally { window.Close(); }
        });

    /// <summary>Kutu açık, yoklama bant buldu: argümanda <c>crop=</c> var, gerekçede boyut; kaynak başına bir yoklama.</summary>
    [Fact]
    public void AcikKutuBulunanDikdortgeniArgumanaYazar()
    {
        var okuma = Pencerede(kutu: true);

        Assert.Equal(Bulunan, okuma.Kirpma);
        Assert.Contains("crop=3840:1600:0:280", okuma.Vf, StringComparison.Ordinal);
        Assert.Contains(okuma.Gerekce, satir => satir.Contains("3840x1600", StringComparison.Ordinal)
            && satir.Contains("3840x2160", StringComparison.Ordinal));
        Assert.Equal(1, _yoklama);
    }

    /// <summary>Olumsuz kontrol: kutu kapalı — yoklama koşmaz, argümanda <c>crop=</c> yok.</summary>
    [Fact]
    public void KapaliKutuKirpmaz()
    {
        var okuma = Pencerede(kutu: false);

        Assert.Null(okuma.Kirpma);
        Assert.DoesNotContain("crop=", okuma.Vf, StringComparison.Ordinal);
        Assert.DoesNotContain(okuma.Gerekce, satir => satir.Contains("3840x1600", StringComparison.Ordinal));
        Assert.Equal(0, _yoklama);
    }

    /// <summary>Kullanıcı belirtimde kendi <c>crop=</c>'unu yazdıysa yoklama sonucu onu ezmez.</summary>
    [Fact]
    public void ElleYazilanKirpmaEzilmez()
    {
        var okuma = Pencerede(kutu: true, belirtim: "crop=3200:1800:320:180");

        Assert.Equal(new CropRect(3200, 1800, 320, 180), okuma.Kirpma);
        Assert.Contains("crop=3200:1800:320:180", okuma.Vf, StringComparison.Ordinal);
        Assert.DoesNotContain("crop=3840:1600:0:280", okuma.Vf, StringComparison.Ordinal);
    }

    /// <summary>
    /// Kuyruk kutuyu bayrak olarak taşır, yüklü dosyanın dikdörtgenini değil: her dosya kendi
    /// yoklamasıyla kırpılır; kutu kapalıyken kuyruk yoklamaz.
    /// </summary>
    [Fact]
    public async Task KuyrukHerDosyayiKendiYoklamasiylaKirpar()
    {
        var ikinci = Kaynak(@"C:\Kayitlar\ikinci-1080p.mkv", 1920, 1080);
        var (acik, kapali, sablon) = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = _ayar };
            try
            {
                window.ChkFltAutoCrop.IsChecked = true;
                window.LoadWithoutProbing(KaynakYolu, Kaynak());
                var acikKuyruk = window.OpenBatch(new[] { ikinci.FilePath });
                window.ChkFltAutoCrop.IsChecked = false;
                var kapaliKuyruk = window.OpenBatch(new[] { ikinci.FilePath });
                return (acikKuyruk, kapaliKuyruk, acikKuyruk.OptionsFor(new ShrinkRequest(0, ikinci.FilePath), ikinci).Options);
            }
            finally { window.Close(); }
        });
        try
        {
            Assert.Null(sablon.Filters.Crop);
            var once = _yoklama;
            var kirpilan = await acik.KirpmaylaAsync(acik.OptionsFor(new ShrinkRequest(0, ikinci.FilePath), ikinci).Options, ikinci, CancellationToken.None);
            Assert.Equal(new CropRect(1920, 800, 0, 140), kirpilan.Filters.Crop);
            Assert.Equal(once + 1, _yoklama);

            var kirpilmayan = await kapali.KirpmaylaAsync(kapali.OptionsFor(new ShrinkRequest(0, ikinci.FilePath), ikinci).Options, ikinci, CancellationToken.None);
            Assert.Null(kirpilmayan.Filters.Crop);
            Assert.Equal(once + 1, _yoklama);
        }
        finally
        {
            AppHost.Run(() => { acik.Close(); kapali.Close(); });
        }
    }

    /// <summary>Yoklama başarısızsa (ffmpeg yok) küçültme sessizce kırpmasız sürer.</summary>
    [Fact]
    public async Task BasarisizYoklamaKirpmasizSurer()
    {
        OtomatikKirpma.Tespit = (_, _) => throw new FileNotFoundException("ffmpeg");
        Assert.Null(await OtomatikKirpma.BulAsync(Kaynak(), CancellationToken.None));
    }

    /// <summary>İki yeni anahtar 42 dilde dolu; gerekçe dört yer tutucuyu taşır.</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            Assert.True(values.TryGetValue("main.advanced.filters.autocrop", out var etiket) && etiket.Length > 0, language);
            Assert.True(values.TryGetValue("main.reason.auto-crop", out var gerekce), language);
            foreach (var yer in new[] { "{0}", "{1}", "{2}", "{3}" })
                Assert.Contains(yer, gerekce!, StringComparison.Ordinal);
        }
    }
}
