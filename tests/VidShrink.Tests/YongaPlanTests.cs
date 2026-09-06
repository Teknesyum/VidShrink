using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// T177: yonga şeridi artık niyeti de taşıyor. Her yonga tek bir plana bağlanır ve o plan
/// burada pimlenir; şeride yeni yonga eklenip eşlemesi unutulduğunda ölçüm kırmızıya düşer.
///
/// <para>Yonga sayısı gözle sayılmıyor: biçimlemedeki <c>ChipButton</c> temalı düğmeler
/// sayılıp eşleme listesiyle karşılaştırılıyor.</para>
/// </summary>
public sealed class YongaPlanTests
{
    private static IReadOnlyList<MainWindow.ChipPlan> Plans() => MainWindow.ChipPlans();

    private static string Sandbox() =>
        Path.Combine(TestPaths.OutputRoot, "t177-yonga-plan", $"{Guid.NewGuid():N}", "settings.json");

    private static void Click(Button chip) => chip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>90 MB'lık sıradan bir 1080p30 kayıt; tavansız hedef kaynaktan türetiliyor.</summary>
    private static MediaInfo Source() => new()
    {
        FilePath = @"C:\Kayitlar\telefon-kaydi-1080p30.mp4",
        FileSizeBytes = 90L * 1024 * 1024,
        DurationSeconds = 62.0,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 11_600_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    private static MainWindow.ChipPlan Plan(string chip) =>
        Assert.Single(Plans(), plan => plan.Chip == chip);

    /// <summary>Şeritteki her düğmenin bir planı var, fazlası da yok.</summary>
    [Fact]
    public void SeritteBirPlansizYongaYok()
    {
        var markup = File.ReadAllText(TipSources.WindowXamlPath);

        var chips = Regex.Matches(markup, """x:Name="(?<name>Chip[\w]*)"[^>]*?Theme="\{StaticResource ChipButton\}""")
            .Select(match => match.Groups["name"].Value)
            .ToList();

        Assert.True(chips.Count > 0, "Biçimlemede ChipButton temalı düğme bulunamadı; tarayıcı ölü.");

        var mapped = Plans().Select(plan => plan.Chip).ToList();

        Assert.Equal(chips.OrderBy(name => name, StringComparer.Ordinal).ToList(),
                     mapped.OrderBy(name => name, StringComparer.Ordinal).ToList());
    }

    [Theory]
    [InlineData("Chip8", 8d, Intent.SocialMedia, CodecPreference.Compatible, FillPolicy.FillTarget)]
    [InlineData("ChipWhatsApp", 16d, Intent.Sharing, CodecPreference.Compatible, FillPolicy.FillTarget)]
    [InlineData("Chip25", 25d, Intent.Sharing, CodecPreference.Compatible, FillPolicy.FillTarget)]
    [InlineData("Chip100", 100d, Intent.Sharing, CodecPreference.Auto, FillPolicy.FillTarget)]
    [InlineData("Chip128", 128d, Intent.Sharing, CodecPreference.Auto, FillPolicy.FillTarget)]
    [InlineData("Chip180", 180d, Intent.Sharing, CodecPreference.Auto, FillPolicy.FillTarget)]
    public void MbYazanYongalarKendiTavaniniTasiyor(
        string chip, double target, Intent intent, CodecPreference codec, FillPolicy fill)
    {
        var plan = Plan(chip);

        Assert.Equal(target, plan.TargetMb);
        Assert.True(plan.SizeCapped, $"{chip} boyut tavanı taşımıyor.");
        Assert.Equal(intent, plan.Intent);
        Assert.Equal(codec, plan.Codec);
        Assert.Equal(fill, plan.Fill);
    }

    /// <summary>
    /// Yarıya indir kaynağa göre hesaplanır: sabit MB yazmaz ama yine bir boyut tavanıdır.
    /// </summary>
    [Fact]
    public void YariyaIndirKaynaktanTuretilir()
    {
        var plan = Plan("ChipHalf");

        Assert.Null(plan.TargetMb);
        Assert.True(plan.SizeCapped, "Yarıya indir boyut tavanı taşımıyor.");
        Assert.Equal(Intent.Sharing, plan.Intent);
        Assert.Equal(FillPolicy.QualityCeiling, plan.Fill);
    }

    /// <summary>
    /// Arşiv yongasının <b>boyut tavanı yoktur</b> ve bu ölçüm eşleme tablosunu değil
    /// <b>davranışı</b> okur: gerçek pencerede "8" yongası seçilip ardından "Arşiv"
    /// yongasına basılır, sonra motora giden <see cref="PlanOptions"/> okunur.
    ///
    /// <para>Eskiden burada <c>ChipPlans()</c> kaydı okunuyordu; kayıt doğruyken plan
    /// kurulumu 8 MB'lık katı tavanı motora taşımaya devam ediyordu ve ölçüm bunu
    /// göremiyordu. Tavansızlığın sayısal karşılığı
    /// <see cref="PlanCalculator.QualityCeilingTargetMb"/>.</para>
    /// </summary>
    [Fact]
    public void ArsivYongasiSecilinceMotoraTavansizPlanGidiyor()
    {
        var info = Source();

        var reading = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = Sandbox() };
            try
            {
                window.UseTurkish();
                window.LoadWithoutProbing(info.FilePath, info);

                Click(window.Chip8);
                var capped = window.PlanOptionsForTest();

                Click(window.ChipArchive);
                var archived = window.PlanOptionsForTest();

                return (capped, archived, Box: window.TxtTarget.Text ?? "", Line: window.TxtChipDerivation.Text ?? "");
            }
            finally { window.Close(); }
        });

        var ceiling = PlanCalculator.QualityCeilingTargetMb(info);

        Assert.Equal(8d, reading.capped.TargetMb, 3);

        Assert.True(
            reading.archived.TargetMb > reading.capped.TargetMb,
            $"Arşiv yongasından sonra motora giden hedef {reading.archived.TargetMb:0.##} MB; "
            + $"8 yongasının {reading.capped.TargetMb:0.##} MB tavanı hâlâ yürürlükte.");

        Assert.Equal(ceiling, reading.archived.TargetMb, 3);
        Assert.Equal(Intent.Archive, reading.archived.Intent);
        Assert.Equal(CodecPreference.MaxCompression, reading.archived.Codec);
        Assert.Equal(FillPolicy.QualityCeiling, reading.archived.FillPolicy);

        Assert.Equal("8", reading.Box);
        Assert.Contains("tavanı yok", reading.Line, StringComparison.OrdinalIgnoreCase);

        Assert.Single(Plans(), plan => !plan.SizeCapped);
    }

    /// <summary>
    /// Tavansızlık kalıcı bir hapis değil: hedefe elle dokunmak boyut tavanını geri
    /// getirir ve yazılan sayı yeniden motora gider.
    /// </summary>
    [Fact]
    public void HedefeDokunmakTavaniGeriGetiriyor()
    {
        var info = Source();

        var target = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = Sandbox() };
            try
            {
                window.UseTurkish();
                window.LoadWithoutProbing(info.FilePath, info);

                Click(window.ChipArchive);
                window.TxtTarget.Text = "12";

                return (window.PlanOptionsForTest().TargetMb, Line: window.TxtChipDerivation.Text ?? "");
            }
            finally { window.Close(); }
        });

        Assert.Equal(12d, target.Item1, 3);
        Assert.Contains("12", target.Line, StringComparison.Ordinal);
    }
}
