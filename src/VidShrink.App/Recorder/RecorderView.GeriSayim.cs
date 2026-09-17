using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private CancellationTokenSource? _countdown;

    internal Func<TimeSpan, CancellationToken, Task> CountdownDelay { get; set; } = Task.Delay;

    internal bool CountingDown => _countdown is not null;

    internal int CountdownLeft { get; private set; }

    internal string StateText => TxtState.Text ?? string.Empty;

    internal int SelectedCountdown
    {
        get
        {
            var index = CmbCountdown.SelectedIndex;
            return index >= 0 && index < RecorderSettings.CountdownChoices.Length
                ? RecorderSettings.CountdownChoices[index]
                : 0;
        }
    }

    private void InitGeriSayim()
    {
        CmbCountdown.ItemsSource = CountdownLabels();
        CmbCountdown.SelectedIndex = Math.Max(0, Array.IndexOf(RecorderSettings.CountdownChoices, _settings.CountdownSeconds));
        CmbCountdown.SelectionChanged += (_, _) =>
        {
            if (CmbCountdown.SelectedIndex < 0) return;
            _settings.CountdownSeconds = SelectedCountdown;
            _settings.Save(RecorderSettings.FilePath);
        };
    }

    private static List<string> CountdownLabels()
        => RecorderSettings.CountdownChoices
            .Select(seconds => seconds == 0
                ? Say("recorder.countdown.off")
                : Say("recorder.countdown.seconds", seconds))
            .ToList();

    private void RefreshCountdownLabels()
    {
        var selected = CmbCountdown.SelectedIndex;
        CmbCountdown.ItemsSource = CountdownLabels();
        CmbCountdown.SelectedIndex = selected >= 0 ? selected : 0;
    }

    internal async Task<bool> CountdownAsync()
    {
        var seconds = SelectedCountdown;
        if (seconds <= 0 || _countdown is not null) return seconds <= 0;

        var cts = new CancellationTokenSource();
        _countdown = cts;
        try
        {
            for (var left = seconds; left > 0; left--)
            {
                CountdownLeft = left;
                RefreshSerit();
                await CountdownDelay(TimeSpan.FromSeconds(1), cts.Token);
                cts.Token.ThrowIfCancellationRequested();
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            _countdown = null;
            CountdownLeft = 0;
            cts.Dispose();
            RefreshSerit();
        }
    }

    internal void CancelCountdown() => _countdown?.Cancel();

    private void OnCountdownCancel(object? sender, RoutedEventArgs e) => CancelCountdown();

    internal static string CountdownDigits(int left) => left.ToString(CultureInfo.InvariantCulture);
}
