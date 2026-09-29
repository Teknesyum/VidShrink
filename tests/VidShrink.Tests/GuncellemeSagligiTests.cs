using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class GuncellemeSagligiTests : IDisposable
{
    private readonly string _ayar = Path.Combine(TipSources.Root, ".calisma", "test-ciktilari", "guncelleme-sagligi", Guid.NewGuid().ToString("N"), "settings.json");

    public GuncellemeSagligiTests() => Directory.CreateDirectory(Path.GetDirectoryName(_ayar)!);

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_ayar)!, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public void UcArtArdaDususTakildiDer()
    {
        var an = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
        var birinci = UpdateHealth.RecordFailure(_ayar, an, "zaman asimi");
        var ikinci = UpdateHealth.RecordFailure(_ayar, an.AddDays(1), "zaman asimi");
        var ucuncu = UpdateHealth.RecordFailure(_ayar, an.AddDays(2), "404");

        Assert.Equal((1, false), (birinci.Failures, birinci.Stuck));
        Assert.Equal((2, false), (ikinci.Failures, ikinci.Stuck));
        Assert.Equal((3, true), (ucuncu.Failures, ucuncu.Stuck));

        var okunan = UpdateHealth.Load(_ayar);
        Assert.True(okunan.Stuck);
        Assert.Equal(an, okunan.FailingSince);
        Assert.Equal("404", okunan.LastError);
    }

    [Fact]
    public void BasariSayaciSifirlar()
    {
        var an = DateTimeOffset.UtcNow;
        for (var i = 0; i < UpdateHealth.FailureLimit; i++) UpdateHealth.RecordFailure(_ayar, an, "x");
        UpdateHealth.RecordSuccess(_ayar, an);

        var okunan = UpdateHealth.Load(_ayar);
        Assert.False(okunan.Stuck);
        Assert.Equal(0, okunan.Failures);
        Assert.Null(okunan.FailingSince);
        Assert.Equal(an, okunan.LastSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bozuk{")]
    [InlineData("[1,2]")]
    [InlineData("{\"failures\":\"uc\"}")]
    public void BozukKayitSaglamSayilir(string icerik)
    {
        File.WriteAllText(UpdateHealth.PathFor(_ayar), icerik);
        var okunan = UpdateHealth.Load(_ayar);
        Assert.Equal(0, okunan.Failures);
        Assert.False(okunan.Stuck);
    }

    [Fact]
    public void KayitAyarDosyasininYanindaDurur()
    {
        Assert.Equal(_ayar + "." + UpdateHealth.FileName, UpdateHealth.PathFor(_ayar));
        Assert.Equal(Path.GetDirectoryName(UpdateSettings.DefaultPath), Path.GetDirectoryName(UpdateHealth.PathFor(null)));
    }
}
