using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.App.Playback;

namespace VidShrink.App;

public partial class MainWindow
{
    private PencereKipi? _kip;

    internal PencereKipi Kip => _kip ??= new PencereKipi(() => this);

    /// <summary>
    /// Orta tuş her sekmede, üst panelde çift tık ve oynatıcının orta tuşu aynı döngüye iner
    /// (<see cref="PencereKipi"/>); bulunulan sekme değişmez. Kendi orta tuş işi olan denetim
    /// (kısayol yakalama, düzenleyiciye atanmış tuş, karşılaştırma sahnesi, oynatıcı) olayı
    /// işlenmiş bırakır ve buraya gelmez. Esc tam ekrandan ya da küçük kipten çıkarır.
    /// </summary>
    private void TrackWindowMode()
    {
        Player.Kip = Kip;
        Kip.Degisti += ApplyWindowFrame;
        AddHandler(PointerPressedEvent, OnOrtaTus, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnPencereKipiTusu, RoutingStrategies.Bubble);
    }

    internal void PencereDongusu()
        => Kip.KucukVeyaTamEkran(Tabs.SelectedIndex == PlayerTabIndex ? Player.VideoAspect : 0);

    private void OnOrtaTus(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled) return;
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.MiddleButtonPressed) return;
        PencereDongusu();
        e.Handled = true;
    }

    private void OnPencereKipiTusu(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None) return;
        if (Kip.Birak()) e.Handled = true;
    }
}
