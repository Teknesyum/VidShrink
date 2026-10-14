using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal sealed class SesCikisMotoru : IPlaybackEngine
{
    private readonly byte[] _piksel = Enumerable.Repeat((byte)128, 4 * 64 * 36).ToArray();

    internal List<AudioDevice> Aygitlar { get; } = new()
    {
        new AudioDevice(AudioDevice.Auto, "Autoselect device"),
        new AudioDevice("wasapi/{kulaklik}", "Kulaklik"),
        new AudioDevice("wasapi/{hoparlor}", "Hoparlor")
    };

    internal List<string> YazilanAygit { get; } = new();

    internal List<AudioOutput> YazilanCikis { get; } = new();

    public IReadOnlyList<AudioDevice> AudioDevices => Aygitlar;

    public string CurrentAudioDevice { get; private set; } = AudioDevice.Auto;

    public bool SetAudioDevice(string name)
    {
        YazilanAygit.Add(name);
        CurrentAudioDevice = name;
        return true;
    }

    public AudioOutput Output { get; private set; } = AudioOutput.Plain;

    public void SetAudioOutput(AudioOutput output)
    {
        YazilanCikis.Add(output);
        Output = output;
    }

    public string Name => "ses-cikis-motoru";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 600;

    public bool HasAudio => true;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached => false;

    public double PositionSeconds { get; private set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered { get; private set; }

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

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
/// Ses cikis aygiti ve cikis secenekleri (OYA-P4). Aygit <c>audio-device-list</c>'ten okunur,
/// secim <c>audio-device</c> ile yazilir ve <c>player-settings.json</c>'da saklanir; kayitli aygit
/// listede yoksa motora yazilmaz, otomatik kalir. Passthrough, ozel kip ve stereoya indirme ucu de
/// kapali gelir ve kapaliyken motora yazilmaz. Hoparlorden ses cikmaz: gercek motor <c>ao=null</c>
/// ile kosar, passthrough ve ozel kip yalniz ozellik yazimi ve geri okumasiyla olculur.
/// </summary>
public sealed class OynaticiSesCikisiTests
{
    private static string Gecmis(string ad)
    {
        var gecmis = Path.Combine(DonanimKanit.Folder, "ses-" + ad, "gecmis.json");
        Directory.CreateDirectory(Path.GetDirectoryName(gecmis)!);
        return gecmis;
    }

    private static string AyarDosyasi(string ad) => Path.Combine(Path.GetDirectoryName(Gecmis(ad))!, PlayerSettings.FileName);

    private static PlayerView Ac(SesCikisMotoru motor, string ad, out Window pencere)
    {
        var gecmis = Gecmis(ad);
        var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => gecmis };
        pencere = new Window { Width = 640, Height = 360, Content = view };
        pencere.Show();
        var acilis = view.OpenAsync(DonanimKanit.Sahte("ses-" + ad + ".mp4"));
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
        DonanimKanit.Kapat("ses-" + ad + ".mp4", "ses-" + ad);
    }

    private static MenuItem CikisMenusu(PlayerView view)
        => Assert.Single(view.BuildAudioItems().OfType<MenuItem>(), item => (item.Header as string) == Strings.Get("player.tracks.output"));

    [Fact]
    public void VarsayilanlarKapaliVeOtomatik()
    {
        Assert.Equal("auto", AudioDevice.Auto);
        Assert.True(AudioOutput.Plain.IsPlain);
        Assert.False(new AudioOutput(Stereo: true).IsPlain);
        var ayar = new PlayerSettings();
        Assert.Null(ayar.AudioDevice);
        Assert.Equal(AudioOutput.Plain, ayar.AudioOutput);
        Assert.Equal("ac3,eac3,dts,dts-hd,truehd", MpvEngine.SpdifCodecs);
    }

    [Fact]
    public void MenudenSecilenAygitMotoraVeDiskeGider()
    {
        AppHost.Run(() =>
        {
            const string ad = "menu";
            File.Delete(AyarDosyasi(ad));
            var motor = new SesCikisMotoru();
            var view = Ac(motor, ad, out var pencere);

            Assert.Empty(motor.YazilanAygit);
            Assert.Empty(motor.YazilanCikis);

            var satirlar = CikisMenusu(view).Items.OfType<MenuItem>().ToList();
            Assert.Equal(3, satirlar.Count);
            Assert.Equal(Strings.Get("player.tracks.output-auto"), satirlar[0].Header);
            Assert.True(satirlar[0].IsChecked);
            Assert.Equal("Kulaklik", satirlar[1].Header);
            Assert.False(satirlar[1].IsChecked);

            satirlar[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(new[] { "wasapi/{kulaklik}" }, motor.YazilanAygit);
            Assert.Equal("audio-device -> wasapi/{kulaklik}", view.Trace[^1]);
            Assert.Equal("wasapi/{kulaklik}", PlayerSettings.Load(AyarDosyasi(ad)).AudioDevice);
            Assert.True(CikisMenusu(view).Items.OfType<MenuItem>().ElementAt(1).IsChecked);

            view.SelectAudioDevice(AudioDevice.Auto);
            Assert.Equal(AudioDevice.Auto, motor.YazilanAygit[^1]);
            Assert.Null(PlayerSettings.Load(AyarDosyasi(ad)).AudioDevice);

            Kapat(view, pencere, ad);
            return "";
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void KayitliAygitVarsaYazilirYoksaOtomatikKalir(bool var)
    {
        AppHost.Run(() =>
        {
            var ad = var ? "kayitli-var" : "kayitli-yok";
            var aygit = var ? "wasapi/{hoparlor}" : "wasapi/{cikarilmis}";
            new PlayerSettings { AudioDevice = aygit }.Save(AyarDosyasi(ad));

            var motor = new SesCikisMotoru();
            var view = Ac(motor, ad, out var pencere);

            Assert.Equal(var ? new[] { aygit } : Array.Empty<string>(), motor.YazilanAygit);
            Assert.Equal(var ? aygit : AudioDevice.Auto, motor.CurrentAudioDevice);
            Assert.True(CikisMenusu(view).Items.OfType<MenuItem>().ElementAt(var ? 2 : 0).IsChecked);
            Assert.Equal(aygit, PlayerSettings.Load(AyarDosyasi(ad)).AudioDevice);

            Kapat(view, pencere, ad);
            return "";
        });
    }

    [Fact]
    public void AyarGidisDonusuVeEskiDosya()
    {
        var klasor = Path.Combine(DonanimKanit.Folder, "ses-ayar-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, PlayerSettings.FileName);
        try
        {
            new PlayerSettings { AudioDevice = "wasapi/{x}", AudioOutput = new AudioOutput(true, false, true), Shuffle = true }.Save(dosya);
            var metin = File.ReadAllText(dosya);
            Assert.Contains("\"audioDevice\": \"wasapi/{x}\"", metin, StringComparison.Ordinal);
            Assert.Contains("\"audioPassthrough\": true", metin, StringComparison.Ordinal);
            Assert.Contains("\"audioExclusive\": false", metin, StringComparison.Ordinal);
            Assert.Contains("\"audioStereo\": true", metin, StringComparison.Ordinal);
            var okunan = PlayerSettings.Load(dosya);
            Assert.Equal("wasapi/{x}", okunan.AudioDevice);
            Assert.Equal(new AudioOutput(true, false, true), okunan.AudioOutput);
            Assert.True(okunan.Shuffle);

            File.WriteAllText(dosya, "{ \"shuffle\": true, \"audioDevice\": \"\", \"audioExclusive\": true }", new UTF8Encoding(false));
            var eski = PlayerSettings.Load(dosya);
            Assert.Null(eski.AudioDevice);
            Assert.Equal(new AudioOutput(Exclusive: true), eski.AudioOutput);
            Assert.True(eski.Shuffle);

            File.WriteAllText(dosya, "{ \"audioDevice\": \"auto\" }", new UTF8Encoding(false));
            Assert.Null(PlayerSettings.Load(dosya).AudioDevice);
        }
        finally
        {
            DonanimKanit.Kapat(Path.GetFileName(klasor));
        }
    }

    [Fact]
    public void PaneldekiUcAnahtarMotoraYazarSifirlamaKapatir()
    {
        AppHost.Run(() =>
        {
            const string ad = "panel";
            File.Delete(AyarDosyasi(ad));
            var motor = new SesCikisMotoru();
            var view = Ac(motor, ad, out var pencere);
            var panel = new PlayerAdvancedPanel { Player = view };
            var panelPenceresi = new Window { Width = 480, Height = 480, Content = panel };
            panelPenceresi.Show();
            Dispatcher.UIThread.RunJobs();

            CheckBox Kutu(string anahtar) => Assert.Single(
                panel.FindControl<StackPanel>("Switches")!.Children.OfType<CheckBox>(),
                kutu => (kutu.Content as string) == Strings.Get(anahtar));

            foreach (var anahtar in new[] { "player.advanced.passthrough", "player.advanced.exclusive", "player.advanced.stereo" })
                Assert.False(Kutu(anahtar).IsChecked);
            Assert.Empty(motor.YazilanCikis);

            Kutu("player.advanced.passthrough").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new AudioOutput(Passthrough: true), motor.Output);
            Assert.True(Kutu("player.advanced.passthrough").IsChecked);

            Kutu("player.advanced.exclusive").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Kutu("player.advanced.stereo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new AudioOutput(true, true, true), motor.Output);
            Assert.Equal(new AudioOutput(true, true, true), PlayerSettings.Load(AyarDosyasi(ad)).AudioOutput);
            Assert.Equal("audio-output -> spdif yes, exclusive yes, stereo yes", view.Trace[^1]);

            view.ResetAdvanced();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(AudioOutput.Plain, motor.Output);
            Assert.Equal(AudioOutput.Plain, PlayerSettings.Load(AyarDosyasi(ad)).AudioOutput);
            Assert.False(Kutu("player.advanced.stereo").IsChecked);
            var yazim = motor.YazilanCikis.Count;
            view.ResetAdvanced();
            Assert.Equal(yazim, motor.YazilanCikis.Count);

            panelPenceresi.Close();
            Kapat(view, pencere, ad);
            return "";
        });
    }

    [Fact]
    public void KayitliCikisSecenegiYeniDosyadaMotoraGider()
    {
        AppHost.Run(() =>
        {
            const string ad = "kayitli-cikis";
            new PlayerSettings { AudioOutput = new AudioOutput(Stereo: true) }.Save(AyarDosyasi(ad));
            var motor = new SesCikisMotoru();
            var view = Ac(motor, ad, out var pencere);
            Assert.Equal(new[] { new AudioOutput(Stereo: true) }, motor.YazilanCikis);
            Kapat(view, pencere, ad);
            return "";
        });
    }

    [Fact]
    public void AnahtarlarButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var gelismis = Locales.Domain(dil, "advanced");
            var parcalar = Locales.Domain(dil, "tracks");
            var degerler = new[]
            {
                gelismis["player.advanced.passthrough"], gelismis["player.advanced.exclusive"], gelismis["player.advanced.stereo"],
                parcalar["player.tracks.output"], parcalar["player.tracks.output-auto"]
            };
            Assert.All(degerler, deger => Assert.False(string.IsNullOrWhiteSpace(deger), dil));
            Assert.Equal(5, degerler.Distinct().Count());
        }

        Assert.Equal("Ses Çıkışı", Locales.Domain("tr", "tracks")["player.tracks.output"]);
        Assert.Equal("Audio Output", Locales.Domain("en", "tracks")["player.tracks.output"]);
    }

    [Fact]
    public void GercekMotordaListeOkunurSeceneklerGeriOkunur()
    {
        var rapor = AppHost.Run(() =>
        {
            var o = KisayolOrtam.Ac(DonanimKanit.Klip, "ses-cikisi");
            var motor = (MpvEngine)o.Motor;
            Assert.Equal("null", o.Oku("ao"));
            var aygitlar = motor.AudioDevices;
            o.Not($"aygit sayisi {aygitlar.Count}: {string.Join(" | ", aygitlar.Select(a => a.Name + " = " + a.Description))}");
            Assert.NotEmpty(aygitlar);
            Assert.Equal(AudioDevice.Auto, aygitlar[0].Name);
            Assert.Equal(AudioDevice.Auto, motor.CurrentAudioDevice);
            o.Not($"varsayilan: audio-spdif '{o.Oku("audio-spdif")}' audio-exclusive '{o.Oku("audio-exclusive")}' audio-channels '{o.Oku("audio-channels")}'");
            Assert.Equal(AudioOutput.Plain, motor.Output);

            var son = aygitlar[^1].Name;
            o.View.SelectAudioDevice(son);
            Assert.Equal(son, o.Oku("audio-device"));
            Assert.Equal(son, motor.CurrentAudioDevice);
            o.View.SelectAudioDevice(AudioDevice.Auto);
            Assert.Equal(AudioDevice.Auto, o.Oku("audio-device"));

            o.View.ToggleAudioPassthrough();
            o.View.ToggleAudioExclusive();
            o.View.ToggleAudioStereo();
            o.Not($"acik: audio-spdif '{o.Oku("audio-spdif")}' audio-exclusive '{o.Oku("audio-exclusive")}' audio-channels '{o.Oku("audio-channels")}'");
            Assert.Equal(MpvEngine.SpdifCodecs, o.Oku("audio-spdif"));
            Assert.Equal("yes", o.Oku("audio-exclusive"));
            Assert.Equal("stereo", o.Oku("audio-channels"));
            Assert.Equal(new AudioOutput(true, true, true), motor.Output);

            o.View.ResetAdvanced();
            Assert.Equal(AudioOutput.Plain, motor.Output);
            Assert.True(motor.IsOpen);

            var kayit = o.Kayit.ToString();
            o.Kapat();
            return kayit;
        });

        Assert.Contains("aygit sayisi", rapor);
        KisayolKanit.Write("ses-cikisi.txt", rapor);
        DonanimKanit.Kapat("klip-160x90.mkv");
        KisayolKanit.Kapat("ses-cikisi.txt", Path.Combine("gecmis", "ses-cikisi"));
    }
}
