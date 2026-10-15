using Avalonia.Interactivity;
using Avalonia.Threading;

namespace VidShrink.App.Playback;

internal partial class ComparisonPanel
{
    private bool _kirpmaBagli;

    internal KirpmaKatmani Kirpma => CropLayer;

    /// <summary>
    /// SIK-P8: elle kırpma katmanını açar ya da kapatır. Katman jestin içerik dikdörtgenini
    /// okur; yakınlaştırma ya da boy değişince yeniden çizilir.
    /// </summary>
    internal void ElleKirpmaAc(bool acik)
    {
        if (!_kirpmaBagli)
        {
            _kirpmaBagli = true;
            CropLayer.Gesture = _gesture;
            Stage.AddHandler(PointerWheelChangedEvent, (_, _) => KirpmayiTazele(), RoutingStrategies.Bubble, handledEventsToo: true);
            Stage.SizeChanged += (_, _) => KirpmayiTazele();
            BtnZoomIn.Click += (_, _) => KirpmayiTazele();
            BtnZoomOut.Click += (_, _) => KirpmayiTazele();
            BtnPanelMaximize.Click += (_, _) => KirpmayiTazele();
            BtnPanelFullScreen.Click += (_, _) => KirpmayiTazele();
        }
        CropLayer.IsVisible = acik;
        KirpmayiTazele();
    }

    private void KirpmayiTazele()
    {
        if (CropLayer.IsVisible) Dispatcher.UIThread.Post(CropLayer.InvalidateVisual, DispatcherPriority.Render);
    }
}
