using System;
using System.Collections.Generic;
using VidShrink.App.Localization;
using VidShrink.Core;
using CoreShare = VidShrink.Core.Share;

namespace VidShrink.App.Share;

/// <summary>
/// Paylaşım tanısının cümlesi burada kurulur. Core anahtar ve ham argüman döndürür;
/// cümleyi arayüz yazar, çünkü dil katmanı burada.
/// </summary>
/// <remarks>
/// Eskiden cümle Core'da kuruluyordu: sabit Türkçe metin, boyutlar makinenin kültürüyle
/// yazılmış. İngilizce arayüzde kullanıcı Türkçe cümle okuyordu. Biçim de buraya indi —
/// bayt ve süre arayüzün diliyle yazılır, <c>CultureInfo.CurrentCulture</c> ile değil.
/// </remarks>
internal static class ShareMessage
{
    internal static string Of(CoreShare.ShareResult result) => Of(result.Key, result.Args);

    internal static string Of(string key, IReadOnlyList<object> args)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        var yazilan = new object?[args.Count];
        for (var i = 0; i < args.Count; i++) yazilan[i] = Yaz(args[i]);

        return Strings.Get(key, yazilan);
    }

    /// <summary>
    /// Ham değeri arayüzün diliyle yazar. Tavanı <c>0</c> olan hedef sınırsızdır; bu
    /// anlam Core'da kalmıştı, yazımı burada.
    /// </summary>
    private static object Yaz(object arg) => arg switch
    {
        long bayt => bayt <= 0 ? Strings.Get("share.size.unlimited") : Bicim.Boyut.Bayt(bayt, Strings.Culture),
        TimeSpan sure => Bekleme(sure),
        CoreShare.ShareStep adim => Strings.Get(AdimAnahtari(adim)),
        _ => arg
    };

    private static string Bekleme(TimeSpan sure) =>
        sure.TotalMinutes < 1
            ? Strings.Get("share.wait.seconds", Math.Max(1, (int)Math.Ceiling(sure.TotalSeconds)))
            : Strings.Get("share.wait.minutes", (int)Math.Ceiling(sure.TotalMinutes));

    private static string AdimAnahtari(CoreShare.ShareStep adim) => adim switch
    {
        CoreShare.ShareStep.Prepare => "share.step.prepare",
        CoreShare.ShareStep.Init => "share.step.init",
        CoreShare.ShareStep.Upload => "share.step.upload",
        CoreShare.ShareStep.Confirm => "share.step.confirm",
        CoreShare.ShareStep.Probe => "share.step.probe",
        CoreShare.ShareStep.Delete => "share.step.delete",
        _ => throw new ArgumentOutOfRangeException(nameof(adim), adim, "Adımın dil anahtarı yok.")
    };
}
