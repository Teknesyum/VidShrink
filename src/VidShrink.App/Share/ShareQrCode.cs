using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using QRCoder;

namespace VidShrink.App.Share;

/// <summary>
/// Paylaşım bağlantısının telefonla okutulacak QR kodu. Üç yüzey de (kaydedici, küçültme
/// sekmesi, küçültme penceresi) bu tek parçayı kullanır; <see cref="Link"/> boşken görünmez,
/// yani yükleme sürerken ve hata durumunda ekranda QR kalmaz. Bağlantı değişince matris
/// yeniden kurulur.
///
/// <para>Renk yazılı değil: plaka ve modül paletin dört opak belirtecinden seçilir
/// (<see cref="ShareQr.Tokens"/>), plaka en açığı, modül en koyusu. Koyu paletlerde de
/// QR açık plaka üstünde koyu modülle çizilir, hiçbir zaman ters değil. Plakanın kenarında
/// <see cref="ShareQr.QuietModules"/> modüllük sessiz bölge durur. Ölçü: <c>PaylasimQrTests</c>.</para>
/// </summary>
internal sealed class ShareQrCode : Control
{
    public static readonly StyledProperty<string?> LinkProperty =
        AvaloniaProperty.Register<ShareQrCode, string?>(nameof(Link));

    public static readonly StyledProperty<double> ModuleSizeProperty =
        AvaloniaProperty.Register<ShareQrCode, double>(nameof(ModuleSize), 1d);

    private bool[,]? _matrix;
    private StreamGeometry? _modules;

    static ShareQrCode()
    {
        AffectsMeasure<ShareQrCode>(LinkProperty, ModuleSizeProperty);
        AffectsRender<ShareQrCode>(LinkProperty, ModuleSizeProperty);
    }

    public ShareQrCode()
    {
        IsVisible = false;
        ResourcesChanged += (_, _) => InvalidateVisual();
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>Kodlanan adres; boşsa denetim görünmez.</summary>
    public string? Link
    {
        get => GetValue(LinkProperty);
        set => SetValue(LinkProperty, value);
    }

    /// <summary>Bir modülün kenarı; değer <c>ShareQrModuleSize</c> belirtecinden gelir.</summary>
    public double ModuleSize
    {
        get => GetValue(ModuleSizeProperty);
        set => SetValue(ModuleSizeProperty, value);
    }

    /// <summary>Çizilen matris, sessiz bölge hariç; bağlantı yoksa boş.</summary>
    internal bool[,]? Matrix => _matrix;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LinkProperty)
        {
            _matrix = ShareQr.Encode(Link);
            _modules = null;
            IsVisible = _matrix is not null;
        }
        else if (change.Property == ModuleSizeProperty)
        {
            _modules = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_matrix is null) return default;
        var side = Side(_matrix);
        return new Size(side, side);
    }

    public override void Render(DrawingContext context)
    {
        if (_matrix is null) return;
        if (ShareQr.Pick(Lookup) is not { } colours) return;

        var side = Side(_matrix);
        context.FillRectangle(new ImmutableSolidColorBrush(colours.Plate), new Rect(0, 0, side, side));
        context.DrawGeometry(new ImmutableSolidColorBrush(colours.Module), null, _modules ??= Build(_matrix, ModuleSize));
    }

    private Color? Lookup(string key)
        => this.TryFindResource(key, ActualThemeVariant, out var value) && value is Color colour ? colour : null;

    private double Side(bool[,] matrix) => (matrix.GetLength(0) + (2 * ShareQr.QuietModules)) * ModuleSize;

    private static StreamGeometry Build(bool[,] matrix, double module)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        var count = matrix.GetLength(0);
        for (var row = 0; row < count; row++)
        {
            var column = 0;
            while (column < count)
            {
                if (!matrix[row, column])
                {
                    column++;
                    continue;
                }

                var start = column;
                while (column < count && matrix[row, column]) column++;

                var left = (start + ShareQr.QuietModules) * module;
                var right = (column + ShareQr.QuietModules) * module;
                var top = (row + ShareQr.QuietModules) * module;
                var bottom = top + module;
                context.BeginFigure(new Point(left, top), true);
                context.LineTo(new Point(right, top));
                context.LineTo(new Point(right, bottom));
                context.LineTo(new Point(left, bottom));
                context.EndFigure(true);
            }
        }

        return geometry;
    }
}

/// <summary>QR matrisi ve renk seçimi; denetimden ayrı ki ölçü piksel çizmeden de okuyabilsin.</summary>
internal static class ShareQr
{
    /// <summary>QR şartnamesinin istediği en küçük sessiz bölge, modül cinsinden.</summary>
    internal const int QuietModules = 4;

    /// <summary>Plaka ve modülün seçildiği palet belirteçleri; hepsi her palette opak.</summary>
    internal static readonly IReadOnlyList<string> Tokens = new[] { "AppBgColor", "SurfaceToneColor", "TextBodyColor", "OnNeonColor" };

    /// <summary>
    /// Bağlantının modül matrisi, sessiz bölge hariç. QRCoder kendi dört modüllük boşluğunu
    /// ekliyor; o kırpılır, boşluk çizimde <see cref="QuietModules"/> ile verilir.
    /// </summary>
    internal static bool[,]? Encode(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return null;

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(link, QRCodeGenerator.ECCLevel.M);
        var rows = data.ModuleMatrix;
        var size = 21 + (4 * (data.Version - 1));
        var offset = (rows.Count - size) / 2;

        var matrix = new bool[size, size];
        for (var row = 0; row < size; row++)
            for (var column = 0; column < size; column++)
                matrix[row, column] = rows[row + offset][column + offset];
        return matrix;
    }

    /// <summary>
    /// Paletin opak belirteçlerinden plaka en açığı, modül en koyusu. Bir belirteç
    /// bulunamazsa ya da seçilen iki renk aynı parlaklıktaysa boş döner.
    /// </summary>
    internal static (Color Plate, Color Module)? Pick(Func<string, Color?> lookup)
    {
        Color? plate = null;
        Color? module = null;
        foreach (var key in Tokens)
        {
            if (lookup(key) is not { } colour) return null;
            if (plate is null || Luminance(colour) > Luminance(plate.Value)) plate = colour;
            if (module is null || Luminance(colour) < Luminance(module.Value)) module = colour;
        }

        if (plate is null || module is null || Luminance(plate.Value) <= Luminance(module.Value)) return null;
        return (plate.Value, module.Value);
    }

    /// <summary>WCAG karşıtlık oranı.</summary>
    internal static double Contrast(Color a, Color b)
    {
        var x = Luminance(a);
        var y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    /// <summary>sRGB bağıl parlaklık.</summary>
    internal static double Luminance(Color colour)
    {
        static double Channel(byte value)
        {
            var part = value / 255.0;
            return part <= 0.03928 ? part / 12.92 : Math.Pow((part + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(colour.R)) + (0.7152 * Channel(colour.G)) + (0.0722 * Channel(colour.B));
    }
}
