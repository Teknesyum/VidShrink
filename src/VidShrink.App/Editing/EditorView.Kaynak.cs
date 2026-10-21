using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Editing;

/// <summary>
/// Ek kaynaklar: ilk kaynagin ardindan cizelgenin sonuna eklenen dosyalar. Sira 1'den baslar ve
/// modelin <see cref="EditTimeline.ExtraSources"/> listesiyle ayni siradadir. Her ek kaynagin ses
/// dalgasi, anahtar karesi ve kucuk resmi ayri taranir; taramalar pencere kapaninca ve kaynak
/// degisince iptal edilir.
/// </summary>
internal partial class EditorView
{
    private readonly List<string> _extraPaths = new();
    private readonly List<SourceStamp?> _extraStamps = new();
    private readonly List<CancellationTokenSource> _extraScans = new();
    private readonly List<ThumbnailQueue> _extraThumbnails = new();

    /// <summary>Eklenen dosyanin akis bilgisini okuyan dikis; verilmezse ffprobe kosar.</summary>
    internal Func<string, CancellationToken, Task<MediaInfo>>? SourceProber { get; set; }

    /// <summary>Kaynak sirasiyla yollar; ilki acilan dosyadir. Kaynak yokken bos.</summary>
    internal IReadOnlyList<string> SourcePaths
        => _source is { } first ? _extraPaths.Prepend(first).ToArray() : Array.Empty<string>();

    private static readonly DataFormat<string> PoolFormat = DataFormat.CreateStringApplicationFormat("vidshrink-media-source");

    private string[] _mediaItems = Array.Empty<string>();

    /// <summary>Medya havuzundaki satirlar: kaynak sirasiyla dosya adlari.</summary>
    internal IReadOnlyList<string> MediaItems => _mediaItems;

    /// <summary>Havuzdan birakilan klip ustune mi yazar; yanlissa araya girer ve sonrakileri iter.</summary>
    internal bool PlaceOverwrite
    {
        get => CmbPlaceMode.SelectedIndex == 1;
        set => CmbPlaceMode.SelectedIndex = value ? 1 : 0;
    }

    private void InitSources()
    {
        BtnAddSource.Click += (_, _) => _ = AddSourceClickedAsync();
        MnuAddSource.Click += (_, _) => _ = AddSourceClickedAsync();
        BtnMedia.Click += (_, _) => ShowMediaPanel(BtnMedia.IsChecked == true);
        CmbPlaceMode.SelectedIndex = 0;
        LstMedia.AddHandler(PointerPressedEvent, OnPoolPressed, handledEventsToo: true);
    }

    internal void ShowMediaPanel(bool open)
    {
        BtnMedia.IsChecked = open;
        MediaPanel.IsVisible = open;
        RefreshMedia();
    }

    private void RefreshMedia()
    {
        var items = SourcePaths.Select(path => Path.GetFileName(path)).ToArray();
        if (items.SequenceEqual(_mediaItems, StringComparer.Ordinal)) return;
        _mediaItems = items;
        LstMedia.ItemsSource = items;
    }

    /// <summary>
    /// Havuzdaki kaynagin tamamini cizelgenin <paramref name="at"/> anina secili kiple koyar ve konan
    /// parcayi secer. Kaynak yoksa ya da teslim suruyorsa cizelge degismez.
    /// </summary>
    internal bool PlaceFromPool(int source, long at)
    {
        if (_model is null || Exporting || source < 0 || source >= SourcePaths.Count) return false;
        var overwrite = PlaceOverwrite;
        var placed = -1;
        if (!Apply(model => (placed = model.PlaceSource(source, Math.Clamp(at, 0, model.Duration), overwrite)) >= 0, -1, reload: true)) return false;
        Timeline.SelectedIndex = placed;
        return true;
    }

    private async void OnPoolPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(LstMedia).Properties.IsLeftButtonPressed || e.Source is not Avalonia.Visual visual) return;
        if (Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<ListBoxItem>(visual, includeSelf: true) is not { } row) return;
        var index = LstMedia.IndexFromContainer(row);
        if (index < 0) return;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(PoolFormat, index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy).ConfigureAwait(true);
    }

    private static int? PoolSource(DragEventArgs e)
        => e.DataTransfer.TryGetValue(PoolFormat) is { } text && int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index) ? index : null;

    private bool DropOnTimeline(DragEventArgs e, int source)
    {
        var x = e.GetPosition(Timeline).X;
        if (x < Timeline.HeaderWidth || x > Timeline.Bounds.Width) return false;
        var at = Timeline.XToTime(x);
        return PlaceFromPool(source, Timeline.SnapTarget(at) ?? Timeline.RoundToFrame(at));
    }

    private bool IsSource(int index, string path)
        => index == 0
            ? CurrentMedia.SamePath(path, _source)
            : index <= _extraPaths.Count && CurrentMedia.SamePath(path, _extraPaths[index - 1]);

    private void ForgetSources()
    {
        foreach (var scan in _extraScans)
        {
            scan.Cancel();
            scan.Dispose();
        }

        _extraScans.Clear();
        _extraPaths.Clear();
        _extraStamps.Clear();
        Timeline.ForgetSources();
        foreach (var queue in _extraThumbnails) queue.Dispose();
        _extraThumbnails.Clear();
    }

    private IReadOnlyList<SourceStamp>? ExtraStamps()
        => _extraStamps.All(stamp => stamp is not null) ? _extraStamps.Select(stamp => stamp!).ToArray() : null;

    /// <summary>
    /// Dosyayi cizelgenin sonuna yeni kaynak olarak ekler. Goruntusu olmayan ses dosyasi A2 muzik izine konur
    /// (<see cref="PlaceMusic"/>). Sure okunamazsa cizelge degismez ve durum satiri nedenini soyler.
    /// </summary>
    internal async Task<bool> AddSourceAsync(string path)
    {
        if (_model is not { } model || _source is null || Exporting) return false;
        if (ImageSequence.IsImage(path)) return await AddImageAsync(path).ConfigureAwait(true);

        var info = await ProbeSourceAsync(path).ConfigureAwait(true);
        if (!ReferenceEquals(model, _model) || Exporting) return false;
        if (info is not null && IsMusicFile(info)) return PlaceMusic(path, info);
        var duration = info is { Width: > 0, Height: > 0 } ? EditTime.FromSeconds(info.DurationSeconds) : 0;
        if (duration <= 0)
        {
            ShowProjectStatus("StatusError", string.Format(Strings.Culture, Strings.Get("editor.source.failed"), Path.GetFileName(path)));
            return false;
        }

        var index = Remember(path, SourceStamp.Of(path));
        return Apply(m => m.AddSource(duration) == index, model.Clips.Count, reload: true);
    }

    private async Task<MediaInfo?> ProbeSourceAsync(string path)
    {
        if (KnownInfo?.Invoke(path) is { } known) return known;
        try
        {
            var prober = SourceProber ?? ((file, ct) => FfprobeClient.ProbeAsync(file, allowAudioOnly: true, ct));
            return await prober(path, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Birakilan ya da secilen dosyalar: cizelge aciksa hepsi sona eklenir, kapaliysa resim olmayan ilki acilir ve
    /// kalani eklenir; hepsi resimse ilki ara videoya cevrilip kaynak olarak acilir.
    /// </summary>
    internal async Task TakeFilesAsync(IReadOnlyList<string> files)
    {
        var rest = files;
        if (_model is null && files.FirstOrDefault(file => !ImageSequence.IsImage(file)) is { } first)
        {
            await OpenSourceAsync(first).ConfigureAwait(true);
            rest = files.Where(file => !ReferenceEquals(file, first)).ToArray();
        }
        else if (_model is null && files.Count > 0)
        {
            if (!await OpenImageAsync(files[0]).ConfigureAwait(true)) return;
            rest = files.Skip(1).ToArray();
        }

        foreach (var file in rest) await AddSourceAsync(file).ConfigureAwait(true);
    }

    /// <summary>
    /// Kayitli projenin ek kaynaklarini alir. Biri yoksa ya da kaydedildiginden beri degismisse
    /// hicbiri alinmaz ve <c>false</c> doner.
    /// </summary>
    private bool AdoptSources(IReadOnlyList<ExtraSource> extras)
    {
        var stamps = extras.Select(extra => SourceStamp.Of(extra.Stamp.Path)).ToArray();
        for (var i = 0; i < extras.Count; i++)
            if (stamps[i] is not { } stamp || !extras[i].Stamp.Matches(stamp)) return false;

        for (var i = 0; i < extras.Count; i++) Remember(extras[i].Stamp.Path, stamps[i]);
        return true;
    }

    private int Remember(string path, SourceStamp? stamp)
    {
        _extraPaths.Add(path);
        _extraStamps.Add(stamp);
        var index = _extraPaths.Count;
        var scan = new CancellationTokenSource();
        _extraScans.Add(scan);
        PeakLoad = Task.WhenAll(PeakLoad, LoadPeaksAsync(path, index, scan.Token));
        KeyframeLoad = Task.WhenAll(KeyframeLoad, LoadKeyframesAsync(path, index, scan.Token));
        OpenExtraThumbnails(index, path);
        return index;
    }

    private async Task AddSourceClickedAsync()
    {
        if (_model is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Get("editor.source.add"),
            AllowMultiple = true
        }).ConfigureAwait(true);
        var paths = files.Select(file => file.TryGetLocalPath()).Where(path => path is not null).Select(path => path!).ToArray();
        if (paths.Length > 0) await TakeFilesAsync(paths).ConfigureAwait(true);
    }
}
