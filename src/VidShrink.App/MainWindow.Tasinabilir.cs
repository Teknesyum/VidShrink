using System;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// AYR-P8: taşınabilir kopyada (<see cref="PortableMode"/>) sessiz güncelleme kutusu ve kabuk
/// menüsü kartı gizlenir, varsayılan uygulama önerisi ve kısayol tazelemesi koşmaz; güncelleme
/// kartının düğmesi indirmez, indirme sayfasını açar.
/// </summary>
public partial class MainWindow
{
    /// <summary>İşaretin arandığı klasör; ölçü geçici bir klasör verir.</summary>
    internal string UygulamaKlasoru { get; set; } = AppContext.BaseDirectory;

    internal bool Tasinabilir => PortableMode.IsActive(UygulamaKlasoru);

    /// <summary>Ölçünün dış bağlantı açıcısı; boşken tarayıcı açılır.</summary>
    internal Action<string?>? DisAcici { get; set; }

    internal void TasinabilirKipiniYansit()
    {
        if (Tasinabilir) ShellMenuPanel.IsVisible = false;
        AutoUpdateRow.IsVisible = UpdateCheck.CanSelfUpdate && !Tasinabilir;
        RefreshUpdateTexts();
    }

    private void IndirmeSayfasiniAc() => (DisAcici ?? OpenExternal)(UpdateCheck.ReleasesPageUrl);
}
