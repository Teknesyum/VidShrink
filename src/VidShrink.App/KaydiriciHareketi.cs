using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;

namespace VidShrink.App;

/// <summary>
/// Kaydırıcı ailesinin ortak davranışı; varsayılan <see cref="Slider"/> teması bağlar.
/// Klavye adımları aralıktan çıkar (ok küçük, PageUp/PageDown büyük adım; Home/End Avalonia'nın
/// kendisinde): 50-10000 kbps aralığında sabit 1/10 adım okla kullanılamıyordu.
/// Değer sürükleme dışında değişince (klavye, kutuya yazılan sayı, ön ayar) başparmak ve iki iz
/// eski yerinden yeni yerine kayar — yalnız RenderTransform, düzen bir kez hesaplanır (FLIP).
/// Sürüklerken hareket yok: başparmak imleçle birlikte gider.
/// </summary>
public static class KaydiriciHareketi
{
    public static readonly AttachedProperty<bool> EtkinProperty =
        AvaloniaProperty.RegisterAttached<Slider, bool>("Etkin", typeof(KaydiriciHareketi));

    private static readonly ConditionalWeakTable<Slider, Bekleyen> Bekleyenler = new();

    static KaydiriciHareketi()
    {
        EtkinProperty.Changed.AddClassHandler<Slider>(Kur);
    }

    public static bool GetEtkin(Slider kaydirici) => kaydirici.GetValue(EtkinProperty);

    public static void SetEtkin(Slider kaydirici, bool deger) => kaydirici.SetValue(EtkinProperty, deger);

    /// <summary>
    /// Aralıktan klavye adımları. Tık sıklığı varsa küçük adım odur, büyük adım onun katı.
    /// Yoksa küçük adım aralığın beş yüzde birinin yukarı yuvarlanmış 1-2-5 basamağı (en az 1),
    /// büyük adım onda birinin.
    /// </summary>
    internal static (double Kucuk, double Buyuk) Adimlar(double enAz, double enCok, double tik)
    {
        var aralik = enCok - enAz;
        if (!(aralik > 0) || double.IsInfinity(aralik)) return (1, 1);
        if (tik > 0)
        {
            var kat = Math.Max(1, Math.Round(Basamak(aralik / 10) / tik));
            return (tik, kat * tik);
        }
        var kucuk = Math.Max(1, Basamak(aralik / 500));
        return (kucuk, Math.Max(kucuk, Basamak(aralik / 10)));
    }

    private static double Basamak(double x)
    {
        if (!(x > 0)) return 0;
        var us = Math.Pow(10, Math.Floor(Math.Log10(x)));
        foreach (var c in new[] { 1d, 2d, 5d })
            if (c * us >= x * (1 - 1e-9)) return c * us;
        return 10 * us;
    }

    private static void Kur(Slider kaydirici, AvaloniaPropertyChangedEventArgs e)
    {
        kaydirici.PropertyChanged -= Degisti;
        kaydirici.TemplateApplied -= SablonKuruldu;
        kaydirici.Classes.CollectionChanged -= DurumDegisti;
        if (e.NewValue is not true) return;
        kaydirici.PropertyChanged += Degisti;
        kaydirici.TemplateApplied += SablonKuruldu;
        kaydirici.Classes.CollectionChanged += DurumDegisti;
        AdimKur(kaydirici);
        Yansit(kaydirici);
    }

    private static readonly ConditionalWeakTable<Classes, Slider> Sahipler = new();

    private static void SablonKuruldu(object? gonderen, TemplateAppliedEventArgs e)
    {
        if (gonderen is not Slider kaydirici) return;
        Sahipler.AddOrUpdate(kaydirici.Classes, kaydirici);
        Yansit(kaydirici);
    }

    private static void DurumDegisti(object? gonderen, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (gonderen is Classes siniflar && Sahipler.TryGetValue(siniflar, out var kaydirici)) Yansit(kaydirici);
    }

    /// <summary>
    /// Kaydırıcının üstünde/basılı/odak durumunu iz parçalarına ve başparmağa sınıf olarak geçirir
    /// (<c>ustunde</c>, <c>basili</c>, <c>odak</c>): Avalonia bir stil seçicisinde iki
    /// <c>/template/</c> adımına izin vermiyor, parçaların kendi temaları bu sınıflara bakar.
    /// </summary>
    internal static void Yansit(Slider kaydirici)
    {
        Sahipler.AddOrUpdate(kaydirici.Classes, kaydirici);
        var iz = kaydirici.GetVisualDescendants().OfType<Track>().FirstOrDefault();
        if (iz is null) return;
        var ustunde = kaydirici.Classes.Contains(":pointerover");
        var basili = kaydirici.Classes.Contains(":pressed");
        var odak = kaydirici.Classes.Contains(":focus-visible");
        foreach (var parca in new Control?[] { iz.Thumb, iz.DecreaseButton, iz.IncreaseButton })
        {
            if (parca is null) continue;
            parca.Classes.Set("ustunde", ustunde);
            parca.Classes.Set("basili", basili);
            parca.Classes.Set("odak", odak);
        }
    }

    private static void AdimKur(Slider kaydirici)
    {
        var (kucuk, buyuk) = Adimlar(kaydirici.Minimum, kaydirici.Maximum,
            kaydirici.IsSnapToTickEnabled ? kaydirici.TickFrequency : 0);
        kaydirici.SetCurrentValue(RangeBase.SmallChangeProperty, kucuk);
        kaydirici.SetCurrentValue(RangeBase.LargeChangeProperty, buyuk);
    }

    private static void Degisti(object? gonderen, AvaloniaPropertyChangedEventArgs e)
    {
        if (gonderen is not Slider kaydirici) return;
        if (e.Property == RangeBase.MinimumProperty || e.Property == RangeBase.MaximumProperty
            || e.Property == Slider.TickFrequencyProperty || e.Property == Slider.IsSnapToTickEnabledProperty)
            AdimKur(kaydirici);
        else if (e.Property == RangeBase.ValueProperty)
            Hazirla(kaydirici);
    }

    /// <summary>Sürükleme sürüyor mu: kaydırıcıya ya da başparmağa basılı.</summary>
    internal static bool Surukleniyor(Slider kaydirici, Track iz)
        => kaydirici.Classes.Contains(":pressed") || (iz.Thumb?.Classes.Contains(":pressed") ?? false);

    private static bool HareketAzaltildi(Slider kaydirici)
        => TopLevel.GetTopLevel(kaydirici) is { } ust && ust.Classes.Contains("reduced-motion");

    private static void Hazirla(Slider kaydirici)
    {
        if (!kaydirici.IsEffectivelyVisible || HareketAzaltildi(kaydirici)) return;
        var iz = kaydirici.GetVisualDescendants().OfType<Track>().FirstOrDefault();
        if (iz?.Thumb is not { } basparmak || iz.Bounds.Width <= 0) return;
        if (Surukleniyor(kaydirici, iz)) return;
        if (Bekleyenler.TryGetValue(kaydirici, out var mevcut) && mevcut.Kuruldu) return;

        var bekleyen = new Bekleyen
        {
            Iz = iz,
            IzGenisligi = iz.Bounds.Width,
            BasparmakX = basparmak.Bounds.X + Oteleme(basparmak),
            Dolu = iz.DecreaseButton?.Bounds ?? default,
            Bos = iz.IncreaseButton?.Bounds ?? default,
            Kuruldu = true,
        };
        Bekleyenler.AddOrUpdate(kaydirici, bekleyen);
        basparmak.PropertyChanged += bekleyen.SinirDegisti;
        bekleyen.Birak = () =>
        {
            basparmak.PropertyChanged -= bekleyen.SinirDegisti;
            bekleyen.Kuruldu = false;
        };
        bekleyen.Kaydirici = kaydirici;
    }

    private static double Oteleme(Visual v) => v.RenderTransform?.Value.M31 ?? 0;

    private sealed class Bekleyen
    {
        public Track Iz = null!;
        public Slider Kaydirici = null!;
        public double IzGenisligi;
        public double BasparmakX;
        public Rect Dolu;
        public Rect Bos;
        public bool Kuruldu;
        public Action Birak = () => { };

        public void SinirDegisti(object? gonderen, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != Visual.BoundsProperty || gonderen is not Thumb basparmak) return;
            Birak();
            if (Math.Abs(Iz.Bounds.Width - IzGenisligi) > 0.5) return;
            var dx = BasparmakX - basparmak.Bounds.X;
            if (Math.Abs(dx) < 0.5) return;
            var sure = Kaydirici.TryFindResource("MotionFast", out var s) && s is TimeSpan t ? t : TimeSpan.Zero;
            Oynat(basparmak, $"translateX({Sayi(dx)}px)", sure);
            if (Iz.DecreaseButton is { } dolu && dolu.Bounds.Width > 0.5)
            {
                dolu.RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
                Oynat(dolu, $"scaleX({Sayi(Dolu.Width / dolu.Bounds.Width)})", sure);
            }
            if (Iz.IncreaseButton is { } bos && bos.Bounds.Width > 0.5)
            {
                bos.RenderTransformOrigin = new RelativePoint(1, 0.5, RelativeUnit.Relative);
                Oynat(bos, $"scaleX({Sayi(Bos.Width / bos.Bounds.Width)})", sure);
            }
        }
    }

    private static string Sayi(double x) => x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static void Oynat(Visual v, string baslangic, TimeSpan sure)
    {
        v.Transitions = null;
        v.RenderTransform = TransformOperations.Parse(baslangic);
        v.Transitions = new Transitions
        {
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = sure,
                Easing = new CubicEaseOut(),
            },
        };
        v.RenderTransform = TransformOperations.Identity;
    }
}
