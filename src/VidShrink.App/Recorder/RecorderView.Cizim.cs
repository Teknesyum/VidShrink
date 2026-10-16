namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    /// <summary>later.md #42: ekrana çizim katmanı. Kısayolla yalnız kayıt sürerken açılır, kayıt bitince kapanır.</summary>
    internal ISketchLayer SketchLayer { get; set; } = new RecorderSketchHost();

    internal void ToggleSketch()
    {
        if (!HasSession) return;
        SketchLayer.Toggle(_frameRegion ?? CaptionFallback());
    }

    private void SyncSketch(bool recording)
    {
        if (!recording && SketchLayer.Visible) SketchLayer.Close();
    }
}
