using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using VidShrink.App.Recorder;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// later.md #66: <c>kaydet</c> kaydedicinin kayıtlı ayarlarından pencere açmadan kayıt isteği kurar.
/// Ekran yakalanmaz: ayrıştırma, istek kurma ve komutun akışı sahte kayıt katmanıyla ölçülür.
/// Geçici dosyalar <c>.calisma/test-ciktilari/cli-kaydet/</c>, test siler.
/// </summary>
public sealed class CliKayitTests : IDisposable
{
    private static readonly DateTime An = new(2026, 10, 15, 9, 30, 5);

    private readonly string _klasor = Path.Combine(TipSources.Root, ".calisma", "test-ciktilari", "cli-kaydet", Guid.NewGuid().ToString("N"));

    public CliKayitTests() => Directory.CreateDirectory(_klasor);

    public void Dispose()
    {
        if (Directory.Exists(_klasor)) Directory.Delete(_klasor, true);
    }

    private static CliRequest Ayristir(params string[] args)
    {
        var parsed = CliParser.Parse(args);
        Assert.True(parsed.Request is not null, parsed.ErrorKey + " " + parsed.ErrorArgument);
        return parsed.Request!;
    }

    private static CliRecordMachine Makine(RecorderPlatform platform = RecorderPlatform.Windows, IReadOnlyList<AudioCaptureDevice>? ses = null, IReadOnlyList<string>? kamera = null)
        => new(platform, ses ?? Array.Empty<AudioCaptureDevice>(), kamera ?? Array.Empty<string>(), Array.Empty<string>(), 8);

    private string AyarYaz(Action<RecorderSettings> doldur)
    {
        var ayar = new RecorderSettings { OutputFolder = _klasor };
        doldur(ayar);
        var dosya = Path.Combine(_klasor, CliRecordSettings.FileName);
        File.WriteAllBytes(dosya, ayar.ToJson());
        return dosya;
    }

    private static CliRecordPlan Kur(CliRequest cli, CliRecordSettings ayar, CliRecordMachine? makine = null)
    {
        var plan = CliRecord.Build(cli, ayar, makine ?? Makine(), An, out var hata, out var arguman);
        Assert.True(plan is not null, hata + " " + arguman);
        return plan!;
    }

    [Theory]
    [InlineData("kaydet")]
    [InlineData("record")]
    public void KomutKayitIstegineDuser(string ad)
    {
        var istek = Ayristir(ad, "--sure", "1:30", "--cikti", "a.mkv", "--ayarlar", "k.json", "--json");

        Assert.Equal(CliCommand.Record, istek.Command);
        Assert.Equal(90, istek.RecordSeconds);
        Assert.Equal("a.mkv", istek.Output);
        Assert.Equal("k.json", istek.RecordSettings);
        Assert.True(istek.Json);
    }

    [Fact]
    public void IngilizceSecenekAdlariAyniIstegiKurar()
    {
        var istek = Ayristir("record", "--duration", "45", "-o", "b.mp4", "--settings", "k.json");

        Assert.Equal(45, istek.RecordSeconds);
        Assert.Equal("b.mp4", istek.Output);
        Assert.Equal("k.json", istek.RecordSettings);
        Assert.Null(Ayristir("kaydet").RecordSeconds);
    }

    [Theory]
    [InlineData("--sure", "0", "error.record-duration")]
    [InlineData("--sure", "-5", "error.record-duration")]
    [InlineData("--sure", "abc", "error.record-duration")]
    [InlineData("--sure", "90000", "error.record-duration")]
    [InlineData("--cikti", "a.webm", "error.record-output-extension")]
    [InlineData("--cikti", "a", "error.record-output-extension")]
    [InlineData("girdi.mp4", null, "error.record-extra")]
    [InlineData("--hedef", "25MB", "error.unknown-option")]
    [InlineData("--sure", null, "error.missing-value")]
    public void YanlisArgumanKullanimHatasidir(string secenek, string? deger, string anahtar)
    {
        var args = deger is null ? new[] { "kaydet", secenek } : new[] { "kaydet", secenek, deger };
        var parsed = CliParser.Parse(args);

        Assert.Null(parsed.Request);
        Assert.Equal(anahtar, parsed.ErrorKey);
    }

    [Fact]
    public void PencereninYazdigiAyarAyniOkunur()
    {
        var dosya = AyarYaz(a =>
        {
            a.ManualMode = true;
            a.TargetSeconds = 120;
            a.TargetMegabytes = 40;
            a.Fps = 24;
            a.Codec = "libx265";
            a.Preset = "medium";
            a.Quality = 28;
            a.ShowCursor = false;
            a.ShowClicks = true;
            a.Target = RecorderTargetKind.Region;
            a.WindowTitle = "Not Defteri";
            a.RegionX = 10;
            a.RegionY = 20;
            a.RegionWidth = 640;
            a.RegionHeight = 360;
            a.MicrophoneName = "Mikrofon (USB)";
            a.SystemAudioName = "Hoparlör";
            a.WebcamName = "Kamera";
            a.WebcamShape = WebcamShape.Circle;
            a.WebcamMirror = true;
            a.WebcamX = 0.25;
            a.WebcamY = 0.5;
            a.WebcamSize = 0.3;
            a.WebcamBackground = WebcamBackground.Green;
            a.Container = RecorderContainer.Mov;
            a.ScreenIndex = 1;
            a.ScaleWidth = 1280;
            a.ScaleHeight = 720;
            a.KeyframeSeconds = 4;
            a.Profile = "main";
            a.Tune = "film";
            a.RateControl = RecorderRateControl.Bitrate;
            a.FrameRateMode = RecorderFrameRateMode.Constant;
            a.BitrateKbps = 4000;
            a.MaxBitrateKbps = 6000;
            a.BufferKbits = 8000;
            a.PixelFormat = "yuv420p10le";
            a.ColorSpace = "bt709";
            a.ColorRange = "tv";
            a.HdrToSdr = true;
            a.MaxDurationSeconds = 600;
            a.SplitSeconds = 300;
            a.SplitMegabytes = 500;
            a.AudioLayout = AudioTrackLayout.SeparateTracks;
            a.AudioGainDb = 3;
            a.AudioNoiseGate = true;
            a.AudioNoiseSuppression = true;
            a.AudioCompressor = true;
            a.AudioLimiter = true;
            a.AudioCodec = RecorderArguments.OpusAudioCodec;
            a.PrivacyMasks = new List<RecorderRegion> { new(1, 2, 30, 40) };
        });
        var pencere = RecorderSettings.Load(dosya);
        var komut = CliRecordSettings.Load(dosya);

        var farklar = new List<string>();
        foreach (var ozellik in typeof(CliRecordSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var karsi = typeof(RecorderSettings).GetProperty(ozellik.Name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Assert.True(karsi is not null, ozellik.Name + " pencerenin ayarında yok");
            var beklenen = karsi!.GetValue(pencere);
            var okunan = ozellik.GetValue(komut);
            var esit = beklenen is IEnumerable<RecorderRegion> b && okunan is IEnumerable<RecorderRegion> o
                ? b.SequenceEqual(o)
                : Equals(beklenen, okunan);
            if (!esit) farklar.Add($"{ozellik.Name}: pencere {beklenen}, komut {okunan}");
        }
        Assert.Empty(farklar);
        Assert.Equal(RecorderContainer.Mov, komut.Container);
        Assert.Equal(new RecorderRegion(1, 2, 30, 40), Assert.Single(komut.PrivacyMasks));
    }

    [Fact]
    public void DosyaYoksaPencereninIlkAcilisVarsayilanlari()
    {
        var komut = CliRecordSettings.Load(Path.Combine(_klasor, "yok.json"));
        var pencere = RecorderSettings.Load(Path.Combine(_klasor, "yok.json"));

        Assert.Equal(pencere.Fps, komut.Fps);
        Assert.Equal(pencere.Codec, komut.Codec);
        Assert.Equal(pencere.Container, komut.Container);
        Assert.Equal(pencere.ManualMode, komut.ManualMode);
        Assert.Equal(pencere.Target, komut.Target);
    }

    [Fact]
    public void ElleKipAyarlariIstegeTasinir()
    {
        var ayar = CliRecordSettings.Load(AyarYaz(a =>
        {
            a.ManualMode = true;
            a.Fps = 24;
            a.Codec = "libx264";
            a.Preset = "faster";
            a.Quality = 26;
            a.Target = RecorderTargetKind.Region;
            a.RegionX = 8;
            a.RegionY = 16;
            a.RegionWidth = 800;
            a.RegionHeight = 600;
            a.Container = RecorderContainer.Mkv;
            a.KeyframeSeconds = 3;
            a.Profile = "high";
            a.Tune = "film";
            a.PixelFormat = "yuv444p";
            a.MaxDurationSeconds = 300;
            a.SplitSeconds = 60;
            a.PrivacyMasks = new List<RecorderRegion> { new(0, 0, 50, 50) };
        }));

        var plan = Kur(Ayristir("kaydet"), ayar);
        var istek = plan.Request;

        Assert.Equal(RecorderPlatform.Windows, istek.Platform);
        Assert.Equal(RecorderTargetKind.Region, istek.Target);
        Assert.Equal(new RecorderRegion(8, 16, 800, 600), istek.Region);
        Assert.Equal(24, istek.Fps);
        Assert.Equal("libx264", istek.VideoCodec);
        Assert.Equal("faster", istek.Preset);
        Assert.Equal(26, istek.Quality);
        Assert.Equal(RecorderContainer.Mkv, istek.Container);
        Assert.Equal(3, istek.KeyframeSeconds);
        Assert.Equal("high", istek.Profile);
        Assert.Equal("film", istek.Tune);
        Assert.Equal(TimeSpan.FromSeconds(300), istek.MaxDuration);
        Assert.Equal(TimeSpan.FromSeconds(60), istek.Split?.Duration);
        Assert.Single(istek.PrivacyMasks);
        Assert.Same(AudioCapturePlan.Silent, istek.Audio);
        Assert.True(PathEquality.Same(Path.Combine(_klasor, "kayit_" + Bicim.DosyaDamgasi(An) + ".mkv"), plan.OutputPath));
        Assert.Empty(RecorderArguments.Validate(istek, plan.OutputPath));
    }

    [Fact]
    public void SureSecenegiKayitliSiniriEzer()
    {
        var ayar = CliRecordSettings.Load(AyarYaz(a => { a.ManualMode = true; a.MaxDurationSeconds = 600; }));

        Assert.Equal(TimeSpan.FromSeconds(600), Kur(Ayristir("kaydet"), ayar).Request.MaxDuration);
        Assert.Equal(TimeSpan.FromSeconds(90), Kur(Ayristir("kaydet", "--sure", "1:30"), ayar).Request.MaxDuration);
        Assert.Contains("-t", RecorderArguments.Build(Kur(Ayristir("kaydet", "--sure", "5"), ayar).Request, Path.Combine(_klasor, "a.mp4")));
    }

    [Fact]
    public void CiktiUzantisiKabiSecer()
    {
        var ayar = CliRecordSettings.Load(AyarYaz(a => { a.ManualMode = true; a.Container = RecorderContainer.Mp4; }));
        var yol = Path.Combine(_klasor, "alt", "film.mov");

        var plan = Kur(Ayristir("kaydet", "--cikti", yol), ayar);

        Assert.Equal(RecorderContainer.Mov, plan.Request.Container);
        Assert.True(PathEquality.Same(yol, plan.OutputPath));
    }

    [Fact]
    public void AyniSaniyedeIkinciKayitSayiAlir()
    {
        var ayar = CliRecordSettings.Load(AyarYaz(a => a.ManualMode = true));
        var ilk = Path.Combine(_klasor, "kayit_" + Bicim.DosyaDamgasi(An) + ".mp4");
        File.WriteAllText(ilk, "");

        var plan = Kur(Ayristir("kaydet"), ayar);

        Assert.True(PathEquality.Same(Path.Combine(_klasor, "kayit_" + Bicim.DosyaDamgasi(An) + "_2.mp4"), plan.OutputPath));
    }

    [Fact]
    public void OtomatikKipHedefSureyiSinirYapar()
    {
        var ayar = CliRecordSettings.Load(AyarYaz(a =>
        {
            a.ManualMode = false;
            a.Fps = 60;
            a.TargetSeconds = 30;
            a.Container = RecorderContainer.Mp4;
        }));

        var istek = Kur(Ayristir("kaydet"), ayar).Request;

        Assert.Equal(TimeSpan.FromSeconds(30), istek.MaxDuration);
        Assert.True(istek.Fps <= RecorderAutoPlan.GdigrabMaxFps, istek.Fps.ToString());
        Assert.Equal(RecorderContainer.Mp4, istek.Container);
    }

    [Fact]
    public void KayitliCihazAdlaBulunurYoksaNotDuser()
    {
        var mikrofon = new AudioCaptureDevice("Mikrofon (USB)", CaptureBackend.DirectShow, AudioSourceRole.Microphone);
        var ayar = CliRecordSettings.Load(AyarYaz(a =>
        {
            a.ManualMode = true;
            a.MicrophoneName = "mikrofon (usb)";
            a.SystemAudioName = "Kayıp Hoparlör";
            a.WebcamName = "Kayıp Kamera";
        }));

        var plan = Kur(Ayristir("kaydet"), ayar, Makine(ses: new[] { mikrofon }));

        Assert.NotSame(AudioCapturePlan.Silent, plan.Request.Audio);
        Assert.Null(plan.Request.Webcam);
        Assert.Equal(new[] { "Kayıp Hoparlör", "Kayıp Kamera" }, plan.Notes.Select(n => n.Argument));
        Assert.All(plan.Notes, n => Assert.Equal("record.device-missing", n.Key));
    }

    /// <summary>Pencerenin ses kodeği, sıkıştırıcı ve sınırlayıcı seçimi komutun kaydına da girer; kap Opus taşımıyorsa AAC kalır.</summary>
    [Fact]
    public void SesKodegiSikistiriciVeSinirlayiciKayitliAyardanGelir()
    {
        var mikrofon = new AudioCaptureDevice("Mikrofon (USB)", CaptureBackend.DirectShow, AudioSourceRole.Microphone);
        var ayar = CliRecordSettings.Load(AyarYaz(a =>
        {
            a.ManualMode = true;
            a.Container = RecorderContainer.Mkv;
            a.MicrophoneName = "Mikrofon (USB)";
            a.AudioCompressor = true;
            a.AudioLimiter = true;
            a.AudioCodec = RecorderArguments.OpusAudioCodec;
        }));

        var istek = Kur(Ayristir("kaydet"), ayar, Makine(ses: new[] { mikrofon })).Request;
        var args = string.Join(' ', RecorderArguments.Build(istek, Path.Combine(_klasor, "a.mkv")));

        Assert.Equal(RecorderArguments.OpusAudioCodec, istek.AudioCodec);
        Assert.Contains("acompressor", args, StringComparison.Ordinal);
        Assert.Contains("alimiter", args, StringComparison.Ordinal);
        Assert.Contains(RecorderArguments.OpusAudioCodec, args, StringComparison.Ordinal);

        var mov = Kur(Ayristir("kaydet", "--cikti", Path.Combine(_klasor, "b.mov")), ayar, Makine(ses: new[] { mikrofon })).Request;
        Assert.Equal(RecorderArguments.DefaultAudioCodec, mov.AudioCodec);
    }

    [Fact]
    public void KameraListedeysePencereninYerlesimiyleGirer()
    {
        var ayar = CliRecordSettings.Load(AyarYaz(a =>
        {
            a.ManualMode = true;
            a.WebcamName = "Kamera";
            a.WebcamShape = WebcamShape.Circle;
            a.WebcamX = 0.1;
            a.WebcamY = 0.2;
            a.WebcamSize = 0.3;
        }));

        var kamera = Kur(Ayristir("kaydet"), ayar, Makine(kamera: new[] { "Kamera" })).Request.Webcam;

        Assert.NotNull(kamera);
        Assert.Equal("Kamera", kamera!.Device);
        Assert.Equal(WebcamShape.Circle, kamera.Shape);
        Assert.Equal(new WebcamPlacement(0.1, 0.2, 0.3), kamera.Placement);
    }

    [Fact]
    public void PencereHedefiYalnizWindowstaKurulur()
    {
        var ayar = CliRecordSettings.Load(AyarYaz(a => { a.ManualMode = true; a.Target = RecorderTargetKind.Window; a.WindowTitle = "Not Defteri"; }));

        Assert.Null(CliRecord.Build(Ayristir("kaydet"), ayar, Makine(RecorderPlatform.Linux), An, out var hata, out _));
        Assert.Equal("error.record-window-platform", hata);
        Assert.Equal("Not Defteri", Kur(Ayristir("kaydet"), ayar).Request.WindowTitle);
    }

    [Fact]
    public async Task KomutSahteKayitlaIstegiTeslimEder()
    {
        var dosya = AyarYaz(a => { a.ManualMode = true; a.Container = RecorderContainer.Mkv; });
        RecorderRequest? verilen = null;
        string? yol = null;
        var hizmet = new CliServices
        {
            MissingTool = () => null,
            RecordMachine = () => Makine(),
            Now = () => An,
            Record = (istek, cikti, _) =>
            {
                verilen = istek;
                yol = cikti;
                return Task.FromResult(new RecordResult(true, cikti, 1.5, false, 0, "", 1));
            }
        };
        var stdout = new StringWriter();

        var exit = await CliApp.RunAsync(new[] { "kaydet", "--ayarlar", dosya, "--sure", "10", "--json" }, stdout, new StringWriter(),
            CliText.ForLanguage("en"), hizmet, CancellationToken.None);

        Assert.Equal(ExitCodes.InBand, exit);
        Assert.Equal(TimeSpan.FromSeconds(10), verilen!.MaxDuration);
        Assert.Equal(RecorderContainer.Mkv, verilen.Container);
        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("kaydet", json.RootElement.GetProperty("command").GetString());
        Assert.True(PathEquality.Same(yol!, json.RootElement.GetProperty("output").GetString()!));
        Assert.Equal(10, json.RootElement.GetProperty("durationLimitSeconds").GetDouble());
        Assert.True(json.RootElement.GetProperty("result").GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task VerilenAyarDosyasiYoksaKayitBaslamaz()
    {
        var hizmet = new CliServices
        {
            MissingTool = () => null,
            RecordMachine = () => Makine(),
            Record = (_, _, _) => throw new InvalidOperationException("record must not run")
        };
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(new[] { "kaydet", "--ayarlar", Path.Combine(_klasor, "yok.json") }, new StringWriter(), stderr,
            CliText.ForLanguage("en"), hizmet, CancellationToken.None);

        Assert.Equal(ExitCodes.Error, exit);
        Assert.Contains("yok.json", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void KaydetSecenekleriTamamlamadaVarVeAyristiriciTanir()
    {
        var kaynak = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.Cli", "CliRecord.cs"));
        var secenekler = Regex.Matches(kaynak, @"case\s+(""[^""]+""(?:\s+or\s+""[^""]+"")*)")
            .SelectMany(m => Regex.Matches(m.Groups[1].Value, @"""([^""]+)""").Select(s => s.Groups[1].Value))
            .Where(s => s.StartsWith('-'))
            .ToList();
        var kaydet = CliCompletion.Commands.Single(c => c.Names.Contains("kaydet"));

        Assert.Contains("record", kaydet.Names);
        Assert.Contains("--sure", secenekler);
        Assert.DoesNotContain(secenekler, s => !kaydet.Options.Contains(s));
        foreach (var secenek in kaydet.Options.Where(o => o is not ("--dil" or "--lang")))
        {
            var args = kaydet.ValueOptions.Contains(secenek) ? new[] { "kaydet", secenek, "5" } : new[] { "kaydet", secenek };
            Assert.NotEqual("error.unknown-option", CliParser.Parse(args).ErrorKey);
        }
        foreach (var kabuk in new[] { "bash", "zsh", "fish", "powershell" })
            Assert.Contains("kaydet", CliCompletion.Script(kabuk), StringComparison.Ordinal);
    }

    [Fact]
    public void YardimKaydetiAnlatir()
    {
        foreach (var dil in new[] { "en", "tr" })
        {
            var yardim = CliText.ForLanguage(dil).Format("help", "1");
            Assert.Contains("vidshrink kaydet", yardim, StringComparison.Ordinal);
            Assert.Contains("--sure", yardim, StringComparison.Ordinal);
            Assert.Contains("--ayarlar", yardim, StringComparison.Ordinal);
        }
    }
}
