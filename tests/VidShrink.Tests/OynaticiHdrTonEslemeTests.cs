using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class TonKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-hdr-ton-esleme");
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

    internal static string Klip(string ad, string trc)
    {
        var path = Path.Combine(Folder, ad);
        if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
        Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli");
        var hdr = trc != "bt709";
        var prim = hdr ? "bt2020" : "bt709";
        var matrix = hdr ? "bt2020nc" : "bt709";
        var pix = hdr ? "yuv420p10le" : "yuv420p";
        var partial = Path.Combine(Folder, "part-" + ad);
        var args = new List<string>
        {
            "-y", "-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=25:duration=3", "-threads", "2",
            "-vf", $"format={pix},setparams=color_primaries={prim}:color_trc={trc}:colorspace={matrix}",
            "-c:v", hdr ? "libx265" : "libx264", "-preset", "ultrafast"
        };
        if (hdr) args.AddRange(new[] { "-x265-params", "pools=2:frame-threads=1:log-level=error" });
        args.AddRange(new[] { "-g", "5", "-pix_fmt", pix, "-color_primaries", prim, "-color_trc", trc, "-colorspace", matrix, "-f", "matroska", partial });
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, args.ToArray());
        Assert.True(kod == 0, $"ffmpeg {ad} uretemedi (kod {kod}): {hata[Math.Max(0, hata.Length - 600)..]}");
        File.Move(partial, path, true);
        return path;
    }
}

internal sealed class TonMotoru : IPlaybackEngine
{
    private readonly byte[] _piksel = Enumerable.Repeat((byte)128, 4 * 64 * 36).ToArray();

    internal List<bool> Yazilan { get; } = new();

    public string Name => "ton-motoru";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 600;

    public bool HasAudio => false;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached => false;

    public double PositionSeconds { get; private set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered { get; private set; }

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public bool ToneMapping { get; private set; } = true;

    public void SetToneMapping(bool on)
    {
        Yazilan.Add(on);
        ToneMapping = on;
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
/// Oynaticida HDR ton esleme: varsayilan acik, yalniz PQ ya da HLG aktarimli videoda mpv'nin
/// <c>gpu</c> suzgeci <c>@vstone</c> etiketiyle takilir; SDR'de takilmaz, kapaliyken kaldirilir.
/// Secim <c>player-settings.json</c>'a yazilir, panelden ve sifirlamadan degisir. GPU baglami
/// acilamayan kosucuda suzgec reddedilir (<c>ToneRefused</c>) ve oynatma bugunku yoldan surer.
/// </summary>
public sealed class OynaticiHdrTonEslemeTests
{
    private static string Gecmis(string ad)
    {
        var gecmis = Path.Combine(TonKanit.Folder, "gecmis-" + ad, "gecmis.json");
        Directory.CreateDirectory(Path.GetDirectoryName(gecmis)!);
        return gecmis;
    }

    private static string AyarDosyasi(string ad) => Path.Combine(Path.GetDirectoryName(Gecmis(ad))!, PlayerSettings.FileName);

    private static PlayerView Ac(TonMotoru motor, string ad, out Window pencere)
    {
        var gecmis = Gecmis(ad);
        var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => gecmis };
        pencere = new Window { Width = 640, Height = 360, Content = view };
        pencere.Show();
        var acilis = view.OpenAsync(TonKanit.Sahte(ad + ".mp4"));
        DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
        acilis.GetAwaiter().GetResult();
        DenetimSurucu.Wait(view, 0.2);
        return view;
    }

    private static void Kapat(PlayerView view, Window pencere, string ad)
    {
        view.Close();
        pencere.Close();
        Dispatcher.UIThread.RunJobs();
        TonKanit.Kapat(ad + ".mp4", "gecmis-" + ad);
    }

    [Theory]
    [InlineData(true, "pq", true)]
    [InlineData(true, "hlg", true)]
    [InlineData(true, " PQ ", true)]
    [InlineData(true, "HLG", true)]
    [InlineData(true, "bt.1886", false)]
    [InlineData(true, "srgb", false)]
    [InlineData(true, "linear", false)]
    [InlineData(true, "", false)]
    [InlineData(true, null, false)]
    [InlineData(false, "pq", false)]
    [InlineData(false, "hlg", false)]
    public void YalnizHdrAktarimindaVeAcikkenTakilir(bool acik, string? aktarim, bool beklenen)
    {
        Assert.Equal(beklenen, MpvEngine.NeedsToneMapping(acik, aktarim));
        Assert.Equal(beklenen || (!acik && aktarim is "pq" or "hlg"), MpvEngine.IsHdrTransfer(aktarim));
    }

    [Fact]
    public void HedefSdrEkranOlarakYazilir()
    {
        Assert.Equal(new[] { ("target-prim", "bt.709"), ("target-trc", "bt.1886") }, MpvEngine.ToneTarget);
        Assert.True(new PlayerSettings().ToneMapping);
    }

    [Fact]
    public void AyarGidisDonusuVeEskiDosyaAcikAcilir()
    {
        var klasor = Path.Combine(TonKanit.Folder, "ayar-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, PlayerSettings.FileName);
        try
        {
            new PlayerSettings { ToneMapping = false, Shuffle = true }.Save(dosya);
            Assert.Contains("\"toneMapping\": false", File.ReadAllText(dosya), StringComparison.Ordinal);
            var kapali = PlayerSettings.Load(dosya);
            Assert.False(kapali.ToneMapping);
            Assert.True(kapali.Shuffle);

            kapali.ToneMapping = true;
            kapali.Save(dosya);
            Assert.Contains("\"toneMapping\": true", File.ReadAllText(dosya), StringComparison.Ordinal);
            Assert.True(PlayerSettings.Load(dosya).ToneMapping);

            foreach (var eski in new[] { "{ \"shuffle\": true }", "{ \"shuffle\": true, \"toneMapping\": null }" })
            {
                File.WriteAllText(dosya, eski, new UTF8Encoding(false));
                var okunan = PlayerSettings.Load(dosya);
                Assert.True(okunan.ToneMapping);
                Assert.True(okunan.Shuffle);
            }
        }
        finally
        {
            TonKanit.Kapat(Path.GetFileName(klasor));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void KayitliKapaliSecimMotoraGiderAcikkenYazilmaz(bool acik)
    {
        AppHost.Run(() =>
        {
            var ad = acik ? "kayitli-acik" : "kayitli-kapali";
            new PlayerSettings { ToneMapping = acik }.Save(AyarDosyasi(ad));

            var motor = new TonMotoru();
            var view = Ac(motor, ad, out var pencere);

            Assert.Equal(acik, view.ToneMappingOn);
            Assert.Equal(acik ? Array.Empty<bool>() : new[] { false }, motor.Yazilan);
            Assert.Equal(acik, motor.ToneMapping);

            Kapat(view, pencere, ad);
            return "";
        });
    }

    [Fact]
    public void PaneldekiAnahtarKapatirSifirlamaAcar()
    {
        AppHost.Run(() =>
        {
            const string ad = "panel";
            File.Delete(AyarDosyasi(ad));
            var motor = new TonMotoru();
            var view = Ac(motor, ad, out var pencere);
            var panel = new PlayerAdvancedPanel { Player = view };
            var panelPenceresi = new Window { Width = 480, Height = 480, Content = panel };
            panelPenceresi.Show();
            Dispatcher.UIThread.RunJobs();

            CheckBox Kutu() => Assert.Single(
                panel.FindControl<StackPanel>("Switches")!.Children.OfType<CheckBox>(),
                kutu => (kutu.Content as string) == Strings.Get("player.advanced.tone-mapping"));

            Assert.True(view.ToneMappingOn);
            Assert.True(Kutu().IsChecked);
            Assert.Empty(motor.Yazilan);

            Kutu().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.ToneMappingOn);
            Assert.False(Kutu().IsChecked);
            Assert.Equal(new[] { false }, motor.Yazilan);
            Assert.Equal("tonemap -> off", view.Trace[^1]);
            Assert.False(PlayerSettings.Load(AyarDosyasi(ad)).ToneMapping);

            view.ResetAdvanced();
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.ToneMappingOn);
            Assert.True(Kutu().IsChecked);
            Assert.Equal(new[] { false, true }, motor.Yazilan);
            Assert.True(PlayerSettings.Load(AyarDosyasi(ad)).ToneMapping);

            view.ResetAdvanced();
            Assert.Equal(2, motor.Yazilan.Count);

            panelPenceresi.Close();
            Kapat(view, pencere, ad);
            return "";
        });
    }

    [Fact]
    public void AnahtarButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var degerler = Locales.Domain(dil, "advanced");
            Assert.True(degerler.TryGetValue("player.advanced.tone-mapping", out var deger) && !string.IsNullOrWhiteSpace(deger), dil);
            Assert.NotEqual(degerler["player.advanced.hardware"], deger);
        }

        Assert.Equal("HDR Ton Eşleme", Locales.Domain("tr", "advanced")["player.advanced.tone-mapping"]);
        Assert.Equal("HDR Tone Mapping", Locales.Domain("en", "advanced")["player.advanced.tone-mapping"]);
    }

    [Fact]
    public void GercekMotordaHdrKlipteHalkaTakilirKapaninceKalkarSdrdeYok()
    {
        var pq = TonKanit.Klip("pq-160x90.mkv", "smpte2084");
        var hlg = TonKanit.Klip("hlg-160x90.mkv", "arib-std-b67");
        var sdr = TonKanit.Klip("sdr-160x90.mkv", "bt709");

        var rapor = AppHost.Run(() =>
        {
            var kayit = new StringBuilder();
            foreach (var (klip, ad) in new[] { (pq, "pq"), (hlg, "hlg") })
            {
                var o = KisayolOrtam.Ac(klip, "ton-" + ad);
                o.Bekle(() => o.Oku("video-params/gamma") != "", 3);
                Assert.Equal(ad, o.Oku("video-params/gamma"));
                Assert.True(o.Motor.ToneMapping);
                Assert.True(o.View.ToneMappingOn);
                o.Bekle(() => o.Oku("vf").Contains("@vstone:gpu", StringComparison.Ordinal) || o.Motor.ToneRefused, 3);
                var takili = o.Oku("vf").Contains("@vstone:gpu", StringComparison.Ordinal);
                Assert.True(takili || o.Motor.ToneRefused, $"{ad}: vf '{o.Oku("vf")}'");
                if (takili) Assert.Equal("bt.709", o.Oku("target-prim"));
                if (takili) Assert.Equal("bt.1886", o.Oku("target-trc"));
                o.Git(1.5);
                Assert.InRange(o.Sayi("time-pos"), 1.25, 1.75);
                kayit.AppendLine($"{ad} acik: takili {takili} reddedildi {o.Motor.ToneRefused} etkin {o.Motor.ToneMappingActive} cikis {o.Oku("video-out-params/gamma")}");

                o.View.ToggleToneMapping();
                o.Bekle(() => !o.Oku("vf").Contains("@vstone", StringComparison.Ordinal), 3);
                Assert.DoesNotContain("@vstone", o.Oku("vf"), StringComparison.Ordinal);
                Assert.False(o.Motor.ToneMapping);
                Assert.False(o.Motor.ToneMappingActive);
                Assert.False(o.Motor.ToneRefused);
                var once = o.Motor.FramesRendered;
                o.Git(0.5);
                Assert.InRange(o.Sayi("time-pos"), 0.25, 0.75);
                Assert.True(o.Motor.FramesRendered > once);

                o.View.ToggleToneMapping();
                o.Bekle(() => o.Oku("vf").Contains("@vstone:gpu", StringComparison.Ordinal) || o.Motor.ToneRefused, 3);
                Assert.True(o.Oku("vf").Contains("@vstone:gpu", StringComparison.Ordinal) || o.Motor.ToneRefused);
                o.Kapat();
            }

            var s = KisayolOrtam.Ac(sdr, "ton-sdr");
            s.Bekle(() => s.Oku("video-params/gamma") != "", 3);
            Assert.Equal("bt.1886", s.Oku("video-params/gamma"));
            Assert.True(s.Motor.ToneMapping);
            s.Bekle(0.3);
            Assert.DoesNotContain("@vstone", s.Oku("vf"), StringComparison.Ordinal);
            Assert.False(s.Motor.ToneMappingActive);
            Assert.False(s.Motor.ToneRefused);
            s.Kapat();
            return kayit.ToString();
        });

        Assert.Contains("pq acik", rapor);
        Assert.Contains("hlg acik", rapor);
        TonKanit.Kapat("pq-160x90.mkv", "hlg-160x90.mkv", "sdr-160x90.mkv");
    }

    [Fact]
    public void VulkanAygitiYokkenHalkaTakilmazOynatmaSurer()
    {
        var pq = TonKanit.Klip("pq-novk-160x90.mkv", "smpte2084");
        VulkanProbe.Override = () => false;
        try
        {
            AppHost.Run(() =>
            {
                var o = KisayolOrtam.Ac(pq, "ton-novk");
                o.Bekle(() => o.Oku("video-params/gamma") != "", 3);
                Assert.Equal("pq", o.Oku("video-params/gamma"));
                o.Bekle(() => o.Motor.ToneRefused, 3);
                Assert.True(o.Motor.ToneRefused);
                Assert.DoesNotContain("@vstone", o.Oku("vf"), StringComparison.Ordinal);
                Assert.False(o.Motor.ToneMappingActive);
                var once = o.Motor.FramesRendered;
                o.Git(1.0);
                Assert.InRange(o.Sayi("time-pos"), 0.75, 1.25);
                Assert.True(o.Motor.FramesRendered > once);
                o.Kapat();
                return 0;
            });
        }
        finally
        {
            VulkanProbe.Override = null;
        }

        TonKanit.Kapat("pq-novk-160x90.mkv");
    }
}
