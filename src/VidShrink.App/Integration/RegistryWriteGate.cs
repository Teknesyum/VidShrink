namespace VidShrink.App.Integration;

/// <summary>
/// Gerçek <c>HKEY_CURRENT_USER\Software\Classes</c> kaydına yalnız kurulu uygulamanın süreci
/// yazar. Kayıtlar o an koşan exe'nin yolunu taşıyor; test konağı ya da ölçüm aracı yazarsa
/// kullanıcının sağ tık menüsü ve "Birlikte aç" listesi o exe'yi gösteriyor.
/// </summary>
internal static class RegistryWriteGate
{
    internal const string AppExecutable = "VidShrink.App.exe";

    internal static bool Allows(string? processPath)
        => !string.IsNullOrWhiteSpace(processPath)
            && string.Equals(Path.GetFileName(processPath), AppExecutable, StringComparison.OrdinalIgnoreCase);
}
