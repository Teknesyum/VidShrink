using Avalonia.Controls.Primitives;
using VidShrink.Core;

namespace VidShrink.App;

public partial class MainWindow
{
    private string? _elleKirpmaYolu;

    private bool ElleKirpmaAcik => ChkFltManualCrop.IsChecked == true;

    private void InitializeManualCropUi()
    {
        Watch(ChkFltManualCrop, ToggleButton.IsCheckedProperty, OnManualCropChanged);
        Preview.Kirpma.Degisti += OnOptionChanged;
    }

    /// <summary>
    /// SIK-P8: elle kırpma açılınca otomatik siyah bant kırpma kapanır ve kilitlenir; iki kırpma
    /// aynı süzgeç yolunu yazdığı için ikisi birlikte seçilemez. Katman önizlemede görünür.
    /// </summary>
    private void OnManualCropChanged()
    {
        var acik = ElleKirpmaAcik;
        if (acik) ChkFltAutoCrop.IsChecked = false;
        ChkFltAutoCrop.IsEnabled = !acik;
        Preview.ElleKirpmaAc(acik);
        OnOptionChanged();
    }

    /// <summary>
    /// Önizlemede çizilen dikdörtgenin kaynak pikseli. Yeni kaynakta dikdörtgen tam kareye döner;
    /// kutu kapalıysa ya da seçim tam kareyse <c>null</c>.
    /// </summary>
    private CropRect? ElleKirpmaSonucu()
    {
        if (!ElleKirpmaAcik || _info is not { } info) return null;
        if (!PathEquality.Same(_elleKirpmaYolu, info.FilePath))
        {
            _elleKirpmaYolu = info.FilePath;
            Preview.Kirpma.Ayarla(ManualCrop.Full);
        }
        return ManualCrop.ToSource(Preview.Kirpma.Oran, info.Width, info.Height);
    }

    /// <summary>Belirtimdeki <c>crop=</c> önce gelir, sonra elle çizilen, en son otomatik bulunan.</summary>
    private VideoFilterOptions KirpmalariUygula(VideoFilterOptions filters)
        => OtomatikKirpma.Uygula(OtomatikKirpma.Uygula(filters, ElleKirpmaAcik, ElleKirpmaSonucu()),
            OtomatikKirpmaAcik && !ElleKirpmaAcik, KirpmaSonucu());
}
