using System;
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
    internal const double MusicDefaultLevelDb = -12;

    private static readonly double[] MusicLevels = { -24, -18, -12, -6, 0, 6 };

    private void InitMusic()
    {
        Timeline.MusicMoved += start => MoveMusic(start);
        TimelineMenu.Opening += (_, _) => BuildMusicMenu();
    }

    internal static bool IsMusicFile(MediaInfo info) => info.HasAudio && (info.IsAudioOnly || info.Width <= 0 || info.Height <= 0);

    internal bool PlaceMusic(string path, MediaInfo info)
    {
        if (_model is not { Duration: > 0 } model || Exporting) return false;
        var duration = EditTime.FromSeconds(info.DurationSeconds);
        if (duration <= 0) return false;
        var start = Timeline.RoundToFrame(Timeline.Playhead);
        if (start >= model.Duration - (long)Math.Ceiling(Timeline.FrameTicks)) start = 0;
        var level = model.Music?.LevelDb ?? MusicDefaultLevelDb;
        if (!Apply(m => m.SetMusic(new EditMusic(path, duration, Math.Max(0, start), level)), -1)) return false;
        ShowProjectStatus("StatusSuccess", string.Format(Strings.Culture, Strings.Get("editor.music.placed"), Path.GetFileName(path)));
        return true;
    }

    internal async Task<bool> AddMusicAsync(string path)
    {
        if (_model is null || Exporting) return false;
        var model = _model;
        var info = await ProbeSourceAsync(path).ConfigureAwait(true);
        if (!ReferenceEquals(model, _model) || Exporting) return false;
        if (info is { HasAudio: true } && PlaceMusic(path, info)) return true;
        ShowProjectStatus("StatusError", string.Format(Strings.Culture, Strings.Get("editor.music.failed"), Path.GetFileName(path)));
        return false;
    }

    internal bool SetMusicLevel(double levelDb)
    {
        if (_model?.Music is not { } music || Exporting) return false;
        var level = EditMusic.ClampLevel(levelDb);
        return Apply(m => m.SetMusic(new EditMusic(music.Path, music.Duration, music.Start, level)), -1);
    }

    internal bool MoveMusic(long start)
    {
        if (_model is not { Music: { } music } model || Exporting) return false;
        var at = Math.Clamp(start, 0, Math.Max(0, model.Duration - 1));
        return Apply(m => m.SetMusic(new EditMusic(music.Path, music.Duration, at, music.LevelDb)), -1);
    }

    internal bool RemoveMusic() => _model is { HasMusic: true } && !Exporting && Apply(m => m.SetMusic(null), -1);

    private void BuildMusicMenu()
    {
        MnuMusic.Items.Clear();
        var model = _model;
        var music = model?.Music;
        var idle = model is not null && !Exporting;

        var add = new MenuItem { Header = Strings.Get(music is null ? "editor.music.add" : "editor.music.replace"), IsEnabled = idle };
        add.Click += (_, _) => _ = AddMusicClickedAsync();
        MnuMusic.Items.Add(add);

        var levels = new MenuItem { Header = Strings.Get("editor.music.level"), IsEnabled = idle && music is not null };
        foreach (var preset in MusicLevels)
        {
            var value = preset;
            var item = new MenuItem
            {
                Header = EditorTimeline.MusicLevelText(value),
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = music is not null && Math.Abs(music.LevelDb - value) < 0.05
            };
            item.Click += (_, _) => SetMusicLevel(value);
            levels.Items.Add(item);
        }

        MnuMusic.Items.Add(levels);

        var here = new MenuItem { Header = Strings.Get("editor.music.to-playhead"), IsEnabled = idle && music is not null };
        here.Click += (_, _) => MoveMusic(Timeline.RoundToFrame(Timeline.Playhead));
        MnuMusic.Items.Add(here);

        var remove = new MenuItem { Header = Strings.Get("editor.music.remove"), IsEnabled = idle && music is not null };
        remove.Click += (_, _) => RemoveMusic();
        MnuMusic.Items.Add(remove);
        MnuMusic.IsEnabled = model is not null;
    }

    private async Task AddMusicClickedAsync()
    {
        if (_model is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Get("editor.music.add"),
            AllowMultiple = false
        }).ConfigureAwait(true);
        if (files.Select(file => file.TryGetLocalPath()).FirstOrDefault(path => path is not null) is { } path) await AddMusicAsync(path).ConfigureAwait(true);
    }
}
