using System;
using System.Collections.Generic;
using System.Linq;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

/// <summary>
/// Isaretci adlari. Ad isaretcinin anina bagli durur; isaretci kalkinca adi da gider. Proje
/// dosyasina <see cref="MarkerList"/> yazilir, acilista <see cref="SetMarkers"/> geri koyar.
/// </summary>
internal sealed partial class EditorTimeline
{
    private readonly Dictionary<long, string> _markerNames = new();

    internal IReadOnlyList<EditMarker> MarkerList =>
        _markers.Select(m => new EditMarker(m, _markerNames.TryGetValue(m, out var name) ? name : null)).ToArray();

    internal string? MarkerName(long time) => _markerNames.TryGetValue(time, out var name) ? name : null;

    internal void SetMarkers(IReadOnlyList<EditMarker> markers)
    {
        _markers.Clear();
        _markerNames.Clear();
        var duration = _model?.Duration ?? 0;
        foreach (var marker in markers.Where(m => m.Time <= duration).OrderBy(m => m.Time))
        {
            var index = _markers.BinarySearch(marker.Time);
            if (index >= 0) continue;
            _markers.Insert(~index, marker.Time);
            if (marker.Name is { } name) _markerNames[marker.Time] = name;
        }

        _overlay.InvalidateVisual();
    }

    /// <summary>Anin isaretcisine ad verir; isaretci yoksa ekler. Bos ad adi siler, isaretci yoksa bir sey eklemez.</summary>
    internal bool NameMarker(long time, string? name)
    {
        if (_model is not { } model) return false;
        var at = Math.Clamp(time, 0, model.Duration);
        var label = EditClip.NormalizeLabel(name);
        var index = _markers.BinarySearch(at);
        if (index < 0 && label is null) return false;
        if (index < 0) _markers.Insert(~index, at);
        else if (MarkerName(at) == label) return false;

        if (label is null) _markerNames.Remove(at);
        else _markerNames[at] = label;
        _overlay.InvalidateVisual();
        return true;
    }

    private void PruneMarkerNames()
    {
        foreach (var time in _markerNames.Keys.Where(t => _markers.BinarySearch(t) < 0).ToArray())
            _markerNames.Remove(time);
    }
}
