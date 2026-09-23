using VidShrink.App;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// <see cref="KucultSutunlari.SolGenislik"/>: sol sütun yalnız ızgarası eşit paya sığmıyorsa ve
/// öbür iki sütun en dar penceredeki eşit payın altına inmiyorsa genişler. Sayılar 1560 ve
/// 1136 px pencerenin ölçülen ızgara genişlikleri (1500, 1076), kabuk 62, dört sütun 496.
/// </summary>
public sealed class KucultSutunlariTests
{
    [Fact]
    public void GenisPenceredeSolSutunIzgaraKadarGenisler()
        => Assert.Equal(558, KucultSutunlari.SolGenislik(1500, 24, 62, 496, 1076));

    [Fact]
    public void EnDarPenceredeEsitKalir()
        => Assert.Null(KucultSutunlari.SolGenislik(1076, 24, 62, 496, 1076));

    [Fact]
    public void IzgaraEsitPayaSigarsaEsitKalir()
        => Assert.Null(KucultSutunlari.SolGenislik(1500, 24, 62, 400, 1076));

    [Fact]
    public void OburSutunlarEnDarPayinAltinaInerseEsitKalir()
        => Assert.Null(KucultSutunlari.SolGenislik(1250, 24, 62, 496, 1076));

    [Fact]
    public void KabukBilinmiyorsaEsitKalir()
        => Assert.Null(KucultSutunlari.SolGenislik(1500, 24, double.NaN, 496, 1076));
}
