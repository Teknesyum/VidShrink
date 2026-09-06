using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// T171: kabuk menusunden gelen kucultme istegini uretimde tuketen kol. Olcu uc kapiyi
/// tutar: bayrak varken istek cozulur mu, ikinci surec kuyrugun sahibi olmadigini gorur
/// mu, ve bes gerekcenin besi de ekrana bir cumle yazar mi.
/// </summary>
public sealed class KabukIstegiTests
{
    private readonly ITestOutputHelper _output;

    public KabukIstegiTests(ITestOutputHelper output) => _output = output;

    private static string SampleFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vidshrink-t171-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "ornek.mp4");
        File.WriteAllBytes(file, new byte[16]);
        return file;
    }

    [Fact]
    public void BayraksizBaslangicAnaPencereyeGider()
    {
        var file = SampleFile();
        Assert.Null(Program.StartupFor(new[] { file }));
        Assert.Null(Program.StartupFor(Array.Empty<string>()));
    }

    [Fact]
    public void BayrakliBaslangicIstegiCozer()
    {
        var file = SampleFile();
        var startup = Program.StartupFor(new[] { ShellIntegration.ShrinkFlag, "250", file });

        Assert.NotNull(startup);
        Assert.Null(startup!.Problem);
        Assert.NotNull(startup.Request);
        Assert.Equal(250, startup.Request!.TargetMegabytes);
        Assert.Equal(file, startup.Request.Path);
        _output.WriteLine($"hedef: {startup.Request.TargetMegabytes} MB  yol: {startup.Request.Path}");
    }

    [Fact]
    public void BayrakliBaslangicGerekceyiTasir()
    {
        var file = SampleFile();
        var startup = Program.StartupFor(new[] { ShellIntegration.ShrinkFlag, "333", file });

        Assert.NotNull(startup);
        Assert.Null(startup!.Request);
        Assert.Equal(ShrinkArgumentProblem.TargetNotInQuickList, startup.Problem);
        Assert.Equal(file, startup.FallbackPath);
    }

    [Fact]
    public void IkinciSurecKuyrugunSahibiDegil()
    {
        var channel = $"t171-{Guid.NewGuid():N}";
        using var first = new ShrinkRequestQueue(channel);
        using var second = new ShrinkRequestQueue(channel);

        Assert.True(Program.OwnsQueue(first), "ilk kuyruk sahibi olmali.");
        Assert.False(Program.OwnsQueue(second), "ikinci kuyruk sahip olmamali, istegi boruyla vermeli.");
        _output.WriteLine($"kanal: {channel}  ilk: {Program.OwnsQueue(first)}  ikinci: {Program.OwnsQueue(second)}");
    }

    [Fact]
    public void BesGerekceninHepsiAyriAnahtaraGider()
    {
        var members = Enum.GetValues<ShrinkArgumentProblem>();
        var keys = ShrinkProblemText.All.Select(ShrinkProblemText.Key).ToArray();

        Assert.Equal(members.Length, ShrinkProblemText.All.Count);
        Assert.Equal(members.OrderBy(m => m.ToString()), ShrinkProblemText.All.OrderBy(m => m.ToString()));
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        _output.WriteLine($"uye: {members.Length}  anahtar: {keys.Length}");
        foreach (var problem in ShrinkProblemText.All)
            _output.WriteLine($"  {problem} -> {ShrinkProblemText.Key(problem)}");
    }

    [Theory]
    [InlineData(ShrinkArgumentProblem.NoTarget)]
    [InlineData(ShrinkArgumentProblem.TargetNotANumber)]
    [InlineData(ShrinkArgumentProblem.TargetNotPositive)]
    [InlineData(ShrinkArgumentProblem.TargetNotInQuickList)]
    [InlineData(ShrinkArgumentProblem.NoPath)]
    public void GerekceCumlesiIkiDilde(ShrinkArgumentProblem problem)
    {
        var key = ShrinkProblemText.Key(problem);
        var en = Strings.GetIn("en", key, ShrinkProblemText.QuickList());
        var tr = Strings.GetIn("tr", key, ShrinkProblemText.QuickList());

        Assert.False(string.IsNullOrWhiteSpace(en), $"{key} en bos.");
        Assert.False(string.IsNullOrWhiteSpace(tr), $"{key} tr bos.");
        Assert.NotEqual(en, tr);
        Assert.DoesNotContain(key, en, StringComparison.Ordinal);
        Assert.DoesNotContain(key, tr, StringComparison.Ordinal);
        _output.WriteLine($"{problem}\n  en: {en}\n  tr: {tr}");
    }

    /// <summary>
    /// Bes gerekcenin ekran anahtari, olcunun kendi icinde harfi harfine yazili. Uretim
    /// tarafindaki <c>ShrinkProblemText.Key</c> degisirse beklenti onunla birlikte kaymaz;
    /// iki gerekce ayni anahtara baglanirsa bu tablo kirmizi olur.
    /// </summary>
    private static string BeklenenAnahtar(ShrinkArgumentProblem problem) => problem switch
    {
        ShrinkArgumentProblem.NoTarget => "main.shrink-job.reason.no-target",
        ShrinkArgumentProblem.TargetNotANumber => "main.shrink-job.reason.not-a-number",
        ShrinkArgumentProblem.TargetNotPositive => "main.shrink-job.reason.not-positive",
        ShrinkArgumentProblem.TargetNotInQuickList => "main.shrink-job.reason.not-in-quick-list",
        ShrinkArgumentProblem.NoPath => "main.shrink-job.reason.no-path",
        _ => throw new ArgumentOutOfRangeException(nameof(problem))
    };

    /// <summary>Pencerenin bastigi cumle ve onu bastigi dil, ayni ornekten okunmus hali.</summary>
    private readonly record struct PencereCiktisi(string Metin, string Dil);

    /// <summary>
    /// Pencereyi arayuz is parcaciginda kurar ve gorulen cumleyi <b>bastigi dille birlikte</b>
    /// dondurur. Dil de olcuye tasiniyor, cunku <c>Strings.Language</c> surec genelinde
    /// degisen bir durum: beklenen metni ondan okuyan olcu koşudan koşuya renk degistirir.
    /// </summary>
    private static PencereCiktisi PencereMetni(ShrinkArgumentProblem problem) => AppHost.Run(() =>
    {
        var window = new ShrinkJobWindow(new ShellShrinkStartup(null, problem, null), null);
        try
        {
            window.Begin();
            var metin = window.State == ShrinkJobState.Gerekce
                ? window.MessageText
                : $"<durum:{window.State}>";
            return new PencereCiktisi(metin, window.Language);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(ShrinkArgumentProblem.NoTarget)]
    [InlineData(ShrinkArgumentProblem.TargetNotANumber)]
    [InlineData(ShrinkArgumentProblem.TargetNotPositive)]
    [InlineData(ShrinkArgumentProblem.TargetNotInQuickList)]
    [InlineData(ShrinkArgumentProblem.NoPath)]
    public void GerekcePencereyeYazilir(ShrinkArgumentProblem problem)
    {
        var anahtar = BeklenenAnahtar(problem);
        var gorulen = PencereMetni(problem);
        var beklenen = LanguageCatalog.Title(
            Strings.GetIn(gorulen.Dil, anahtar, ShrinkProblemText.QuickList()),
            ShrinkJobWindow.Turkish(gorulen.Dil));

        Assert.Equal(anahtar, ShrinkProblemText.Key(problem));
        Assert.False(string.IsNullOrWhiteSpace(beklenen), $"{anahtar} karsiligi bos.");
        Assert.Equal(beklenen, gorulen.Metin);
        _output.WriteLine($"{problem} -> {anahtar}  dil: {gorulen.Dil}");
        _output.WriteLine($"  {gorulen.Metin}");
    }

    /// <summary>
    /// K11: olcunun kendisi surec genelindeki dilden bagimsiz mi. Beklenen metin pencerenin
    /// <b>kendi</b> dilinden hesaplandigi icin, olcu kosarken <c>Strings.Use</c> iki yone de
    /// cevrilse sonuc degismez. Eski surumde beklenen <c>Strings.Get</c> ile okunuyordu:
    /// asagidaki iki koldan biri her zaman kirmizi olurdu.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    public void GerekceOlcusuSurecDilindenEtkilenmez(string surecDili)
    {
        var onceki = Strings.Language;
        try
        {
            Strings.Use(surecDili);
            var gorulen = PencereMetni(ShrinkArgumentProblem.TargetNotInQuickList);
            var beklenen = LanguageCatalog.Title(
                Strings.GetIn(gorulen.Dil, ShrinkProblemText.TargetNotInQuickList, ShrinkProblemText.QuickList()),
                ShrinkJobWindow.Turkish(gorulen.Dil));

            _output.WriteLine($"surec dili: {surecDili}  pencere dili: {gorulen.Dil}");
            _output.WriteLine($"  {gorulen.Metin}");
            Assert.Equal(beklenen, gorulen.Metin);
        }
        finally { Strings.Use(onceki); }
    }

    [Fact]
    public void BesGerekceBesAyriCumleBasar()
    {
        var cumleler = ShrinkProblemText.All.Select(p => PencereMetni(p).Metin).ToArray();
        var ayri = cumleler.Distinct(StringComparer.Ordinal).Count();

        _output.WriteLine($"gerekce: {cumleler.Length}  ayri cumle: {ayri}");
        foreach (var (problem, cumle) in ShrinkProblemText.All.Zip(cumleler))
            _output.WriteLine($"  {problem} -> {cumle}");

        Assert.Equal(5, cumleler.Length);
        Assert.Equal(cumleler.Length, ayri);
    }

    /// <summary>
    /// K6: tek argv'de gelen her var olan yol icin bir istek. Kusur sinifi tam burada
    /// kapaniyor — ilk yol kucultulup kalanlarin sessizce dusmesi bu olcuyle kirmizi olur.
    /// </summary>
    [Fact]
    public void TekArgvdekiUcYolUcIstekUretir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vidshrink-t171-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var yollar = new[] { "a.mp4", "b.mp4", "c.mp4" }
            .Select(ad => Path.Combine(dir, ad))
            .ToArray();
        foreach (var yol in yollar) File.WriteAllBytes(yol, new byte[16]);

        var args = new List<string> { ShellIntegration.ShrinkFlag, "100" };
        args.AddRange(yollar);

        var startup = Program.StartupFor(args);

        Assert.NotNull(startup);
        Assert.Null(startup!.Problem);
        Assert.Equal(3, startup.Items.Count);
        Assert.Equal(yollar, startup.Items.Select(r => r.Path).ToArray());
        Assert.All(startup.Items, r => Assert.Equal(100, r.TargetMegabytes));
        _output.WriteLine($"argv yolu: {yollar.Length}  uretilen istek: {startup.Items.Count}");
        foreach (var istek in startup.Items)
            _output.WriteLine($"  {istek.TargetMegabytes} MB -> {istek.Path}");
    }

    /// <summary>
    /// K6/K7: pencere uc istegin ucunu de kabul etti mi. "Ucu de islendi" iddiasi surec
    /// sayimindan degil bu sayilan degerden gelir.
    /// </summary>
    [Fact]
    public void PencereUcIstegiDeKabulEder()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vidshrink-t171-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var yollar = new[] { "a.mp4", "b.mp4", "c.mp4" }
            .Select(ad => Path.Combine(dir, ad))
            .ToArray();
        foreach (var yol in yollar) File.WriteAllBytes(yol, new byte[16]);

        var args = new List<string> { ShellIntegration.ShrinkFlag, "100" };
        args.AddRange(yollar);
        var startup = Program.StartupFor(args);
        Assert.NotNull(startup);

        var kabul = AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(startup!, null);
            try
            {
                window.Begin();
                return window.AcceptedCount;
            }
            finally { window.Close(); }
        });

        _output.WriteLine($"AcceptedCount: {kabul}");
        Assert.Equal(3, kabul);
    }

    /// <summary>
    /// K8: kabuk istegi varken acilan pencere ilerleme penceresidir. Piksel genisligine
    /// degil, <c>App.StartupWindow</c>un dondurdugu turun kendisine bakar.
    /// </summary>
    [Fact]
    public void KabukIstegiAnaPencereyiAcmaz()
    {
        var file = SampleFile();
        var startup = Program.StartupFor(new[] { ShellIntegration.ShrinkFlag, "100", file });
        Assert.NotNull(startup);

        var ad = AppHost.Run(() =>
        {
            var window = new VidShrink.App.App(startup!, null).StartupWindow();
            try { return window.GetType().Name; }
            finally { window.Close(); }
        });

        _output.WriteLine($"acilan pencere turu: {ad}");
        Assert.Equal(nameof(ShrinkJobWindow), ad);
        Assert.NotEqual(nameof(MainWindow), ad);
    }

    /// <summary>
    /// K12: sahibe teslim edilen istek bu surecte yeniden kodlanmaz. Ikinci teslim
    /// basarisiz olunca geriye <b>yalniz o istek</b> kalir; eski tek bayrakli kolda ucu de
    /// geri geliyor ve ilk dosya iki kez kodlaniyordu.
    /// </summary>
    [Fact]
    public void TeslimEdilenIstekGeriDonmez()
    {
        var istekler = new[]
        {
            new ShrinkRequest(100, "a.mp4"),
            new ShrinkRequest(100, "b.mp4"),
            new ShrinkRequest(100, "c.mp4")
        };
        var startup = new ShellShrinkStartup(istekler, null, null);

        var denenen = new List<string>();
        var kalan = Program.Handoff(startup, request =>
        {
            denenen.Add(request.Path);
            return request.Path != "b.mp4";
        });

        _output.WriteLine($"denenen: {string.Join(", ", denenen)}");
        _output.WriteLine($"kalan: {(kalan is null ? "<yok>" : string.Join(", ", kalan.Items.Select(r => r.Path)))}");

        Assert.Equal(3, denenen.Count);
        Assert.NotNull(kalan);
        Assert.Equal(new[] { "b.mp4" }, kalan!.Items.Select(r => r.Path).ToArray());

        Assert.Null(Program.Handoff(startup, _ => true));

        var gerekce = new ShellShrinkStartup(null, ShrinkArgumentProblem.NoPath, null);
        Assert.Same(gerekce, Program.Handoff(gerekce, _ => true));
    }

    /// <summary>
    /// K13: bulunamayan yol istek uretmez ama sessizce de dusmez — argv taramasinin ikinci
    /// kovasinda adiyla durur.
    /// </summary>
    [Fact]
    public void BulunamayanYolSessizceDusmez()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vidshrink-t171-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var a = Path.Combine(dir, "a.mp4");
        var b = Path.Combine(dir, "b.mp4");
        var yok = Path.Combine(dir, "yok.mp4");
        File.WriteAllBytes(a, new byte[16]);
        File.WriteAllBytes(b, new byte[16]);

        var startup = Program.StartupFor(new[] { ShellIntegration.ShrinkFlag, "100", a, yok, b });

        Assert.NotNull(startup);
        Assert.Equal(new[] { a, b }, startup!.Items.Select(r => r.Path).ToArray());
        Assert.Equal(new[] { yok }, startup.Missing.ToArray());
        _output.WriteLine($"istek: {startup.Items.Count}  bulunamayan: {startup.Missing.Count}");
        foreach (var eksik in startup.Missing) _output.WriteLine($"  eksik: {eksik}");
    }

    /// <summary>K13: bulunamayan yolun cumlesi gercekten pencereye yaziliyor mu.</summary>
    [Fact]
    public void BulunamayanYolPencereyeYazilir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vidshrink-t171-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var yok = Path.Combine(dir, "yok.mp4");
        var startup = new ShellShrinkStartup(null, ShrinkArgumentProblem.NoPath, null)
        {
            Missing = new[] { yok }
        };

        var okunan = AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(startup, null);
            try
            {
                window.Begin();
                return (window.NoticeText, window.Language);
            }
            finally { window.Close(); }
        });

        var beklenen = LanguageCatalog.Title(
            Strings.GetIn(okunan.Language, "main.shrink-job.missing-paths", 1, yok),
            ShrinkJobWindow.Turkish(okunan.Language));

        _output.WriteLine($"dil: {okunan.Language}");
        _output.WriteLine($"  {okunan.NoticeText}");
        Assert.Equal(beklenen, okunan.NoticeText);
        Assert.Contains(Path.GetFileName(yok), okunan.NoticeText, StringComparison.Ordinal);
    }

    [Fact]
    public void BulunamayanYolCumlesiIkiDilde()
    {
        const string key = "main.shrink-job.missing-paths";
        var en = Strings.GetIn("en", key, 1, "yok.mp4");
        var tr = Strings.GetIn("tr", key, 1, "yok.mp4");

        Assert.False(string.IsNullOrWhiteSpace(en));
        Assert.False(string.IsNullOrWhiteSpace(tr));
        Assert.NotEqual(en, tr);
        Assert.DoesNotContain(key, en, StringComparison.Ordinal);
        Assert.DoesNotContain(key, tr, StringComparison.Ordinal);
        _output.WriteLine($"en: {en}");
        _output.WriteLine($"tr: {tr}");
    }

    /// <summary>
    /// K14 / borc 5: en-uzun-eslesme sezgiselinin bedeli. Tirnagi kaybolmus bosluklu yolu
    /// geri toplayabilmek icin parcalar once en uzun birlesimden denenir; ayni dizinde
    /// <c>a.mp4</c>, <c>b.mp4</c> ve adi <c>"a.mp4 b.mp4"</c> olan ucuncu bir dosya varsa
    /// goreli adlarla gelen iki istek tek yanlis isteğe cokuyor. Bu olcu bedeli <b>yaziyor</b>:
    /// kayit defteri sablonu her zaman mutlak <c>%1</c> verdigi icin uretimde erisilemez —
    /// birlestirilen aday o zaman <c>C:\Klip\a.mp4 C:\Klip\b.mp4</c> olur ve Windows'ta
    /// boyle bir dosya adi kurulamaz (icinde <c>:</c> ve ayirac var). Davranis degisirse
    /// burasi kirmizi olur.
    /// </summary>
    [Fact]
    public void SezgiselGoreliAdlardaYanlisDosyayiSecebilir()
    {
        var dosyalar = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a.mp4", "b.mp4", "a.mp4 b.mp4" };
        var goreli = ShellShrinkStartup.ScanPaths(new[] { ShellIntegration.ShrinkFlag, "100", "a.mp4", "b.mp4" }, 2, dosyalar.Contains);

        _output.WriteLine($"goreli adlar -> bulunan: {string.Join(" | ", goreli.Found)}  eksik: {goreli.Missing.Count}");
        Assert.Equal(new[] { "a.mp4 b.mp4" }, goreli.Found.ToArray());

        var kok = OperatingSystem.IsWindows() ? @"C:\Klip\" : "/klip/";
        var mutlak = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { kok + "a.mp4", kok + "b.mp4" };
        var tam = ShellShrinkStartup.ScanPaths(
            new[] { ShellIntegration.ShrinkFlag, "100", kok + "a.mp4", kok + "b.mp4" }, 2, mutlak.Contains);

        _output.WriteLine($"mutlak yollar -> bulunan: {string.Join(" | ", tam.Found)}");
        Assert.Equal(new[] { kok + "a.mp4", kok + "b.mp4" }, tam.Found.ToArray());
    }

    [Fact]
    public void HizliListeSozlesmedekiBesDeger()
    {
        Assert.Equal(new[] { 100, 250, 500, 1024, 2048 }, ShellIntegration.QuickShrinkTargetsMegabytes.ToArray());
        _output.WriteLine($"hizli liste: {ShrinkProblemText.QuickList()}");
    }
}
