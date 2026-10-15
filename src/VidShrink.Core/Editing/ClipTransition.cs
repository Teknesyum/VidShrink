namespace VidShrink.Core.Editing;

/// <summary>Iki klip arasindaki gecisin turu; teslimde <c>xfade</c>'in <c>transition</c> degerine iner.</summary>
public enum TransitionKind
{
    Dissolve,
    Wipe,
    Slide,
    Iris
}

/// <summary>
/// Onceki klipten bu klibe gecis: tur ve istenen sure (tick). Iki klip gecis suresi kadar ust uste biner, teslim o kadar
/// kisalir. Sure klip uzunluklarinin yarisina kirpilir (<see cref="EditTimeline.TransitionLength"/>). Onizlemede gosterilmez;
/// gecis yalniz Tam kipte kodlanir.
/// </summary>
public sealed record ClipTransition
{
    public const double MinSeconds = 0.1;
    public const double MaxSeconds = 5;
    public const double DefaultSeconds = 1;

    public ClipTransition(TransitionKind kind, long duration)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind), kind, "Bilinmeyen gecis turu");
        if (duration < EditTime.FromSeconds(MinSeconds) || duration > EditTime.FromSeconds(MaxSeconds))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Gecis suresi 0,1 ile 5 sn arasinda olmalidir");
        Kind = kind;
        Duration = duration;
    }

    public TransitionKind Kind { get; }

    public long Duration { get; }

    /// <summary>ffmpeg <c>xfade</c> adi: erime <c>fade</c>, silme <c>wipeleft</c>, kaydirma <c>slideleft</c>, iris <c>circleopen</c>.</summary>
    public static string XfadeName(TransitionKind kind) => kind switch
    {
        TransitionKind.Wipe => "wipeleft",
        TransitionKind.Slide => "slideleft",
        TransitionKind.Iris => "circleopen",
        _ => "fade"
    };

    public static string Key(TransitionKind kind) => kind switch
    {
        TransitionKind.Wipe => "wipe",
        TransitionKind.Slide => "slide",
        TransitionKind.Iris => "iris",
        _ => "dissolve"
    };

    public static TransitionKind? FromKey(string? key) => key switch
    {
        "dissolve" => TransitionKind.Dissolve,
        "wipe" => TransitionKind.Wipe,
        "slide" => TransitionKind.Slide,
        "iris" => TransitionKind.Iris,
        _ => null
    };
}
