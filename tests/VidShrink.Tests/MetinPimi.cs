using System.Text.RegularExpressions;

namespace VidShrink.Tests;

/// <summary>
/// Belge pimleri icin bosluk normallestirmesi. Bir README cumlesini <c>Assert.Contains</c> ile
/// ararken satir sarma konumu iddianin parcasi olmamali: paragraf yeniden sarildiginda davranis
/// degismeden test kirmizi oluyordu. <see cref="Duz"/> her bosluk dizisini (satir sonu dahil) tek
/// bosluga indirger, iki ucu kirpar; iddianin kelimeleri ve sirasi aynen korunur.
/// </summary>
internal static class MetinPimi
{
    public static string Duz(string metin) => Regex.Replace(metin, @"\s+", " ").Trim();
}
