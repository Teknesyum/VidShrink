using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class UygulamaAdiTests : IDisposable
{
    private static readonly Regex AdindanSonraHarf = new(@"\{app\}\p{L}", RegexOptions.Compiled);

    private static readonly HashSet<string> BitisikDiller = new(StringComparer.OrdinalIgnoreCase) { "ja", "zh-Hans" };

    private readonly string sandbox;

    public UygulamaAdiTests()
    {
        Strings.Reset();
        sandbox = Path.Combine(TestPaths.OutputRoot, "uygulama-adi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(sandbox, "en"));
    }

    public void Dispose()
    {
        Strings.Reset();
        try
        {
            Directory.Delete(sandbox, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static IEnumerable<(string Dil, string Dosya, string Anahtar, string Metin)> Metinler()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Locales");
        foreach (var folder in Directory.GetDirectories(root))
        {
            var dil = Path.GetFileName(folder);
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                var texts = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))!;
                foreach (var pair in texts)
                    yield return (dil, Path.GetFileName(file), pair.Key, pair.Value);
            }
        }
    }

    [Fact]
    public void AdKullanicininYazdigiGibi()
    {
        Assert.Equal("VidShrink Player", AppIdentity.Name);
    }

    [Fact]
    public void CevirilerdeAdDuzYazilmiyorYerTutucudanGeliyor()
    {
        var sikayet = new StringBuilder();
        var yerTutucu = 0;
        foreach (var (dil, dosya, anahtar, metin) in Metinler())
        {
            if (metin.Contains("VidShrink", StringComparison.Ordinal))
                sikayet.AppendLine($"{dil}/{dosya} {anahtar}: ad düz yazılmış");
            yerTutucu += Regex.Matches(metin, Regex.Escape(Strings.AppToken)).Count;
        }

        Assert.True(sikayet.Length == 0, sikayet.ToString());
        Assert.True(yerTutucu > 1000, $"yer tutucu sayısı {yerTutucu}");
    }

    [Fact]
    public void YerTutucuyaEkBitismiyor()
    {
        var sikayet = new StringBuilder();
        foreach (var (dil, dosya, anahtar, metin) in Metinler())
        {
            if (BitisikDiller.Contains(dil)) continue;
            if (AdindanSonraHarf.IsMatch(metin))
                sikayet.AppendLine($"{dil}/{dosya} {anahtar}: {AdindanSonraHarf.Match(metin).Value}");
        }

        Assert.True(sikayet.Length == 0, sikayet.ToString());
    }

    [Fact]
    public void YuklenenMetinAdiTekKaynaktanBasar()
    {
        File.WriteAllText(Path.Combine(sandbox, "en", "main.json"),
            "{\"t.ad\": \"{app} hazır\", \"t.bicim\": \"{app}: {0}\"}");
        Strings.UseRoot(sandbox);

        Assert.Equal($"{AppIdentity.Name} hazır", Strings.PeekIn("en", "t.ad"));
        Assert.Equal($"{AppIdentity.Name} hazır", Strings.GetIn("en", "t.ad"));
        Assert.Equal($"{AppIdentity.Name}: 7", Strings.GetIn("en", "t.bicim", 7));
    }
}
