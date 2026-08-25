using System.Diagnostics;
using System.Globalization;
using VidShrink.Core.Playback;

namespace VidShrink.Ffmpeg.Playback;

/// <summary>
/// Iki girdiden tek cift genislikli kare ureten ffmpeg grafigini kurar:
/// <c>[0:v]fps=N,scale=W:H[l];[1:v]fps=N,scale=W:H[r];[l][r]hstack=inputs=2[v]</c>
/// </summary>
/// <remarks>
/// Dosya yollari filtre metnine <b>girmez</b>; ayri argumanlar olarak <c>-i</c> ile gecirilir
/// ve filtre girdilere yalnizca indisle bakar. Bosluk, tirnak, <c>:</c> ve <c>'</c> iceren
/// yollar boylece hic kacis gerektirmez — birlestirme degil, arguman gecirme.
/// </remarks>
public static class ComparisonGraph
{
    private static readonly object ProbeGate = new();
    private static bool? _hstackWorks;

    /// <summary>
    /// Sonda suresi. Eski 6 sn makine yukluyken yetmiyordu; T32 surec acilisini tek basina
    /// 692-800 ms p95 olcmustu, dort ffmpeg birden kosarken bu kuyruk cok daha uzun.
    /// </summary>
    private const int ProbeTimeoutMs = 20000;

    public const string OutputLabel = "[v]";

    /// <summary>Filtre metni. Icinde yalniz etiket ve sayi vardir, kullanici verisi yoktur.</summary>
    public static string BuildFilter(int panelWidth, int panelHeight, int fps)
    {
        if (panelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(panelWidth));
        if (panelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(panelHeight));
        if (fps <= 0) throw new ArgumentOutOfRangeException(nameof(fps));

        var w = panelWidth.ToString(CultureInfo.InvariantCulture);
        var h = panelHeight.ToString(CultureInfo.InvariantCulture);
        var r = fps.ToString(CultureInfo.InvariantCulture);

        return $"[0:v]fps={r},scale={w}:{h}[l];[1:v]fps={r},scale={w}:{h}[r];[l][r]hstack=inputs=2{OutputLabel}";
    }

    /// <summary>
    /// Bir filtre argumanina gomulecek metni kacisli hale getirir. Grafik su an yol gommuyor;
    /// gomen bir filtre eklenirse metin buradan gecmelidir.
    /// </summary>
    public static string EscapeFilterValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var escaped = new System.Text.StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            if (c is '\\' or '\'' or ':' or ',' or ';' or '[' or ']' or '=') escaped.Append('\\');
            escaped.Append(c);
        }
        return escaped.ToString();
    }

    /// <summary>Kalici boru surecinin tam arguman listesi.</summary>
    public static IReadOnlyList<string> BuildArguments(ComparisonFrameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin" };

        AppendInput(args, request, request.LeftPath);
        AppendInput(args, request, request.RightPath);

        args.Add("-filter_complex");
        args.Add(BuildFilter(request.PanelWidth, request.PanelHeight, request.Fps));
        args.Add("-map");
        args.Add(OutputLabel);
        args.Add("-an");
        args.Add("-sn");
        args.Add("-f");
        args.Add("rawvideo");
        args.Add("-pix_fmt");
        args.Add("bgra");
        args.Add("-");

        return args;
    }

    private static void AppendInput(List<string> args, ComparisonFrameRequest request, string path)
    {
        if (request.Realtime) args.Add("-re");
        if (request.Loop) { args.Add("-stream_loop"); args.Add("-1"); }
        if (request.Position > TimeSpan.Zero)
        {
            args.Add("-ss");
            args.Add(request.Position.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        }
        args.Add("-i");
        args.Add(path);
    }

    /// <summary>Sondanin verdigi cevap. <see cref="Belirsiz"/> bir yetenek cevabi degildir.</summary>
    public enum ProbeOutcome
    {
        Calisiyor,
        Calismiyor,

        /// <summary>Sonda tamamlanamadi — sure doldu ya da ffmpeg hic kosmadi.</summary>
        Belirsiz
    }

    /// <summary>
    /// <c>hstack</c> bu ffmpeg yapisinda gercekten calisiyor mu. Liste sorgusu degil, kucuk
    /// bir sentetik kosu — T33/K2 liste sorgusunun sahte negatif verdigini olctu.
    /// </summary>
    /// <remarks>
    /// Yalnizca <b>kesin</b> cevaplar onbellege girer. Sure dolmasi bir yetenek cevabi degil:
    /// makine yukluyken sonda gec kaliyordu ve onbellek o gecikmeyi "bu yapida hstack yok"
    /// diye oturum boyu kalici hale getiriyordu. Olculdu: dort kosudan biri boyle dustu,
    /// ayni makinede ayni ffmpeg ile digerleri calisti.
    /// </remarks>
    public static ProbeOutcome ProbeHstack()
    {
        lock (ProbeGate)
        {
            if (_hstackWorks is { } cached) return cached ? ProbeOutcome.Calisiyor : ProbeOutcome.Calismiyor;

            var outcome = Probe();
            if (outcome == ProbeOutcome.Belirsiz) outcome = Probe();
            if (outcome != ProbeOutcome.Belirsiz) _hstackWorks = outcome == ProbeOutcome.Calisiyor;
            return outcome;
        }
    }

    public static bool HstackWorks() => ProbeHstack() == ProbeOutcome.Calisiyor;

    /// <summary>Onbellegi bosaltir. Testler icin.</summary>
    public static void ResetProbeCache()
    {
        lock (ProbeGate) _hstackWorks = null;
    }

    private static ProbeOutcome Probe()
    {
        try
        {
            var args = new[]
            {
                "-hide_banner", "-loglevel", "error",
                "-f", "lavfi", "-i", "testsrc2=size=64x64:rate=1:duration=0.1",
                "-f", "lavfi", "-i", "testsrc2=size=64x64:rate=1:duration=0.1",
                "-filter_complex", BuildFilter(64, 64, 1),
                "-map", OutputLabel, "-frames:v", "1",
                "-f", "rawvideo", "-pix_fmt", "bgra",
                OperatingSystem.IsWindows() ? "NUL" : "/dev/null"
            };
            using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(ProbeTimeoutMs))
            {
                try { process.Kill(true); } catch { }
                return ProbeOutcome.Belirsiz;
            }
            Task.WaitAll(new Task[] { output, error }, 1000);
            return process.ExitCode == 0 ? ProbeOutcome.Calisiyor : ProbeOutcome.Calismiyor;
        }
        catch
        {
            return ProbeOutcome.Belirsiz;
        }
    }
}
