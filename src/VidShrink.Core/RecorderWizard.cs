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
