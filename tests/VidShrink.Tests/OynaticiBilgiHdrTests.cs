using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class BilgiHdrKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-a74f7ed3d2eeddfb0");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Folder, adlar);

    internal static string Sahte(string ad)
    {
        var path = Path.Combine(Folder, ad);
        File.WriteAllBytes(path, new byte[16]);
        return path;
    }

    private static readonly string[][] HdrKodlayicilar =
    {
        new[] { "-c:v", "libx265", "-preset", "ultrafast", "-x265-params", "pools=2:frame-threads=1:log-level=error" },
        new[] { "-c:v", "libsvtav1", "-preset", "12", "-svtav1-params", "lp=2" },
        new[] { "-c:v", "libx264", "-preset", "ultrafast" }
    };

    internal static string Sdr(string ad)
    {
        var path = Path.Combine(Folder, ad);
        File.Delete(path);
        Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli");
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, new[]
        {
            "-y", "-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=25:duration=3",
            "-threads", "2", "-vf", "format=yuv420p,setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "5", "-pix_fmt", "yuv420p",
            "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709", path
        });
        Assert.True(kod == 0, $"ffmpeg {ad} uretemedi (kod {kod}): {hata[Math.Max(0, hata.Length - 600)..]}");
        return path;
    }

    internal static (string Yol, string Kodlayici) Hdr(string ad)
    {
        var path = Path.Combine(Folder, ad);
        Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli");
        var gunluk = new List<string>();
        foreach (var kodlayici in HdrKodlayicilar)
        {
            File.Delete(path);
            var args = new List<string>
            {
                "-y", "-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=25:duration=3", "-threads", "2",
                "-vf", "format=yuv420p10le,setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc"
            };
            args.AddRange(kodlayici);
            args.AddRange(new[]
            {
                "-g", "5", "-pix_fmt", "yuv420p10le",
                "-color_primaries", "bt2020", "-color_trc", "smpte2084", "-colorspace", "bt2020nc", path
            });
            var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, args.ToArray());
            if (kod == 0 && File.Exists(path) && new FileInfo(path).Length > 0) return (path, kodlayici[1]);
            gunluk.Add($"{kodlayici[1]}: kod {kod} {hata[Math.Max(0, hata.Length - 200)..]}");
        }

        Assert.Fail("10 bit PQ etiketli klip hicbir kodlayiciyla uretilemedi: " + string.Join(" | ", gunluk));
        return ("", "");
    }
}

internal sealed class BilgiMotoru : IPlaybackEngine
{
    private readonly byte[] _piksel = Enumerable.Repeat((byte)128, 4 * 64 * 36).ToArray();

    internal int StatsOkuma { get; private set; }

    internal int DetailsOkuma { get; private set; }

    internal long Dusen { get; set; }

    internal MediaDetails Kimlik { get; set; } = new("hevc", 3840, 2160, 60, 20_000_000, "eac3", 6, 48_000, "bt.2020", "pq", 10);

    public string Name => "bilgi-motoru";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 600;

    public bool HasAudio => false;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached => false;

    public double PositionSeconds { get; private set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered { get; private set; }

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public HardwareDecoding Hardware { get; private set; }

    public void SetHardwareDecoding(HardwareDecoding mode) => Hardware = mode;

    public MediaDetails? Details
    {
        get
        {
            DetailsOkuma++;
            return Kimlik;
        }
    }

    public PlaybackStats? Stats
    {
        get
        {
            StatsOkuma++;
            return new PlaybackStats(Dusen, 0, Hardware == HardwareDecoding.AutoCopy ? "d3d11va-copy" : "no", 950_400, 59.94);
        }
    }

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        IsOpen = true;
        FramesRendered = 1;
        return Task.CompletedTask;
    }

    public void Play() => IsPaused = false;

    public void Pause() => IsPaused = true;

    public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        PositionSeconds = seconds;
        FramesRendered++;
        return Task.FromResult(new SeekResult(SeekOutcome.Shown, 1));
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy)
    {
        if (seen == FramesRendered) return false;
        seen = FramesRendered;
        var handle = GCHandle.Alloc(_piksel, GCHandleType.Pinned);
        try
        {
            copy(handle.AddrOfPinnedObject(), 64, 36, 64 * 4);
        }
        finally
        {
            handle.Free();
        }

        return true;
    }

    public void Dispose() => IsOpen = false;
}

/// <summary>
/// Oynatici bilgi panelinin HDR alani ve canli istatistigi: dinamik aralik ve renk satiri motorun
/// <c>video-params</c> okumasindan, dusen kare, cozucu, anlik bit hizi ve gosterilen kare hizi <c>Stats</c>'tan.
/// Canli satirlar gozetleyicinin 500 ms'lik vurusuyla yenilenir; panel kapaliyken motor hic okunmaz.
/// Beklenen metinler elle yazili; gercek libmpv kolu ffmpeg'in urettigi SDR ve 10 bit PQ etiketli klibi acar.
/// </summary>
public sealed class OynaticiBilgiHdrTests
{
    private static readonly string[] Anahtarlar =
    {
        "player.info.range", "player.info.color", "player.info.decoder", "player.info.software", "player.info.hardware",
        "player.info.dropped", "player.info.decoderdropped", "player.info.livebitrate", "player.info.livefps"
    };

    private static string Gecmis(string ad)
    {
        var gecmis = Path.Combine(BilgiHdrKanit.Folder, "gecmis-" + ad, "gecmis.json");
        Directory.CreateDirectory(Path.GetDirectoryName(gecmis)!);
        return gecmis;
    }

    private static PlayerView Ac(BilgiMotoru motor, string ad, out Window pencere)
    {
        var gecmis = Gecmis(ad);
        File.Delete(Path.Combine(Path.GetDirectoryName(gecmis)!, PlayerSettings.FileName));
        var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => gecmis };
        pencere = new Window { Width = 640, Height = 360, Content = view };
        pencere.Show();
        var acilis = view.OpenAsync(BilgiHdrKanit.Sahte(ad + ".mp4"));
        DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
        acilis.GetAwaiter().GetResult();
        DenetimSurucu.Wait(view, 0.2);
        return view;
    }

    private static void Kapat(PlayerView view, Window pencere)
    {
        view.Close();
        pencere.Close();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Turkce<T>(Func<T> govde)
        => AppHost.Run(() =>
        {
            var onceki = Strings.Language;
            Strings.Use("tr");
            try
            {
                return govde();
            }
            finally
            {
                Strings.Use(onceki);
            }
        });

    private static void Paneli(PlayerView view) => view.Apply(new PlayerCommand(PlayerCommandKind.ToggleInfo, 0));

    [Theory]
    [InlineData("pq", 0, "HDR10")]
    [InlineData("PQ", 0, "HDR10")]
    [InlineData("hlg", 0, "HLG")]
    [InlineData("bt.1886", 0, "SDR")]
    [InlineData("srgb", 0, "SDR")]
    [InlineData("pq", 5, "Dolby Vision")]
    [InlineData("bt.1886", 8, "Dolby Vision")]
    [InlineData(null, 0, null)]
    [InlineData("", 0, null)]
    public void AktarimEgrisindenDinamikAralik(string? aktarim, int profil, string? beklenen)
    {
        var kimlik = new MediaDetails("hevc", 1920, 1080, 30, 1_000_000, null, 0, 0, "bt.2020", aktarim, 10, profil);
        Assert.Equal(beklenen, kimlik.DynamicRange);
    }

    [Theory]
    [InlineData("yuv420p", 8)]
    [InlineData("yuvj420p", 8)]
    [InlineData("yuva420p", 8)]
    [InlineData("nv12", 8)]
    [InlineData("gbrp", 8)]
    [InlineData("yuv420p10", 10)]
    [InlineData("yuv420p10le", 10)]
    [InlineData("YUV422P10BE", 10)]
    [InlineData("yuv444p12", 12)]
    [InlineData("gbrp10", 10)]
    [InlineData("p010", 10)]
    [InlineData("p010le", 10)]
    [InlineData("p016", 16)]
    [InlineData("yuv420p99", 0)]
    [InlineData("d3d11", 0)]
    [InlineData("pal8", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void PikselBicimindenBitDerinligi(string? bicim, int beklenen)
        => Assert.Equal(beklenen, MediaDetails.BitDepthOf(bicim));

    [Theory]
    [InlineData("bt.2020", "BT.2020")]
    [InlineData("bt.709", "BT.709")]
    [InlineData("bt.601-625", "BT.601-625")]
    [InlineData("pq", "PQ")]
    [InlineData("hlg", "HLG")]
    [InlineData("srgb", "sRGB")]
    [InlineData("dci-p3", "DCI-P3")]
    [InlineData("gamma2.2", "gamma2.2")]
    [InlineData("unknown", null)]
    [InlineData("auto", null)]
    [InlineData(" ", null)]
    [InlineData(null, null)]
    public void RenkAdlariOkunurYazimaCevrilir(string? ham, string? beklenen)
        => Assert.Equal(beklenen, MediaDetails.ColorName(ham));

    [Fact]
    public void HdrVeSdrDosyadaAralikVeRenkSatiri()
    {
        var metin = Turkce(() => (
            hdr: PlayerView.Describe(new MediaDetails("hevc", 3840, 2160, 60, 20_000_000, "eac3", 6, 48_000, "bt.2020", "pq", 10)),
            hlg: PlayerView.Describe(new MediaDetails("hevc", 3840, 2160, 50, 20_000_000, null, 0, 0, "bt.2020", "hlg", 10)),
            dv: PlayerView.Describe(new MediaDetails("hevc", 3840, 2160, 24, 20_000_000, null, 0, 0, "bt.2020", "pq", 10, 8)),
            sdr: PlayerView.Describe(new MediaDetails("h264", 1920, 1080, 30, 1_500_000, "aac", 2, 48_000, "bt.709", "bt.1886", 8)),
            yarim: PlayerView.Describe(new MediaDetails("h264", 1920, 1080, 30, 1_500_000, "aac", 2, 48_000, null, "bt.1886")),
            eski: PlayerView.Describe(new MediaDetails("h264", 1920, 1080, 30, 1_500_000, "aac", 2, 48_000))));

        var hdr = metin.hdr.Split(Environment.NewLine);
        Assert.Equal(7, hdr.Length);
        Assert.Equal("Dinamik aralık: HDR10", hdr[4]);
        Assert.Equal("Renk: BT.2020, PQ, 10 bit", hdr[5]);
        Assert.StartsWith("Bit hızı: ", hdr[3], StringComparison.Ordinal);
        Assert.StartsWith("Ses: ", hdr[6], StringComparison.Ordinal);

        Assert.Contains("Dinamik aralık: HLG", metin.hlg);
        Assert.Contains("Renk: BT.2020, HLG, 10 bit", metin.hlg);
        Assert.Contains("Dinamik aralık: Dolby Vision", metin.dv);

        Assert.Contains("Dinamik aralık: SDR", metin.sdr);
        Assert.Contains("Renk: BT.709, BT.1886, 8 bit", metin.sdr);
        Assert.DoesNotContain("HDR", metin.sdr);

        Assert.Contains("Renk: BT.1886" + Environment.NewLine, metin.yarim);

        Assert.Contains("Dinamik aralık: bilinmiyor", metin.eski);
        Assert.Contains("Renk: bilinmiyor", metin.eski);
        Assert.DoesNotContain("SDR", metin.eski);
        Assert.DoesNotContain("Çözücü", metin.eski);
        Assert.DoesNotContain("Düşen kare", metin.eski);
    }

    [Fact]
    public void CanliSatirlarOlcumuYazarBilinmeyeniSoyler()
    {
        var kimlik = new MediaDetails("h264", 1920, 1080, 30, 1_500_000, "aac", 2, 48_000, "bt.709", "bt.1886", 8);
        var metin = Turkce(() => (
            donanim: PlayerView.Describe(kimlik, new PlaybackStats(3, 1, "d3d11va-copy", 950_400, 59.94)),
            yazilim: PlayerView.Describe(kimlik, new PlaybackStats(0, 0, "no", 2_000_600, 23.976)),
            bos: PlayerView.Describe(kimlik, new PlaybackStats(-1, -1, null, double.NaN, 0)),
            motorsuz: PlayerView.Describe(null, new PlaybackStats(3, 1, "no", 950_400, 59.94))));

        var donanim = metin.donanim.Split(Environment.NewLine);
        Assert.Equal(12, donanim.Length);
        Assert.Equal("Çözücü: donanım (d3d11va-copy)", donanim[7]);
        Assert.Equal("Düşen kare: 3", donanim[8]);
        Assert.Equal("Çözücüde düşen kare: 1", donanim[9]);
        Assert.Equal("Anlık bit hızı: 950 kbit/sn", donanim[10]);
        Assert.Equal("Gösterilen kare hızı: 59,94 fps", donanim[11]);

        Assert.Contains("Çözücü: yazılım", metin.yazilim);
        Assert.DoesNotContain("donanım", metin.yazilim);
        Assert.Contains("Düşen kare: 0", metin.yazilim);
        Assert.Contains("Anlık bit hızı: 2001 kbit/sn", metin.yazilim);
        Assert.Contains("Gösterilen kare hızı: 23,98 fps", metin.yazilim);

        Assert.Contains("Çözücü: bilinmiyor", metin.bos);
        Assert.Contains("Düşen kare: 0", metin.bos);
        Assert.Contains("Çözücüde düşen kare: 0", metin.bos);
        Assert.Contains("Anlık bit hızı: bilinmiyor", metin.bos);
        Assert.Contains("Gösterilen kare hızı: bilinmiyor", metin.bos);

        Assert.DoesNotContain("Çözücü", metin.motorsuz);
    }

    [Fact]
    public void PanelKapaliykenMotorOkunmazAcikkenVurusIstatistigiYeniler()
    {
        const string ad = "ritim";
        try
        {
            AppHost.Run(() =>
            {
                var motor = new BilgiMotoru();
                var view = Ac(motor, ad, out var pencere);

                Assert.False(view.InfoVisible);
                for (var i = 0; i < 3; i++) view.OnWatchdog();
                Assert.Equal(0, motor.StatsOkuma);
                Assert.Equal(0, motor.DetailsOkuma);

                Paneli(view);
                Assert.True(view.InfoVisible);
                Assert.True(motor.StatsOkuma >= 1);
                Assert.Contains(Strings.Get("player.info.dropped", "0"), view.InfoText);
                Assert.Contains(Strings.Get("player.info.range", "HDR10"), view.InfoText);
                Assert.Contains("BT.2020, PQ, 10 bit", view.InfoText);

                motor.Dusen = 7;
                var once = motor.StatsOkuma;
                view.OnWatchdog();
                Assert.True(motor.StatsOkuma > once);
                Assert.Contains(Strings.Get("player.info.dropped", "7"), view.InfoText);
                Assert.DoesNotContain(Strings.Get("player.info.dropped", "0"), view.InfoText);

                motor.Kimlik = motor.Kimlik with { Transfer = "hlg" };
                view.OnWatchdog();
                Assert.Contains(Strings.Get("player.info.range", "HLG"), view.InfoText);

                Paneli(view);
                Assert.False(view.InfoVisible);
                var kapanista = (motor.StatsOkuma, motor.DetailsOkuma);
                motor.Dusen = 9;
                for (var i = 0; i < 3; i++) view.OnWatchdog();
                Assert.Equal(kapanista, (motor.StatsOkuma, motor.DetailsOkuma));
                Assert.DoesNotContain(Strings.Get("player.info.dropped", "9"), view.InfoText);

                Kapat(view, pencere);
                return "";
            });
        }
        finally
        {
            BilgiHdrKanit.Kapat(ad + ".mp4", "gecmis-" + ad);
        }
    }

    [Fact]
    public void CozucuSatiriDonanimCozmeAnahtariniIzler()
    {
        const string ad = "cozucu";
        try
        {
            AppHost.Run(() =>
            {
                var motor = new BilgiMotoru();
                var view = Ac(motor, ad, out var pencere);
                var yazilim = Strings.Get("player.info.decoder", Strings.Get("player.info.software"));
                var donanim = Strings.Get("player.info.decoder", Strings.Get("player.info.hardware", "d3d11va-copy"));

                Assert.False(view.HardwareDecodingOn);
                Paneli(view);
                Assert.Contains(yazilim, view.InfoText);
                Assert.DoesNotContain("d3d11va-copy", view.InfoText);

                view.ToggleHardwareDecoding();
                view.OnWatchdog();
                Assert.True(view.HardwareDecodingOn);
                Assert.Contains(donanim, view.InfoText);
                Assert.DoesNotContain(yazilim, view.InfoText);

                view.ToggleHardwareDecoding();
                view.OnWatchdog();
                Assert.Contains(yazilim, view.InfoText);

                Kapat(view, pencere);
                return "";
            });
        }
        finally
        {
            BilgiHdrKanit.Kapat(ad + ".mp4", "gecmis-" + ad);
        }
    }

    [Fact]
    public void MotorOlcmuyorsaCanliSatirYok()
    {
        const string ad = "olcmeyen";
        try
        {
            AppHost.Run(() =>
            {
                var motor = new DonanimMotoru();
                var gecmis = Gecmis(ad);
                var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => gecmis };
                var pencere = new Window { Width = 640, Height = 360, Content = view };
                pencere.Show();
                var acilis = view.OpenAsync(BilgiHdrKanit.Sahte(ad + ".mp4"));
                DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
                acilis.GetAwaiter().GetResult();

                Assert.Null(((IPlaybackEngine)motor).Stats);
                Paneli(view);
                view.OnWatchdog();
                Assert.Equal(Strings.Get("player.info.none"), view.InfoText);

                Kapat(view, pencere);
                return "";
            });
        }
        finally
        {
            BilgiHdrKanit.Kapat(ad + ".mp4", "gecmis-" + ad);
        }
    }

    [Fact]
    public void AnahtarlarButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var degerler = Locales.Domain(dil, "playback");
            foreach (var anahtar in Anahtarlar)
            {
                Assert.True(degerler.TryGetValue(anahtar, out var deger) && !string.IsNullOrWhiteSpace(deger), $"{dil} {anahtar}");
                Assert.Equal(anahtar != "player.info.software", deger!.Contains("{0}", StringComparison.Ordinal));
                Assert.DoesNotContain("{1}", deger, StringComparison.Ordinal);
            }

            Assert.Equal(Anahtarlar.Length, Anahtarlar.Select(anahtar => degerler[anahtar]).Distinct(StringComparer.Ordinal).Count());
            Assert.NotEqual(degerler["player.info.bitrate"], degerler["player.info.livebitrate"]);
            Assert.NotEqual(degerler["player.info.framerate"], degerler["player.info.livefps"]);
            var kare = degerler["player.info.framerate"];
            Assert.EndsWith(kare[kare.IndexOf("{0}", StringComparison.Ordinal)..], degerler["player.info.livefps"], StringComparison.Ordinal);
        }

        var tr = Locales.Domain("tr", "playback");
        Assert.Equal("Dinamik aralık: {0}", tr["player.info.range"]);
        Assert.Equal("Çözücü: {0}", tr["player.info.decoder"]);
        Assert.Equal("donanım ({0})", tr["player.info.hardware"]);
        var en = Locales.Domain("en", "playback");
        Assert.Equal("Dynamic range: {0}", en["player.info.range"]);
        Assert.Equal("Dropped frames: {0}", en["player.info.dropped"]);
        Assert.Equal("software", en["player.info.software"]);
    }

    [Fact]
    public void GercekMotordaSdrKlipSdrVeYazilimCozucuOkunur()
    {
        const string ad = "canli-sdr.mkv";
        const string ortam = "bilgi-hdr-sdr";
        try
        {
            var klip = BilgiHdrKanit.Sdr(ad);
            var rapor = AppHost.Run(() =>
            {
                var o = KisayolOrtam.Ac(klip, ortam);
                o.Bekle(() => !string.IsNullOrEmpty(o.Motor.Details?.Transfer), 5);
                var kimlik = o.Motor.Details!;
                Paneli(o.View);
                var durgun = o.View.InfoText;
                var olcum = o.Motor.Stats!;

                o.View.Apply(new PlayerCommand(PlayerCommandKind.TogglePlay, 0));
                var hiz = 0.0;
                var bilinmeyen = Strings.Get("player.info.livefps", Strings.Get("player.info.unknown"));
                o.Bekle(() =>
                {
                    var anlik = o.Motor.Stats?.DisplayedFramesPerSecond ?? 0;
                    if (double.IsFinite(anlik) && anlik > hiz) hiz = anlik;
                    o.View.OnWatchdog();
                    return hiz > 0 && !o.View.InfoText.Contains(bilinmeyen, StringComparison.Ordinal);
                }, 6);
                var oynarken = o.View.InfoText;
                o.Not($"sdr: {kimlik} | {olcum} | hiz {hiz:0.##} | bit {o.Sayi("video-bitrate"):0}");
                var kayit = o.Kayit.ToString();
                o.Kapat();
                return (kimlik, durgun, olcum, hiz, oynarken, kayit);
            });

            Assert.Equal("h264", rapor.kimlik.VideoCodec);
            Assert.Equal("bt.709", rapor.kimlik.Primaries);
            Assert.Equal("bt.1886", rapor.kimlik.Transfer);
            Assert.Equal(8, rapor.kimlik.BitDepth);
            Assert.Equal(0, rapor.kimlik.DolbyVisionProfile);
            Assert.Equal("SDR", rapor.kimlik.DynamicRange);
            Assert.Contains(Strings.Get("player.info.range", "SDR"), rapor.durgun);
            Assert.Contains(Strings.Get("player.info.color", "BT.709, BT.1886, 8 bit"), rapor.durgun);
            Assert.Equal("no", rapor.olcum.Decoder);
            Assert.Contains(Strings.Get("player.info.decoder", Strings.Get("player.info.software")), rapor.durgun);
            Assert.InRange(rapor.olcum.DroppedFrames, 0, 75);
            Assert.True(rapor.hiz is > 5 and < 100, rapor.kayit);
            Assert.DoesNotContain(Strings.Get("player.info.livefps", Strings.Get("player.info.unknown")), rapor.oynarken);
        }
        finally
        {
            BilgiHdrKanit.Kapat(ad);
            KisayolKanit.Kapat(Path.Combine("gecmis", ortam));
        }
    }

    [Fact]
    public void GercekMotordaPqEtiketliOnBitKlipHdrOkunur()
    {
        const string ad = "canli-hdr.mkv";
        const string ortam = "bilgi-hdr-pq";
        try
        {
            var (klip, kodlayici) = BilgiHdrKanit.Hdr(ad);
            var rapor = AppHost.Run(() =>
            {
                var o = KisayolOrtam.Ac(klip, ortam);
                o.Bekle(() => !string.IsNullOrEmpty(o.Motor.Details?.Transfer), 5);
                var kimlik = o.Motor.Details!;
                Paneli(o.View);
                var metin = o.View.InfoText;
                var tepe = o.Oku("video-params/sig-peak");
                o.Kapat();
                return (kimlik, metin, tepe);
            });

            var iz = $"{kodlayici}: {rapor.kimlik} sig-peak {rapor.tepe}";
            Assert.True(rapor.kimlik.Transfer == "pq", iz);
            Assert.True(rapor.kimlik.Primaries == "bt.2020", iz);
            Assert.True(rapor.kimlik.BitDepth == 10, iz);
            Assert.Equal("HDR10", rapor.kimlik.DynamicRange);
            Assert.Contains(Strings.Get("player.info.range", "HDR10"), rapor.metin);
            Assert.Contains(Strings.Get("player.info.color", "BT.2020, PQ, 10 bit"), rapor.metin);
            Assert.DoesNotContain("SDR", rapor.metin);
        }
        finally
        {
            BilgiHdrKanit.Kapat(ad);
            KisayolKanit.Kapat(Path.Combine("gecmis", ortam));
        }
    }
}
