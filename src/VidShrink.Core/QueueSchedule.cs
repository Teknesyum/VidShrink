using System.Globalization;

namespace VidShrink.Core;

/// <summary>SIK-P7: kuyruğun ne zaman başlayacağı; saatte ya da şu kadar sonra.</summary>
public enum QueueStartMode
{
    At,
    After
}

/// <summary>
/// SIK-P7: zamanlama girişini ana çevirir. "Şu saatte" <c>SS:dd</c> ya da <c>S.dd</c> alır; şu andan
/// önceki ya da şu ana eşit saat ertesi güne kayar. "Şu kadar sonra" dakika ya da <c>s:dd</c> alır,
/// sıfırdan büyük olmalı. Okunamayan giriş <c>null</c>.
/// </summary>
public static class QueueSchedule
{
    public static DateTimeOffset? Resolve(QueueStartMode mode, string? text, DateTimeOffset nowLocal)
    {
        var metin = (text ?? "").Trim();
        if (metin.Length == 0) return null;

        if (mode == QueueStartMode.After)
        {
            TimeSpan sure;
            if (SaatDakika(metin, 9999, out var s, out var d)) sure = new TimeSpan(s, d, 0);
            else if (int.TryParse(metin, NumberStyles.None, CultureInfo.InvariantCulture, out var dakika)) sure = TimeSpan.FromMinutes(dakika);
            else return null;
            return sure > TimeSpan.Zero ? nowLocal + sure : null;
        }

        if (!SaatDakika(metin, 23, out var saat, out var dk)) return null;
        var hedef = new DateTimeOffset(nowLocal.Year, nowLocal.Month, nowLocal.Day, saat, dk, 0, nowLocal.Offset);
        return hedef <= nowLocal ? hedef.AddDays(1) : hedef;
    }

    private static bool SaatDakika(string metin, int enCokSaat, out int saat, out int dakika)
    {
        saat = dakika = 0;
        var parca = metin.Split(':', '.');
        return parca.Length == 2
               && parca[1].Length == 2
               && int.TryParse(parca[0], NumberStyles.None, CultureInfo.InvariantCulture, out saat)
               && int.TryParse(parca[1], NumberStyles.None, CultureInfo.InvariantCulture, out dakika)
               && saat <= enCokSaat
               && dakika < 60;
    }
}
