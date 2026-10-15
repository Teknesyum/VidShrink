using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// AYR-47: elle "şimdi denetle". Ağa çıkmaz: manifest sahte istemciden gelir. Her sonuç
/// <see cref="UpdateHealth"/>'e işlenir; düşüş sayılmayan yol kalmaz.
/// </summary>
public sealed class ElleDenetimTests
{
    private static readonly string Klasor = Path.Combine(TipSources.Root, ".calisma", "test-ciktilari", "elle-denetim");
    private static readonly DateTimeOffset Simdi = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static string AyarYolu()
    {
        Directory.CreateDirectory(Klasor);
        return Path.Combine(Klasor, Guid.NewGuid().ToString("N") + ".json");
    }

    private static string Manifest(string surum) =>
        $$"""{"version":"{{surum}}","commit":"x","built":"2026-10-01T00:00:00Z","rid":"win-x64","files":[]}""";

    [Fact]
    public async Task GuncelSurumBasariSayilir()
    {
        var ayar = AyarYolu();
        UpdateHealth.RecordFailure(ayar, Simdi.AddDays(-1), "onceki");

        var sonuc = await ElleDenetim.CalistirAsync(_ => Task.FromResult<string?>(Manifest("1.2.17")), "1.2.17", ayar, Simdi, CancellationToken.None);

        var saglik = UpdateHealth.Load(ayar);
        Assert.Equal(ElleDenetimDurumu.Guncel, sonuc.Durum);
        Assert.Equal("1.2.17", sonuc.Surum);
        Assert.Equal(Simdi, sonuc.Zaman);
        Assert.Equal(0, saglik.Failures);
        Assert.Equal(Simdi, saglik.LastSuccess);
    }

    /// <summary>Yeni sürüm bulunması sağlığı sıfırlamaz: indirme yolu kendi sonucunu sayar.</summary>
    [Fact]
    public async Task YeniSurumSagligiSifirlamaz()
    {
        var ayar = AyarYolu();
        UpdateHealth.RecordFailure(ayar, Simdi.AddDays(-1), "onceki");

        var sonuc = await ElleDenetim.CalistirAsync(_ => Task.FromResult<string?>(Manifest("1.2.18")), "1.2.17+abc", ayar, Simdi, CancellationToken.None);

        Assert.Equal(ElleDenetimDurumu.YeniSurum, sonuc.Durum);
        Assert.Equal("1.2.18", sonuc.Surum);
        Assert.Equal(1, UpdateHealth.Load(ayar).Failures);
    }

    [Fact]
    public async Task AgHatasiDususSayilir()
    {
        var ayar = AyarYolu();

        var sonuc = await ElleDenetim.CalistirAsync(_ => throw new HttpRequestException("yok"), "1.2.17", ayar, Simdi, CancellationToken.None);

        var saglik = UpdateHealth.Load(ayar);
        Assert.Equal(ElleDenetimDurumu.Dustu, sonuc.Durum);
        Assert.Equal(1, saglik.Failures);
        Assert.StartsWith("elle-denetim", saglik.LastError);
        Assert.Contains("HttpRequestException", saglik.LastError);
    }

    /// <summary>İstemcinin boş dönüşü (404, zaman aşımı) de düşüştür; sessiz geçmez.</summary>
    [Fact]
    public async Task BosManifestDususSayilir()
    {
        var ayar = AyarYolu();

        var sonuc = await ElleDenetim.CalistirAsync(_ => Task.FromResult<string?>(null), "1.2.17", ayar, Simdi, CancellationToken.None);

        Assert.Equal(ElleDenetimDurumu.Dustu, sonuc.Durum);
        Assert.Equal(1, UpdateHealth.Load(ayar).Failures);
    }

    [Fact]
    public async Task BozukManifestDususSayilir()
    {
        var ayar = AyarYolu();

        var sonuc = await ElleDenetim.CalistirAsync(_ => Task.FromResult<string?>("{bozuk"), "1.2.17", ayar, Simdi, CancellationToken.None);

        Assert.Equal(ElleDenetimDurumu.Dustu, sonuc.Durum);
        Assert.Equal(1, UpdateHealth.Load(ayar).Failures);
    }

    /// <summary>Kullanıcının iptali düşüş değildir; sayılmaz ve dışarı taşınır.</summary>
    [Fact]
    public async Task IptalSayilmaz()
    {
        var ayar = AyarYolu();
        using var iptal = new CancellationTokenSource();
        iptal.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ElleDenetim.CalistirAsync(ct => Task.FromCanceled<string?>(ct), "1.2.17", ayar, Simdi, iptal.Token));

        Assert.Equal(0, UpdateHealth.Load(ayar).Failures);
    }
}
