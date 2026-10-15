using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// DZB-P5: klip panelinde ses temizleme ve duzey. Gurultu azaltma (<c>afftdn</c>), uc bantli ekolayzir (<c>equalizer</c>),
/// kompresor (<c>acompressor</c>), yukseklik esitleme (<c>loudnorm</c> + <c>aresample=48000</c>) ve sinirlayici
/// (<c>alimiter</c>) klibin ses zincirine sabit sirayla girer; disa aktarim grafi, proje kaydi ve onizlemenin zaman
/// cizelgeli zinciri (yalniz <c>enable</c> alan suzgecler) argüman duzeyinde olculur. Canli olcum: teslim edilen dosyanin
/// <c>ebur128</c> butunlesik yuksekligi -24 LUFS'a iner. Duzey gostergesi: libmpv <c>af-metadata</c> uzerinden
/// <c>astats</c> tepe duzeyi, <c>ao=null</c> ile (hoparlore ses yok).
/// </summary>
public sealed class DuzenleyiciSesTemizlemeTests
{
    private const long Gb = 1_000_000_000;
    private static readonly double[] Anahtarlar = { 0, 2, 4, 6, 8 };
    private static readonly string Kanit = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-abde1d1e56883e0e8", "ses-temizleme");

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static MediaInfo Bilgi() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        AudioCodec = "aac",
        TotalBitrateBps = 1,
    };

    private static ClipEffects Hepsi => new()
    {
        Denoise = true,
        BassDb = 3,
        MidDb = -2,
        TrebleDb = 1.5,
        VolumeDb = -3,
        Compressor = true,
        Loudnorm = true,
        Limiter = true,
        FadeIn = S(0.5),
    };

    [Fact]
    public void SesZinciriSabitSiradaKurulur()
    {
        Assert.Equal(string.Empty, ClipFilters.Audio(ClipEffects.None, S(5)));
        Assert.Equal(
            "afftdn,equalizer=f=100:t=o:w=2:g=3,equalizer=f=1000:t=o:w=2:g=-2,equalizer=f=8000:t=o:w=2:g=1.5,volume=-3dB,"
            + "acompressor=threshold=0.125:ratio=4:attack=20:release=250,loudnorm=I=-24:LRA=7:TP=-2,aresample=48000,"
            + "alimiter=limit=0.89:level=0,afade=t=in:st=0:d=0.5",
            ClipFilters.Audio(Hepsi, S(5)));

        Assert.Equal("afftdn", ClipFilters.Audio(new ClipEffects { Denoise = true }, S(5)));
        Assert.Equal(StreamMapping.LoudnormFilter + ",aresample=48000", ClipFilters.Audio(new ClipEffects { Loudnorm = true }, S(5)));
        Assert.Equal(ClipFilters.CompressorFilter, ClipFilters.Audio(new ClipEffects { Compressor = true }, S(5)));
        Assert.Equal(ClipFilters.LimiterFilter, ClipFilters.Audio(new ClipEffects { Limiter = true }, S(5)));
        Assert.Equal("equalizer=f=8000:t=o:w=2:g=-4", ClipFilters.Audio(new ClipEffects { TrebleDb = -4 }, S(5)));
        Assert.Equal("volume=0", ClipFilters.Audio(Hepsi with { Muted = true, FadeIn = 0 }, S(5)));
    }

    [Fact]
    public void AlanlarEtkiSayilirVeNormallesir()
    {
        foreach (var e in new[]
                 {
                     new ClipEffects { Denoise = true }, new ClipEffects { Loudnorm = true }, new ClipEffects { Compressor = true },
                     new ClipEffects { Limiter = true }, new ClipEffects { BassDb = 1 }, new ClipEffects { MidDb = -1 }, new ClipEffects { TrebleDb = 2 },
                 })
        {
            Assert.True(e.HasAudio);
            Assert.True(e.HasCleanup);
            Assert.False(e.IsNeutral);
            Assert.False(e.HasColor);
        }

        var n = new ClipEffects { BassDb = 20, MidDb = -30, TrebleDb = double.NaN }.Normalized();
        Assert.Equal((12.0, -12.0, 0.0), (n.BassDb, n.MidDb, n.TrebleDb));
        Assert.Equal(2.3, new ClipEffects { BassDb = 2.345 }.Normalized().BassDb);

        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.SetEffects(new[] { 0 }, e => e with { Denoise = true }));
        Assert.True(c.HasEffects);
        Assert.True(c.SetEffects(new[] { 0 }, e => e with { Denoise = false }));
        Assert.Same(ClipEffects.None, c.Clips[0].Effects);
    }

    [Fact]
    public void ProjeKaydiTemizlemeAlanlariniTasir()
    {
        var c = EditTimeline.FromSource(S(10));
        c.SetEffects(new[] { 0 }, _ => Hepsi);
        var json = EditProject.From(new SourceStamp("a.mp4", 1, 1), c).ToJson();
        var geri = EditProject.Parse(json)!.Clips[0].Effects;
        Assert.Equal(Hepsi.Normalized(), geri);

        var eski = json.Replace("\"denoise\": true,", string.Empty, StringComparison.Ordinal);
        Assert.NotEqual(json, eski);
        Assert.False(EditProject.Parse(eski)!.Clips[0].Effects.Denoise);
    }

    [Fact]
    public void DisaAktarimGrafiZinciriTasirHizliKipTamaDuser()
    {
        var c = EditTimeline.FromSource(S(10));
        c.SetEffects(new[] { 0 }, e => e with { Denoise = true, Loudnorm = true, Limiter = true });
        var plan = EditExport.Build(c, Bilgi(), Anahtarlar, 0, ExportMode.Fast, "cikti.mp4", "is", 8 * Gb);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.EffectsForcedFull);
        var graf = Deger(plan.Steps[0].Args, "-filter_complex");
        Assert.Contains("asetpts=PTS-STARTPTS,afftdn,loudnorm=I=-24:LRA=7:TP=-2,aresample=48000,alimiter=limit=0.89:level=0", graf);
    }

    [Fact]
    public void OnizlemeYalnizZamanCizelgeliSuzgecleriAlir()
    {
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.DeleteRange(S(2), S(4)));
        c.SetEffects(new[] { 1 }, _ => Hepsi with { FadeIn = 0, VolumeDb = 0 });
        var (video, ses) = ClipFilters.Timed(new EdlPreview("k.mp4", c));
        Assert.Null(video);
        const string w = ":enable='gte(t,2)*lt(t,8)'";
        Assert.Equal(
            "afftdn=enable='gte(t,2)*lt(t,8)',equalizer=f=100:t=o:w=2:g=3" + w + ",equalizer=f=1000:t=o:w=2:g=-2" + w + ",equalizer=f=8000:t=o:w=2:g=1.5" + w
            + ",alimiter=limit=0.89:level=0" + w,
            ses);
        Assert.DoesNotContain("acompressor", ses);
        Assert.DoesNotContain("loudnorm", ses);
    }

    [Fact]
    public void ArayuzPaneliTemizlemeyiYazar()
    {
        var klasor = Path.Combine(Kanit, "arayuz");
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        try
        {
            AppHost.Run(() =>
            {
                var motor = new KlipMotoru();
                var view = new EditorView
                {
                    KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 }
                };
                view.Player.EngineFactory = () => motor;
                view.OverlayRoot = Path.Combine(klasor, "katman");
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    DenetimSurucu.Pump(view.Player, () => motor.Acilan >= 1, 10);
                    var model = view.Model!;
                    view.TimelineView.SelectedIndex = 0;
                    Assert.True(view.ClipPanel.IsVisible);
                    Assert.Equal("0", view.TxtClipBass.Text);

                    foreach (var (dugme, oku) in new (Avalonia.Controls.Primitives.ToggleButton, Func<ClipEffects, bool>)[]
                             {
                                 (view.BtnClipDenoise, e => e.Denoise), (view.BtnClipLoudnorm, e => e.Loudnorm),
                                 (view.BtnClipCompressor, e => e.Compressor), (view.BtnClipLimiter, e => e.Limiter),
                             })
                    {
                        dugme.IsChecked = true;
                        dugme.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert.True(oku(model.Clips[0].Effects));
                    }

                    foreach (var (kutu, deger, oku) in new (TextBox, string, Func<ClipEffects, double>)[]
                             {
                                 (view.TxtClipBass, "4", e => e.BassDb), (view.TxtClipMid, "-2.5", e => e.MidDb), (view.TxtClipTreble, "40", e => e.TrebleDb),
                             })
                    {
                        kutu.Text = deger;
                        kutu.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                        Assert.Equal(Math.Clamp(double.Parse(deger, CultureInfo.InvariantCulture), -12, 12), oku(model.Clips[0].Effects));
                    }

                    Assert.Equal("12", view.TxtClipTreble.Text);
                    DenetimSurucu.Pump(view.Player, () => motor.ZamanliSes?.Contains("afftdn", StringComparison.Ordinal) == true, 10);
                    Assert.Contains("equalizer=f=8000:t=o:w=2:g=12", motor.ZamanliSes);

                    view.TxtClipMid.Text = "x";
                    view.TxtClipMid.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                    Assert.Equal(-2.5, model.Clips[0].Effects.MidDb);

                    Assert.True(view.Undo());
                    Assert.Equal(0, model.Clips[0].Effects.TrebleDb);
                    Assert.Equal("0", view.TxtClipTreble.Text);

                    view.BtnClipReset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(model.Clips[0].Effects.IsNeutral);
                    Assert.False(view.BtnClipDenoise.IsChecked);
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }
            });
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void DuzeyGostergesiMotorunTepeDegeriniGosterir()
    {
        var klasor = Path.Combine(Kanit, "duzey");
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        try
        {
            AppHost.Run(() =>
            {
                var motor = new DuzeyMotoru();
                var view = new EditorView
                {
                    KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", AudioCodec = "aac", TotalBitrateBps = 1 }
                };
                view.Player.EngineFactory = () => motor;
                view.OverlayRoot = Path.Combine(klasor, "katman");
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    DenetimSurucu.Pump(view.Player, () => motor.IsOpen, 10);

                    motor.Tepe = -12;
                    view.Follow();
                    Assert.True(motor.Olcuyor);
                    Assert.True(view.LevelMeter.IsVisible);
                    Assert.Equal(-12, view.LevelMeter.Value);

                    motor.Tepe = double.NegativeInfinity;
                    view.Follow();
                    Assert.Equal(view.LevelMeter.Minimum, view.LevelMeter.Value);

                    motor.Tepe = 3;
                    view.Follow();
                    Assert.Equal(0, view.LevelMeter.Value);
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }
            });
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task CanliTeslimYuksekligiEsitler()
    {
        var klasor = Path.Combine(Kanit, "canli");
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=gray:s=160x90:r=25:d=4",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=4,volume=-35dB",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", "-threads", "2", kaynak);

            async Task<double> Aktar(string ad, Func<ClipEffects, ClipEffects> ayar)
            {
                var c = EditTimeline.FromSource(S(4));
                c.SetEffects(new[] { 0 }, ayar);
                var cikti = Path.Combine(klasor, ad);
                var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, ExportMode.Full, cikti, 8 * Gb);
                var args = plan.Steps[0].Args.ToList();
                args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
                await EditExportRunner.RunAsync(plan with { Steps = new[] { plan.Steps[0] with { Args = args } } }, null);
                return Yukseklik(cikti);
            }

            var yalin = await Aktar("yalin.mp4", e => e);
            var esit = await Aktar("esit.mp4", e => e with { Loudnorm = true, Denoise = true, Limiter = true });
            var kayit = $"yalin {yalin.ToString(CultureInfo.InvariantCulture)} LUFS, esitlenmis {esit.ToString(CultureInfo.InvariantCulture)} LUFS";
            Assert.True(yalin < -30, kayit);
            Assert.InRange(esit, -26, -22);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task LibmpvTepeDuzeyiniAfMetadatadanOkur()
    {
        var klasor = Path.Combine(Kanit, "mpv");
        Directory.CreateDirectory(klasor);
        try
        {
            var ton = Path.Combine(klasor, "ton.wav");
            Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=3,volume=-12dB", "-threads", "2", ton);

            using var motor = new MpvEngine();
            motor.SetProperty("ao", "null");
            Assert.True(double.IsNaN(motor.AudioPeakDb));
            Assert.True(motor.MeterAudio(true));
            await motor.OpenAsync(ton);
            motor.Play();
            var bitis = DateTime.UtcNow.AddSeconds(5);
            var tepe = double.NaN;
            while (DateTime.UtcNow < bitis && !double.IsFinite(tepe))
            {
                await Task.Delay(50);
                tepe = motor.AudioPeakDb;
            }

            var kayit = $"tepe {tepe.ToString(CultureInfo.InvariantCulture)} dB; ham: {motor.GetProperty("af-metadata/vsmeter")}; log: {string.Join(" | ", motor.RecentLog.TakeLast(10))}";
            Assert.True(double.IsFinite(tepe), kayit);
            Assert.InRange(tepe, -33, -27);

            Assert.True(motor.MeterAudio(false));
            Assert.True(double.IsNaN(motor.AudioPeakDb));
            Assert.DoesNotContain("vsmeter", motor.GetProperty("af") ?? string.Empty);
            motor.Dispose();
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    private static double Yukseklik(string dosya)
    {
        var dokum = Ffmpeg("-hide_banner", "-nostdin", "-i", dosya, "-map", "0:a", "-af", "ebur128", "-threads", "2", "-f", "null", "-");
        var eslesme = Regex.Matches(dokum, @"I:\s+(-?[\d.]+) LUFS");
        Assert.True(eslesme.Count > 0, dokum);
        return double.Parse(eslesme[^1].Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static string Deger(System.Collections.Generic.IReadOnlyList<string> args, string anahtar) => args[args.ToList().IndexOf(anahtar) + 1];

    private static string Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var standart = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(60_000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 60 sn icinde bitmedi");
        }

        surec.WaitForExit();
        Assert.True(surec.ExitCode == 0, hata.Result);
        return hata.Result;
    }
}

internal sealed class DuzeyMotoru : IPlaybackEngine
{
    internal double Tepe { get; set; } = double.NaN;

    internal bool Olcuyor { get; private set; }

    public string Name => "duzey-motoru";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 600;

    public bool HasAudio => true;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached { get; set; }

    public double PositionSeconds { get; private set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered { get; private set; }

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public bool MeterAudio(bool on)
    {
        Olcuyor = on;
        return true;
    }

    public double AudioPeakDb => Olcuyor ? Tepe : double.NaN;

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        IsOpen = true;
        FramesRendered++;
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

    public bool TryCopyLatest(ref long seen, FrameCopy copy) => false;

    public void Dispose() => IsOpen = false;
}
