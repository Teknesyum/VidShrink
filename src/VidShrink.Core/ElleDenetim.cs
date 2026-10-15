namespace VidShrink.Core;

/// <summary>Elle denetimin üç sonucu.</summary>
public enum ElleDenetimDurumu
{
    /// <summary>Yayındaki sürüm kurulu olandan yeni değil.</summary>
    Guncel,

    /// <summary>Yayında daha yeni bir sürüm var.</summary>
    YeniSurum,

    /// <summary>Manifest alınamadı ya da okunamadı.</summary>
    Dustu
}

/// <summary>Elle denetimin sonucu; Hakkında'daki "son denetim" satırı bundan yazılır.</summary>
public sealed record ElleDenetimSonucu(ElleDenetimDurumu Durum, string? Surum, DateTimeOffset Zaman);

/// <summary>
/// AYR-47: otomatik güncelleme açıkken de koşan "şimdi denetle". Yalnız yayın manifestini
/// sorar, indirmez. Her sonuç <see cref="UpdateHealth"/>'e işlenir: güncelse başarı, alınamaz
/// ya da okunamazsa düşüş. Yeni sürüm sağlığa dokunmaz; onu indirme yolu sayar.
/// Kullanıcının iptali düşüş değildir.
/// </summary>
public static class ElleDenetim
{
    /// <summary>Gerçek istemci: platformun manifestini yayının son sürümünden ister.</summary>
    public static Task<string?> ManifestiGetir(CancellationToken iptal) =>
        UpdateCheck.FetchManifestAsync(UpdateCheck.LatestAssetUrl(UpdateCheck.ManifestAssetName(UpdateCheck.Rid)), iptal);

    public static async Task<ElleDenetimSonucu> CalistirAsync(
        Func<CancellationToken, Task<string?>> manifestGetir,
        string mevcutSurum,
        string? ayarYolu,
        DateTimeOffset simdi,
        CancellationToken iptal)
    {
        string surum;
        try
        {
            var json = await manifestGetir(iptal).ConfigureAwait(false);
            if (json is null) return Dus(ayarYolu, simdi, "manifest-yok");
            surum = UpdateCheck.ParseManifest(json).Version;
        }
        catch (OperationCanceledException) when (iptal.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception hata)
        {
            return Dus(ayarYolu, simdi, $"{hata.GetType().Name}: {hata.Message}");
        }

        if (UpdateCheck.IsNewer(surum, mevcutSurum)) return new ElleDenetimSonucu(ElleDenetimDurumu.YeniSurum, surum, simdi);
        UpdateHealth.RecordSuccess(ayarYolu, simdi);
        return new ElleDenetimSonucu(ElleDenetimDurumu.Guncel, surum, simdi);
    }

    private static ElleDenetimSonucu Dus(string? ayarYolu, DateTimeOffset simdi, string sebep)
    {
        UpdateHealth.RecordFailure(ayarYolu, simdi, "elle-denetim " + sebep);
        return new ElleDenetimSonucu(ElleDenetimDurumu.Dustu, null, simdi);
    }
}
