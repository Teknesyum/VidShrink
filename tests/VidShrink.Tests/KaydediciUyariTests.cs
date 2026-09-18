using System.IO;
using Avalonia.Controls;
using Sekil = Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Styling;
using VidShrink.App.Recorder;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Yarim kayit uyaridir, basarisiz kayit hatadir. 27 paletin hicbirinde uyari hue'su yok ve
/// elle secmek "renk uydurma" yasagini cignerdi; karar
/// <c>docs/netlestirme/018-uyari-rengi-27-palette-yok.md</c>: ayrimi renk degil bicim tasir.
/// Bu sinif iki kolu da cizip okur — uyarinin hata fircasiyla yazilmadigini ve ucgen-unlem
/// simgesinin yalniz uyarida gorundugunu. Negatif kontrol gercek hata kolu: kirmizi kaliyor.
/// </summary>
public sealed class KaydediciUyariTests
{
    private static RecordSahne Sahne(bool yarim) => AppHost.Run(() =>
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var klasor = Path.GetTempPath();
        var mkv = Path.Combine(klasor, "uyari-olcu.mkv");

        var gorunum = new RecorderView(ayar.Yol);
        gorunum.ShowResult(new VidShrink.Ffmpeg.RecordResult(
            Ok: !yarim ? false : true,
            OutputPath: mkv,
            OutputMb: 1,
            Partial: yarim,
            ExitCode: yarim ? 0 : 1,
            StandardError: string.Empty,
            Segments: 1));

        var satir = (StackPanel)gorunum.FindControl<StackPanel>("WarningRow")!;
        var simge = (Sekil.Path)gorunum.FindControl<Sekil.Path>("WarningGlyph")!;
        var metin = (TextBlock)gorunum.FindControl<TextBlock>("TxtWarning")!;

        return new RecordSahne(
            satir.IsVisible,
            simge.IsVisible,
            (metin.Theme as ControlTheme)?.ToString() ?? string.Empty,
            Firca(gorunum, "StatusError"),
            Firca(gorunum, "StatusWarning"),
            metin.Theme);
    });

    private static IBrush? Firca(Control kok, string anahtar)
        => Avalonia.Application.Current?.TryFindResource(anahtar, out var deger) == true && deger is ControlTheme tema
            ? TemaFircasi(tema)
            : null;

    private static IBrush? TemaFircasi(ControlTheme tema)
    {
        foreach (var setter in tema.Setters)
            if (setter is Setter s && s.Property == TextBlock.ForegroundProperty)
                return s.Value as IBrush;
        return null;
    }

    private sealed record RecordSahne(
        bool SatirGorunur,
        bool SimgeGorunur,
        string TemaAdi,
        IBrush? HataFircasi,
        IBrush? UyariFircasi,
        ControlTheme? Tema);

    [Fact]
    public void YarimKayitUyariGibiCizilir()
    {
        var sahne = Sahne(yarim: true);

        Assert.True(sahne.SatirGorunur);
        Assert.True(sahne.SimgeGorunur);
        Assert.Same(TemaFircasi(sahne.Tema!), sahne.UyariFircasi);
        Assert.NotSame(TemaFircasi(sahne.Tema!), sahne.HataFircasi);
    }

    [Fact]
    public void BasarisizKayitHataGibiCizilir()
    {
        var sahne = Sahne(yarim: false);

        Assert.True(sahne.SatirGorunur);
        Assert.False(sahne.SimgeGorunur);
        Assert.Same(TemaFircasi(sahne.Tema!), sahne.HataFircasi);
    }

    /// <summary>
    /// Iki fircanin gercekten ayri olmasi olcunun on sarti: ayni fircaya baglansalardi
    /// yukaridaki iki test de gecerdi ve ayrim kagit uzerinde kalirdi.
    /// </summary>
    [Fact]
    public void UyariVeHataFircalariAyri()
    {
        var sahne = Sahne(yarim: true);

        Assert.NotNull(sahne.UyariFircasi);
        Assert.NotNull(sahne.HataFircasi);
        Assert.NotSame(sahne.UyariFircasi, sahne.HataFircasi);
    }
}
