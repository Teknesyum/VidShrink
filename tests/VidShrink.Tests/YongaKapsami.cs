using System;

namespace VidShrink.Tests;

/// <summary>
/// Yonga şeridinde iki tür düğme var: hedef taşıyan <b>kalite</b> yongaları ve hedefi
/// olmayan <b>eylem</b> düğmeleri (kullanıcı ön ayarı ekleyen "+"). İkisi de
/// <c>ChipButton</c> temalı, bu yüzden ölçüler eylem düğmesini adıyla değil
/// biçimlemedeki <c>Classes="eylem"</c> niteliğiyle ayırır: yarın eklenecek yeni bir
/// kalite yongası pimlere yine takılır, yalnız eylem düğmeleri dışarıda kalır.
/// </summary>
internal static class YongaKapsami
{
    /// <summary>
    /// Adı verilen düğmenin kendi gövdesinde balon var mı. Şeritteki balon sayısını
    /// yonga sayısıyla karşılaştırmak eylem düğmesi de balon taşıdığı için yanlış
    /// hüküm veriyordu; ölçü artık her yongaya tek tek bakıyor.
    /// </summary>
    internal static bool Balonlu(string markup, string ad)
    {
        var govde = Govde(markup, ad, "</Button>");
        return govde.Contains("<ToolTip.Tip>", StringComparison.Ordinal)
            && govde.Contains("<StackPanel", StringComparison.Ordinal);
    }

    private static string Govde(string markup, string ad, string kapanis)
    {
        var yer = markup.IndexOf("x:Name=\"" + ad + "\"", StringComparison.Ordinal);
        if (yer < 0) return string.Empty;

        var son = markup.IndexOf(kapanis, yer, StringComparison.Ordinal);
        return son < 0 ? markup[yer..] : markup[yer..son];
    }

    internal static bool Eylem(string markup, string ad)
    {
        var yer = markup.IndexOf("x:Name=\"" + ad + "\"", StringComparison.Ordinal);
        if (yer < 0) return false;

        var son = markup.IndexOf('>', yer);
        if (son < 0) son = markup.Length;

        return markup[yer..son].Contains("Classes=\"eylem\"", StringComparison.Ordinal);
    }
}
