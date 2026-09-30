namespace VidShrink.Core.Editing;

/// <summary>
/// Onizlemenin klip ayarlari: parca basina geometri zinciri ve butun EDL icin sabit, zaman
/// pencereli solma ve ses zincirleri. Tuval disa aktarimla aynidir.
/// </summary>
public sealed class EditLook
{
    private readonly int _width;
    private readonly int _height;
    private readonly int _parNum;
    private readonly int _parDen;
    private readonly (int Width, int Height)? _canvas;

    public EditLook(EdlPreview preview, int width, int height, int parNum = 1, int parDen = 1)
    {
        ArgumentNullException.ThrowIfNull(preview);
        _width = Math.Max(2, width);
        _height = Math.Max(2, height);
        _parNum = parNum;
        _parDen = parDen;
        _canvas = ClipFilters.Canvas(preview.Parts.Select(p => p.Clip).ToList(), _width, _height, parNum, parDen);
        (TimedVideo, TimedAudio) = ClipFilters.Timed(preview);
    }

    public (int Width, int Height)? Canvas => _canvas;

    public string? TimedVideo { get; }

    public string? TimedAudio { get; }

    public string? Geometry(EdlPart part)
    {
        var chain = ClipFilters.Geometry(part.Clip.Effects, _width, _height, _parNum, _parDen, _canvas);
        return chain.Length == 0 ? null : chain;
    }
}
