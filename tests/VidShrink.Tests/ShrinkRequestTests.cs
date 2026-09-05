using System.Collections.Concurrent;
using System.IO.Pipes;
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
    public void K3_resolve_startup_path_still_resolves_every_known_shape()
    {
        var single = Write("kayit.mp4");
        var quotedTarget = Write("baska.mp4");
        var quoted = "\"" + quotedTarget + "\"";
        var brokenFile = Write("tatil cekimi 2160p.mp4");
        var broken = brokenFile.Split(' ');
        var missing = Path.Combine(_root, "yok.mp4");

        Assert.Equal(single, ShellIntegration.ResolveStartupPath(new[] { single }));
        Assert.Equal(quotedTarget, ShellIntegration.ResolveStartupPath(new[] { quoted }));
        Assert.Equal(brokenFile, ShellIntegration.ResolveStartupPath(broken));
        Assert.Null(ShellIntegration.ResolveStartupPath(new[] { missing }));
        Assert.Null(ShellIntegration.ResolveStartupPath(Array.Empty<string>()));
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

    [Fact]
    public void K5_single_sender_requests_are_handled_in_submission_order()
    {
        var channel = Channel();
        using var owner = new ShrinkRequestQueue(channel);
        Assert.True(owner.IsOwner);

        var processed = new ConcurrentQueue<string>();
        var gate = new ManualResetEventSlim(false);
        var done = new CountdownEvent(5);
        var firstSeen = 0;

        owner.StartOwning(request =>
        {
            processed.Enqueue(request.Path);
            if (Interlocked.Exchange(ref firstSeen, 1) == 0)
                gate.Wait(TimeSpan.FromSeconds(15));
            done.Signal();
        });

        using var sender = new ShrinkRequestQueue(channel);
        Assert.False(sender.IsOwner);

        var expected = Enumerable.Range(0, 5).Select(i => $"sira-{i}.mp4").ToArray();
        foreach (var name in expected)
            Assert.True(sender.Submit(new ShrinkRequest(500, name)), $"{name} onaylanmadi.");

        gate.Set();
        Assert.True(done.Wait(TimeSpan.FromSeconds(15)), "Bes istek zamaninda islenmedi.");

        Assert.Equal(expected, processed.ToArray());
    }

    [Fact]
    public void L1_second_StartOwning_is_refused_so_a_single_consumer_remains()
    {
        var channel = Channel();
        using var owner = new ShrinkRequestQueue(channel);
        Assert.True(owner.IsOwner);

        var firstSaw = new ConcurrentQueue<string>();
        var secondSaw = new ConcurrentQueue<string>();
        var done = new CountdownEvent(5);

        owner.StartOwning(request =>
        {
            firstSaw.Enqueue(request.Path);
            Thread.Sleep(100);
            done.Signal();
        });

        Assert.Throws<InvalidOperationException>(() => owner.StartOwning(request => secondSaw.Enqueue(request.Path)));

        for (var i = 0; i < 5; i++)
            Assert.True(owner.Submit(new ShrinkRequest(500, $"tek-tuketici-{i}.mp4")));

        Assert.True(done.Wait(TimeSpan.FromSeconds(15)), "Bes istek zamaninda islenmedi.");
        Assert.Empty(secondSaw);
        Assert.Equal(5, firstSaw.Count);
    }

    [Fact]
    public void L2_malformed_pipe_message_is_answered_with_a_refusal_not_a_silent_drop()
    {
        var channel = Channel();
        using var owner = new ShrinkRequestQueue(channel);
        Assert.True(owner.IsOwner);

        var handled = new ConcurrentQueue<string>();
        owner.StartOwning(request => handled.Enqueue(request.Path));

        Assert.Equal(ShrinkRequestQueue.Nack, RawExchange(owner.PipeName, "sekmesiz-bozuk-mesaj"));
        Assert.Equal(ShrinkRequestQueue.Nack, RawExchange(owner.PipeName, "sayidegil\tc:/yol/a.mp4"));
        Assert.Equal(ShrinkRequestQueue.Ack, RawExchange(owner.PipeName, "500\tc:/yol/gecerli.mp4"));

        Assert.True(SpinWait.SpinUntil(() => handled.Count == 1, TimeSpan.FromSeconds(15)));
        Assert.Equal(new[] { "c:/yol/gecerli.mp4" }, handled.ToArray());
    }

    [Fact]
    public void L2_submit_reports_failure_when_the_owner_is_not_listening()
    {
        var channel = Channel();
        using var orphan = new ShrinkRequestQueue(channel);
        Assert.True(orphan.IsOwner);

        using var client = new ShrinkRequestQueue(channel);
        Assert.False(client.IsOwner);

        Assert.False(client.Submit(new ShrinkRequest(500, "sahipsiz.mp4"), TimeSpan.FromMilliseconds(300)));
    }

    private static string? RawExchange(string pipeName, string message)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
        client.Connect(15000);
        using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(client, leaveOpen: true);
        writer.WriteLine(message);
        return reader.ReadLine();
    }
}

}
