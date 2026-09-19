using System.Text.RegularExpressions;
using VidShrink.Core.Setup;

namespace VidShrink.Tests;

/// <summary>
/// Kurucunun dili. Uygulama kırk iki dil taşıyor ama kurucu onun çeviri katmanını göremiyor:
/// kırpılmış tek dosya, <c>InvariantGlobalization</c>, App'e referans yok ve ilk konuştuğunda
/// diskte <c>Locales</c> klasörü yok. Karşılığı <see cref="SetupText"/>'teki gömülü tr+en tablo.
///
/// <para>Bu sınıfın son ölçüsü bir <b>kaynak pimi</b>: kurucunun kodunda Türkçe cümle taşıyan
/// tek dosya <c>SetupText.cs</c>. Tablo doğru olsa bile biri yarın doğrudan cümle yazarsa
/// kurucu yine tek dilli bir yüzey kazanır, ve yalnız bu pim onu görür.</para>
/// </summary>
public class KurucuDiliTests
{
    private static readonly string Root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly Regex Placeholder = new(@"\{(\d+)", RegexOptions.Compiled);

    private static readonly Regex Turkish = new(@"[çğıöşüÇĞİÖŞÜ]", RegexOptions.Compiled);

    private static readonly Regex Literal = new("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

    /// <summary>Her anahtar iki dilde de dolu: eksik çeviri anahtarın kendisini bastırır.</summary>
    [Fact]
    public void HerAnahtarIkiDildeDolu()
    {
        var bos = new List<string>();
        foreach (var key in SetupText.Keys)
        {
            foreach (var language in new[] { "tr", "en" })
            {
                var text = SetupText.GetIn(language, key);
                if (string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal))
                    bos.Add($"{key} [{language}]");
            }
        }

        Assert.Equal(Array.Empty<string>(), bos.ToArray());
    }

    /// <summary>
    /// Yer tutucular iki dilde aynı. Biri İngilizce çeviride bir <c>{0}</c> düşürürse
    /// kullanıcı hatanın hangi dosyada olduğunu hiç görmez.
    /// </summary>
    [Fact]
    public void YerTutucularIkiDildeAyni()
    {
        var ayrisan = new List<string>();
        foreach (var key in SetupText.Keys)
        {
            var tr = Indices(SetupText.GetIn("tr", key));
            var en = Indices(SetupText.GetIn("en", key));
            if (!tr.SequenceEqual(en)) ayrisan.Add($"{key}: tr={string.Join(",", tr)} en={string.Join(",", en)}");
        }

        Assert.Equal(Array.Empty<string>(), ayrisan.ToArray());
    }

    /// <summary>
    /// <c>SetupDownloads</c>'un 404 yakalaması indirme hatasının metnini eşleştiriyor.
    /// İki dilden birinde <c>HTTP {0}</c> düşerse eksik varlık mesajı sessizce genel
    /// indirme hatasına dönüşür.
    /// </summary>
    [Fact]
    public void IndirmeHatasiIkiDildeDeHttpKoduTasiyor()
    {
        Assert.Contains("HTTP 404", SetupText.GetIn("tr", "setup.download.failed", 404, "u"), StringComparison.Ordinal);
        Assert.Contains("HTTP 404", SetupText.GetIn("en", "setup.download.failed", 404, "u"), StringComparison.Ordinal);
    }

    /// <summary>Kullanılmayan anahtar tabloyu şişiriyor; her anahtar kodda geçiyor.</summary>
    [Fact]
    public void KullanilmayanAnahtarYok()
    {
        var kaynak = string.Concat(Sources()
            .Where(path => Path.GetFileName(path) != "SetupText.cs")
            .Append(Path.Combine(Root, "src", "VidShrink.Core", "UpdateCheck.cs"))
            .Select(File.ReadAllText));
        var kullanilmayan = SetupText.Keys
            .Where(key => !kaynak.Contains($"\"{key}\"", StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Array.Empty<string>(), kullanilmayan);
    }

    /// <summary>
    /// Kaynak pimi: kurucunun kodunda Türkçe cümle taşıyan tek dosya <c>SetupText.cs</c>.
    /// Kapsam kullanıcıya çıkan yüzey — <c>Core/Setup</c> ile <c>VidShrink.Setup</c>.
    /// </summary>
    [Fact]
    public void KurucununTekTurkceDosyasiSetupText()
    {
        var suclu = new List<string>();
        foreach (var path in Sources())
        {
            if (Path.GetFileName(path) == "SetupText.cs") continue;
            var line = 0;
            foreach (var text in File.ReadLines(path))
            {
                line++;
                var trimmed = text.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal))
                    continue;
                foreach (Match match in Literal.Matches(text))
                {
                    var value = match.Groups[1].Value;
                    if (value.Length > 3 && Turkish.IsMatch(value))
                    {
                        suclu.Add($"{Path.GetFileName(path)}:{line}");
                        break;
                    }
                }
            }
        }

        Assert.Equal(Array.Empty<string>(), suclu.ToArray());
    }

    private static IEnumerable<string> Sources() =>
        Directory.EnumerateFiles(Path.Combine(Root, "src", "VidShrink.Core", "Setup"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(Root, "src", "VidShrink.Setup"), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static int[] Indices(string text) =>
        Placeholder.Matches(text).Select(m => int.Parse(m.Groups[1].Value)).Distinct().OrderBy(i => i).ToArray();
}
