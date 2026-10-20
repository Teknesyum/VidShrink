using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private IReadOnlyList<string> _slideImages = Array.Empty<string>();
    private string? _slideMusic;
    private bool _slideBusy;

    internal IReadOnlyList<string> SlideImages
    {
        get => _slideImages;
        set
        {
            _slideImages = Slideshow.Order(value ?? Array.Empty<string>());
            ShowSlides();
        }
    }

    internal string? SlideMusic
    {
        get => _slideMusic;
        set
        {
            _slideMusic = string.IsNullOrWhiteSpace(value) ? null : value;
            ShowSlides();
        }
    }

    internal string SlideStatusText => TxtSlideStatus.Text ?? string.Empty;

    private void InitSlides()
    {
        CmbSlideTransition.Items.Clear();
        foreach (var kind in TransitionChoices)
            CmbSlideTransition.Items.Add(new ComboBoxItem { Content = Strings.Get(kind is { } k ? "editor.clip.transition." + ClipTransition.Key(k) : "editor.clip.transition.none") });
        CmbSlideTransition.SelectedIndex = Array.IndexOf(TransitionChoices, TransitionKind.Dissolve);
        CmbSlideTransition.SelectionChanged += (_, _) => TxtSlideTransition.IsEnabled = SlideTransitionKind is not null;
        TxtSlideSeconds.Text = Slideshow.DefaultSeconds.ToString("0.##", Strings.Culture);
        TxtSlideTransition.Text = ClipTransition.DefaultSeconds.ToString("0.##", Strings.Culture);
        BtnSlides.Click += (_, _) => ShowSlidePanel(BtnSlides.IsChecked == true);
        BtnSlidePick.Click += (_, _) => _ = PickSlidesAsync();
        BtnSlideMusic.Click += (_, _) => _ = PickSlideMusicAsync();
        BtnSlideMusicClear.Click += (_, _) => SlideMusic = null;
        BtnSlideBuild.Click += (_, _) => _ = BuildSlidesClickedAsync();
        ShowSlides();
    }

    internal void ShowSlidePanel(bool open)
    {
        BtnSlides.IsChecked = open;
        SlidePanel.IsVisible = open;
        ShowSlides();
    }

    private TransitionKind? SlideTransitionKind
    {
        get
        {
            var index = CmbSlideTransition.SelectedIndex;
            return index >= 0 && index < TransitionChoices.Length ? TransitionChoices[index] : null;
        }
    }

    private void ShowSlides()
    {
        TxtSlideCount.Text = string.Format(Strings.Culture, Strings.Get("editor.slides.count"), _slideImages.Count);
        TxtSlideMusic.Text = _slideMusic is { } music ? Path.GetFileName(music) : Strings.Get("editor.slides.no-music");
        BtnSlideMusicClear.IsEnabled = _slideMusic is not null && !_slideBusy;
        ChkSlideFit.IsEnabled = _slideMusic is not null && !_slideBusy;
        TxtSlideTransition.IsEnabled = SlideTransitionKind is not null;
        BtnSlideBuild.IsEnabled = _slideImages.Count > 0 && !_slideBusy && !Exporting;
        BtnSlidePick.IsEnabled = !_slideBusy;
        BtnSlideMusic.IsEnabled = !_slideBusy;
    }

    private async Task PickSlidesAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Get("editor.slides.pick"),
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(Strings.Get("editor.slides.pick"))
                {
                    Patterns = ImageSequence.Extensions.Select(ext => "*" + ext).ToArray()
                }
            }
        }).ConfigureAwait(true);
        var paths = files.Select(file => file.TryGetLocalPath()).Where(path => path is not null).Select(path => path!).ToArray();
        if (paths.Length > 0) SlideImages = paths;
    }

    private async Task PickSlideMusicAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Get("editor.music.add"),
            AllowMultiple = false
        }).ConfigureAwait(true);
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) SlideMusic = path;
    }

    private Task<bool> BuildSlidesClickedAsync()
    {
        var seconds = ParseSeconds(TxtSlideSeconds.Text) ?? Slideshow.DefaultSeconds;
        var transition = ParseSeconds(TxtSlideTransition.Text) ?? ClipTransition.DefaultSeconds;
        return BuildSlideshowAsync(_slideImages, seconds, SlideTransitionKind, transition, _slideMusic, ChkSlideFit.IsChecked == true);
    }

    internal async Task<bool> BuildSlideshowAsync(IReadOnlyList<string> images, double seconds, TransitionKind? kind, double transitionSeconds, string? music, bool fitMusic)
    {
        var order = Slideshow.Order(images);
        if (order.Count == 0 || _slideBusy || Exporting) return false;
        _slideBusy = true;
        ShowSlides();
        try
        {
            return await BuildSlidesAsync(order, seconds, kind, transitionSeconds, music, fitMusic).ConfigureAwait(true);
        }
        finally
        {
            _slideBusy = false;
            ShowSlides();
        }
    }

    private async Task<bool> BuildSlidesAsync(IReadOnlyList<string> images, double seconds, TransitionKind? kind, double transitionSeconds, string? music, bool fitMusic)
    {
        MediaInfo? musicInfo = null;
        if (music is not null)
        {
            musicInfo = await ProbeSourceAsync(music).ConfigureAwait(true);
            if (musicInfo is not { HasAudio: true, DurationSeconds: > 0 }) return SlideFailed(Path.GetFileName(music));
        }

        var slide = fitMusic && musicInfo is not null ? Slideshow.FitSeconds(musicInfo.DurationSeconds, images.Count) : StillClip.ClampSeconds(seconds);
        var model = _model;
        int width, height;
        double fps;
        if (model is not null && _source is not null) (width, height, fps) = await StillFrameAsync().ConfigureAwait(true);
        else
        {
            (width, height) = StillClip.ImageFrame(await ProbeSourceAsync(images[0]).ConfigureAwait(true));
            fps = StillClip.FallbackFps;
        }

        var plan = Slideshow.Plan(images.Count, slide, kind is null ? 0 : transitionSeconds, fps);
        var clips = new string[images.Count];
        for (var i = 0; i < images.Count; i++)
        {
            TxtSlideStatus.Text = string.Format(Strings.Culture, Strings.Get("editor.slides.working"), i + 1, images.Count);
            var path = images[i];
            var length = plan.Seconds[i];
            var clip = SourceStamp.Of(path) is { } stamp
                ? await MakeStillAsync(StillClip.OutputName("resim", Path.GetFullPath(path), stamp.Size, stamp.ModifiedUtcTicks, width, height, fps, length),
                    output => StillClip.FromImage(path, output, width, height, fps, length)).ConfigureAwait(true)
                : null;
            if (!ReferenceEquals(model, _model) || Exporting)
            {
                TxtSlideStatus.Text = string.Empty;
                return false;
            }

            if (clip is null) return SlideFailed(Path.GetFileName(path));
            clips[i] = clip;
        }

        var transition = kind is { } k && plan.TransitionSeconds > 0 ? new ClipTransition(k, EditTime.FromSeconds(plan.TransitionSeconds)) : null;
        var ticks = plan.Seconds.Select(EditTime.FromSeconds).ToArray();
        if (model is null)
        {
            await OpenSourceAsync(clips[0]).ConfigureAwait(true);
            if (_model is null || !CurrentMedia.SamePath(clips[0], _source)) return SlideFailed(Path.GetFileName(images[0]));
        }

        if (_model is not { } target) return false;
        var start = model is null ? 0 : target.Duration;
        var placed = musicInfo is null || music is null ? null : SlideshowMusic(target, music, musicInfo, start);
        var from = model is null ? 1 : 0;
        var first = target.Clips.Count;
        if (from < clips.Length || placed is not null)
        {
            for (var i = from; i < clips.Length; i++) Remember(clips[i], SourceStamp.Of(clips[i]));
            var done = from < clips.Length
                ? Apply(m => m.AppendSources(ticks[from..], transition, joinPrevious: model is null, placed) >= 0, first, reload: true)
                : Apply(m => placed is not null && m.SetMusic(placed), -1);
            if (!done) return SlideFailed(Path.GetFileName(images[from < clips.Length ? from : 0]));
        }

        var text = string.Format(Strings.Culture, Strings.Get("editor.slides.done"), images.Count);
        TxtSlideStatus.Text = text;
        ShowProjectStatus("StatusSuccess", text);
        return true;
    }

    private static EditMusic SlideshowMusic(EditTimeline model, string path, MediaInfo info, long start)
    {
        var placed = new EditMusic(path, EditTime.FromSeconds(info.DurationSeconds), start, model.Music?.LevelDb ?? MusicDefaultLevelDb);
        return model.Music is { } previous ? placed with { Duck = previous.Duck, DuckDb = previous.DuckDb, DuckReleaseMs = previous.DuckReleaseMs } : placed;
    }

    private bool SlideFailed(string what)
    {
        TxtSlideStatus.Text = string.Empty;
        return StillFailed(what);
    }
}
