using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

/// <summary>EKB-25: son kayıtlar penceresine açılan düğme. Liste kayıt klasöründen okunur, pencere her açılışta tazedir.</summary>
internal partial class RecorderView
{
    internal const int HistoryLimit = 20;

    internal Action<string> TrashFile { get; set; } = VidShrink.App.Platform.MoveToTrash;

    internal RecordingHistoryWindow? HistoryWindow { get; private set; }

    private void InitGecmis() => BtnHistory.Click += (_, _) => ShowHistory();

    internal RecordingHistoryWindow ShowHistory()
    {
        if (HistoryWindow is { } open)
        {
            open.Activate();
            return open;
        }

        var window = new RecordingHistoryWindow(new RecordingHistorySources(
            () => RecentRecordings.List(_settings.ResolveFolder(), HistoryLimit),
            QuickTrimMeasure,
            ResultThumbnails,
            OpenInPlayer,
            RevealFolder,
            TrashFile,
            new Share.ShareSession(() => CreateShareFlow)));
        window.Closed += (_, _) => HistoryWindow = null;
        HistoryWindow = window;
        if (TopLevel.GetTopLevel(this) is Window owner) window.Show(owner);
        else window.Show();
        return window;
    }
}
