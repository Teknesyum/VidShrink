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

    private static string PencereMetni(ShrinkArgumentProblem problem) => AppHost.Run(() =>
    {
        var window = new ShrinkJobWindow(new ShellShrinkStartup(null, problem, null), null);
        try
        {
            window.Begin();
            return window.State == ShrinkJobState.Gerekce
                ? window.MessageText
                : $"<durum:{window.State}>";
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
        var beklenen = LanguageCatalog.Display(Strings.Get(anahtar, ShrinkProblemText.QuickList()));

        Assert.Equal(anahtar, ShrinkProblemText.Key(problem));
        Assert.False(string.IsNullOrWhiteSpace(beklenen), $"{anahtar} karsiligi bos.");

        var gorulen = PencereMetni(problem);
        Assert.Equal(beklenen, gorulen);
        _output.WriteLine($"{problem} -> {anahtar}");
        _output.WriteLine($"  {gorulen}");
    }

    [Fact]
    public void BesGerekceBesAyriCumleBasar()
    {
        var cumleler = ShrinkProblemText.All.Select(PencereMetni).ToArray();
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

    [Fact]
    public void HizliListeSozlesmedekiBesDeger()
    {
        Assert.Equal(new[] { 100, 250, 500, 1024, 2048 }, ShellIntegration.QuickShrinkTargetsMegabytes.ToArray());
        _output.WriteLine($"hizli liste: {ShrinkProblemText.QuickList()}");
    }
}
