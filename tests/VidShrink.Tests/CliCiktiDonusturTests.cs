using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// DAG-P2: <c>--json</c> ile NDJSON ilerleme, <c>donustur</c>, <c>profiller --disa-aktar</c> ve
/// <c>tamamlama</c>. <c>donustur</c>'un argümanı Dönüştür panelinin komut kutusuyla aynı olmalı;
/// panel başsız açılır, aynı seçim <see cref="MainWindow.ApplyConvertPreset"/> ile verilir.
/// Geçici dosyalar <c>.calisma/test-ciktilari/cli-donustur/</c>, test siler.
/// </summary>
public sealed class CliCiktiDonusturTests
{
    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);

    private static MediaInfo Ornek() => Kaynak(62, Video, Ses);

    private static CliRequest Ayristir(params string[] args)
    {
        var parsed = CliParser.Parse(args);
        Assert.True(parsed.Request is not null, parsed.ErrorKey + " " + parsed.ErrorArgument);
        return parsed.Request!;
    }

    private static string? Hata(params string[] args) => CliParser.Parse(args).ErrorKey;

    [Fact]
    public void DonusturVarsayilaniPanelinVarsayilani()
    {
        var request = Ayristir("donustur", "a.mkv");

        Assert.Equal(CliCommand.Convert, request.Command);
        Assert.Equal("a.mkv", request.Input);
        var preset = request.Conversion!;
        Assert.Equal("mp4", preset.Container);
        Assert.Equal("libx264", preset.VideoCodec);
        Assert.Equal(ConversionQualityMode.Crf, preset.QualityMode);
        Assert.Equal(new ConversionPreset().Quality, preset.Quality);
        Assert.Equal(ConversionPreset.Source, preset.Resolution);
        Assert.Equal("aac", preset.AudioCodec);
    }

    [Fact]
    public void DonusturSecenekleriOnAyaraGecer()
    {
        var request = Ayristir("convert", "a.mkv", "--container", "webm", "--codec", "vp9", "--bitrate", "2500",
            "--resolution", "1280x720", "--fps", "29.97", "--audio-codec", "opus", "--audio-bitrate", "96",
            "--no-metadata", "--cut", "5-20", "--json");

        var preset = request.Conversion!;
        Assert.Equal("webm", preset.Container);
        Assert.Equal("libvpx-vp9", preset.VideoCodec);
        Assert.Equal(ConversionQualityMode.Bitrate, preset.QualityMode);
        Assert.Equal(2500, preset.Quality);
        Assert.Equal(ConversionPreset.Custom, preset.Resolution);
        Assert.Equal("1280x720", preset.CustomResolution);
        Assert.Equal(ConversionPreset.Custom, preset.Fps);
        Assert.Equal("29.97", preset.CustomFps);
        Assert.Equal("libopus", preset.AudioCodec);
        Assert.Equal(96, preset.AudioBitrateK);
        Assert.True(preset.DropMetadata);
        Assert.Equal(5, request.TrimStartSeconds);
        Assert.Equal(20, request.TrimEndSeconds);
        Assert.True(request.Json);
    }

    [Fact]
    public void KapCiktiUzantisindanGelir()
    {
        Assert.Equal("mkv", Ayristir("donustur", "a.mp4", "-o", "b.MKV").Conversion!.Container);
        Assert.Equal("aiff", Ayristir("donustur", "a.mp4", "-o", "b.aif").Conversion!.Container);
        Assert.Equal("mp3", Ayristir("donustur", "a.mp4", "--kap", "mp3", "--cikti", "b.mp3").Conversion!.Container);
    }

    [Theory]
    [InlineData("error.no-input", new[] { "donustur" })]
    [InlineData("error.bad-container", new[] { "donustur", "a.mp4", "--kap", "xyz" })]
    [InlineData("error.convert-codec", new[] { "donustur", "a.mp4", "--kodek", "mpeg1" })]
    [InlineData("error.convert-audio-codec", new[] { "donustur", "a.mp4", "--ses-kodek", "dts" })]
    [InlineData("error.convert-crf", new[] { "donustur", "a.mp4", "--crf", "60" })]
    [InlineData("error.convert-crf", new[] { "donustur", "a.mp4", "--kodek", "vp9", "--crf", "64" })]
    [InlineData("error.bad-crf", new[] { "donustur", "a.mp4", "--crf", "22.5" })]
    [InlineData("error.crf-and-bitrate", new[] { "donustur", "a.mp4", "--crf", "22", "--bit-hizi", "3000" })]
    [InlineData("error.bad-bitrate", new[] { "donustur", "a.mp4", "--bit-hizi", "10" })]
    [InlineData("error.bad-resolution", new[] { "donustur", "a.mp4", "--cozunurluk", "8x8" })]
    [InlineData("error.bad-audio-bitrate", new[] { "donustur", "a.mp4", "--ses-bit-hizi", "4" })]
    [InlineData("error.convert-output-extension", new[] { "donustur", "a.mp4", "-o", "b.txt" })]
    [InlineData("error.convert-output-extension", new[] { "donustur", "a.mp4", "--kap", "mkv", "-o", "b.mp4" })]
    [InlineData("error.unknown-option", new[] { "donustur", "a.mp4", "--hedef", "25MB" })]
    [InlineData("error.extra-input", new[] { "donustur", "a.mp4", "b.mp4" })]
    public void DonusturHatalari(string beklenen, string[] args) => Assert.Equal(beklenen, Hata(args));

    [Fact]
    public void ProfillerDisaAktarAyristirilir()
    {
        var request = Ayristir("profiller", "--disa-aktar", "p.json", "--profil", "a", "--profile", "b");
        Assert.Equal(CliCommand.Profiller, request.Command);
        Assert.Equal("p.json", request.ExportPath);
        Assert.Equal(new[] { "a", "b" }, request.ExportIds);

        Assert.Null(Ayristir("presets").ExportPath);
        Assert.Equal("error.export-needs-file", Hata("profiller", "--profil", "a"));
        Assert.Equal("error.unknown-option", Hata("profiller", "--bilinmez"));
    }

    [Theory]
    [InlineData("bash", "bash")]
    [InlineData("zsh", "zsh")]
    [InlineData("fish", "fish")]
    [InlineData("powershell", "powershell")]
    [InlineData("pwsh", "powershell")]
    [InlineData("PowerShell", "powershell")]
    public void TamamlamaKabuguAyristirilir(string verilen, string beklenen)
    {
        var request = Ayristir("tamamlama", verilen);
        Assert.Equal(CliCommand.Completion, request.Command);
        Assert.Equal(beklenen, request.Shell);
    }

    [Fact]
    public async Task TamamlamaBilinmeyenKabuktaKullanimHatasi()
    {
        var stdout = new StringWriter();
        var exit = await CliApp.RunAsync(new[] { "tamamlama", "cmd" }, stdout, new StringWriter(), CliText.ForLanguage("en"), Unreachable(), CancellationToken.None);
        Assert.Equal(ExitCodes.Usage, exit);
        Assert.Equal("", stdout.ToString());
        Assert.Equal(ExitCodes.Usage, await CliApp.RunAsync(new[] { "tamamlama" }, new StringWriter(), new StringWriter(), CliText.ForLanguage("en"), Unreachable(), CancellationToken.None));
    }

    [Theory]
    [InlineData("bash", "complete -o filenames")]
    [InlineData("zsh", "bashcompinit")]
    [InlineData("fish", "complete -c vidshrink")]
    [InlineData("powershell", "Register-ArgumentCompleter")]
    public async Task TamamlamaBetigiStdoutaGelir(string kabuk, string imza)
    {
        var stdout = new StringWriter();
        var exit = await CliApp.RunAsync(new[] { "tamamlama", kabuk }, stdout, new StringWriter(), CliText.ForLanguage("tr"), Unreachable(), CancellationToken.None);
        var betik = stdout.ToString();

        Assert.Equal(ExitCodes.InBand, exit);
        Assert.Contains(imza, betik, StringComparison.Ordinal);
        foreach (var komut in new[] { "kucult", "izle", "donustur", "profiller", "tamamlama" })
            Assert.Contains(komut, betik, StringComparison.Ordinal);
        foreach (var secenek in new[] { "container", "audio-bitrate", "export", "hedef" })
            Assert.Contains(secenek, betik, StringComparison.Ordinal);
        Assert.Contains("libsvtav1", betik, StringComparison.Ordinal);
    }

    private static IEnumerable<string> KaynaktakiSecenekler(string dosya)
        => Regex.Matches(File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.Cli", dosya)), @"case\s+(""[^""]+""(?:\s+or\s+""[^""]+"")*)")
            .SelectMany(m => Regex.Matches(m.Groups[1].Value, @"""([^""]+)""").Select(s => s.Groups[1].Value))
            .Where(s => s.StartsWith('-'));

    [Fact]
    public void AyristiricininHerSecenegiTamamlamadaVar()
    {
        var tamamlama = CliCompletion.Commands.SelectMany(c => c.Options).ToHashSet(StringComparer.Ordinal);
        var eksik = KaynaktakiSecenekler("CliRequest.cs").Concat(KaynaktakiSecenekler("CliConvert.cs"))
            .Distinct().Where(s => !tamamlama.Contains(s)).ToList();
        Assert.Empty(eksik);

        var donustur = CliCompletion.Commands.Single(c => c.Names.Contains("donustur")).Options.ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(KaynaktakiSecenekler("CliConvert.cs"), s => !donustur.Contains(s));
    }

    [Fact]
    public void TamamlamaninHerSeceneginiAyristiriciTanir()
    {
        var tanimayan = new List<string>();
        foreach (var komut in CliCompletion.Commands)
        foreach (var ad in komut.Names)
        foreach (var secenek in komut.Options)
        {
            var hata = DilliHata(new[] { ad, secenek, "1", "a.mp4" });
            if (hata == "error.unknown-option") tanimayan.Add(ad + " " + secenek);
        }
        Assert.Empty(tanimayan);
    }

    private static string? DilliHata(IReadOnlyList<string> args)
    {
        var dil = CliText.SplitLanguage(args);
        return dil.Invalid is { } bozuk ? "dil:" + bozuk : CliParser.Parse(dil.Rest.ToArray()).ErrorKey;
    }

    [Fact]
    public void TamamlamaDegerleriAyristiriciyaGecer()
    {
        var donustur = CliCompletion.Commands.Single(c => c.Names.Contains("donustur"));
        var gecmeyen = new List<string>();
        foreach (var (secenek, degerler) in donustur.Values)
        foreach (var deger in degerler)
        {
            var args = new List<string> { "donustur", "a.mp4", secenek, deger };
            if (secenek is "--kap" or "--container" && deger is "png" or "jpg") args.AddRange(new[] { "-o", "dizi" });
            if (DilliHata(args) is { } hata) gecmeyen.Add(secenek + " " + deger + " → " + hata);
        }
        Assert.Empty(gecmeyen);
    }

    [Fact]
    public void JsonIlerlemeSatirBasinaBirNesne()
    {
        var stderr = new StringWriter();
        var ilerleme = new CliProgress.Encode(stderr, CliText.ForLanguage("tr"), json: true);
        ilerleme.Report(new EncodeProgress(0.421, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(12.54), 1.2, "pass 1/2"));
        ilerleme.Report(new EncodeProgress(0.425, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(12), 1.2, "pass 1/2"));
        ilerleme.Report(new EncodeProgress(0.43, TimeSpan.FromSeconds(4), null, 1.3, "pass 1/2"));
        CliProgress.Stage(stderr, CliText.ForLanguage("tr"), "progress.probe", json: true);

        var satirlar = stderr.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, satirlar.Length);

        using var ilk = JsonDocument.Parse(satirlar[0]);
        Assert.Equal("progress", ilk.RootElement.GetProperty("type").GetString());
        Assert.Equal(42, ilk.RootElement.GetProperty("percent").GetInt32());
        Assert.Equal(0.421, ilk.RootElement.GetProperty("fraction").GetDouble(), 4);
        Assert.Equal("pass 1/2", ilk.RootElement.GetProperty("stage").GetString());
        Assert.Equal(12.5, ilk.RootElement.GetProperty("remainingSeconds").GetDouble(), 3);

        using var ikinci = JsonDocument.Parse(satirlar[1]);
        Assert.Equal(43, ikinci.RootElement.GetProperty("percent").GetInt32());
        Assert.Equal(JsonValueKind.Null, ikinci.RootElement.GetProperty("remainingSeconds").ValueKind);

        using var asama = JsonDocument.Parse(satirlar[2]);
        Assert.Equal("stage", asama.RootElement.GetProperty("type").GetString());
        Assert.Equal("probe", asama.RootElement.GetProperty("stage").GetString());
    }

    [Fact]
    public void JsonsuzIlerlemeMetinKalir()
    {
        var text = CliText.ForLanguage("en");
        var stderr = new StringWriter();
        new CliProgress.Encode(stderr, text, json: false).Report(new EncodeProgress(0.5, TimeSpan.Zero, null, 0, "pass 1/2"));
        CliProgress.Stage(stderr, text, "progress.probe", json: false);

        Assert.Equal(text.Format("progress.encode", 50, "pass 1/2", "-") + Environment.NewLine + text["progress.probe"] + Environment.NewLine,
            stderr.ToString());
    }

    private static T Klasorde<T>(Func<string, T> govde)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "cli-donustur", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try { return govde(klasor); }
        finally { Directory.Delete(klasor, recursive: true); }
    }

    private static async Task<T> KlasordeAsync<T>(Func<string, Task<T>> govde)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "cli-donustur", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try { return await govde(klasor); }
        finally { Directory.Delete(klasor, recursive: true); }
    }

    private static readonly PresetProfile[] Kullanici =
    {
        new() { Id = "ekip-25", Name = "Ekip 25", TargetMb = 25 },
        new() { Id = "arsiv", Name = "Arşiv", TargetMb = 400, AudioKbps = 192 }
    };

    [Fact]
    public async Task DisaAktarilanDosyaIceAktarilir()
    {
        var (exit, okunan, cikti) = await KlasordeAsync(async klasor =>
        {
            var dosya = Path.Combine(klasor, "alt", "profiller.json");
            var stdout = new StringWriter();
            var e = await CliApp.RunAsync(new[] { "profiller", "--disa-aktar", dosya }, stdout, new StringWriter(), CliText.ForLanguage("en"),
                new CliServices { UserPresets = () => Kullanici }, CancellationToken.None);
            return (e, File.Exists(dosya) ? PresetLibrary.Import(dosya) : Array.Empty<PresetProfile>(), stdout.ToString());
        });

        Assert.Equal(ExitCodes.InBand, exit);
        Assert.Equal(new[] { "ekip-25", "arsiv" }, okunan.Select(p => p.Id));
        Assert.Equal(400, okunan[1].TargetMb);
        Assert.Equal(192, okunan[1].AudioKbps);
        Assert.Contains("profiller.json", cikti, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisaAktarmaSecilenProfiliYazar()
    {
        var okunan = await KlasordeAsync(async klasor =>
        {
            var dosya = Path.Combine(klasor, "p.json");
            await CliApp.RunAsync(new[] { "profiller", "--disa-aktar", dosya, "--profil", "Arşiv" }, new StringWriter(), new StringWriter(),
                CliText.ForLanguage("en"), new CliServices { UserPresets = () => Kullanici }, CancellationToken.None);
            return PresetLibrary.Import(dosya);
        });

        Assert.Equal("arsiv", Assert.Single(okunan).Id);
    }

    [Fact]
    public async Task DisaAktarmaYerlesikVeBosRedleri()
    {
        var yerlesik = PresetLibrary.BuiltIn.Profiles[0].Id;
        var yerlesikHata = new StringWriter();
        var bilinmezHata = new StringWriter();
        var (yerlesikCikis, bosCikis, yazildi, bilinmez) = await KlasordeAsync(async klasor =>
        {
            var dosya = Path.Combine(klasor, "p.json");
            var y = await CliApp.RunAsync(new[] { "profiller", "--disa-aktar", dosya, "--profil", yerlesik }, new StringWriter(), yerlesikHata,
                CliText.ForLanguage("en"), new CliServices { UserPresets = () => Kullanici }, CancellationToken.None);
            var b = await CliApp.RunAsync(new[] { "profiller", "--disa-aktar", dosya }, new StringWriter(), new StringWriter(),
                CliText.ForLanguage("en"), new CliServices { UserPresets = () => Array.Empty<PresetProfile>() }, CancellationToken.None);
            var u = await CliApp.RunAsync(new[] { "profiller", "--disa-aktar", dosya, "--profil", "yok-boyle" }, new StringWriter(), bilinmezHata,
                CliText.ForLanguage("en"), new CliServices { UserPresets = () => Kullanici }, CancellationToken.None);
            return (y, b, File.Exists(dosya), u);
        });

        Assert.Equal(ExitCodes.Usage, yerlesikCikis);
        Assert.Equal(ExitCodes.Error, bosCikis);
        Assert.Equal(ExitCodes.Usage, bilinmez);
        Assert.False(yazildi);
        var en = CliText.ForLanguage("en");
        Assert.Contains(en.Format("error.export-builtin", yerlesik), yerlesikHata.ToString(), StringComparison.Ordinal);
        Assert.Contains(en.Format("error.bad-profile", "yok-boyle"), bilinmezHata.ToString(), StringComparison.Ordinal);
        Assert.NotEqual(en.Format("error.export-builtin", "x"), en.Format("error.bad-profile", "x"));
    }

    [Fact]
    public async Task DonusturJsonSonucuVeNdjsonIlerleme()
    {
        var (exit, stdout, stderr, verilenCikti, info) = await KlasordeAsync(async klasor =>
        {
            var girdi = Path.Combine(klasor, "girdi.mkv");
            File.WriteAllBytes(girdi, new byte[16]);
            var kaynak = Ornek() with { FilePath = girdi };
            string? cikti = null;
            var services = new CliServices
            {
                MissingTool = () => null,
                Probe = (_, _) => Task.FromResult(kaynak),
                Availability = () => null,
                Convert = (_, _, output, progress, _) =>
                {
                    cikti = output;
                    progress.Report(new EncodeProgress(0.5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 0.5, "encode"));
                    return Task.FromResult(new ConversionResult(output, 1.25));
                }
            };
            var o = new StringWriter();
            var e = new StringWriter();
            var x = await CliApp.RunAsync(new[] { "donustur", girdi, "--kodek", "hevc", "--crf", "26", "--json" }, o, e,
                CliText.ForLanguage("en"), services, CancellationToken.None);
            return (x, o.ToString(), e.ToString(), cikti, kaynak);
        });

        Assert.Equal(ExitCodes.InBand, exit);
        Assert.EndsWith("girdi_converted.mp4", verilenCikti, StringComparison.Ordinal);
        using var sonuc = JsonDocument.Parse(stdout);
        var kok = sonuc.RootElement;
        Assert.Equal("donustur", kok.GetProperty("command").GetString());
        Assert.Equal("libx265", kok.GetProperty("videoCodec").GetString());
        Assert.True(kok.GetProperty("result").GetProperty("success").GetBoolean());
        Assert.Equal(1.25, kok.GetProperty("result").GetProperty("outputMb").GetDouble());
        var plan = CliConvert.Plan(Ayristir("donustur", info.FilePath, "--kodek", "hevc", "--crf", "26"));
        Assert.Equal(CliConvert.Arguments(info, plan, verilenCikti!, null),
            kok.GetProperty("arguments").EnumerateArray().Select(a => a.GetString()!).ToList());

        var satirlar = stderr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(satirlar);
        foreach (var satir in satirlar)
        {
            using var nesne = JsonDocument.Parse(satir);
            Assert.True(nesne.RootElement.TryGetProperty("type", out _), satir);
        }
        Assert.Contains(satirlar, s => s.Contains("\"type\":\"progress\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DonusturGecersizPlandaKodlamaKosmaz()
    {
        var exit = await KlasordeAsync(async klasor =>
        {
            var girdi = Path.Combine(klasor, "girdi.mkv");
            File.WriteAllBytes(girdi, new byte[16]);
            var services = new CliServices
            {
                MissingTool = () => null,
                Probe = (_, _) => Task.FromResult(Ornek() with { FilePath = girdi }),
                Availability = () => null,
                Convert = (_, _, _, _, _) => throw new InvalidOperationException("kodlama kosmamali")
            };
            return await CliApp.RunAsync(new[] { "donustur", girdi, "--kap", "webm", "--kodek", "h264" }, new StringWriter(), new StringWriter(),
                CliText.ForLanguage("en"), services, CancellationToken.None);
        });

        Assert.Equal(ExitCodes.Usage, exit);
    }

    public static TheoryData<string[]> Secimler => new()
    {
        new[] { "--kodek", "hevc", "--crf", "26" },
        new[] { "--kap", "webm", "--kodek", "vp9", "--bit-hizi", "2500", "--cozunurluk", "720", "--ses-kodek", "opus", "--ses-bit-hizi", "96" },
        new[] { "--kap", "mkv", "--kodek", "av1", "--crf", "35", "--cozunurluk", "1280x720", "--ses-kodek", "yok", "--meta-yok" },
        new[] { "--kap", "mov", "--kodek", "prores", "--kare-hizi", "25" },
        new[] { "--kap", "gif", "--cozunurluk", "480", "--kare-hizi", "15" },
        new[] { "--kap", "mp3", "--ses-bit-hizi", "192" },
        new[] { "--kap", "flac" }
    };

    [Theory]
    [MemberData(nameof(Secimler))]
    public void DonusturArgumaniPanelinKomutuylaAyni(string[] secim)
    {
        var info = Ornek();
        var request = Ayristir(new[] { "donustur", info.FilePath }.Concat(secim).ToArray());
        var cliPlan = CliConvert.Plan(request);
        var sabit = Path.Combine(@"C:\cikti", "out." + cliPlan.Container);
        var cliSabit = FfmpegArguments.ToCommandLine(CliConvert.Arguments(info, cliPlan, sabit, null));
        var cliKomut = FfmpegArguments.ToCommandLine(CliConvert.Arguments(info, cliPlan, CliConvert.OutputPath(request, info, cliPlan), null));

        var (panelPlan, panelSabit, panelKomut) = Pencerede(window =>
        {
            window.LoadWithoutProbing(info.FilePath, info);
            window.ApplyConvertPreset(request.Conversion!);
            var plan = window.CurrentConversionPlan();
            return (plan, FfmpegArguments.ToCommandLine(ConversionArguments.Build(info, plan, sabit, plan.Gif ? "palette.png" : null)),
                window.TxtConvertCommand.Text ?? "");
        });

        Assert.Equal(cliPlan.Container, panelPlan.Container);
        Assert.Equal(cliPlan.VideoCodec, panelPlan.VideoCodec);
        Assert.Equal(cliPlan.AudioCodec, panelPlan.AudioCodec);
        Assert.Equal(panelSabit, cliSabit);
        Assert.Equal(panelKomut, cliKomut);
    }

    [Fact]
    public void KodekVeKapKumeleriPanelinListeleri()
    {
        var (kaplar, videolar, sesler) = Pencerede(window => (
            Etiketler(window.CmbContainer),
            Etiketler(window.CmbConvertCodec),
            window.CmbConvertAudio.Items.OfType<ComboBoxItem>().Select(i => i.Tag as string ?? ConversionPreset.NoAudio).ToList()));

        Assert.Equal(kaplar.Order(), CliConvert.Containers.Order());
        Assert.Equal(videolar.Order(), CliConvert.VideoCodecs.Order());
        Assert.Equal(sesler.Order(), CliConvert.AudioCodecs.Order());
    }

    private static List<string> Etiketler(ComboBox box)
        => box.Items.OfType<ComboBoxItem>().Select(i => i.Tag as string).OfType<string>().ToList();

    private static T Pencerede<T>(Func<MainWindow, T> govde)
        => Klasorde(klasor => AppHost.Run(() =>
        {
            var window = new MainWindow
            {
                SettingsPathOverride = Path.Combine(klasor, "settings.json"),
                PresetPathOverride = Path.Combine(klasor, "presets.json"),
                ConvertPresetPathOverride = Path.Combine(klasor, "convert-presets.json")
            };
            try { return govde(window); }
            finally { window.Close(); }
        }));

    private static CliServices Unreachable() => new()
    {
        MissingTool = () => throw new InvalidOperationException("tool lookup must not run"),
        Probe = (_, _) => throw new InvalidOperationException("probe must not run"),
        Availability = () => throw new InvalidOperationException("availability must not run")
    };
}
