using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core.Editing;
using VidShrink.Player;

namespace VidShrink.App.Editing;

/// <summary>
/// Teslime giden ses izi. Liste oynaticinin kendi izlerinden (dis olmayan ses izleri, oynaticidaki sirayla) kurulur;
/// sira <see cref="EditAudioTrack"/> indeksidir ve <c>-map 0:a:N</c>'e iner. Varsayilan "Tum izler"dir (butun ses ve
/// altyazi izleri, monitorde ilk iz calar). Monitor dugmesi tek izler arasinda doner, teslim kutusu ayrica "Tum izler"i
/// ve "ses yok"u secer. Secim projeye yazilir ve onizleme yeniden yuklenince motora yeniden verilir.
/// </summary>
internal partial class EditorView
{
    private IReadOnlyList<PlaybackTrack> _audioTracks = Array.Empty<PlaybackTrack>();
    private int _audioTrack = EditAudioTrack.Default;
    private bool _audioTrackSync;

    internal int SelectedAudioTrack => _audioTrack;

    internal IReadOnlyList<PlaybackTrack> AudioTracks => _audioTracks;

    private void InitAudioTrack()
    {
        BtnAudioTrack.Click += (_, _) => CycleAudioTrack();
        CmbExportAudioTrack.SelectionChanged += (_, _) =>
        {
            if (_audioTrackSync || CmbExportAudioTrack.SelectedIndex < 0) return;
            var index = CmbExportAudioTrack.SelectedIndex;
            SetAudioTrack(index == 0 ? EditAudioTrack.All : index > _audioTracks.Count ? EditAudioTrack.None : index - 1);
        };
        SyncAudioTrackControls();
    }

    /// <summary>
    /// Monitor dugmesi: sonraki ize gecer, sondan ilke doner. "Ses yok"tan ilk ize, "Tum izler"den (monitorde ilk iz
    /// calar) ikinci ize gelir; tek izli kaynakta "Tum izler" degismez.
    /// </summary>
    internal void CycleAudioTrack()
    {
        if (_audioTracks.Count == 0) return;
        var next = _audioTrack == EditAudioTrack.None ? 0 : (Math.Max(_audioTrack, 0) + 1) % _audioTracks.Count;
        if (_audioTrack == EditAudioTrack.All && next == 0) return;
        SetAudioTrack(next);
    }

    private void SetAudioTrack(int track)
    {
        if (track == _audioTrack) return;
        _audioTrack = track;
        SyncAudioTrackControls();
        if (_audioTracks.Count > 0) Preview.SelectAudio(EngineAudioId());
        _pendingPlan = null;
        MarkEdited();
    }

    private void UseAudioTrack(int track)
    {
        _audioTrack = track is EditAudioTrack.None or EditAudioTrack.All || (track >= 0 && (_audioTracks.Count == 0 || track < _audioTracks.Count))
            ? track
            : EditAudioTrack.Default;
        SyncAudioTrackControls();
    }

    private void ForgetAudioTracks()
    {
        _audioTracks = Array.Empty<PlaybackTrack>();
        _audioTrack = EditAudioTrack.Default;
        SyncAudioTrackControls();
    }

    private void CaptureAudioTracks(IPlaybackEngine engine)
    {
        _audioTracks = engine.Tracks.Where(track => track.Kind == PlaybackTrackKind.Audio && !track.External).ToArray();
        _audioTrack = EditAudioTrack.Default;
        SyncAudioTrackControls();
    }

    private void ApplyAudioTrack(IPlaybackEngine engine)
    {
        if (_audioTracks.Count > 0) engine.SetAudioTrack(EngineAudioId());
    }

    private long EngineAudioId()
    {
        var index = _audioTrack == EditAudioTrack.All ? 0 : _audioTrack;
        return index >= 0 && index < _audioTracks.Count ? _audioTracks[index].Id : 0;
    }

    private void SyncAudioTrackControls()
    {
        _audioTrackSync = true;
        try
        {
            var items = _audioTracks.Select(track => (object)PlayerView.TrackName(track)).ToList();
            if (_audioTracks.Count > 0)
            {
                items.Insert(0, Strings.Get("editor.export.audio-track.all"));
                items.Add(Strings.Get("editor.export.audio-track.none"));
            }

            CmbExportAudioTrack.ItemsSource = items;
            CmbExportAudioTrack.IsEnabled = _audioTracks.Count > 0;
            CmbExportAudioTrack.SelectedIndex = _audioTracks.Count == 0 ? -1
                : _audioTrack == EditAudioTrack.All ? 0
                : _audioTrack < 0 ? _audioTracks.Count + 1 : Math.Min(_audioTrack, _audioTracks.Count - 1) + 1;
        }
        finally
        {
            _audioTrackSync = false;
        }

        BtnAudioTrack.IsVisible = _audioTracks.Count > 1;
        TxtAudioTrack.Text = _audioTrack == EditAudioTrack.None
            ? string.Empty
            : (Math.Max(_audioTrack, 0) + 1).ToString(CultureInfo.CurrentCulture) + "/" + _audioTracks.Count.ToString(CultureInfo.CurrentCulture);
        if (this.TryFindResource(_audioTrack == EditAudioTrack.None ? "IconVolumeMute" : "IconVolume", out var glyph) && glyph is Geometry geometry)
            GlyphAudioTrack.Data = geometry;
    }
}
