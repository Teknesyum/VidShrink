using System;
using System.IO;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Duraklatma simgesi ve kayit sonrasi vurgusu. Olculen sey cizim degil kaynagin kurdugu
/// karar: simgenin sahnede nerede durdugu, tik tutmadigi, olcusunun ve suresinin
/// belirtecten geldigi, ve yalniz duraklatma kolunda tetiklendigi.
/// </summary>
public sealed class DuraklatmaSimgesiTests
{
    private static string Oku(params string[] parca)
        => File.ReadAllText(Path.Combine(TipSources.Root, Path.Combine(parca))).Replace("\r\n", "\n");

    /// <summary>Simge kareyi ortaliyor, tik tutmuyor, olculeri belirtecten geliyor.</summary>
    [Fact]
    public void SimgeKareninUstundeVeTikGecirgen()
    {
        var xaml = Oku("src", "VidShrink.App", "Playback", "PlayerView.axaml");

        var kare = xaml.IndexOf("x:Name=\"Frame\"", StringComparison.Ordinal);
        var simge = xaml.IndexOf("x:Name=\"PauseGlyph\"", StringComparison.Ordinal);

        Assert.True(kare > 0 && simge > kare, "simge kareden sonra bildirilmeli");
        Assert.Contains("IsHitTestVisible=\"False\"", xaml[simge..(simge + 400)]);
        Assert.Contains("{StaticResource PauseGlyphSize}", xaml);
        Assert.Contains("{StaticResource IconPause}", xaml[simge..(simge + 900)]);
        Assert.Contains("TransformOperationsTransition", xaml[simge..(simge + 900)]);
    }

    /// <summary>Uc sayi da belirtecte: olcu, saydamlik, ekranda kalis.</summary>
    [Fact]
    public void OlculerBelirtecten()
    {
        var tema = Oku("src", "VidShrink.App", "Themes", "Theme.axaml");

        Assert.Contains("x:Key=\"PauseGlyphSize\"", tema);
        Assert.Contains("x:Key=\"PauseGlyphOpacity\"", tema);
        Assert.Contains("x:Key=\"PauseGlyphHold\"", tema);
    }

    /// <summary>
    /// Yalniz duraklatma kolunda tetikleniyor: oynatma kolunda goruntunun onune bir sey
    /// konmuyor, cunku orada her katman icerigi kapatir.
    /// </summary>
    [Fact]
    public void YalnizDuraklatmadaCagriliyor()
    {
        var kod = Oku("src", "VidShrink.App", "Playback", "PlayerView.axaml.cs");

        var toggle = kod.IndexOf("internal void TogglePlay()", StringComparison.Ordinal);
        var govde = kod[toggle..(toggle + 700)];

        var oynat = govde.IndexOf("engine.Play();", StringComparison.Ordinal);
        var duraklat = govde.IndexOf("engine.Pause();", StringComparison.Ordinal);
        var parlama = govde.IndexOf("FlashPause();", StringComparison.Ordinal);

        Assert.True(oynat > 0 && duraklat > oynat, "iki kol da bulunmali");
        Assert.True(parlama > duraklat, "parlama duraklatma kolunda olmali");
    }

    /// <summary>Kayit bitince hedef klasor ve paylas dugmeleri vurgulu temada.</summary>
    [Fact]
    public void KayitSonrasiIkiDugmeVurgulu()
    {
        var xaml = Oku("src", "VidShrink.App", "Recorder", "RecorderView.axaml");

        foreach (var ad in new[] { "BtnReveal", "BtnRecShare" })
        {
            var i = xaml.IndexOf($"x:Name=\"{ad}\"", StringComparison.Ordinal);
            Assert.True(i > 0, ad + " bulunmali");
            Assert.Contains("Theme=\"{StaticResource PrimaryButton}\"", xaml[i..(i + 200)]);
        }

        foreach (var ad in new[] { "BtnToShrink", "BtnToPlayer" })
        {
            var i = xaml.IndexOf($"x:Name=\"{ad}\"", StringComparison.Ordinal);
            Assert.Contains("Theme=\"{StaticResource GhostButton}\"", xaml[i..(i + 200)]);
        }
    }
}
