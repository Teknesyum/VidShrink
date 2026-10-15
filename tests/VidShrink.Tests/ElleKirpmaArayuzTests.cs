using System.IO;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// SIK-P8 (later 57): Gelişmiş'teki "Önizlemede elle kırp" kutusu. Önizleme katmanındaki oran
/// kaynak pikseline çevrilip planın <c>crop</c> süzgecine yazılır; kutu açılınca otomatik siyah
/// bant kırpma kapanır ve yoklama koşmaz. Otomatik yoklama sahtedir, ffmpeg koşmaz.
/// </summary>
public sealed class ElleKirpmaArayuzTests : IDisposable
{
    private const string KaynakYolu = @"C:\Kayitlar\elle-kirpma-2160p.mkv";
    private readonly string _ayar = Path.Combine(GirdiKanit.Root, ".calisma", "elle-kirpma-arayuz", Guid.NewGuid().ToString("N")[..8], "settings.json");
    private int _yoklama;

    public ElleKirpmaArayuzTests()
    {
        OtomatikKirpma.Tespit = (info, _) =>
        {
            _yoklama++;
            return Task.FromResult(new CropDetection(new CropRect(3840, 1600, 0, 280), Array.Empty<CropRect>(), TimeSpan.Zero));
        };
    }

    public void Dispose() => OtomatikKirpma.Tespit = CropProbe.RunAsync;

    private static MediaInfo Kaynak(string yol = KaynakYolu) => new()
    {
        FilePath = yol,
        FileSizeBytes = 420_000_000L,
        DurationSeconds = 187.5,
        Width = 3840,
        Height = 2160,
        Fps = 23.976,
        VideoCodec = "hevc",
        TotalBitrateBps = 18_800_000,
        AudioCodec = "aac",
        AudioBitrateBps = 192_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    private static string Vf(EncodePlan plan)
    {
        var args = FfmpegArguments.Build(Kaynak(), plan, "cikti.mp4", 0, null).ToList();
        return args.Contains("-vf") ? args[args.IndexOf("-vf") + 1] : "";
    }

    private T Pencerede<T>(Func<MainWindow, T> govde)
        => AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = _ayar };
            try { return govde(window); }
            finally { window.Close(); }
        });

    [Fact]
    public void CizilenOranKaynakPikselineCevrilipArgumanaYazilir()
    {
        var (kirpma, vf, katman) = Pencerede(w =>
        {
            w.LoadWithoutProbing(KaynakYolu, Kaynak());
            w.ChkFltManualCrop.IsChecked = true;
            w.RecalculateForTest();
            w.Preview.Kirpma.Ayarla(new CropFraction(0.25, 0.25, 0.75, 0.75));
            w.RecalculateForTest();
            var plan = w.ActivePlanForTest!;
            return (plan.Filters?.Crop, Vf(plan), w.Preview.Kirpma.IsVisible);
        });

        Assert.True(katman);
        Assert.Equal(new CropRect(1920, 1080, 960, 540), kirpma);
        Assert.Contains("crop=1920:1080:960:540", vf, StringComparison.Ordinal);
    }

    [Fact]
    public void ElleKirpmaOtomatikKirpmayiKapatirVeYoklamaKosmaz()
    {
        var (otomatik, etkin, kirpma) = Pencerede(w =>
        {
            w.ChkFltAutoCrop.IsChecked = true;
            w.ChkFltManualCrop.IsChecked = true;
            w.LoadWithoutProbing(KaynakYolu, Kaynak());
            w.RecalculateForTest();
            w.RecalculateForTest();
            return (w.ChkFltAutoCrop.IsChecked, w.ChkFltAutoCrop.IsEnabled, w.ActivePlanForTest!.Filters?.Crop);
        });

        Assert.False(otomatik);
        Assert.False(etkin);
        Assert.Null(kirpma);
        Assert.Equal(0, _yoklama);
    }

    [Fact]
    public void YeniKaynakDikdortgeniSifirlarKutuKapanincaKirpmaGider()
    {
        var (yeni, kapali, otomatikEtkin, katman) = Pencerede(w =>
        {
            w.ChkFltManualCrop.IsChecked = true;
            w.LoadWithoutProbing(KaynakYolu, Kaynak());
            w.RecalculateForTest();
            w.Preview.Kirpma.Ayarla(new CropFraction(0.25, 0.25, 0.75, 0.75));
            w.RecalculateForTest();
            Assert.NotNull(w.ActivePlanForTest!.Filters?.Crop);

            w.LoadWithoutProbing(@"C:\Kayitlar\ikinci-2160p.mkv", Kaynak(@"C:\Kayitlar\ikinci-2160p.mkv"));
            w.RecalculateForTest();
            var yeni = w.ActivePlanForTest!.Filters?.Crop;

            w.Preview.Kirpma.Ayarla(new CropFraction(0.25, 0.25, 0.75, 0.75));
            w.ChkFltManualCrop.IsChecked = false;
            w.RecalculateForTest();
            return (yeni, w.ActivePlanForTest!.Filters?.Crop, w.ChkFltAutoCrop.IsEnabled, w.Preview.Kirpma.IsVisible);
        });

        Assert.Null(yeni);
        Assert.Null(kapali);
        Assert.True(otomatikEtkin);
        Assert.False(katman);
    }
}
