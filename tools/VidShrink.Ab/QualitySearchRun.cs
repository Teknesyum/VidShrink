using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Ab;

internal static class QualitySearchRun
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        string? source = null;
        double? quality = null;
        var threads = 2;
        for (var i = 0; i + 1 < args.Count; i += 2)
        {
            switch (args[i])
            {
                case "--kaynak": source = args[i + 1]; break;
                case "--kalite": quality = double.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                case "--threads": threads = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
            }
        }
        if (source is null || quality is not { } requested)
        {
            Console.Error.WriteLine("kullanım: ab ara --kaynak <yol> --kalite <puan> [--threads 2]");
            return 1;
        }

        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            Process.GetCurrentProcess().ProcessorAffinity = (nint)((1L << Math.Clamp(threads, 1, 62)) - 1);

        var info = await FfprobeClient.ProbeAsync(source);
        var options = new PlanOptions();
        var modelMb = PlanCalculator.TargetMbForQuality(info, options, requested, null, null).TargetMb;
        var meter = QualityMeasurement.Instance;
        if (!meter.IsAvailable)
        {
            Console.Error.WriteLine("libvmaf ya da zscale yok; ölçülemez.");
            return 2;
        }

        var watch = Stopwatch.StartNew();
        var progress = new SyncProgress(round =>
        {
            if (round.MeasuredQuality is not { } score) return;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"tur {round.Round}/{round.MaxRounds}  {round.TargetMb:0.000} MB  ölçülen {score:0.00}  sapma {score - requested:+0.00;-0.00}  {watch.Elapsed.TotalSeconds:0.0} sn"));
        });
        var result = await QualitySearchSampler.RunAsync(info, options, requested, modelMb, null, null, meter,
            options.SpeedMode, null, progress, CancellationToken.None, threads);

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"istenen {requested:0.0}  model {modelMb:0.000} MB  seçilen {result.TargetMb:0.000} MB  ölçülen {result.MeasuredQuality:0.00}  sapma {result.Deviation:+0.00;-0.00}  tur {result.Rounds.Count}  yakınsadı {result.Converged}  süre {watch.Elapsed.TotalSeconds:0.0} sn"));
        return result.MeasuredQuality is null ? 2 : 0;
    }

    private sealed class SyncProgress : IProgress<QualitySearchRound>
    {
        private readonly Action<QualitySearchRound> _report;
        public SyncProgress(Action<QualitySearchRound> report) => _report = report;
        public void Report(QualitySearchRound value) => _report(value);
    }
}
