using System;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal sealed partial class EditorTimeline
{
    private const string MusicTrackName = "A2";

    private bool _musicPress;
    private bool _musicMoved;
    private long _musicStart;

    internal event Action<long>? MusicMoved;

    internal double MusicTop => TracksBottom + Metric("EditorTrackGap");

    internal double MusicHeight => AudioHeight;

    internal double MusicBottom => MusicTop + MusicHeight;

    internal bool MusicDragging => _musicPress;

    internal long? MusicGhost => _musicPress && _musicMoved ? _musicStart : null;

    internal bool OnMusicRow(Point position) => position.Y >= MusicTop && position.Y < MusicBottom;

    internal bool MusicAt(double x)
    {
        if (_model?.Music is not { } music || _ppt <= 0) return false;
        var left = TimeToX(music.Start);
        return x >= left && x < left + MusicSpan(music) * _ppt;
    }

    private long MusicSpan(EditMusic music) => Math.Max(0, Math.Min(music.Duration, (_model?.Duration ?? 0) - music.Start));

    private bool PressMusic(Point position)
    {
        if (!OnMusicRow(position) || _model?.Music is not { } music) return false;
        if (!MusicAt(position.X)) return true;
        _press = position;
        _musicPress = true;
        _musicMoved = false;
        _musicStart = music.Start;
        return true;
    }

    internal void DragMusic(double x)
    {
        if (!_musicPress || _model is not { Music: { } music } model || _ppt <= 0) return;
        if (!_musicMoved)
        {
            if (Math.Abs(x - _press.X) < Metric("EditorDragThresholdPx")) return;
            _musicMoved = true;
        }

        var delta = (long)Math.Round((x - _press.X) / _ppt);
        var latest = Math.Max(0, model.Duration - 1);
        _musicStart = Math.Clamp(Snapped(music.Start + delta, out var snapped), 0, latest);
        SnapLine = snapped;
        _canvas.InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    private void CommitMusic()
    {
        var start = _musicStart;
        var moved = _musicMoved;
        var music = _model?.Music;
        EndMusic();
        if (!moved || music is null || start == music.Start) return;
        MusicMoved?.Invoke(start);
    }

    private void EndMusic()
    {
        if (!_musicPress) return;
        _musicPress = false;
        _musicMoved = false;
        SnapLine = null;
        _canvas.InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    private void DrawMusic(DrawingContext context)
    {
        if (_model?.Music is not { } music) return;
        var top = MusicTop;
        var height = MusicHeight;
        var start = _musicPress && _musicMoved ? _musicStart : music.Start;
        var x = TimeToX(start);
        var w = Math.Max(0, Math.Min(music.Duration, _model.Duration - start) * _ppt);
        var box = new Rect(x, top, w, height);
        var pen = new Pen(Paint(_musicPress ? "NeonPurple" : "NeonPurpleBorder"), Metric(_musicPress ? "EditorClipSelectedBorder" : "EditorClipBorder"));
        context.DrawRectangle(Paint("NeonBlueFill"), pen, new RoundedRect(box, Metric("EditorClipRadius")));
        var pad = Metric("EditorClipPadding");
        if (w <= pad * 2) return;
        var name = System.IO.Path.GetFileName(music.Path) + "  " + MusicLevelText(music.LevelDb);
        var label = new FormattedText(name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(EditorTokens.Font(this, "FontMono")), Metric("FontSizeSm"), Paint("TextBody"));
        var fit = HeaderLabelScale(label.Height, height);
        using (context.PushClip(box.Deflate(pad)))
        using (context.PushTransform(Matrix.CreateScale(fit, fit) * Matrix.CreateTranslation(x + pad, top + (height - label.Height * fit) / 2)))
            context.DrawText(label, default);
    }

    internal static string MusicLevelText(double levelDb)
        => (levelDb > 0 ? "+" : string.Empty) + levelDb.ToString("0.#", VidShrink.App.Localization.Strings.Culture) + " dB";

    private void UpdateMusicCursor(Point position)
        => SetCursor(MusicAt(position.X) ? StandardCursorType.SizeWestEast : StandardCursorType.Arrow);
}
