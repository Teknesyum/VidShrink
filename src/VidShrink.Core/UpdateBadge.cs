using System;
using System.Globalization;

namespace VidShrink.Core;

/// <summary>
/// Üst çubuktaki güncelleme rozetinin durumu. Özel rafın güncelleme paneli ölçütü
/// rozetin her an konuşmasını istiyor: denetim sessizce vazgeçemez, "güncel" de
/// "çevrimdışı" da görünür.
/// </summary>
public enum UpdateBadgeState
{
    /// <summary>Denetim koşuyor.</summary>
    Checking,

    /// <summary>Sürüm en yenisi.</summary>
    UpToDate,

    /// <summary>Daha yeni sürüm var.</summary>
    NewVersion,

    /// <summary>Ağ yok, oran sınırı ya da bozuk manifest.</summary>
    Offline,

    /// <summary>Yükleme başlatıcıya devredildi, süreç kapanıyor.</summary>
    Installing,

    /// <summary>Yeni sürüm arka planda sahneye iniyor; kurulum henüz başlamadı.</summary>
    Downloading,

    /// <summary>Sahne indi ve doğrulandı; kullanıcı "Yükle"ye basınca kurulur.</summary>
    Ready
}

/// <summary>
/// Rozetin metnini ve saatini üreten saf kural. Arayüzden ayrı durur ki ölçülebilsin:
/// durum ve an verilir, gösterilecek metin ile saat damgası geri gelir.
/// </summary>
public static class UpdateBadge
{
    /// <summary>Saat kalıbı: özel rafın rozet ölçütü 24 saatlik <c>HH:MM</c> istiyor.</summary>
    private const string TimePattern = "HH':'mm";

    /// <summary>Saat damgası taşıyan durumlar. Denetim sürerken ve yüklemede saat yazılmaz.</summary>
    public static bool CarriesTime(UpdateBadgeState state) =>
        state is UpdateBadgeState.UpToDate or UpdateBadgeState.Offline;

    /// <summary>
    /// Rozet metni: gövde cümlesi, saat taşıyan durumlarda sonuna <c> · HH:MM</c> eklenir.
    /// Saat yerelin kendi biçiminde değil, panelin ölçütündeki 24 saatlik kalıpta yazılır.
    /// </summary>
    public static string Compose(UpdateBadgeState state, string body, DateTimeOffset now) =>
        CarriesTime(state) ? $"{body} · {now.ToString(TimePattern, CultureInfo.InvariantCulture)}" : body;
}
