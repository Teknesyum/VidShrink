using System;
using System.Collections.Generic;
using Avalonia;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// Kabuk menusunden gelen bir kucultme istegi ve onu cozerken cikan gerekce.
/// <see cref="Request"/> doluysa istek tuketilebilir; <see cref="Problem"/> doluysa
/// kullaniciya gerekce gosterilir. <see cref="FallbackPath"/> "Uygulamada ac" dugmesinin
/// ana pencereye tasiyacagi yoldur ve gerekce halinde de dolabilir.
/// </summary>
internal sealed record ShellShrinkStartup(
    ShrinkRequest? Request,
    ShrinkArgumentProblem? Problem,
    string? FallbackPath)
{
    /// <summary>
    /// Argumanlarda <see cref="ShellIntegration.ShrinkFlag"/> yoksa <c>null</c> doner ve
    /// uygulama bugunku gibi ana pencereyle acilir. Bayrak varsa istek her halukarda
    /// tuketilir: cozulurse kodlanir, cozulmezse gerekcesiyle gosterilir.
    /// </summary>
    public static ShellShrinkStartup? From(IReadOnlyList<string>? args)
    {
        if (args is null) return null;

        var flagged = false;
        for (var i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], ShellIntegration.ShrinkFlag, StringComparison.Ordinal)) continue;
            flagged = true;
            break;
        }

        if (!flagged) return null;

        var resolved = ShrinkRequestResolver.Resolve(args, ShellIntegration.QuickShrinkTargetsMegabytes);
        return new ShellShrinkStartup(resolved.Request, resolved.Problem, ShellIntegration.ResolveStartupPath(args));
    }
}

internal static class Program
{
    /// <summary>Kabuk isteklerinin toplandigi tek kuyrugun kanal adi.</summary>
    internal const string QueueChannel = "kabuk-kucult";

    [STAThread]
    public static void Main(string[] args)
    {
        var startup = ShellShrinkStartup.From(args);
        if (startup is null)
        {
            Build(ShellIntegration.ResolveStartupPath(args)).StartWithClassicDesktopLifetime(args);
            return;
        }

        var queue = new ShrinkRequestQueue(QueueChannel);
        if (!OwnsQueue(queue))
        {
            var handed = startup.Request is not null && queue.Submit(startup.Request);
            queue.Dispose();
            if (handed) return;
            BuildShrink(startup, null).StartWithClassicDesktopLifetime(args);
            return;
        }

        BuildShrink(startup, queue).StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Bu surec kuyrugun sahibi mi. Ayri durmasinin sebebi olculebilir olmasi: bu kapi
    /// her zaman <c>true</c> derse her surec kendi kodlamasini baslatir.
    /// </summary>
    internal static bool OwnsQueue(ShrinkRequestQueue queue) => queue.IsOwner;

    public static AppBuilder BuildAvaloniaApp() => Build(null);

    private static AppBuilder Build(string? startupFile)
        => AppBuilder.Configure(() => new App(startupFile))
            .UsePlatformDetect()
            .LogToTrace();

    private static AppBuilder BuildShrink(ShellShrinkStartup startup, ShrinkRequestQueue? queue)
        => AppBuilder.Configure(() => new App(startup, queue))
            .UsePlatformDetect()
            .LogToTrace();
}
