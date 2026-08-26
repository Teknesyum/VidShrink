namespace VidShrink.Tests;

/// <summary>
/// Canlı ölçümlerin yazdığı yer. Masaüstü değil: ölçüm çıktıları gigabaytlarca sürüyor
/// ve masaüstüne dağıldıklarında kimin bıraktığı belli olmuyor, silmek de kullanıcıya
/// kalıyor. Hepsi projenin kendi çalışma klasörüne iniyor; orası .gitignore içinde,
/// git'e sızmıyor ve tek komutla temizleniyor.
///
/// VIDSHRINK_LIVE_OUT hâlâ üstüne yazar; başka bir diske ölçüm alan biri onu verir.
/// </summary>
internal static class TestPaths
{
    /// <summary>Bütün ölçüm çıktılarının ortak kökü.</summary>
    internal static string OutputRoot => Path.Combine(TipSources.Root, ".calisma", "test-ciktilari");

    /// <summary>
    /// <paramref name="name"/> ölçümü adlandırır ve klasörü ayırır: canlı bant ölçümü ile
    /// aşırı sıkıştırma ölçümü birbirinin dosyasını ezmez.
    /// </summary>
    internal static string LiveOut(string name)
        => Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_OUT")
           ?? Path.Combine(OutputRoot, name);
}
