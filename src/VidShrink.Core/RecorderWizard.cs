namespace VidShrink.Core;

public enum RecorderWizardContent
{
    Game,
    Lesson,
    Meeting,
    General
}

public enum RecorderWizardAudio
{
    System,
    Microphone,
    Both,
    Silent
}

public enum RecorderWizardDestination
{
    Share,
    Archive
}

public sealed record RecorderWizardAnswers(
    RecorderWizardContent Content,
    RecorderWizardAudio Audio,
    RecorderWizardDestination Destination);

public readonly record struct RecorderWizardPlan(
    int MaxFps,
    double Quality,
    bool ShrinkLargeCapture,
    bool Microphone,
    bool SystemAudio);

public static class RecorderWizard
{
    public const int QuestionCount = 3;

    public const double ArchiveQuality = 18;

    public const double ShareQuality = 28;

    public const double ShareGameQuality = 26;

    public const double ShareLessonQuality = 30;

    public const int GameMaxFps = 60;

    public const int LessonMaxFps = 24;

    public const int DefaultMaxFps = 30;

    public const int ShareMaxHeight = 1080;

    public static RecorderWizardPlan Plan(RecorderWizardAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(answers);

        return new RecorderWizardPlan(
            MaxFps(answers.Content),
            Quality(answers),
            answers.Destination == RecorderWizardDestination.Share,
            answers.Audio is RecorderWizardAudio.Microphone or RecorderWizardAudio.Both,
            answers.Audio is RecorderWizardAudio.System or RecorderWizardAudio.Both);
    }

    public static int MaxFps(RecorderWizardContent content) => content switch
    {
        RecorderWizardContent.Game => GameMaxFps,
        RecorderWizardContent.Lesson => LessonMaxFps,
        _ => DefaultMaxFps
    };

    public static double Quality(RecorderWizardAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(answers);

        if (answers.Destination == RecorderWizardDestination.Archive) return ArchiveQuality;

        return answers.Content switch
        {
            RecorderWizardContent.Game => ShareGameQuality,
            RecorderWizardContent.Lesson => ShareLessonQuality,
            _ => ShareQuality
        };
    }

    /// <summary>
    /// "Oyun" cevabında kaydın hedef ekranı: oyun penceresinin en çok örtüştüğü ekran. Pencere
    /// bilinmiyorsa ya da hiçbir ekrana değmiyorsa birincil ekran. Liste boşsa 0.
    /// </summary>
    public static int GameScreen(IReadOnlyList<ScreenBounds> screens, RecorderRegion? window)
    {
        if (screens is null || screens.Count == 0) return 0;
        if (window is { Width: > 0, Height: > 0 } && screens.Any(s => Overlaps(s, window)))
            return RecorderArguments.ScreenOf(screens, window);
        return PrimaryScreen(screens);
    }

    /// <summary>
    /// Birincil ekran: masaüstü kökünü (0,0) içeren ekran. Windows'ta ve macOS'ta birincil ekranın
    /// sol üst köşesi köktür; kökü içeren yoksa listenin ilki.
    /// </summary>
    public static int PrimaryScreen(IReadOnlyList<ScreenBounds> screens)
    {
        if (screens is null || screens.Count == 0) return 0;
        var primary = screens.FirstOrDefault(s => s.X <= 0 && s.X + s.Width > 0 && s.Y <= 0 && s.Y + s.Height > 0);
        return (primary ?? screens[0]).Index;
    }

    private static bool Overlaps(ScreenBounds screen, RecorderRegion window)
        => Math.Min(window.X + window.Width, screen.X + screen.Width) > Math.Max(window.X, screen.X)
            && Math.Min(window.Y + window.Height, screen.Y + screen.Height) > Math.Max(window.Y, screen.Y);

    public static IReadOnlyList<RecorderAutoChoice> Shape(
        IReadOnlyList<RecorderAutoChoice> candidates,
        RecorderWizardAnswers answers,
        RecorderMachine machine)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(answers);

        var plan = Plan(answers);
        var shrink = plan.ShrinkLargeCapture && machine.CaptureHeight > ShareMaxHeight
            ? RecorderAutoPlan.Halved(machine.CaptureWidth, machine.CaptureHeight)
            : null;

        var shaped = new List<RecorderAutoChoice>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var fps = Math.Min(candidate.Fps, plan.MaxFps);
            var scale = candidate.Scale ?? shrink;
            if (shaped.Any(s => s.Fps == fps && Equals(s.Scale, scale))) continue;

            var notes = candidate.Notes
                .Select(n => n switch
                {
                    RecorderAutoNote.FpsFollowsRefreshRate when fps < candidate.Fps => RecorderAutoNote.FpsSteppedDown,
                    RecorderAutoNote.ResolutionKept when scale is not null => RecorderAutoNote.ResolutionHalved,
                    _ => n
                })
                .ToList();

            shaped.Add(candidate with { Fps = fps, Scale = scale, Quality = plan.Quality, Notes = notes });
        }

        return shaped;
    }
}
