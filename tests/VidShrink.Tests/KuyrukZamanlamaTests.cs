using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// SIK-P7: kuyrukta zamanlama, bekleyen işi düzenleme, "bitince uygulamadan çık" ve bitiş sesi.
/// Saat sahte (<see cref="TimeProvider"/>), koşturucu sahte, kuyruk sonu eylemi sahte, ses sahte:
/// hoparlörden ses çıkmaz, gerçek süreç kapanmaz.
/// </summary>
public sealed class KuyrukZamanlamaTests
{
    private sealed class SahteSaat : TimeProvider
    {
        public DateTimeOffset Simdi { get; set; }
        public override DateTimeOffset GetUtcNow() => Simdi;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class SahteEylem : IQueueEndActions
    {
        public List<string> Cagrilar { get; } = new();
        public void Reveal(string path) => Cagrilar.Add("reveal:" + path);
        public void Sleep() => Cagrilar.Add("sleep");
        public void PowerOff() => Cagrilar.Add("power-off");
        public void Quit() => Cagrilar.Add("quit");
    }

    private sealed class SahteSes : IBitisSesi
    {
        public List<bool> Calinan { get; } = new();
        public void Cal(bool hata) => Calinan.Add(hata);
    }

    private sealed class SahteKosum
    {
        private readonly Dictionary<string, TaskCompletionSource> _bitis = new();
        public List<string> Baslayan { get; } = new();
        public List<(string Yol, double Hedef, CodecPreference Kodek)> Secenek { get; } = new();
        public ShrinkJobWindow? Pencere { get; set; }

        public Task Kostur(ShrinkRequest istek, CancellationToken ct)
        {
            var bitis = new TaskCompletionSource();
            _bitis[istek.Path] = bitis;
            Baslayan.Add(istek.Path);
            if (Pencere is { } pencere)
            {
                var (options, hedef) = pencere.OptionsFor(istek, Bilgi(istek.Path));
                Secenek.Add((istek.Path, hedef, options.Codec));
            }
            ct.Register(() => bitis.TrySetCanceled(ct));
            return bitis.Task;
        }

        public void Bitir(string yol)
        {
            _bitis[yol].TrySetResult();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static MediaInfo Bilgi(string yol) => new()
    {
        FilePath = yol,
        DurationSeconds = 10,
        Width = 1920,
        Height = 1080,
        Fps = 24,
        VideoCodec = "h264",
        FileSizeBytes = 10_000_000,
        TotalBitrateBps = 8_000_000
    };

    private static readonly DateTimeOffset Dokuz = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    private static ShrinkJobWindow Pencere(SahteEylem eylem, SahteKosum kosum, SahteSaat saat, params string[] yollar)
    {
        var pencere = new ShrinkJobWindow(yollar, new PlanOptions { TargetMb = 25 }, false, null)
        {
            Actions = eylem,
            Kosturucu = kosum.Kostur,
            ParallelLimit = 1,
            ZamanSaati = saat
        };
        kosum.Pencere = pencere;
        return pencere;
    }

    private static string[] Adlar(IEnumerable<ShrinkRequest> istekler) => istekler.Select(r => r.Path).ToArray();

    [Theory]
    [InlineData(QueueStartMode.At, "10:30", "2026-10-15T10:30:00")]
    [InlineData(QueueStartMode.At, "9.45", "2026-10-15T09:45:00")]
    [InlineData(QueueStartMode.At, "08:00", "2026-10-16T08:00:00")]
    [InlineData(QueueStartMode.At, "09:00", "2026-10-16T09:00:00")]
    [InlineData(QueueStartMode.At, " 23:59 ", "2026-10-15T23:59:00")]
    [InlineData(QueueStartMode.After, "90", "2026-10-15T10:30:00")]
    [InlineData(QueueStartMode.After, "1:30", "2026-10-15T10:30:00")]
    [InlineData(QueueStartMode.After, "0:01", "2026-10-15T09:01:00")]
    public void ZamanCozulur(QueueStartMode kip, string metin, string beklenen)
    {
        var sonuc = QueueSchedule.Resolve(kip, metin, Dokuz);
        Assert.Equal(DateTimeOffset.Parse(beklenen + "+00:00", System.Globalization.CultureInfo.InvariantCulture), sonuc);
    }

    [Theory]
    [InlineData(QueueStartMode.At, "")]
    [InlineData(QueueStartMode.At, "abc")]
    [InlineData(QueueStartMode.At, "24:00")]
    [InlineData(QueueStartMode.At, "10:60")]
    [InlineData(QueueStartMode.At, "10")]
    [InlineData(QueueStartMode.After, "0")]
    [InlineData(QueueStartMode.After, "-5")]
    [InlineData(QueueStartMode.After, "0:00")]
    [InlineData(QueueStartMode.After, "1:75")]
    [InlineData(QueueStartMode.After, "x")]
    public void BozukZamanReddedilir(QueueStartMode kip, string metin)
        => Assert.Null(QueueSchedule.Resolve(kip, metin, Dokuz));

    /// <summary>
    /// Sahte saatle: 10:00'a zamanlanan sıra 9:59:59'da başlamaz, 10:00'da başlar; bekleme sırasında
    /// geri sayım satırı kalan süreyi saniyesi yukarı yuvarlanmış yazar.
    /// </summary>
    [Fact]
    public void ZamanlananSiraSaatGelinceBaslar()
    {
        var saat = new SahteSaat { Simdi = Dokuz.AddMinutes(59) };
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var pencere = Pencere(new SahteEylem(), kosum, saat, "a.mp4", "b.mp4");
            try
            {
                var kabul = pencere.Schedule(QueueStartMode.At, "10:00");
                pencere.Begin();
                var bekleyen = (pencere.Paused, Adlar(pencere.RunningJobs), pencere.ScheduleLeft, pencere.CountdownText);

                saat.Simdi = Dokuz.AddMinutes(59).AddSeconds(59.5);
                pencere.ScheduleTick();
                var sonSaniye = (pencere.Paused, Adlar(pencere.RunningJobs), pencere.CountdownText);

                saat.Simdi = Dokuz.AddHours(1);
                pencere.ScheduleTick();
                var basladi = (pencere.Paused, Adlar(pencere.RunningJobs), Adlar(pencere.Pending), pencere.ScheduleLeft,
                    pencere.FindControl<Grid>("CountdownRow")!.IsVisible);
                return (kabul, bekleyen, sonSaniye, basladi);
            }
            finally { pencere.Close(); }
        });

        Assert.True(sonuc.kabul);
        Assert.True(sonuc.bekleyen.Paused);
        Assert.Empty(sonuc.bekleyen.Item2);
        Assert.Equal(TimeSpan.FromMinutes(1), sonuc.bekleyen.ScheduleLeft);
        Assert.Contains("0:01:00", sonuc.bekleyen.CountdownText, StringComparison.Ordinal);
        Assert.True(sonuc.sonSaniye.Paused);
        Assert.Empty(sonuc.sonSaniye.Item2);
        Assert.Contains("0:00:01", sonuc.sonSaniye.CountdownText, StringComparison.Ordinal);
        Assert.False(sonuc.basladi.Paused);
        Assert.Equal(new[] { "a.mp4" }, sonuc.basladi.Item2);
        Assert.Equal(new[] { "b.mp4" }, sonuc.basladi.Item3);
        Assert.Null(sonuc.basladi.ScheduleLeft);
        Assert.False(sonuc.basladi.Item5);
    }

    /// <summary>"Şu kadar sonra" denetimlerden: kip seçimi, metin kutusu ve düğme. Vazgeç zamanlamayı kaldırır, sıra duraklatılmış kalır.</summary>
    [Fact]
    public void SureyleZamanlamaVeVazgec()
    {
        var saat = new SahteSaat { Simdi = Dokuz };
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var pencere = Pencere(new SahteEylem(), kosum, saat, "a.mp4");
            try
            {
                pencere.FindControl<ComboBox>("CmbStartAt")!.SelectedIndex = (int)QueueStartMode.After;
                pencere.FindControl<TextBox>("TxtStartAt")!.Text = "5";
                pencere.FindControl<Button>("BtnSchedule")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                pencere.Begin();
                var kalan = pencere.ScheduleLeft;

                pencere.FindControl<Button>("BtnCountdownCancel")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var vazgecildi = (pencere.ScheduleLeft, pencere.Paused, pencere.FindControl<Grid>("CountdownRow")!.IsVisible);

                saat.Simdi = Dokuz.AddHours(2);
                pencere.ScheduleTick();
                var sonra = (pencere.Paused, Adlar(pencere.RunningJobs));

                pencere.Schedule(QueueStartMode.After, "30");
                pencere.SetPaused(false);
                var hemen = (pencere.ScheduleLeft, Adlar(pencere.RunningJobs));
                return (kalan, vazgecildi, sonra, hemen);
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(TimeSpan.FromMinutes(5), sonuc.kalan);
        Assert.Null(sonuc.vazgecildi.ScheduleLeft);
        Assert.True(sonuc.vazgecildi.Paused);
        Assert.False(sonuc.vazgecildi.Item3);
        Assert.True(sonuc.sonra.Paused);
        Assert.Empty(sonuc.sonra.Item2);
        Assert.Null(sonuc.hemen.ScheduleLeft);
        Assert.Equal(new[] { "a.mp4" }, sonuc.hemen.Item2);
    }

    /// <summary>Okunamayan saat zamanlamaz: sıra duraklamaz, hata satırı görünür; geçerli girişte satır kalkar.</summary>
    [Fact]
    public void BozukGirisZamanlamaz()
    {
        var saat = new SahteSaat { Simdi = Dokuz };
        var sonuc = AppHost.Run(() =>
        {
            var pencere = Pencere(new SahteEylem(), new SahteKosum(), saat, "a.mp4");
            try
            {
                var hata = pencere.FindControl<TextBlock>("TxtScheduleError")!;
                var kabul = pencere.Schedule(QueueStartMode.At, "25:00");
                var bozuk = (kabul, pencere.Paused, pencere.ScheduleLeft, hata.IsVisible, hata.Text);
                var ikinci = pencere.Schedule(QueueStartMode.At, "10:00");
                return (bozuk, ikinci, hata.IsVisible);
            }
            finally { pencere.Close(); }
        });

        Assert.False(sonuc.bozuk.kabul);
        Assert.False(sonuc.bozuk.Paused);
        Assert.Null(sonuc.bozuk.ScheduleLeft);
        Assert.True(sonuc.bozuk.IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(sonuc.bozuk.Text));
        Assert.True(sonuc.ikinci);
        Assert.False(sonuc.Item3);
    }

    [Fact]
    public void PompaBekleyeniYerindeDegistirir()
    {
        var pompa = new JobPump<string>((_, _) => Task.CompletedTask, () => 1) { Paused = true };
        var degisti = 0;
        pompa.Changed += () => degisti++;
        string a = "a", b = "b", c = "c", yeni = new string('b', 2);
        pompa.Enqueue(a);
        pompa.Enqueue(b);
        pompa.Enqueue(c);
        var once = degisti;

        Assert.True(pompa.ReplacePending(b, yeni));
        Assert.Equal(new[] { a, yeni, c }, pompa.Pending);
        Assert.Same(yeni, pompa.Pending[1]);
        Assert.Equal(once + 1, degisti);
        Assert.False(pompa.ReplacePending(b, "d"));
        Assert.Equal(once + 1, degisti);
    }

    /// <summary>
    /// Düzenlenen iş sıradan çıkmadan yeni hedef ve kodekle koşar; dokunulmayan iş eski ayarla.
    /// Sıra ve kabul sayısı değişmez.
    /// </summary>
    [Fact]
    public void DuzenlenenIsYeniAyarlaKosar()
    {
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var pencere = Pencere(new SahteEylem(), kosum, new SahteSaat { Simdi = Dokuz }, "a.mp4", "b.mp4", "c.mp4");
            try
            {
                pencere.Begin();
                var duzenlendi = pencere.EditPending(0, 8, CodecPreference.MaxCompression);
                var sira = (Adlar(pencere.Pending), pencere.AcceptedCount, pencere.Pending[0].TargetMegabytes);
                var bozuk = pencere.EditPending(5, 8, CodecPreference.Auto);
                var sifir = pencere.EditPending(0, 0, CodecPreference.Auto);
                kosum.Bitir("a.mp4");
                kosum.Bitir("b.mp4");
                return (duzenlendi, sira, bozuk, sifir, kosum.Secenek.ToArray());
            }
            finally { pencere.Close(); }
        });

        Assert.True(sonuc.duzenlendi);
        Assert.Equal(new[] { "b.mp4", "c.mp4" }, sonuc.sira.Item1);
        Assert.Equal(3, sonuc.sira.AcceptedCount);
        Assert.Equal(8, sonuc.sira.TargetMegabytes);
        Assert.False(sonuc.bozuk);
        Assert.False(sonuc.sifir);
        Assert.Equal(new[]
        {
            ("a.mp4", 25d, CodecPreference.Compatible),
            ("b.mp4", 8d, CodecPreference.MaxCompression),
            ("c.mp4", 25d, CodecPreference.Compatible)
        }, sonuc.Item5);
    }

    /// <summary>Satırdaki Düzenle düğmesi paneli işin değerleriyle açar; Kaydet işi değiştirir, bozuk hedef kaydetmez.</summary>
    [Fact]
    public void DuzenlePaneliIsiDegistirir()
    {
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var pencere = Pencere(new SahteEylem(), kosum, new SahteSaat { Simdi = Dokuz }, "a.mp4", "b.mp4");
            try
            {
                pencere.SetPaused(true);
                pencere.Begin();
                var panel = pencere.FindControl<Border>("EditPanel")!;
                var hedef = pencere.FindControl<TextBox>("TxtEditTarget")!;
                var kodek = pencere.FindControl<ComboBox>("CmbEditCodec")!;
                var kapali = panel.IsVisible;

                var satir = (Grid)pencere.FindControl<StackPanel>("PendingList")!.Children[1];
                var duzenle = satir.Children.OfType<Button>().Single(d => Grid.GetColumn(d) == 3);
                duzenle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var acildi = (panel.IsVisible, hedef.Text, kodek.SelectedIndex);

                hedef.Text = "abc";
                pencere.FindControl<Button>("BtnEditSave")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var bozuk = (panel.IsVisible, pencere.Pending[1].TargetMegabytes);

                hedef.Text = "12";
                kodek.SelectedIndex = 0;
                pencere.FindControl<Button>("BtnEditSave")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var kaydedildi = (panel.IsVisible, Adlar(pencere.Pending), pencere.Pending[1].TargetMegabytes);

                pencere.SetPaused(false);
                kosum.Bitir("a.mp4");
                return (kapali, acildi, bozuk, kaydedildi, kosum.Secenek.ToArray());
            }
            finally { pencere.Close(); }
        });

        Assert.False(sonuc.kapali);
        Assert.True(sonuc.acildi.IsVisible);
        Assert.Equal("25", sonuc.acildi.Text);
        Assert.Equal(1, sonuc.acildi.SelectedIndex);
        Assert.True(sonuc.bozuk.IsVisible);
        Assert.Equal(0, sonuc.bozuk.TargetMegabytes);
        Assert.False(sonuc.kaydedildi.IsVisible);
        Assert.Equal(new[] { "a.mp4", "b.mp4" }, sonuc.kaydedildi.Item2);
        Assert.Equal(12, sonuc.kaydedildi.TargetMegabytes);
        Assert.Equal(("b.mp4", 12d, CodecPreference.Auto), sonuc.Item5[^1]);
    }

    /// <summary>"Uygulamadan çık" listede, seçilir ve iş bitene dek seçili kalır; sıra boşalınca bir kez çağrılır.</summary>
    [Fact]
    public void UygulamadanCikSecilirVeSonundaBirKez()
    {
        var eylem = new SahteEylem();
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var pencere = Pencere(eylem, kosum, new SahteSaat { Simdi = Dokuz }, "a.mp4", "b.mp4");
            try
            {
                var secim = pencere.FindControl<ComboBox>("CmbWhenDone")!;
                var ogeler = secim.ItemCount;
                pencere.WhenDone = QueueEndChoice.Quit;
                pencere.Begin();
                kosum.Bitir("a.mp4");
                var arada = (eylem.Cagrilar.ToArray(), pencere.WhenDone, secim.SelectedIndex);
                kosum.Bitir("b.mp4");
                pencere.QueueDrained();
                return (ogeler, arada, eylem.Cagrilar.ToArray());
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(5, sonuc.ogeler);
        Assert.Empty(sonuc.arada.Item1);
        Assert.Equal(QueueEndChoice.Quit, sonuc.arada.WhenDone);
        Assert.Equal(4, sonuc.arada.SelectedIndex);
        Assert.Equal(new[] { "quit" }, sonuc.Item3);
    }

    /// <summary>Duraklatılmış sırada "çık" çağrılmaz; "hiçbir şey" seçiliyken de.</summary>
    [Fact]
    public void DuraklatilmisSiradaCikilmaz()
    {
        var eylem = new SahteEylem();
        AppHost.Run(() =>
        {
            var pencere = Pencere(eylem, new SahteKosum(), new SahteSaat { Simdi = Dokuz }, "a.mp4");
            try
            {
                pencere.WhenDone = QueueEndChoice.Quit;
                pencere.SetPaused(true);
                pencere.Begin();
                pencere.RemovePending(0);
                pencere.QueueDrained();
                return 0;
            }
            finally { pencere.Close(); }
        });
        Assert.Empty(eylem.Cagrilar);
    }

    [Fact]
    public void BitisSesiKarari()
    {
        var basarili = new BitenIs("a.mp4", IsSonucu.Basarili);
        var hatali = new BitenIs("b.mp4", IsSonucu.Hatali);
        var iptal = new BitenIs("c.mp4", IsSonucu.Iptal);

        Assert.False(IsBittiBildirimi.SesKarari(new[] { basarili }, true));
        Assert.True(IsBittiBildirimi.SesKarari(new[] { basarili, hatali }, true));
        Assert.True(IsBittiBildirimi.SesKarari(new[] { hatali, iptal }, true));
        Assert.Null(IsBittiBildirimi.SesKarari(new[] { iptal }, true));
        Assert.Null(IsBittiBildirimi.SesKarari(Array.Empty<BitenIs>(), true));
        Assert.Null(IsBittiBildirimi.SesKarari(new[] { basarili }, false));
    }

    /// <summary>
    /// Kuyruk penceresi ayar açıkken sahte sese bir kez çalar (hata ayrı), kapalıyken ve yalnız iptalde çalmaz.
    /// Ayar dosyasından okunur; test konağında varsayılan ses sessizdir.
    /// </summary>
    [Fact]
    public void KuyrukBitinceSesCalar()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "kuyruk-zamanlama", "ses-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var acik = Path.Combine(klasor, "acik.json");
            new AppSettings { FinishSound = true }.Save(acik);
            var sonuc = AppHost.Run(() =>
            {
                var onceki = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
                Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", acik);
                ShrinkJobWindow pencere;
                try { pencere = new ShrinkJobWindow(new[] { "a.mp4" }, new PlanOptions { TargetMb = 25 }, false, null); }
                finally { Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", onceki); }
                try
                {
                    var okunan = pencere.BitisSesi;
                    var varsayilan = pencere.Ses.GetType();
                    var ses = new SahteSes();
                    pencere.Ses = ses;
                    pencere.IsBitti("a.mp4", IsSonucu.Basarili);
                    pencere.HaberVer();
                    pencere.HaberVer();
                    pencere.IsBitti("b.mp4", IsSonucu.Hatali);
                    pencere.HaberVer();
                    pencere.IsBitti("c.mp4", IsSonucu.Iptal);
                    pencere.HaberVer();
                    pencere.BitisSesi = false;
                    pencere.IsBitti("d.mp4", IsSonucu.Basarili);
                    pencere.HaberVer();
                    return (okunan, varsayilan, ses.Calinan.ToArray());
                }
                finally { pencere.Close(); }
            });

            Assert.True(sonuc.okunan);
            Assert.Equal(typeof(SessizBitisSesi), sonuc.varsayilan);
            Assert.Equal(new[] { false, true }, sonuc.Item3);
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public void BitisSesiAyariGidipDoner()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "kuyruk-zamanlama", "ayar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var dosya = Path.Combine(klasor, "settings.json");
            var eski = Path.Combine(klasor, "eski.json");
            new AppSettings { FinishSound = true }.Save(dosya);
            File.WriteAllText(eski, "{\"notifyWhenDone\": true}");

            Assert.False(new AppSettings().FinishSound);
            Assert.True(AppSettings.Load(dosya).FinishSound);
            Assert.Contains("\"finishSound\": true", File.ReadAllText(dosya), StringComparison.Ordinal);
            Assert.False(AppSettings.Load(eski).FinishSound);
        }
        finally { Directory.Delete(klasor, true); }
    }

    /// <summary>Ayarlar'daki kutu varsayılan kapalı, ayara gider ve geri yüklenir; açıkken tek işin bitişi de sahte sese çalar.</summary>
    [Fact]
    public void AnaPencereSesKutusu()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "kuyruk-zamanlama", "ana-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var dosya = Path.Combine(klasor, "settings.json");
            var sonuc = AppHost.Run(() =>
            {
                var ses = new SahteSes();
                var pencere = new MainWindow { SettingsPathOverride = dosya, Bildirim = SessizIsBildirimYuzu.Instance, Ses = ses };
                try
                {
                    var kutu = pencere.FindControl<CheckBox>("ChkFinishSound")!;
                    var varsayilan = kutu.IsChecked == true;
                    pencere.IsBittiHaberi(new BitenIs("a.mp4", IsSonucu.Basarili));
                    var kapaliCalinan = ses.Calinan.Count;
                    kutu.IsChecked = true;
                    pencere.IsBittiHaberi(new BitenIs("a.mp4", IsSonucu.Hatali));
                    pencere.IsBittiHaberi(new BitenIs("a.mp4", IsSonucu.Iptal));
                    var acikCalinan = ses.Calinan.ToArray();
                    var toplanan = pencere.CaptureAppSettingsForTest().FinishSound;
                    pencere.RestoreAppSettingsForTest(new AppSettings { FinishSound = false });
                    var geriKapali = kutu.IsChecked == true;
                    pencere.RestoreAppSettingsForTest(new AppSettings { FinishSound = true });
                    var geriAcik = kutu.IsChecked == true;
                    return (varsayilan, toplanan, geriKapali, geriAcik, kapaliCalinan, acikCalinan);
                }
                finally { pencere.Close(); }
            });

            Assert.Equal(0, sonuc.kapaliCalinan);
            Assert.Equal(new[] { true }, sonuc.acikCalinan);
            Assert.False(sonuc.varsayilan);
            Assert.True(sonuc.toplanan);
            Assert.False(sonuc.geriKapali);
            Assert.True(sonuc.geriAcik);
        }
        finally { Directory.Delete(klasor, true); }
    }

    /// <summary>On dört anahtar 42 dilde; geri sayım yer tutucuyu, düzenleme başlığı dosya adını taşır.</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        var anahtarlar = new[]
        {
            "main.shrink-job.done.quit", "main.shrink-job.start", "main.shrink-job.start.at", "main.shrink-job.start.after",
            "main.shrink-job.start.hint", "main.shrink-job.schedule", "main.shrink-job.schedule.bad",
            "main.shrink-job.countdown.start", "main.shrink-job.edit", "main.shrink-job.edit.title",
            "main.shrink-job.edit.target", "main.shrink-job.edit.save",
            "settings-tab.finish-sound.label", "settings-tab.finish-sound.hint"
        };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
            Assert.Contains("{0}", values["main.shrink-job.countdown.start"], StringComparison.Ordinal);
            Assert.Contains("{0}", values["main.shrink-job.edit.title"], StringComparison.Ordinal);
            Assert.NotEqual(values["main.shrink-job.start.at"], values["main.shrink-job.start.after"]);
        }
    }
}
