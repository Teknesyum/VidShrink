using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// EKA-15, EKA-22, EKA-28: yalniz ses hedefi, ses kodegi secimi, sikistirici ve sinirlayici.
/// Her olcu uretilen arguman dizisini okur.
/// <para>
/// Opus'un hangi kapta tasindigi ffmpeg 9.0 ile olculdu: mkv ve mp4 tasiyor, mov
/// "Could not write header" ile dusuyor (cikis -22). MP4/MOV Matroska'ya yakalanip
/// <c>-c copy</c> ile aktarildigi icin mov'da Opus teslimde kirilirdi
/// (<c>docs/olcumler/kaydedici-ses-secenekleri.md</c>).
/// </para>
/// </summary>
public sealed class KaydediciYalnizSesTests
{
    private static readonly AudioCaptureDevice Mikrofon =
        new("Mikrofon", CaptureBackend.DirectShow, AudioSourceRole.Microphone);

    private static readonly AudioCaptureDevice SistemSesi =
        new("Hoparlor", CaptureBackend.DirectShow, AudioSourceRole.SystemAudio);

    private static readonly AudioCaptureDevice[] Liste = { Mikrofon, SistemSesi };

    private static AudioCapturePlan Plan(RecorderTargetKind hedef, params AudioCaptureDevice[] cihazlar)
        => AudioCaptureArguments.Build(
            new AudioCaptureSelection(
                cihazlar.FirstOrDefault(c => c.Role == AudioSourceRole.Microphone),
                cihazlar.FirstOrDefault(c => c.Role == AudioSourceRole.SystemAudio)),
            Liste, RecorderArguments.AudioFirstInputIndexFor(hedef), AudioTrackLayout.MixedSingleTrack, null);

    private static RecorderRequest YalnizSes(params AudioCaptureDevice[] cihazlar) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.AudioOnly,
        Audio = Plan(RecorderTargetKind.AudioOnly, cihazlar)
    };

    [Fact]
    public void YalnizSesGoruntuGirdisiKurmazVeSesSifirdanBaslar()
    {
        var args = RecorderArguments.Build(YalnizSes(Mikrofon), @"C:\kayit\a.mp4");

        Assert.Equal(0, RecorderArguments.AudioFirstInputIndexFor(RecorderTargetKind.AudioOnly));
        Assert.Equal(1, RecorderArguments.AudioFirstInputIndexFor(RecorderTargetKind.Screen));
        Assert.DoesNotContain("gdigrab", args);
        Assert.DoesNotContain("-c:v", args);
        Assert.DoesNotContain("-an", args);
        Assert.DoesNotContain("0:v", args);
        Assert.Contains("-vn", args);
        Assert.Equal("0:a", Deger(args, "-map"));
        Assert.Equal(RecorderArguments.DefaultAudioCodec, Deger(args, "-c:a"));
        Assert.Equal("audio=Mikrofon", Deger(args, "-i"));
        Assert.Equal(1, args.Count(a => a == "-i"));
    }

    [Fact]
    public void YalnizSesIkiGirdiyiSifirdanKaristirir()
    {
        var args = RecorderArguments.Build(YalnizSes(Mikrofon, SistemSesi), @"C:\kayit\a.mp4");
        var grafik = Deger(args, "-filter_complex")!;

        Assert.StartsWith("[0:a][1:a]amix=inputs=2", grafik);
        Assert.Equal($"[{AudioCaptureArguments.MixOutputLabel}]", Deger(args, "-map"));
        Assert.Equal(2, args.Count(a => a == "-i"));
        Assert.DoesNotContain("-c:v", args);
    }

    [Fact]
    public void GoruntuHedefiSesiBirdenBaslatir()
    {
        var args = RecorderArguments.Build(new RecorderRequest
        {
            Platform = RecorderPlatform.Windows,
            Target = RecorderTargetKind.Screen,
            Audio = Plan(RecorderTargetKind.Screen, Mikrofon)
        }, @"C:\kayit\a.mp4");

        Assert.Contains("1:a", args);
        Assert.Contains("gdigrab", args);
        Assert.DoesNotContain("-vn", args);
    }

    [Fact]
    public void YalnizSesKayitDisiIstekleriReddeder()
    {
        var sessiz = RecorderArguments.Validate(YalnizSes(), @"C:\kayit\a.mp4");
        var gif = RecorderArguments.Validate(YalnizSes(Mikrofon) with { Container = RecorderContainer.Gif }, @"C:\kayit\a.gif");
        var kamera = RecorderArguments.Validate(YalnizSes(Mikrofon) with { Webcam = new RecorderWebcam("Kamera", 320) }, @"C:\kayit\a.mp4");
        var onizleme = RecorderArguments.Validate(YalnizSes(Mikrofon) with { PreviewPath = @"C:\kayit\p.jpg" }, @"C:\kayit\a.mp4");
        var ddagrab = RecorderArguments.Validate(YalnizSes(Mikrofon) with { Capture = RecorderCapture.Ddagrab }, @"C:\kayit\a.mp4");
        var gecerli = RecorderArguments.Validate(YalnizSes(Mikrofon) with { VideoCodec = "h264_videotoolbox" }, @"C:\kayit\a.mp4");

        Assert.Contains(sessiz, e => e.Contains("Audio-only", StringComparison.Ordinal) && e.Contains("audio input", StringComparison.Ordinal));
        Assert.Contains(gif, e => e.Contains("Audio-only", StringComparison.Ordinal));
        Assert.Contains(kamera, e => e.Contains("Audio-only", StringComparison.Ordinal));
        Assert.Contains(onizleme, e => e.Contains("Audio-only", StringComparison.Ordinal));
        Assert.Contains(ddagrab, e => e.Contains("Audio-only", StringComparison.Ordinal));
        Assert.Empty(gecerli);
        Assert.Throws<InvalidOperationException>(() => RecorderArguments.BuildSnapshot(YalnizSes(Mikrofon), @"C:\kayit\s.png"));
    }

    [Fact]
    public void YalnizSesYakalamaYoluSecmez()
    {
        var istek = YalnizSes(Mikrofon) with { DdaOutputs = new[] { new DdaOutput(0, 0, 0, 1920, 1080) } };

        var secilen = RecorderArguments.ChooseCapture(istek, true, out var sebep);

        Assert.Equal(RecorderCapture.Gdigrab, secilen.Capture);
        Assert.Equal(DdagrabFallback.None, sebep);
    }

    [Theory]
    [InlineData(RecorderContainer.Mp4, "aac,libopus")]
    [InlineData(RecorderContainer.Mkv, "aac,libopus")]
    [InlineData(RecorderContainer.Mov, "aac")]
    [InlineData(RecorderContainer.Gif, "")]
    public void OpusYalnizTasiyanKaptaSunulur(RecorderContainer kap, string beklenen)
        => Assert.Equal(beklenen, string.Join(",", RecorderArguments.AudioCodecsFor(kap)));

    [Fact]
    public void SecilenSesKodegiHerIzeYazilir()
    {
        var tek = RecorderArguments.Build(YalnizSes(Mikrofon) with { Container = RecorderContainer.Mkv, AudioCodec = "libopus" }, @"C:\kayit\a.mkv");
        var ayriPlan = AudioCaptureArguments.Build(
            new AudioCaptureSelection(Mikrofon, SistemSesi), Liste, 1, AudioTrackLayout.SeparateTracks, null);
        var ayri = RecorderArguments.Build(new RecorderRequest
        {
            Platform = RecorderPlatform.Windows,
            Target = RecorderTargetKind.Screen,
            Container = RecorderContainer.Mp4,
            AudioCodec = "libopus",
            Audio = ayriPlan
        }, @"C:\kayit\a.mp4");
        var varsayilan = RecorderArguments.Build(YalnizSes(Mikrofon), @"C:\kayit\a.mp4");

        Assert.Equal("libopus", Deger(tek, "-c:a"));
        Assert.Equal("libopus", Deger(ayri, "-c:a:0"));
        Assert.Equal("libopus", Deger(ayri, "-c:a:1"));
        Assert.Equal("aac", Deger(varsayilan, "-c:a"));
        Assert.Equal("aac", new RecorderRequest { Platform = RecorderPlatform.Windows, Target = RecorderTargetKind.Screen }.AudioCodec);
    }

    [Fact]
    public void KabinTasimadigiSesKodegiReddedilir()
    {
        var mov = RecorderArguments.Validate(YalnizSes(Mikrofon) with { Container = RecorderContainer.Mov, AudioCodec = "libopus" }, @"C:\kayit\a.mov");
        var uydurma = RecorderArguments.Validate(YalnizSes(Mikrofon) with { AudioCodec = "libmp3lame" }, @"C:\kayit\a.mp4");
        var mkv = RecorderArguments.Validate(YalnizSes(Mikrofon) with { Container = RecorderContainer.Mkv, AudioCodec = "libopus" }, @"C:\kayit\a.mkv");

        Assert.Contains(mov, e => e.Contains("libopus", StringComparison.Ordinal) && e.Contains("mov", StringComparison.Ordinal));
        Assert.Contains(uydurma, e => e.Contains("libmp3lame", StringComparison.Ordinal));
        Assert.Empty(mkv);
    }

    [Fact]
    public void SikistiriciVeSinirlayiciZincireSondaGirer()
    {
        Assert.Equal("acompressor", AudioCaptureArguments.FilterChain(new AudioFilterOptions(Compressor: true)));
        Assert.Equal("alimiter", AudioCaptureArguments.FilterChain(new AudioFilterOptions(Limiter: true)));
        Assert.Equal("volume=3dB,agate,acompressor,alimiter",
            AudioCaptureArguments.FilterChain(new AudioFilterOptions(3, NoiseGate: true, Compressor: true, Limiter: true)));
        Assert.True(new AudioFilterOptions(Compressor: true).IsSet);
        Assert.True(new AudioFilterOptions(Limiter: true).IsSet);
        Assert.Null(AudioCaptureArguments.FilterChain(AudioFilterOptions.None));

        var plan = AudioCaptureArguments.Build(new AudioCaptureSelection(Mikrofon), Liste, 0,
            AudioTrackLayout.MixedSingleTrack, new AudioFilterOptions(Compressor: true, Limiter: true));
        Assert.Equal($"[0:a]acompressor,alimiter[{AudioCaptureArguments.MixOutputLabel}]", plan.FilterComplex);
    }

    [Fact]
    public void ArayuzYalnizSesHedefiniIstegeCevirir()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var view = new RecorderView(ayarYolu) { ListAudioDevices = () => new[] { Mikrofon } };
            Elle(view);
            view.RefreshAudioBoxes();
            Bul<ComboBox>(view, "CmbMicrophone").SelectedIndex = 1;
            Bul<CheckBox>(view, "ChkLivePreview").IsChecked = true;
            var hedef = Bul<ComboBox>(view, "CmbTarget");
            var adet = hedef.ItemCount;
            hedef.SelectedIndex = (int)RecorderTargetKind.AudioOnly;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var istek = view.BuildRequest(applyAuto: false);
            return (adet, istek,
                Satirlar: Bul<Control>(view, "RowScreen").IsVisible || Bul<Control>(view, "RowWindow").IsVisible || Bul<Control>(view, "RowRegion").IsVisible);
        }));

        Assert.Equal(5, olcu.adet);
        Assert.NotNull(olcu.istek);
        Assert.Equal(RecorderTargetKind.AudioOnly, olcu.istek!.Target);
        Assert.Equal(new[] { "0:a" }, olcu.istek.Audio!.Maps);
        Assert.Null(olcu.istek.PreviewPath);
        Assert.Null(olcu.istek.Webcam);
        Assert.False(olcu.Satirlar);
        Assert.Empty(RecorderArguments.Validate(olcu.istek, @"C:\kayit\a.mp4"));
    }

    [Fact]
    public void KodekKutusuKabaGoreOpusSunarVeSecimKalir()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var once = new RecorderView(ayarYolu);
            Elle(once);
            Sec(once, "CmbContainer", "MOV");
            var movAdet = Bul<ComboBox>(once, "CmbAudioCodec").ItemCount;
            Sec(once, "CmbContainer", "MKV");
            var mkvAdet = Bul<ComboBox>(once, "CmbAudioCodec").ItemCount;
            Sec(once, "CmbAudioCodec", "Opus");
            Bul<CheckBox>(once, "ChkCompressor").IsChecked = true;
            Bul<CheckBox>(once, "ChkLimiter").IsChecked = true;
            var istek = once.BuildRequest(applyAuto: false);
            var sonra = new RecorderView(ayarYolu);
            return (movAdet, mkvAdet, Kodek: istek?.AudioCodec, sonra.Settings.AudioCodec, sonra.Settings.AudioFilters,
                Kutular: (Bul<ComboBox>(sonra, "CmbAudioCodec").SelectedItem as string,
                    Bul<CheckBox>(sonra, "ChkCompressor").IsChecked, Bul<CheckBox>(sonra, "ChkLimiter").IsChecked));
        }));

        Assert.Equal(1, olcu.movAdet);
        Assert.Equal(2, olcu.mkvAdet);
        Assert.Equal("libopus", olcu.Kodek);
        Assert.Equal("libopus", olcu.AudioCodec);
        Assert.True(olcu.AudioFilters.Compressor);
        Assert.True(olcu.AudioFilters.Limiter);
        Assert.Equal(("Opus", (bool?)true, (bool?)true), olcu.Kutular);
    }

    [Fact]
    public void MovaGeceneOpusAacyeDuser()
    {
        var kodek = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var view = new RecorderView(ayarYolu);
            Elle(view);
            Sec(view, "CmbContainer", "MKV");
            Sec(view, "CmbAudioCodec", "Opus");
            Sec(view, "CmbContainer", "MOV");
            return (Kutu: Bul<ComboBox>(view, "CmbAudioCodec").SelectedItem as string, Istek: view.BuildRequest(applyAuto: false)?.AudioCodec);
        }));

        Assert.Equal(("AAC", (string?)"aac"), kodek);
    }
}
