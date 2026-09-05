using System.Globalization;
using System.Reflection;
using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// T165: bilincli kullanicinin motorun sekiz kararini elle gecersiz kilmasi
/// (<c>docs/danisma/004-arayuz-yonu-gorusu.md</c>'nin "acilacak" listesi).
/// </summary>
public sealed class ManualOverrideTests
{
    private readonly ITestOutputHelper _output;

    public ManualOverrideTests(ITestOutputHelper output) => _output = output;

    private sealed class FakeAvailability : IEncoderAvailability
    {
        private readonly Dictionary<string, EncoderProbeState> _states;

        public FakeAvailability(params (string Codec, EncoderProbeState State)[] states)
            => _states = states.ToDictionary(s => s.Codec, s => s.State, StringComparer.OrdinalIgnoreCase);

        public bool HasEncoder(string name) => _states.ContainsKey(name);
        public bool WorksAsEncoder(string codec) => _states.TryGetValue(codec, out var s) && s == EncoderProbeState.Working;
        public EncoderProbeState EncoderState(string codec) => _states.TryGetValue(codec, out var s) ? s : EncoderProbeState.NotWorking;
    }

    private static FakeAvailability AllWorking() => new(
        ("libx264", EncoderProbeState.Working),
        ("libx265", EncoderProbeState.Working),
        ("libsvtav1", EncoderProbeState.Working),
        ("h264_nvenc", EncoderProbeState.Working),
        ("hevc_nvenc", EncoderProbeState.Working),
        ("av1_nvenc", EncoderProbeState.Working));

    private static FakeAvailability YalnizYazilim() => new(
        ("libx264", EncoderProbeState.Working),
        ("libx265", EncoderProbeState.Working),
        ("libsvtav1", EncoderProbeState.Working),
        ("h264_nvenc", EncoderProbeState.NotWorking),
        ("hevc_nvenc", EncoderProbeState.NotWorking),
        ("av1_nvenc", EncoderProbeState.NotWorking));

    private static MediaInfo Info(int width = 1920, int height = 1080, double fps = 30, double durationSeconds = 120,
        long fileSizeBytes = 500L * 1024 * 1024, int audioChannels = 2, long audioBitrateBps = 128_000) => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = fileSizeBytes,
        DurationSeconds = durationSeconds,
        Width = width,
        Height = height,
        Fps = fps,
        VideoCodec = "h264",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = audioBitrateBps,
        AudioChannels = audioChannels
    };

    // --- K1: varsayilan, T165 oncesi motorla birebir ayni plani uretiyor ---
    //
    // Beklenen degerler uydurulmadi: 9b092e9 (T165'in ebeveyni, sozlesme oncesi motor)
    // agacinda ayni bes bilesim kosuldu ve cikti buraya alindi. Olcum ve ham cikti
    // docs/olcumler/elle-gecersiz-kilma.md'de. Varsayilan davranisi degistiren her
    // mutasyon bu kollari dusurur.

    [Theory]
    [InlineData(1920, 1080, 30, 120, 25.0, "libsvtav1", "2pass", 1567, -1, 1920, 1080, 30.0, 128, -1, "6")]
    [InlineData(1280, 720, 24, 300, 8.0, "libsvtav1", "2pass", 188, -1, 1202, 676, 24.0, 26, 1, "6")]
    [InlineData(3840, 2160, 60, 45, 50.0, "libsvtav1", "2pass", 9016, -1, 3840, 2160, 60.0, 128, -1, "6")]
    [InlineData(1920, 1080, 30, 600, 6.0, "libsvtav1", "2pass", 80, -1, 690, 388, 30.0, 0, -1, "6")]
    [InlineData(1280, 720, 30, 30, 100.0, "libx264", "2pass", 27305, -1, 1280, 720, 30.0, 128, -1, "slow")]
    public void K1_VarsayilanT165OncesiMotorlaBirebirAyni(
        int srcW, int srcH, double srcFps, double durationSeconds, double targetMb,
        string codec, string mode, int videoK, int crf, int width, int height, double fps,
        int audioK, int audioChannels, string preset)
    {
        var info = Info(srcW, srcH, srcFps, durationSeconds);
        var result = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = targetMb, Codec = CodecPreference.Auto }, null, AllWorking());
        var p = result.Plan;

        _output.WriteLine($"{srcW}x{srcH}@{srcFps} -> {targetMb}MB");
        _output.WriteLine($"  taban (9b092e9): {codec}|{mode}|{videoK}k|crf={(crf < 0 ? "-" : crf.ToString())}|{width}x{height}@{fps:0.###}|ses {audioK}k/{(audioChannels < 0 ? "kaynak" : audioChannels.ToString())}|preset {preset}");
        _output.WriteLine($"  simdi   (T165): {p.Codec}|{p.Mode}|{p.VideoBitrateK}k|crf={p.Crf?.ToString() ?? "-"}|{p.Width}x{p.Height}@{p.Fps:0.###}|ses {p.AudioBitrateK}k/{p.AudioChannels?.ToString() ?? "kaynak"}|preset {p.Preset}");

        Assert.Equal(codec, p.Codec);
        Assert.Equal(mode, p.Mode);
        Assert.Equal(videoK, p.VideoBitrateK);
        Assert.Equal(crf < 0 ? (int?)null : crf, p.Crf);
        Assert.Equal(width, p.Width);
        Assert.Equal(height, p.Height);
        Assert.Equal(fps, p.Fps, 3);
        Assert.Equal(audioK, p.AudioBitrateK);
        Assert.Equal(audioChannels < 0 ? (int?)null : audioChannels, p.AudioChannels);
        Assert.Equal(preset, p.Preset);
        Assert.DoesNotContain(p.ReasonCodes, n => n.Code.ToString().StartsWith("Manual", StringComparison.Ordinal));
    }

    // --- K2: acilan sekiz kalemin her biri komut satirina ulasiyor ---

    private sealed record Kalem(
        string Ad,
        MediaInfo Info,
        PlanOptions Options,
        int Pass,
        string SabitlenenDeger,
        Func<EncodePlan, string> BeklenenArguman,
        ReasonCode Not,
        Func<EncodePlan, bool>? EkKosul = null,
        string EkKosulAdi = "");

    public static IEnumerable<object[]> KalemIdleri() => new[]
    {
        "kip-2pass", "kip-crf", "crf", "on-ayar", "ses-kbps",
        "ses-stereo", "ses-mono", "ses-yok", "cozunurluk-tabani", "kare-hizi-tabani",
        "kodlayici-yazilim", "kodlayici-donanim"
    }.Select(id => new object[] { id });

    private static MediaInfo KucukKaynak() => new()
    {
        FilePath = "kucuk.mp4",
        FileSizeBytes = 200L * 1024 * 1024,
        DurationSeconds = 10,
        Width = 320,
        Height = 180,
        Fps = 24,
        VideoCodec = "h264",
        TotalBitrateBps = 400_000,
        AudioCodec = "aac",
        AudioBitrateBps = 96_000,
        AudioChannels = 2
    };

    /// <summary>
    /// Kullanicinin bir kalemi sabitledigi durumu, motorun o kalemde baska bir sey
    /// sectigi bir senaryoyla birlikte kurar. Kalemin "gecti" sayilmasi icin uretilen
    /// ffmpeg komut satirinda gorunmesi gerekir; senaryo motorun kendiliginden ayni
    /// degeri sectigi bir yere denk geliyorsa olcu hicbir sey kanitlamaz, o yuzden
    /// her senaryonun basinda motorun ne sectigi ayrica dogrulaniyor.
    /// </summary>
    private static Kalem KalemFor(string id)
    {
        switch (id)
        {
            case "kip-2pass":
            {
                var info = KucukKaynak();
                var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 90, Codec = CodecPreference.Compatible, FillPolicy = FillPolicy.QualityCeiling }, null, AllWorking());
                Assert.Equal("crf", dogal.Plan.Mode);
                return new Kalem("EncodeMode", info,
                    new PlanOptions { TargetMb = 90, Codec = CodecPreference.Compatible, FillPolicy = FillPolicy.QualityCeiling, LockedMode = EncodeMode.TwoPass },
                    2, "TwoPass", p => $"-b:v {p.VideoBitrateK}k", ReasonCode.ManualModeOverride,
                    p => p.Mode == "2pass" && p.Crf is null, "kip 2pass ve crf yok");
            }
            case "kip-crf":
            {
                var info = Info();
                var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 4, Codec = CodecPreference.Compatible }, null, AllWorking());
                Assert.Equal("2pass", dogal.Plan.Mode);
                return new Kalem("EncodeMode", info,
                    new PlanOptions { TargetMb = 4, Codec = CodecPreference.Compatible, LockedMode = EncodeMode.Crf },
                    0, "Crf", p => $"-crf {p.Crf}", ReasonCode.ManualModeOverride,
                    p => p.Mode == "crf" && p.Crf is not null, "kip crf");
            }
            case "crf":
            {
                var info = Info();
                var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible }, null, AllWorking());
                Assert.NotEqual(19, dogal.Plan.Crf);
                return new Kalem("CRF degeri", info,
                    new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, LockedCrf = 19 },
                    0, "19", _ => "-crf 19", ReasonCode.ManualCrfOverride,
                    p => p.Crf == 19, "crf 19");
            }
            case "on-ayar":
            {
                var info = Info();
                var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality }, null, AllWorking());
                Assert.NotEqual("veryslow", dogal.Plan.Preset);
                return new Kalem("preset / hiz", info,
                    new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality, LockedPreset = "veryslow" },
                    2, "veryslow", _ => "-preset veryslow", ReasonCode.ManualPresetOverride,
                    p => p.Preset == "veryslow", "preset veryslow");
            }
            case "ses-kbps":
            {
                var info = Info();
                var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible }, null, AllWorking());
                Assert.NotEqual(96, dogal.Plan.AudioBitrateK);
                return new Kalem("ses hedefi", info,
                    new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, LockedAudioKbps = 96 },
                    0, "96 kbps", _ => "-b:a 96k", ReasonCode.ManualAudioBitrateOverride,
                    p => p.AudioBitrateK == 96, "ses 96k");
            }
            case "ses-stereo":
            case "ses-mono":
            {
                var mono = id == "ses-mono";
                var info = Info();
                var kanal = mono ? AudioChannelOverride.Mono : AudioChannelOverride.Stereo;
                var beklenen = mono ? 1 : 2;
                var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible }, null, AllWorking());
                Assert.NotEqual(beklenen, dogal.Plan.AudioChannels);
                return new Kalem("ses kanali", info,
                    new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, AudioChannels = kanal },
                    0, kanal.ToString(), _ => $"-ac {beklenen}", ReasonCode.ManualAudioChannelsOverride,
                    p => p.AudioChannels == beklenen, $"kanal {beklenen}");
            }
            case "ses-yok":
            {
                var info = Info();
                var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible }, null, AllWorking());
                Assert.NotNull(dogal.Plan.AudioCodec);
                return new Kalem("ses kanali", info,
                    new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, AudioChannels = AudioChannelOverride.None },
                    0, "None", _ => "-an", ReasonCode.ManualAudioChannelsOverride,
                    p => p.AudioCodec is null && p.AudioBitrateK == 0, "ses yok");
            }
            case "cozunurluk-tabani":
            {
                var info = Info(1920, 1080, 30, 120);
                var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 3, Codec = CodecPreference.Auto }, null, AllWorking());
                Assert.True(dogal.Plan.Height < 720, $"taban olmadan da 720'nin altina dusmezse bu olcu bir sey kanitlamaz (bulunan {dogal.Plan.Height})");
                return new Kalem("cozunurluk tabani", info,
                    new PlanOptions { TargetMb = 3, Codec = CodecPreference.Auto, MinResolutionHeight = 720 },
                    2, "en az 720p", p => $"scale={p.Width}:{p.Height}", ReasonCode.ManualMinResolutionOverride,
                    p => p.Height >= 720, "yukseklik >= 720");
            }
            case "kare-hizi-tabani":
            {
                var info = Info(1920, 1080, 60, 600);
                double hedef = 0;
                PlanResult? dogal = null;
                foreach (var aday in new[] { 8.0, 5.0, 3.0, 2.0, 1.5, 1.0, 0.6, 0.4 })
                {
                    var deneme = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = aday, Codec = CodecPreference.Auto, AllowResolutionDrop = false }, null, AllWorking());
                    if (deneme.Plan.Fps < 24) { dogal = deneme; hedef = aday; break; }
                }
                Assert.NotNull(dogal);
                return new Kalem("kare hizi tabani", info,
                    new PlanOptions { TargetMb = hedef, Codec = CodecPreference.Auto, AllowResolutionDrop = false, MinFps = 24 },
                    2, "en az 24", p => $"fps={p.Fps.ToString("0.###", CultureInfo.InvariantCulture)}", ReasonCode.ManualMinFpsOverride,
                    p => p.Fps >= 24 - 0.01 && p.Fps < info.Fps - 0.01, "24 <= fps < kaynak fps");
            }
            case "kodlayici-yazilim":
            {
                var info = Info();
                var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Fast, SpeedMode = SpeedMode.Fast }, null, AllWorking());
                Assert.True(CodecModel.IsHardware(dogal.Plan.Codec), $"motor kendiliginden donanim secmezse bu olcu bir sey kanitlamaz (bulunan {dogal.Plan.Codec})");
                return new Kalem("kodlayici yolu", info,
                    new PlanOptions { TargetMb = 25, Codec = CodecPreference.Fast, SpeedMode = SpeedMode.Fast, EncoderPath = EncoderPathOverride.Software },
                    0, "Software", p => $"-c:v {p.Codec}", ReasonCode.ManualEncoderPathOverride,
                    p => !CodecModel.IsHardware(p.Codec), "yazilim kodlayici");
            }
            case "kodlayici-donanim":
            {
                var info = Info();
                var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality }, null, AllWorking());
                Assert.False(CodecModel.IsHardware(dogal.Plan.Codec), $"motor kendiliginden yazilim secmezse bu olcu bir sey kanitlamaz (bulunan {dogal.Plan.Codec})");
                return new Kalem("kodlayici yolu", info,
                    new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality, EncoderPath = EncoderPathOverride.Hardware },
                    0, "Hardware", p => $"-c:v {p.Codec}", ReasonCode.ManualEncoderPathOverride,
                    p => CodecModel.IsHardware(p.Codec), "donanim kodlayici");
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(id), id, "bilinmeyen kalem");
        }
    }

    [Theory]
    [MemberData(nameof(KalemIdleri))]
    public void K2_SabitlenenDegerFfmpegKomutSatirindaGorunuyor(string id)
    {
        var kalem = KalemFor(id);
        var result = PlanCalculator.BuildDetailed(kalem.Info, kalem.Options, null, AllWorking());
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(kalem.Info, result.Plan, "out.mp4", kalem.Pass, kalem.Pass == 1 ? "log" : null));
        var beklenen = kalem.BeklenenArguman(result.Plan);

        _output.WriteLine($"| {kalem.Ad} | {kalem.SabitlenenDeger} | {beklenen} |");
        _output.WriteLine($"args: {args}");

        Assert.Contains(beklenen, args);
        if (kalem.EkKosul is not null)
            Assert.True(kalem.EkKosul(result.Plan), $"{kalem.EkKosulAdi} bekleniyordu; plan {result.Plan.Codec}/{result.Plan.Mode}/crf={result.Plan.Crf}/{result.Plan.Width}x{result.Plan.Height}@{result.Plan.Fps:0.##}/ses {result.Plan.AudioBitrateK}k");
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == kalem.Not);
    }

    [Fact]
    public void K2_KipSabitlenincCrfArgumaniDusuyor()
    {
        var kalem = KalemFor("kip-2pass");
        var result = PlanCalculator.BuildDetailed(kalem.Info, kalem.Options, null, AllWorking());
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(kalem.Info, result.Plan, "out.mp4", 2, null));

        _output.WriteLine(args);
        Assert.DoesNotContain("-crf", args);
    }

    // --- K3: CRF sabitlenince hedef boyut zorlanmiyor ---

    [Fact]
    public void K3_CrfSabitlenincHedefBoyutZorlanmiyor()
    {
        var info = Info(1920, 1080, 30, 300);
        const double hedefMb = 25.0;

        var serbest = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = hedefMb, Codec = CodecPreference.Compatible }, null, AllWorking());
        var sabit = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = hedefMb, Codec = CodecPreference.Compatible, LockedCrf = 16 }, null, AllWorking());

        _output.WriteLine($"hedef {hedefMb}MB");
        _output.WriteLine($"  serbest: mode={serbest.Plan.Mode} crf={serbest.Plan.Crf?.ToString() ?? "-"} {serbest.Plan.Width}x{serbest.Plan.Height}@{serbest.Plan.Fps:0.##} videoK={serbest.Plan.VideoBitrateK} tahmin={serbest.Estimate.ExpectedMb:0.00}MB");
        _output.WriteLine($"  crf=16 : mode={sabit.Plan.Mode} crf={sabit.Plan.Crf} {sabit.Plan.Width}x{sabit.Plan.Height}@{sabit.Plan.Fps:0.##} videoK={sabit.Plan.VideoBitrateK} tahmin={sabit.Estimate.ExpectedMb:0.00}MB");
        _output.WriteLine($"  gerekce: {sabit.Plan.Reason}");

        Assert.True(serbest.Estimate.ExpectedMb <= hedefMb * 1.02,
            $"gecersiz kilma yokken motor hedefi zorlamali; tahmin {serbest.Estimate.ExpectedMb:0.00}MB, hedef {hedefMb}MB");
        Assert.True(sabit.Estimate.ExpectedMb > hedefMb * 1.2,
            $"CRF sabitken hedef zorlanmamali; tahmin {sabit.Estimate.ExpectedMb:0.00}MB, hedef {hedefMb}MB");
        Assert.Equal("crf", sabit.Plan.Mode);
        Assert.Equal(16, sabit.Plan.Crf);
        Assert.Contains(sabit.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualCrfOverride);
    }

    /// <summary>
    /// Ayni CRF iki farkli hedefte ayni kodlama yogunlugunu vermeli: hedef boyut artik
    /// CRF'i cekmiyor. Hedef yine zorlanmaya baslarsa iki taraf ayrisir ve bu kol duser.
    /// </summary>
    [Fact]
    public void K3_AyniCrfFarkliHedeflerdeAyniCrfiVeriyor()
    {
        var info = Info(1920, 1080, 30, 300);

        var serbestKucuk = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible }, null, AllWorking());
        var serbestBuyuk = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 120, Codec = CodecPreference.Compatible }, null, AllWorking());
        Assert.NotEqual(serbestKucuk.Plan.VideoBitrateK, serbestBuyuk.Plan.VideoBitrateK);

        var sabitKucuk = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, LockedCrf = 22 }, null, AllWorking());
        var sabitBuyuk = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 120, Codec = CodecPreference.Compatible, LockedCrf = 22 }, null, AllWorking());

        _output.WriteLine($"serbest 25MB videoK={serbestKucuk.Plan.VideoBitrateK} / 120MB videoK={serbestBuyuk.Plan.VideoBitrateK}");
        _output.WriteLine($"crf=22  25MB crf={sabitKucuk.Plan.Crf} videoK={sabitKucuk.Plan.VideoBitrateK} / 120MB crf={sabitBuyuk.Plan.Crf} videoK={sabitBuyuk.Plan.VideoBitrateK}");

        Assert.Equal(22, sabitKucuk.Plan.Crf);
        Assert.Equal(22, sabitBuyuk.Plan.Crf);
        Assert.NotEqual(serbestKucuk.Plan.VideoBitrateK, sabitKucuk.Plan.VideoBitrateK);
        Assert.NotEqual(serbestBuyuk.Plan.VideoBitrateK, sabitBuyuk.Plan.VideoBitrateK);
        Assert.True(sabitKucuk.Plan.VideoBitrateK > serbestKucuk.Plan.VideoBitrateK,
            $"CRF sabitken bitrate butceden degil CRF'ten turemeli (butce {serbestKucuk.Plan.VideoBitrateK}k, plan {sabitKucuk.Plan.VideoBitrateK}k)");
    }

    // --- K4: gecersiz kilma plan panelinde gerekcelenir ---

    [Theory]
    [MemberData(nameof(KalemIdleri))]
    public void K4_HerKalemNotuIkiAlaniDolduruyor(string id)
    {
        var kalem = KalemFor(id);
        var result = PlanCalculator.BuildDetailed(kalem.Info, kalem.Options, null, AllWorking());
        var not = Assert.Single(result.Plan.ReasonCodes, n => n.Code == kalem.Not);

        _output.WriteLine($"| {kalem.Ad} | {kalem.Not} | ManualOverrideValue={not.ManualOverrideValue} | EngineWouldHaveChosen={not.EngineWouldHaveChosen} |");

        Assert.False(string.IsNullOrWhiteSpace(not.ManualOverrideValue), $"{id}: ManualOverrideValue bos");
        Assert.False(string.IsNullOrWhiteSpace(not.EngineWouldHaveChosen), $"{id}: EngineWouldHaveChosen bos");
        Assert.Contains(not.EngineWouldHaveChosen!, result.Plan.Reason);
    }

    [Fact]
    public void K4_CrfNotuMotorunKendiSeciminiTasiyor()
    {
        var info = Info();
        var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible }, null, AllWorking());
        var result = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, LockedCrf = 30 }, null, AllWorking());
        var not = Assert.Single(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualCrfOverride);

        var motorunSecimi = dogal.Plan.Crf?.ToString(CultureInfo.InvariantCulture) ?? $"{dogal.Plan.Mode}@{dogal.Plan.VideoBitrateK}k";
        _output.WriteLine($"motorun secimi={motorunSecimi} ManualOverrideValue={not.ManualOverrideValue} EngineWouldHaveChosen={not.EngineWouldHaveChosen}");

        Assert.Equal("30", not.ManualOverrideValue);
        Assert.Equal(motorunSecimi, not.EngineWouldHaveChosen);
    }

    // --- D2: karsilanmayan donanim istegi karsilanmis gibi anlatilmiyor ---

    [Fact]
    public void D2_DonanimYokkenIstekKarsilanmadiDeniyor()
    {
        var info = Info();
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality, EncoderPath = EncoderPathOverride.Hardware };
        var result = PlanCalculator.BuildDetailed(info, options, null, YalnizYazilim());

        _output.WriteLine($"codec={result.Plan.Codec}");
        _output.WriteLine($"gerekce: {result.Plan.Reason}");

        Assert.False(CodecModel.IsHardware(result.Plan.Codec));
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathOverride);
        var not = Assert.Single(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathUnmet);
        Assert.Equal("Hardware", not.ManualOverrideValue);
        Assert.Equal(result.Plan.Codec, not.FallbackCodec);
        Assert.Contains("karsilanmadi", result.Plan.Reason);
    }

    [Fact]
    public void D2_DonanimVarkenIstekKarsilandiDeniyor()
    {
        var info = Info();
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality, EncoderPath = EncoderPathOverride.Hardware };
        var result = PlanCalculator.BuildDetailed(info, options, null, AllWorking());

        _output.WriteLine($"codec={result.Plan.Codec}");
        Assert.True(CodecModel.IsHardware(result.Plan.Codec));
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathUnmet);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathOverride);
    }

    // --- D3: kopyalama yolunda istek ya uygulanir ya dusuruldugu soylenir ---

    private static MediaInfo HedefinAltindaKaynak() => new()
    {
        FilePath = "zaten-kucuk.mp4",
        FileSizeBytes = 10L * 1024 * 1024,
        DurationSeconds = 60,
        Width = 1280,
        Height = 720,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1_400_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    [Theory]
    [InlineData("crf")]
    [InlineData("kip")]
    [InlineData("on-ayar")]
    [InlineData("ses-kbps")]
    [InlineData("ses-kanali")]
    public void D3_KopyaYolundaYenidenKodlamaIsteyenGecersizKilmaUygulaniyor(string kalem)
    {
        var info = HedefinAltindaKaynak();
        var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Auto }, null, AllWorking());
        Assert.Equal("passthrough", dogal.Plan.Mode);

        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Auto };
        switch (kalem)
        {
            case "crf": options.LockedCrf = 20; break;
            case "kip": options.LockedMode = EncodeMode.TwoPass; break;
            case "on-ayar": options.LockedPreset = "veryslow"; break;
            case "ses-kbps": options.LockedAudioKbps = 64; break;
            case "ses-kanali": options.AudioChannels = AudioChannelOverride.Mono; break;
        }

        var result = PlanCalculator.BuildDetailed(info, options, null, AllWorking());
        _output.WriteLine($"{kalem}: dogal={dogal.Plan.Mode} sabitlenmis={result.Plan.Mode} codec={result.Plan.Codec} crf={result.Plan.Crf?.ToString() ?? "-"} preset={result.Plan.Preset} ses={result.Plan.AudioBitrateK}k/{result.Plan.AudioChannels?.ToString() ?? "kaynak"}");

        Assert.NotEqual("passthrough", result.Plan.Mode);
        switch (kalem)
        {
            case "crf": Assert.Equal(20, result.Plan.Crf); break;
            case "kip": Assert.Equal("2pass", result.Plan.Mode); break;
            case "on-ayar": Assert.Equal("veryslow", result.Plan.Preset); break;
            case "ses-kbps": Assert.Equal(64, result.Plan.AudioBitrateK); break;
            case "ses-kanali": Assert.Equal(1, result.Plan.AudioChannels); break;
        }
    }

    [Theory]
    [InlineData("kodlayici-yolu")]
    [InlineData("cozunurluk-tabani")]
    [InlineData("kare-hizi-tabani")]
    public void D3_KopyaYolundaUygulanamayanIstekSessizceDusmuyor(string kalem)
    {
        var info = HedefinAltindaKaynak();
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Auto };
        var beklenenMetin = kalem switch
        {
            "kodlayici-yolu" => "kodlayici yolu",
            "cozunurluk-tabani" => "cozunurluk tabani",
            _ => "kare hizi tabani"
        };
        switch (kalem)
        {
            case "kodlayici-yolu": options.EncoderPath = EncoderPathOverride.Hardware; break;
            case "cozunurluk-tabani": options.MinResolutionHeight = 2160; break;
            case "kare-hizi-tabani": options.MinFps = 60; break;
        }

        var result = PlanCalculator.BuildDetailed(info, options, null, AllWorking());
        var not = Assert.Single(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualOverrideDroppedOnPassThrough);

        _output.WriteLine($"{kalem}: mode={result.Plan.Mode} not={not.ManualOverrideValue} -> {not.EngineWouldHaveChosen}");
        _output.WriteLine($"gerekce: {result.Plan.Reason}");

        Assert.Equal("passthrough", result.Plan.Mode);
        Assert.StartsWith(beklenenMetin + "=", not.ManualOverrideValue);
        Assert.Contains(beklenenMetin, result.Plan.Reason);
    }

    private static PlanOptions TabansizKopya(PlanOptions options) => new()
    {
        TargetMb = options.TargetMb,
        Intent = options.Intent,
        Codec = options.Codec,
        AllowResolutionDrop = options.AllowResolutionDrop,
        AllowFpsDrop = options.AllowFpsDrop,
        HdrPolicy = options.HdrPolicy,
        FillPolicy = options.FillPolicy,
        SpeedMode = options.SpeedMode
    };

    /// <summary>
    /// D1: kare hizi tabani komut satirini gercekten degistiriyor. Tabansiz plan 24'un
    /// altina inip `fps=` filtresini o degerle yaziyor; taban konunca ayni komut satiri
    /// tabana uyan baska bir deger tasiyor. Iki komut satiri ayni cikarsa taban ffmpeg'e
    /// hic ulasmamis demektir ve bu kol duser.
    /// </summary>
    [Fact]
    public void D1_KareHiziTabaniFfmpegKomutSatirindakiFpsiDegistiriyor()
    {
        var kalem = KalemFor("kare-hizi-tabani");
        var tabansiz = PlanCalculator.BuildDetailed(kalem.Info, TabansizKopya(kalem.Options), null, AllWorking());
        var tabanli = PlanCalculator.BuildDetailed(kalem.Info, kalem.Options, null, AllWorking());

        var tabansizArgs = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(kalem.Info, tabansiz.Plan, "out.mp4", 2, null));
        var tabanliArgs = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(kalem.Info, tabanli.Plan, "out.mp4", 2, null));

        _output.WriteLine($"tabansiz ({tabansiz.Plan.Fps:0.##} fps): {tabansizArgs}");
        _output.WriteLine($"tabanli  ({tabanli.Plan.Fps:0.##} fps): {tabanliArgs}");

        Assert.Contains($"fps={tabansiz.Plan.Fps.ToString("0.###", CultureInfo.InvariantCulture)}", tabansizArgs);
        Assert.True(tabansiz.Plan.Fps < 24, $"tabansiz plan 24'un altina inmezse bu olcu bir sey kanitlamaz (bulunan {tabansiz.Plan.Fps:0.##})");
        Assert.True(tabanli.Plan.Fps >= 24 - 0.01, $"tabanli plan tabana uymali (bulunan {tabanli.Plan.Fps:0.##})");
        Assert.Contains($"fps={tabanli.Plan.Fps.ToString("0.###", CultureInfo.InvariantCulture)}", tabanliArgs);
        Assert.NotEqual(tabansizArgs, tabanliArgs);
    }

    /// <summary>
    /// D1: kodlayici yolu komut satirina `-c:v` olarak ulasiyor. Yazilim ve donanim
    /// istekleri ayni girdide iki ayri `-c:v` uretmezse istek komut satirina hic
    /// gecmemis demektir.
    /// </summary>
    [Fact]
    public void D1_KodlayiciYoluFfmpegKomutSatirindakiCVyiDegistiriyor()
    {
        var info = Info();
        var temel = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality };
        var yazilim = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality, EncoderPath = EncoderPathOverride.Software }, null, AllWorking());
        var donanim = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Quality, EncoderPath = EncoderPathOverride.Hardware }, null, AllWorking());
        var otomatik = PlanCalculator.BuildDetailed(info, temel, null, AllWorking());

        var yazilimArgs = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(info, yazilim.Plan, "out.mp4", 0, null));
        var donanimArgs = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(info, donanim.Plan, "out.mp4", 0, null));

        _output.WriteLine($"otomatik: -c:v {otomatik.Plan.Codec}");
        _output.WriteLine($"yazilim : {yazilimArgs}");
        _output.WriteLine($"donanim : {donanimArgs}");

        Assert.Contains($"-c:v {yazilim.Plan.Codec}", yazilimArgs);
        Assert.Contains($"-c:v {donanim.Plan.Codec}", donanimArgs);
        Assert.False(CodecModel.IsHardware(yazilim.Plan.Codec));
        Assert.True(CodecModel.IsHardware(donanim.Plan.Codec));
        Assert.NotEqual(yazilim.Plan.Codec, donanim.Plan.Codec);
    }

    // --- D4: etkisiz istek "sabitlendi" diye kaydedilmiyor ---

    [Fact]
    public void D4_EtkisizTabanIstegiNotUretmiyor()
    {
        var info = Info(1920, 1080, 30, 600);
        var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 6, Codec = CodecPreference.Auto }, null, AllWorking());
        var options = new PlanOptions { TargetMb = 6, Codec = CodecPreference.Auto, MinResolutionHeight = 100, MinFps = 5 };
        var result = PlanCalculator.BuildDetailed(info, options, null, AllWorking());

        _output.WriteLine($"tabansiz {dogal.Plan.Width}x{dogal.Plan.Height}@{dogal.Plan.Fps:0.##} / istek 100p+5fps -> {result.Plan.Width}x{result.Plan.Height}@{result.Plan.Fps:0.##}");

        Assert.Equal(dogal.Plan.Height, result.Plan.Height);
        Assert.Equal(dogal.Plan.Fps, result.Plan.Fps, 3);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualMinResolutionOverride);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualMinFpsOverride);
    }

    [Fact]
    public void D4_EtkiliTabanNotuPlaninGercekDegeriniTasiyor()
    {
        var info = Info(1920, 1080, 30, 120);
        var dogal = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 3, Codec = CodecPreference.Auto }, null, AllWorking());
        var result = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 3, Codec = CodecPreference.Auto, MinResolutionHeight = 720 }, null, AllWorking());
        var not = Assert.Single(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualMinResolutionOverride);

        _output.WriteLine($"motor {dogal.Plan.Width}x{dogal.Plan.Height} -> plan {result.Plan.Width}x{result.Plan.Height}; not Height={not.Height} deger={not.ManualOverrideValue} motor={not.EngineWouldHaveChosen}");

        Assert.Equal(result.Plan.Height, not.Height);
        Assert.Equal(result.Plan.Width, not.Width);
        Assert.Equal("720", not.ManualOverrideValue);
        Assert.Equal(dogal.Plan.Height.ToString(CultureInfo.InvariantCulture), not.EngineWouldHaveChosen);
        Assert.NotEqual(not.ManualOverrideValue, not.EngineWouldHaveChosen);
    }

    [Fact]
    public void D4_EtkiliFpsTabanNotuPlaninGercekFpsiniTasiyor()
    {
        var kalem = KalemFor("kare-hizi-tabani");
        var dogal = PlanCalculator.BuildDetailed(kalem.Info, TabansizKopya(kalem.Options), null, AllWorking());
        var result = PlanCalculator.BuildDetailed(kalem.Info, kalem.Options, null, AllWorking());
        var not = Assert.Single(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualMinFpsOverride);

        _output.WriteLine($"motor {dogal.Plan.Fps:0.##} -> plan {result.Plan.Fps:0.##}; not Fps={not.Fps:0.##} deger={not.ManualOverrideValue} motor={not.EngineWouldHaveChosen}");

        Assert.Equal(result.Plan.Fps, not.Fps, 3);
        Assert.Equal("24", not.ManualOverrideValue);
        Assert.Equal(dogal.Plan.Fps.ToString("0.##", CultureInfo.InvariantCulture), not.EngineWouldHaveChosen);
    }

    // --- F1: hicbir kolun olcmedigi varsayilan kapi ---
    //
    // Tur 2'de bir mutasyon (bu belgede M9'un ilk varyanti) hicbir kolu dusurmeden gecti;
    // formulu kaydedilmemisti. Aranan yer bulundu: EffectiveTargetMb'nin
    // `Math.Min(targetMb, sourceMb * SourceSizeCap)` kapisi. `SourceSizeCap = 0.95 -> 0.80`
    // mutasyonu ManualOverrideTests'in 54 kolunu **ve** plan hesabina dokunan 14 sinifin
    // 280 kolunu hic dusurmeden geciyordu; kapiyi olcen tek kol yoktu. Bu kol o kapiyi
    // ucundan tutuyor: kaynagin 500 MB'inin %95'i 475 MB, kullanicinin 490 MB'lik hedefi
    // oraya kirpiliyor ve not kirpilan degeri tasiyor.

    [Fact]
    public void F1_KaynakUstuHedefKaynaginYuzde95ineKirpiliyor()
    {
        var info = Info(1920, 1080, 30, 120, fileSizeBytes: 500L * 1024 * 1024);
        var result = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 490, Codec = CodecPreference.Auto }, null, AllWorking());
        var not = Assert.Single(result.Plan.ReasonCodes, n => n.Code == ReasonCode.TargetCappedToSource);

        _output.WriteLine($"kaynak {info.FileSizeMb:0.##} MB, hedef 490 MB -> kirpilan {not.Mb:0.####} MB (not TargetMb={not.TargetMb:0.##})");
        _output.WriteLine($"EffectiveTargetMb(490, {info.FileSizeMb:0.##}) = {PlanCalculator.EffectiveTargetMb(490, info.FileSizeMb):0.####}");

        Assert.NotEqual(EncodeMode.PassThrough, result.Plan.ModeEnum);
        Assert.Equal(490.0, not.TargetMb, 6);
        Assert.Equal(info.FileSizeMb * 0.95, not.Mb, 6);
        Assert.Equal(475.0, not.Mb, 6);
        Assert.Equal(475.0, PlanCalculator.EffectiveTargetMb(490, info.FileSizeMb), 6);
    }

    // --- F2: kaynagin ustundeki taban istegi yeniden kodlama yolunda da soyleniyor ---
    //
    // Motor hicbir yolda yukari olcekleme yapmiyor: ScaleCandidates 1.0'dan baslayip
    // asagi iniyor, FpsCandidates kaynak fps'in ustune cikmiyor. Kaynagin ustundeki
    // taban istegi bu yuzden karsilanamaz; karsilanmadigi **yazilmak** zorunda.

    [Fact]
    public void F2_KaynagiAsanFpsTabaniYenidenKodlamaYolundaKarsilanmadiDeniyor()
    {
        var info = Info(1920, 1080, 30, 120);
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Auto, MinFps = 60 };
        var result = PlanCalculator.BuildDetailed(info, options, null, AllWorking());

        _output.WriteLine($"kaynak 1920x1080@30, istek MinFps=60 -> plan {result.Plan.Width}x{result.Plan.Height}@{result.Plan.Fps:0.##} kip={result.Plan.Mode}");
        _output.WriteLine($"gerekce: {result.Plan.Reason}");

        Assert.NotEqual(EncodeMode.PassThrough, result.Plan.ModeEnum);
        Assert.True(result.Plan.Fps < 60 - 0.01, $"motor 60 fps'e cikabiliyorsa bu senaryo bir sey olcmez (bulunan {result.Plan.Fps:0.##})");
        var not = Assert.Single(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualMinFpsUnmet);
        Assert.Equal("60", not.ManualOverrideValue);
        Assert.Equal(result.Plan.Fps, not.Fps, 3);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualMinFpsOverride);
        Assert.Contains("karsilanmadi", result.Plan.Reason);
    }

    [Fact]
    public void F2_KaynagiAsanCozunurlukTabaniYenidenKodlamaYolundaKarsilanmadiDeniyor()
    {
        var info = Info(1920, 1080, 30, 120);
        var options = new PlanOptions { TargetMb = 25, Codec = CodecPreference.Auto, MinResolutionHeight = 2160 };
        var result = PlanCalculator.BuildDetailed(info, options, null, AllWorking());

        _output.WriteLine($"kaynak 1920x1080@30, istek MinResolutionHeight=2160 -> plan {result.Plan.Width}x{result.Plan.Height}@{result.Plan.Fps:0.##} kip={result.Plan.Mode}");
        _output.WriteLine($"gerekce: {result.Plan.Reason}");

        Assert.NotEqual(EncodeMode.PassThrough, result.Plan.ModeEnum);
        Assert.True(result.Plan.Height < 2160, $"motor 2160p'ye cikabiliyorsa bu senaryo bir sey olcmez (bulunan {result.Plan.Height})");
        var not = Assert.Single(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualMinResolutionUnmet);
        Assert.Equal("2160", not.ManualOverrideValue);
        Assert.Equal(result.Plan.Height, not.Height);
        Assert.Equal(result.Plan.Width, not.Width);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualMinResolutionOverride);
        Assert.Contains("karsilanmadi", result.Plan.Reason);
    }

    /// <summary>
    /// F2 negatif kontrolu: karsilanabilen taban istegi "karsilanmadi" diye yazilmamali.
    /// Bu kol olmadan F2 duzeltmesi kosulsuz not yazarak yesile donebilirdi.
    /// </summary>
    [Fact]
    public void F2_KarsilanabilenTabanIstegiKarsilanmadiDemiyor()
    {
        var kalem = KalemFor("kare-hizi-tabani");
        var fps = PlanCalculator.BuildDetailed(kalem.Info, kalem.Options, null, AllWorking());
        var cozunurluk = PlanCalculator.BuildDetailed(Info(1920, 1080, 30, 120),
            new PlanOptions { TargetMb = 3, Codec = CodecPreference.Auto, MinResolutionHeight = 720 }, null, AllWorking());

        _output.WriteLine($"fps istegi 24 -> plan {fps.Plan.Fps:0.##}; cozunurluk istegi 720p -> plan {cozunurluk.Plan.Width}x{cozunurluk.Plan.Height}");

        Assert.True(fps.Plan.Fps >= 24 - 0.01);
        Assert.True(cozunurluk.Plan.Height >= 720);
        Assert.DoesNotContain(fps.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualMinFpsUnmet);
        Assert.DoesNotContain(cozunurluk.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualMinResolutionUnmet);
    }

    // --- K5: kapali kalanlar disaridan degistirilemiyor ---

    private static readonly HashSet<string> IzinliPlanOptionsAlanlari = new()
    {
        "TargetMb", "Intent", "Codec", "AllowResolutionDrop", "AllowFpsDrop", "HdrPolicy", "FillPolicy", "SpeedMode",
        "LockedCodec", "LockedMode", "LockedCrf", "LockedPreset", "LockedAudioKbps", "AudioChannels",
        "MinResolutionHeight", "MinFps", "EncoderPath"
    };

    [Fact]
    public void K5_PlanOptionsKapaliSabitleriDisaAcmiyor()
    {
        var fields = typeof(PlanOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToArray();
        _output.WriteLine($"PlanOptions alanlari: {string.Join(", ", fields)}");
        Assert.Equal(IzinliPlanOptionsAlanlari.OrderBy(x => x), fields.OrderBy(x => x));
    }

    private static readonly Dictionary<string, string[]> KapaliTipAlanlari = new()
    {
        ["FillBand"] = new[] { "LowerMb", "HardFloorMb", "UpperMb", "CenterMb", "RelativeWidth" },
        ["RegimeFloors"] = new[] { "MinScale", "MinHeight", "MinFps" }
    };

    [Theory]
    [InlineData("FillBand")]
    [InlineData("RegimeFloors")]
    public void K5_KapaliTiplerAlanKumesiDegismedi(string typeName)
    {
        var type = typeName == "FillBand" ? typeof(FillBand) : typeof(RegimeFloors);
        var fields = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToArray();
        _output.WriteLine($"{typeName}: {string.Join(", ", fields)}");
        Assert.Equal(KapaliTipAlanlari[typeName].OrderBy(x => x), fields.OrderBy(x => x));
    }
}
