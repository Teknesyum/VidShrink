using VidShrink.Core;

namespace VidShrink.Ffmpeg;

/// <summary>
/// Bir adayin kisa deneme kaydinin sonucu.
/// </summary>
/// <param name="Choice">Denenen aday.</param>
/// <param name="Frames">Dosyaya giren kare sayisi.</param>
/// <param name="DroppedFrames">ffmpeg'in dusurdugu kare sayisi.</param>
/// <param name="Ok">Deneme kosumu tamamlandi mi.</param>
/// <param name="Error">Tamamlanmadiysa sebep; yutulmuyor.</param>
public sealed record RecorderTrial(
    RecorderAutoChoice Choice,
    long Frames,
    long DroppedFrames,
    bool Ok,
    string? Error)
{
    /// <summary>
    /// Dusen karenin yakalanan kareye orani. Hic kare gelmediyse 1: olculemeyen aday
    /// kazanmiyor.
    /// </summary>
    public double DropRatio
    {
        get
        {
            var total = Frames + DroppedFrames;
            return total <= 0 ? 1.0 : (double)DroppedFrames / total;
        }
    }
}

/// <summary>
/// Otomatik kipin kazanani ve onu dogduran denemeler.
/// </summary>
/// <param name="Choice">Yazilacak aday.</param>
/// <param name="Trials">Kosulan denemeler, sirasiyla.</param>
/// <param name="Measured">
/// Kazanan gercekten olculdu mu. <c>false</c> oldugunda liste ya bostur ya da hicbir deneme
/// tamamlanmamistir; o zaman merdivenin ilk adayi yaziliyor ve bu durum kullaniciya
/// bildiriliyor.
/// </param>
public sealed record RecorderAutoResult(
    RecorderAutoChoice Choice,
    IReadOnlyList<RecorderTrial> Trials,
    bool Measured);

/// <summary>
/// Otomatik kipin olcen kolu. Aday merdivenini <see cref="RecorderAutoPlan"/> uretiyor;
/// burasi her adayi kisa bir gercek kayitla deniyor ve <b>en az kare dusureni</b> seciyor.
/// <para>
/// Esik yok: bu depoda kayit icin olculmus bir "kabul edilebilir dusen kare" sayisi
/// bulunmuyor, o yuzden sayi uydurulmadi. Sifir dusuren ilk aday merdiveni kisa devre
/// ediyor; hicbiri sifir degilse orani en kucuk olan kazaniyor, esitlikte merdiven sirasi.
/// </para>
/// </summary>
public static class RecorderAutoProbe
{
    /// <summary>Aday basina kosulan deneme kaydinin suresi, saniye.</summary>
    public const int TrialSeconds = 3;

    /// <summary>
    /// Yeglenen donanim kollarindan yoklamanin <b>calistigini gordukleri</b>.
    /// <see cref="EncoderProbeState.Unmeasured"/> calisiyor sayilmiyor: surucusu olmayan
    /// makinede <c>h264_nvenc</c> listede duruyor ama kodlamiyor.
    /// </summary>
    public static IReadOnlyList<string> WorkingHardwareEncoders(IEncoderProbeState probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var working = new List<string>();
        foreach (var codec in RecorderAutoPlan.HardwareCodecs)
            if (probe.WorksAsEncoderState(codec) == EncoderProbeState.Working)
                working.Add(codec);

        return working;
    }

    /// <summary>
    /// Merdiveni deneyip kazanani dondurur. Deneme kayitlari
    /// <paramref name="scratchFolder"/> altina yaziliyor ve kosum bitince siliniyor; ses
    /// kolu denemeye girmiyor, olculen sey goruntu kodlayicisinin kareyi yetistirip
    /// yetistirmedigi.
    /// </summary>
    public static async Task<RecorderAutoResult> ChooseAsync(
        RecorderRequest baseRequest,
        string scratchFolder,
        IReadOnlyList<RecorderAutoChoice> candidates,
        int trialSeconds = TrialSeconds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(baseRequest);
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0) throw new ArgumentException("The candidate ladder is empty.", nameof(candidates));
        if (string.IsNullOrWhiteSpace(scratchFolder)) throw new ArgumentException("A scratch folder is required.", nameof(scratchFolder));
        if (trialSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(trialSeconds));

        Directory.CreateDirectory(scratchFolder);
        var trials = new List<RecorderTrial>(candidates.Count);

        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();

            var trial = await RunTrialAsync(baseRequest, scratchFolder, candidate, trialSeconds, ct);
            trials.Add(trial);
            if (trial.Ok && trial.DroppedFrames == 0)
                return new RecorderAutoResult(candidate, trials, true);
        }

        RecorderTrial? best = null;
        foreach (var trial in trials)
        {
            if (!trial.Ok) continue;
            if (best is null || trial.DropRatio < best.DropRatio) best = trial;
        }

        return best is null
            ? new RecorderAutoResult(candidates[0], trials, false)
            : new RecorderAutoResult(best.Choice, trials, true);
    }

    private static async Task<RecorderTrial> RunTrialAsync(
        RecorderRequest baseRequest, string scratchFolder,
        RecorderAutoChoice candidate, int trialSeconds, CancellationToken ct)
    {
        var request = RecorderAutoPlan.Apply(baseRequest, candidate) with
        {
            Audio = null,
            Webcam = null,
            Split = null,
            MaxDuration = TimeSpan.FromSeconds(trialSeconds)
        };

        var path = Path.Combine(
            scratchFolder,
            "auto-" + Guid.NewGuid().ToString("n") + "." + RecorderArguments.Extension(candidate.Container));

        RecordProgress? last = null;
        var progress = new Progress<RecordProgress>(value => last = value);

        try
        {
            await using var session = await RecorderSession.StartAsync(request, path, progress, ct);
            await Task.Delay(TimeSpan.FromSeconds(trialSeconds), ct);
            var result = await session.StopAsync(ct: ct);

            return new RecorderTrial(
                candidate,
                last?.Frames ?? 0,
                last?.DroppedFrames ?? 0,
                result.Ok && !result.Partial,
                result.Ok ? null : result.StandardError);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            return new RecorderTrial(candidate, 0, 0, false, error.Message);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
