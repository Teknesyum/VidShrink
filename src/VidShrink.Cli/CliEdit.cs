using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.Cli;

/// <summary>
/// <c>teslim</c> komutu: duzenleyicinin proje dosyasini pencere acmadan teslim eder. Cizelge
/// <see cref="EditProject.ToJob"/> ile kurulur, plan ve kosu duzenleyicinin kendi teslim yoludur
/// (<see cref="EditExportRunner"/>); her parca, metin, muzik ve ek kaynak projeden gelir.
/// </summary>
public static class CliEdit
{
    public static IReadOnlyList<string> Modes { get; } = new[] { "hizli", "akilli", "tam", "fast", "smart", "full" };

    public static bool TryMode(string text, out ExportMode mode)
    {
        var index = Modes.ToList().IndexOf(text.Trim().ToLowerInvariant());
        mode = (ExportMode)(index < 0 ? 0 : index % 3);
        return index >= 0;
    }

    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        var request = new CliRequest { Command = CliCommand.Edit };
        for (var i = 1; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h" or "--yardim" or "--help":
                    return CliParser.Success(new CliRequest { Command = CliCommand.Help });
                case "--cikti" or "--output" or "-o":
                    if (!CliParser.TryValue(args, ref i, out var output)) return CliParser.Fail("error.missing-value", arg);
                    request = request with { Output = output };
                    break;
                case "--kip" or "--mode":
                    if (!CliParser.TryValue(args, ref i, out var kip)) return CliParser.Fail("error.missing-value", arg);
                    if (!TryMode(kip, out var mode)) return CliParser.Fail("error.bad-edit-mode", kip);
                    request = request with { EditMode = mode };
                    break;
                case "--meta-yok" or "--no-metadata":
                    request = request with { DropMetadata = true };
                    break;
                case "--json":
                    request = request with { Json = true };
                    break;
                default:
                    if (arg.StartsWith('-') && arg.Length > 1) return CliParser.Fail("error.unknown-option", arg);
                    if (request.Input is not null) return CliParser.Fail("error.extra-input", arg);
                    request = request with { Input = arg };
                    break;
            }
        }

        return request.Input is null ? CliParser.Fail("error.no-input", null) : CliParser.Success(request);
    }

    internal static async Task<int> RunAsync(CliRequest request, CliServices services, TextWriter stdout, TextWriter stderr,
        CliText text, CancellationToken ct)
    {
        var projectPath = Path.GetFullPath(request.Input!);
        if (!File.Exists(projectPath))
        {
            stderr.WriteLine(text.Format("error.input-missing", projectPath));
            return ExitCodes.Error;
        }

        if (EditProject.Read(projectPath) is not { } project)
        {
            stderr.WriteLine(text.Format("error.edit-unreadable", projectPath));
            return ExitCodes.Error;
        }

        if (project.ToJob(services.Stamp, out var stale) is not { } job)
        {
            stderr.WriteLine(stale is null ? text.Format("error.edit-empty", projectPath) : text.Format("error.edit-stale", stale));
            return ExitCodes.Error;
        }

        if (request.EditMode is { } mode) job = job with { Mode = mode };
        var output = request.Output is { } given ? Path.GetFullPath(given) : EditOutputName.For(job.Sources[0]);
        if (job.Sources.Any(source => PathEquality.Same(source, output)) || PathEquality.Same(projectPath, output))
        {
            stderr.WriteLine(text["error.same-output"]);
            return ExitCodes.Usage;
        }

        var folder = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        CliProgress.Stage(stderr, text, "progress." + EditExportRunner.Stage, request.Json);
        var plan = await services.EditPrepare(job, output, request.DropMetadata, ct);
        if (plan.ReverseOverLimit.Count > 0)
        {
            var clips = string.Join(", ", plan.ReverseOverLimit.Select(i => (i + 1).ToString(CultureInfo.InvariantCulture)));
            stderr.WriteLine(text.Format("error.edit-reverse-limit", clips, Saat.Ekran(TimeSpan.FromSeconds(Math.Min(plan.ReverseLimitSeconds, TimeSpan.MaxValue.TotalSeconds / 2)))));
            return ExitCodes.Error;
        }

        var clock = Stopwatch.StartNew();
        string? failure = null;
        try
        {
            await services.EditRun(plan, new CliProgress.Encode(stderr, text, request.Json), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failure = ex.Message;
        }
        clock.Stop();

        var exit = failure is null ? ExitCodes.InBand : ExitCodes.Error;
        var sizeMb = failure is null && File.Exists(plan.OutputPath) ? Megabayt.Oku(new FileInfo(plan.OutputPath).Length) : (double?)null;
        if (request.Json)
            stdout.Write(ResultJson(projectPath, plan, job, sizeMb, exit, clock.Elapsed));
        else if (failure is null)
        {
            stdout.WriteLine(text.Format("result.output", plan.OutputPath));
            stdout.WriteLine(text.Format("edit.mode", Modes[(int)plan.Effective + (text.Language == "tr" ? 0 : 3)]));
            if (sizeMb is { } mb) stdout.WriteLine(text.Format("convert.size", mb.ToString("0.0", text.Culture)));
            stdout.WriteLine(text.Format("result.duration", clock.Elapsed.TotalSeconds < 60 ? clock.Elapsed.TotalSeconds.ToString("0.0", text.Culture) + " s" : Saat.Ekran(clock.Elapsed)));
        }
        if (failure is not null) stderr.WriteLine(text.Format("error.failed", failure));
        return exit;
    }

    private static string ResultJson(string project, ExportPlan plan, EditProjectJob job, double? sizeMb, int exit, TimeSpan elapsed)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteString("command", "teslim");
            writer.WriteString("project", project);
            writer.WriteString("output", plan.OutputPath);
            writer.WriteStartArray("sources");
            foreach (var source in job.Sources) writer.WriteStringValue(source);
            writer.WriteEndArray();
            writer.WriteNumber("clips", job.Timeline.Clips.Count);
            writer.WriteString("requestedMode", plan.Requested.ToString());
            writer.WriteString("effectiveMode", plan.Effective.ToString());
            writer.WriteStartObject("result");
            writer.WriteNumber("exitCode", exit);
            writer.WriteBoolean("success", exit == ExitCodes.InBand);
            if (sizeMb is { } mb) writer.WriteNumber("outputMb", Math.Round(mb, 3)); else writer.WriteNull("outputMb");
            writer.WriteNumber("elapsedSeconds", Math.Round(elapsed.TotalSeconds, 1));
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
    }
}
