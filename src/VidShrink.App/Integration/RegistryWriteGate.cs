namespace VidShrink.App.Integration;

/// <summary>
/// Gerçek <c>HKEY_CURRENT_USER\Software\Classes</c> kaydına yalnız kurulu uygulamanın süreci
/// yazar. Kayıtlar o an koşan exe'nin yolunu taşıyor; test konağı ya da ölçüm aracı yazarsa
/// kullanıcının sağ tık menüsü ve "Birlikte aç" listesi o exe'yi gösteriyor. Ad yetmez: derleme
/// çıktısındaki <c>VidShrink.App.exe</c> de aynı adı taşır. İzin yalnız kurulu düzende verilir,
/// exe bir <c>app\</c> klasöründe durur ve bir üstte <c>VidShrink.exe</c> başlatıcısı bulunur.
/// </summary>
internal static class RegistryWriteGate
{
    internal const string AppExecutable = VidShrink.Core.Setup.ShellRegistration.AppExecutableName;

    internal static bool Allows(string? processPath, Func<string, bool>? fileExists = null)
        => VidShrink.Core.Setup.ShellRegistration.IsInstalledLayout(processPath, fileExists);
}
