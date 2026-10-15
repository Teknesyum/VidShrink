using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// C1-6: B1'in yalnız CLI'da duran yüzeyi arayüze iner — ses yüksekliği (<c>loudnorm</c>),
/// gürültü temizleme (<c>afftdn</c>, ARC-P1), sabit kazanç, dış altyazı dosyaları ve kaynağın metin ya da görüntü altyazısını yakma. Dış altyazı ve
/// yakma o videoya aittir: yeni kaynak açılınca sıfırlanır, kuyruk penceresine taşınmaz.
/// </summary>
public partial class MainWindow
{
    internal static readonly int[] GainCandidates = { -12, -9, -6, -3, 0, 3, 6, 9, 12 };

    private readonly List<ExternalSubtitle> _externalSubtitles = new();
    private ExternalAudio? _externalAudio;
    private readonly List<int> _burnChoices = new();
    private bool _textBurnOffered = true;

    internal IReadOnlyList<ExternalSubtitle> ExternalSubtitles => _externalSubtitles;

    private void InitializeTrackUi()
    {
        var gain = CmbAudioGain.SelectedIndex;
        CmbAudioGain.ItemsSource = GainCandidates
            .Select(db => db == 0 ? Say("main.audio.gain.none") : db.ToString("+0;-0", CultureInfo.InvariantCulture) + " dB")
            .ToList();
        CmbAudioGain.SelectedIndex = gain >= 0 ? gain : Array.IndexOf(GainCandidates, 0);
        var font = CmbBurnFont.SelectedIndex;
        CmbBurnFont.ItemsSource = new[] { Say("main.subtitles.style.default") }.Concat(BurnFontChoices).ToList();
        CmbBurnFont.SelectedIndex = Math.Max(font, 0);
        var size = CmbBurnSize.SelectedIndex;
        CmbBurnSize.ItemsSource = new[] { Say("main.subtitles.style.default") }
            .Concat(BurnSizeCandidates.Select(s => s.ToString(CultureInfo.InvariantCulture))).ToList();
        CmbBurnSize.SelectedIndex = Math.Max(size, 0);
        RefreshBurnChoices();
        RefreshSubtitleList();
        RefreshAudioFileList();
        YukseklikDurumu();
    }

    internal static IReadOnlyList<string> BurnFontChoices => VideoFilterChain.BurnFontChoices;

    internal static readonly int[] BurnSizeCandidates = { 12, 16, 20, 24, 28, 32, 40, 48 };

    /// <summary>
    /// SIK-P5: yazı tipi, boyut ve kutu seçimi yalnız metin izi yakılırken görünür; resim
    /// altyazıda (PGS, VOBSUB, DVB) <c>force_style</c> anlamsızdır ve plan onu yazmaz.
    /// </summary>
    private void RefreshBurnStyle() => BurnStylePanel.IsVisible = BurnsTextSubtitle;

    private bool BurnsTextSubtitle
    {
        get
        {
            if (BurnChoice is not int burn || _info is not { } info) return false;
            var subtitles = info.Streams.Where(stream => stream.Kind == StreamKind.Subtitle).ToList();
            return burn < subtitles.Count && StreamMapping.IsTextSubtitle(subtitles[burn].Codec);
        }
    }

    private void ResetBurnStyle()
    {
        CmbBurnFont.SelectedIndex = 0;
        CmbBurnSize.SelectedIndex = 0;
        ChkBurnBox.IsChecked = false;
    }

    /// <summary>
    /// Kaynağın metin ve görüntü altyazıları (PGS, VOBSUB, DVB) yakma listesine girer. Metin izi
    /// <c>subtitles</c> süzgeciyle, görüntü izi <see cref="VideoFilterChain.OverlayGraph"/> ile yakılır;
    /// yolu plan seçer, liste yalnız izi söyler. ffmpeg derlemesinde <c>subtitles</c> süzgeci yoksa
    /// (libass'sız) metin izi listeye girmez; yoklama okunamadıysa girer, CLI'daki kuralla aynı.
    /// </summary>
    private void RefreshBurnChoices()
    {
        var chosen = BurnChoice;
        _burnChoices.Clear();
        var items = new List<string> { Say("main.subtitles.burn.off") };
        if (_info is { } info)
        {
            var textBurn = TextBurnAvailable;
            var subtitles = info.Streams.Where(stream => stream.Kind == StreamKind.Subtitle).ToList();
            for (var i = 0; i < subtitles.Count; i++)
            {
                var text = StreamMapping.IsTextSubtitle(subtitles[i].Codec);
                if (text ? !textBurn : !StreamMapping.IsImageSubtitle(subtitles[i].Codec)) continue;
                _burnChoices.Add(i);
                var label = subtitles[i].Title ?? subtitles[i].Language;
                items.Add(label is null ? $"#{i + 1}" : $"#{i + 1} {label}");
            }
        }
        CmbBurnSubtitle.ItemsSource = items;
        var at = chosen is int c ? _burnChoices.IndexOf(c) : -1;
        CmbBurnSubtitle.SelectedIndex = at + 1;
        CmbBurnSubtitle.IsEnabled = _burnChoices.Count > 0;
        RefreshBurnStyle();
        TaramayiSifirla();
    }

    internal bool TextBurnAvailable
        => _encoders is not VidShrink.Ffmpeg.EncoderCapabilities { Loaded: true } loaded || loaded.HasFilter(VideoFilterChain.BurnFilterName);

    /// <summary>Seçilen yakma izi, kaynağın altyazıları içinde 0 tabanlı sıra; seçim yoksa <c>null</c>.</summary>
    internal int? BurnChoice
    {
        get
        {
            var index = CmbBurnSubtitle.SelectedIndex - 1;
            return index >= 0 && index < _burnChoices.Count ? _burnChoices[index] : null;
        }
    }

    private void ApplyTrackOptions(PlanOptions options)
    {
        options.AudioLoudnorm = ChkAudioLoudnorm.IsChecked == true;
        options.AudioDenoise = ChkAudioDenoise.IsChecked == true;
        var gain = CmbAudioGain.SelectedIndex;
        options.AudioGainDb = gain >= 0 && gain < GainCandidates.Length && GainCandidates[gain] != 0 ? GainCandidates[gain] : null;
        options.ExternalSubtitles = _externalSubtitles.ToList();
        options.ExternalAudio = ExternalAudioChoice;
        if (BurnChoice is int burn) options.Filters = options.Filters with { BurnSubtitle = burn };
        if (BurnsTextSubtitle)
        {
            var font = CmbBurnFont.SelectedIndex - 1;
            var size = CmbBurnSize.SelectedIndex - 1;
            options.Filters = options.Filters with
            {
                BurnFont = font >= 0 && font < BurnFontChoices.Count ? BurnFontChoices[font] : null,
                BurnFontSize = size >= 0 && size < BurnSizeCandidates.Length ? BurnSizeCandidates[size] : null,
                BurnBox = ChkBurnBox.IsChecked == true
            };
        }
    }

    /// <summary>Dosya başına bir kez eklenir; desteklenmeyen uzantı ve olmayan dosya sessizce atlanır. Eklenen sayı döner.</summary>
    internal int AddSubtitleFiles(IEnumerable<string> paths)
    {
        var added = 0;
        foreach (var path in paths)
        {
            if (ExternalSubtitle.FromFile(path) is not { } subtitle) continue;
            if (_externalSubtitles.Any(s => string.Equals(s.Path, subtitle.Path, StringComparison.OrdinalIgnoreCase))) continue;
            _externalSubtitles.Add(subtitle);
            added++;
        }
        if (added > 0) SubtitlesChanged();
        return added;
    }

    internal void RemoveSubtitle(int index)
    {
        if (index < 0 || index >= _externalSubtitles.Count) return;
        _externalSubtitles.RemoveAt(index);
        SubtitlesChanged();
    }

    /// <summary>Yeni kaynak: önceki videonun dış altyazıları, dış sesi ve yakma seçimi taşınmaz.</summary>
    private void ResetTracksForSource()
    {
        _externalSubtitles.Clear();
        _externalAudio = null;
        ChkAudioReplace.IsChecked = false;
        RefreshAudioFileList();
        CmbBurnSubtitle.SelectedIndex = 0;
        ResetBurnStyle();
        RefreshBurnChoices();
        RefreshSubtitleList();
        ResetConvertSubtitles();
        ResetConvertAudio();
        YuksekligiSifirla();
    }

    private void SubtitlesChanged()
    {
        RefreshSubtitleList();
        OnOptionChanged();
    }

    internal static bool IsSubtitleFile(string path)
        => ExternalSubtitle.Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    private void RefreshSubtitleList()
    {
        SubtitleList.Children.Clear();
        for (var i = 0; i < _externalSubtitles.Count; i++)
        {
            var index = i;
            SubtitleList.Children.Add(SubtitleRow(_externalSubtitles[i], () => RemoveSubtitle(index)));
        }
        SubtitleList.IsVisible = _externalSubtitles.Count > 0;
    }

    private Grid SubtitleRow(ExternalSubtitle subtitle, Action onRemove)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = TokenOf("SpaceXs") };
        var name = new TextBlock
        {
            Text = subtitle.Language is { } language ? $"{Path.GetFileName(subtitle.Path)} · {language}" : Path.GetFileName(subtitle.Path),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (this.FindResource("Hint") is Avalonia.Styling.ControlTheme hint) name.Theme = hint;
        ToolTip.SetTip(name, subtitle.Language is { } dil ? $"{subtitle.Path} · {dil}" : subtitle.Path);
        row.Children.Add(name);
        var size = TokenOf("IconSizeSm");
        var remove = new Button
        {
            Content = new PathIcon { Data = this.FindResource("IconClose") as Geometry, Width = size, Height = size },
            Padding = this.FindResource("ChipPadding") is Avalonia.Thickness padding ? padding : default,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (this.FindResource("GhostButton") is Avalonia.Styling.ControlTheme ghost) remove.Theme = ghost;
        AutomationProperties.SetName(remove, Say("main.subtitles.remove"));
        ToolTip.SetTip(remove, Say("main.subtitles.remove"));
        remove.Click += (_, _) => onRemove();
        Grid.SetColumn(remove, 1);
        row.Children.Add(remove);
        return row;
    }

    private double TokenOf(string key) => this.FindResource(key) is double value ? value : 0;

    private async void OnAddSubtitle(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(Say("main.subtitles.file-type")) { Patterns = ExternalSubtitle.Extensions.Select(x => "*" + x).ToArray() },
                    FilePickerFileTypes.All
                }
            });
            AddSubtitleFiles(files.Select(file => file.TryGetLocalPath()).OfType<string>());
        }
        catch (Exception ex)
        {
            ReportSourceError($"{Say("main.error.pick")}: {ex.Message}");
        }
    }

    /// <summary>
    /// SIK-P4: tek dış ses dosyası. "Yerine koy" kutusu kaynağın sesini bırakır; kutu kapalıyken iz
    /// kaynağın sesinin yanına eklenir. Desteklenmeyen uzantı ve olmayan dosya <c>false</c> döner.
    /// </summary>
    internal bool SetAudioFile(string path)
    {
        if (ExternalAudio.FromFile(path) is not { } audio) return false;
        _externalAudio = audio;
        RefreshAudioFileList();
        OnOptionChanged();
        return true;
    }

    internal void RemoveAudioFile()
    {
        if (_externalAudio is null) return;
        _externalAudio = null;
        RefreshAudioFileList();
        OnOptionChanged();
    }

    internal ExternalAudio? ExternalAudioChoice
        => _externalAudio is { } audio ? audio with { Replace = ChkAudioReplace.IsChecked == true } : null;

    private void RefreshAudioFileList()
    {
        AudioFileList.Children.Clear();
        if (_externalAudio is { } audio) AudioFileList.Children.Add(AudioFileRow(audio));
        AudioFileList.IsVisible = _externalAudio is not null;
    }

    private Grid AudioFileRow(ExternalAudio audio)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = TokenOf("SpaceXs") };
        var name = new TextBlock
        {
            Text = audio.Language is { } language ? $"{Path.GetFileName(audio.Path)} · {language}" : Path.GetFileName(audio.Path),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (this.FindResource("Hint") is Avalonia.Styling.ControlTheme hint) name.Theme = hint;
        ToolTip.SetTip(name, audio.Language is { } dil ? $"{audio.Path} · {dil}" : audio.Path);
        row.Children.Add(name);
        var size = TokenOf("IconSizeSm");
        var remove = new Button
        {
            Content = new PathIcon { Data = this.FindResource("IconClose") as Geometry, Width = size, Height = size },
            Padding = this.FindResource("ChipPadding") is Avalonia.Thickness padding ? padding : default,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (this.FindResource("GhostButton") is Avalonia.Styling.ControlTheme ghost) remove.Theme = ghost;
        AutomationProperties.SetName(remove, Say("main.audio.external.remove"));
        ToolTip.SetTip(remove, Say("main.audio.external.remove"));
        remove.Click += (_, _) => RemoveAudioFile();
        Grid.SetColumn(remove, 1);
        row.Children.Add(remove);
        return row;
    }

    private async void OnAddAudioFile(object? sender, RoutedEventArgs e)
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
            if (files.Select(file => file.TryGetLocalPath()).OfType<string>().FirstOrDefault() is { } path) SetAudioFile(path);
        }
        catch (Exception ex)
        {
            ReportSourceError($"{Say("main.error.pick")}: {ex.Message}");
        }
    }
}
