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

    [Theory]
    [InlineData(ShrinkArgumentProblem.NoTarget)]
    [InlineData(ShrinkArgumentProblem.TargetNotANumber)]
    [InlineData(ShrinkArgumentProblem.TargetNotPositive)]
    [InlineData(ShrinkArgumentProblem.TargetNotInQuickList)]
    [InlineData(ShrinkArgumentProblem.NoPath)]
    public void GerekcePencereyeYazilir(ShrinkArgumentProblem problem)
    {
        var text = AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(new ShellShrinkStartup(null, problem, null), null);
            try
            {
                window.Begin();
                return (window.State, window.MessageText);
            }
            finally { window.Close(); }
        });

        Assert.Equal(ShrinkJobState.Gerekce, text.State);
        Assert.False(string.IsNullOrWhiteSpace(text.MessageText), $"{problem} icin ekranda cumle yok.");
        _output.WriteLine($"{problem} -> {text.MessageText}");
    }

    [Fact]
    public void HizliListeSozlesmedekiBesDeger()
    {
        Assert.Equal(new[] { 100, 250, 500, 1024, 2048 }, ShellIntegration.QuickShrinkTargetsMegabytes.ToArray());
        _output.WriteLine($"hizli liste: {ShrinkProblemText.QuickList()}");
    }
}
