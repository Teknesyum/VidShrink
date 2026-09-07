using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using VidShrink.App.Integration;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// T188: "Birlikte aç" kaydının içeriği, öneri şeridinin kararı ve reddin kalıcılığı.
/// Kayıt defterine yazan tarafın planı ayrı durduğu için ölçü hiçbir anahtara dokunmaz.
/// </summary>
public sealed class KabukEntegrasyonTests
{
    private const string Executable = @"C:\Programs\VidShrink\VidShrink.exe";

    private static IReadOnlyList<(string Key, string Name, string? Value)> Plan()
        => FileAssociation.Plan(Executable);

    [Fact]
    public void PlanYalnizKullaniciKapsamindaYaziyor()
    {
        Assert.NotEmpty(Plan());
        Assert.All(Plan(), entry =>
            Assert.StartsWith(@"Software\Classes", entry.Key, StringComparison.Ordinal));
    }

    [Fact]
    public void ProgIdKomutuSecilenDosyayiUygulamayaVeriyor()
    {
        var command = Plan().Single(entry =>
            entry.Key == $@"Software\Classes\{FileAssociation.ProgId}\shell\open\command" && entry.Name.Length == 0);

        Assert.Equal($"\"{Executable}\" \"%1\"", command.Value);
    }

    [Fact]
    public void SimgeUygulamaninKendiDosyasindanGeliyor()
    {
        var icon = Plan().Single(entry =>
            entry.Key == $@"Software\Classes\{FileAssociation.ProgId}\DefaultIcon");

        Assert.Equal($"{Executable},0", icon.Value);
    }

    [Fact]
    public void HerMedyaUzantisiOpenWithProgidsListesineGiriyor()
    {
        var listed = Plan()
            .Where(entry => entry.Key.EndsWith(@"\OpenWithProgids", StringComparison.Ordinal))
            .Select(entry => entry.Key.Split(@"\", StringSplitOptions.None)[2].TrimStart(@".".ToCharArray()))
            .ToArray();

        Assert.Equal(ShellIntegration.MediaExtensions.OrderBy(name => name, StringComparer.Ordinal),
            listed.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("mp4")]
    [InlineData("mkv")]
    [InlineData("mov")]
    [InlineData("avi")]
    [InlineData("webm")]
    public void SozlesmedeAdiGecenUzantiKayitta(string extension)
    {
        var entry = Plan().Single(row => row.Key == $@"Software\Classes\.{extension}\OpenWithProgids");

        Assert.Equal(FileAssociation.ProgId, entry.Name);
        Assert.Null(entry.Value);
    }

    [Fact]
    public void OpenWithProgidsSatiriBosDegerTasiyor()
    {
        Assert.All(
            Plan().Where(entry => entry.Key.EndsWith(@"\OpenWithProgids", StringComparison.Ordinal)),
            entry => Assert.Null(entry.Value));
    }

    [Fact]
    public void PlanHicbirYerdeVarsayilanSecimineYazmiyor()
    {
        Assert.All(Plan(), entry =>
        {
            Assert.DoesNotContain("FileExts", entry.Key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Explorer", entry.Key, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void OneriYalnizWindowsUzerindeVeVarsayilanDegilkenCikiyor()
    {
        Assert.True(DefaultAppSuggestion.ShouldShow(onWindows: true, alreadyDefault: false, dismissed: false));
        Assert.False(DefaultAppSuggestion.ShouldShow(onWindows: false, alreadyDefault: false, dismissed: false));
        Assert.False(DefaultAppSuggestion.ShouldShow(onWindows: true, alreadyDefault: true, dismissed: false));
        Assert.False(DefaultAppSuggestion.ShouldShow(onWindows: true, alreadyDefault: false, dismissed: true));
    }

    [Fact]
    public void RetKaliciVeDosyadakiDigerAnahtarlarDuruyor()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "t188", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "settings.json");
        File.WriteAllText(file, @"{ ""advCrf"": 23, ""outputFolder"": ""D:/cikti"" }");

        Assert.False(DefaultAppSuggestion.Dismissed(file));

        DefaultAppSuggestion.Dismiss(file);

        Assert.True(DefaultAppSuggestion.Dismissed(file));

        using var document = JsonDocument.Parse(File.ReadAllText(file));
        Assert.Equal(23, document.RootElement.GetProperty("advCrf").GetInt32());
        Assert.Equal("D:/cikti", document.RootElement.GetProperty("outputFolder").GetString());

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void OkunamayanDosyaReddedilmemisSayilir()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "t188", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "settings.json");
        File.WriteAllText(file, "{ bu json degil");

        Assert.False(DefaultAppSuggestion.Dismissed(file));
        Assert.False(DefaultAppSuggestion.Dismissed(Path.Combine(folder, "hic-yok.json")));

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void KaynaktaVarsayilanSecimAnahtarinaYazan_Satir_Yok()
    {
        var source = Path.Combine(TipSources.Root, "src");
        var guilty = Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("UserChoice", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(guilty);
    }

    [Fact]
    public void OneriMetinleriIkiDildeDeVar()
    {
        foreach (var language in new[] { "tr", "en" })
        {
            var file = Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales", language, "settings.json");
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var key in new[]
                     {
                         "settings.default-app.suggestion",
                         "settings.default-app.open",
                         "settings.default-app.dismiss"
                     })
            {
                Assert.True(document.RootElement.TryGetProperty(key, out var value), $"{language}: {key}");
                Assert.False(string.IsNullOrWhiteSpace(value.GetString()), $"{language}: {key}");
            }
        }
    }

    [Fact]
    public void KayitAyniYolIcinIkinciKezYazilmiyor()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "t188", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "settings.json");

        Assert.Null(FileAssociationSetup.Recorded(file));
        Assert.True(FileAssociationSetup.Needed(null, Executable));

        FileAssociationSetup.Record(Executable, file);

        Assert.Equal(Executable, FileAssociationSetup.Recorded(file));
        Assert.False(FileAssociationSetup.Needed(FileAssociationSetup.Recorded(file), Executable));
        Assert.True(FileAssociationSetup.Needed(FileAssociationSetup.Recorded(file), @"D:\Baska\VidShrink.exe"));

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void KayitNotuDosyadakiDigerAnahtarlariSilmiyor()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "t188", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "settings.json");
        File.WriteAllText(file, @"{ ""advCrf"": 23 }");

        FileAssociationSetup.Record(Executable, file);
        DefaultAppSuggestion.Dismiss(file);

        using var document = JsonDocument.Parse(File.ReadAllText(file));
        Assert.Equal(23, document.RootElement.GetProperty("advCrf").GetInt32());
        Assert.Equal(Executable, document.RootElement.GetProperty(FileAssociationSetup.RegisteredKey).GetString());
        Assert.True(document.RootElement.GetProperty("defaultAppSuggestionDismissed").GetBoolean());

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void BosYolIcinKayitIstenmiyor()
    {
        Assert.False(FileAssociationSetup.Needed(null, ""));
    }

    [Fact]
    public void SeritVeKayitUretimYolundanCagriliyor()
    {
        var app = Path.Combine(TipSources.Root, "src", "VidShrink.App");
        var window = File.ReadAllText(Path.Combine(app, "MainWindow.axaml.cs"));
        var entry = File.ReadAllText(Path.Combine(app, "App.axaml.cs"));

        Assert.Contains("new Integration.DefaultAppSuggestionBar()", window, StringComparison.Ordinal);
        Assert.Contains("FileAssociationSetup.Ensure", entry, StringComparison.Ordinal);
    }

    [Fact]
    public void SeritKapatilinca_Kayboluyor_Ve_Ret_Kalici_Kaliyor()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "t188", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "settings.json");

        AppHost.Run(() =>
        {
            var bar = new DefaultAppSuggestionBar(file);
            var buttons = bar.GetLogicalDescendants().OfType<Button>().ToList();

            Assert.Equal(2, buttons.Count);
            Assert.True(bar.IsVisible);

            buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.False(bar.IsVisible);
        });

        Assert.True(DefaultAppSuggestion.Dismissed(file));
        Assert.False(DefaultAppSuggestion.ShouldShow(onWindows: true, alreadyDefault: false,
            dismissed: DefaultAppSuggestion.Dismissed(file)));

        Directory.Delete(folder, recursive: true);
    }
}
