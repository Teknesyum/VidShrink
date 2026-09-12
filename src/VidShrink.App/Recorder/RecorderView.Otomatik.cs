using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>
/// Otomatik kip: kodlama kolunu kullanıcı değil program yazıyor. Karar iki parçadan
/// geliyor — aday merdivenini <see cref="RecorderAutoPlan"/> (saf, Core), kazananı
/// <see cref="RecorderAutoProbe"/> (aday başına kısa gerçek kayıt, düşen kare sayımı)
/// üretiyor.
///
/// <para>Kip açıkken elle ayar paneli gizleniyor: devre dışı bırakılmış kutu bırakmak
/// yerine görünmüyor — deponun şeritte de izlediği kalıp bu.</para>
/// </summary>
internal partial class RecorderView
{
    private RecorderAutoChoice? _autoChoice;
    private RecorderAutoResult? _autoResult;
    private CancellationTokenSource? _autoStop;

    /// <summary>Otomatik kip açık mı.</summary>
    internal bool AutoMode => ChkAuto.IsChecked ?? false;

    /// <summary>Yazılacak aday; ölçüm koşmadıysa merdivenin ilk adayı.</summary>
    internal RecorderAutoChoice? AutoChoice => _autoChoice;

    /// <summary>Ölçümün kendisi; ölçüm koşmadıysa <c>null</c>.</summary>
    internal RecorderAutoResult? AutoResult => _autoResult;

    internal string AutoSummaryText => TxtAutoSummary.Text ?? string.Empty;

    private void InitOtomatik()
    {
        ChkAuto.IsChecked = _settings.AutoMode;
        ChkAuto.IsCheckedChanged += OnAutoToggled;
        ApplyAutoVisibility();
    }

    /// <summary>
    /// Kip açılınca ölçüm kendiliğinden koşuyor: kullanıcıdan beklenen tek şey kutuyu
    /// işaretlemek. Kapanınca elle panel geri geliyor ve ölçüm sonucu düşüyor.
    /// </summary>
    private async void OnAutoToggled(object? sender, RoutedEventArgs e)
    {
        _settings.AutoMode = AutoMode;
        _settings.Save(RecorderSettings.FilePath);
        ApplyAutoVisibility();

        if (!AutoMode)
        {
            _autoChoice = null;
            _autoResult = null;
            TxtAutoSummary.Text = string.Empty;
            return;
        }

        await MeasureAsync();
    }

    private async void OnAutoMeasure(object? sender, RoutedEventArgs e) => await MeasureAsync();

    private void ApplyAutoVisibility()
    {
        var auto = AutoMode;
        PanelManualOptions.IsVisible = !auto;
        PanelAutoResult.IsVisible = auto;
        BtnAutoMeasure.IsVisible = auto;
    }

    /// <summary>
    /// Merdiveni kurar, denemeleri koşar ve kazananı yazar. ffmpeg yoksa ya da deneme
    /// kaydı tamamlanmazsa sebep yutulmuyor: merdivenin ilk adayı yazılıyor ve ölçülmediği
    /// ekranda söyleniyor.
    /// </summary>
    internal async Task MeasureAsync()
    {
        if (_session is not null) return;

        var candidates = RecorderAutoPlan.Candidates(Machine());
        _autoChoice = candidates[0];
        _autoResult = null;

        if (BuildRequest(applyAuto: false) is not { } request || !ToolLocator.IsAvailable(out _))
        {
            ShowAutoSummary(null);
            return;
        }

        TxtAutoSummary.Text = Say("recorder.auto.measuring");
        BtnAutoMeasure.IsEnabled = false;

        _autoStop?.Cancel();
        _autoStop = new CancellationTokenSource();

        try
        {
            var scratch = Path.Combine(Path.GetTempPath(), "VidShrink", "auto");
            var result = await RecorderAutoProbe.ChooseAsync(
                request, scratch, candidates, RecorderAutoProbe.TrialSeconds, _autoStop.Token);

            _autoResult = result;
            _autoChoice = result.Choice;
            ShowAutoSummary(result);
        }
        catch (OperationCanceledException)
        {
            ShowAutoSummary(null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowError(Say("recorder.error.start", ex.Message));
            ShowAutoSummary(null);
        }
        finally
        {
            BtnAutoMeasure.IsEnabled = true;
        }
    }

    private void ShowAutoSummary(RecorderAutoResult? result)
    {
        if (_autoChoice is not { } choice)
        {
            TxtAutoSummary.Text = string.Empty;
            return;
        }

        var size = choice.Scale is { } scale
            ? scale.Width.ToString(CultureInfo.InvariantCulture) + "×" + scale.Height.ToString(CultureInfo.InvariantCulture)
            : Say("recorder.auto.native");

        var head = Say(
            "recorder.auto.summary",
            choice.VideoCodec,
            choice.Fps.ToString(CultureInfo.InvariantCulture),
            size,
            RecorderArguments.Extension(choice.Container));

        var tail = result is { Measured: true }
            ? Say(
                "recorder.auto.trials",
                result.Trials.Count.ToString(CultureInfo.InvariantCulture),
                result.Trials.First(t => t.Choice == result.Choice).DroppedFrames.ToString("N0", Strings.Culture))
            : Say("recorder.auto.unmeasured");

        TxtAutoSummary.Text = head + " — " + tail + " " + string.Join(" · ", choice.Notes.Select(NoteText));
    }

    private static string NoteText(RecorderAutoNote note) => note switch
    {
        RecorderAutoNote.HardwareEncoderChosen => Say("recorder.auto.note.hardware"),
        RecorderAutoNote.SoftwareEncoderFallback => Say("recorder.auto.note.software"),
        RecorderAutoNote.FpsFollowsRefreshRate => Say("recorder.auto.note.refresh"),
        RecorderAutoNote.FpsSteppedDown => Say("recorder.auto.note.stepped"),
        RecorderAutoNote.ResolutionKept => Say("recorder.auto.note.kept"),
        RecorderAutoNote.ResolutionHalved => Say("recorder.auto.note.halved"),
        RecorderAutoNote.ContainerSurvivesKill => Say("recorder.auto.note.container"),
        RecorderAutoNote.PresetFollowsEncoder => Say("recorder.auto.note.preset"),
        _ => string.Empty
    };

    /// <summary>
    /// Otomatik kipin girdisi. Yakalama boyutu seçilen hedeften, yenileme hızı işletim
    /// sisteminden, çalışan donanım kolları yoklamadan geliyor; hiçbiri varsayılmıyor.
    /// </summary>
    internal RecorderMachine Machine()
    {
        var (width, height) = CaptureSize();
        return new RecorderMachine(
            width,
            height,
            ScreenRefresh.PrimaryHz(),
            Environment.ProcessorCount,
            WorkingEncoders());
    }

    private (int Width, int Height) CaptureSize()
    {
        if (SelectedTarget == RecorderTargetKind.Region
            && int.TryParse(TxtRegionWidth.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)
            && int.TryParse(TxtRegionHeight.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)
            && w > 0 && h > 0)
            return (w, h);

        var screens = TopLevel.GetTopLevel(this)?.Screens;
        if (screens is null || screens.All.Count == 0) return (0, 0);

        var index = _settings.ScreenIndex >= 0 && _settings.ScreenIndex < screens.All.Count ? _settings.ScreenIndex : 0;
        var bounds = screens.All[index].Bounds;
        return (bounds.Width, bounds.Height);
    }

    /// <summary>
    /// Yoklamanın çalıştığını gördüğü donanım kolları. ffmpeg yoksa boş liste dönüyor ve
    /// merdiven yazılım koluna düşüyor.
    /// </summary>
    private static IReadOnlyList<string> WorkingEncoders()
    {
        if (!ToolLocator.IsAvailable(out _)) return Array.Empty<string>();

        try { return RecorderAutoProbe.WorkingHardwareEncoders(EncoderCapabilities.Instance); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException) { return Array.Empty<string>(); }
    }
}
