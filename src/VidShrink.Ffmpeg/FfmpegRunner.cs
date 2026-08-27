using System.Diagnostics;

namespace VidShrink.Ffmpeg;

/// <summary>
/// Tek seferlik bir ffmpeg kosusunun sonucu. Hata metni yutulmaz: cagiran
/// <see cref="StandardError"/> ile kullaniciya sebep gosterebilir.
/// </summary>
/// <param name="Ok">Surec 0 ile dondu.</param>
/// <param name="ExitCode">Surec cikis kodu; surec hic kosmadiysa -1.</param>
/// <param name="StandardError">ffmpeg'in son hata satirlari.</param>
/// <param name="Elapsed">Duvar saati.</param>
public sealed record FfmpegRun(bool Ok, int ExitCode, string StandardError, TimeSpan Elapsed);

/// <summary>
/// Hazir bir arguman listesini kosturur ve bitmesini bekler. Arguman uretmez — ne
/// uretilecegini <see cref="VidShrink.Core.FfmpegArguments"/> bilir, burasi yalnizca kosturur.
/// </summary>
/// <remarks>
/// Iptal sureci <b>oldurur</b>: kisa parca kodlamasi kullanicinin bir sonraki ayar
/// degisikliginde atilir ve olen istegin ffmpeg'i makinede kalmaz. stdout ve stderr ayni
/// anda bosaltilir; bosaltilmazsa boru dolar ve surec asilir.
/// </remarks>
public static class FfmpegRunner
{
    /// <summary>Hata metninde tutulan son satir sayisi.</summary>
    public const int ErrorTailLines = 8;

    public static async Task<FfmpegRun> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ct.ThrowIfCancellationRequested();

        var clock = Stopwatch.StartNew();
        Process process;
        try
        {
            process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, arguments) };
            process.StartInfo.RedirectStandardInput = true;
            process.Start();
        }
        catch (Exception ex)
        {
            clock.Stop();
            return new FfmpegRun(false, -1, ex.Message, clock.Elapsed);
        }

        using (process)
        using (ct.Register(() => TryKill(process)))
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(CancellationToken.None);
                _ = await stdout;
                var text = await stderr;
                clock.Stop();
                ct.ThrowIfCancellationRequested();
                return new FfmpegRun(process.ExitCode == 0, process.ExitCode, Tail(text), clock.Elapsed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                clock.Stop();
                return new FfmpegRun(false, -1, ex.Message, clock.Elapsed);
            }
        }
    }

    /// <summary>Uzun hata akisindan yalnizca son satirlar; sebep hep sonda durur.</summary>
    public static string Tail(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        if (lines.Count > ErrorTailLines) lines = lines.Skip(lines.Count - ErrorTailLines).ToList();
        return string.Join(Environment.NewLine, lines);
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }
}
