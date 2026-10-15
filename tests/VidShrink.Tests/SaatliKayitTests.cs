using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// EKA-10 saatli kayıt. Saat görünüme enjekte ediliyor; hiçbir ölçü gerçek saati beklemiyor,
/// başlatma da sahte işle sayılıyor, ffmpeg açılmıyor.
/// </summary>
public sealed class SaatliKayitTests
{
    private static readonly DateTime Pazartesi = new(2026, 10, 12, 14, 0, 0);

    [Fact]
    public void IlkSaatBugunGecmediyseBugunGectiyseYarin()
    {
        Assert.Equal(new DateTime(2026, 10, 12, 14, 30, 0), RecorderSchedule.First(Pazartesi, new TimeSpan(14, 30, 0), RecorderRepeat.Daily, DayOfWeek.Monday));
        Assert.Equal(new DateTime(2026, 10, 13, 13, 0, 0), RecorderSchedule.First(Pazartesi, new TimeSpan(13, 0, 0), RecorderRepeat.Once, DayOfWeek.Monday));
        Assert.Equal(new DateTime(2026, 10, 13, 14, 0, 0), RecorderSchedule.First(Pazartesi, new TimeSpan(14, 0, 0), RecorderRepeat.Daily, DayOfWeek.Monday));
    }

    [Fact]
    public void HaftalikIlkSaatSecilenGuneDuser()
    {
        Assert.Equal(new DateTime(2026, 10, 15, 9, 0, 0), RecorderSchedule.First(Pazartesi, new TimeSpan(9, 0, 0), RecorderRepeat.Weekly, DayOfWeek.Thursday));
        Assert.Equal(new DateTime(2026, 10, 19, 9, 0, 0), RecorderSchedule.First(Pazartesi, new TimeSpan(9, 0, 0), RecorderRepeat.Weekly, DayOfWeek.Monday));
        Assert.Equal(new DateTime(2026, 10, 12, 15, 0, 0), RecorderSchedule.First(Pazartesi, new TimeSpan(15, 0, 0), RecorderRepeat.Weekly, DayOfWeek.Monday));
    }

    [Fact]
    public void SonrakiSaatYinelemeyeGoreIlerler()
    {
        var capa = new DateTime(2026, 10, 12, 14, 30, 0);
        Assert.Equal(capa, RecorderSchedule.Upcoming(capa, RecorderRepeat.Once, Pazartesi));
        Assert.Null(RecorderSchedule.Upcoming(capa, RecorderRepeat.Once, capa.AddSeconds(1)));
        Assert.Equal(capa.AddDays(3), RecorderSchedule.Upcoming(capa, RecorderRepeat.Daily, capa.AddDays(2).AddHours(1)));
        Assert.Equal(capa.AddDays(14), RecorderSchedule.Upcoming(capa, RecorderRepeat.Weekly, capa.AddDays(8)));
        Assert.Null(RecorderSchedule.Upcoming(capa, RecorderRepeat.Off, Pazartesi));
    }

    [Fact]
    public void GunlukKayitSaatGelinceBirKezBaslarErtesiGunYineBaslar()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var olcu = AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol);
            var saat = Pazartesi;
            var baslama = new List<DateTime>();
            view.ScheduleClock = () => saat;
            view.ScheduledStart = () => { baslama.Add(saat); return Task.CompletedTask; };

            var kuruldu = view.SetSchedule(RecorderRepeat.Daily, new TimeSpan(14, 30, 0), DayOfWeek.Monday);
            saat = new DateTime(2026, 10, 12, 14, 29, 59);
            var erken = view.ScheduleTickAsync().GetAwaiter().GetResult();
            saat = new DateTime(2026, 10, 12, 14, 30, 0);
            var zamaninda = view.ScheduleTickAsync().GetAwaiter().GetResult();
            saat = new DateTime(2026, 10, 12, 14, 30, 20);
            var tekrar = view.ScheduleTickAsync().GetAwaiter().GetResult();
            var sonraki = view.ScheduleUpcoming;
            saat = new DateTime(2026, 10, 13, 14, 30, 1);
            var ertesiGun = view.ScheduleTickAsync().GetAwaiter().GetResult();
            return (kuruldu, erken, zamaninda, tekrar, sonraki, ertesiGun, baslama, view.Settings.ScheduleRepeat);
        });

        Assert.True(olcu.kuruldu);
        Assert.False(olcu.erken);
        Assert.True(olcu.zamaninda);
        Assert.False(olcu.tekrar);
        Assert.Equal(new DateTime(2026, 10, 13, 14, 30, 0), olcu.sonraki);
        Assert.True(olcu.ertesiGun);
        Assert.Equal(2, olcu.baslama.Count);
        Assert.Equal(RecorderRepeat.Daily, olcu.Item8);
    }

    [Fact]
    public void BirKezlikKayitBaslayincaKapanirVeDosyayaYazilir()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var olcu = AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol);
            var saat = Pazartesi;
            var sayac = 0;
            view.ScheduleClock = () => saat;
            view.ScheduledStart = () => { sayac++; return Task.CompletedTask; };
            view.SetSchedule(RecorderRepeat.Once, new TimeSpan(15, 0, 0), DayOfWeek.Monday);
            var kayitli = RecorderSettings.Load(ayar.Yol);
            saat = new DateTime(2026, 10, 12, 15, 0, 5);
            var basladi = view.ScheduleTickAsync().GetAwaiter().GetResult();
            saat = saat.AddDays(1);
            view.ScheduleTickAsync().GetAwaiter().GetResult();
            return (kayitli.ScheduleRepeat, kayitli.ScheduleAt, basladi, sayac, view.Settings.ScheduleRepeat, RecorderSettings.Load(ayar.Yol).ScheduleRepeat, view.ScheduleUpcoming);
        });

        Assert.Equal(RecorderRepeat.Once, olcu.Item1);
        Assert.Equal(new DateTime(2026, 10, 12, 15, 0, 0), olcu.Item2);
        Assert.True(olcu.basladi);
        Assert.Equal(1, olcu.sayac);
        Assert.Equal(RecorderRepeat.Off, olcu.Item5);
        Assert.Equal(RecorderRepeat.Off, olcu.Item6);
        Assert.Null(olcu.Item7);
    }

    [Fact]
    public void GecKalinanSaatKaydiBaslatmaz()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var olcu = AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol);
            var saat = Pazartesi;
            var sayac = 0;
            view.ScheduleClock = () => saat;
            view.ScheduledStart = () => { sayac++; return Task.CompletedTask; };
            view.SetSchedule(RecorderRepeat.Daily, new TimeSpan(14, 10, 0), DayOfWeek.Monday);
            saat = new DateTime(2026, 10, 12, 16, 0, 0);
            var basladi = view.ScheduleTickAsync().GetAwaiter().GetResult();
            return (basladi, sayac, view.ScheduleUpcoming);
        });

        Assert.False(olcu.basladi);
        Assert.Equal(0, olcu.sayac);
        Assert.Equal(new DateTime(2026, 10, 13, 14, 10, 0), olcu.ScheduleUpcoming);
    }

    [Fact]
    public void KutularSaatiKurarSeritPlaniGosterirGecersizSaatReddedilir()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var olcu = AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol);
            view.ScheduleClock = () => Pazartesi;
            var bosta = view.StateText;
            var saatKutusu = view.FindControl<TextBox>("TxtScheduleTime")!;
            var yineleme = view.FindControl<ComboBox>("CmbScheduleRepeat")!;
            var hata = view.FindControl<TextBlock>("TxtScheduleError")!;

            saatKutusu.Text = "25:99";
            yineleme.SelectedIndex = Array.IndexOf(RecorderSchedule.Choices, RecorderRepeat.Daily);
            var gecersizKuruldu = view.Settings.ScheduleRepeat;
            var hataGorunur = hata.IsVisible && !string.IsNullOrWhiteSpace(hata.Text);

            saatKutusu.Text = "16:45";
            var kuruldu = view.Settings.ScheduleRepeat;
            var plan = view.StateText;
            var sonraki = view.ScheduleUpcoming;
            yineleme.SelectedIndex = Array.IndexOf(RecorderSchedule.Choices, RecorderRepeat.Off);
            return (bosta, gecersizKuruldu, hataGorunur, kuruldu, plan, sonraki, view.ScheduleUpcoming, view.StateText, hata.IsVisible);
        });

        Assert.Equal(RecorderRepeat.Off, olcu.gecersizKuruldu);
        Assert.True(olcu.hataGorunur);
        Assert.Equal(RecorderRepeat.Daily, olcu.kuruldu);
        Assert.Equal(new DateTime(2026, 10, 12, 16, 45, 0), olcu.sonraki);
        Assert.Contains("16:45", olcu.plan);
        Assert.NotEqual(olcu.bosta, olcu.plan);
        Assert.Null(olcu.Item7);
        Assert.Equal(olcu.bosta, olcu.Item8);
        Assert.False(olcu.Item9);
    }
}
