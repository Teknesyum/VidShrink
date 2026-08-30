using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VidShrink.App.Localization;

namespace VidShrink.Tests;

public sealed class LocalizationTests : IDisposable
{
    private static readonly Regex KeyShape = new(@"^[a-z0-9]+([.\-][a-z0-9]+)*$", RegexOptions.Compiled);

    private readonly string sandbox;

    public LocalizationTests()
    {
        Strings.Reset();
        sandbox = Path.Combine(TestPaths.OutputRoot, "localization", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
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

    [Fact]
    public void SevkiyattakiDillerinAnahtarKumesiIngilizceyleBirebirAyni()
    {
        var reference = new SortedSet<string>(Strings.KeysOf(Strings.FallbackLanguage), StringComparer.Ordinal);
        var complaints = new StringBuilder();

        foreach (var language in Strings.Languages)
        {
            if (string.Equals(language, Strings.FallbackLanguage, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var keys = new SortedSet<string>(Strings.KeysOf(language), StringComparer.Ordinal);

            foreach (var missing in reference.Except(keys, StringComparer.Ordinal))
            {
                complaints.AppendLine($"'{missing}' anahtarı '{language}' dilinde eksik.");
            }

            foreach (var extra in keys.Except(reference, StringComparer.Ordinal))
            {
                complaints.AppendLine($"'{extra}' anahtarı '{language}' dilinde var ama '{Strings.FallbackLanguage}' dilinde yok.");
            }
        }

        Assert.True(complaints.Length == 0, complaints.ToString());
    }

    [Fact]
    public void SevkiyatDosyalariDuzSozlukVeAnahtarlarNoktaAyrilmisKucukHarf()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Locales");
        Assert.True(Directory.Exists(root), $"Locales klasörü çıktıya kopyalanmamış: {root}");

        var files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var texts = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
            Assert.NotNull(texts);

            foreach (var key in texts!.Keys)
            {
                Assert.True(KeyShape.IsMatch(key), $"'{key}' anahtarı ({file}) nokta.ayrilmis.kucuk-harf biçiminde değil.");
            }
        }
    }

    [Fact]
    public void DortAlanDosyasiHerDilIcinCiktidaVar()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Locales");

        foreach (var language in new[] { "en", "tr" })
        {
            foreach (var domain in new[] { "main", "playback", "performance", "settings" })
            {
                var file = Path.Combine(root, language, domain + ".json");
                Assert.True(File.Exists(file), $"eksik: {file}");
            }
        }
    }

    [Fact]
    public void DillerKlasorlerdenOkunurSahteDilGorulur()
    {
        Assert.Contains("en", Strings.Languages, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("tr", Strings.Languages, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("zz", Strings.Languages, StringComparer.OrdinalIgnoreCase);

        Write("zz", "main", new Dictionary<string, string> { ["olcum.baslik"] = "Zz" });
        Strings.UseRoot(sandbox);

        Assert.Contains("zz", Strings.Languages, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void YururlukteDiliVarsaOnunMetniDoner()
    {
        Sandbox();
        Strings.Use("tr");

        Assert.Equal("Türkçe metin", Strings.Get("olcum.metin"));
    }

    [Fact]
    public void YururlukteDildeYoksaIngilizceDoner()
    {
        Sandbox();
        Strings.Use("tr");

        Assert.Equal("English only", Strings.Get("olcum.yalniz-ingilizce"));
    }

    [Fact]
    public void IkisindeDeYoksaAnahtarinKendisiDoner()
    {
        Sandbox();
        Strings.AssertOnMissingKey = false;
        Strings.Use("tr");

        Assert.Equal("olcum.hicbir-yerde", Strings.Get("olcum.hicbir-yerde"));
    }

    [Fact]
    public void BicimParametreleriUygulanir()
    {
        Sandbox();
        Strings.Use("tr");

        Assert.Equal("2 dosya, 5 saniye", Strings.Get("olcum.bicim", 2, 5));
    }

    [Fact]
    public void DilDegisinceChangedTetiklenir()
    {
        var count = 0;
        void Handler(object? sender, EventArgs e) => count++;

        Strings.Changed += Handler;
        try
        {
            Strings.Use("tr");
            Strings.Use("tr");
            Strings.Use("en");
        }
        finally
        {
            Strings.Changed -= Handler;
        }

        Assert.Equal(2, count);
        Assert.Equal("en", Strings.Language);
    }

    private void Sandbox()
    {
        Write("en", "main", new Dictionary<string, string>
        {
            ["olcum.metin"] = "English text",
            ["olcum.yalniz-ingilizce"] = "English only",
            ["olcum.bicim"] = "{0} files, {1} seconds",
        });

        Write("tr", "main", new Dictionary<string, string>
        {
            ["olcum.metin"] = "Türkçe metin",
            ["olcum.bicim"] = "{0} dosya, {1} saniye",
        });

        Strings.UseRoot(sandbox);
    }

    private void Write(string language, string domain, Dictionary<string, string> texts)
    {
        var directory = Path.Combine(sandbox, language);
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, domain + ".json"),
            JsonSerializer.Serialize(texts, new JsonSerializerOptions { WriteIndented = true }));
    }
}
