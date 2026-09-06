using System.Text.RegularExpressions;
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
    /// Arşiv yongasının <b>boyut tavanı yoktur</b>: motor kalite tavanına kadar bit
    /// harcar. Tavansızlığı <c>TargetMb</c> boşluğundan okumak yetmez — yarıya indir de
    /// boş, ayrım <c>SizeCapped</c>'te.
    /// </summary>
    [Fact]
    public void ArsivYongasininBoyutTavaniYok()
    {
        var plan = Plan("ChipArchive");

        Assert.False(plan.SizeCapped, "Arşiv yongası boyut tavanı taşıyor.");
        Assert.Null(plan.TargetMb);
        Assert.Equal(Intent.Archive, plan.Intent);
        Assert.Equal(CodecPreference.MaxCompression, plan.Codec);
        Assert.Equal(FillPolicy.QualityCeiling, plan.Fill);

        Assert.Single(Plans(), other => !other.SizeCapped);
    }
}
