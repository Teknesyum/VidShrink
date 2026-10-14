using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// Ayar yedeği: bozuk settings.json'dan dönüşün uyarısı ve Ayarlar'daki dışa/içe aktarma.
/// </summary>
public partial class MainWindow
{
    internal const string SettingsRecoveryRestoredKey = "settings.recovery.restored";
    internal const string SettingsRecoveryDefaultedKey = "settings.recovery.defaulted";

    private string SettingsFile => SettingsPathOverride ?? UpdateSettings.DefaultPath;

    /// <summary>
    /// Ayar dosyası okunmadan önce yoklanır; uygulamanın açılış okuması dosyayı zaten
    /// kurtardıysa onun bekleyen sonucu alınır.
    /// </summary>
    private SettingsRecovery CheckSettingsRecovery()
    {
        var recovery = SettingsBackup.Check(SettingsFile);
        var pending = SettingsBackup.TakePending(SettingsFile);
        return recovery == SettingsRecovery.None ? pending : recovery;
    }

    /// <summary>Ayar dosyasının yedekten ya da varsayılandan döndüğünü uyarı şeridinde söyler.</summary>
    private void ShowSettingsRecovery(SettingsRecovery recovery)
    {
        if (recovery == SettingsRecovery.None) return;
        TxtSettingsRecovery.Text = Say(recovery == SettingsRecovery.Restored ? SettingsRecoveryRestoredKey : SettingsRecoveryDefaultedKey);
        SettingsRecoveryNotice.IsVisible = true;
    }

    private void OnDismissSettingsRecovery(object? sender, RoutedEventArgs e)
        => SettingsRecoveryNotice.IsVisible = false;

    private async void OnExportSettings(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = "vidshrink-ayarlar.zip",
                DefaultExtension = "zip",
                FileTypeChoices = new[] { new FilePickerFileType(Say("settings.backup.file-type")) { Patterns = new[] { "*.zip" } } }
            });
            if (file?.TryGetLocalPath() is { } path) ExportSettings(path);
        }
        catch (Exception ex)
        {
            ShowSettingsBackup($"{Say("settings.error.export")}: {ex.Message}", error: true);
        }
    }

    private async void OnImportSettings(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType(Say("settings.backup.file-type")) { Patterns = new[] { "*.zip" } } }
            });
            if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) ImportSettings(path);
        }
        catch (Exception ex)
        {
            ShowSettingsBackup($"{Say("settings.error.import")}: {ex.Message}", error: true);
        }
    }

    /// <summary>Ayar paketini yazar; hata durum satırına düşer.</summary>
    internal bool ExportSettings(string zipPath)
    {
        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(SettingsFile)) ?? "";
            var names = AppDataArchive.Export(zipPath, folder, SettingsFile);
            ShowSettingsBackup(string.Format(Strings.Culture, Say("settings.backup.exported"), names.Count), error: false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowSettingsBackup($"{Say("settings.error.export")}: {ex.Message}", error: true);
            return false;
        }
    }

    /// <summary>Paketi yerine yazar ve pencereyi sıfırlamadaki gibi yeni ayarlarla yeniler.</summary>
    internal bool ImportSettings(string zipPath)
    {
        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(SettingsFile)) ?? "";
            var names = AppDataArchive.Import(zipPath, folder, SettingsFile);
            Playback.ShortcutBindings.Load();
            var settings = UpdateSettings.Load(SettingsPathOverride);
            _settingsSyncing = true;
            UseLanguage(ResolveLanguage(settings.Language, CultureInfo.CurrentUICulture.Name));
            RestoreSettings(settings);
            RestoreAppSettings(AppSettings.Load(SettingsPathOverride));
            ShowSettingsBackup(string.Format(Strings.Culture, Say("settings.backup.imported"), names.Count), error: false);
            return true;
        }
        catch (InvalidDataException)
        {
            ShowSettingsBackup(Say("settings.backup.invalid"), error: true);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowSettingsBackup($"{Say("settings.error.import")}: {ex.Message}", error: true);
            return false;
        }
    }

    private void ShowSettingsBackup(string text, bool error)
    {
        if (Application.Current?.TryFindResource(error ? "StatusError" : "Body", out var found) == true && found is ControlTheme theme)
            TxtSettingsBackup.Theme = theme;
        TxtSettingsBackup.Text = text;
    }
}
