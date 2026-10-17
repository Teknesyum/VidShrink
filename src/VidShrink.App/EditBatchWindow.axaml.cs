using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

/// <summary>
/// DZA-09: düzenleyicinin toplu teslim penceresi. Her satır bir proje dosyası; projeler sırayla, komut satırı
/// teslimiyle aynı yoldan teslim edilir (<see cref="EditBatch.ExportOneAsync"/>). Düşen proje sırayı durdurmaz,
/// nedeni satırın ipucundadır; sonda sayılar tek satırda.
/// </summary>
public partial class EditBatchWindow : Window
{
    private readonly string _language;
    private readonly Dictionary<EditBatchItem, TextBlock> _rows = new();
    private CancellationTokenSource? _cts;
    private Task? _run;

    public EditBatchWindow() : this(Array.Empty<string>())
    {
    }

    internal EditBatchWindow(IReadOnlyList<string> paths)
    {
        Batch = new EditBatch(paths);

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
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        Classes.Set("untracked", Strings.IsUntrackedLanguage(_language));

        if (OperatingSystem.IsMacOS()) WindowDecorations = WindowDecorations.Full;
        QueueShell.PointerPressed += OnShellPointerPressed;

        BtnAdd.Click += (_, _) => _ = AddClickedAsync();
        BtnStart.Click += (_, _) => _ = StartAsync();
        BtnCancel.Click += OnCancel;
        BtnReveal.Click += OnReveal;

        foreach (var item in Batch.Items) ItemList.Children.Add(Row(item));
        Refresh();
    }

    internal EditBatch Batch { get; }

    /// <summary>Bir projenin teslimi; testler sahtesini verir, yoksa gerçek ffmpeg koşar.</summary>
    internal Func<string, IProgress<EncodeProgress>, CancellationToken, Task<string>>? Kosturucu { get; set; }

    internal IQueueEndActions Actions { get; set; } = SystemQueueEndActions.Instance;

    /// <summary>SIK-P2: kuyruk koşarken tutulan uyku isteği; testler sahte katmanlı nöbet verir.</summary>
    internal SleepGuard Uyku { get; set; } = SleepGuard.Shared;

    /// <summary>Boşsa Ayarlar'daki kutu iş başlarken okunur.</summary>
    internal bool? UykuyuEngelle { get; set; }

    internal string StateTextFor(int index) => _rows[Batch.Items[index]].Text ?? "";

    internal string? ErrorTextFor(int index) => ToolTip.GetTip(_rows[Batch.Items[index]]) as string;

    internal void Add(IEnumerable<string> paths)
    {
        if (_run is not null) return;
        foreach (var item in Batch.Add(paths)) ItemList.Children.Add(Row(item));
        Refresh();
    }

    private async Task AddClickedAsync()
    {
        if (StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Say("editor.batch.add"),
            AllowMultiple = true,
            FileTypeFilter = new[] { new FilePickerFileType("VidShrink") { Patterns = new[] { "*" + EditProject.Extension } } }
        }).ConfigureAwait(true);
        Add(files.Select(file => file.TryGetLocalPath()).OfType<string>());
    }

    private void Refresh()
    {
        TxtHeadline.Text = Say("editor.batch.headline", Batch.Items.Count);
        BtnStart.IsEnabled = _run is null && Batch.Items.Any(item => item.State == ConversionItemState.Waiting);
    }

    private static async Task<string> RunOneAsync(string path, IProgress<EncodeProgress> progress, CancellationToken ct)
        => await EditBatch.ExportOneAsync(path, SourceStamp.Of,
            (job, output, token) => EditExportRunner.PrepareAsync(job, output, false, token),
            (plan, token) => EditExportRunner.RunAsync(plan, progress, token), ct).ConfigureAwait(true);

    internal Task StartAsync()
    {
        if (_run is not null) return _run;
        _run = RunAsync();
        return _run;
    }

    private async Task RunAsync()
    {
        BtnStart.IsEnabled = false;
        BtnAdd.IsEnabled = false;
        TxtSummary.Text = "";
        var cts = new CancellationTokenSource();
        _cts = cts;
        var total = Math.Max(1, Batch.Items.Count);
        var index = 0;
        var runner = Kosturucu ?? RunOneAsync;
        using var uyku = Uyku.HoldIf(UykuyuEngelle ?? SleepGuard.PreventSleepSetting());
        try
        {
            var summary = await Batch.RunAsync(async (item, ct) =>
            {
                var at = index++;
                var progress = new Progress<EncodeProgress>(p =>
                {
                    Progress.Value = (at + Math.Clamp(p.Fraction, 0, 1)) / total;
                    TxtStage.Text = MainWindow.LocalizeStageIn(p.Stage, _language);
                });
                return await runner(item.Path, progress, ct);
            }, OnChanged, cts.Token);

            Progress.Value = 1;
            TxtStage.Text = "";
            TxtSummary.Text = Say("main.convert-queue.summary", summary.Done, summary.Failed, summary.Cancelled);
            BtnReveal.IsVisible = Batch.Items.Any(item => item.Output is not null);
        }
        finally
        {
            _cts = null;
            cts.Dispose();
        }
    }

    private void OnChanged(EditBatchItem item)
    {
        if (!_rows.TryGetValue(item, out var state)) return;
        state.Text = StateText(item.State);
        ToolTip.SetTip(state, item.State == ConversionItemState.Failed ? ErrorText(item.Error) : null);
        if (item.State == ConversionItemState.Failed && this.TryFindResource("StatusError", out var found) && found is Avalonia.Styling.ControlTheme error)
            state.Theme = error;
    }

    internal string? ErrorText(Exception? error) => error switch
    {
        null => null,
        EditBatchException { Reason: EditBatchFailure.Unreadable } e => Say("editor.project.unreadable", e.Detail),
        EditBatchException { Reason: EditBatchFailure.Stale } => Say("editor.project.stale"),
        EditBatchException { Reason: EditBatchFailure.Empty } e => Say("editor.batch.empty", e.Detail),
        EditBatchException { Reason: EditBatchFailure.SameOutput } e => Say("editor.batch.same-output", e.Detail),
        EditBatchException { Reason: EditBatchFailure.ReverseLimit } e => Say("editor.batch.reverse-limit", e.Detail,
            Saat.Ekran(TimeSpan.FromSeconds(Math.Min(e.LimitSeconds, TimeSpan.MaxValue.TotalSeconds / 2)))),
        _ => error.Message
    };

    private string StateText(ConversionItemState state) => state switch
    {
        ConversionItemState.Running => Say("editor.batch.running"),
        ConversionItemState.Done => Say("main.convert-queue.done"),
        ConversionItemState.Failed => Say("main.convert-queue.failed"),
        ConversionItemState.Cancelled => Say("main.convert-queue.cancelled"),
        _ => Say("main.convert-queue.waiting")
    };

    private Grid Row(EditBatchItem item)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Token("SpaceXs") };
        var name = new SarilanMetin
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

        var state = new SarilanMetin
        {
            Text = StateText(item.State),
            FontSize = Token("FontSizeSm"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(state, 1);
        row.Children.Add(state);

        _rows[item] = state;
        return row;
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        if (_cts is not null) _cts.Cancel();
        else Close();
    }

    private void OnReveal(object? sender, RoutedEventArgs e)
    {
        var last = Batch.Items.LastOrDefault(item => item.Output is not null)?.Output;
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
