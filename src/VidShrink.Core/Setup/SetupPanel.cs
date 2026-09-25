using System.Globalization;

namespace VidShrink.Core.Setup;

public enum SetupPanelButton
{
    Install,
    Installing,
    OpenApp,
    OpenLog,
    Retry,
    Close
}

public enum SetupPanelPhase
{
    Ready,
    Running,
    Done,
    Failed
}

public enum SetupStageState
{
    Pending,
    Running,
    Done,
    Failed,
    Skipped
}

public sealed record SetupStage(string Name, string Detail, int Start);

public sealed record SetupPanelSnapshot(double Bar, double Percent, string Step, InstallState State, IReadOnlyList<string> Lines);

public sealed record SetupPanelChoice(IReadOnlyList<SetupPanelButton> Buttons, SetupPanelButton? Primary, SetupPanelButton? Focus)
{
    public static SetupPanelChoice None { get; } = new(Array.Empty<SetupPanelButton>(), null, null);
}

/// <summary>
/// Panelin iş parçacığı ile kurulumun iş parçacığı arasındaki tek köprü. İş tarafı
/// yalnız <see cref="Step"/>, <see cref="Log"/>, <see cref="Complete"/> ve
/// <see cref="Fail"/> çağırır; pencere her karede <see cref="Advance"/> ile çubuğu
/// ilerletip bir kopya alır. Tavan kuralı <see cref="InstallProgress"/>'in kendisidir.
/// </summary>
public sealed class SetupPanelProgress
{
    public const int VisibleLines = 9;

    public const int KeptLines = 300;

    private readonly object gate = new();
    private readonly List<string> lines = new();
    private readonly InstallProgress bar = new();
    private readonly Action<string> sink;
    private readonly Func<DateTime> clock;

    public SetupPanelProgress(Action<string>? sink = null, Func<DateTime>? clock = null)
    {
        this.sink = sink ?? (_ => { });
        this.clock = clock ?? (() => DateTime.Now);
    }

    public double Percent => bar.Percent;

    public double Ceiling => bar.Ceiling;

    public InstallState State => bar.State;

    /// <summary>Yüzde geri gitmez, tavan yüzdenin altına inmez; iş bittikten sonra yok sayılır.</summary>
    public void Step(int percent, int ceiling, string text)
    {
        if (bar.State != InstallState.Running) return;
        bar.Step(percent, ceiling, text);
    }

    public void Log(string message)
    {
        var line = FormatLine(clock(), message);
        lock (gate)
        {
            lines.Add(line);
            if (lines.Count > KeptLines) lines.RemoveRange(0, lines.Count - KeptLines);
            try
            {
                sink(line);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public void Complete(string text) => bar.Finish(true, text);

    public void Fail(string text)
    {
        Log(text);
        bar.Finish(false, text);
    }

    public SetupPanelSnapshot Advance(TimeSpan elapsed)
    {
        bar.Advance(elapsed);
        return Read();
    }

    public SetupPanelSnapshot Read()
    {
        var state = bar.State;
        var history = bar.History;
        var step = history.Count == 0 ? "" : history[^1];
        lock (gate) return new SetupPanelSnapshot(bar.Bar, bar.Percent, step, state, Tail(lines, VisibleLines));
    }

    internal int KeptCount
    {
        get
        {
            lock (gate) return lines.Count;
        }
    }

    public static string FormatLine(DateTime at, string message) =>
        at.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + Flatten(message);

    public static IReadOnlyList<string> Tail(IReadOnlyList<string> all, int count) =>
        all.Skip(Math.Max(0, all.Count - count)).ToArray();

    private static string Flatten(string message) =>
        message.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ');
}

/// <summary>
/// Panelin beş adımı ve motor yüzdesinin hangi adıma düştüğü. Sınırlar
/// <see cref="SetupRunner"/>'ın adım çağrılarıyla aynıdır: indirme ve araçlar 0-62,
/// sha256 62-70, yerleştirme 70-86, kabuk kaydı 86-93, kısayol 93-100.
/// </summary>
public static class SetupPanelStages
{
    public const int Shell = 3;

    public static IReadOnlyList<int> Starts { get; } = new[] { 0, 62, 70, 86, 93 };

    public static IReadOnlyList<SetupStage> All => new[]
    {
        new SetupStage(SetupText.Get("setup.stage.download"), SetupText.Get("setup.stage.download.detail"), Starts[0]),
        new SetupStage(SetupText.Get("setup.stage.verify"), SetupText.Get("setup.stage.verify.detail"), Starts[1]),
        new SetupStage(SetupText.Get("setup.stage.place"), SetupText.Get("setup.stage.place.detail"), Starts[2]),
        new SetupStage(SetupText.Get("setup.stage.shell"), SetupText.Get("setup.stage.shell.detail"), Starts[3]),
        new SetupStage(SetupText.Get("setup.stage.shortcut"), SetupText.Get("setup.stage.shortcut.detail"), Starts[4])
    };

    public static int IndexOf(double percent)
    {
        var index = 0;
        for (var i = 0; i < Starts.Count; i++)
        {
            if (percent >= Starts[i]) index = i;
        }
        return index;
    }

    public static bool Skipped(int index, bool skipShortcuts) => skipShortcuts && index >= Shell;

    /// <summary>
    /// Hazırda hepsi bekler; sürerken eriştiği adımdan öncekiler biter, o adım sürer; hatada
    /// o adım hatalıdır; bitişte hepsi biter. Kısayolsuz kurulumda son iki adım atlanır.
    /// </summary>
    public static IReadOnlyList<SetupStageState> States(SetupPanelPhase phase, double percent, bool skipShortcuts)
    {
        var at = IndexOf(percent);
        var states = new SetupStageState[Starts.Count];
        for (var i = 0; i < states.Length; i++)
        {
            states[i] = Skipped(i, skipShortcuts)
                ? SetupStageState.Skipped
                : phase switch
                {
                    SetupPanelPhase.Ready => SetupStageState.Pending,
                    SetupPanelPhase.Done => SetupStageState.Done,
                    _ when i < at => SetupStageState.Done,
                    _ when i > at => SetupStageState.Pending,
                    SetupPanelPhase.Failed => SetupStageState.Failed,
                    _ => SetupStageState.Running
                };
        }
        return states;
    }

    public static SetupPanelPhase PhaseOf(InstallState state) => state switch
    {
        InstallState.Done => SetupPanelPhase.Done,
        InstallState.Failed => SetupPanelPhase.Failed,
        _ => SetupPanelPhase.Running
    };
}

public static class SetupPanelLocation
{
    /// <summary>
    /// Seçilen klasörün adı VidShrink değilse altına VidShrink eklenir. Sonuç
    /// <c>%LOCALAPPDATA%\Programs</c> altında değilse <c>null</c>.
    /// </summary>
    public static string? Resolve(string? picked, string localAppData)
    {
        if (string.IsNullOrWhiteSpace(picked)) return null;
        var full = Path.GetFullPath(picked).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = string.Equals(Path.GetFileName(full), "VidShrink", StringComparison.OrdinalIgnoreCase)
            ? full
            : Path.Combine(full, "VidShrink");
        return SetupRunner.UnderPrograms(root, localAppData) ? root : null;
    }

    public static string ProgramsOf(string localAppData) => Path.Combine(localAppData, "Programs");
}

public static class SetupPanelChoices
{
    /// <summary>
    /// Hazırda "Kur"; sürerken tek pasif "Kuruluyor"; başarıda "Kapat" + birincil "Programı Aç"
    /// (prova ya da açılacak yol yoksa yalnız "Kapat"); hatada "Günlüğü Aç" + birincil "Yeniden Dene".
    /// </summary>
    public static SetupPanelChoice For(SetupPanelPhase phase, bool rehearsal, bool canLaunch) => phase switch
    {
        SetupPanelPhase.Ready => new(new[] { SetupPanelButton.Install }, SetupPanelButton.Install, SetupPanelButton.Install),
        SetupPanelPhase.Running => new(new[] { SetupPanelButton.Installing }, SetupPanelButton.Installing, null),
        SetupPanelPhase.Done when !rehearsal && canLaunch => new(new[] { SetupPanelButton.Close, SetupPanelButton.OpenApp }, SetupPanelButton.OpenApp, SetupPanelButton.OpenApp),
        SetupPanelPhase.Done => new(new[] { SetupPanelButton.Close }, SetupPanelButton.Close, SetupPanelButton.Close),
        SetupPanelPhase.Failed => new(new[] { SetupPanelButton.OpenLog, SetupPanelButton.Retry }, SetupPanelButton.Retry, SetupPanelButton.Retry),
        _ => SetupPanelChoice.None
    };

    public static bool Enabled(SetupPanelButton button) => button != SetupPanelButton.Installing;

    public static bool CanClose(SetupPanelPhase phase) => phase != SetupPanelPhase.Running;

    public static bool CanChangeLocation(SetupPanelPhase phase) => phase is SetupPanelPhase.Ready or SetupPanelPhase.Failed;
}

public static class SetupLaunch
{
    public const string RehearsalVariable = "VIDSHRINK_SETUP_PROVA";

    public const string LogFileName = "kurulum.log";

    /// <summary>
    /// Seçeneksiz açılış panel. <c>--console</c> her zaman konsol; <c>--panel</c> kaldırma ve
    /// yardım dışında verilen seçeneklerle paneli açar. Başka her bayrak bugünkü konsoldur.
    /// </summary>
    public static bool UsePanel(IReadOnlyList<string> args)
    {
        if (args.Count == 0) return true;
        var flags = args.Select(a => a.ToLowerInvariant()).ToArray();
        if (flags.Contains("--console")) return false;
        if (!flags.Contains("--panel")) return false;
        return !flags.Any(f => f is "--uninstall" or "--help" or "-h" or "/?");
    }

    public static bool RehearsalRequested(string? value) => !string.IsNullOrWhiteSpace(value);

    public static SetupOptions ForPanel(SetupOptions options) => options with { NoLaunch = true };

    /// <summary>
    /// Prova: geçici köke kurar, kısayol/menü/ilişkilendirme yazmaz, programı açmaz. Kayıt
    /// kökü varsayılandan ayrılır ki Windows 11 kabuk paketine de dokunulmasın.
    /// </summary>
    public static SetupOptions Rehearsal(SetupOptions options, string scratch) => options with
    {
        LocalAppData = scratch,
        InstallRoot = Path.Combine(scratch, "Programs", "VidShrink"),
        ClassesRoot = @"Software\VidShrink-Prova\Classes",
        SkipShortcuts = true,
        NoLaunch = true,
        ShortcutDirectory = null
    };

    public static string LogPath(string localAppData) => Path.Combine(localAppData, "VidShrink", LogFileName);
}
