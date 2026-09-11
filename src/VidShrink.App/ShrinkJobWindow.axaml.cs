using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

/// <summary>Ilerleme penceresinin gorunur durumu; olcunun sordugu tek alan.</summary>
internal enum ShrinkJobState
{
    Bekliyor,
    Kosuyor,
    Bitti,
    Gerekce,
    Hata
}

/// <summary>
/// Bes ret gerekcesini kullanicinin gordugu cumlenin anahtarina ceviren tek kapi. Bes uye
/// de burada adiyla gecer: bu switch olmadan gerekceler uretilir ama hicbiri ekrana ulasmaz.
/// </summary>
internal static class ShrinkProblemText
{
    internal const string NoTarget = "main.shrink-job.reason.no-target";
    internal const string TargetNotANumber = "main.shrink-job.reason.not-a-number";
    internal const string TargetNotPositive = "main.shrink-job.reason.not-positive";
    internal const string TargetNotInQuickList = "main.shrink-job.reason.not-in-quick-list";
    internal const string NoPath = "main.shrink-job.reason.no-path";

    internal static IReadOnlyList<ShrinkArgumentProblem> All { get; } = new[]
    {
        ShrinkArgumentProblem.NoTarget,
        ShrinkArgumentProblem.TargetNotANumber,
        ShrinkArgumentProblem.TargetNotPositive,
        ShrinkArgumentProblem.TargetNotInQuickList,
        ShrinkArgumentProblem.NoPath
    };

    internal static string Key(ShrinkArgumentProblem problem) => problem switch
    {
        ShrinkArgumentProblem.NoTarget => NoTarget,
        ShrinkArgumentProblem.TargetNotANumber => TargetNotANumber,
        ShrinkArgumentProblem.TargetNotPositive => TargetNotPositive,
        ShrinkArgumentProblem.TargetNotInQuickList => TargetNotInQuickList,
        ShrinkArgumentProblem.NoPath => NoPath,
        _ => throw new ArgumentOutOfRangeException(nameof(problem), problem, null)
    };

    internal static string QuickList()
        => string.Join(", ", ShellIntegration.QuickShrinkTargetsMegabytes.Select(ShellIntegration.FormatQuickShrinkLabel));
}

/// <summary>
/// Kabuk menusunden gelen kucultme isteginin tuketicisi. Ana pencere acilmaz: gorev
/// cubugunda duran bu kucuk pencere kodlamayi kosar, <see cref="EncodeProgress"/>
/// degerlerini gosterir ve is bitince kendini kapatir. Istek cozulemediyse pencere kalir,
/// tek satir gerekceyi ve ana pencereye gecis dugmesini gosterir.
/// </summary>
public partial class ShrinkJobWindow : Window
{
    internal static readonly TimeSpan Linger = TimeSpan.FromSeconds(6);

    private readonly ShellShrinkStartup _startup;
    private readonly ShrinkRequestQueue? _queue;
    private readonly string _language;
    private readonly Queue<ShrinkRequest> _pending = new();
    private readonly List<string> _outputs = new();
    private CancellationTokenSource? _cts;
    private DispatcherTimer? _closeTimer;
    private bool _busy;
    private bool _started;
    private int _accepted;
    private int _finished;

    public ShrinkJobWindow() : this(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null)
    {
    }

    internal ShrinkJobWindow(ShellShrinkStartup startup, ShrinkRequestQueue? queue)
    {
        _startup = startup;
        _queue = queue;

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

        FlowDirection = Strings.IsRightToLeftLanguage(_language)
            ? Avalonia.Media.FlowDirection.RightToLeft
            : Avalonia.Media.FlowDirection.LeftToRight;

        if (OperatingSystem.IsMacOS()) WindowDecorations = WindowDecorations.Full;
        JobShell.PointerPressed += OnShellPointerPressed;

        BtnClose.Click += (_, _) => Close();
        BtnOpenInApp.Click += OnOpenInApp;
        BtnReveal.Click += OnReveal;

        TxtHeadline.Text = Say("main.shrink-job.waiting");
        TxtTarget.Text = "";
        TxtStage.Text = "-";
        TxtRemaining.Text = "-";
        TxtMessage.Text = "";
        TxtNotice.Text = "";
    }

    private void OnShellPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    /// <summary>
    /// Bu pencerenin metinlerini urettigi dil. Kurulusta bir kez cozulur ve bir daha
    /// degismez: <see cref="Say(string)"/> surec genelindeki <see cref="Strings.Language"/>
    /// degerini okumaz, yoksa baska bir is parcaciginda kosan bir olcu dili degistirdiginde
    /// pencerenin metni koşudan koşuya kayar.
    /// </summary>
    internal string Language => _language;

    /// <summary>Pencerenin gorunur durumu; olcu buna bakar, piksele degil.</summary>
    internal ShrinkJobState State { get; private set; } = ShrinkJobState.Bekliyor;

    /// <summary>Bu pencerenin teslim ettigi cikti yollari, kabul sirasiyla.</summary>
    internal IReadOnlyList<string> Outputs => _outputs;

    /// <summary>Kuyruga girmis istek sayisi; ikinci surecin boruyla verdikleri dahil.</summary>
    internal int AcceptedCount => _accepted;

    internal string MessageText => TxtMessage.Text ?? "";

    /// <summary>Bulunamayan yollar icin basilan uyari satiri; bos ise satir gorunmez.</summary>
    internal string NoticeText => TxtNotice.Text ?? "";

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Begin();
    }

    /// <summary>
    /// Isteklerin tuketildigi yer. Gerekce varsa pencere kalir; ayni argv'den gelen her
    /// istek kuyruga girer ve kuyrugun sahibiysek boru da acilir, ikinci surecin istegi
    /// buraya duser.
    /// </summary>
    internal void Begin()
    {
        if (_started) return;
        _started = true;

        ShowMissing();

        if (_startup.Problem is { } problem)
        {
            ShowProblem(problem);
            return;
        }

        foreach (var request in _startup.Items) Accept(request);

        if (_queue is null) return;
        try
        {
            _queue.StartOwning(incoming => Dispatcher.UIThread.Post(() => Accept(incoming)));
        }
        catch (InvalidOperationException) { }
    }

    /// <summary>
    /// Diskte bulunamayan argv parcalari. Kodlama satirinin ustunde ayri bir satirda durur:
    /// <c>TxtMessage</c> her istekte silindigi icin uyari orada kaybolurdu.
    /// </summary>
    private void ShowMissing()
    {
        if (_startup.Missing.Count == 0) return;
        TxtNotice.Text = Say("main.shrink-job.missing-paths",
            _startup.Missing.Count,
            string.Join(", ", _startup.Missing));
        TxtNotice.IsVisible = true;
    }

    private void Accept(ShrinkRequest request)
    {
        _accepted++;
        _pending.Enqueue(request);
        _ = PumpAsync();
    }

    private void ShowProblem(ShrinkArgumentProblem problem)
    {
        State = ShrinkJobState.Gerekce;
        TxtHeadline.Text = Say("main.shrink-job.rejected");
        TxtTarget.Text = "";
        RowFacts.IsVisible = false;
        Progress.IsVisible = false;
        TxtMessage.Text = problem == ShrinkArgumentProblem.TargetNotInQuickList
            ? Say(ShrinkProblemText.Key(problem), ShrinkProblemText.QuickList())
            : Say(ShrinkProblemText.Key(problem));
        BtnOpenInApp.IsVisible = true;
    }

    private async Task PumpAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            while (_pending.Count > 0)
            {
                var request = _pending.Dequeue();
                await RunOneAsync(request);
            }
        }
        finally
        {
            _busy = false;
        }

        if (_pending.Count == 0 && State == ShrinkJobState.Bitti) ArmClose();
    }

    private async Task RunOneAsync(ShrinkRequest request)
    {
        State = ShrinkJobState.Kosuyor;
        Progress.IsVisible = true;
        RowFacts.IsVisible = true;
        BtnReveal.IsVisible = false;
        Progress.Value = 0;
        TxtHeadline.Text = Path.GetFileName(request.Path);
        TxtTarget.Text = Say("main.shrink-job.target",
            ShellIntegration.FormatQuickShrinkLabel(request.TargetMegabytes),
            _finished + 1,
            _accepted);
        TxtMessage.Text = "";

        var cts = new CancellationTokenSource();
        _cts = cts;
        try
        {
            var info = await FfprobeClient.ProbeAsync(request.Path, cts.Token);
            var options = new PlanOptions { TargetMb = request.TargetMegabytes };
            var plan = PlanCalculator.Build(info, options);
            var output = UniqueOutputPath(request.Path);

            var progress = new Progress<EncodeProgress>(step =>
            {
                Progress.Value = step.Fraction;
                TxtStage.Text = step.Stage;
                TxtRemaining.Text = step.Remaining?.ToString(@"mm\:ss") ?? "-";
            });

            var result = await new EncodeRunner().RunAsync(
                info, plan, output, request.TargetMegabytes, progress, cts.Token, options.FillPolicy);

            if (result.Success)
            {
                _outputs.Add(result.OutputPath);
                State = ShrinkJobState.Bitti;
                Progress.Value = 1;
                TxtMessage.Text = result.OutputPath;
                BtnReveal.IsVisible = true;
            }
            else
            {
                State = ShrinkJobState.Hata;
                TxtMessage.Text = result.CeilingExceeded
                    ? Say("main.run.over-ceiling", request.TargetMegabytes, result.Attempts,
                        result.OutputMb.ToString("0.0", CultureInfo.InvariantCulture))
                    : Say("main.run.ended");
                BtnOpenInApp.IsVisible = true;
            }
        }
        catch (OperationCanceledException)
        {
            State = ShrinkJobState.Hata;
            TxtMessage.Text = Say("main.run.cancelled");
        }
        catch (Exception ex)
        {
            State = ShrinkJobState.Hata;
            TxtMessage.Text = ex.Message;
            BtnOpenInApp.IsVisible = true;
        }
        finally
        {
            _finished++;
            _cts = null;
            cts.Dispose();
        }
    }

    private void ArmClose()
    {
        if (_closeTimer is not null) return;
        _closeTimer = new DispatcherTimer { Interval = Linger };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer?.Stop();
            if (_pending.Count == 0 && !_busy) Close();
        };
        _closeTimer.Start();
    }

    private void OnOpenInApp(object? sender, RoutedEventArgs e)
    {
        var window = new MainWindow(_startup.FallbackPath ?? _startup.Request?.Path);
        window.Show();
        Close();
    }

    private void OnReveal(object? sender, RoutedEventArgs e)
    {
        var path = _outputs.Count > 0 ? _outputs[^1] : null;
        if (path is null) return;
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(Path.GetDirectoryName(path)!) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            TxtMessage.Text = Say("main.error.folder");
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _closeTimer?.Stop();
        _cts?.Cancel();
        _queue?.Dispose();
        base.OnClosing(e);
    }

    private static string UniqueOutputPath(string inputPath)
    {
        var dir = Path.GetDirectoryName(inputPath)!;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        if (name.EndsWith("_shrunk", StringComparison.OrdinalIgnoreCase)) name = name[..^"_shrunk".Length];
        var candidate = Path.Combine(dir, name + "_shrunk.mp4");
        for (var index = 2; File.Exists(candidate); index++)
            candidate = Path.Combine(dir, name + "_shrunk_" + index + ".mp4");
        return candidate;
    }

    private string Say(string key)
        => LanguageCatalog.Title(Strings.GetIn(_language, key), _language);

    private string Say(string key, params object?[] args)
        => LanguageCatalog.Title(Strings.GetIn(_language, key, args), _language);
}
