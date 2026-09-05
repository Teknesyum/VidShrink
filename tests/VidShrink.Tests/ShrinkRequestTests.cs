using System.Collections.Concurrent;
using VidShrink.Core;

namespace VidShrink.Tests;

public static class ShrinkRequestTests
{

public sealed class ResolverTests : IDisposable
{
    private readonly string _root =
        Path.Combine(TestPaths.OutputRoot, "shrink-request", Guid.NewGuid().ToString("n"));

    public ResolverTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private string Write(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "veri");
        return path;
    }

    public static IEnumerable<object[]> ValidFormats()
    {
        yield return new object[] { "SimpleUnquotedPath" };
        yield return new object[] { "QuotedPathWithSpaces" };
        yield return new object[] { "UnquotedPathBrokenIntoTwoPieces" };
        yield return new object[] { "UnquotedPathBrokenIntoThreePieces" };
        yield return new object[] { "GigabyteTarget" };
    }

    [Theory]
    [MemberData(nameof(ValidFormats))]
    public void K1_five_argument_formats_resolve_both_target_and_path(string shape)
    {
        var quickTargets = ShellIntegration.QuickShrinkTargetsMegabytes;

        (IReadOnlyList<string> args, int expectedTarget, string expectedPath) Build()
        {
            return shape switch
            {
                "SimpleUnquotedPath" => (new[] { ShellIntegration.ShrinkFlag, "500", Write("basit.mp4") }, 500, Write("basit.mp4")),
                "QuotedPathWithSpaces" => (new[] { ShellIntegration.ShrinkFlag, "500", "\"" + Write("bosluklu a b.mp4") + "\"" }, 500, ""),
                "UnquotedPathBrokenIntoTwoPieces" => (BrokenArgs("kirik dosya.mp4", "500"), 500, ""),
                "UnquotedPathBrokenIntoThreePieces" => (BrokenArgs("tatil cekimi 2160p.mp4", "100"), 100, ""),
                "GigabyteTarget" => (new[] { ShellIntegration.ShrinkFlag, "2048", Write("buyuk.mp4") }, 2048, Write("buyuk.mp4")),
                _ => throw new InvalidOperationException(shape),
            };
        }

        string[] BrokenArgs(string fileName, string target)
        {
            var file = Write(fileName);
            var pieces = file.Split(' ');
            var all = new List<string> { ShellIntegration.ShrinkFlag, target };
            all.AddRange(pieces);
            return all.ToArray();
        }

        var (args, expectedTarget, _) = Build();
        var result = ShrinkRequestResolver.Resolve(args, quickTargets);

        Assert.True(result.Ok, $"{shape}: {result.Problem}");
        Assert.Equal(expectedTarget, result.Request!.TargetMegabytes);
        Assert.True(File.Exists(result.Request.Path), $"{shape}: cozulen yol yok -> {result.Request.Path}");
    }

    public static IEnumerable<object[]> InvalidFormats()
    {
        yield return new object[] { "MissingTarget", ShrinkArgumentProblem.NoTarget };
        yield return new object[] { "TargetNotANumber", ShrinkArgumentProblem.TargetNotANumber };
        yield return new object[] { "NegativeTarget", ShrinkArgumentProblem.TargetNotPositive };
        yield return new object[] { "ZeroTarget", ShrinkArgumentProblem.TargetNotPositive };
        yield return new object[] { "TargetOutsideQuickList", ShrinkArgumentProblem.TargetNotInQuickList };
        yield return new object[] { "MissingPath", ShrinkArgumentProblem.NoPath };
    }

    [Theory]
    [MemberData(nameof(InvalidFormats))]
    public void K2_invalid_targets_are_rejected_with_a_named_problem(string shape, ShrinkArgumentProblem expected)
    {
        var quickTargets = ShellIntegration.QuickShrinkTargetsMegabytes;
        var file = Write("gecerli.mp4");

        var args = shape switch
        {
            "MissingTarget" => new[] { ShellIntegration.ShrinkFlag },
            "TargetNotANumber" => new[] { ShellIntegration.ShrinkFlag, "abuk", file },
            "NegativeTarget" => new[] { ShellIntegration.ShrinkFlag, "-5", file },
            "ZeroTarget" => new[] { ShellIntegration.ShrinkFlag, "0", file },
            "TargetOutsideQuickList" => new[] { ShellIntegration.ShrinkFlag, "777", file },
            "MissingPath" => new[] { ShellIntegration.ShrinkFlag, "500" },
            _ => throw new InvalidOperationException(shape),
        };

        var result = ShrinkRequestResolver.Resolve(args, quickTargets);

        Assert.False(result.Ok, $"{shape}: sessizce kabul edildi.");
        Assert.Equal(expected, result.Problem);
    }

    [Fact]
    public void K3_before_after_resolve_startup_path_is_unchanged_by_this_contract()
    {
        var single = Write("kayit.mp4");
        var quotedTarget = Write("baska.mp4");
        var quoted = "\"" + quotedTarget + "\"";
        var brokenFile = Write("tatil cekimi 2160p.mp4");
        var broken = brokenFile.Split(' ');
        var missing = Path.Combine(_root, "yok.mp4");

        string?[] Before() => new[]
        {
            ShellIntegration.ResolveStartupPath(new[] { single }),
            ShellIntegration.ResolveStartupPath(new[] { quoted }),
            ShellIntegration.ResolveStartupPath(broken),
            ShellIntegration.ResolveStartupPath(new[] { missing }),
        };

        var before = Before();
        _ = ShrinkRequestResolver.Resolve(new[] { ShellIntegration.ShrinkFlag, "500", single }, ShellIntegration.QuickShrinkTargetsMegabytes);
        var after = Before();

        Assert.Equal(before, after);
        Assert.Equal(single, before[0]);
        Assert.Equal(quotedTarget, before[1]);
        Assert.Equal(brokenFile, before[2]);
        Assert.Null(before[3]);
    }
}

public sealed class QueueTests
{
    private static string Channel() => Guid.NewGuid().ToString("n");

    [Fact]
    public void First_instance_becomes_owner_second_does_not()
    {
        var channel = Channel();
        using var first = new ShrinkRequestQueue(channel);
        using var second = new ShrinkRequestQueue(channel);

        Assert.True(first.IsOwner);
        Assert.False(second.IsOwner);
    }

    [Fact]
    public async Task K4_second_instance_hands_off_and_never_runs_concurrently_with_the_owner()
    {
        var channel = Channel();
        using var owner = new ShrinkRequestQueue(channel);
        Assert.True(owner.IsOwner);

        var concurrentCount = 0;
        var maxConcurrent = 0;
        var handledPaths = new ConcurrentBag<string>();
        var done = new CountdownEvent(5);

        owner.StartOwning(request =>
        {
            var now = Interlocked.Increment(ref concurrentCount);
            InterlockedMax(ref maxConcurrent, now);
            Thread.Sleep(60);
            handledPaths.Add(request.Path);
            Interlocked.Decrement(ref concurrentCount);
            done.Signal();
        });

        var submitters = Enumerable.Range(0, 5).Select(i => Task.Run(() =>
        {
            using var instance = new ShrinkRequestQueue(channel);
            Assert.False(instance.IsOwner);
            return instance.Submit(new ShrinkRequest(500, $"es-zamanli-{i}.mp4"));
        })).ToArray();

        var results = await Task.WhenAll(submitters);
        Assert.All(results, ok => Assert.True(ok, "Istek sahibe ulasamadi."));

        Assert.True(done.Wait(TimeSpan.FromSeconds(15)), "Bes istek zamaninda islenmedi.");
        Assert.Equal(1, maxConcurrent);
        Assert.Equal(5, handledPaths.Distinct().Count());
    }

    private static void InterlockedMax(ref int location, int value)
    {
        int initial;
        do
        {
            initial = location;
            if (value <= initial) return;
        } while (Interlocked.CompareExchange(ref location, value, initial) != initial);
    }

    [Fact]
    public async Task K5_five_files_are_all_processed_exactly_once_no_loss()
    {
        var channel = Channel();
        using var owner = new ShrinkRequestQueue(channel);
        Assert.True(owner.IsOwner);

        var processed = new ConcurrentQueue<string>();
        var done = new CountdownEvent(5);
        owner.StartOwning(request =>
        {
            processed.Enqueue(request.Path);
            done.Signal();
        });

        var expected = Enumerable.Range(0, 5).Select(i => $"dosya-{i}.mp4").ToArray();
        var submitters = expected.Select(name => Task.Run(() =>
        {
            using var instance = new ShrinkRequestQueue(channel);
            return instance.Submit(new ShrinkRequest(500, name));
        })).ToArray();

        var results = await Task.WhenAll(submitters);
        Assert.All(results, ok => Assert.True(ok, "Istek sahibe ulasamadi, dosya kayboldu."));
        Assert.True(done.Wait(TimeSpan.FromSeconds(15)), "Bes dosya zamaninda islenmedi.");

        var seen = processed.ToArray();
        Assert.Equal(5, seen.Length);
        Assert.Equal(expected.OrderBy(x => x), seen.OrderBy(x => x));
        Assert.Equal(seen.Length, seen.Distinct().Count());
    }
}

}
