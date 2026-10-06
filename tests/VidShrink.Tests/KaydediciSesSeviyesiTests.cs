using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class KaydediciSesSeviyesiTests
{
    private static readonly AudioCaptureDevice MikA = new("Mik A", CaptureBackend.DirectShow, AudioSourceRole.Microphone);
    private static readonly AudioCaptureDevice MikB = new("Mik B: USB", CaptureBackend.DirectShow, AudioSourceRole.Microphone);
    private static readonly AudioCaptureDevice Miks = new("Stereo Mix", CaptureBackend.DirectShow, AudioSourceRole.SystemAudio);

    private static readonly string[] Anahtarlar =
    {
        "recorder.audio.level.microphone", "recorder.audio.level.system", "recorder.audio.level.hint"
    };

    private readonly ITestOutputHelper _output;

    public KaydediciSesSeviyesiTests(ITestOutputHelper output) => _output = output;

    private sealed class SahteKaynak : IAudioLevelSource
    {
        public SahteKaynak(AudioCaptureDevice cihaz) => Cihaz = cihaz;

        public AudioCaptureDevice Cihaz { get; }

        public bool Kapandi { get; private set; }

        public event Action<double>? Level;

        public event Action? Ended;

        public void Ver(double db)
        {
            Level?.Invoke(db);
            Dispatcher.UIThread.RunJobs();
        }

        public void Bitir()
        {
            Ended?.Invoke();
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Kapandi = true;
    }

    private static T Kur<T>(Func<RecorderView, Window, List<SahteKaynak>, T> olc, bool hareketsiz = false, bool kaynakVar = true, bool gecisli = false)
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        return AppHost.Run(() =>
        {
            var acilan = new List<SahteKaynak>();
            var view = new RecorderView(ayar.Yol)
            {
                ListAudioDevices = () => new[] { MikA, MikB, Miks }
            };
            view.OpenLevelSource = cihaz =>
            {
                if (!kaynakVar) return null;
                var kaynak = new SahteKaynak(cihaz);
                acilan.Add(kaynak);
                return kaynak;
            };
            var pencere = new Window { Width = 1536, Height = 832, ShowActivated = false };
            if (hareketsiz) pencere.Classes.Add("reduced-motion");
            pencere.Content = view;
            pencere.Show();
            Dispatcher.UIThread.RunJobs();
            if (!gecisli)
            {
                view.BarMicrophoneLevel.Transitions = null;
                view.BarSystemAudioLevel.Transitions = null;
            }
            try
            {
                return olc(view, pencere, acilan);
            }
            finally
            {
                pencere.Content = null;
                pencere.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    private static void Sec(RecorderView view, ComboBox kutu, int sira)
    {
        kutu.SelectedIndex = sira;
        Dispatcher.UIThread.RunJobs();
    }

    [Theory]
    [InlineData(32768, 0.0)]
    [InlineData(32767, -0.0003)]
    [InlineData(16384, -6.0206)]
    [InlineData(4096, -18.0618)]
    [InlineData(33, -59.94)]
    public void TepeGenligiDbfsOlur(int tepe, double beklenen)
    {
        Assert.Equal(beklenen, AudioLevel.Db(tepe), 2);
        Assert.True(double.IsNegativeInfinity(AudioLevel.Db(0)));
        Assert.True(AudioLevel.Db(tepe) > AudioLevel.Db(tepe / 2));
    }

    [Fact]
    public void OranSessizlikEsigininAltindaSifirUstundeDogrusal()
    {
        Assert.Equal(0, AudioLevel.Fraction(double.NegativeInfinity));
        Assert.Equal(0, AudioLevel.Fraction(double.NaN));
        Assert.Equal(0, AudioLevel.Fraction(AudioLevel.FloorDb));
        Assert.Equal(0, AudioLevel.Fraction(AudioLevel.SilenceDb));
        Assert.Equal(10.1 / 60, AudioLevel.Fraction(-49.9), 6);
        Assert.Equal(0.5, AudioLevel.Fraction(-30), 6);
        Assert.Equal(0.9, AudioLevel.Fraction(-6), 6);
        Assert.Equal(1, AudioLevel.Fraction(0));
        Assert.Equal(1, AudioLevel.Fraction(3));

        Assert.True(AudioLevel.IsSilent(-50));
        Assert.True(AudioLevel.IsSilent(double.NaN));
        Assert.False(AudioLevel.IsSilent(-49.9));
    }

    [Fact]
    public void TepeHemenYukselirTutulurSonraSabitHizlaDuser()
    {
        var adim = AudioLevel.WindowSeconds;
        var durum = AudioLevel.Advance(AudioLevelHold.Silent, -6, adim);
        Assert.Equal(-6, durum.Db);
        Assert.Equal(0, durum.HeldSeconds);

        var tutulan = (int)Math.Round(AudioLevel.HoldSeconds / adim);
        for (var i = 0; i < tutulan; i++)
        {
            durum = AudioLevel.Advance(durum, double.NegativeInfinity, adim);
            Assert.Equal(-6, durum.Db);
        }

        var dusen = AudioLevel.Advance(durum, double.NegativeInfinity, adim);
        Assert.Equal(-6 - AudioLevel.FallDbPerSecond * adim, dusen.Db, 6);
        Assert.True(dusen.Db < durum.Db);

        var olculenUstte = AudioLevel.Advance(durum, -6.5, adim);
        Assert.Equal(-6.5, olculenUstte.Db, 6);

        var yukselen = AudioLevel.Advance(dusen, -3, adim);
        Assert.Equal(-3, yukselen.Db);
        Assert.Equal(0, yukselen.HeldSeconds);

        var taban = AudioLevelHold.Silent;
        for (var i = 0; i < 200; i++) taban = AudioLevel.Advance(taban, double.NaN, adim);
        Assert.Equal(AudioLevel.FloorDb, taban.Db);
        Assert.Equal(0, AudioLevel.Advance(AudioLevelHold.Silent, 12, adim).Db);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(801)]
    [InlineData(1600)]
    public void PencereOrnekOrtasindanBolunseDeAyniTepeyiVerir(int obek)
    {
        var bayt = new byte[AudioLevel.WindowSamples * 2 * 2];
        var orta = 123 * 2;
        bayt[orta] = 0x00;
        bayt[orta + 1] = 0xC0;

        var pencere = new AudioLevelWindow();
        var cikan = new List<double>();
        for (var i = 0; i < bayt.Length; i += obek)
            pencere.Add(bayt.AsSpan(i, Math.Min(obek, bayt.Length - i)), cikan.Add);

        Assert.Equal(2, cikan.Count);
        Assert.Equal(-6.0206, cikan[0], 3);
        Assert.True(double.IsNegativeInfinity(cikan[1]));

        var eksik = new List<double>();
        new AudioLevelWindow().Add(new byte[AudioLevel.WindowSamples * 2 - 1], eksik.Add);
        Assert.Empty(eksik);
    }

    [Fact]
    public void ArgumanlarTekKanalHamAkisTekIsParcacigiVeKisaTampon()
    {
        var dshow = AudioLevel.Arguments(MikB).ToList();
        _output.WriteLine(string.Join(' ', dshow));

        Assert.Equal(new[] { "-threads", "1" }, dshow.Skip(dshow.IndexOf("-threads")).Take(2));
        Assert.Equal(new[] { "-f", "dshow", "-audio_buffer_size", AudioLevel.DirectShowBufferMs, "-i", @"audio=Mik B\: USB" },
            dshow.Skip(dshow.IndexOf("-f")).Take(6));
        Assert.Equal(new[] { "-ac", "1", "-ar", "8000" }, dshow.Skip(dshow.IndexOf("-ac")).Take(4));
        Assert.Equal(new[] { "-f", "s16le", "pipe:1" }, dshow.TakeLast(3));
        Assert.Contains("-nostdin", dshow);
        Assert.True(dshow.IndexOf("-audio_buffer_size") < dshow.IndexOf("-i"));

        var pulse = AudioLevel.Arguments(new AudioCaptureDevice("alsa.monitor", CaptureBackend.PulseAudio, AudioSourceRole.SystemAudio, "alsa.monitor"));
        Assert.DoesNotContain("-audio_buffer_size", pulse);
        Assert.Equal(new[] { "-f", "pulse", "-i", "alsa.monitor" }, pulse.Skip(pulse.ToList().IndexOf("-f")).Take(4));
        Assert.Equal(new[] { "-f", "s16le", "pipe:1" }, pulse.TakeLast(3));
    }

    [Fact]
    public void TestKonagindaGercekOkuyucuKapali()
    {
        Assert.True(AudioLevelSource.Disabled);
        Assert.Null(AudioLevelSource.Open(MikA));

        using var ayar = new KaydediciAyarTests.OzelAyar();
        var gorunur = AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol) { ListAudioDevices = () => new[] { MikA } };
            var pencere = new Window { Width = 1536, Height = 832, ShowActivated = false, Content = view };
            pencere.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                view.RefreshAudioBoxes();
                Sec(view, view.CmbMicrophone, 1);
                return (Secili: view.Chosen(AudioSourceRole.Microphone), Cubuk: view.LevelVisible(AudioSourceRole.Microphone), Kaynak: view.LevelSource(AudioSourceRole.Microphone));
            }
            finally
            {
                pencere.Content = null;
                pencere.Close();
            }
        });

        Assert.Equal(MikA, gorunur.Secili);
        Assert.False(gorunur.Cubuk);
        Assert.Null(gorunur.Kaynak);
    }

    [Fact]
    public void GirisSeciliDegilkenGostergeYokSecilinceYalnizOKutununCubuguGorunur()
    {
        var o = Kur((view, _, acilan) =>
        {
            view.RefreshAudioBoxes();
            var bos = (Mik: view.LevelVisible(AudioSourceRole.Microphone), Sis: view.LevelVisible(AudioSourceRole.SystemAudio), Acilan: acilan.Count);

            Sec(view, view.CmbMicrophone, 1);
            var secili = (Mik: view.LevelVisible(AudioSourceRole.Microphone), Sis: view.LevelVisible(AudioSourceRole.SystemAudio), Acilan: acilan.Count, Cihaz: acilan[0].Cihaz);

            acilan[0].Ver(-6);
            var dolu = view.LevelValue(AudioSourceRole.Microphone);
            acilan[0].Ver(-70);
            var tutulan = view.LevelValue(AudioSourceRole.Microphone);
            for (var i = 0; i < 200; i++) acilan[0].Ver(double.NegativeInfinity);
            var sessiz = view.LevelValue(AudioSourceRole.Microphone);

            Sec(view, view.CmbMicrophone, 0);
            return (bos, secili, dolu, tutulan, sessiz,
                Sonra: view.LevelVisible(AudioSourceRole.Microphone), Kapandi: acilan[0].Kapandi, Toplam: acilan.Count,
                SonDeger: view.LevelValue(AudioSourceRole.Microphone));
        });

        Assert.False(o.bos.Mik);
        Assert.False(o.bos.Sis);
        Assert.Equal(0, o.bos.Acilan);

        Assert.True(o.secili.Mik);
        Assert.False(o.secili.Sis);
        Assert.Equal(1, o.secili.Acilan);
        Assert.Equal(MikA, o.secili.Cihaz);

        Assert.Equal(0.9, o.dolu, 6);
        Assert.Equal(0.9, o.tutulan, 6);
        Assert.Equal(0, o.sessiz);

        Assert.False(o.Sonra);
        Assert.True(o.Kapandi);
        Assert.Equal(1, o.Toplam);
        Assert.Equal(0, o.SonDeger);
    }

    [Fact]
    public void CihazDegisinceOkuyucuYenidenBaslarDilYenilemesindeBaslamaz()
    {
        var o = Kur((view, _, acilan) =>
        {
            view.RefreshAudioBoxes();
            Sec(view, view.CmbMicrophone, 1);
            Sec(view, view.CmbSystemAudio, 1);
            var ilk = acilan.Count;

            view.RefreshAudioBoxes();
            Dispatcher.UIThread.RunJobs();
            var yenilemeden = acilan.Count;
            var yenilemedeKapanan = acilan.Count(k => k.Kapandi);

            Sec(view, view.CmbMicrophone, 2);
            return (ilk, yenilemeden, yenilemedeKapanan,
                Cihazlar: acilan.Select(k => k.Cihaz.Name).ToArray(),
                Kapali: acilan.Select(k => k.Kapandi).ToArray(),
                Mik: view.LevelVisible(AudioSourceRole.Microphone), Sis: view.LevelVisible(AudioSourceRole.SystemAudio));
        });

        Assert.Equal(2, o.ilk);
        Assert.Equal(2, o.yenilemeden);
        Assert.Equal(0, o.yenilemedeKapanan);
        Assert.Equal(new[] { "Mik A", "Stereo Mix", "Mik B: USB" }, o.Cihazlar);
        Assert.Equal(new[] { true, false, false }, o.Kapali);
        Assert.True(o.Mik);
        Assert.True(o.Sis);
    }

    [Fact]
    public void SekmedenCikincaVePencereKapanincaOkuyucuKapanir()
    {
        List<SahteKaynak>? hepsi = null;
        var o = Kur((view, pencere, acilan) =>
        {
            hepsi = acilan;
            view.RefreshAudioBoxes();
            Sec(view, view.CmbMicrophone, 1);
            var acikken = (Sayi: acilan.Count, Kapali: acilan[0].Kapandi);

            pencere.Content = null;
            Dispatcher.UIThread.RunJobs();
            var cikinca = (Sayi: acilan.Count, Kapali: acilan[0].Kapandi, Cubuk: view.LevelVisible(AudioSourceRole.Microphone));

            pencere.Content = view;
            Dispatcher.UIThread.RunJobs();
            var donunce = (Sayi: acilan.Count, Kapali: acilan[^1].Kapandi, Cubuk: view.LevelVisible(AudioSourceRole.Microphone));

            pencere.Close();
            Dispatcher.UIThread.RunJobs();
            return (acikken, cikinca, donunce, KapanincaKapali: acilan[^1].Kapandi, KapanincaCubuk: view.LevelVisible(AudioSourceRole.Microphone));
        });

        Assert.Equal((1, false), o.acikken);
        Assert.Equal((1, true, false), o.cikinca);
        Assert.Equal((2, false, true), o.donunce);
        Assert.True(o.KapanincaKapali);
        Assert.False(o.KapanincaCubuk);
        Assert.All(hepsi!, kaynak => Assert.True(kaynak.Kapandi));
    }

    [Fact]
    public void KayitBaslarkenKapanirBaslayincaAcilirBitinceYenidenBaslar()
    {
        var o = Kur((view, _, acilan) =>
        {
            view.RefreshAudioBoxes();
            Sec(view, view.CmbMicrophone, 1);

            view.PauseLevels();
            var durunca = (Sayi: acilan.Count, Kapali: acilan[0].Kapandi, Cubuk: view.LevelVisible(AudioSourceRole.Microphone));
            Sec(view, view.CmbMicrophone, 2);
            var durukSecim = acilan.Count;

            view.ResumeLevels();
            var surunce = (Sayi: acilan.Count, Cihaz: acilan[^1].Cihaz.Name, Kapali: acilan[^1].Kapandi, Cubuk: view.LevelVisible(AudioSourceRole.Microphone));

            view.RestartLevels();
            return (durunca, durukSecim, surunce,
                Bitince: (Sayi: acilan.Count, Onceki: acilan[1].Kapandi, Yeni: acilan[^1].Kapandi, Cubuk: view.LevelVisible(AudioSourceRole.Microphone)));
        });

        Assert.Equal((1, true, false), o.durunca);
        Assert.Equal(1, o.durukSecim);
        Assert.Equal((2, "Mik B: USB", false, true), o.surunce);
        Assert.Equal((3, true, false, true), o.Bitince);

        var serit = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Recorder", "RecorderView.Serit.cs"));
        var dur = serit.IndexOf("PauseLevels();", StringComparison.Ordinal);
        var basla = serit.IndexOf("RecorderSession.StartAsync(", StringComparison.Ordinal);
        var sur = serit.IndexOf("ResumeLevels();", StringComparison.Ordinal);
        Assert.True(dur >= 0 && dur < basla && basla < sur, $"kayit baslangicinda sira bozuk: {dur} {basla} {sur}");
        Assert.Equal(2, serit.Split("RestartLevels();").Length - 1);
        Assert.Equal(2, serit.Split("_session = null;\r\n            _frameRegion = null;").Length - 1 + serit.Split("_session = null;\n            _frameRegion = null;").Length - 1);
    }

    [Fact]
    public void KendiligindenBitenYaDaAcilamayanKaynaktaCubukGizlenirVeYenidenDenenmez()
    {
        var o = Kur((view, _, acilan) =>
        {
            view.RefreshAudioBoxes();
            Sec(view, view.CmbMicrophone, 1);
            var kaynak = acilan[0];
            kaynak.Ver(-6);
            var once = (Cubuk: view.LevelVisible(AudioSourceRole.Microphone), Deger: view.LevelValue(AudioSourceRole.Microphone));

            kaynak.Bitir();
            var bitince = (Cubuk: view.LevelVisible(AudioSourceRole.Microphone), Kapali: kaynak.Kapandi, Deger: view.LevelValue(AudioSourceRole.Microphone));

            kaynak.Ver(-3);
            view.RefreshAudioBoxes();
            Dispatcher.UIThread.RunJobs();
            var sonra = (Sayi: acilan.Count, Deger: view.LevelValue(AudioSourceRole.Microphone), Cubuk: view.LevelVisible(AudioSourceRole.Microphone));

            Sec(view, view.CmbMicrophone, 2);
            return (once, bitince, sonra, YeniSecim: (Sayi: acilan.Count, Cubuk: view.LevelVisible(AudioSourceRole.Microphone)));
        });

        Assert.Equal((true, 0.9), (o.once.Cubuk, Math.Round(o.once.Deger, 6)));
        Assert.Equal((false, true, 0d), o.bitince);
        Assert.Equal((1, 0d, false), o.sonra);
        Assert.Equal((2, true), o.YeniSecim);

        var acilamayan = Kur((view, _, acilan) =>
        {
            view.RefreshAudioBoxes();
            Sec(view, view.CmbMicrophone, 1);
            return (Cubuk: view.LevelVisible(AudioSourceRole.Microphone), Sayi: acilan.Count, Secili: view.Chosen(AudioSourceRole.Microphone));
        }, kaynakVar: false);

        Assert.False(acilamayan.Cubuk);
        Assert.Equal(0, acilamayan.Sayi);
        Assert.Equal(MikA, acilamayan.Secili);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HareketAzaltilmisPenceredeCubukCanlandirilmaz(bool hareketsiz)
    {
        var gecis = Kur((view, _, _) =>
        {
            view.RefreshAudioBoxes();
            Sec(view, view.CmbMicrophone, 1);
            return view.BarMicrophoneLevel.Transitions?.Count ?? 0;
        }, hareketsiz, gecisli: true);

        if (hareketsiz) Assert.Equal(0, gecis);
        else Assert.True(gecis > 0, "hareket acikken cubugun gecisi olmali; yoksa bu kol hicbir seyi olcmuyor");
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("en")]
    public void CubuklarErisilebilirAdVeIpucuTasirRenkVeOlcuTemadanGelir(string dil)
    {
        var o = Kur((view, _, _) =>
        {
            Strings.Use(dil);
            try
            {
                Dispatcher.UIThread.RunJobs();
                view.RefreshAudioBoxes();
                Sec(view, view.CmbMicrophone, 1);
                Sec(view, view.CmbSystemAudio, 1);
                Dispatcher.UIThread.RunJobs();
                var mik = view.BarMicrophoneLevel;
                var sis = view.BarSystemAudioLevel;
                view.TryFindResource("ProgressBarHeight", out var yukseklik);
                view.TryFindResource("RecorderAudioLevelGap", out var bosluk);
                view.TryFindResource("SpaceSm", out var kucuk);
                view.TryFindResource("LabelMargin", out var etiketPayi);
                var etiket = (TextBlock)((Grid)mik.Parent!).Children[0];
                var satir = (Grid)mik.Parent!;
                var etiketYeri = etiket.TranslatePoint(new Point(0, etiket.Bounds.Height / 2), satir)!.Value.Y;
                var cubukYeri = mik.TranslatePoint(new Point(0, mik.Bounds.Height / 2), satir)!.Value.Y;
                return (
                    MikAd: AutomationProperties.GetName(mik), SisAd: AutomationProperties.GetName(sis),
                    MikIpucu: ToolTip.GetTip(mik) as string, SisIpucu: ToolTip.GetTip(sis) as string,
                    Beklenen: Anahtarlar.Select(a => LanguageCatalog.Display(Strings.Get(a))).ToArray(),
                    Yukseklik: mik.Bounds.Height, TemaYukseklik: (double)yukseklik!,
                    Bosluk: mik.Margin, TemaBosluk: (Thickness)bosluk!, Kucuk: (double)kucuk!,
                    EtiketAlti: ((Thickness)etiketPayi!).Bottom, EtiketOrta: etiketYeri, CubukOrta: cubukYeri,
                    Aralik: (mik.Minimum, mik.Maximum));
            }
            finally
            {
                Strings.Use("en");
            }
        });

        Assert.Equal(o.Beklenen[0], o.MikAd);
        Assert.Equal(o.Beklenen[1], o.SisAd);
        Assert.Equal(o.Beklenen[2], o.MikIpucu);
        Assert.Equal(o.Beklenen[2], o.SisIpucu);
        Assert.NotEqual(o.MikAd, o.SisAd);
        Assert.All(o.Beklenen, metin => Assert.False(string.IsNullOrWhiteSpace(metin)));
        Assert.DoesNotContain("recorder.audio", o.MikAd);

        Assert.Equal(o.TemaYukseklik, o.Yukseklik, 3);
        Assert.Equal(o.TemaBosluk, o.Bosluk);
        Assert.Equal(new Thickness(o.Kucuk, 0, 0, o.EtiketAlti), o.TemaBosluk);
        Assert.Equal(o.EtiketOrta, o.CubukOrta, 0);
        Assert.Equal((0d, 1d), o.Aralik);

        var xaml = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Recorder", "RecorderView.axaml"));
        foreach (var ad in new[] { "BarMicrophoneLevel", "BarSystemAudioLevel" })
        {
            var bas = xaml.IndexOf($"x:Name=\"{ad}\"", StringComparison.Ordinal);
            var blok = xaml.Substring(bas, xaml.IndexOf("/>", bas, StringComparison.Ordinal) - bas);
            Assert.DoesNotContain("#", blok);
            Assert.DoesNotContain("Height=", blok);
            Assert.DoesNotContain("Foreground", blok);
            Assert.DoesNotContain("Background", blok);
        }
    }

    [Fact]
    public void AnahtarlarButunDillerdeDoluVeCevrilmis()
    {
        var diller = Locales.Languages;
        Assert.Equal(42, diller.Count);

        var ingilizce = Locales.Domain("en", "recorder");
        var ayni = new List<string>();
        foreach (var dil in diller)
        {
            var sozluk = Locales.Domain(dil, "recorder");
            foreach (var anahtar in Anahtarlar)
            {
                Assert.True(sozluk.TryGetValue(anahtar, out var metin), $"{dil}: {anahtar} yok");
                Assert.False(string.IsNullOrWhiteSpace(metin), $"{dil}: {anahtar} bos");
                if (dil != "en" && metin == ingilizce[anahtar]) ayni.Add($"{dil}:{anahtar}");
            }

            Assert.False(sozluk.ContainsKey("recorder.audio.level.yok"));
            Assert.Equal(3, Anahtarlar.Select(a => sozluk[a]).Distinct().Count());
        }

        Assert.True(ayni.Count == 0, "Ingilizce kalmis: " + string.Join(", ", ayni));
    }

    [Theory]
    [InlineData(1560, 1060, "tr")]
    [InlineData(1024, 1060, "tr")]
    [InlineData(1600, 1000, "tr")]
    [InlineData(1600, 1000, "en")]
    [InlineData(1920, 1040, "tr")]
    public void GercekPenceredeSekmeDegisinceKapanirVeCubuklarSayfayiKaydirmaz(int en, int boy, string dil)
    {
        var boyut = new Size(en, boy);
        var acilan = new List<SahteKaynak>();
        var o = KaydirmasizSekmeTests.AyarlariKorurken(() => GorselDenetimTests.Pencere(boyut, w =>
        {
            var view = w.RecorderPaneForTest;
            view.ListAudioDevices = () => new[] { MikA, MikB, Miks };
            view.OpenLevelSource = cihaz =>
            {
                var kaynak = new SahteKaynak(cihaz);
                acilan.Add(kaynak);
                return kaynak;
            };
            GorselDenetimTests.Ad<RadioButton>(view, "RadAdvanced").IsChecked = true;
            foreach (var parca in w.GetVisualDescendants().OfType<Animatable>()) parca.Transitions = null;
            GorselDenetimTests.Yerlestir(w, boyut);
            var sayfa = GorselDenetimTests.Ad<ScrollViewer>(w, "PageRecorder");
            var cubuksuz = sayfa.Extent.Height;

            view.RefreshAudioBoxes();
            view.CmbMicrophone.SelectedIndex = 1;
            view.CmbSystemAudio.SelectedIndex = 1;
            GorselDenetimTests.Yerlestir(w, boyut);
            var cubuklu = (Extent: sayfa.Extent.Height, Viewport: sayfa.Viewport.Height,
                Mik: view.BarMicrophoneLevel.IsVisible, Sis: view.BarSystemAudioLevel.IsVisible,
                MikBoy: view.BarMicrophoneLevel.Bounds.Height, Acilan: acilan.Count, Kapali: acilan.Count(k => k.Kapandi));

            GorselDenetimTests.Ad<TabControl>(w, "Tabs").SelectedIndex = 0;
            GorselDenetimTests.Yerlestir(w, boyut);
            var cikinca = (Acilan: acilan.Count, Kapali: acilan.Count(k => k.Kapandi));

            view.CmbMicrophone.SelectedIndex = 0;
            view.CmbSystemAudio.SelectedIndex = 0;
            return (cubuksuz, cubuklu, cikinca);
        }, sekme: 4, dil: dil, dolu: false, hazirla: KaydediciDuzenTests.HareketsizAc));

        _output.WriteLine($"{en}x{boy} {dil}: cubuksuz {o.cubuksuz:0.#}, cubuklu {o.cubuklu.Extent:0.#}, viewport {o.cubuklu.Viewport:0.#}, cubuk {o.cubuklu.MikBoy:0.#}");
        Assert.True(o.cubuklu.Mik);
        Assert.True(o.cubuklu.Sis);
        Assert.True(o.cubuklu.MikBoy > 0);
        Assert.Equal((2, 0), (o.cubuklu.Acilan, o.cubuklu.Kapali));
        Assert.Equal(o.cubuksuz, o.cubuklu.Extent, 1);
        Assert.True(o.cubuklu.Extent <= o.cubuklu.Viewport + 0.5, $"cubuklar sayfayi kaydiriyor: extent {o.cubuklu.Extent} viewport {o.cubuklu.Viewport}");
        Assert.Equal((2, 2), o.cikinca);
        Assert.All(acilan, kaynak => Assert.True(kaynak.Kapandi));
    }

    [FfmpegFact]
    public async Task CanliSinusSeviyesiOkunurVeKapatinceSurecOlur()
    {
        var girdi = new[] { "-re", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=8000" };
        var seviyeler = new List<double>();
        var yeter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bitti = false;

        var kaynak = AudioLevelSource.Start(ToolLocator.Ffmpeg, AudioLevel.Arguments(girdi));
        var pid = kaynak.ProcessId;
        try
        {
            kaynak.Ended += () => bitti = true;
            kaynak.Level += db =>
            {
                lock (seviyeler)
                {
                    seviyeler.Add(db);
                    if (seviyeler.Count >= 10) yeter.TrySetResult();
                }
            };

            var kazanan = await Task.WhenAny(yeter.Task, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.True(ReferenceEquals(kazanan, yeter.Task), $"3 sn icinde 10 pencere gelmedi; gelen {seviyeler.Count}");

            using (var canli = Process.GetProcessById(pid))
                Assert.False(canli.HasExited);
            if (OperatingSystem.IsWindows()) Assert.Equal(ProcessPriorityClass.BelowNormal, kaynak.Priority);
        }
        finally
        {
            kaynak.Dispose();
        }

        await Task.WhenAny(kaynak.Completion, Task.Delay(TimeSpan.FromSeconds(3)));
        double[] okunan;
        lock (seviyeler) okunan = seviyeler.ToArray();
        _output.WriteLine(string.Join(' ', okunan.Select(db => db.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))));

        Assert.All(okunan.Skip(1), db => Assert.InRange(db, -18.6, -17.6));
        Assert.All(okunan.Skip(1), db => Assert.InRange(AudioLevel.Fraction(db), 0.69, 0.71));
        Assert.False(bitti);

        Process? kalan = null;
        try { kalan = Process.GetProcessById(pid); } catch (ArgumentException) { }
        Assert.True(kalan is null || kalan.HasExited || !kalan.ProcessName.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase), $"ffmpeg {pid} hala kosuyor");
        kalan?.Dispose();
    }

    [FfmpegFact]
    public async Task AcilamayanGirdideKaynakBittiginiSoylerVeSeviyeVermez()
    {
        var bitti = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seviye = 0;

        using var kaynak = AudioLevelSource.Start(ToolLocator.Ffmpeg, AudioLevel.Arguments(new[] { "-f", "lavfi", "-i", "boyle_bir_kaynak_yok" }));
        kaynak.Level += _ => Interlocked.Increment(ref seviye);
        kaynak.Ended += () => bitti.TrySetResult();

        var kazanan = await Task.WhenAny(bitti.Task, kaynak.Completion.ContinueWith(_ => Task.Delay(200)).Unwrap(), Task.Delay(TimeSpan.FromSeconds(5)));
        await Task.WhenAny(kaynak.Completion, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.True(kaynak.Completion.IsCompleted, "ffmpeg hatali girdide kapanmadi");
        Assert.Equal(0, seviye);
        Assert.True(bitti.Task.IsCompleted || ReferenceEquals(kazanan, bitti.Task) || kaynak.Completion.IsCompleted);
    }
}
