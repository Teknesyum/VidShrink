using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// K2: Windows'ta sistem sesinin WASAPI loopback ile kayda girmesi. Hicbir olcu gercek ses
/// cihazina dokunmaz; yakalama <see cref="ISystemAudioCapture"/> arkasinda durur ve burada
/// sahtesi verilir. Uc katman olculuyor: zaman cizgisi (saf), arguman ve boru beslemesi,
/// kaydedici kutusundaki secenek ile durum satiri.
/// </summary>
public sealed class SistemSesiTests
{
    private const int Hiz = LoopbackAudio.SampleRate;
    private const int SaniyeBayt = LoopbackAudio.SampleRate * LoopbackAudio.BytesPerFrame;

    private static readonly AudioCaptureDevice Mik = new("Mik A", CaptureBackend.DirectShow, AudioSourceRole.Microphone);
    private static readonly AudioCaptureDevice Miks = new("Stereo Mix", CaptureBackend.DirectShow, AudioSourceRole.SystemAudio);

    private readonly ITestOutputHelper _output;

    public SistemSesiTests(ITestOutputHelper output) => _output = output;

    private static string Klasor
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "sistem-sesi");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    /// <summary>
    /// Sahte kaynak. Sesliyken kendi saatine gore 500 Hz kare dalga verir, sessizken hic veri
    /// vermez: gercek loopback sistem sustugunda boyle davranir.
    /// </summary>
    private sealed class SahteSes : ISystemAudioCapture
    {
        private readonly Stopwatch _saat = Stopwatch.StartNew();
        private readonly bool _sesli;
        private long _verilen;
        private volatile int _hal = (int)SystemAudioState.Capturing;
        private volatile bool _kapandi;

        public SahteSes(bool sesli) => _sesli = sesli;

        public bool Kapandi => _kapandi;

        public DateTime IlkOkuma { get; private set; }

        public SystemAudioState State
        {
            get => (SystemAudioState)_hal;
            set => _hal = (int)value;
        }

        public int Read(byte[] buffer)
        {
            if (IlkOkuma == default) IlkOkuma = DateTime.UtcNow;
            if (!_sesli) return 0;

            var kare = (int)Math.Min(_saat.ElapsedMilliseconds * Hiz / 1000 - _verilen, buffer.Length / LoopbackAudio.BytesPerFrame);
            for (var i = 0; i < kare; i++)
            {
                var deger = (short)((_verilen + i) / 48 % 2 == 0 ? 8000 : -8000);
                var yer = i * LoopbackAudio.BytesPerFrame;
                buffer[yer] = buffer[yer + 2] = (byte)(deger & 0xFF);
                buffer[yer + 1] = buffer[yer + 3] = (byte)((deger >> 8) & 0xFF);
            }

            _verilen += kare;
            return kare * LoopbackAudio.BytesPerFrame;
        }

        public void Dispose() => _kapandi = true;
    }

    private static async Task Bekle(Func<bool> kosul, int ms = 5000)
    {
        var saat = Stopwatch.StartNew();
        while (!kosul() && saat.ElapsedMilliseconds < ms) await Task.Delay(20);
    }

    [Fact]
    public void SessizlikteDuvarSaatiKadarSessizlikYazilir()
    {
        var cizgi = new LoopbackTimeline();

        Assert.Equal(new LoopbackStep(Hiz, 0), cizgi.Next(TimeSpan.FromSeconds(1), 0));
        Assert.Equal(new LoopbackStep(Hiz / 2, 0), cizgi.Next(TimeSpan.FromSeconds(1.5), 0));
        Assert.Equal(new LoopbackStep(0, 0), cizgi.Next(TimeSpan.FromSeconds(1.5), 0));
    }

    [Fact]
    public void YoklamaTitremesiSessizlikSayilmaz()
    {
        var cizgi = new LoopbackTimeline();

        Assert.Equal(new LoopbackStep(0, 0), cizgi.Next(TimeSpan.FromMilliseconds(50), 0));
        Assert.Equal(new LoopbackStep(0, 0), cizgi.Next(TimeSpan.FromMilliseconds(100), 0));
        Assert.Equal(new LoopbackStep(Hiz * 110 / 1000, 0), cizgi.Next(TimeSpan.FromMilliseconds(110), 0));
    }

    [Fact]
    public void VeriGelirkenKucukGecikmeAkisiDelmezBuyukBoslukDoldurulur()
    {
        var cizgi = new LoopbackTimeline();

        Assert.Equal(new LoopbackStep(0, 0), cizgi.Next(TimeSpan.FromMilliseconds(20), 480));
        Assert.Equal(new LoopbackStep(Hiz - 960, 0), cizgi.Next(TimeSpan.FromSeconds(1), 480));
        Assert.Equal(new LoopbackStep(0, 0), cizgi.Next(TimeSpan.FromSeconds(1.01), 480));
    }

    [Fact]
    public void TekTurdaEnCokBirSaniyeSessizlikYazilirKalaniSonrakiTurKapatir()
    {
        var cizgi = new LoopbackTimeline();
        var bes = TimeSpan.FromSeconds(5);
        var adimlar = Enumerable.Range(0, 6).Select(_ => cizgi.Next(bes, 0).SilenceFrames).ToArray();

        Assert.Equal(new[] { Hiz, Hiz, Hiz, Hiz, Hiz, 0 }, adimlar);
    }

    [Fact]
    public void SaatinOnuneGecenFazlaAtilir()
    {
        var cizgi = new LoopbackTimeline();

        Assert.Equal(new LoopbackStep(0, 0), cizgi.Next(TimeSpan.Zero, LoopbackTimeline.AheadToleranceFrames));
        Assert.Equal(new LoopbackStep(0, 4800), cizgi.Next(TimeSpan.Zero, 4800));
        Assert.Equal(new LoopbackStep(0, 100), cizgi.Next(TimeSpan.Zero, 100));
    }

    [Fact]
    public void NegatifKareSayisiReddedilir()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new LoopbackTimeline().Next(TimeSpan.Zero, -1));

    /// <summary>
    /// Cihaz saati duvar saatinden sapsa da (hizli ya da yavas) bir dakikanin sonunda yazilan
    /// ornek sayisi gecen surenin toleransi icinde kalir; sapma birikmez.
    /// </summary>
    [Theory]
    [InlineData(48480)]
    [InlineData(47520)]
    [InlineData(48000)]
    public void SaatiSapanCihazdaIzDuvarSaatindenKopmaz(int cihazHizi)
    {
        var cizgi = new LoopbackTimeline();
        long yazilan = 0, verilen = 0;
        var enBuyukSapma = 0L;

        for (var ms = 10; ms <= 60_000; ms += 10)
        {
            var kare = (int)((long)ms * cihazHizi / 1000 - verilen);
            verilen += kare;
            var adim = cizgi.Next(TimeSpan.FromMilliseconds(ms), kare);
            yazilan += adim.SilenceFrames + kare - adim.SkipFrames;
            enBuyukSapma = Math.Max(enBuyukSapma, Math.Abs((long)ms * Hiz / 1000 - yazilan));
        }

        Assert.InRange(enBuyukSapma, 0, LoopbackTimeline.AheadToleranceFrames + cihazHizi / 100);
    }

    [Fact]
    public void LoopbackGirdisiSabitBicimliHamPcmOkur()
    {
        Assert.Equal(
            new[] { "-f", "s16le", "-ar", "48000", "-ac", "2", "-i", LoopbackAudio.PipeToken },
            AudioCaptureArguments.InputArguments(LoopbackAudio.Device));
        Assert.Equal(AudioSourceRole.SystemAudio, LoopbackAudio.Device.Role);
        Assert.Equal(LoopbackAudio.SampleRate * LoopbackAudio.Channels * 2, SaniyeBayt);
    }

    [Fact]
    public void MikrofonlaBirlikteBugunkuKaristirmaYoluKullanilir()
    {
        var liste = new[] { Mik, Miks, LoopbackAudio.Device };
        var ikili = AudioCaptureArguments.Build(
            new AudioCaptureSelection(Mik, LoopbackAudio.Device), liste, RecorderArguments.AudioFirstInputIndex);
        var eski = AudioCaptureArguments.Build(
            new AudioCaptureSelection(Mik, Miks), liste, RecorderArguments.AudioFirstInputIndex);
        var tek = AudioCaptureArguments.Build(
            new AudioCaptureSelection(null, LoopbackAudio.Device), liste, RecorderArguments.AudioFirstInputIndex);

        Assert.Equal(2, ikili.InputCount);
        Assert.Equal(eski.FilterComplex, ikili.FilterComplex);
        Assert.Contains("amix", ikili.FilterComplex);
        Assert.Equal(eski.Maps, ikili.Maps);
        Assert.True(LoopbackAudio.Uses(ikili.Inputs));
        Assert.False(LoopbackAudio.Uses(eski.Inputs));
        Assert.False(LoopbackAudio.Uses(null));
        Assert.Equal(1, tek.InputCount);
        Assert.Equal(AudioCaptureArguments.InputArguments(LoopbackAudio.Device), tek.Inputs);
    }

    [Theory]
    [InlineData(RecorderPlatform.Windows, false)]
    [InlineData(RecorderPlatform.MacOs, true)]
    [InlineData(RecorderPlatform.Linux, true)]
    public void LoopbackYalnizWindowsIsteginGecer(RecorderPlatform platform, bool reddedilir)
    {
        var plan = AudioCaptureArguments.Build(
            new AudioCaptureSelection(null, LoopbackAudio.Device), new[] { LoopbackAudio.Device }, RecorderArguments.AudioFirstInputIndex);
        var hatalar = RecorderArguments.Validate(BolgeIstegi(plan) with { Platform = platform }, "kayit.mp4");

        Assert.Equal(reddedilir, hatalar.Any(h => h.Contains("loopback", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void KayitArgumaniBoruJetonunuBirKezTasir()
    {
        var plan = AudioCaptureArguments.Build(
            new AudioCaptureSelection(null, LoopbackAudio.Device), new[] { LoopbackAudio.Device }, RecorderArguments.AudioFirstInputIndex);
        var arguman = RecorderArguments.Build(BolgeIstegi(plan), "kayit.mp4");

        Assert.Single(arguman, a => a == LoopbackAudio.PipeToken);
        Assert.Equal("-i", arguman[arguman.ToList().IndexOf(LoopbackAudio.PipeToken) - 1]);
    }

    [Fact]
    public void WindowsListesininSonundaHerMakinedeAyniSecenekDurur()
    {
        var bos = CaptureDevices.WithLoopback(CaptureDevices.ParseDirectShow(string.Empty));
        var dolu = CaptureDevices.WithLoopback(new CaptureDeviceList(
            new[] { Mik, Miks }, Array.Empty<string>(), CaptureBackend.DirectShow, Loaded: true));

        Assert.Equal(new[] { LoopbackAudio.Device }, bos.Audio);
        Assert.Equal(new[] { Mik, Miks, LoopbackAudio.Device }, dolu.Audio);
        Assert.Single(dolu.Audio, d => d.Name == LoopbackAudio.LoopbackName);
    }

    [Fact]
    public void JetonYoksaBesleAcilmazKaynakSorulmaz()
    {
        var arguman = new List<string> { "-f", "dshow", "-i", "audio=Mik A" };
        var soruldu = false;

        var besleme = LoopbackFeed.Bind(arguman, () =>
        {
            soruldu = true;
            return null;
        });

        Assert.Null(besleme);
        Assert.False(soruldu);
        Assert.Equal(new[] { "-f", "dshow", "-i", "audio=Mik A" }, arguman);
    }

    [Fact]
    public async Task BaglananOlmazsaKapatmaBeklemeyiKeserKaynagiBirakir()
    {
        if (!OperatingSystem.IsWindows()) return;

        var ses = new SahteSes(sesli: true);
        var arguman = AudioCaptureArguments.InputArguments(LoopbackAudio.Device).ToList();
        var besleme = LoopbackFeed.Bind(arguman, () => ses)!;

        Assert.StartsWith(LoopbackAudio.PipeToken + "-", arguman[^1]);
        Assert.DoesNotContain(LoopbackAudio.PipeToken, arguman);
        Assert.Equal(SystemAudioState.Capturing, besleme.State);

        besleme.Dispose();
        await besleme.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(ses.Kapandi);
    }

    [Fact]
    public async Task KaynakAcilamazsaBoruYineBeslenirHalYokDer()
    {
        if (!OperatingSystem.IsWindows()) return;

        var arguman = AudioCaptureArguments.InputArguments(LoopbackAudio.Device).ToList();
        var besleme = LoopbackFeed.Bind(arguman, () => throw new InvalidOperationException("cihaz yok"))!;
        var (bayt, sure, sifirDisi) = await Oku(arguman[^1], SaniyeBayt / 4);
        besleme.Dispose();
        await besleme.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        _output.WriteLine($"kaynaksiz: {bayt} bayt, {sure} ms");
        Assert.Equal(SystemAudioState.Unavailable, besleme.State);
        Assert.True(bayt >= SaniyeBayt / 4);
        Assert.Equal(0, sifirDisi);
    }

    /// <summary>
    /// Sistem sessizken kaynak hic veri vermez; boru yine de duvar saati hizinda sessizlikle
    /// dolar. Veri saatten once de gelemez: yarim saniyelik ornek en az yarim saniye surer.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoruDuvarSaatiHizindaDolarOkuyanGidincePompaBiter(bool sesli)
    {
        if (!OperatingSystem.IsWindows()) return;

        var ses = new SahteSes(sesli);
        var arguman = AudioCaptureArguments.InputArguments(LoopbackAudio.Device).ToList();
        var besleme = LoopbackFeed.Bind(arguman, () => ses)!;
        ses.State = SystemAudioState.Switched;

        var (bayt, sure, sifirDisi) = await Oku(arguman[^1], SaniyeBayt / 2);
        await besleme.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        _output.WriteLine($"sesli={sesli}: {bayt} bayt, {sure} ms, sifir olmayan {sifirDisi}");
        Assert.Equal(SystemAudioState.Switched, besleme.State);
        Assert.Equal(0, bayt % LoopbackAudio.BytesPerFrame);
        Assert.InRange(sure, 400, 5000);
        Assert.Equal(sesli, sifirDisi > 0);
        Assert.True(ses.Kapandi);
    }

    [Fact]
    public void IlkKareDamgasiYalnizEkranYakalamaSatirindanOkunur()
    {
        var damga = LoopbackAudio.CaptureStart("  Duration: N/A, start: 1791630023.446262, bitrate: 73740 kb/s");

        Assert.NotNull(damga);
        Assert.Equal(DateTimeKind.Utc, damga!.Value.Kind);
        Assert.InRange((damga.Value - DateTime.UnixEpoch).TotalSeconds, 1791630023.446, 1791630023.447);
        Assert.Null(LoopbackAudio.CaptureStart("  Duration: N/A, start: 0.000000, bitrate: N/A"));
        Assert.Null(LoopbackAudio.CaptureStart("  Duration: 00:00:05.00, start: 12.500000, bitrate: 900 kb/s"));
        Assert.Null(LoopbackAudio.CaptureStart("  Stream #0:0: Video: bmp, start: 1791630023.446262, 30 fps"));
        Assert.Null(LoopbackAudio.CaptureStart("  Duration: N/A, bitrate: N/A"));
        Assert.Null(LoopbackAudio.CaptureStart("  Duration: N/A, start: bozuk, bitrate: N/A"));
        Assert.Null(LoopbackAudio.CaptureStart(null));
    }

    [Fact]
    public void OnSessizlikIlkKareIleBaglantiArasidirSinirDisiSifir()
    {
        var baglanti = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(TimeSpan.FromMilliseconds(330), LoopbackAudio.Lead(baglanti.AddMilliseconds(-330), baglanti));
        Assert.Equal(LoopbackAudio.MaxLead, LoopbackAudio.Lead(baglanti - LoopbackAudio.MaxLead, baglanti));
        Assert.Equal(TimeSpan.Zero, LoopbackAudio.Lead(baglanti - LoopbackAudio.MaxLead - TimeSpan.FromMilliseconds(1), baglanti));
        Assert.Equal(TimeSpan.Zero, LoopbackAudio.Lead(baglanti.AddMilliseconds(50), baglanti));
        Assert.Equal(TimeSpan.Zero, LoopbackAudio.Lead(baglanti, baglanti));
        Assert.Equal(TimeSpan.Zero, LoopbackAudio.Lead(null, baglanti));
    }

    /// <summary>
    /// Goruntu boru baglanmadan bir saniye once basladiysa ses izinin basina o kadar sessizlik
    /// hemen yazilir: sesli kaynakta bile ilk yarim saniye sifirdir ve duvar saatini beklemez.
    /// Damgasiz es <see cref="BoruDuvarSaatiHizindaDolarOkuyanGidincePompaBiter"/>'tir.
    /// </summary>
    [Fact]
    public async Task IlkKareDamgasiSesinBasinaSessizlikKoyar()
    {
        if (!OperatingSystem.IsWindows()) return;

        var ses = new SahteSes(sesli: true);
        var arguman = AudioCaptureArguments.InputArguments(LoopbackAudio.Device).ToList();
        var besleme = LoopbackFeed.Bind(arguman, () => ses)!;
        var once = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds - 1;
        besleme.Anchor("  Stream #0:0: Video: bmp, 30 fps");
        besleme.Anchor(FormattableString.Invariant($"  Duration: N/A, start: {once:F6}, bitrate: 73740 kb/s"));
        besleme.Anchor("  Duration: N/A, start: 1000000001.000000, bitrate: 1 kb/s");

        var (bayt, sure, sifirDisi) = await Oku(arguman[^1], SaniyeBayt / 2);
        await besleme.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        _output.WriteLine($"damgali: {bayt} bayt, {sure} ms, sifir olmayan {sifirDisi}");
        Assert.InRange(sure, 0, 300);
        Assert.Equal(0, sifirDisi);
    }

    private static async Task<(int Bayt, long Sure, int SifirDisi)> Oku(string boru, int istenen)
    {
        using var iptal = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var saat = Stopwatch.StartNew();
        await using var istemci = new NamedPipeClientStream(".", Path.GetFileName(boru), PipeDirection.In, PipeOptions.Asynchronous);
        await istemci.ConnectAsync(iptal.Token);

        var tampon = new byte[SaniyeBayt];
        int toplam = 0, sifirDisi = 0;
        while (toplam < istenen)
        {
            var okunan = await istemci.ReadAsync(tampon.AsMemory(), iptal.Token);
            if (okunan == 0) break;
            sifirDisi += tampon.AsSpan(0, okunan).ToArray().Count(b => b != 0);
            toplam += okunan;
        }

        return (toplam, saat.ElapsedMilliseconds, sifirDisi);
    }

    /// <summary>
    /// Gercek ffmpeg boruyu kaydedicinin urettigi argumanla okur. Kaynak sessizken de bir
    /// saniyelik iz cikar: besleme durmaz, ffmpeg ses girdisinde beklemez.
    /// </summary>
    [FfmpegFact]
    public async Task FfmpegSessizKaynaktanBirSaniyelikIzYazar()
    {
        if (!OperatingSystem.IsWindows()) return;

        const string ad = "sessiz.wav";
        KanitKapanisi.Onceki(Klasor, ad);
        var cikti = Path.Combine(Klasor, ad);
        var ses = new SahteSes(sesli: false);
        var arguman = new List<string> { "-hide_banner", "-y" };
        arguman.AddRange(AudioCaptureArguments.InputArguments(LoopbackAudio.Device));
        arguman.AddRange(new[] { "-t", "1", cikti });
        var besleme = LoopbackFeed.Bind(arguman, () => ses)!;

        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in arguman) psi.ArgumentList.Add(a);

        using var surec = Process.Start(psi)!;
        var cikis = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        using var iptal = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await surec.WaitForExitAsync(iptal.Token);
        await cikis;
        var stderr = await hata;
        besleme.Dispose();
        await besleme.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(surec.ExitCode == 0, stderr);
        Assert.InRange(new FileInfo(cikti).Length, SaniyeBayt, SaniyeBayt + 512);
        Assert.True(ses.Kapandi);

        KanitKapanisi.Kapat(Klasor, ad);
    }

    /// <summary>Seviye cubugu ayni boruyu kullanir: sahte kaynagin tonu olculur, kapaninca kaynak birakilir.</summary>
    [FfmpegFact]
    public async Task SeviyeOlcumuLoopbackKaynagindanOkur()
    {
        if (!OperatingSystem.IsWindows()) return;

        var ses = new SahteSes(sesli: true);
        var enYuksek = double.NegativeInfinity;
        var kilit = new object();
        var kaynak = AudioLevelSource.Start(ToolLocator.Ffmpeg, AudioLevel.Arguments(LoopbackAudio.Device), () => ses);
        kaynak.Level += db =>
        {
            lock (kilit) enYuksek = Math.Max(enYuksek, db);
        };

        await Bekle(() =>
        {
            lock (kilit) return enYuksek > -20;
        }, 10_000);
        kaynak.Dispose();
        await Bekle(() => ses.Kapandi);

        _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"en yuksek seviye: {enYuksek:F1} dB"));
        Assert.InRange(enYuksek, -20, 0);
        Assert.True(ses.Kapandi);
    }

    /// <summary>
    /// Uctan uca: kaydedici oturumu sahte kaynakla dort saniye kaydeder, dosyada goruntu ve ses
    /// akisi birlikte cikar, oturum kapaninca kaynak birakilir.
    /// </summary>
    [KayitFact]
    public async Task KayitSistemSesiyleIkiAkisUretirKaynagiBirakir()
    {
        const string ad = "sistem-sesi.mp4";
        const string kanit = "sistem-sesi.ffprobe.txt";
        KanitKapanisi.Onceki(KayitKanit.Folder, ad, kanit);
        var cikti = KayitKanit.Path_(ad);
        var ses = new SahteSes(sesli: true);
        var plan = AudioCaptureArguments.Build(
            new AudioCaptureSelection(null, LoopbackAudio.Device), new[] { LoopbackAudio.Device }, RecorderArguments.AudioFirstInputIndex);

        var once = DateTime.UtcNow;
        var oturum = await RecorderSession.StartAsync(BolgeIstegi(plan), cikti, systemAudio: () => ses);
        var hal = oturum.SystemAudio;
        await Task.Delay(4000);
        var durdurma = DateTime.UtcNow;
        var sonuc = await oturum.StopAsync();

        var (kod, metin) = KayitKanit.Ffprobe(cikti, kanit);
        var sureler = AkisSureleri(cikti);
        var ilkSes = IlkSesAni(cikti);
        _output.WriteLine(metin);
        _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"dosyada ilk ses: {ilkSes:F3} sn"));
        _output.WriteLine(string.Join(Environment.NewLine, sureler.Select(s => string.Create(CultureInfo.InvariantCulture, $"{s.Key}: {s.Value:F3} sn"))));
        _output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"durdurmadan geriye ilk kare: +{(durdurma - once).TotalMilliseconds - sureler["video"] * 1000:F0} ms, ilk ses: +{(durdurma - once).TotalMilliseconds - sureler["audio"] * 1000:F0} ms, boru baglantisi: +{(ses.IlkOkuma - once).TotalMilliseconds:F0} ms"));

        Assert.True(sonuc.Ok, $"sistem sesli kayit 0 ile kapanmali; stderr: {sonuc.StandardError}");
        Assert.False(sonuc.Partial);
        Assert.Equal(0, kod);
        Assert.Equal(SystemAudioState.Capturing, hal);
        Assert.Null(oturum.SystemAudio);
        Assert.Contains("codec_type=video", metin);
        Assert.Contains("codec_type=audio", metin);
        Assert.InRange(Math.Abs(sureler["audio"] - sureler["video"]), 0, 1.0);
        Assert.InRange(ilkSes, 0.05, LoopbackAudio.MaxLead.TotalSeconds);
        Assert.True(ses.Kapandi);

        KayitKanit.Kapat(ad, kanit);
    }

    private static Dictionary<string, double> AkisSureleri(string dosya)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffprobe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in new[] { "-v", "error", "-show_entries", "stream=codec_type,duration", "-of", "csv=p=0", dosya })
            psi.ArgumentList.Add(a);

        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var metin = surec.StandardOutput.ReadToEnd();
        surec.WaitForExit(20_000);
        hata.Wait(5000);

        var sonuc = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var satir in metin.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parca = satir.Split(',');
            if (parca.Length >= 2 && double.TryParse(parca[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var sure))
                sonuc[parca[0]] = sure;
        }

        return sonuc;
    }

    /// <summary>Dosyanin ses izinde ilk duyulur ornegin ani (saniye); hic yoksa eksi bir.</summary>
    private static double IlkSesAni(string dosya)
    {
        const int hiz = 8000;
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in new[] { "-v", "error", "-threads", "2", "-i", dosya, "-vn", "-ac", "1", "-ar", "8000", "-f", "s16le", "-" })
            psi.ArgumentList.Add(a);

        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        using var bellek = new MemoryStream();
        surec.StandardOutput.BaseStream.CopyTo(bellek);
        surec.WaitForExit(20_000);
        hata.Wait(5000);

        var ham = bellek.ToArray();
        for (var i = 0; i + 1 < ham.Length; i += 2)
        {
            if (Math.Abs(BitConverter.ToInt16(ham, i)) > 1000) return i / 2 / (double)hiz;
        }

        return -1;
    }

    private static RecorderRequest BolgeIstegi(AudioCapturePlan plan) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Region = new RecorderRegion(0, 0, 640, 480),
        Fps = 15,
        Preset = "ultrafast",
        Audio = plan
    };

    private static T Kur<T>(Func<RecorderView, T> olc)
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        return AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol)
            {
                ListAudioDevices = () => new[] { Mik, Miks, LoopbackAudio.Device },
                OpenLevelSource = _ => null
            };
            var pencere = new Window { Width = 1536, Height = 832, ShowActivated = false };
            pencere.Content = view;
            pencere.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                return olc(view);
            }
            finally
            {
                Strings.Use("en");
                pencere.Content = null;
                pencere.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    private static string Yazi(string anahtar) => LanguageCatalog.Display(Strings.Get(anahtar));

    /// <summary>
    /// Secenek her dilde kendi yazisiyla gorunur, ama secim ve saklanan ad dilden bagimsizdir:
    /// dil degisince secim kaybolmaz, secilen cihaz hep ayni sabit adi tasir.
    /// </summary>
    [Fact]
    public void SecenekCevriliGorunurSecimDilDegisinceKaybolmaz()
    {
        var o = Kur(view =>
        {
            Strings.Use("tr");
            view.RefreshAudioBoxes();
            var trOgeler = ((IEnumerable<string>)view.CmbSystemAudio.ItemsSource!).ToArray();
            var trBeklenen = Yazi("recorder.audio.loopback");
            view.CmbSystemAudio.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();

            Strings.Use("en");
            view.RefreshAudioBoxes();
            var enOgeler = ((IEnumerable<string>)view.CmbSystemAudio.ItemsSource!).ToArray();
            return (
                TrOgeler: trOgeler, TrBeklenen: trBeklenen, EnOgeler: enOgeler, EnBeklenen: Yazi("recorder.audio.loopback"),
                Secili: view.CmbSystemAudio.SelectedIndex, Cihaz: view.Chosen(AudioSourceRole.SystemAudio),
                Mikrofon: ((IEnumerable<string>)view.CmbMicrophone.ItemsSource!).ToArray());
        });

        Assert.Equal(3, o.TrOgeler.Length);
        Assert.Equal("Stereo Mix", o.TrOgeler[1]);
        Assert.Equal(o.TrBeklenen, o.TrOgeler[2]);
        Assert.Equal(o.EnBeklenen, o.EnOgeler[2]);
        Assert.NotEqual(o.TrBeklenen, o.EnBeklenen);
        Assert.Equal(2, o.Secili);
        Assert.Same(LoopbackAudio.Device, o.Cihaz);
        Assert.DoesNotContain(o.EnBeklenen, o.Mikrofon);
        Assert.Equal(LoopbackAudio.LoopbackName, RecorderView.AudioLabel(LoopbackAudio.LoopbackName) == o.EnBeklenen ? LoopbackAudio.Device.Name : null);
        Assert.Equal("Stereo Mix", RecorderView.AudioLabel("Stereo Mix"));
    }

    /// <summary>
    /// Cikis cihazi degisince ya da kaybolunca kayit surer, durum satiri ne oldugunu soyler;
    /// kaynak ilk cihazina donunce satir kalkar. Ayni hal ikinci kez yazilmaz.
    /// </summary>
    [Fact]
    public void DurumSatiriCihazDegisiminiVeKaybiniSoyler()
    {
        var o = Kur(view =>
        {
            view.ShowSystemAudio(null);
            var bos = view.NoticeText;
            view.ShowSystemAudio(SystemAudioState.Capturing);
            var yakaliyor = view.NoticeText;
            view.ShowSystemAudio(SystemAudioState.Switched);
            var degisti = view.NoticeText;
            view.ShowSystemAudio(SystemAudioState.Unavailable);
            var kayip = view.NoticeText;
            view.ShowSystemAudio(SystemAudioState.Capturing);
            var dondu = view.NoticeText;
            return (
                Bos: bos, Yakaliyor: yakaliyor, Degisti: degisti, Kayip: kayip, Dondu: dondu,
                DegistiBeklenen: Yazi("recorder.audio.loopback.switched"), KayipBeklenen: Yazi("recorder.audio.loopback.lost"));
        });

        Assert.Equal(string.Empty, o.Bos);
        Assert.Equal(string.Empty, o.Yakaliyor);
        Assert.Equal(o.DegistiBeklenen, o.Degisti);
        Assert.Equal(o.KayipBeklenen, o.Kayip);
        Assert.NotEqual(o.Degisti, o.Kayip);
        Assert.Equal(string.Empty, o.Dondu);
        Assert.Null(RecorderView.AudioStateKey(SystemAudioState.Capturing));
    }

    /// <summary>Uc yeni metin her dilde dolu, iki durum cumlesi ayni dilde birbirinden ayri.</summary>
    [Fact]
    public void SistemSesiMetinleriButunDillerde()
    {
        string[] anahtarlar = ["recorder.audio.loopback", "recorder.audio.loopback.switched", "recorder.audio.loopback.lost"];

        foreach (var dil in Locales.Languages)
        {
            var metin = Locales.Values(dil);
            foreach (var anahtar in anahtarlar)
                Assert.False(string.IsNullOrWhiteSpace(metin.GetValueOrDefault(anahtar)), dil + " " + anahtar);

            Assert.NotEqual(metin["recorder.audio.loopback.switched"], metin["recorder.audio.loopback.lost"]);
        }
    }
}
