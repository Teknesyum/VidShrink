using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

public partial class MainWindow
{
    private ImageSequence? _sequence;
    private ImageSequenceFrame? _sequenceFrame;

    /// <summary>Klasör seçici dikişi; ölçüm sahte klasör verir, üretimde sistemin klasör seçicisi.</summary>
    internal Func<Task<string?>>? SequenceFolderPicker { get; set; }

    /// <summary>İlk karenin yoklama dikişi; ölçüm ffprobe koşturmadan ölçü verir.</summary>
    internal Func<string, CancellationToken, Task<ImageSequenceFrame?>>? SequenceProber { get; set; }

    internal ImageSequence? LoadedSequence => _sequence;

    /// <summary>
    /// Dönüştür'ün kaynağı: resim dizisi seçiliyse dizinin kare hızı kutusundan okunan hızla video bilgisi,
    /// değilse yüklenen dosya.
    /// </summary>
    private MediaInfo? ConvertInfo => _sequence is { } sequence && _sequenceFrame is { } frame
        ? sequence.ToMediaInfo(frame, ReadConversionPlan().Fps is { } fps and > 0 ? fps : ImageSequence.DefaultFps)
        : _info;

    private async void OnBrowseSequence(object? sender, RoutedEventArgs e) => await BrowseSequenceAsync();

    internal async Task BrowseSequenceAsync()
    {
        if (_cts is not null) return;
        try
        {
            var folder = await (SequenceFolderPicker ?? PickSequenceFolderAsync)();
            if (folder is null) return;
            await LoadSequenceAsync(Directory.EnumerateFiles(folder));
        }
        catch (Exception ex)
        {
            ReportSourceError($"{Say("main.error.pick")}: {ex.Message}");
        }
    }

    private async Task<string?> PickSequenceFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        return folders.Select(folder => folder.TryGetLocalPath()).OfType<string>().FirstOrDefault();
    }

    /// <summary>
    /// Verilen dosyalardan numaralı diziyi bulur, ilk karesini yoklar ve Dönüştür'ün kaynağı yapar.
    /// Dizi yoksa ya da ilk kare okunamazsa kaynak satırında söylenir, önceki kaynak kalır.
    /// </summary>
    internal async Task<bool> LoadSequenceAsync(IEnumerable<string> files)
    {
        var sequence = ImageSequence.Detect(files);
        if (sequence is null)
        {
            ReportSourceError(Say("main.convert.sequence.none"));
            return false;
        }

        ImageSequenceFrame? frame;
        try { frame = await (SequenceProber ?? FfprobeClient.ProbeFrameAsync)(sequence.Files[0], CancellationToken.None); }
        catch (Exception) { frame = null; }
        if (frame is null)
        {
            ReportSourceError(string.Format(Strings.Culture, Say("main.convert.sequence.unreadable"), Path.GetFileName(sequence.Files[0])));
            return false;
        }

        ClearSourceError();
        _sequence = sequence;
        _sequenceFrame = frame;
        ShowSourceName();
        RefreshConversion();
        return true;
    }

    private string SequenceSourceText(ImageSequence sequence)
    {
        var text = string.Format(Strings.Culture, Say("main.convert.sequence.source"), sequence.DisplayName, Num(sequence.Count, "0"));
        return sequence.Skipped > 0 ? text + "\n" + Say("main.convert.sequence.gap", Num(sequence.Skipped, "0")) : text;
    }

    private string ConversionOutputPath(MediaInfo info, ConversionPlan plan)
    {
        var source = info.Sequence is null ? info.FilePath : info.FilePath + ".seq";
        return plan.ImageSequenceOutput && !plan.SingleFrame
            ? EncodeRunner.SequenceOutputFolder(source, UsableFixedFolder(OutputFolderModeIndex, TxtOutputFolder.Text))
            : BuildUniqueOutputPath(source, "converted", plan.Container);
    }

    private static string CommandOutputPath(ConversionPlan plan, string output)
        => plan.ImageSequenceOutput && !plan.SingleFrame
            ? ConversionArguments.SequenceOutputPattern(output, Path.GetFileName(output), plan.Container)
            : output;

    private static string? FirstFrameOf(string folder)
        => Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder).Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault()
            : folder;
}
