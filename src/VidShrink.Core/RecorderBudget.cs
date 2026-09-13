using System;

namespace VidShrink.Core;

/// <summary>Bütçe hesabının neden uygulanmadığını söyleyen sebep.</summary>
public enum RecorderBudgetVerdict
{
    /// <summary>Hedef verilmedi; kalite kolu olduğu gibi kalıyor.</summary>
    NotRequested,

    /// <summary>Hedef okunamadı ya da sıfır/negatif.</summary>
    Invalid,

    /// <summary>Hesap taban bit hızının altına düştü; bütçe uygulanmıyor.</summary>
    TooSmall,

    /// <summary>Hesap geçerli; <see cref="RecorderBudget.VideoKbps"/> okunabilir.</summary>
    Usable
}

/// <summary>
/// Hedef dosya boyutunun bit hızına çevrilmesi. Kayıt gerçek zamanlı olduğu için iki
/// geçişli kodlama yok; tavanlı bit hızı kullanılıyor.
/// </summary>
/// <param name="Verdict">Hesabın hükmü.</param>
/// <param name="VideoKbps">Video koluna yazılacak bit hızı; hüküm
/// <see cref="RecorderBudgetVerdict.Usable"/> değilse sıfır.</param>
public readonly record struct RecorderBudget(RecorderBudgetVerdict Verdict, int VideoKbps)
{
    /// <summary>Kaydın ses koluna ayrılan bit hızı; <c>RecorderArguments.AudioBitrate</c> ile aynı sayı.</summary>
    public const int AudioKbps = 160;

    /// <summary>Altına inilince bütçenin uygulanmadığı taban.</summary>
    public const int MinimumVideoKbps = 200;

    /// <summary>
    /// Hedef boyutu ve süreyi bit hızına çevirir. Katsayı OBS'in tampon hesabının
    /// yazımıdır (<c>×1000/8/1024/1024</c>'ün tersi); yaygın <c>×8192</c> yazımıyla
    /// arasında %2,4 fark vardır ve burada bilerek OBS'inki seçilmiştir.
    /// </summary>
    /// <param name="megabytes">Hedef dosya boyutu; verilmediyse <c>null</c>.</param>
    /// <param name="seconds">Hedef süre; verilmediyse <c>null</c>.</param>
    /// <param name="audioTracks">Ses izi sayısı; sessiz kayıtta sıfır.</param>
    public static RecorderBudget From(double? megabytes, int? seconds, int audioTracks)
    {
        if (megabytes is null || seconds is null) return new RecorderBudget(RecorderBudgetVerdict.NotRequested, 0);

        if (megabytes is not { } mb || seconds is not { } sn
            || double.IsNaN(mb) || double.IsInfinity(mb) || mb <= 0 || sn <= 0 || audioTracks < 0)
            return new RecorderBudget(RecorderBudgetVerdict.Invalid, 0);

        var total = mb * 8 * 1024 * 1024 / 1000 / sn;
        var video = (int)Math.Floor(total - (double)AudioKbps * audioTracks);

        return video < MinimumVideoKbps
            ? new RecorderBudget(RecorderBudgetVerdict.TooSmall, 0)
            : new RecorderBudget(RecorderBudgetVerdict.Usable, video);
    }
}
