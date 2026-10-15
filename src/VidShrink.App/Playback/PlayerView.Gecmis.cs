using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    /// <summary>OYB-11: diskteki geçmişin pencere satırları; açık dosyanın henüz yazılmamış konumu sayılmaz.</summary>
    internal IReadOnlyList<HistoryRow> HistoryRows(string? search)
        => (HistoryPath?.Invoke() is { } file ? PlaybackHistory.Load(file) : _history).Rows(search);

    /// <summary>OYB-11: geçmiş penceresini açar; seçilen satır son dosyalar yolundan açılır, dosya yoksa bildirim düşer.</summary>
    internal HistoryWindow ShowHistory()
    {
        var window = new HistoryWindow(HistoryRows, OpenRecent);
        if (TopLevel.GetTopLevel(this) is Window owner) window.Show(owner);
        else window.Show();
        return window;
    }

    /// <summary>OYB-14: son dosyaları ve izleme geçmişini siler; yer imleri kalır.</summary>
    internal void ClearHistory()
    {
        EnsureSettings();
        _recent.Clear();
        _recent.Save(RecentFile());
        if (HistoryPath?.Invoke() is { } file)
        {
            var history = PlaybackHistory.Load(file);
            history.Clear();
            history.Save(file);
            _history = history;
        }
        else _history.Clear();

        _trace.Add("history -> cleared");
        _notice = Strings.Get("player.list.history-cleared");
        RefreshState();
    }

    internal void ToggleKeepHistory()
    {
        EnsureSettings();
        _settings.KeepHistory = !_settings.KeepHistory;
        SaveSettings();
        _trace.Add("keephistory -> " + _settings.KeepHistory);
        AdvancedChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void SetResume(ResumeMode mode)
    {
        EnsureSettings();
        _settings.Resume = mode;
        SaveSettings();
        _trace.Add("resumeonopen -> " + mode);
        AdvancedChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>159: devam ayarı üç seçenekli; "sor" kaldığı yer olan dosyada açılışta karar ister.</summary>
    internal MenuItem ResumeMenu()
    {
        EnsureSettings();
        var current = _settings.Resume;
        return Submenu(Strings.Get("player.view.resume"), new List<Control>
        {
            Choice(Strings.Get("player.view.resume-on"), current == ResumeMode.On, () => SetResume(ResumeMode.On)),
            Choice(Strings.Get("player.view.resume-ask"), current == ResumeMode.Ask, () => SetResume(ResumeMode.Ask)),
            Choice(Strings.Get("player.view.resume-off"), current == ResumeMode.Off, () => SetResume(ResumeMode.Off))
        });
    }

    private double? _pendingResume;

    internal bool ResumePromptVisible => ResumeBox.IsVisible;

    /// <summary>159: "sor" kipinde dosya baştan, duraklatılmış açılır ve karar kutusu belirir; karar verilene dek kayıttaki konum korunur.</summary>
    private void AskResume(double at)
    {
        _pendingResume = at;
        ToolTip.SetTip(BtnResumeContinue, Saat.Ekran(TimeSpan.FromSeconds(at)));
        ResumeBox.IsVisible = true;
        _trace.Add("resume -> ask " + Saat.Tani.Konum(at));
    }

    private void HideResumePrompt()
    {
        _pendingResume = null;
        if (ResumeBox is not null) ResumeBox.IsVisible = false;
    }

    internal void ResumeChoice(bool continueFrom)
    {
        if (_pendingResume is not { } at) return;
        HideResumePrompt();
        _seek.GoTo(continueFrom ? at : 0);
        _trace.Add("resume -> " + (continueFrom ? Saat.Tani.Konum(at) : "restart"));
        if (!_playing) TogglePlay(false);
    }

    private void OnResumeContinue(object? sender, RoutedEventArgs e) => ResumeChoice(true);

    private void OnResumeRestart(object? sender, RoutedEventArgs e) => ResumeChoice(false);

    /// <summary>OYB-15: açık dosyanın yer imi penceresi; gitme, adlandırma ve silme geçmiş kaydına hemen yazılır.</summary>
    internal BookmarksWindow? ShowBookmarks()
    {
        if (_path is not { } path)
        {
            _trace.Add("bookmarklist -> no");
            return null;
        }

        var window = new BookmarksWindow(
            () => _history.NamedBookmarks(path),
            at =>
            {
                if (!string.Equals(_path, path, StringComparison.Ordinal)) return;
                _trackPaused = false;
                _seek.GoTo(at);
                _trace.Add("bookmarkgo -> " + Saat.Tani.Konum(at));
            },
            (at, name) => StoreBookmarks(_history.RenameBookmark(path, at, name)),
            at => StoreBookmarks(_history.RemoveBookmark(path, at)));
        if (TopLevel.GetTopLevel(this) is Window owner) window.Show(owner);
        else window.Show();
        _trace.Add("bookmarklist -> " + _history.Bookmarks(path).Count);
        return window;
    }

    private bool StoreBookmarks(bool changed)
    {
        if (!changed) return false;
        _history.Save(HistoryPath?.Invoke());
        _marksKey = "";
        RefreshState();
        return true;
    }

    internal void ToggleAudioLanguage()
    {
        EnsureSettings();
        _settings.AudioInInterfaceLanguage = !_settings.AudioInInterfaceLanguage;
        SaveSettings();
        _trace.Add("audiolanguage -> " + _settings.AudioInInterfaceLanguage);
        AdvancedChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void ToggleSubtitleLanguage()
    {
        EnsureSettings();
        _settings.SubtitleInInterfaceLanguage = !_settings.SubtitleInInterfaceLanguage;
        SaveSettings();
        _trace.Add("subtitlelanguage -> " + _settings.SubtitleInInterfaceLanguage);
        AdvancedChanged?.Invoke(this, EventArgs.Empty);
    }
}
