using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// DON-P4: Dönüştür'ün ses izleri. Kaynağın her ses izi bir kutudur; açılışta yalnız ffmpeg'in
/// kendiliğinden seçeceği iz (<see cref="ConversionArguments.AutoAudio"/>) işaretlidir ve plan seçimsiz
/// kalır, yani komut bugünküyle aynıdır. Kutular o videoya aittir: yeni kaynak açılınca sıfırlanır.
/// </summary>
public partial class MainWindow
{
    private readonly List<CheckBox> _convertAudioChecks = new();
    private int? _convertAudioDefault;

    internal IReadOnlyList<CheckBox> ConvertAudioChecks => _convertAudioChecks;

    /// <summary>İşaretler varsayılanla aynıysa <c>null</c> (ffmpeg'in seçimi), değilse işaretli izlerin sırası.</summary>
    internal IReadOnlyList<int>? ConvertAudioChoice()
    {
        var chosen = Enumerable.Range(0, _convertAudioChecks.Count).Where(i => _convertAudioChecks[i].IsChecked == true).ToList();
        if (_convertAudioDefault is { } auto && chosen.Count == 1 && chosen[0] == auto) return null;
        if (_convertAudioChecks.Count == 0) return null;
        return chosen;
    }

    private void ResetConvertAudio()
    {
        _convertAudioChecks.Clear();
        ConvertAudioTracks.Children.Clear();
        _convertAudioDefault = null;
        if (_info is { } info)
        {
            var audio = info.Streams.Where(stream => stream.Kind == StreamKind.Audio).ToList();
            var auto = ConversionArguments.AutoAudio(info);
            _convertAudioDefault = auto is null ? null : audio.IndexOf(auto);
            for (var i = 0; i < audio.Count; i++) ConvertAudioTracks.Children.Add(ConvertAudioRow(audio[i], i, i == _convertAudioDefault));
        }
        ConvertAudioTracks.IsVisible = _convertAudioChecks.Count > 0;
        ConvertAudioPanel.IsVisible = _info is not null;
    }

    private CheckBox ConvertAudioRow(SourceStream track, int ordinal, bool chosen)
    {
        var name = track.Title ?? track.Language;
        var label = (name is null ? $"#{ordinal + 1}" : $"#{ordinal + 1} {name}") + " · " + track.Codec;
        var check = new CheckBox { Content = label, IsChecked = chosen, VerticalAlignment = VerticalAlignment.Center };
        if (this.FindResource("CheckStyle") is Avalonia.Styling.ControlTheme checkStyle) check.Theme = checkStyle;
        AutomationProperties.SetName(check, label);
        check.IsCheckedChanged += (_, _) => RefreshConversion();
        _convertAudioChecks.Add(check);
        return check;
    }

    /// <summary>Kap ses taşımıyorsa kutuları kapatır; ipucu taşımayan kabı ya da sessiz kaynağı söyler.</summary>
    private void RefreshConvertAudioState(ConversionPlan plan)
    {
        var carried = plan.CarriesAudio && plan.AudioCodec is not null;
        foreach (var check in _convertAudioChecks) check.IsEnabled = carried;
        var hint = !carried ? Say("main.convert.audio-tracks.not-carried")
            : _convertAudioChecks.Count == 0 ? Say("main.convert.audio-tracks.none")
            : "";
        TxtConvertAudioHint.Text = hint;
        TxtConvertAudioHint.IsVisible = hint.Length > 0;
    }
}
