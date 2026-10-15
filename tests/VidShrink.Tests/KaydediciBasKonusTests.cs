using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// Bas-konuş (EKA-09): mikrofon izi yalnız kısayol basılıyken kayda girer. Mikrofon açılmaz,
/// gerçek kısayol kaydı kurulmaz: tuşlar sahte <see cref="IGlobalHotkeys"/> ile basılıp bırakılır,
/// ffmpeg'e giden komut lavfi'nin ürettiği sinüs üzerinde ölçülür.
/// </summary>
public sealed class KaydediciBasKonusTests
{
    private static readonly AudioCaptureDevice Mikrofon =
        new("Mikrofon", CaptureBackend.DirectShow, AudioSourceRole.Microphone);

    private static readonly AudioCaptureDevice SistemSesi =
        new("Hoparlor", CaptureBackend.DirectShow, AudioSourceRole.SystemAudio);

    private static readonly AudioCaptureDevice[] Liste = { Mikrofon, SistemSesi };

    private static readonly string[] YeniAnahtarlar = { "recorder.audio.push-to-talk", "recorder.audio.push-to-talk.hint" };

    private sealed class SahteKanca : IGlobalHotkeys
    {
        private Action<HotkeyAction>? _basildi;
        private Action<HotkeyAction>? _birakildi;

        public IReadOnlyList<HotkeyBinding>? Kayitli { get; private set; }

        public IReadOnlyList<HotkeyBinding> Register(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyAction> pressed, Action<HotkeyAction> released)
        {
            _basildi = pressed;
            _birakildi = released;
            Kayitli = bindings.ToList();
            return [];
        }

        public void Unregister() => Kayitli = null;

        public bool Bas(Key tus, KeyModifiers ek)
        {
            if (Kayitli?.FirstOrDefault(b => b.Key == tus && b.Modifiers == ek) is not { } bag) return false;
            _basildi!(bag.Action);
            return true;
        }

        public bool Birak(Key tus, KeyModifiers ek)
        {
            if (Kayitli?.FirstOrDefault(b => b.Key == tus && b.Modifiers == ek) is not { } bag) return false;
            _birakildi!(bag.Action);
            return true;
        }
    }

    [Fact]
    public void VarsayilanTusCtrlF5VeEskiAyarAtamalariKorunur()
    {
        var bag = RecorderHotkeys.Of(RecorderHotkeys.All, HotkeyAction.PushToTalk);
        Assert.Equal((Key.F5, KeyModifiers.Control, 0x74u), (bag.Key, bag.Modifiers, bag.VirtualKey));

        var alti = RecorderHotkeys.Read("0:F1,0:F2,0:F3,0:F4,2:K,0:F6")!;
        Assert.Equal(RecorderHotkeys.All.Count, alti.Count);
        Assert.Equal(Key.K, RecorderHotkeys.Of(alti, HotkeyAction.ReplaySave).Key);
        Assert.Equal(bag, RecorderHotkeys.Of(alti, HotkeyAction.PushToTalk));
        Assert.Null(RecorderHotkeys.Read("0:F1,0:F2,0:F3,0:F4,2:F5,0:F6"));

        var yazim = RecorderHotkeys.Write(RecorderHotkeys.All);
        Assert.EndsWith(",2:F5", yazim);
        Assert.Equal<HotkeyBinding>(RecorderHotkeys.All, RecorderHotkeys.Read(yazim)!);
    }

    [Fact]
    public void TekMikrofondaKomutHedefiSessizBaslar()
    {
        var plan = AudioCaptureArguments.Build(new AudioCaptureSelection(Mikrofon), Liste, 1,
            AudioTrackLayout.MixedSingleTrack, new AudioFilterOptions(PushToTalk: true));

        Assert.Equal("[1:a]volume@ptt=0[aout]", plan.FilterComplex);
        Assert.Equal(new[] { "[aout]" }, plan.Maps);
        Assert.True(AudioCaptureArguments.UsesPushToTalk(plan));
        Assert.False(AudioCaptureArguments.UsesPushToTalk(
            AudioCaptureArguments.Build(new AudioCaptureSelection(Mikrofon), Liste, 1)));
    }

    [Fact]
    public void YalnizMikrofonKolunaKurulurSistemSesiDokunulmaz()
    {
        var secim = new AudioCaptureSelection(Mikrofon, SistemSesi);

        var karisik = AudioCaptureArguments.Build(secim, Liste, 1, AudioTrackLayout.MixedSingleTrack, new AudioFilterOptions(PushToTalk: true));
        Assert.Equal("[1:a]volume@ptt=0[amix0];[amix0][2:a]amix=inputs=2:duration=longest:dropout_transition=0[aout]", karisik.FilterComplex);
        Assert.Equal(new[] { "[aout]" }, karisik.Maps);

        var ayri = AudioCaptureArguments.Build(secim, Liste, 1, AudioTrackLayout.SeparateTracks, new AudioFilterOptions(PushToTalk: true));
        Assert.Equal("[1:a]volume@ptt=0[aout0]", ayri.FilterComplex);
        Assert.Equal(new[] { "[aout0]", "2:a" }, ayri.Maps);

        var zincirli = AudioCaptureArguments.Build(secim, Liste, 1, AudioTrackLayout.SeparateTracks, new AudioFilterOptions(3, Limiter: true, PushToTalk: true));
        Assert.Equal("[1:a]volume@ptt=0,volume=3dB,alimiter[aout0];[2:a]volume=3dB,alimiter[aout1]", zincirli.FilterComplex);
        Assert.Equal(new[] { "[aout0]", "[aout1]" }, zincirli.Maps);

        var mikrofonsuz = AudioCaptureArguments.Build(new AudioCaptureSelection(SystemAudio: SistemSesi), Liste, 1,
            AudioTrackLayout.MixedSingleTrack, new AudioFilterOptions(PushToTalk: true));
        Assert.Null(mikrofonsuz.FilterComplex);
        Assert.False(AudioCaptureArguments.UsesPushToTalk(mikrofonsuz));
    }

    [Fact]
    public void KomutSatiriFfmpeginKomutBicimindedir()
    {
        Assert.Equal("cvolume@ptt -1 volume 1\n", AudioCaptureArguments.PushToTalkCommand(true));
        Assert.Equal("cvolume@ptt -1 volume 0\n", AudioCaptureArguments.PushToTalkCommand(false));

        using var yazici = new StringWriter(CultureInfo.InvariantCulture);
        Assert.True(RecorderSession.WriteMicrophone(yazici, true));
        Assert.True(RecorderSession.WriteMicrophone(yazici, false));
        Assert.Equal("cvolume@ptt -1 volume 1\ncvolume@ptt -1 volume 0\n", yazici.ToString());
    }

    /// <summary>
    /// Gerçek ffmpeg: sessiz başlayan mikrofon kolu komutla açılır. Ölçülen gecikme 235 ms
    /// (<c>docs/olcumler/bas-konus-komut-gecikmesi.md</c>); eşik bunun çok üstünde, yalnız açılmanın olduğunu ölçer.
    /// </summary>
    [Fact]
    public async Task CanliFfmpegKomutlaMikrofonuAcar()
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-a83f4b54094b75629", "bas-konus");
        Directory.CreateDirectory(klasor);
        var cikis = Path.Combine(klasor, "ptt.wav");
        try
        {
            var plan = AudioCaptureArguments.Build(new AudioCaptureSelection(Mikrofon), Liste, 0,
                AudioTrackLayout.MixedSingleTrack, new AudioFilterOptions(PushToTalk: true));
            var bilgi = ToolLocatorStart(new[]
            {
                "-hide_banner", "-y", "-threads", "2", "-re",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=3",
                "-filter_complex", plan.FilterComplex!, "-map", plan.Maps[0], cikis
            });
            bilgi.RedirectStandardInput = true;
            using (var surec = Process.Start(bilgi)!)
            {
                var hata = surec.StandardError.ReadToEndAsync();
                var cikti = surec.StandardOutput.ReadToEndAsync();
                await Task.Delay(1000);
                Assert.True(RecorderSession.WriteMicrophone(surec.StandardInput, true));
                await surec.WaitForExitAsync();
                await Task.WhenAll(hata, cikti);
                Assert.Equal(0, surec.ExitCode);
            }

            var olcum = ToolLocatorStart(new[] { "-hide_banner", "-i", cikis, "-af", "silencedetect=n=-60dB:d=0.05", "-f", "null", "-" });
            using var olcer = Process.Start(olcum)!;
            var metinGorev = olcer.StandardError.ReadToEndAsync();
            await olcer.StandardOutput.ReadToEndAsync();
            await olcer.WaitForExitAsync();
            var metin = await metinGorev;

            Assert.Matches(@"silence_start: 0(\s|$)", metin);
            var bitis = Regex.Match(metin, @"silence_end: ([\d.]+)");
            Assert.True(bitis.Success, metin);
            Assert.InRange(double.Parse(bitis.Groups[1].Value, CultureInfo.InvariantCulture), 0.5, 2.8);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    private static ProcessStartInfo ToolLocatorStart(IEnumerable<string> args)
    {
        var bilgi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) bilgi.ArgumentList.Add(arg);
        return bilgi;
    }

    [Fact]
    public void Win32TutulanTusBirakilincaBirKezBildirilir()
    {
        var tutulan = new HeldHotkeys();
        Assert.True(tutulan.Press(2));
        Assert.False(tutulan.Press(2));
        Assert.True(tutulan.Any);

        var basili = true;
        Assert.Empty(tutulan.Release(_ => basili));
        basili = false;
        Assert.Equal(new[] { 2 }, tutulan.Release(_ => basili));
        Assert.Empty(tutulan.Release(_ => basili));
        Assert.False(tutulan.Any);
        Assert.True(tutulan.Press(2));
    }

    [Fact]
    public void X11BasisVeBirakisAyriSayilir()
    {
        var tutulan = new HashSet<uint>();
        Assert.Equal(1, X11GlobalHotkeys.Edge(tutulan, 2, 71));
        Assert.Equal(0, X11GlobalHotkeys.Edge(tutulan, 2, 71));
        Assert.Equal(-1, X11GlobalHotkeys.Edge(tutulan, 3, 71));
        Assert.Equal(0, X11GlobalHotkeys.Edge(tutulan, 3, 71));
        Assert.Equal(1, X11GlobalHotkeys.Edge(tutulan, 2, 71));
        Assert.Equal(6u, MacGlobalHotkeys.HotKeyReleased);
    }

    [Fact]
    public void KapaliykenTusKaydedilmezAcilincaBasBirakMikrofonuSurer()
    {
        using var ayar = new OzelAyar();
        var olcu = AppHost.Run(() =>
        {
            Strings.Use("en");
            var kanca = new SahteKanca();
            var kapi = new List<bool>();
            var view = new RecorderView(ayar.Yol) { GlobalHotkeys = kanca, MicrophoneGate = acik => { kapi.Add(acik); return true; } };
            view.ActivateHotkeys();
            var kapaliKayit = kanca.Kayitli!.Any(b => b.Action == HotkeyAction.PushToTalk);
            var kapaliBasis = kanca.Bas(Key.F5, KeyModifiers.Control);

            Bul<CheckBox>(view, "ChkPushToTalk").IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            var acikKayit = kanca.Kayitli!.Any(b => b.Action == HotkeyAction.PushToTalk);
            kanca.Bas(Key.F5, KeyModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            kanca.Birak(Key.F5, KeyModifiers.Control);
            kanca.Birak(Key.F7, KeyModifiers.None);
            Dispatcher.UIThread.RunJobs();

            var ipucu = ToolTip.GetTip(Bul<CheckBox>(view, "ChkPushToTalk")) as string;
            var sonra = new RecorderView(ayar.Yol);
            view.DeactivateHotkeys();
            return (kapaliKayit, kapaliBasis, acikKayit, kapi, ipucu, Kalici: Bul<CheckBox>(sonra, "ChkPushToTalk").IsChecked);
        });

        Assert.False(olcu.kapaliKayit);
        Assert.False(olcu.kapaliBasis);
        Assert.True(olcu.acikKayit);
        Assert.Equal(new[] { true, false }, olcu.kapi);
        Assert.Contains("Ctrl+F5", olcu.ipucu?.Replace(" ", string.Empty));
        Assert.True(olcu.Kalici);
    }

    [Fact]
    public void YalnizKayitIstegiKomutHedefiTasirTamponTasimaz()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var view = new RecorderView(ayarYolu) { ListAudioDevices = () => new[] { Mikrofon } };
            Elle(view);
            view.RefreshAudioBoxes();
            Bul<ComboBox>(view, "CmbMicrophone").SelectedIndex = 1;
            Bul<ComboBox>(view, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.AudioOnly;
            Bul<CheckBox>(view, "ChkPushToTalk").IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            var kayit = view.PrepareRecording()?.Request;
            var genel = view.BuildRequest(applyAuto: false);
            Bul<CheckBox>(view, "ChkPushToTalk").IsChecked = false;
            var kapali = view.PrepareRecording()?.Request;
            return (kayit, genel, kapali);
        }));

        Assert.Equal("[0:a]volume@ptt=0[aout]", olcu.kayit!.Audio!.FilterComplex);
        Assert.False(AudioCaptureArguments.UsesPushToTalk(olcu.genel!.Audio));
        Assert.Equal(new[] { "0:a" }, olcu.genel.Audio!.Maps);
        Assert.False(AudioCaptureArguments.UsesPushToTalk(olcu.kapali!.Audio));
    }

    [Fact]
    public void BasKonusMetinleriButunDillerde()
    {
        foreach (var dil in Locales.Languages)
        {
            var metin = Locales.Values(dil);
            foreach (var anahtar in YeniAnahtarlar)
                Assert.False(string.IsNullOrWhiteSpace(metin.GetValueOrDefault(anahtar)), dil + " " + anahtar);

            var ipucu = metin["recorder.audio.push-to-talk.hint"];
            Assert.Contains("{0}", ipucu);
            Assert.StartsWith("• ", ipucu);
            Assert.Contains("\n• ", ipucu);
        }
    }
}
