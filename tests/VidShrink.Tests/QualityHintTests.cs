using System.Diagnostics;
using System.Text.RegularExpressions;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// T36 İş 3: her yongada kalite tahmini paneli.
///
/// Panelin verisi <see cref="QualityHint"/>; çizim onu yalnız okur. Ölçüm bu yüzden veri
/// katmanında yapılabiliyor ve pencere açmayı gerektirmiyor.
/// </summary>
public sealed class QualityHintTests
{
    private static MediaInfo SampleInfo() => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    private static ComplexityProfile MeasuredProfile()
        => ComplexityProfile.FromProbe(0.09, 0.045, 6, 180);

    private static PlanOptions Options() => new() { Intent = Intent.Sharing, Codec = CodecPreference.Compatible };

    /// <summary>Yedi yonga ve hedefleri. <c>Half</c> kaynaktan türetildiği için burada yok.</summary>
    public static TheoryData<double> ChipTargets() => new() { 8, 16, 25, 100, 128, 180 };

    /// <summary>
    /// K: yedi yonganın her birinde panel açılıyor. Yongalar ve balonları XAML'den,
    /// hesaba giren liste koddan okunuyor; ikisi ayrışırsa bir yonga sessizce panelsiz kalır.
    /// </summary>
    [Fact]
    public void EverySevenChipsCarryATooltipPanelAndAreListedInTheCode()
    {
        var xaml = File.ReadAllText(TipSources.WindowXamlPath);
        var start = xaml.IndexOf("x:Name=\"ChipWhatsApp\"", StringComparison.Ordinal);
        var end = xaml.IndexOf("</WrapPanel>", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Hedef yongalarının WrapPanel'i bulunamadı.");

        var block = xaml[start..end];
        var chips = Regex.Matches(block, "x:Name=\"(Chip\\w+)\"").Select(match => match.Groups[1].Value).ToList();
        chips.Insert(0, "ChipWhatsApp");
        chips = chips.Distinct().ToList();

        Assert.Equal(
            new[] { "ChipWhatsApp", "Chip8", "Chip25", "Chip100", "Chip128", "Chip180", "ChipHalf" }.Order(),
            chips.Order());

        // Her yonganın balonu bir StackPanel: ilk çocuk ipucu, kalanını panel dolduruyor.
        Assert.Equal(chips.Count, Regex.Matches(block, "<ToolTip.Tip>\\s*<StackPanel").Count);

        var code = File.ReadAllText(TipSources.WindowCodePath);
        var listed = Regex.Match(code, """\(ChipWhatsApp, 16\), \(Chip8, 8\), \(Chip25, 25\), \(Chip100, 100\),\s*\(Chip128, 128\), \(Chip180, 180\), \(ChipHalf, null\)""");
        Assert.True(listed.Success, "QualityChips() yedi yongayı listelemiyor.");
    }

    /// <summary>
    /// K: video yüklü değilken skor boş. Sıfır ya da uydurma bir sayı dönmez.
    /// </summary>
    [Fact]
    public void WithNoVideoLoadedTheScoreIsEmpty()
    {
        var hint = QualityHint.For(null, Options(), 16, null, null);

        Assert.Null(hint.Score);
        Assert.Null(hint.TargetMb);
        Assert.Equal(QualityBasis.NoSource, hint.Basis);
    }

    /// <summary>Hedefi olmayan yonga (kaynaksız <c>Half</c>) da boş kalır.</summary>
    [Fact]
    public void WithNoTargetTheScoreIsEmpty()
        => Assert.Equal(QualityBasis.NoSource, QualityHint.For(SampleInfo(), Options(), null, null, null).Basis);

    /// <summary>
    /// K: video yüklüyken skor dolu. Yedi yonganın altısı sabit hedefli; hepsi için skor
    /// gelir ve 0-100 aralığında kalır.
    /// </summary>
    [Theory]
    [MemberData(nameof(ChipTargets))]
    public void WithAVideoLoadedEveryChipGetsAScore(double targetMb)
    {
        var hint = QualityHint.For(SampleInfo(), Options(), targetMb, null, null);

        Assert.Equal(targetMb, hint.TargetMb);
        Assert.NotNull(hint.Score);
        Assert.InRange(hint.Score!.Value, 0, 100);
        Assert.Equal(100 - hint.Score.Value, hint.LossPoints, 6);
    }

    /// <summary>Yumuşak hedefte skor sıkı hedeftekinden düşük olamaz.</summary>
    [Fact]
    public void ALooserTargetNeverScoresWorseThanATighterOne()
    {
        var tight = QualityHint.For(SampleInfo(), Options(), 8, null, null).Score;
        var loose = QualityHint.For(SampleInfo(), Options(), 180, null, null).Score;

        Assert.NotNull(tight);
        Assert.NotNull(loose);
        Assert.True(loose >= tight, $"180 MB skoru ({loose:0.#}) 8 MB skorunun ({tight:0.#}) altında.");
    }

    /// <summary>
    /// K: tahmin ölçüm gibi sunulmayacak. Kalibrasyon ölçümü yokken dayanak
    /// <see cref="QualityBasis.Estimated"/>, ölçüm varken <see cref="QualityBasis.Measured"/>
    /// olur ve panel ikisini ayrı kelimeyle yazar.
    /// </summary>
    [Fact]
    public void AnEstimateIsNeverLabelledAsAMeasurement()
    {
        Assert.Equal(QualityBasis.Estimated, QualityHint.For(SampleInfo(), Options(), 16, null, null).Basis);
        Assert.Equal(QualityBasis.Measured, QualityHint.For(SampleInfo(), Options(), 16, MeasuredProfile(), null).Basis);

        var code = File.ReadAllText(TipSources.WindowCodePath);
        Assert.Contains("Kaynak bit hızından tahmin edildi", code, StringComparison.Ordinal);
        Assert.Contains("Estimated from the source bitrate", code, StringComparison.Ordinal);
        Assert.Contains("Bu klipten kodlanan örnekle ölçüldü", code, StringComparison.Ordinal);
        Assert.Contains("Measured from a sample encoded from this clip", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Hedef kaynaktan büyükse yeniden kodlama olmaz. Panel bunu üçüncü bir cümleyle söyler,
    /// yoksa 100/100 bir ölçüm gibi okunurdu.
    /// </summary>
    [Fact]
    public void ATargetAboveTheSourceIsReportedAsACopy()
    {
        var hint = QualityHint.For(SampleInfo(), Options(), 900, null, null);

        Assert.Equal(QualityBasis.SourceUnderTarget, hint.Basis);
        Assert.Equal(0, hint.LossPoints, 6);
    }

    /// <summary>
    /// K: bekletme yok. Yedi panelin tamamı fare üstüne gelmeden önce kuruluyor, bu yüzden
    /// asıl maliyet yedi <c>BuildDetailed</c> çağrısı. Ölçüm bunu tavana bağlar; hesap bir
    /// gün ffmpeg çağırmaya başlarsa süre bu tavanı saniyelerle aşar ve ölçüm kırılır.
    /// </summary>
    [Fact]
    public void SevenPanelsAreComputedWellUnderAFrame()
    {
        var info = SampleInfo();
        var targets = new double[] { 8, 16, 25, 100, 128, 180, 250 };

        // JIT ısınması ölçüme karışmasın.
        foreach (var target in targets) QualityHint.For(info, Options(), target, null, null);

        var clock = Stopwatch.StartNew();
        for (var round = 0; round < 20; round++)
            foreach (var target in targets)
                QualityHint.For(info, Options(), target, null, null);
        clock.Stop();

        var perRefresh = clock.Elapsed.TotalMilliseconds / 20;
        // Ölçülen: yedi panel 0,26 ms (Release, 8.0.424). Tavan 20 ms, yani yetmiş kat pay.
        Assert.True(perRefresh < 20, $"Yedi panelin hesabı {perRefresh:0.##} ms sürdü.");
    }

    /// <summary>
    /// Hesap yolunda süreç başlatan tek bir satır olmayacak. Süre ölçümü yavaşlığı yakalar,
    /// bu ölçüm niyeti yakalar: <c>QualityHint</c> yalnız <c>PlanCalculator</c> çağırır.
    /// </summary>
    [Fact]
    public void TheScorePathStartsNoProcess()
    {
        var code = File.ReadAllText(TipSources.WindowCodePath);
        var start = code.IndexOf("internal readonly record struct QualityHint", StringComparison.Ordinal);
        var end = code.IndexOf("internal sealed record ShareTarget(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "QualityHint gövdesi bulunamadı.");

        var body = code[start..end];
        Assert.DoesNotContain("Process", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Ffprobe", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Ffmpeg", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("await", body, StringComparison.Ordinal);
        Assert.Contains("PlanCalculator.BuildDetailed", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// K: yeni etiketler ve panel metinleri iki dilde. Panel metni çalışma zamanında
    /// kuruluyor, bu yüzden <c>T(turkish, english)</c> çiftleri kaynakta aranıyor; kataloğu
    /// yalnız XAML'deki sabit metin kullanır.
    /// </summary>
    [Theory]
    [InlineData("WhatsApp recommended", "WhatsApp için önerilen")]
    [InlineData("Sharing maximum", "Paylaşım için en fazla")]
    [InlineData("WhatsApp Web maximum", "WhatsApp Web için en fazla")]
    [InlineData("Ceiling", "Tavan")]
    [InlineData("Lifetime", "Ömür")]
    [InlineData("Deletion", "Silme")]
    [InlineData("Delete the shared file", "Paylaşılan dosyayı sil")]
    public void EveryNewLabelHasBothLanguages(string english, string turkish)
    {
        var titled = LanguageCatalog.Title(english, false);

        Assert.True(LanguageCatalog.EnglishToTurkish.TryGetValue(titled, out var found), $"{english} sözlükte yok.");
        Assert.Equal(LanguageCatalog.Title(turkish, true), found);
    }

    /// <summary>
    /// Panelin çalışma zamanında kurulan her satırı iki dilde yazılmış olacak. Türkçe tarafı
    /// unutulursa <c>T()</c> çağrısının ikinci dizesi hiç eklenmez ve bu ölçüm kırılır.
    /// </summary>
    [Theory]
    [InlineData("Tahmini kalite", "Predicted quality")]
    [InlineData("Kaynağa göre kayıp", "Loss against the source")]
    [InlineData("Dayanak", "Basis")]
    [InlineData("Bir video yükleyin; bu hedefin tahmini kalite skoru burada çıkar.", "Load a video and this target's predicted quality score appears here.")]
    public void EveryPanelLineIsWrittenInBothLanguages(string turkish, string english)
    {
        var code = File.ReadAllText(TipSources.WindowCodePath);
        Assert.Contains($"\"{turkish}\"", code, StringComparison.Ordinal);
        Assert.Contains($"\"{english}\"", code, StringComparison.Ordinal);
    }
}
