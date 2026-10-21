using System;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

public partial class MainWindow
{
    internal delegate Task<QualitySearchResult> OlcerekAramaKosusu(
        MediaInfo info, PlanOptions options, double requestedQuality, double startMb,
        IProgress<QualitySearchRound> progress, CancellationToken ct);

    private bool _kaliteSurer;
    private QualitySearchResult? _sonArama;

    internal OlcerekAramaKosusu? OlcerekAramaKosucu { get; set; }

    internal Func<bool> OlcerHazir { get; set; } = () => QualityMeasurement.Instance.IsAvailable;

    internal int OlcerekAramaCagrisi { get; private set; }

    internal bool KaliteSurerForTest
    {
        get => _kaliteSurer;
        set => _kaliteSurer = value;
    }

    private bool OlcerekAramaKosar
        => MeasuredQualitySearch.ShouldRun(ChkAdvSearchByMeasure.IsChecked == true, _kaliteSurer, OlcerHazir(), _aiPlan is not null);

    private Task<QualitySearchResult> VarsayilanArama(MediaInfo info, PlanOptions options, double requestedQuality,
        double startMb, IProgress<QualitySearchRound> progress, CancellationToken ct)
    {
        var profile = _profile;
        var encoders = _planEncoders;
        var scenes = _sceneMap?.Map;
        return Task.Run(() => QualitySearchSampler.RunAsync(info, options, requestedQuality, startMb, profile, encoders,
            ShrinkEngine.ProbeMeter(scenes), options.SpeedMode, scenes, progress, ct), ct);
    }

    internal async Task<(double TargetMb, EncodePlan Plan)> BaslangicPlaniAsync(double targetMb, EncodePlan plan, CancellationToken ct)
    {
        _sonArama = null;
        if (_info is not { } info || !OlcerekAramaKosar) return (targetMb, plan);

        OlcerekAramaCagrisi++;
        var options = CurrentOptions();
        var kosucu = OlcerekAramaKosucu ?? VarsayilanArama;
        var result = await kosucu(info, options, ParseQualityTarget(), targetMb,
            new Progress<QualitySearchRound>(ShowSearchRound), ct);
        _sonArama = result;
        if (result.MeasuredQuality is null) return (targetMb, plan);

        var searched = ShrinkEngine.Decide(info, PlanCalculator.WithTarget(options, result.TargetMb), _profile, _planEncoders).Plan;
        return (result.TargetMb, searched);
    }

    private void ShowSearchRound(QualitySearchRound round)
    {
        Progress.Value = Math.Clamp((round.Round - (round.MeasuredQuality is null ? 1.0 : 0.0)) / round.MaxRounds, 0.0, 1.0);
        SetStage(TxtStage, round.MeasuredQuality is { } score
            ? Say("main.run.search-round-done", round.Round, round.MaxRounds, Num(round.TargetMb, "0.0"), Num(score, "0.0"))
            : Say("main.run.search-round", round.Round, round.MaxRounds, Num(round.TargetMb, "0.0")));
    }

    private string AramaOzeti()
    {
        if (_sonArama is not { } arama) return "";
        return arama.MeasuredQuality is { } measured
            ? " " + Say("main.run.search-result", Num(arama.TargetMb, "0.0"), arama.SearchRounds.Count, Num(measured, "0.0"), Num(arama.RequestedQuality, "0.#"))
            : " " + Say("main.run.search-failed");
    }

    internal string AramaOzetiForTest() => AramaOzeti();

    internal double TargetMbForTest => ParseTargetMb();
}
