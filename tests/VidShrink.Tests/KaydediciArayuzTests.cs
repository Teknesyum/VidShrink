using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// 8b — kaydedici arayuzunun motorla uyumu.
///
/// <para>Arayuz kodlayici ve on ayar listesini kendi dosyasinda tutuyor, cunku motorun
/// <c>KnownVideoCodecs</c> dizisi <c>private</c>: listeyi oradan okumak mumkun degil.
/// Kopya listenin motordan kaymasi sessiz bir kusur olurdu — kullanici listeden bir ad
/// secer, kayit baslamaz. Bu olcu kaymayi kaymanin oldugu turda yakalar: listedeki her
/// adin <see cref="RecorderArguments.Validate"/>'ten gectigi tek tek sinanir.</para>
///
/// <para>Kabul olcusunun yaninda <b>negatif denetim</b> de duruyor: uydurma bir kodlayici
/// adi gercekten reddediliyor mu. O olmadan "hepsi gecti" bulgusu bir sey soylemez —
/// dogrulama her seyi kabul ediyor da olabilir.</para>
/// </summary>
public sealed class KaydediciArayuzTests
{
    private static string Output => Path.Combine(Path.GetTempPath(), "vidshrink-kaydedici-olcu.mp4");

    private static RecorderRequest Request(string? codec = null, string? preset = null) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Screen,
        VideoCodec = codec ?? RecorderArguments.DefaultVideoCodec,
        Preset = preset ?? RecorderArguments.DefaultPreset
    };

    /// <summary>Listelenen her kodlayici motorca kabul ediliyor.</summary>
    [Fact]
    public void SeritteListelenenHerKodlayiciMotordanGeciyor()
    {
        var sikayet = new List<string>();

        foreach (var codec in RecorderView.Codecs)
        {
            var errors = RecorderArguments.Validate(Request(codec: codec), Output);
            if (errors.Count > 0)
                sikayet.Add($"{codec}: {string.Join(" ", errors)}");
        }

        Assert.Empty(sikayet);
        Assert.Equal(RecorderView.Codecs.Length, RecorderView.Codecs.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Listelenen her on ayar motorca kabul ediliyor.</summary>
    [Fact]
    public void SeritteListelenenHerOnAyarMotordanGeciyor()
    {
        var sikayet = new List<string>();

        foreach (var preset in RecorderView.Presets)
        {
            var errors = RecorderArguments.Validate(Request(preset: preset), Output);
            if (errors.Count > 0)
                sikayet.Add($"{preset}: {string.Join(" ", errors)}");
        }

        Assert.Empty(sikayet);
        Assert.Equal(RecorderView.Presets.Length, RecorderView.Presets.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Negatif denetim: dogrulama her adi yutmuyor. Bu olcu kirmizi verirse yukaridaki iki
    /// olcunun yesili bir sey kanitlamiyor demektir.
    /// </summary>
    [Fact]
    public void UydurmaKodlayiciAdiReddediliyor()
    {
        var errors = RecorderArguments.Validate(Request(codec: "libhicboyle"), Output);
        Assert.NotEmpty(errors);
    }

    /// <summary>
    /// Arayuzun acilista gosterdigi degerler motorun varsayilanlariyla ayni. Ayri bir sayi
    /// yazilsaydi kullanici arayuzde bir sey, kayitta baskasini gorurdu.
    /// </summary>
    [Fact]
    public void AcilisDegerleriMotorunVarsayilanlariyla()
    {
        var settings = new RecorderSettings();

        Assert.Equal(RecorderArguments.DefaultFps, settings.Fps);
        Assert.Equal(RecorderArguments.DefaultVideoCodec, settings.Codec);
        Assert.Equal(RecorderArguments.DefaultPreset, settings.Preset);
        Assert.Equal(RecorderArguments.DefaultQuality, settings.Quality);
        Assert.Contains(settings.Codec, RecorderView.Codecs);
        Assert.Contains(settings.Preset, RecorderView.Presets);
    }

    /// <summary>
    /// Hedef listesinin sirasi <see cref="RecorderTargetKind"/> ile ayni. Arayuz secimi
    /// indeksten okudugu icin sira kaymasi yanlis hedefi kaydettirirdi.
    /// </summary>
    [Fact]
    public void HedefListesininSirasiNumaralandirmayaBagli()
    {
        Assert.Equal(0, (int)RecorderTargetKind.Screen);
        Assert.Equal(1, (int)RecorderTargetKind.Window);
        Assert.Equal(2, (int)RecorderTargetKind.Region);
    }

    /// <summary>
    /// Gecen surenin yazimi. Bir saatin altinda dakika:saniye, ustunde saat de yaziliyor;
    /// sayi bicimi degismez kulturden geliyor, dil degisince kaymiyor.
    /// </summary>
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(9, "00:09")]
    [InlineData(61, "01:01")]
    [InlineData(599, "09:59")]
    [InlineData(3600, "01:00:00")]
    [InlineData(3732, "01:02:12")]
    public void GecenSureninYazimi(int saniye, string beklenen)
        => Assert.Equal(beklenen, RecorderView.Clock(TimeSpan.FromSeconds(saniye)));

    /// <summary>
    /// Ayni saniyede iki kayit baslarsa ikincisi birincinin uzerine yazmiyor. Dosya adi
    /// zaman damgasindan geldigi icin bu gercek bir carpisma.
    /// </summary>
    [Fact]
    public void AyniSaniyedekiIkinciKayitUstuneYazmiyor()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vidshrink-kaydedici-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var settings = new RecorderSettings { OutputFolder = folder };
            var now = new DateTime(2026, 9, 12, 14, 30, 0, DateTimeKind.Local);

            var first = settings.OutputPath(now);
            File.WriteAllText(first, string.Empty);
            var second = settings.OutputPath(now);

            Assert.NotEqual(first, second);
            Assert.False(File.Exists(second));
            Assert.Equal(folder, Path.GetDirectoryName(second));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
