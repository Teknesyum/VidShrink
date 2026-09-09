using System.Text.RegularExpressions;
using VidShrink.App;

namespace VidShrink.Tests;

/// <summary>
/// T25 K1: bütünüyle büyük harf hiçbir yerde kullanılmayacak. Kural sözleşmede yazılıydı
/// ama hiçbir ölçüm onu tutmuyordu; karşılaştırma panelinin rozetleri iki tur boyunca
/// <c>Upper()</c> ile büyük harf kaldı ve borç olarak iki mühre birden yazıldı.
///
/// Aynı kriter kısaltmaları ve özel adları muaf tutuyor (<c>MP4</c>, <c>GPU</c>,
/// <c>H.264</c>). Ölçüm bu ikisini uzunlukla ayırıyor: bu kod tabanındaki kısaltmaların
/// en uzunu dört harf, eşik beş harfte. Daha uzun bir kısaltma gelirse ölçüm onu
/// yakalar ve eşiğin yeniden düşünülmesi gerekir — sessizce geçirmez.
///
/// <para>T184: <c>ORİJİNAL</c> ve <c>İŞLENMİŞ</c> artık bu eşiğin yakaladığı örnekler
/// <b>değil</b>, <see cref="ShoutedByDesign"/> ile muaf tutulan iki anahtar. Kullanıcı
/// rozetlerin büyük harf olmasını istedi; ev kuralıyla çelişti ve cümle kazandı. Muafiyet
/// anahtar düzeyinde ve jokersiz, ama anahtarın <b>değerini</b> sınırlamıyor: o iki
/// anahtarın altına ne yazılırsa yazılsın burası susar. Değerleri
/// <c>PlaybackPanelTests</c> birebir pimliyor; o pim kalkarsa bu delik sessizce
/// genişler.</para>
/// </summary>
public sealed class CasingTests
{
    private const int AbbreviationCeiling = 4;

    private static readonly string AppRoot =
        Path.Combine(TipSources.Root, "src", "VidShrink.App");

    /// <summary>
    /// Metni tümüyle büyük harfe çeviren çağrı. <c>LanguageCatalog.Title</c> her kelimenin
    /// <b>ilk harfini</b> büyütür; o dilim bir bağırma değil, kuralın kendisidir.
    /// </summary>
    private static readonly Regex UpperCall = new(
        @"(?<!\[\.\.1\])\.ToUpper(Invariant)?\s*\(", RegexOptions.Compiled);

    private static readonly Regex VisibleText = new(
        "(?:Text|Content)=\"([^\"{}]*)\"", RegexOptions.Compiled);

    /// <summary>Beş harf ve üzeri, tümü büyük harf olan kelime.</summary>
    private static readonly Regex ShoutedWord = new(
        @"\p{Lu}{" + (AbbreviationCeiling + 1) + ",}", RegexOptions.Compiled);

    private static IEnumerable<string> Files(string extension) =>
        Directory.EnumerateFiles(AppRoot, "*" + extension, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void NoCodePathShoutsItsText()
    {
        var offenders = Files(".cs")
            .SelectMany(path => File.ReadAllLines(path)
                .Select((line, index) => (path, line, number: index + 1))
                .Where(entry => UpperCall.IsMatch(entry.line)))
            .Select(entry => $"{Path.GetFileName(entry.path)}:{entry.number}: {entry.line.Trim()}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Metni tümüyle büyük harfe çeviren çağrı var:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void NoVisibleLabelIsWrittenInCapitals()
    {
        var offenders = new List<string>();

        foreach (var path in Files(".axaml"))
        {
            foreach (Match match in VisibleText.Matches(File.ReadAllText(path)))
            {
                var text = match.Groups[1].Value;
                if (!ShoutedWord.IsMatch(text)) continue;

                offenders.Add($"{Path.GetFileName(path)}: {text}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Tümü büyük harf yazılmış görünür metin var:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// T184/K3: kurala bilerek açılmış iki delik. Karşılaştırma panelinin iki rozeti —
    /// solda kaynağın, sağda çıktının etiketi — kullanıcının istediği biçimde, tümü büyük
    /// harfle yazılıyor. Liste tam bu iki anahtarla sınırlı; üçüncü bir bağıran metin
    /// eklenirse ölçü yine düşer.
    /// </summary>
    private static readonly HashSet<string> ShoutedByDesign = new()
    {
        "playback.badge.original",
        "playback.badge.processed"
    };

    /// <summary>
    /// Sözlükteki hiçbir çeviri bağırmayacak. Rozetler kaynağı değil sözlüğü okuyor;
    /// yalnız XAML'e bakan bir ölçüm onları göremezdi.
    /// </summary>
    [Fact]
    public void NoTranslationIsWrittenInCapitals()
    {
        var offenders = Locales.Languages
            .SelectMany(language => Locales.Values(language)
                .Where(pair => !ShoutedByDesign.Contains(pair.Key) && ShoutedWord.IsMatch(pair.Value))
                .Select(pair => $"{language}/{pair.Key} -> {pair.Value}"))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Sözlükte tümü büyük harf yazılmış metin var:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// T65 K1: ölçü birimi ve kodlayıcı tanımlayıcısı büyük harf geçidinden yazıldığı gibi
    /// çıkar. <c>Ms</c> SI'da megasaniyedir ve <c>Libx264</c> diye bir ffmpeg kodlayıcısı
    /// yoktur — kullanıcı kodlayıcı adını kaydedicisinin ayarlarında arıyor.
    ///
    /// <para>T192: beklenen değerlerin yarısı yeniden temellendirildi. Ölçünün tuttuğu şey
    /// birim ve kodlayıcı yazımıdır ve o hiç değişmedi; değişen, <b>cümlenin geri kalanının</b>
    /// nasıl büyütüldüğü. <c>ReadsAsProse</c> artık işlev sözcüğü (<c>the</c>, <c>of</c>,
    /// <c>is</c>, <c>on</c>) taşıyan metni gövde sayıyor, dolayısıyla "the probe took 120 ms"
    /// bir başlık değil bir cümle ve yalnız satır başı büyüyor. Pim susturulmadı: birim
    /// yazımını bozan bir mutasyon hâlâ kırmızıya döndürür.</para>
    /// </summary>
    [Theory]
    [InlineData("the probe took 120 ms", "The probe took 120 ms")]
    [InlineData("budget of 20000 ms", "Budget of 20000 ms")]
    [InlineData("target is 16 MB", "Target is 16 MB")]
    [InlineData("halve the fps", "Halve the fps")]
    [InlineData("software encoder libx264", "Software Encoder libx264")]
    [InlineData("hardware encoder h264_nvenc", "Hardware Encoder h264_nvenc")]
    [InlineData("hevc_qsv beats libsvtav1 on aac", "hevc_qsv beats libsvtav1 on aac")]
    public void UnitsAndEncoderNamesKeepTheirSpelling(string text, string expected)
        => Assert.Equal(expected, LanguageCatalog.Title(text, "en"));

    /// <summary>
    /// T65 K3: birim ve tanımlayıcı listesi tek bir bildirimde durur. İkinci bir kopya
    /// çıkarsa listeler bir süre sonra ayrışır, biri güncellenirken öteki eskir.
    /// </summary>
    [Fact]
    public void TheVerbatimListIsDeclaredOnce()
    {
        var declarations = Files(".cs")
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\bVerbatim\s*=\s*new"))
            .ToList();

        Assert.Single(declarations);
    }
}
