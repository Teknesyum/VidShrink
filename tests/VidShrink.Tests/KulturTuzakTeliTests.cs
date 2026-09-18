using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VidShrink.App;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// <c>InvariantGlobalization</c> tuzak teli. O anahtar acikken
/// <c>CultureInfo.CurrentUICulture.Name</c> bos dizge olur; <c>MainWindow.axaml.cs</c>'deki
/// dil cozumu oraya bos dizge verir ve kaydedilmis dili olmayan kullanici 42 yerellestirme
/// dosyasinin hicbirini almadan Ingilizce acar.
///
/// <para><see cref="SettingsTests.LanguageUsesSavedThenOperatingSystemThenEnglish"/>'in
/// <c>(null, "", "en")</c> kolu bu <b>sonucu</b> belgeliyor ama tel degil: bos dizge orada
/// degismez bir <c>InlineData</c>, <c>ResolveLanguage</c> saf bir islev ve kulturu hic
/// okumuyor — anahtar acilsa o satir yesil kalirdi. Tel burada, cunku burasi anahtarin
/// kendisine ve kulturu okuyan <b>kabloya</b> bakiyor.</para>
///
/// <para>Calisma zamaninda olcmek ise yanlis yeri olcerdi: bayrak uygulamanin yayin
/// ayarinda acilsa bile test sureci kendi ayarlariyla kosar ve yesil kalirdi. Bu yuzden
/// olcu kaynagi okuyor.</para>
/// </summary>
public sealed class KulturTuzakTeliTests
{
    private readonly ITestOutputHelper _cikti;

    public KulturTuzakTeliTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static readonly Regex Anahtar = new(
        @"<(InvariantGlobalization|PredefinedCulturesOnly)>\s*(true|false)\s*</\1>|[-/]p:(InvariantGlobalization|PredefinedCulturesOnly)=(true|false)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Uygulamanin derlemesini besleyen dosyalar. Baska projelerin (<c>VidShrink.Launcher</c>,
    /// <c>VidShrink.Setup</c>, <c>tools/</c>) kendi bayragi var ve onlar dil dosyasi okumuyor;
    /// tel yalniz arayuzu tasiyan zincire bakiyor.
    /// </summary>
    private static IEnumerable<string> Zincir()
    {
        var kok = TipSources.Root;
        yield return Path.Combine(kok, "Directory.Build.props");
        yield return Path.Combine(kok, "src", "VidShrink.App", "VidShrink.App.csproj");
        var akis = Path.Combine(kok, ".github", "workflows");
        if (Directory.Exists(akis))
            foreach (var y in Directory.GetFiles(akis, "*.yml", SearchOption.AllDirectories))
                yield return y;
    }

    /// <summary>
    /// Arayuzu yayinlayan zincirin hicbir yerinde kulturu kapatan bayrak yok.
    /// </summary>
    [Fact]
    public void ArayuzZincirindeKulturKapatilmiyor()
    {
        var bulunan = new List<string>();
        foreach (var yol in Zincir().Where(File.Exists))
            foreach (Match m in Anahtar.Matches(File.ReadAllText(yol)))
                bulunan.Add($"{Path.GetFileName(yol)}: {m.Value}");

        _cikti.WriteLine("taranan " + Zincir().Count(File.Exists) + " dosya, bulunan " + bulunan.Count);
        Assert.True(bulunan.Count == 0,
            "Kulturu kapatan bayrak arayuz zincirine girdi: " + string.Join(" | ", bulunan)
            + ". Bayrak acilirsa CurrentUICulture.Name bos doner ve kaydedilmis dili olmayan "
            + "kullanici 42 dilden hicbirini almaz.");
    }

    /// <summary>
    /// Tarayicinin kor olmadiginin pozitif kontrolu: ayni desen, bayragi tasiyan gercek
    /// dosyalarda onu buluyor. Bu iki proje dil dosyasi okumuyor, bayraklari yerinde kaliyor.
    /// </summary>
    [Theory]
    [InlineData("VidShrink.Launcher")]
    [InlineData("VidShrink.Setup")]
    public void TarayiciBayragiGerektiginideBuluyor(string proje)
    {
        var yol = Path.Combine(TipSources.Root, "src", proje, proje + ".csproj");
        Assert.True(File.Exists(yol), yol + " yok.");
        Assert.Matches(Anahtar, File.ReadAllText(yol));
    }

    /// <summary>
    /// Kablonun kendisi: dil cozumu isletim sisteminin kulturunu <b>okuyor</b>. Biri bu
    /// okumayi sabit bir dizgeye cevirirse bayrak kapali olsa da kullanici sistem dilini
    /// kaybeder, ve yukaridaki iki olcu bunu goremez.
    /// </summary>
    [Fact]
    public void DilCozumuKulturuOkuyor()
    {
        var kaynak = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.axaml.cs"));
        var sayi = Regex.Matches(kaynak, @"ResolveLanguage\([^)]*CultureInfo\.CurrentUICulture\.Name\)").Count;
        _cikti.WriteLine($"ResolveLanguage(... CurrentUICulture.Name) {sayi} yerde");
        Assert.True(sayi >= 1, "Dil cozumu artik isletim sisteminin kulturunu okumuyor.");
    }

    /// <summary>
    /// Bos kulturun sonucu: kaydedilmis dili olmayan kullanici Ingilizce'ye duser. Bu kol
    /// tel degil, telin <b>neden</b> gerildigini gosteren olgu; gercek bir kulturle karsilastirilip
    /// ikisinin ayni olmadigi da olculuyor, yoksa iddia bosa doner.
    /// </summary>
    [Fact]
    public void BosKulturIngilizceyeDuserGercekKulturDusmez()
    {
        Assert.Equal("en", MainWindow.ResolveLanguage(null, string.Empty));
        Assert.Equal("tr", MainWindow.ResolveLanguage(null, CultureInfo.GetCultureInfo("tr-TR").Name));
    }
}
