using System.Globalization;

namespace VidShrink.Core;

public enum ConversionItemState
{
    Waiting,
    Running,
    Done,
    Failed,
    Cancelled
}

/// <summary>
/// Toplu Dönüştür kuyruğunun bir öğesi. <see cref="Own"/> boşsa öğe kuyruğun ortak ayarıyla
/// dönüşür; doluysa yalnız bu dosya kendi ayarıyla. İkisi aynı yapı: <see cref="ConversionPreset"/>.
/// </summary>
public sealed class ConversionQueueItem
{
    public ConversionQueueItem(string path) => Path = path;

    public string Path { get; }
    public ConversionPreset? Own { get; set; }
    public ConversionItemState State { get; internal set; } = ConversionItemState.Waiting;
    public string? Output { get; internal set; }
    public string? Error { get; internal set; }
}

public sealed record ConversionQueueSummary(int Done, int Failed, int Cancelled);

/// <summary>
/// DON-P6: Dönüştür'ün toplu kuyruğu. Dosyalar sırayla, birer birer dönüşür; düşen dosya kuyruğu
/// durdurmaz, iptal kalan dosyaları iptal sayar. Dönüştürme işi dışarıdan verilir; bu sınıf yalnız
/// sırayı, her dosyanın ayarını ve sonucu tutar.
/// </summary>
public sealed class ConversionQueue
{
    public ConversionQueue(IEnumerable<string> paths, ConversionPreset shared)
    {
        Items = paths.Select(path => new ConversionQueueItem(path)).ToList();
        Shared = shared;
    }

    public IReadOnlyList<ConversionQueueItem> Items { get; }
    public ConversionPreset Shared { get; set; }

    public ConversionPreset PresetFor(ConversionQueueItem item) => item.Own ?? Shared;

    public async Task<ConversionQueueSummary> RunAsync(
        Func<ConversionQueueItem, ConversionPreset, CancellationToken, Task<string>> convert,
        Action<ConversionQueueItem>? changed,
        CancellationToken ct)
    {
        foreach (var item in Items)
        {
            if (item.State != ConversionItemState.Waiting) continue;
            if (ct.IsCancellationRequested)
            {
                Mark(item, ConversionItemState.Cancelled, changed);
                continue;
            }

            Mark(item, ConversionItemState.Running, changed);
            try
            {
                item.Output = await convert(item, PresetFor(item), ct);
                Mark(item, ConversionItemState.Done, changed);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Mark(item, ConversionItemState.Cancelled, changed);
            }
            catch (Exception ex)
            {
                item.Error = ex.Message;
                Mark(item, ConversionItemState.Failed, changed);
            }
        }

        return Summary();
    }

    public ConversionQueueSummary Summary() => new(
        Items.Count(item => item.State == ConversionItemState.Done),
        Items.Count(item => item.State == ConversionItemState.Failed),
        Items.Count(item => item.State == ConversionItemState.Cancelled));

    private static void Mark(ConversionQueueItem item, ConversionItemState state, Action<ConversionQueueItem>? changed)
    {
        item.State = state;
        changed?.Invoke(item);
    }
}

/// <summary>
/// Ön ayardan dönüştürme planı. Dönüştür panelinin planı (<c>MainWindow.ReadConversionPlan</c>)
/// ile aynı eşleme; kesit kuyrukta dosya başına anlamsız olduğu için yok.
/// </summary>
public static class ConversionPresetPlan
{
    public static ConversionPlan ToPlan(this ConversionPreset preset, HdrPolicy hdr = HdrPolicy.Preserve)
    {
        var defaults = new ConversionPlan();
        var intermediate = IntermediateCodecs.FromId(preset.VideoCodec);

        int? width = null, height = null;
        if (preset.Resolution == ConversionPreset.Custom)
        {
            var parts = (preset.CustomResolution ?? "").ToLowerInvariant().Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h)) (width, height) = (w, h);
        }
        else if (int.TryParse(preset.Resolution, out var fixedHeight))
        {
            height = fixedHeight;
        }

        double? fps = null;
        var fpsText = preset.Fps == ConversionPreset.Custom ? preset.CustomFps : preset.Fps;
        if (double.TryParse(fpsText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFps)) fps = parsedFps;

        string? audio = preset.AudioCodec == ConversionPreset.NoAudio ? null : preset.AudioCodec;
        if (new ConversionPlan { Container = preset.Container }.AudioOnly) audio = AudioCodecForAudioContainer(preset.Container);

        return new ConversionPlan
        {
            Container = preset.Container,
            VideoCodec = intermediate?.Encoder ?? preset.VideoCodec,
            VideoProfile = intermediate?.Profile,
            QualityMode = preset.QualityMode,
            Crf = preset.Quality > 0 ? preset.Quality : defaults.Crf,
            VideoBitrateK = preset.Quality > 0 ? preset.Quality : defaults.VideoBitrateK,
            Width = width,
            Height = height,
            Fps = fps,
            AudioCodec = audio,
            AudioBitrateK = preset.AudioBitrateK > 0 ? preset.AudioBitrateK : defaults.AudioBitrateK,
            HdrPolicy = hdr,
            DropMetadata = preset.DropMetadata
        };
    }

    public static string AudioCodecForAudioContainer(string container) => container switch
    {
        "mp3" => "libmp3lame",
        "wav" => "pcm_s16le",
        "flac" => "flac",
        "opus" or "ogg" => "libopus",
        "aiff" => "pcm_s16be",
        "ac3" => "ac3",
        _ => "aac"
    };
}
