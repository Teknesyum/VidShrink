using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using VidShrink.App.Localization;
using VidShrink.Core.Setup;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Sağ tık menüsünün etiketi üç yerden yazılıyor: PowerShell kurucusu, kurucu exe
/// (<see cref="ShellRegistration"/>) ve uygulamanın kendisi. Uygulama tarafı 42 dilin
/// hepsini biliyordu, iki kurucu ise <c>tr</c> ile <c>en</c> arasında seçim yapıyordu;
/// Almanca Windows'ta kurup uygulamayı hiç açmadan sağ tıklayan kullanıcı İngilizce
/// etiket görüyordu.
/// </summary>
/// <remarks>
/// Ölçülen şey kurucunun <b>yayına kopyalanan çeviriyi okuduğu</b>: etiket artık
/// <c>Locales/&lt;dil&gt;/main.json</c>'daki <c>shell.menu.open</c> anahtarından geliyor,
/// kaynak uygulamanınkiyle aynı. Klasör yerinde değilse eski iki dilli kola düşülüyor —
/// kurucu yarım bir ağaçta da menü yazabilmeli, bu yüzden düşüş kolu da pimli.
/// </remarks>
public class KurucuMenuDiliTests
{
    private static string Locales =>
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales");

    private static string Katalog(string dil, string anahtar)
    {
        using var belge = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Locales, dil, "main.json")));
        return belge.RootElement.GetProperty(anahtar).GetString()!;
    }

    [Theory]
    [InlineData("de")]
    [InlineData("ja")]
    [InlineData("zh-Hans")]
    [InlineData("ar")]
    [InlineData("tr")]
    public void EtiketCeviriDosyasindanGeliyor(string dil)
    {
        Assert.Equal(Katalog(dil, "shell.menu.open"), ShellRegistration.OpenLabel(dil, Locales));
        Assert.Equal(Katalog(dil, "shell.menu.shrink"), ShellRegistration.ShrinkLabel(dil, Locales));
    }

    /// <summary>
    /// Olumsuz kontrol: çeviri okunmasaydı Almanca etiket İngilizce gömülü metin olurdu.
    /// Bu iddia tutmazsa üstteki ölçü dosyayı hiç okumadan da yeşil dönebilirdi.
    /// </summary>
    [Fact]
    public void GomuluMetinAlmancaDegil()
    {
        var almanca = ShellRegistration.OpenLabel("de", Locales);
        Assert.NotEqual("Open this video with VidShrink", almanca);
        Assert.NotEqual("Bu Videoyu VidShrink ile Aç", almanca);
    }

    /// <summary>Klasör yoksa kurucu çakmaz, bugünkü iki dilli metne düşer.</summary>
    [Theory]
    [InlineData("tr", "Bu Videoyu VidShrink ile Aç")]
    [InlineData("en", "Open this video with VidShrink")]
    [InlineData("de", "Open this video with VidShrink")]
    public void KlasorYokkaGomuluMetneDusuyor(string dil, string beklenen)
    {
        var olmayan = Path.Combine(Path.GetTempPath(), "vidshrink-locales-yok-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(beklenen, ShellRegistration.OpenLabel(dil, olmayan));
        Assert.Equal(beklenen, ShellRegistration.OpenLabel(dil, null));
    }

    [Theory]
    [InlineData("de-DE", "de")]
    [InlineData("zh-Hans-CN", "zh-Hans")]
    [InlineData("pt-BR", "pt")]
    [InlineData("TR-tr", "tr")]
    [InlineData("kl-GL", "en")]
    [InlineData("", "en")]
    public void IsletimSistemininEtiketiKlasoreEsleniyor(string sistem, string beklenen) =>
        Assert.Equal(beklenen, ShellRegistration.ResolveLanguage("auto", () => sistem, Locales));

    /// <summary>Kullanıcının seçimi işletim sistemini eziyor; tanınmayan seçim düşmüyor.</summary>
    [Fact]
    public void SecimSistemiEziyor()
    {
        Assert.Equal("ja", ShellRegistration.ResolveLanguage("ja", () => "de-DE", Locales));
        Assert.Equal("de", ShellRegistration.ResolveLanguage("uydurma", () => "de-DE", Locales));
        Assert.Equal("en", ShellRegistration.ResolveLanguage("auto", () => throw new InvalidOperationException(), Locales));
    }

    /// <summary>
    /// Klasör verilmediğinde eşleme eski haline iner: yalnız <c>tr</c> ve <c>en</c>.
    /// Bu kolu kaybetmek kurucuyu çevirisiz ağaçta İngilizceye değil boşluğa düşürürdü.
    /// </summary>
    [Theory]
    [InlineData("de-DE", "en")]
    [InlineData("tr-TR", "tr")]
    public void KlasorsuzEslemeIkiDilde(string sistem, string beklenen) =>
        Assert.Equal(beklenen, ShellRegistration.ResolveLanguage("auto", () => sistem, null));

    /// <summary>Uygulamanın yazdığı etiketle kurucununki aynı dilde aynı metin.</summary>
    [Theory]
    [InlineData("de")]
    [InlineData("ja")]
    [InlineData("tr")]
    public void UygulamaVeKurucuAyniEtiketiYaziyor(string dil)
    {
        var onceki = Strings.Language;
        try
        {
            Strings.Use(dil);
            Assert.Equal(Strings.Get("shell.menu.open"), ShellRegistration.OpenLabel(dil, Locales));
            Assert.Equal(Strings.Get("shell.menu.shrink"), ShellRegistration.ShrinkLabel(dil, Locales));
        }
        finally
        {
            Strings.Use(onceki);
        }
    }

    /// <summary>
    /// PowerShell kurucusu da aynı dosyayı okuyor. Betik koşulmadan, iki yazıcının
    /// aynı anahtara baktığı kaynaktan pimleniyor; betiğin gömülü metinleri yalnız
    /// düşüş kolunda kalmış olmalı.
    /// </summary>
    [Fact]
    public void BetikDeAyniAnahtariOkuyor()
    {
        var betik = File.ReadAllText(Path.Combine(TipSources.Root, "Install-VidShrink.ps1"));

        Assert.Contains("'shell.menu.open'", betik, StringComparison.Ordinal);
        Assert.Contains("'shell.menu.shrink'", betik, StringComparison.Ordinal);
        Assert.Contains("Resolve-ShellMenuLanguage", betik, StringComparison.Ordinal);

        var gomulu = betik.Split('\n')
            .Where(s => s.Contains("Open this video with VidShrink", StringComparison.Ordinal))
            .ToArray();
        Assert.Single(gomulu);
    }

    /// <summary>
    /// Kurucu çeviri klasörünü gerçekten geçiriyor. Bu satır olmadan gövde doğru çalışır
    /// ama kurucu ona hiç klasör vermez ve her dil İngilizceye düşerdi; ilk mutasyon
    /// turunda bu kesim sıfır kırmızı verdiği için ayrı pim yazıldı.
    /// </summary>
    [Fact]
    public void KurucuCeviriKlasorunuGeciriyor()
    {
        var kaynak = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.Core", "Setup", "SetupRunner.cs"));

        Assert.Contains("ShellRegistration.LocalesFolder(root)", kaynak, StringComparison.Ordinal);
        Assert.Contains("ResolveLanguage(options.MenuLanguage, host.UiLanguage, locales)", kaynak, StringComparison.Ordinal);
        Assert.Contains("WriteMenus(options.ClassesRoot, installedExe, language, locales)", kaynak, StringComparison.Ordinal);
    }

    /// <summary>
    /// Kataloğun kendisi eksik olmasın: iki anahtar 42 dilin hepsinde dolu.
    /// </summary>
    [Fact]
    public void IkiAnahtarButunDillerdeVar()
    {
        var diller = Directory.GetDirectories(Locales).Select(Path.GetFileName).ToArray();
        Assert.True(diller.Length >= 42, $"dil sayısı {diller.Length}");

        foreach (var dil in diller)
        foreach (var anahtar in new[] { "shell.menu.open", "shell.menu.shrink" })
            Assert.False(string.IsNullOrWhiteSpace(Katalog(dil!, anahtar)), $"{dil}/{anahtar}");
    }
}
