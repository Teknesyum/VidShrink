using System.Globalization;
using System.Text;
using VidShrink.Core;

namespace VidShrink.App.Playback;

internal enum KurtarmaAsamasi
{
    Bosta,
    SesYenilendi,
    Aciliyor,
    Acildi,
    Dustu
}

internal enum KurtarmaAdimi
{
    Yok,
    YenidenAc,
    Basarisiz,
    Kurtuldu
}

internal sealed class OynatmaKurtarici
{
    internal const double BeklemeSaniye = 2.0;

    private long _kare;
    private double _since;

    internal KurtarmaAsamasi Asama { get; private set; }

    internal bool Denendi { get; private set; }

    internal bool Baslat(long kare, double simdi)
    {
        if (Asama != KurtarmaAsamasi.Bosta) return false;
        Asama = KurtarmaAsamasi.SesYenilendi;
        Denendi = false;
        _kare = kare;
        _since = simdi;
        return true;
    }

    internal KurtarmaAdimi SesSonucu(bool yapildi, double simdi)
    {
        if (Asama != KurtarmaAsamasi.SesYenilendi) return KurtarmaAdimi.Yok;
        _since = simdi;
        if (yapildi)
        {
            Denendi = true;
            return KurtarmaAdimi.Yok;
        }

        Asama = KurtarmaAsamasi.Aciliyor;
        return KurtarmaAdimi.YenidenAc;
    }

    internal KurtarmaAdimi Gozle(long kare, double simdi)
    {
        if (Asama is KurtarmaAsamasi.Bosta or KurtarmaAsamasi.Aciliyor) return KurtarmaAdimi.Yok;
        if (kare != _kare)
        {
            var kurtuldu = Asama != KurtarmaAsamasi.Dustu;
            Asama = KurtarmaAsamasi.Bosta;
            Denendi = false;
            return kurtuldu ? KurtarmaAdimi.Kurtuldu : KurtarmaAdimi.Yok;
        }

        if (Asama == KurtarmaAsamasi.Dustu || simdi - _since < BeklemeSaniye) return KurtarmaAdimi.Yok;
        if (Asama == KurtarmaAsamasi.SesYenilendi)
        {
            Asama = KurtarmaAsamasi.Aciliyor;
            _since = simdi;
            return KurtarmaAdimi.YenidenAc;
        }

        Asama = KurtarmaAsamasi.Dustu;
        return KurtarmaAdimi.Basarisiz;
    }

    internal KurtarmaAdimi Acildi(bool basarili, double simdi)
    {
        if (Asama != KurtarmaAsamasi.Aciliyor) return KurtarmaAdimi.Yok;
        _since = simdi;
        if (!basarili)
        {
            Asama = KurtarmaAsamasi.Dustu;
            return KurtarmaAdimi.Basarisiz;
        }

        Denendi = true;
        Asama = KurtarmaAsamasi.Acildi;
        return KurtarmaAdimi.Yok;
    }

    internal void Iptal()
    {
        Asama = KurtarmaAsamasi.Bosta;
        Denendi = false;
    }
}

internal static class TakilmaGunlugu
{
    internal const string DosyaAdi = "oynatici-takilma.log";
    internal const int SinirBayt = 64 * 1024;
    internal const int SatirSiniri = 400;

    internal static string Klasor => Path.GetDirectoryName(Path.GetFullPath(UpdateSettings.DefaultPath)) ?? ".";

    internal static string Dosya => Path.Combine(Klasor, DosyaAdi);

    internal static bool Yaz(string olay, IEnumerable<string> satirlar, DateTime zaman)
    {
        try
        {
            var blok = new StringBuilder();
            blok.Append("=== ").Append(zaman.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(' ').Append(olay).Append('\n');
            foreach (var satir in satirlar)
            {
                var temiz = Gunluk.YoluIndirge(satir.TrimEnd());
                if (temiz.Length > SatirSiniri) temiz = temiz[..SatirSiniri];
                blok.Append(temiz).Append('\n');
            }

            Directory.CreateDirectory(Klasor);
            var dosya = Dosya;
            var metin = (File.Exists(dosya) ? File.ReadAllText(dosya) : "") + blok;
            File.WriteAllText(dosya, Kirp(metin), new UTF8Encoding(false));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    internal static string Kirp(string metin)
    {
        while (Encoding.UTF8.GetByteCount(metin) > SinirBayt)
        {
            var sonraki = metin.IndexOf("\n=== ", 1, StringComparison.Ordinal);
            if (sonraki < 0) return metin[^(SinirBayt / 4)..];
            metin = metin[(sonraki + 1)..];
        }

        return metin;
    }
}
