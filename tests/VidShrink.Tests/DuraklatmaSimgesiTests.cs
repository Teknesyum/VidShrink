using System;
using System.IO;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kayit sonrasi vurgusu. Duraklatma simgesinin davranisi (yarim saniye, ortada, tik
/// gecirgen, yalniz duraklatmada) <see cref="OynaticiYolHaritasiTests"/>'te olculur.
/// </summary>
public sealed class DuraklatmaSimgesiTests
{
    private static string Oku(params string[] parca)
        => File.ReadAllText(Path.Combine(TipSources.Root, Path.Combine(parca))).Replace("\r\n", "\n");

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
