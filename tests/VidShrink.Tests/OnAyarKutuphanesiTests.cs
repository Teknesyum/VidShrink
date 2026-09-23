using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class OnAyarKutuphanesiTests
{
    private static readonly string SourcesDoc =
        Path.Combine(TipSources.Root, "docs", "olcumler", "onayar-kaynaklari.md");

    private static PresetProfile Get(string id) =>
        PresetLibrary.BuiltIn.Find(id) ?? throw new Xunit.Sdk.XunitException($"{id} tabloda yok");

    [Fact]
    public void YongaPlanlariTablodanOkunur()
    {
        var expected = new[]
        {
            new MainWindow.ChipPlan("ChipArchive", null, false, Intent.Archive, CodecPreference.MaxCompression, FillPolicy.QualityCeiling),
            new MainWindow.ChipPlan("Chip8", 8, true, Intent.SocialMedia, CodecPreference.Compatible, FillPolicy.FillTarget),
            new MainWindow.ChipPlan("ChipWhatsApp", 16, true, Intent.Sharing, CodecPreference.Compatible, FillPolicy.FillTarget),
            new MainWindow.ChipPlan("Chip25", 25, true, Intent.Sharing, CodecPreference.Compatible, FillPolicy.FillTarget),
            new MainWindow.ChipPlan("Chip100", 100, true, Intent.Sharing, CodecPreference.Auto, FillPolicy.FillTarget),
            new MainWindow.ChipPlan("Chip134", 134, true, Intent.Sharing, CodecPreference.Auto, FillPolicy.FillTarget),
            new MainWindow.ChipPlan("Chip180", 180, true, Intent.Sharing, CodecPreference.Auto, FillPolicy.FillTarget),
            new MainWindow.ChipPlan("ChipHalf", null, true, Intent.Sharing, CodecPreference.Auto, FillPolicy.QualityCeiling),
        };

        Assert.Equal(expected, MainWindow.ChipPlans());

        var code = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.axaml.cs"));
        Assert.DoesNotMatch(new Regex("new(\\s+ChipPlan)?\\(\"Chip"), code);
    }

    [Fact]
    public void YongaDegerleriDegismedi()
    {
        Assert.Null(Get("archive").TargetMb);
        Assert.False(Get("archive").SizeCapped);
        Assert.Equal(16, Get("whatsapp-chat").TargetMb);
        Assert.Equal("ChipWhatsApp", Get("whatsapp-chat").Chip);
        Assert.Equal(134, Get("share-uguu").TargetMb);
        Assert.Equal(180, Get("whatsapp-web").TargetMb);
        Assert.Equal(FillPolicy.QualityCeiling, Get("half").Fill);
    }

    [Fact]
    public void PlatformTavanlariResmiKaynaktan()
    {
        Assert.Equal(20, Get("discord-free").TargetMb);
        Assert.Equal(50, Get("discord-nitro-basic").TargetMb);
        Assert.Equal(500, Get("discord-nitro").TargetMb);
        Assert.Equal(2000, Get("telegram").TargetMb);
        Assert.Equal(25, Get("email-gmail").TargetMb);
        Assert.Equal(20, Get("email-outlook").TargetMb);

        foreach (var platform in new[] { "discord-free", "telegram", "email-gmail", "email-outlook" })
        {
            var profile = Get(platform);
            Assert.Equal(PresetKind.Platform, profile.Kind);
            Assert.Equal(PresetSourceStatus.Official, profile.Source.Status);
            Assert.StartsWith("https://", profile.Source.Url);
            Assert.Equal(CodecPreference.Compatible, profile.Codec);
        }

        Assert.Equal(PresetSourceStatus.Code, Get("whatsapp-chat").Source.Status);
    }

    [Fact]
    public void ResmiKaynakliHerDegerinAdresiVeTarihiVar()
    {
        foreach (var profile in PresetLibrary.BuiltIn.Profiles)
        {
            if (profile.Source.Status == PresetSourceStatus.Official)
            {
                Assert.False(string.IsNullOrWhiteSpace(profile.Source.Url), profile.Id);
                Assert.Equal("2026-09-17", profile.Source.Checked);
            }
            else
            {
                Assert.Null(profile.Source.Checked);
            }
        }

        Assert.Equal(PresetLibrary.BuiltIn.Profiles.Count,
            PresetLibrary.BuiltIn.Profiles.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void BelgeTablosuVeriyleAyni()
    {
        var rows = File.ReadAllLines(SourcesDoc)
            .Select(line => line.Split('|').Select(cell => cell.Trim()).ToArray())
            .Where(cells => cells.Length >= 6 && Regex.IsMatch(cells[1], "^[a-z0-9]+(-[a-z0-9]+)*$") && cells[1] != "id")
            .ToDictionary(cells => cells[1], cells => cells);

        Assert.Equal(PresetLibrary.BuiltIn.Profiles.Select(p => p.Id).OrderBy(id => id),
                     rows.Keys.OrderBy(id => id));

        foreach (var profile in PresetLibrary.BuiltIn.Profiles)
        {
            var row = rows[profile.Id];
            var status = profile.Source.Status == PresetSourceStatus.Official ? "resmi" : "kodda";
            Assert.True(row[3] == status, $"{profile.Id}: belge {row[3]}, veri {status}");

            if (profile.TargetMb is { } mb)
                Assert.True(row[2] == mb.ToString(CultureInfo.InvariantCulture) + " MB", $"{profile.Id}: belge {row[2]}, veri {mb} MB");
            if (profile.MaxShortEdge is { } edge)
                Assert.True(row[2] == edge.ToString(CultureInfo.InvariantCulture) + " kısa kenar", $"{profile.Id}: belge {row[2]}");
            if (profile.Source.Url is { } url && profile.Source.Status == PresetSourceStatus.Official)
                Assert.True(row[4] == url, $"{profile.Id}: belge adresi {row[4]}");
        }
    }

    [Fact]
    public void CihazProfiliCozunurlukTavaniTasir()
    {
        var devices = PresetLibrary.BuiltIn.OfKind(PresetKind.Device).ToList();
        Assert.Equal(4, devices.Count);

        var chromecast = Get("device-chromecast-gen3").ToPlanOptions();
        Assert.Equal(1080, chromecast.FixedResolution);
        Assert.Equal(CodecPreference.Compatible, chromecast.Codec);
        Assert.Equal(FillPolicy.QualityCeiling, chromecast.FillPolicy);
        Assert.Equal(720, Get("device-nest-hub").ToPlanOptions().FixedResolution);

        var discord = Get("discord-free").ToPlanOptions();
        Assert.Null(discord.FixedResolution);
        Assert.Equal(20, discord.TargetMb);
        Assert.Equal(FillPolicy.FillTarget, discord.FillPolicy);
    }

    [Fact]
    public void GomuluKaynakDerlemeyeGirer()
    {
        var names = typeof(PresetLibrary).Assembly.GetManifestResourceNames();
        Assert.Contains("VidShrink.Core.Presets.platformlar.json", names);
        Assert.Equal(18, PresetLibrary.BuiltIn.Profiles.Count);
    }
}
