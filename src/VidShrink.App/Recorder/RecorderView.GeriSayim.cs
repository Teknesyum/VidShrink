using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App.Localization;

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
            _settings.Save(_settingsPath);
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

    private DispatcherTimer? _scheduleTimer;

    private DateTime? _scheduleHandled;

    private bool _scheduleLoading;

    internal Func<DateTime> ScheduleClock { get; set; } = () => DateTime.Now;

    internal Func<Task>? ScheduledStart { get; set; }

    internal DateTime? ScheduleUpcoming => RecorderSchedule.Upcoming(_settings.ScheduleAt, _settings.ScheduleRepeat, ScheduleClock());

    private DayOfWeek[] ScheduleDays
    {
        get
        {
            var first = (int)Strings.Culture.DateTimeFormat.FirstDayOfWeek;
            return Enumerable.Range(0, 7).Select(i => (DayOfWeek)((first + i) % 7)).ToArray();
        }
    }

    private void InitSaatliKayit()
    {
        _scheduleLoading = true;
        CmbScheduleRepeat.ItemsSource = ScheduleRepeatLabels();
        CmbScheduleRepeat.SelectedIndex = Math.Max(0, Array.IndexOf(RecorderSchedule.Choices, _settings.ScheduleRepeat));
        CmbScheduleDay.ItemsSource = ScheduleDayLabels();
        var armed = _settings.ScheduleRepeat != RecorderRepeat.Off;
        CmbScheduleDay.SelectedIndex = Math.Max(0, Array.IndexOf(ScheduleDays, armed ? _settings.ScheduleAt.DayOfWeek : DateTime.Today.DayOfWeek));
        TxtScheduleTime.Text = armed ? ScheduleClockText(_settings.ScheduleAt) : string.Empty;
        _scheduleLoading = false;

        CmbScheduleRepeat.SelectionChanged += (_, _) => ApplyScheduleBoxes();
        CmbScheduleDay.SelectionChanged += (_, _) => ApplyScheduleBoxes();
        TxtScheduleTime.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Controls.TextBox.TextProperty) ApplyScheduleBoxes();
        };
        SyncSchedule();
    }

    private static List<string> ScheduleRepeatLabels()
        => RecorderSchedule.Choices
            .Select(repeat => repeat switch
            {
                RecorderRepeat.Once => Say("recorder.schedule.once"),
                RecorderRepeat.Daily => Say("recorder.schedule.daily"),
                RecorderRepeat.Weekly => Say("recorder.schedule.weekly"),
                _ => Say("recorder.schedule.off"),
            })
            .ToList();

    private List<string> ScheduleDayLabels()
        => ScheduleDays.Select(day => Strings.Culture.DateTimeFormat.GetDayName(day)).ToList();

    private void RefreshScheduleLabels()
    {
        var repeat = CmbScheduleRepeat.SelectedIndex;
        var day = CmbScheduleDay.SelectedIndex >= 0 ? ScheduleDays[CmbScheduleDay.SelectedIndex] : DateTime.Today.DayOfWeek;
        _scheduleLoading = true;
        CmbScheduleRepeat.ItemsSource = ScheduleRepeatLabels();
        CmbScheduleRepeat.SelectedIndex = repeat >= 0 ? repeat : 0;
        CmbScheduleDay.ItemsSource = ScheduleDayLabels();
        CmbScheduleDay.SelectedIndex = Array.IndexOf(ScheduleDays, day);
        _scheduleLoading = false;
        SyncSchedule();
    }

    private void ApplyScheduleBoxes()
    {
        if (_scheduleLoading) return;
        var index = CmbScheduleRepeat.SelectedIndex;
        var repeat = index >= 0 && index < RecorderSchedule.Choices.Length ? RecorderSchedule.Choices[index] : RecorderRepeat.Off;
        var day = CmbScheduleDay.SelectedIndex >= 0 ? ScheduleDays[CmbScheduleDay.SelectedIndex] : DateTime.Today.DayOfWeek;
        TxtScheduleError.IsVisible = false;
        if (repeat == RecorderRepeat.Off)
        {
            SetSchedule(RecorderRepeat.Off, default, day);
            return;
        }

        if (!RecorderSchedule.TryParseTime(TxtScheduleTime.Text, out var time))
        {
            TxtScheduleError.Text = Say("recorder.schedule.invalid");
            TxtScheduleError.IsVisible = true;
            SyncSchedule();
            return;
        }

        SetSchedule(repeat, time, day);
    }

    internal bool SetSchedule(RecorderRepeat repeat, TimeSpan time, DayOfWeek day)
    {
        if (repeat == RecorderRepeat.Off)
        {
            _settings.ScheduleRepeat = RecorderRepeat.Off;
        }
        else
        {
            if (time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)) return false;
            _settings.ScheduleRepeat = repeat;
            _settings.ScheduleAt = RecorderSchedule.First(ScheduleClock(), time, repeat, day);
        }

        _scheduleHandled = null;
        _settings.Save(_settingsPath);
        SyncSchedule();
        return true;
    }

    internal async Task<bool> ScheduleTickAsync()
    {
        var repeat = _settings.ScheduleRepeat;
        var now = ScheduleClock();
        if (RecorderSchedule.Latest(_settings.ScheduleAt, repeat, now) is not { } due || due == _scheduleHandled) return false;

        _scheduleHandled = due;
        var onTime = now - due < RecorderSchedule.LateLimit;
        if (repeat == RecorderRepeat.Once)
        {
            _settings.ScheduleRepeat = RecorderRepeat.Off;
            _settings.Save(_settingsPath);
            SyncScheduleBoxes();
        }

        SyncSchedule();
        if (!onTime) return false;
        await (ScheduledStart ?? StartAsync)();
        return true;
    }

    private void SyncScheduleBoxes()
    {
        _scheduleLoading = true;
        CmbScheduleRepeat.SelectedIndex = Math.Max(0, Array.IndexOf(RecorderSchedule.Choices, _settings.ScheduleRepeat));
        _scheduleLoading = false;
    }

    private void SyncSchedule()
    {
        var upcoming = ScheduleUpcoming;
        var weekly = CmbScheduleRepeat.SelectedIndex >= 0 && CmbScheduleRepeat.SelectedIndex < RecorderSchedule.Choices.Length
            && RecorderSchedule.Choices[CmbScheduleRepeat.SelectedIndex] == RecorderRepeat.Weekly;
        PanelScheduleDay.IsVisible = weekly;
        TxtScheduleNext.IsVisible = upcoming is not null;
        TxtScheduleNext.Text = upcoming is { } next
            ? Say("recorder.schedule.next", next.ToString("ddd HH:mm", Strings.Culture))
            : string.Empty;

        if (_settings.ScheduleRepeat == RecorderRepeat.Off)
        {
            _scheduleTimer?.Stop();
        }
        else
        {
            _scheduleTimer ??= CreateScheduleTimer();
            if (!_scheduleTimer.IsEnabled) _scheduleTimer.Start();
        }

        RefreshSerit();
    }

    private DispatcherTimer CreateScheduleTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += async (_, _) => await ScheduleTickAsync();
        return timer;
    }

    private static string ScheduleClockText(DateTime at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);
}
