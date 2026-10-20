using System.Linq;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    /// <summary>later.md #42: ekrana çizim katmanı. Kısayolla yalnız kayıt sürerken açılır, kayıt bitince kapanır.</summary>
    internal ISketchLayer SketchLayer { get; set; } = new RecorderSketchHost();

    internal void ToggleSketch()
    {
        if (!HasSession) return;
        var screens = Avalonia.Controls.TopLevel.GetTopLevel(this)?.Screens?.All
            .Select(s => new SketchScreen(s.Bounds, s.Scaling)).ToList() ?? [];
        SketchLayer.Toggle(SketchArea.Of(_frameRegion, screens, ScreenGrab.Cursor()));
    }

    private void SyncSketch(bool recording)
    {
        if (!recording && SketchLayer.Visible) SketchLayer.Close();
    }
}
