using System.Text.Json;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Varsayılan Neon'dan Teknesyum'a geçince işaretsiz ayar dosyasındaki <c>Neon</c> eski
/// varsayılandır: bir kez Teknesyum'a çevrilir ve işaret dosyaya yazılır. Sonra elle seçilen
/// Neon korunur, öteki temalara göç hiç dokunmaz.
/// </summary>
public sealed class TemaGocuTests
{
    private static string SettingsFile(string json)
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "tema-gocu");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "settings-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(file, json);
        return file;
    }

    private static JsonElement Root(string file) => JsonDocument.Parse(File.ReadAllText(file)).RootElement.Clone();

    [Fact]
    public void IsaretsizNeonBirKezTeknesyumaGocerVeIsaretYazilir()
    {
        var file = SettingsFile("{\"theme\":\"Neon\",\"targetMb\":42}");
        try
        {
            Assert.Equal("Teknesyum", AppSettings.Load(file).Theme);

            var root = Root(file);
            Assert.Equal("Teknesyum", root.GetProperty("theme").GetString());
            Assert.True(root.GetProperty(AppSettings.ThemeMigrationMarker).GetBoolean());
            Assert.Equal(42, root.GetProperty("targetMb").GetInt32());
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void GoctenSonraElleSecilenNeonKorunur()
    {
        var file = SettingsFile("{\"theme\":\"Neon\"}");
        try
        {
            var migrated = AppSettings.Load(file);
            Assert.Equal("Teknesyum", migrated.Theme);

            new UpdateSettings { TargetMb = 9 }.Save(file);
            migrated.Theme = "Neon";
            migrated.Save(file);

            Assert.Equal("Neon", AppSettings.Load(file).Theme);
            Assert.Equal("Neon", AppSettings.Load(file).Theme);
            Assert.Equal("Neon", Root(file).GetProperty("theme").GetString());
        }
        finally { File.Delete(file); }
    }

    [Theory]
    [InlineData("Dracula")]
    [InlineData("Teknesyum")]
    [InlineData("AyuLight")]
    [InlineData("neon")]
    [InlineData("")]
    public void GocOtekiTemalaraDokunmaz(string theme)
    {
        var file = SettingsFile("{\"theme\":\"" + theme + "\"}");
        try
        {
            Assert.Equal(theme, AppSettings.Load(file).Theme);
            Assert.Equal(theme, Root(file).GetProperty("theme").GetString());
            Assert.True(Root(file).GetProperty(AppSettings.ThemeMigrationMarker).GetBoolean());
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void YeniKayitIsaretTasirVeNeonuKorur()
    {
        var file = SettingsFile("{}");
        try
        {
            new AppSettings { Theme = "Neon" }.Save(file);
            Assert.True(Root(file).GetProperty(AppSettings.ThemeMigrationMarker).GetBoolean());
            Assert.Equal("Neon", AppSettings.Load(file).Theme);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void DosyaYoksaGocDosyaYaratmaz()
    {
        var file = Path.Combine(TestPaths.OutputRoot, "tema-gocu", "yok-" + Guid.NewGuid().ToString("N") + ".json");
        Assert.Equal("", AppSettings.Load(file).Theme);
        Assert.False(File.Exists(file));
    }
}
