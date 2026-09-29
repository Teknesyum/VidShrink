using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private static readonly string[] ContentKeys =
    [
        "recorder.wizard.content.game",
        "recorder.wizard.content.lesson",
        "recorder.wizard.content.meeting",
        "recorder.wizard.content.general"
    ];

    private static readonly string[] AudioKeys =
    [
        "recorder.wizard.audio.system",
        "recorder.wizard.audio.microphone",
        "recorder.wizard.audio.both",
        "recorder.wizard.audio.silent"
    ];

    private static readonly string[] DestinationKeys =
    [
        "recorder.wizard.destination.share",
        "recorder.wizard.destination.archive"
    ];

    private static readonly string[] QuestionKeys =
    [
        "recorder.wizard.question.content",
        "recorder.wizard.question.audio",
        "recorder.wizard.question.destination"
    ];

    private readonly List<int> _wizardPicks = [];

    internal Func<Task>? WizardMeasure { get; set; }

    internal bool WizardOpen => PanelWizard.IsVisible;

    internal int WizardStep => _wizardPicks.Count;

    internal string WizardQuestion => TxtWizardQuestion.Text ?? string.Empty;

    internal IReadOnlyList<string> WizardOptions => WizardButtons()
        .Where(b => b.IsVisible)
        .Select(b => ((TextBlock)b.Content!).Text ?? string.Empty)
        .ToList();

    internal bool WizardSameOffered => PanelWizard.IsVisible && BtnWizardSame.IsVisible;

    internal string WizardSummary => TxtWizardSummary.IsVisible ? TxtWizardSummary.Text ?? string.Empty : string.Empty;

    internal RecorderWizardAnswers? WizardAnswers =>
        _settings.WizardContent is { } content && _settings.WizardAudio is { } audio && _settings.WizardDestination is { } destination
            ? new RecorderWizardAnswers(content, audio, destination)
            : null;

    private Button[] WizardButtons() => [BtnWizardOpt0, BtnWizardOpt1, BtnWizardOpt2, BtnWizardOpt3];

    private static string[] KeysFor(int step) => step switch
    {
        0 => ContentKeys,
        1 => AudioKeys,
        _ => DestinationKeys
    };

    private void OnWizardOpen(object? sender, RoutedEventArgs e)
    {
        if (PanelWizard.IsVisible) CloseWizard();
        else OpenWizard();
    }

    internal void OpenWizard()
    {
        if (_session is not null || CountingDown || ReplayRunning) return;
        _wizardPicks.Clear();
        TxtWizardSummary.IsVisible = false;
        PanelWizard.IsVisible = true;
        Body.IsVisible = false;
        ShowWizardStep();
    }

    private void CloseWizard()
    {
        if (!PanelWizard.IsVisible) return;
        _wizardPicks.Clear();
        PanelWizard.IsVisible = false;
        Body.IsVisible = true;
    }

    private void RefreshWizard()
    {
        if (PanelWizard.IsVisible) ShowWizardStep();
    }

    private void ShowWizardStep()
    {
        var step = _wizardPicks.Count;
        TxtWizardStep.Text = Say("recorder.wizard.step", step + 1, RecorderWizard.QuestionCount);
        TxtWizardQuestion.Text = Say(QuestionKeys[step]);
        AutomationProperties.SetName(RowWizardOptions, TxtWizardQuestion.Text);

        var keys = KeysFor(step);
        var buttons = WizardButtons();
        for (var i = 0; i < buttons.Length; i++)
        {
            var visible = i < keys.Length;
            buttons[i].IsVisible = visible;
            if (!visible) continue;
            var label = Say(keys[i]);
            ((TextBlock)buttons[i].Content!).Text = label;
            AutomationProperties.SetName(buttons[i], label);
        }

        var same = Say("recorder.wizard.same");
        BtnWizardSame.Content = same;
        AutomationProperties.SetName(BtnWizardSame, same);
        BtnWizardSame.IsVisible = step == 0 && WizardAnswers is not null;
    }

    private async void OnWizardOption(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            await PickWizardAsync(index);
    }

    private async void OnWizardSame(object? sender, RoutedEventArgs e) => await WizardSameAsync();

    private void OnWizardCancel(object? sender, RoutedEventArgs e) => CloseWizard();

    internal async Task PickWizardAsync(int index)
    {
        if (!PanelWizard.IsVisible || index < 0 || index >= KeysFor(_wizardPicks.Count).Length) return;
        _wizardPicks.Add(index);
        if (_wizardPicks.Count < RecorderWizard.QuestionCount)
        {
            ShowWizardStep();
            return;
        }

        var answers = new RecorderWizardAnswers(
            (RecorderWizardContent)_wizardPicks[0],
            (RecorderWizardAudio)_wizardPicks[1],
            (RecorderWizardDestination)_wizardPicks[2]);
        await ApplyWizardAsync(answers);
    }

    internal async Task WizardSameAsync()
    {
        if (WizardAnswers is { } answers) await ApplyWizardAsync(answers);
    }

    private async Task ApplyWizardAsync(RecorderWizardAnswers answers)
    {
        CloseWizard();

        _settings.WizardContent = answers.Content;
        _settings.WizardAudio = answers.Audio;
        _settings.WizardDestination = answers.Destination;
        _settings.Save(_settingsPath);

        var plan = RecorderWizard.Plan(answers);
        var notes = new List<string>();
        WizardAudioBox(CmbMicrophone, plan.Microphone, "recorder.audio.microphone", notes);
        WizardAudioBox(CmbSystemAudio, plan.SystemAudio, "recorder.audio.system", notes);

        _guess = null;
        _autoChoice = null;
        if (RadManual.IsChecked ?? false)
        {
            var skip = SkipAutoMeasure;
            SkipAutoMeasure = true;
            try { RadAuto.IsChecked = true; }
            finally { SkipAutoMeasure = skip; }
        }

        await (WizardMeasure ?? MeasureAsync)();

        var summary = Say(
            "recorder.wizard.summary",
            Say(ContentKeys[(int)answers.Content]),
            Say(AudioKeys[(int)answers.Audio]),
            Say(DestinationKeys[(int)answers.Destination]),
            plan.Quality.ToString("0", CultureInfo.InvariantCulture));

        var parts = new List<string> { summary };
        parts.AddRange(notes);
        if (AutoSummaryText.Length > 0) parts.Add(AutoSummaryText);
        TxtWizardSummary.Text = string.Join(" ", parts);
        TxtWizardSummary.IsVisible = true;
    }

    private void WizardAudioBox(ComboBox box, bool wanted, string roleKey, List<string> notes)
    {
        if (!wanted)
        {
            box.SelectedIndex = 0;
            return;
        }

        if (box.SelectedIndex > 0) return;
        if (box.ItemCount > 1)
        {
            box.SelectedIndex = 1;
            return;
        }

        notes.Add(Say("recorder.wizard.no-device", Say(roleKey)));
    }
}
