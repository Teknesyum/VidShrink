using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

/// <summary>
/// DON-P6: Dönüştür'ün toplu kuyruğu. Her satır bir dosya; açılır kutu "ortak ayar" ya da
/// kayıtlı bir Dönüştür ön ayarı seçer. İkisi de <see cref="ConversionPreset"/>; plan
/// <see cref="ConversionPresetPlan.ToPlan"/> ile kurulur. Düşen dosya kuyruğu durdurmaz,
/// sonda sayılar tek satırda.
/// </summary>
public partial class ConvertQueueWindow : Window
{
    private readonly string _language;
    private readonly IReadOnlyList<ConversionPreset> _presets;
    private readonly HdrPolicy _hdr;
    private readonly string? _outputFolder;
    private readonly Dictionary<ConversionQueueItem, (ComboBox Choice, TextBlock State)> _rows = new();
    private CancellationTokenSource? _cts;
    private Task? _run;

    public ConvertQueueWindow() : this(Array.Empty<string>(), new ConversionPreset(), Array.Empty<ConversionPreset>(), HdrPolicy.Preserve, null)
    {
    }

    internal ConvertQueueWindow(IReadOnlyList<string> paths, ConversionPreset shared, IReadOnlyList<ConversionPreset> presets,
        HdrPolicy hdr, string? outputFolder)
    {
        Queue = new ConversionQueue(paths, shared);
        _presets = presets;
        _hdr = hdr;
        _outputFolder = outputFolder;

        var language = MainWindow.ResolveLanguage(null, CultureInfo.CurrentUICulture.Name);
        try
        {
            var settings = UpdateSettings.Load();
            language = MainWindow.ResolveLanguage(settings.Language, CultureInfo.CurrentUICulture.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        _language = language;
        Strings.Use(_language);

        InitializeComponent();
        if (Playback.HoverZone.MotionReduced) Classes.Add("reduced-motion");

        FlowDirection = Strings.IsRightToLeftLanguage(_language)
            ? Avalonia.Media.FlowDirection.RightToLeft
            : Avalonia.Media.FlowDirection.LeftToRight;
        Classes.Set("untracked", Strings.IsUntrackedLanguage(_language));

        if (OperatingSystem.IsMacOS()) WindowDecorations = WindowDecorations.Full;
        QueueShell.PointerPressed += OnShellPointerPressed;

        BtnStart.Click += (_, _) => _ = StartAsync();
        BtnCancel.Click += OnCancel;
        BtnReveal.Click += OnReveal;

        TxtHeadline.Text = Say("main.convert-queue.headline", Queue.Items.Count);
        foreach (var item in Queue.Items) ItemList.Children.Add(Row(item));
    }

    internal ConversionQueue Queue { get; }

    internal string Language => _language;

    /// <summary>Bir dosyanın dönüştürülmesi; testler sahtesini verir, yoksa gerçek ffmpeg koşar.</summary>
    internal Func<string, ConversionPreset, IProgress<EncodeProgress>, CancellationToken, Task<string>>? Kosturucu { get; set; }

    internal IQueueEndActions Actions { get; set; } = SystemQueueEndActions.Instance;

    internal ComboBox ChoiceFor(int index) => _rows[Queue.Items[index]].Choice;

    internal string StateTextFor(int index) => _rows[Queue.Items[index]].State.Text ?? "";

    /// <summary>
    /// Ön ayardan plan ve çıktı yolu. Doğrulama iletileri dile çevrilip istisna olarak atılır:
    /// kuyruk onu o dosyanın hatası sayar, sonrakilere geçer.
    /// </summary>
    internal static (ConversionPlan Plan, string Output) Prepare(MediaInfo info, ConversionPreset preset, HdrPolicy hdr, string? outputFolder)
    {
        var plan = preset.ToPlan(hdr);
        var errors = ConversionArguments.Validate(info, plan);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors.Select(LanguageCatalog.Validation)));
        return (plan, ShrinkEngine.UniqueOutputPath(info.FilePath, "converted", plan.Container, outputFolder));
    }

    private async Task<string> RunOneAsync(string path, ConversionPreset preset, IProgress<EncodeProgress> progress, CancellationToken ct)
    {
        var info = await FfprobeClient.ProbeAsync(path, allowAudioOnly: true, ct);
        var (plan, output) = Prepare(info, preset, _hdr, _outputFolder);
        QueueWatch.MarkOwn(output);
        var result = await new EncodeRunner().ConvertAsync(info, plan, output, progress, ct);
        return result.OutputPath;
    }

    internal Task StartAsync()
    {
        if (_run is not null) return _run;
        _run = RunAsync();
        return _run;
    }

    private async Task RunAsync()
    {
        BtnStart.IsEnabled = false;
        foreach (var (choice, _) in _rows.Values) choice.IsEnabled = false;
        TxtSummary.Text = "";
        var cts = new CancellationTokenSource();
        _cts = cts;
        var total = Math.Max(1, Queue.Items.Count);
        var index = 0;
        var runner = Kosturucu ?? RunOneAsync;
        try
        {
            var summary = await Queue.RunAsync(async (item, preset, ct) =>
            {
                var at = index++;
                var progress = new Progress<EncodeProgress>(p =>
                {
                    Progress.Value = (at + Math.Clamp(p.Fraction, 0, 1)) / total;
                    TxtStage.Text = MainWindow.LocalizeStageIn(p.Stage, _language);
                });
                return await runner(item.Path, preset, progress, ct);
            }, OnChanged, cts.Token);

            Progress.Value = 1;
            TxtStage.Text = "";
            TxtSummary.Text = Say("main.convert-queue.summary", summary.Done, summary.Failed, summary.Cancelled);
            BtnReveal.IsVisible = Queue.Items.Any(item => item.Output is not null);
        }
        finally
        {
            _cts = null;
            cts.Dispose();
        }
    }

    private void OnChanged(ConversionQueueItem item)
    {
        if (!_rows.TryGetValue(item, out var row)) return;
        row.State.Text = StateText(item.State);
        ToolTip.SetTip(row.State, item.State == ConversionItemState.Failed ? item.Error : null);
        if (item.State == ConversionItemState.Failed && this.TryFindResource("StatusError", out var found) && found is Avalonia.Styling.ControlTheme error)
            row.State.Theme = error;
    }

    private string StateText(ConversionItemState state) => state switch
    {
        ConversionItemState.Running => Say("main.convert-queue.running"),
        ConversionItemState.Done => Say("main.convert-queue.done"),
        ConversionItemState.Failed => Say("main.convert-queue.failed"),
        ConversionItemState.Cancelled => Say("main.convert-queue.cancelled"),
        _ => Say("main.convert-queue.waiting")
    };

    private Grid Row(ConversionQueueItem item)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = Token("SpaceXs") };
        var name = new TextBlock
        {
            Text = Path.GetFileName(item.Path),
            FontSize = Token("FontSizeSm"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            FlowDirection = FlowDirection.LeftToRight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(name, item.Path);
        row.Children.Add(name);

        var choices = new List<string> { Say("main.convert-queue.shared") };
        choices.AddRange(_presets.Select(p => p.Name ?? p.Id));
        var choice = new ComboBox
        {
            ItemsSource = choices,
            SelectedIndex = 0,
            FontSize = Token("FontSizeSm"),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = _presets.Count > 0
        };
        var label = Say("main.convert-queue.setting");
        AutomationProperties.SetName(choice, label);
        ToolTip.SetTip(choice, label);
        choice.SelectionChanged += (_, _) =>
        {
            var at = choice.SelectedIndex - 1;
            item.Own = at >= 0 && at < _presets.Count ? _presets[at] : null;
        };
        Grid.SetColumn(choice, 1);
        row.Children.Add(choice);

        var state = new TextBlock
        {
            Text = StateText(item.State),
            FontSize = Token("FontSizeSm"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(state, 2);
        row.Children.Add(state);

        _rows[item] = (choice, state);
        return row;
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        if (_cts is not null) _cts.Cancel();
        else Close();
    }

    private void OnReveal(object? sender, RoutedEventArgs e)
    {
        var last = Queue.Items.LastOrDefault(item => item.Output is not null)?.Output;
        if (last is null) return;
        try
        {
            Actions.Reveal(last);
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            TxtSummary.Text = Say("main.error.folder");
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnClosing(e);
    }

    private void OnShellPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private double Token(string key) => this.FindResource(key) is double value ? value : 0;

    private string Say(string key)
        => LanguageCatalog.Title(Strings.GetIn(_language, key), _language);

    private string Say(string key, params object?[] args)
        => LanguageCatalog.Title(Strings.GetIn(_language, key, args), _language);
}
