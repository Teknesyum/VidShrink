using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// T162: kullanicinin kodegi acikca sectigi <see cref="PlanOptions.LockedCodec"/> kilidi.
/// </summary>
public sealed class CodecLockTests
{
    private readonly ITestOutputHelper _output;

    public CodecLockTests(ITestOutputHelper output) => _output = output;

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

    private static MediaInfo SampleInfo() => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    // --- K1 ---

    [Fact]
    public void VarsayilanKilitYoktur()
    {
        Assert.Null(new PlanOptions().LockedCodec);
    }

    [Theory]
    [InlineData(CodecPreference.Compatible)]
    [InlineData(CodecPreference.MaxCompression)]
    [InlineData(CodecPreference.Auto)]
    public void KilitBosKenPlanCiktisiDegismiyor(CodecPreference pref)
    {
        var withoutLock = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = pref };
        var withNullLock = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = pref, LockedCodec = null };
        var availability = AllWorking();

        var a = PlanCalculator.BuildDetailed(SampleInfo(), withoutLock, null, availability);
        var b = PlanCalculator.BuildDetailed(SampleInfo(), withNullLock, null, availability);

        _output.WriteLine($"{pref}: codec={a.Plan.Codec} mode={a.Plan.Mode} videoK={a.Plan.VideoBitrateK} {a.Plan.Width}x{a.Plan.Height}@{a.Plan.Fps:0.##}");
        Assert.Equal(a.Plan.Codec, b.Plan.Codec);
        Assert.Equal(a.Plan.Mode, b.Plan.Mode);
        Assert.Equal(a.Plan.VideoBitrateK, b.Plan.VideoBitrateK);
        Assert.Equal(a.Plan.Width, b.Plan.Width);
        Assert.Equal(a.Plan.Height, b.Plan.Height);
        Assert.Equal(a.Plan.Reason, b.Plan.Reason);
    }

    /// <summary>K4'un pini: bugun Extreme rejimde Auto'nun dustugu yer libsvtav1'dir; degismedi.</summary>
    [Fact]
    public void AutoKilitYokkenBugunkuKodegeDusuyor()
    {
        var options = new PlanOptions { TargetMb = 6, Codec = CodecPreference.Auto };
        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, AllWorking());

        _output.WriteLine($"Auto: codec={result.Plan.Codec} mode={result.Plan.Mode} videoK={result.Plan.VideoBitrateK} {result.Plan.Width}x{result.Plan.Height}@{result.Plan.Fps:0.##}");
        _output.WriteLine(result.Plan.Reason);
        Assert.Equal("libsvtav1", result.Plan.Codec);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback);
    }

    // --- K2 ---

    /// <summary>
    /// K2 kilidi: uc yazilim kilidi ayni kaynak ve ayni hedefte farkli cozunurluk uretmeli.
    /// Bitrate ayrisma iddiasi burada yok — yazilim kilitleri arasinda ayrismaz, bkz.
    /// <see cref="DonanimKilidiYazilimdanFarkliBitrateUretiyor"/>.
    /// </summary>
    [Fact]
    public void UcYazilimKilidiFarkliCozunurlukUretiyor()
    {
        var locks = new[] { "libx264", "libx265", "libsvtav1" };
        var availability = AllWorking();

        var rows = locks
            .Select(codec =>
            {
                var options = new PlanOptions { TargetMb = 6, Intent = Intent.Sharing, Codec = CodecPreference.Auto, LockedCodec = codec };
                var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, availability);
                return (Lock: codec, result.Plan);
            })
            .ToArray();

        _output.WriteLine("| kilit | secilen | mode | videoK | cozunurluk | fps |");
        _output.WriteLine("|---|---|---|---|---|---|");
        foreach (var row in rows)
            _output.WriteLine($"| {row.Lock} | {row.Plan.Codec} | {row.Plan.Mode} | {row.Plan.VideoBitrateK} | {row.Plan.Width}x{row.Plan.Height} | {row.Plan.Fps:0.##} |");

        Assert.Equal(3, rows.Length);
        Assert.Equal(locks, rows.Select(r => r.Plan.Codec));

        var resolutions = rows.Select(r => (r.Plan.Width, r.Plan.Height)).Distinct().Count();
        Assert.True(resolutions > 1,
            $"uc yazilim kilidi ayni cozunurluk uretti ({rows[0].Plan.Width}x{rows[0].Plan.Height}), kilit isleve etki etmiyor");
    }

    /// <summary>
    /// K1/K2: yazilim kilitleri ayni <c>DeliveryReserveK</c>'i (0) paylastigi icin bitrate
    /// hicbir zaman ayrisamaz (bkz. T166 denetim bulgusu). Ayrisma yalniz donanim kilidiyle
    /// kanitlanabilir: <see cref="PlanCalculator.HardwareDeliveryReserveK"/> yalniz donanim
    /// kodlayicida devreye giriyor, bu saf hesap oldugu icin gercek GPU gerekmiyor.
    /// </summary>
    [Fact]
    public void DonanimKilidiYazilimdanFarkliBitrateUretiyor()
    {
        var locks = new[] { "libx264", "libx265", "libsvtav1", "av1_nvenc" };
        var availability = AllWorking();

        var rows = locks
            .Select(codec =>
            {
                var options = new PlanOptions { TargetMb = 6, Intent = Intent.Sharing, Codec = CodecPreference.Auto, LockedCodec = codec };
                var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, availability);
                return (Lock: codec, result.Plan);
            })
            .ToArray();

        _output.WriteLine("| kilit | secilen | mode | videoK | cozunurluk | fps |");
        _output.WriteLine("|---|---|---|---|---|---|");
        foreach (var row in rows)
            _output.WriteLine($"| {row.Lock} | {row.Plan.Codec} | {row.Plan.Mode} | {row.Plan.VideoBitrateK} | {row.Plan.Width}x{row.Plan.Height} | {row.Plan.Fps:0.##} |");

        Assert.Equal(4, rows.Length);
        Assert.Equal(locks, rows.Select(r => r.Plan.Codec));

        var bitrates = rows.Select(r => r.Plan.VideoBitrateK).Distinct().Count();
        Assert.True(bitrates > 1,
            $"donanim kilidi de yazilim kilitleriyle ayni bitrate uretti (videoK={rows[0].Plan.VideoBitrateK}), DeliveryReserveK ayrimi kayboldu");
    }

    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    public void KilitliDonanimKodlayiciCalisiyorsaOnuKullaniyor(string locked)
    {
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Auto, LockedCodec = locked };
        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, AllWorking());

        Assert.Equal(locked, result.Plan.Codec);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback);
    }

    // --- K3 ---

    /// <summary>
    /// Aday derlemede yok: <see cref="PlanCalculator"/> yoklama sorusu sormadan yedege duser
    /// ve sebep <see cref="EncoderFallbackCause.NotInBuild"/> tasir.
    /// </summary>
    [Fact]
    public void KilitliKodlayiciDerlemedeYoksaNotInBuildIleDusuyor()
    {
        var availability = new FakeAvailability(("libx264", EncoderProbeState.Working));
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Auto, LockedCodec = "av1_nvenc" };

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, availability);
        var note = Assert.Single(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback);

        _output.WriteLine($"NotInBuild: codec={result.Plan.Codec} sebep={note.FallbackCause} istenen={note.RequestedCodec} dusulen={note.FallbackCodec}");
        Assert.Equal(EncoderFallbackCause.NotInBuild, note.FallbackCause);
        Assert.Equal("av1_nvenc", note.RequestedCodec);
        Assert.Equal("libsvtav1", note.FallbackCodec);
        Assert.Equal("libsvtav1", result.Plan.Codec);
        Assert.False(result.HardwareNotMeasured);
    }

    /// <summary>
    /// Aday derlemede var ama olculmedi: motor sessizce baska bir kodege kaymaz, kilitli
    /// kodlayiciyi <b>gecici</b> olarak kullanir ve <see cref="PlanResult.HardwareNotMeasured"/>
    /// ile bunu isaretler. Bu durumda henuz bir yedege dusme yok, cunku aday hakkinda
    /// "calismiyor" diye bir olcum yok — <see cref="EncoderFallbackCause.NotMeasured"/> ile
    /// <see cref="EncoderFallbackCause.NotWorking"/>'i karistirmamak bu ayrimdir.
    /// </summary>
    [Fact]
    public void KilitliKodlayiciOlculmediyseGeciciKendisiKullanilirVeYedegeDusmez()
    {
        var availability = new FakeAvailability(
            ("libx264", EncoderProbeState.Working),
            ("av1_nvenc", EncoderProbeState.Unmeasured));
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Auto, LockedCodec = "av1_nvenc" };

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, availability);

        _output.WriteLine($"NotMeasured: codec={result.Plan.Codec} codecNotMeasured={result.Plan.CodecNotMeasured} hardwareNotMeasured={result.HardwareNotMeasured}");
        Assert.Equal("av1_nvenc", result.Plan.Codec);
        Assert.True(result.Plan.CodecNotMeasured);
        Assert.True(result.HardwareNotMeasured);
        Assert.DoesNotContain(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback);
    }

    /// <summary>Aday olculdu ve calismiyor: yedege duser, sebep <c>NotWorking</c>'dir.</summary>
    [Fact]
    public void KilitliKodlayiciOlcupCalismiyorsaNotWorkingIleDusuyor()
    {
        var availability = new FakeAvailability(
            ("libx264", EncoderProbeState.Working),
            ("av1_nvenc", EncoderProbeState.NotWorking));
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.Auto, LockedCodec = "av1_nvenc" };

        var result = PlanCalculator.BuildDetailed(SampleInfo(), options, null, availability);
        var note = Assert.Single(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback);

        _output.WriteLine($"NotWorking: codec={result.Plan.Codec} sebep={note.FallbackCause} istenen={note.RequestedCodec} dusulen={note.FallbackCodec}");
        Assert.Equal(EncoderFallbackCause.NotWorking, note.FallbackCause);
        Assert.Equal("av1_nvenc", note.RequestedCodec);
        Assert.Equal("libsvtav1", note.FallbackCodec);
        Assert.Equal("libsvtav1", result.Plan.Codec);
        Assert.False(result.HardwareNotMeasured);
    }

    /// <summary>Uc sebep birbirine karismiyor: uc ayri <see cref="EncoderFallbackCause"/> uretiyor.</summary>
    [Fact]
    public void UcSebepBirbirindenFarkli()
    {
        var notInBuild = PlanCalculator.BuildDetailed(SampleInfo(),
            new PlanOptions { TargetMb = 25, LockedCodec = "av1_nvenc" }, null,
            new FakeAvailability(("libx264", EncoderProbeState.Working)));
        var notMeasured = PlanCalculator.BuildDetailed(SampleInfo(),
            new PlanOptions { TargetMb = 25, LockedCodec = "av1_nvenc" }, null,
            new FakeAvailability(("libx264", EncoderProbeState.Working), ("av1_nvenc", EncoderProbeState.Unmeasured)));
        var notWorking = PlanCalculator.BuildDetailed(SampleInfo(),
            new PlanOptions { TargetMb = 25, LockedCodec = "av1_nvenc" }, null,
            new FakeAvailability(("libx264", EncoderProbeState.Working), ("av1_nvenc", EncoderProbeState.NotWorking)));

        var sebepler = new[]
        {
            notInBuild.Plan.ReasonCodes.SingleOrDefault(n => n.Code == ReasonCode.EncoderFallback)?.FallbackCause,
            notMeasured.Plan.ReasonCodes.SingleOrDefault(n => n.Code == ReasonCode.EncoderFallback)?.FallbackCause,
            notWorking.Plan.ReasonCodes.SingleOrDefault(n => n.Code == ReasonCode.EncoderFallback)?.FallbackCause
        };

        _output.WriteLine($"NotInBuild -> {sebepler[0]?.ToString() ?? "yok (dusmedi)"}");
        _output.WriteLine($"Unmeasured -> {sebepler[1]?.ToString() ?? "yok (dusmedi)"}");
        _output.WriteLine($"NotWorking -> {sebepler[2]?.ToString() ?? "yok (dusmedi)"}");

        Assert.Equal(EncoderFallbackCause.NotInBuild, sebepler[0]);
        Assert.Null(sebepler[1]);
        Assert.Equal(EncoderFallbackCause.NotWorking, sebepler[2]);
    }

    // --- Kilit gecersiz ad ---

    [Fact]
    public void BilinmeyenKilitliKodlayiciPatliyor()
    {
        var options = new PlanOptions { TargetMb = 25, LockedCodec = "uydurma_kodek" };
        Assert.Throws<ArgumentException>(() => PlanCalculator.BuildDetailed(SampleInfo(), options, null, AllWorking()));
    }
}
