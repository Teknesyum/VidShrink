using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Playback;

internal enum FrameKind
{
    Frames,
    Sheet
}

/// <summary>
/// <see cref="FrameKind.Frames"/>'te <c>Target</c> karelerin yazılacağı klasördür, kareler
/// <c>&lt;klasör adı&gt;_0001.png</c> diye numaralanır; <see cref="FrameKind.Sheet"/>'te tablonun png yolu.
/// </summary>
internal sealed record FrameRequest(
    string Source,
    string Target,
    double StartSeconds,
    double DurationSeconds,
    FrameKind Kind,
    double IntervalSeconds = ToolsOptions.DefaultFrameSeconds,
    int Columns = ToolsOptions.DefaultSheetColumns,
    int Rows = ToolsOptions.DefaultSheetRows,
    int CellWidth = ToolsOptions.DefaultSheetWidth,
    string Header = "");

internal sealed record FrameResult(bool Ok, string Target, int Count, string Error, TimeSpan Elapsed);

/// <summary>
/// Kare dışa aktarma ve küçük resim tablosu. Kareler aralıkta her <c>IntervalSeconds</c>'ta bir
/// numaralı png olur; tablo aralığa eşit dağılmış <c>Columns × Rows</c> kareyi <c>tile</c> ile
/// tek resme dizer, üstüne <see cref="HeaderHeight"/> yüksekliğinde başlık şeridi koyar. Hücre
/// 16:9'dur, kaynak oranı korunur ve kalan siyahla doldurulur. Başlık metni süzgece değil
/// <c>textfile</c> ile geçer; dosya adındaki <c>:</c> ya da <c>%</c> süzgeç dilini bozmaz.
/// </summary>
internal static class FrameExport
{
    internal const int MaximumFrames = 1000;
    internal const int HeaderHeight = 32;
    internal const int HeaderFontSize = 16;
    internal const string Threads = "2";

    internal static bool IsValid(FrameRequest request)
        => !string.IsNullOrWhiteSpace(request.Source)
           && !string.IsNullOrWhiteSpace(request.Target)
           && double.IsFinite(request.StartSeconds) && request.StartSeconds >= 0
           && double.IsFinite(request.DurationSeconds) && request.DurationSeconds > 0
           && double.IsFinite(request.IntervalSeconds) && request.IntervalSeconds > 0
           && request.Columns > 0 && request.Rows > 0 && request.CellWidth > 0;

    internal static int FrameCount(double durationSeconds, double intervalSeconds)
        => (int)Math.Clamp(Math.Floor(durationSeconds / intervalSeconds + 1e-9), 1, MaximumFrames);

    internal static int CellHeight(int cellWidth)
    {
        var height = (int)Math.Round(cellWidth * 9 / 16.0, MidpointRounding.AwayFromZero);
        return height % 2 == 0 ? height : height + 1;
    }

    internal static (int Width, int Height) SheetSize(int columns, int rows, int cellWidth)
        => (cellWidth * columns, CellHeight(cellWidth) * rows + HeaderHeight);

    internal static string Header(string media, double startSeconds, double durationSeconds, int columns, int rows)
    {
        var start = TimeSpan.FromSeconds(Math.Max(0, startSeconds));
        var end = start + TimeSpan.FromSeconds(Math.Max(0, durationSeconds));
        var name = MediaAddress.IsAddress(media) ? MediaAddress.Name(media) : Path.GetFileName(media);
        return (name + "   " + Saat.Ekran(start, end) + " - " + Saat.Ekran(end, end) + "   "
                + columns.ToString(CultureInfo.InvariantCulture) + "x" + rows.ToString(CultureInfo.InvariantCulture))
            .Replace('\r', ' ').Replace('\n', ' ');
    }

    internal static string? FontFile()
    {
        IEnumerable<string> candidates = OperatingSystem.IsWindows()
            ? new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "arial.ttf") }
            : OperatingSystem.IsMacOS()
                ? new[] { "/System/Library/Fonts/Supplemental/Arial.ttf", "/Library/Fonts/Arial.ttf", "/System/Library/Fonts/Helvetica.ttc" }
                : Array.Empty<string>();
        foreach (var candidate in candidates)
            if (File.Exists(candidate)) return candidate;
        return null;
    }

    internal static string FramePattern(string folder)
        => ImageSequence.Escape(Path.Combine(folder, Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)))) + "_%04d" + ToolsOptions.ImageExtension;

    internal static string SheetFilter(FrameRequest request, string? textFile, string? fontFile)
    {
        var cellHeight = CellHeight(request.CellWidth);
        var cells = request.Columns * request.Rows;
        var size = request.CellWidth.ToString(CultureInfo.InvariantCulture) + ":" + cellHeight.ToString(CultureInfo.InvariantCulture);
        var filter = new StringBuilder()
            .Append("fps=").Append(cells.ToString(CultureInfo.InvariantCulture)).Append('/').Append(Seconds(request.DurationSeconds))
            .Append(",scale=").Append(size).Append(":force_original_aspect_ratio=decrease")
            .Append(",pad=").Append(size).Append(":(ow-iw)/2:(oh-ih)/2")
            .Append(",tile=").Append(request.Columns.ToString(CultureInfo.InvariantCulture)).Append('x').Append(request.Rows.ToString(CultureInfo.InvariantCulture))
            .Append(",pad=iw:ih+").Append(HeaderHeight.ToString(CultureInfo.InvariantCulture)).Append(":0:").Append(HeaderHeight.ToString(CultureInfo.InvariantCulture));
        if (textFile is not null)
        {
            filter.Append(",drawtext=");
            if (fontFile is not null) filter.Append("fontfile='").Append(VideoFilterChain.FilterPath(fontFile)).Append("':");
            filter.Append("textfile='").Append(VideoFilterChain.FilterPath(textFile)).Append('\'')
                .Append(":expansion=none:x=8:y=8:fontsize=").Append(HeaderFontSize.ToString(CultureInfo.InvariantCulture))
                .Append(":fontcolor=white");
        }

        return filter.ToString();
    }

    internal static IReadOnlyList<string> Arguments(FrameRequest request, string? textFile, string? fontFile)
    {
        var head = new List<string>
        {
            "-y", "-hide_banner", "-nostdin", "-threads", Threads,
            "-ss", Seconds(request.StartSeconds), "-t", Seconds(request.DurationSeconds), "-i", request.Source,
            "-an", "-sn", "-threads", Threads
        };

        if (request.Kind == FrameKind.Frames)
        {
            head.AddRange(new[]
            {
                "-vf", "fps=1/" + Seconds(request.IntervalSeconds),
                "-frames:v", FrameCount(request.DurationSeconds, request.IntervalSeconds).ToString(CultureInfo.InvariantCulture),
                "-start_number", "1",
                FramePattern(request.Target)
            });
            return head;
        }

        head.AddRange(new[]
        {
            "-vf", SheetFilter(request, textFile, fontFile),
            "-frames:v", "1", "-update", "1",
            request.Target
        });
        return head;
    }

    internal static async Task<FrameResult> RunAsync(FrameRequest request, CancellationToken ct = default)
    {
        if (!IsValid(request)) return new FrameResult(false, request.Target, 0, "range", TimeSpan.Zero);

        var folder = request.Kind == FrameKind.Frames ? request.Target : Path.GetDirectoryName(request.Target);
        var textFile = request.Kind == FrameKind.Sheet && request.Header.Length > 0 ? request.Target + ".txt" : null;
        try
        {
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            if (textFile is not null) File.WriteAllText(textFile, request.Header, new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FrameResult(false, request.Target, 0, ex.Message, TimeSpan.Zero);
        }

        FfmpegRun run;
        try
        {
            run = await FfmpegRunner.RunAsync(Arguments(request, textFile, textFile is null ? null : FontFile()), ct).ConfigureAwait(false);
        }
        finally
        {
            if (textFile is not null) Delete(textFile);
        }

        var count = request.Kind == FrameKind.Frames ? WrittenFrames(request.Target) : File.Exists(request.Target) && new FileInfo(request.Target).Length > 0 ? 1 : 0;
        var ok = run.Ok && count > 0;
        if (!ok) Discard(request);
        return new FrameResult(ok, request.Target, ok ? count : 0, ok ? "" : run.StandardError, run.Elapsed);
    }

    private static int WrittenFrames(string folder)
        => Directory.Exists(folder) ? Directory.GetFiles(folder, "*" + ToolsOptions.ImageExtension).Length : 0;

    private static void Discard(FrameRequest request)
    {
        try
        {
            if (request.Kind == FrameKind.Sheet)
            {
                Delete(request.Target);
                return;
            }

            if (!Directory.Exists(request.Target)) return;
            foreach (var file in Directory.GetFiles(request.Target, "*" + ToolsOptions.ImageExtension)) File.Delete(file);
            if (Directory.GetFileSystemEntries(request.Target).Length == 0) Directory.Delete(request.Target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void Delete(string file)
    {
        try { File.Delete(file); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static string Seconds(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
