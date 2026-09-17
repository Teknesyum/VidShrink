using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class KullaniciOnAyarTests
{
    private const string SettingsVariable = "VIDSHRINK_SETTINGS_PATH";

    private static string Sandbox()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "onayar", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static PresetFileException ParseFails(string json) =>
        Assert.Throws<PresetFileException>(() => PresetLibrary.Parse(json));

    private static readonly PresetProfile Sample = new()
    {
        Id = "benim-discord",
        Name = "Benim Discord",
        TargetMb = 9.5,
        Codec = CodecPreference.Auto,
        MaxShortEdge = 720,
        AudioKbps = 96,
        Container = OutputContainer.WebM
    };

    [Fact]
    public void KayitVarsayilanAyarKlasorunuIzler()
    {
        var folder = Sandbox();
        var settings = Path.Combine(folder, "settings.json");
        var previous = Environment.GetEnvironmentVariable(SettingsVariable);
        try
        {
            Environment.SetEnvironmentVariable(SettingsVariable, settings);
            Assert.Equal(Path.Combine(folder, PresetLibrary.UserFileName), PresetLibrary.DefaultUserPath);

            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VidShrink");
            Assert.False(PresetLibrary.DefaultUserPath.StartsWith(appData, StringComparison.OrdinalIgnoreCase));

            PresetLibrary.SaveUser(Sample);
            Assert.True(File.Exists(Path.Combine(folder, PresetLibrary.UserFileName)));
            Assert.Equal(Sample, Assert.Single(PresetLibrary.LoadUser()));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SettingsVariable, previous);
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void KaydetYukleGidisDonusVeAyniIdDegistirir()
    {
        var folder = Sandbox();
        var path = Path.Combine(folder, "alt", "presets.json");
        try
        {
            Assert.Empty(PresetLibrary.LoadUser(path));

            PresetLibrary.SaveUser(Sample, path);
            PresetLibrary.SaveUser(Sample with { Id = "ikinci", TargetMb = 40, MaxShortEdge = null }, path);
            var loaded = PresetLibrary.LoadUser(path);
            Assert.Equal(2, loaded.Count);
            Assert.Equal(Sample, loaded[0]);

            var replaced = PresetLibrary.SaveUser(Sample with { Id = "BENIM-discord", TargetMb = 12 }, path);
            Assert.Equal(2, replaced.Count);
            var reloaded = PresetLibrary.LoadUser(path);
            Assert.Equal(new double?[] { 40, 12 }, reloaded.Select(p => p.TargetMb));

            Assert.Throws<PresetFileException>(() => PresetLibrary.SaveUser(Sample with { TargetMb = 0 }, path));
            Assert.Equal(2, PresetLibrary.LoadUser(path).Count);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void DisaIceAktarmaGidisDonusVeBilinmeyenAlanYokSayilir()
    {
        var folder = Sandbox();
        var path = Path.Combine(folder, "disa.json");
        try
        {
            PresetLibrary.Export(new[] { Sample }, path);
            var text = File.ReadAllText(path);
            Assert.Contains("\"schema\": 1", text);
            Assert.Contains("\"kind\": \"vidshrink-presets\"", text);
            Assert.Equal(Sample, Assert.Single(PresetLibrary.Import(path)));

            var future = text.Replace("\"schema\": 1", "\"schema\": 1, \"yeniAlan\": {\"x\": [1,2]}")
                             .Replace("\"id\": \"benim-discord\"", "\"id\": \"benim-discord\", \"gelecekAlan\": true");
            File.WriteAllText(path, future);
            Assert.Equal(Sample, Assert.Single(PresetLibrary.Import(path)));

            var withChip = Sample with { Kind = PresetKind.Platform, Chip = "Chip8" };
            PresetLibrary.Export(new[] { withChip }, path);
            var imported = Assert.Single(PresetLibrary.Import(path));
            Assert.Equal(PresetKind.User, imported.Kind);
            Assert.Null(imported.Chip);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void BozukDosyaAnlasilirHataVerir()
    {
        var valid = PresetLibrary.Serialize(new[] { Sample });
        Assert.Single(PresetLibrary.Parse(valid));

        var notJson = ParseFails("{ \"schema\": 1, \"presets\": [ }");
        Assert.Equal(PresetFileError.NotJson, notJson.Error);
        Assert.Equal("main.preset.error.not-json", notJson.LocaleKey);

        Assert.Equal(PresetFileError.NotJson, ParseFails("{\"schema\":1,\"schema\":1,\"presets\":[]}").Error);
        Assert.Equal(PresetFileError.NotPresetFile, ParseFails("{\"schema\":1,\"items\":[]}").Error);
        Assert.Equal(PresetFileError.NotPresetFile, ParseFails("{\"schema\":1,\"kind\":\"baska\",\"presets\":[]}").Error);

        Assert.Equal(PresetFileError.SchemaMissing, ParseFails("{\"presets\":[]}").Error);
        Assert.Equal(PresetFileError.SchemaMissing, ParseFails("{\"schema\":\"bir\",\"presets\":[]}").Error);

        var tooNew = ParseFails(valid.Replace("\"schema\": 1", "\"schema\": 2"));
        Assert.Equal(PresetFileError.SchemaTooNew, tooNew.Error);
        Assert.Equal("2", tooNew.Detail);

        var badEnum = ParseFails(valid.Replace("\"codec\": \"Auto\"", "\"codec\": \"Uydurma\""));
        Assert.Equal(PresetFileError.InvalidValue, badEnum.Error);
        Assert.StartsWith("presets[0]", badEnum.Detail);

        var negative = ParseFails(valid.Replace("\"targetMb\": 9.5", "\"targetMb\": -1"));
        Assert.Equal(PresetFileError.InvalidValue, negative.Error);
        Assert.Equal("presets[0].targetMb", negative.Detail);

        var oddEdge = ParseFails(valid.Replace("\"maxShortEdge\": 720", "\"maxShortEdge\": 721"));
        Assert.Equal("presets[0].maxShortEdge", oddEdge.Detail);

        var noId = ParseFails(valid.Replace("\"id\": \"benim-discord\"", "\"id\": \"\""));
        Assert.Equal("presets[0].id", noId.Detail);

        var unsourced = ParseFails(PresetLibrary.Serialize(new[] { Sample with { Source = new PresetSource { Status = PresetSourceStatus.Official } } }));
        Assert.Equal("presets[0].source.url", unsourced.Detail);
        Assert.Single(PresetLibrary.Parse(PresetLibrary.Serialize(new[] { Sample with { Source = new PresetSource { Status = PresetSourceStatus.Official, Url = "https://example.org/limit" } } })));

        var handBrake = File.ReadAllText(HandBrakeOnAyarCeviriTests.FixturePath);
        Assert.Equal(PresetFileError.HandBrakeFile, ParseFails(handBrake).Error);
    }

    [Fact]
    public void YerlesikIdIceAlinamaz()
    {
        var folder = Sandbox();
        var path = Path.Combine(folder, "ayrilmis.json");
        try
        {
            PresetLibrary.Export(new[] { Sample with { Id = "Discord-Free" } }, path);
            var error = Assert.Throws<PresetFileException>(() => PresetLibrary.Import(path));
            Assert.Equal(PresetFileError.ReservedId, error.Error);
            Assert.Equal("main.preset.error.reserved-id", error.LocaleKey);

            Assert.Equal(PresetFileError.ReservedId,
                Assert.Throws<PresetFileException>(() => PresetLibrary.SaveUser(Sample with { Id = "telegram" }, path)).Error);

            PresetLibrary.Export(new[] { Sample with { Id = "discord-free-2" } }, path);
            Assert.Single(PresetLibrary.Import(path));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void HataVeNotAnahtarlari42Dilde()
    {
        var keys = Enum.GetValues<PresetFileError>().Select(PresetLibrary.ErrorKey)
            .Concat(new[]
            {
                new PresetFieldNote("f", PresetNoteOutcome.Carried, PresetNoteReason.None, null),
                new PresetFieldNote("f", PresetNoteOutcome.Approximated, PresetNoteReason.None, null),
                new PresetFieldNote("f", PresetNoteOutcome.Approximated, PresetNoteReason.SizeFromName, null),
                new PresetFieldNote("f", PresetNoteOutcome.Dropped, PresetNoteReason.EngineDecides, null),
                new PresetFieldNote("f", PresetNoteOutcome.Dropped, PresetNoteReason.NoEquivalent, null),
                new PresetFieldNote("f", PresetNoteOutcome.Dropped, PresetNoteReason.UnsupportedCodec, null),
                new PresetFieldNote("f", PresetNoteOutcome.Dropped, PresetNoteReason.Structural, null)
            }.Select(note => note.LocaleKey))
            .ToList();

        Assert.Equal(15, keys.Distinct().Count());
        Assert.Contains("main.preset.error.hand-brake-file", keys);
        Assert.Contains("main.preset.handbrake.dropped.engine-decides", keys);

        var languages = Locales.Languages;
        Assert.Equal(42, languages.Count);
        var english = Locales.Values("en");
        var turkish = Locales.Values("tr");
        foreach (var language in languages)
        {
            var values = Locales.Values(language);
            foreach (var key in keys)
            {
                Assert.True(values.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text), $"{language}: {key} yok");
                if (english[key].Contains("{0}"))
                    Assert.True(text!.Contains("{0}"), $"{language}: {key} yer tutucusu yok");
            }
        }

        foreach (var key in keys)
            Assert.NotEqual(english[key], turkish[key]);
    }
}
