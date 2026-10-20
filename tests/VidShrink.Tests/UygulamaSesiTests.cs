using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Later 105, uygulama başına ses: Windows 10 2004 (19041) ve üstünde ses çalan programlar
/// sistem sesi kutusunda ayrı seçenek olur, kayıt <c>ActivateAudioInterfaceAsync</c> süreç geri
/// döngüsüyle o programın ağacını okur. Ölçü gerçek ses cihazına dokunmaz: sürüm kapısı, liste
/// birleşimi, jeton, etkinleştirme baytları ve borunun sahte kaynakla beslenmesi saf ölçülür.
/// Desteklenmeyen sistemde seçenek gizlenmez, kapalı görünür ve ipucu sebebini söyler.
/// </summary>
public sealed class UygulamaSesiTests
{
    private static readonly AudioCaptureDevice Mik = new("Mik A", CaptureBackend.DirectShow, AudioSourceRole.Microphone);

    [Theory]
    [InlineData(true, 10, 0, 19041, true)]
    [InlineData(true, 10, 0, 22631, true)]
    [InlineData(true, 10, 0, 19040, false)]
    [InlineData(true, 10, 0, 18363, false)]
    [InlineData(true, 6, 3, 9600, false)]
    [InlineData(true, 11, 0, 0, true)]
    [InlineData(false, 10, 0, 22631, false)]
    public void SurumKapisi2004veUstuWindowsIster(bool windows, int major, int minor, int build, bool beklenen)
        => Assert.Equal(beklenen, AppAudio.Supported(windows, new Version(major, minor, build)));

    [Fact]
    public void SurumYoksaDesteklenmez() => Assert.False(AppAudio.Supported(true, null));

    [Fact]
    public void DesteklenmeyenSistemdeYerTutucuSonaEklenirUygulamaListelenmez()
    {
        var eski = new[] { Mik, LoopbackAudio.Device, AppAudio.For("eski"), AppAudio.Placeholder };
        var liste = AppAudio.Compose(eski, false, [new AppAudioSession(42, "spotify", true)], 1, AppAudio.For("chrome").Name);

        Assert.Equal(new[] { Mik, LoopbackAudio.Device, AppAudio.Placeholder }, liste);
    }

    [Fact]
    public void DesteklenenSistemdeOturumlarTekilSiraliKendiSureciHaric()
    {
        var oturumlar = new[]
        {
            new AppAudioSession(42, "Spotify", true),
            new AppAudioSession(43, "spotify", false),
            new AppAudioSession(7, "VidShrink", true),
            new AppAudioSession(0, "Idle", true),
            new AppAudioSession(50, "chrome", false),
            new AppAudioSession(51, "kotu\\ad", true),
            new AppAudioSession(52, " ", true)
        };

        var liste = AppAudio.Compose([Mik, LoopbackAudio.Device, AppAudio.Placeholder], true, oturumlar, 7, AppAudio.For("discord").Name);

        Assert.Equal(new[] { "Mik A", LoopbackAudio.LoopbackName, AppAudio.NamePrefix + "chrome", AppAudio.NamePrefix + "discord", AppAudio.NamePrefix + "Spotify" },
            liste.Select(d => d.Name));
        Assert.All(liste.Skip(2), d =>
        {
            Assert.Equal(CaptureBackend.WasapiLoopback, d.Backend);
            Assert.Equal(AudioSourceRole.SystemAudio, d.Role);
            Assert.Equal(AppAudio.Token(AppAudio.ProcessName(d)!), d.Reference);
        });
    }

    [Fact]
    public void JetonSurecAdiniTasirTamJetonSistemSesidir()
    {
        Assert.True(AppAudio.TryReadToken(LoopbackAudio.PipeToken, out var sistem));
        Assert.Null(sistem);
        Assert.True(AppAudio.TryReadToken(AppAudio.Token("spotify"), out var ad));
        Assert.Equal("spotify", ad);
        Assert.False(AppAudio.TryReadToken(LoopbackAudio.PipeToken + "-baska", out _));
        Assert.False(AppAudio.TryReadToken(LoopbackAudio.PipeToken + AppAudio.TokenInfix, out _));
        Assert.False(AppAudio.TryReadToken("audio=Mik A", out _));
        Assert.False(AppAudio.TryReadToken(null, out _));
        Assert.Throws<ArgumentException>(() => AppAudio.For("a/b"));
        Assert.Equal("spotify", AppAudio.ProcessName(AppAudio.For("spotify")));
        Assert.Null(AppAudio.ProcessName(LoopbackAudio.Device));
    }

    [Fact]
    public void UygulamaGirdisiBoruyuOkurKullanimdaSayilir()
    {
        var arguman = AudioCaptureArguments.InputArguments(AppAudio.For("spotify"));

        Assert.Equal(AppAudio.Token("spotify"), arguman[^1]);
        Assert.Contains("s16le", arguman);
        Assert.True(LoopbackAudio.Uses(arguman));
        Assert.False(LoopbackAudio.Uses(AudioCaptureArguments.InputArguments(Mik)));
    }

    [Fact]
    public void SurecSecimiCalaniOnceAlir()
    {
        var adaylar = new[]
        {
            new AppAudioSession(10, "chrome", false),
            new AppAudioSession(11, "Chrome", true),
            new AppAudioSession(0, "chrome", true),
            new AppAudioSession(12, "spotify", true)
        };

        Assert.Equal(11, AppAudio.PickProcess("chrome", adaylar));
        Assert.Equal(10, AppAudio.PickProcess("chrome", adaylar.Where(a => a.ProcessId != 11)));
        Assert.Null(AppAudio.PickProcess("discord", adaylar));
    }

    [Fact]
    public void EtkinlestirmeParametresiSurecGeriDongusuAgaciDahil()
    {
        var bayt = AppAudio.ActivationParams(0x01020304);

        Assert.Equal(new byte[] { 1, 0, 0, 0, 4, 3, 2, 1, 0, 0, 0, 0 }, bayt);
        Assert.Throws<ArgumentOutOfRangeException>(() => AppAudio.ActivationParams(0));
    }

    [Fact]
    public void UygulamaJetonuUygulamaKaynaginiAdlaAcar()
    {
        var arguman = AudioCaptureArguments.InputArguments(AppAudio.For("spotify")).ToList();
        string? istenen = null;
        var sistemSoruldu = false;

        var besleme = LoopbackFeed.Bind(arguman, () => { sistemSoruldu = true; return null; }, ad => { istenen = ad; return null; });
        try
        {
            Assert.NotNull(besleme);
            Assert.Equal("spotify", istenen);
            Assert.False(sistemSoruldu);
            Assert.StartsWith(LoopbackAudio.PipeToken + "-", arguman[^1]);
            Assert.False(AppAudio.TryReadToken(arguman[^1], out var kalan) && kalan is not null);
            Assert.Equal(SystemAudioState.Unavailable, besleme!.State);
        }
        finally
        {
            besleme?.Dispose();
        }

        var sistem = AudioCaptureArguments.InputArguments(LoopbackAudio.Device).ToList();
        istenen = null;
        using (LoopbackFeed.Bind(sistem, () => { sistemSoruldu = true; return null; }, ad => { istenen = ad; return null; }))
        {
            Assert.True(sistemSoruldu);
            Assert.Null(istenen);
        }
    }

    [Fact]
    public async Task UygulamaKaynagininVerisiBorudanAkar()
    {
        if (!OperatingSystem.IsWindows()) return;

        var ses = new SabitSes();
        var arguman = AudioCaptureArguments.InputArguments(AppAudio.For("spotify")).ToList();
        var besleme = LoopbackFeed.Bind(arguman, () => null, _ => ses)!;

        var sifirDisi = 0;
        using (var istemci = new NamedPipeClientStream(".", arguman[^1][@"\\.\pipe\".Length..], PipeDirection.In))
        {
            await istemci.ConnectAsync(5000);
            var tampon = new byte[LoopbackAudio.SampleRate * LoopbackAudio.BytesPerFrame / 4];
            var toplam = 0;
            while (toplam < tampon.Length)
            {
                var n = await istemci.ReadAsync(tampon.AsMemory(toplam));
                if (n == 0) break;
                toplam += n;
            }

            sifirDisi = tampon.Take(toplam).Count(b => b != 0);
        }

        await besleme.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(sifirDisi > 0);
        Assert.Equal(SystemAudioState.Capturing, besleme.State);
        Assert.True(ses.Kapandi);
    }

    [Fact]
    public void KutudaUygulamaEtiketliDesteksizdeKapaliVeIpucluGorunur()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var o = AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol)
            {
                ListAudioDevices = () => [Mik, LoopbackAudio.Device, AppAudio.For("spotify"), AppAudio.Placeholder],
                OpenLevelSource = _ => null
            };
            var pencere = new Window { Width = 1536, Height = 832, ShowActivated = false, Content = view };
            pencere.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                Strings.Use("tr");
                view.RefreshAudioBoxes();
                var tr = ((IEnumerable<string>)view.CmbSystemAudio.ItemsSource!).ToArray();
                view.CmbSystemAudio.IsDropDownOpen = true;
                Dispatcher.UIThread.RunJobs();
                var yerTutucu = view.CmbSystemAudio.ContainerFromIndex(3) as ComboBoxItem;
                var uygulama = view.CmbSystemAudio.ContainerFromIndex(2) as ComboBoxItem;
                var kapali = yerTutucu?.IsEnabled;
                var ipucu = yerTutucu is null ? null : ToolTip.GetTip(yerTutucu) as string;
                var ipucuKapalida = yerTutucu is not null && ToolTip.GetShowOnDisabled(yerTutucu);
                var uygulamaAcik = uygulama?.IsEnabled;
                view.CmbSystemAudio.IsDropDownOpen = false;

                view.CmbSystemAudio.SelectedIndex = 2;
                Dispatcher.UIThread.RunJobs();
                var secilen = view.Chosen(AudioSourceRole.SystemAudio);
                view.CmbSystemAudio.SelectedIndex = 3;
                Dispatcher.UIThread.RunJobs();
                var yerTutucuSecili = view.Chosen(AudioSourceRole.SystemAudio);

                Strings.Use("en");
                view.RefreshAudioBoxes();
                var en = ((IEnumerable<string>)view.CmbSystemAudio.ItemsSource!).ToArray();
                return (tr, en, kapali, ipucu, ipucuKapalida, uygulamaAcik, secilen, yerTutucuSecili,
                    TrIpucu: Locales.Values("tr")[RecorderView.AppAudioRequiresKey]);
            }
            finally
            {
                Strings.Use("en");
                pencere.Content = null;
                pencere.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });

        Assert.Equal(4, o.tr.Length);
        Assert.Equal("Uygulama: spotify", o.tr[2]);
        Assert.Equal("Uygulama Sesi", o.tr[3]);
        Assert.Equal("App: spotify", o.en[2]);
        Assert.False(o.kapali);
        Assert.Equal(LanguageCatalog.Display(o.TrIpucu), o.ipucu);
        Assert.True(o.ipucuKapalida);
        Assert.True(o.uygulamaAcik);
        Assert.Equal(AppAudio.For("spotify"), o.secilen);
        Assert.Null(o.yerTutucuSecili);
    }

    [Fact]
    public void UygulamaSesiMetinleriButunDillerde()
    {
        string[] anahtarlar = ["recorder.audio.app", "recorder.audio.app.off", RecorderView.AppAudioRequiresKey];

        foreach (var dil in Locales.Languages)
        {
            var metin = Locales.Values(dil);
            foreach (var anahtar in anahtarlar)
                Assert.False(string.IsNullOrWhiteSpace(metin.GetValueOrDefault(anahtar)), dil + " " + anahtar);
            Assert.Contains("{0}", metin["recorder.audio.app"]);
            Assert.Contains("2004", metin[RecorderView.AppAudioRequiresKey]);
        }
    }

    private sealed class SabitSes : ISystemAudioCapture
    {
        private readonly System.Diagnostics.Stopwatch _saat = System.Diagnostics.Stopwatch.StartNew();
        private long _verilen;

        public bool Kapandi { get; private set; }

        public SystemAudioState State => SystemAudioState.Capturing;

        public int Read(byte[] buffer)
        {
            var kare = (int)Math.Min(_saat.ElapsedMilliseconds * LoopbackAudio.SampleRate / 1000 - _verilen, buffer.Length / LoopbackAudio.BytesPerFrame);
            if (kare <= 0) return 0;
            Array.Fill(buffer, (byte)0x40, 0, kare * LoopbackAudio.BytesPerFrame);
            _verilen += kare;
            return kare * LoopbackAudio.BytesPerFrame;
        }

        public void Dispose() => Kapandi = true;
    }
}
