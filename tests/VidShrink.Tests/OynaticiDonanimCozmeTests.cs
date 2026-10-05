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

internal static class DonanimKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-donanim-cozme");
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

    internal static string Klip
    {
        get
        {
            const string ad = "klip-160x90.mkv";
            var path = Path.Combine(Folder, ad);
            if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
            Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli");
            var partial = Path.Combine(Folder, "part-" + ad);
            var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, new[]
            {
                "-y", "-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=10:duration=3",
                "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-g", "5", "-pix_fmt", "yuv420p", partial
            });
            Assert.True(kod == 0, $"ffmpeg {ad} uretemedi (kod {kod}): {hata[Math.Max(0, hata.Length - 600)..]}");
            File.Move(partial, path, true);
            return path;
        }
    }
}

internal sealed class DonanimMotoru : IPlaybackEngine
{
    private readonly byte[] _piksel = Enumerable.Repeat((byte)128, 4 * 64 * 36).ToArray();

    internal List<HardwareDecoding> Yazilan { get; } = new();

    public string Name => "donanim-motoru";

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

    public void SetHardwareDecoding(HardwareDecoding mode)
    {
        Yazilan.Add(mode);
        Hardware = mode;
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
/// Oynaticida donanim cozme anahtari: secim <c>player-settings.json</c>'a yazilir, sahte motora
/// dogru kiple gider, kapaliyken gitmez; gercek libmpv'de <c>hwdec</c> ozelligi geri okunur.
/// <c>hwdec-current</c> pimlenmez, GPU'suz kosucuda <c>no</c> kalir. Varsayilan kapali.
/// </summary>
public sealed class OynaticiDonanimCozmeTests
{
    private static string Gecmis(string ad)
    {
        var gecmis = Path.Combine(DonanimKanit.Folder, "gecmis-" + ad, "gecmis.json");
        Directory.CreateDirectory(Path.GetDirectoryName(gecmis)!);
        return gecmis;
    }

    private static string AyarDosyasi(string ad) => Path.Combine(Path.GetDirectoryName(Gecmis(ad))!, PlayerSettings.FileName);

    private static PlayerView Ac(DonanimMotoru motor, string ad, out Window pencere)
    {
        var gecmis = Gecmis(ad);
        var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => gecmis };
        pencere = new Window { Width = 640, Height = 360, Content = view };
        pencere.Show();
        var acilis = view.OpenAsync(DonanimKanit.Sahte(ad + ".mp4"));
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
        DonanimKanit.Kapat(ad + ".mp4", "gecmis-" + ad);
    }

    [Fact]
    public void MotorSecenegiKopyaliKipVeKapaliykenNo()
    {
        Assert.Equal("auto-copy", MpvEngine.HardwareValue(HardwareDecoding.AutoCopy));
        Assert.Equal("no", MpvEngine.HardwareValue(HardwareDecoding.Off));
        Assert.Contains(("hwdec", "no"), MpvEngine.OptionsFor(new PlaybackOptions()));
        Assert.Contains(("hwdec", "auto-copy"), MpvEngine.OptionsFor(new PlaybackOptions { Hardware = HardwareDecoding.AutoCopy }));
        Assert.Equal(HardwareDecoding.Off, new PlaybackOptions().Hardware);
        Assert.Equal(HardwareDecoding.Off, new PlayerSettings().Hardware);
    }

    [Fact]
    public void CalarkenDegisenSecimMotoraYenidenYazilirVeDiskeGider()
    {
        AppHost.Run(() =>
        {
            const string ad = "calarken";
            File.Delete(AyarDosyasi(ad));
            var motor = new DonanimMotoru();
            var view = Ac(motor, ad, out var pencere);
            var ayar = AyarDosyasi(ad);

            Assert.False(view.HardwareDecodingOn);
            Assert.Empty(motor.Yazilan);
            Assert.False(File.Exists(ayar));

            view.ToggleHardwareDecoding();
            Assert.True(view.HardwareDecodingOn);
            Assert.Equal(new[] { HardwareDecoding.AutoCopy }, motor.Yazilan);
            Assert.Equal("hwdec -> auto-copy", view.Trace[^1]);
            Assert.Equal(HardwareDecoding.AutoCopy, PlayerSettings.Load(ayar).Hardware);

            view.ToggleHardwareDecoding();
            Assert.False(view.HardwareDecodingOn);
            Assert.Equal(new[] { HardwareDecoding.AutoCopy, HardwareDecoding.Off }, motor.Yazilan);
            Assert.Equal("hwdec -> no", view.Trace[^1]);
            Assert.Equal(HardwareDecoding.Off, PlayerSettings.Load(ayar).Hardware);

            Kapat(view, pencere, ad);
            return "";
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void KayitliSecimYeniDosyadaMotoraGiderKapaliysaGitmez(bool acik)
    {
        AppHost.Run(() =>
        {
            var ad = acik ? "kayitli-acik" : "kayitli-kapali";
            new PlayerSettings { Hardware = acik ? HardwareDecoding.AutoCopy : HardwareDecoding.Off }.Save(AyarDosyasi(ad));

            var motor = new DonanimMotoru();
            var view = Ac(motor, ad, out var pencere);

            Assert.Equal(acik, view.HardwareDecodingOn);
            Assert.Equal(acik ? new[] { HardwareDecoding.AutoCopy } : Array.Empty<HardwareDecoding>(), motor.Yazilan);
            Assert.Equal(acik ? HardwareDecoding.AutoCopy : HardwareDecoding.Off, motor.Hardware);

            Kapat(view, pencere, ad);
            return "";
        });
    }

    [Fact]
    public void AyarGidisDonusuVeEskiDosyaKapaliAcilir()
    {
        var klasor = Path.Combine(DonanimKanit.Folder, "ayar-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, PlayerSettings.FileName);
        try
        {
            AyarGidisDonusu(dosya);
        }
        finally
        {
            DonanimKanit.Kapat(Path.GetFileName(klasor));
        }
    }

    private static void AyarGidisDonusu(string dosya)
    {
        new PlayerSettings { Hardware = HardwareDecoding.AutoCopy, Shuffle = true }.Save(dosya);
        Assert.Contains("\"hardwareDecoding\": \"AutoCopy\"", File.ReadAllText(dosya), StringComparison.Ordinal);
        var acik = PlayerSettings.Load(dosya);
        Assert.Equal(HardwareDecoding.AutoCopy, acik.Hardware);
        Assert.True(acik.Shuffle);

        acik.Hardware = HardwareDecoding.Off;
        acik.Save(dosya);
        Assert.Contains("\"hardwareDecoding\": \"Off\"", File.ReadAllText(dosya), StringComparison.Ordinal);
        Assert.Equal(HardwareDecoding.Off, PlayerSettings.Load(dosya).Hardware);

        const string eski = "{\n  \"screenshotFolder\": null,\n  \"screenshotPattern\": \"{name}_{time}\",\n  \"repeat\": \"All\",\n  \"shuffle\": true,\n  \"showRemaining\": true,\n  \"speedA\": 1,\n  \"speedB\": 1.8,\n  \"speedMode\": \"B\"\n}";
        File.WriteAllText(dosya, eski, new UTF8Encoding(false));
        var eskiden = PlayerSettings.Load(dosya);
        Assert.Equal(HardwareDecoding.Off, eskiden.Hardware);
        Assert.Equal(RepeatMode.All, eskiden.Repeat);
        Assert.True(eskiden.SpeedModeB);

        foreach (var bozuk in new[] { "\"7\"", "\"d3d11va\"", "\"\"", "null" })
        {
            File.WriteAllText(dosya, "{ \"shuffle\": true, \"hardwareDecoding\": " + bozuk + " }", new UTF8Encoding(false));
            var okunan = PlayerSettings.Load(dosya);
            Assert.Equal(HardwareDecoding.Off, okunan.Hardware);
            Assert.True(okunan.Shuffle);
        }

        File.WriteAllText(dosya, "{ \"hardwareDecoding\": \"autocopy\" }", new UTF8Encoding(false));
        Assert.Equal(HardwareDecoding.AutoCopy, PlayerSettings.Load(dosya).Hardware);
    }

    [Fact]
    public void PaneldekiAnahtarSecimiDegistirirSifirlamaKapatir()
    {
        AppHost.Run(() =>
        {
            const string ad = "panel";
            File.Delete(AyarDosyasi(ad));
            var motor = new DonanimMotoru();
            var view = Ac(motor, ad, out var pencere);
            var panel = new PlayerAdvancedPanel { Player = view };
            var panelPenceresi = new Window { Width = 480, Height = 480, Content = panel };
            panelPenceresi.Show();
            Dispatcher.UIThread.RunJobs();

            CheckBox Kutu() => Assert.Single(
                panel.FindControl<StackPanel>("Switches")!.Children.OfType<CheckBox>(),
                kutu => (kutu.Content as string) == Strings.Get("player.advanced.hardware"));

            Assert.Contains(Strings.Get("player.advanced.hardware"), panel.Shown);
            Assert.False(Kutu().IsChecked);

            Kutu().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.HardwareDecodingOn);
            Assert.True(Kutu().IsChecked);
            Assert.Equal(new[] { HardwareDecoding.AutoCopy }, motor.Yazilan);
            Assert.Equal(HardwareDecoding.AutoCopy, PlayerSettings.Load(AyarDosyasi(ad)).Hardware);

            view.ResetAdvanced();
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.HardwareDecodingOn);
            Assert.False(Kutu().IsChecked);
            Assert.Equal(new[] { HardwareDecoding.AutoCopy, HardwareDecoding.Off }, motor.Yazilan);
            Assert.Equal(HardwareDecoding.Off, PlayerSettings.Load(AyarDosyasi(ad)).Hardware);

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
            Assert.True(degerler.TryGetValue("player.advanced.hardware", out var deger) && !string.IsNullOrWhiteSpace(deger), dil);
            Assert.NotEqual(degerler["player.advanced.deinterlace"], deger);
        }

        Assert.Equal("Donanım Çözme", Locales.Domain("tr", "advanced")["player.advanced.hardware"]);
        Assert.Equal("Hardware Decoding", Locales.Domain("en", "advanced")["player.advanced.hardware"]);
    }

    [Fact]
    public void GercekMotordaHwdecYazilirVeGeriOkunur()
    {
        var rapor = AppHost.Run(() =>
        {
            var o = KisayolOrtam.Ac(DonanimKanit.Klip, "donanim-cozme");
            Assert.Equal("no", o.Oku("hwdec"));
            Assert.Equal(HardwareDecoding.Off, o.Motor.Hardware);
            Assert.False(o.View.HardwareDecodingOn);

            o.View.ToggleHardwareDecoding();
            o.Bekle(() => o.Oku("hwdec") == "auto-copy", 3);
            Assert.Equal("auto-copy", o.Oku("hwdec"));
            Assert.Equal(HardwareDecoding.AutoCopy, o.Motor.Hardware);
            o.Git(1.5);
            o.Not($"auto-copy: hwdec {o.Oku("hwdec")} hwdec-current {o.Oku("hwdec-current")} time-pos {o.Sayi("time-pos"):0.###}");
            Assert.InRange(o.Sayi("time-pos"), 1.25, 1.75);
            Assert.True(o.Motor.IsOpen);

            o.View.ToggleHardwareDecoding();
            o.Bekle(() => o.Oku("hwdec") == "no", 3);
            Assert.Equal("no", o.Oku("hwdec"));
            Assert.Equal(HardwareDecoding.Off, o.Motor.Hardware);
            o.Git(0.5);
            o.Not($"no: hwdec {o.Oku("hwdec")} hwdec-current {o.Oku("hwdec-current")} time-pos {o.Sayi("time-pos"):0.###}");
            Assert.InRange(o.Sayi("time-pos"), 0.25, 0.75);

            var kayit = o.Kayit.ToString();
            o.Kapat();
            return kayit;
        });

        Assert.Contains("auto-copy: hwdec auto-copy", rapor);
        DonanimKanit.Kapat("klip-160x90.mkv");
        KisayolKanit.Kapat(Path.Combine("gecmis", "donanim-cozme"));
    }
}
