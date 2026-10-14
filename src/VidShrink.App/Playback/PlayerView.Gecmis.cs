using System;
using System.Collections.Generic;
using Avalonia.Controls;
using VidShrink.App.Localization;

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

    internal void ToggleResume()
    {
        EnsureSettings();
        _settings.Resume = !_settings.Resume;
        SaveSettings();
        _trace.Add("resumeonopen -> " + _settings.Resume);
        AdvancedChanged?.Invoke(this, EventArgs.Empty);
    }
}
