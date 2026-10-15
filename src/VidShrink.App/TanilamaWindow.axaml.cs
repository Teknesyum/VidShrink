using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// AYR-10, AYR-07, AYR-27: tanılama penceresi. Günlük uygulama içinde okunur ve kopyalanır;
/// "Sorun bildir" hazır GitHub kaydını açar; paket düğmesi günlüğü ve sistem özetini tek
/// zip'e yazar. Hiçbir şey kendiliğinden gönderilmez: paketi kayda kullanıcı ekler.
/// Durum satırında tam yol yok, yalnız dosya adı.
/// </summary>
internal sealed partial class TanilamaWindow : Window
{
    private readonly string _gunlukKlasoru;
    private readonly string _hataAdresi;
    private readonly Action<string?> _disAcici;

    public TanilamaWindow() : this(Gunluk.Klasor, string.Empty, string.Empty, _ => { })
    {
    }

    internal TanilamaWindow(string gunlukKlasoru, string sistemBilgisi, string hataAdresi, Action<string?> disAcici)
    {
        _gunlukKlasoru = gunlukKlasoru;
        _hataAdresi = hataAdresi;
        _disAcici = disAcici;
        InitializeComponent();
        TxtSystem.Text = sistemBilgisi.TrimEnd();
        var gunluk = Gunluk.Oku(gunlukKlasoru);
        TxtLog.Text = gunluk.Length == 0 ? Say("settings.log.empty") : gunluk.TrimEnd();
        BtnCopy.Click += (_, _) => _ = CopyAsync();
        BtnPackage.Click += (_, _) => _ = PaketKaydetAsync();
        BtnReport.Click += (_, _) => _disAcici(_hataAdresi);
        BtnReveal.Click += (_, _) => GunluguGoster();
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Close();
            e.Handled = true;
        };
    }

    /// <summary>Ölçünün dosya seçici yerine koyduğu iş; önerilen adı alır, seçilen yolu döner.</summary>
    internal Func<string, Task<string?>>? PaketHedefiSec { get; set; }

    /// <summary>Panoya giden metin: sistem özeti, boş satır, günlük.</summary>
    internal string KopyaMetni() =>
        (TxtSystem.Text ?? string.Empty) + Environment.NewLine + Environment.NewLine + Gunluk.Oku(_gunlukKlasoru);

    internal async Task<bool> CopyAsync()
    {
        try
        {
            if (Clipboard is not { } pano) return false;
            await pano.SetTextAsync(KopyaMetni()).ConfigureAwait(true);
            TxtStatus.Text = Say("settings.diag.copied");
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            TxtStatus.Text = Say("settings.diag.failed", ex.Message);
            return false;
        }
    }

    internal async Task<bool> PaketKaydetAsync()
    {
        try
        {
            var hedef = await (PaketHedefiSec ?? SecAsync)(Destek.PaketAdi(DateTimeOffset.Now)).ConfigureAwait(true);
            if (hedef is null) return false;
            Destek.PaketYaz(hedef, _gunlukKlasoru, TxtSystem.Text ?? string.Empty);
            TxtStatus.Text = Say("settings.diag.saved", Path.GetFileName(hedef));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            TxtStatus.Text = Say("settings.diag.failed", Gunluk.YoluIndirge(ex.Message));
            return false;
        }
    }

    private void GunluguGoster()
    {
        var dosya = Path.Combine(_gunlukKlasoru, Gunluk.DosyaAdi);
        try
        {
            if (!File.Exists(dosya))
            {
                TxtStatus.Text = Say("settings.log.empty");
                return;
            }
            Platform.Reveal(dosya);
            TxtStatus.Text = $"{Say("settings.log.opened")}: {Gunluk.DosyaAdi}";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = Say("settings.diag.failed", Gunluk.YoluIndirge(ex.Message));
        }
    }

    private static string Say(string key) => LanguageCatalog.Display(Strings.Get(key));

    private static string Say(string key, params object?[] args) => LanguageCatalog.Display(Strings.Get(key, args));

    private async Task<string?> SecAsync(string onerilen)
    {
        var dosya = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = onerilen,
            DefaultExtension = "zip",
            FileTypeChoices = new[] { new FilePickerFileType(Say("settings.diag.file-type")) { Patterns = new[] { "*.zip" } } }
        }).ConfigureAwait(true);
        return dosya?.TryGetLocalPath();
    }
}
