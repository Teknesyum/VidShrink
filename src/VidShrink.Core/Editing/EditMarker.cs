namespace VidShrink.Core.Editing;

/// <summary>
/// Cizelgedeki isaretci: cizelge ani (tick) ve istege bagli adi. Ad kirpilir, bos ad <c>null</c>
/// sayilir. Proje dosyasina yazilir (<see cref="EditProject.Markers"/>).
/// </summary>
public sealed record EditMarker
{
    public EditMarker(long time, string? name)
    {
        if (time < 0)
            throw new ArgumentOutOfRangeException(nameof(time), time, "Isaretci ani negatif olamaz");
        Time = time;
        Name = EditClip.NormalizeLabel(name);
    }

    public long Time { get; }

    public string? Name { get; }
}
