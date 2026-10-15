using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// DON-P4: Dönüştür'ün ses izleri. Kaynağın her ses izi bir kutudur; açılışta yalnız ffmpeg'in
/// kendiliğinden seçeceği iz (<see cref="ConversionArguments.AutoAudio"/>) işaretlidir ve plan seçimsiz
/// kalır, yani komut bugünküyle aynıdır. Kutular o videoya aittir: yeni kaynak açılınca sıfırlanır.
/// DON-P7: tek dış ses dosyası; "yerine koy" kaynağın sesini bırakır, kapalıyken iz eklenir. Dosya da
/// o videoya aittir.
/// </summary>
public partial class MainWindow
{
    private readonly List<CheckBox> _convertAudioChecks = new();
    private int? _convertAudioDefault;
    private ExternalAudio? _convertExternalAudio;

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
        _convertExternalAudio = null;
        ChkConvertAudioReplace.IsChecked = false;
        ChkConvertAudioShortest.IsChecked = false;
        RefreshConvertAudioFileList();
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
        var text = new TextBlock { Text = label, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        if (this.FindResource("Body") is Avalonia.Styling.ControlTheme body) text.Theme = body;
        var check = new CheckBox { Content = text, IsChecked = chosen, VerticalAlignment = VerticalAlignment.Center };
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
        BtnConvertAudioFile.IsEnabled = carried;
        ChkConvertAudioReplace.IsEnabled = ChkConvertAudioShortest.IsEnabled = carried && _convertExternalAudio is not null;
        var hint = !carried ? Say("main.convert.audio-tracks.not-carried")
            : _convertAudioChecks.Count == 0 ? Say("main.convert.audio-tracks.none")
            : "";
        TxtConvertAudioHint.Text = hint;
        TxtConvertAudioHint.IsVisible = hint.Length > 0;
    }

    internal ExternalAudio? ConvertExternalAudio
        => _convertExternalAudio is { } audio ? audio with { Replace = ChkConvertAudioReplace.IsChecked == true } : null;

    /// <summary>Desteklenmeyen uzantı ve olmayan dosya <c>false</c> döner, seçim değişmez.</summary>
    internal bool SetConvertAudioFile(string path)
    {
        if (ExternalAudio.FromFile(path) is not { } audio) return false;
        _convertExternalAudio = audio;
        RefreshConvertAudioFileList();
        RefreshConversion();
        return true;
    }

    internal void RemoveConvertAudioFile()
    {
        if (_convertExternalAudio is null) return;
        _convertExternalAudio = null;
        ChkConvertAudioShortest.IsChecked = false;
        RefreshConvertAudioFileList();
        RefreshConversion();
    }

    private void RefreshConvertAudioFileList()
    {
        ConvertAudioFileList.Children.Clear();
        if (_convertExternalAudio is { } audio) ConvertAudioFileList.Children.Add(AudioFileRow(audio, RemoveConvertAudioFile));
        ConvertAudioFileList.IsVisible = _convertExternalAudio is not null;
        ChkConvertAudioReplace.IsEnabled = ChkConvertAudioShortest.IsEnabled = _convertExternalAudio is not null && BtnConvertAudioFile.IsEnabled;
    }

    private async void OnConvertAddAudioFile(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(Say("main.audio.external.file-type")) { Patterns = ExternalAudio.Extensions.Select(x => "*" + x).ToArray() },
                    FilePickerFileTypes.All
                }
            });
            if (files.Select(file => file.TryGetLocalPath()).OfType<string>().FirstOrDefault() is { } path) SetConvertAudioFile(path);
        }
        catch (Exception ex)
        {
            ReportSourceError($"{Say("main.error.pick")}: {ex.Message}");
        }
    }
}
