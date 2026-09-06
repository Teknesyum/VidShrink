using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// Kabuk menusunden gelen kucultme istekleri ve onlari cozerken cikan gerekce.
/// <see cref="Requests"/> doluysa istekler sirayla tuketilir; <see cref="Problem"/> doluysa
/// kullaniciya gerekce gosterilir. <see cref="FallbackPath"/> "Uygulamada ac" dugmesinin
/// ana pencereye tasiyacagi yoldur ve gerekce halinde de dolabilir.
/// </summary>
internal sealed record ShellShrinkStartup(
    IReadOnlyList<ShrinkRequest>? Requests,
    ShrinkArgumentProblem? Problem,
    string? FallbackPath)
{
    /// <summary>Kabuk argv'sinden cikan isteklerin tamami, gelis sirasiyla.</summary>
    internal IReadOnlyList<ShrinkRequest> Items => Requests ?? Array.Empty<ShrinkRequest>();

    /// <summary>Ilk istek; tek yollu kabuk komutunda tek olan istek.</summary>
    internal ShrinkRequest? Request => Items.Count > 0 ? Items[0] : null;

    /// <summary>
    /// Bayraktan sonra gelip diskte bulunamayan argv parcalari. Bos degilse kullaniciya
    /// pencerede tek satir bildirilir: bunlar eskiden sessizce dusuyordu.
    /// </summary>
    internal IReadOnlyList<string> Missing { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Argumanlarda <see cref="ShellIntegration.ShrinkFlag"/> yoksa <c>null</c> doner ve
    /// uygulama bugunku gibi ana pencereyle acilir. Bayrak varsa istek her halukarda
    /// tuketilir: cozulurse kodlanir, cozulmezse gerekcesiyle gosterilir. Bayraktan sonra
    /// birden cok var olan yol gelirse her yol icin ayri bir istek uretilir. Diskte
    /// bulunamayan parca istek uretmez ama <see cref="Missing"/> icinde durur ve pencerede
    /// tek satirla soylenir; sessizce dusen parca kalmaz.
    /// </summary>
    public static ShellShrinkStartup? From(IReadOnlyList<string>? args)
    {
        if (args is null) return null;

        var flagIndex = -1;
        for (var i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], ShellIntegration.ShrinkFlag, StringComparison.Ordinal)) continue;
            flagIndex = i;
            break;
        }

        if (flagIndex < 0) return null;

        var fallback = ShellIntegration.ResolveStartupPath(args);
        var resolved = ShrinkRequestResolver.Resolve(args, ShellIntegration.QuickShrinkTargetsMegabytes);
        if (resolved.Request is null)
            return new ShellShrinkStartup(null, resolved.Problem, fallback);

        var target = resolved.Request.TargetMegabytes;
        var scan = ScanPaths(args, flagIndex + 2);
        var requests = new List<ShrinkRequest>();
        foreach (var path in scan.Found)
            requests.Add(new ShrinkRequest(target, path));

        if (requests.Count == 0) requests.Add(resolved.Request);
        return new ShellShrinkStartup(requests, null, fallback) { Missing = scan.Missing };
    }

    /// <summary>Bulunan yollar ve diskte karsiligi olmayan argv parcalari.</summary>
    internal sealed record PathScan(IReadOnlyList<string> Found, IReadOnlyList<string> Missing);


    /// <summary>
    /// <paramref name="start"/> konumundan itibaren var olan butun dosya yollarini sirayla
    /// toplar. Her konumda en uzun birlesim once denenir — tirnagi kaybolmus bosluklu yol
    /// birden cok parca olarak geldigi icin — ve eslesen parcalar tuketilerek ilerlenir.
    /// </summary>
    internal static PathScan ScanPaths(IReadOnlyList<string> args, int start, Func<string, bool>? exists = null)
    {
        var probe = exists ?? Exists;
        var found = new List<string>();
        var missing = new List<string>();
        var i = start;
        while (i < args.Count)
        {
            var advanced = false;
            for (var end = args.Count - 1; end >= i; end--)
            {
                var candidate = Join(args, i, end);
                if (candidate.Length == 0 || !probe(candidate)) continue;
                found.Add(candidate);
                i = end + 1;
                advanced = true;
                break;
            }

            if (advanced) continue;
            var token = (args[i] ?? "").Trim().Trim('"');
            if (token.Length > 0) missing.Add(token);
            i++;
        }

        return new PathScan(found, missing);
    }

    private static string Join(IReadOnlyList<string> args, int start, int end)
    {
        var parts = new string[end - start + 1];
        for (var i = 0; i < parts.Length; i++) parts[i] = args[start + i] ?? "";
        return string.Join(' ', parts).Trim().Trim('"');
    }

    private static bool Exists(string path)
    {
        try { return File.Exists(path); }
        catch { return false; }
    }
}

internal static class Program
{
    /// <summary>Kabuk isteklerinin toplandigi tek kuyrugun kanal adi.</summary>
    internal const string QueueChannel = "kabuk-kucult";

    [STAThread]
    public static void Main(string[] args)
    {
        var startup = StartupFor(args);
        if (startup is null)
        {
            Build(ShellIntegration.ResolveStartupPath(args)).StartWithClassicDesktopLifetime(args);
            return;
        }

        var queue = new ShrinkRequestQueue(QueueChannel);
        if (!OwnsQueue(queue))
        {
            var remaining = Handoff(startup, request => queue.Submit(request));
            queue.Dispose();
            if (remaining is null) return;
            BuildShrink(remaining, null).StartWithClassicDesktopLifetime(args);
            return;
        }

        BuildShrink(startup, queue).StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Kabuk istegini tuketen kol. Ayri durmasinin sebebi olculebilir olmasi: burasi
    /// her zaman <c>null</c> derse uygulama eski <c>ResolveStartupPath</c> davranisina doner.
    /// </summary>
    internal static ShellShrinkStartup? StartupFor(IReadOnlyList<string>? args)
        => ShellShrinkStartup.From(args);

    /// <summary>
    /// Sahibi olmadigimiz kuyruga istekleri teslim eder. Bayrak istek basina: teslim edilen
    /// istek bu surecte <b>yeniden kodlanmaz</b>, geriye yalniz teslim edilemeyenler kalir.
    /// Hepsi teslim edildiyse <c>null</c> doner ve surec pencere acmadan cikar; gerekce
    /// tasiyan baslangicta (istek yok) baslangic oldugu gibi geri gelir.
    /// </summary>
    internal static ShellShrinkStartup? Handoff(ShellShrinkStartup startup, Func<ShrinkRequest, bool> submit)
    {
        var pending = startup.Items;
        if (pending.Count == 0) return startup;

        var leftovers = new List<ShrinkRequest>();
        foreach (var request in pending)
            if (!submit(request)) leftovers.Add(request);

        return leftovers.Count == 0 ? null : startup with { Requests = leftovers };
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
