using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

public sealed class ZamankoduTests
{
    private static long FrameTicks(double fps) => fps switch
    {
        23.976 => 10010,
        29.97 => 8008,
        59.94 => 4004,
        _ => (long)Math.Round(EditTime.TicksPerSecond / fps),
    };

    [Theory]
    [InlineData(23.976)]
    [InlineData(25.0)]
    [InlineData(29.97)]
    [InlineData(30.0)]
    [InlineData(59.94)]
    [InlineData(60.0)]
    public void Yazip_geri_okuma_kayipsiz(double fps)
    {
        var step = FrameTicks(fps);
        var hours = 4L * 3600 * EditTime.TicksPerSecond / step;
        for (long frame = 0; frame <= hours; frame += frame < 20000 ? 1 : 997)
        {
            var time = frame * step;
            var text = EditTimecode.Format(time, fps);
            Assert.True(EditTimecode.TryParse(text, fps, out var back), text);
            Assert.Equal(time, back);
        }
    }

    [Theory]
    [InlineData(23.976)]
    [InlineData(25.0)]
    [InlineData(29.97)]
    [InlineData(30.0)]
    [InlineData(59.94)]
    [InlineData(60.0)]
    public void Kare_kare_art_arda_gelir(double fps)
    {
        var step = FrameTicks(fps);
        var previous = -1L;
        for (long frame = 0; frame < 110_000; frame++)
        {
            Assert.True(EditTimecode.TryParse(EditTimecode.Format(frame * step, fps), fps, out var back));
            Assert.Equal(frame * step, back);
            Assert.True(back > previous);
            previous = back;
        }
    }

    [Fact]
    public void Dusmeyen_bicimde_ayirac_iki_nokta_ust_uste()
    {
        Assert.Equal("00:00:01:00", EditTimecode.Format(EditTime.TicksPerSecond, 25));
        Assert.Equal("00:00:01:05", EditTimecode.Format(EditTime.TicksPerSecond + 5 * 9600, 25));
        Assert.Equal("00:00:01:00", EditTimecode.Format(EditTime.TicksPerSecond * 24 / 24, 24));
    }

    [Fact]
    public void Dusen_kare_bilinen_degerler_29_97()
    {
        Assert.Equal("00:00:59;29", EditTimecode.Format(1799 * 8008L, 29.97));
        Assert.Equal("00:01:00;02", EditTimecode.Format(1800 * 8008L, 29.97));
        Assert.Equal("00:01:01;00", EditTimecode.Format((1800 + 28) * 8008L, 29.97));
        Assert.Equal("00:09:59;29", EditTimecode.Format(17981 * 8008L, 29.97));
        Assert.Equal("00:10:00;00", EditTimecode.Format(17982 * 8008L, 29.97));
        Assert.Equal("01:00:00;00", EditTimecode.Format(107892 * 8008L, 29.97));
    }

    [Fact]
    public void Dusen_kare_bilinen_degerler_59_94()
    {
        Assert.Equal("00:00:59;59", EditTimecode.Format(3599 * 4004L, 59.94));
        Assert.Equal("00:01:00;04", EditTimecode.Format(3600 * 4004L, 59.94));
        Assert.Equal("00:10:00;00", EditTimecode.Format(35964 * 4004L, 59.94));
    }

    [Fact]
    public void Dusen_kare_ayrilmis_numaralar_reddedilir()
    {
        Assert.False(EditTimecode.TryParse("00:01:00;00", 29.97, out _));
        Assert.False(EditTimecode.TryParse("00:01:00;01", 29.97, out _));
        Assert.True(EditTimecode.TryParse("00:01:00;02", 29.97, out var t));
        Assert.Equal(1800 * 8008L, t);
        Assert.True(EditTimecode.TryParse("00:10:00;00", 29.97, out t));
        Assert.Equal(17982 * 8008L, t);
        Assert.False(EditTimecode.TryParse("00:01:00;03", 59.94, out _));
        Assert.True(EditTimecode.TryParse("00:01:00;04", 59.94, out _));
    }

    [Fact]
    public void Kisaltilmis_girdiler_sagdan_hizalanir()
    {
        Assert.True(EditTimecode.TryParse("10515", 25, out var t));
        Assert.Equal((65 * 25L + 15) * 9600, t);
        Assert.True(EditTimecode.TryParse("1:02:03", 25, out t));
        Assert.Equal((62 * 25L + 3) * 9600, t);
        Assert.True(EditTimecode.TryParse("5", 25, out t));
        Assert.Equal(5 * 9600L, t);
        Assert.True(EditTimecode.TryParse("120", 25, out t));
        Assert.Equal((1 * 25L + 20) * 9600, t);
        Assert.True(EditTimecode.TryParse("00:00:00:00", 25, out t));
        Assert.Equal(0, t);
        Assert.True(EditTimecode.TryParse("  00:01:05:15 ", 25, out t));
        Assert.Equal((65 * 25L + 15) * 9600, t);
    }

    [Fact]
    public void Goreli_kare_tabana_eklenir()
    {
        var baseTime = 100 * 9600L;
        Assert.True(EditTimecode.TryParse("+10", 25, baseTime, out var t));
        Assert.Equal(110 * 9600L, t);
        Assert.True(EditTimecode.TryParse("-10", 25, baseTime, out t));
        Assert.Equal(90 * 9600L, t);
        Assert.False(EditTimecode.TryParse("-101", 25, baseTime, out _));
        Assert.False(EditTimecode.TryParse("+10", 25, out _));
        Assert.True(EditTimecode.TryParse("00:00:01:00", 25, baseTime, out t));
        Assert.Equal(EditTime.TicksPerSecond, t);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1:2:x")]
    [InlineData("00:00:00:25")]
    [InlineData("00:60:00:00")]
    [InlineData("00:00:60:00")]
    [InlineData("1:2:3:4:5")]
    [InlineData("1::2")]
    [InlineData("+")]
    [InlineData("+x")]
    [InlineData("12345678901234")]
    [InlineData("1.5x")]
    public void Hatali_girdi_false_doner(string text)
    {
        Assert.False(EditTimecode.TryParse(text, 25, 0, out var t));
        Assert.Equal(0, t);
        Assert.False(EditTimecode.TryParse(text, 25, out _));
    }

    [Fact]
    public void Null_ve_gecersiz_hiz_false_doner()
    {
        Assert.False(EditTimecode.TryParse(null, 25, out _));
        Assert.False(EditTimecode.TryParse("10", 0, out _));
        Assert.False(EditTimecode.TryParse("10", double.NaN, out _));
    }
}
