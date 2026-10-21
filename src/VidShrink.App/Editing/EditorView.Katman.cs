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
    internal static readonly double[] LayerSizes = { 0.25, 0.33, 0.5, 1 };

    internal static readonly (string Key, double X, double Y)[] LayerPositions =
    {
        ("editor.layer.position.top-left", 0.05, 0.05),
        ("editor.layer.position.top-right", 0.95, 0.05),
        ("editor.layer.position.bottom-left", 0.05, 0.95),
        ("editor.layer.position.bottom-right", VideoLayer.DefaultX, VideoLayer.DefaultY),
        ("editor.layer.position.center", 0.5, 0.5)
    };

    private void InitLayers()
    {
        Timeline.LayerMoved += (index, start) => MoveLayer(index, start);
        TimelineMenu.Opening += (_, _) => BuildLayerMenu();
    }

    internal int TargetLayer()
    {
        if (_model is not { HasLayers: true } model) return -1;
        if (Timeline.SelectedLayer >= 0 && Timeline.SelectedLayer < model.Layers.Count) return Timeline.SelectedLayer;
        var at = Timeline.Playhead;
        for (var i = model.Layers.Count - 1; i >= 0; i--)
            if (at >= model.Layers[i].Start && at < model.Layers[i].End) return i;
        return model.Layers.Count - 1;
    }

    internal bool PlaceLayer(string path, MediaInfo info)
    {
        if (_model is not { Duration: > 0 } model || Exporting) return false;
        var duration = EditTime.FromSeconds(info.DurationSeconds);
        if (duration <= 0 || info.Width <= 0 || info.Height <= 0) return false;
        var start = Timeline.RoundToFrame(Timeline.Playhead);
        if (start >= model.Duration - (long)Math.Ceiling(Timeline.FrameTicks)) start = 0;
        var layer = new VideoLayer(path, duration, Math.Max(0, start));
        var index = model.Layers.Count;
        if (!Apply(m => m.SetLayers(m.Layers.Append(layer).ToArray()), -1, reload: true)) return false;
        Timeline.SelectedLayer = index;
        ShowProjectStatus("StatusSuccess", string.Format(Strings.Culture, Strings.Get("editor.layer.placed"), Path.GetFileName(path)));
        return true;
    }

    internal async Task<bool> AddLayerAsync(string path)
    {
        if (_model is null || Exporting) return false;
        var model = _model;
        var info = await ProbeSourceAsync(path).ConfigureAwait(true);
        if (!ReferenceEquals(model, _model) || Exporting) return false;
        if (info is { IsAudioOnly: false } && PlaceLayer(path, info)) return true;
        ShowProjectStatus("StatusError", string.Format(Strings.Culture, Strings.Get("editor.layer.failed"), Path.GetFileName(path)));
        return false;
    }

    internal bool ChangeLayer(int index, Func<VideoLayer, VideoLayer> change)
    {
        if (_model is not { } model || Exporting || index < 0 || index >= model.Layers.Count) return false;
        var after = change(model.Layers[index]);
        return Apply(m => m.UpdateLayer(index, after), -1, reload: true);
    }

    internal bool MoveLayer(int index, long start)
    {
        if (_model is not { } model || index < 0 || index >= model.Layers.Count) return false;
        var at = Math.Clamp(start, 0, Math.Max(0, model.Duration - 1));
        return ChangeLayer(index, layer => layer with { Start = at });
    }

    internal bool RemoveLayer(int index)
    {
        if (_model is not { } model || Exporting || index < 0 || index >= model.Layers.Count) return false;
        if (!Apply(m =>
            {
                m.DeleteLayer(index);
                return true;
            }, -1, reload: true)) return false;
        Timeline.SelectedLayer = -1;
        return true;
    }

    private void BuildLayerMenu()
    {
        MnuLayer.Items.Clear();
        var model = _model;
        var idle = model is not null && !Exporting;
        var index = TargetLayer();
        var layer = index >= 0 ? model!.Layers[index] : null;
        var some = idle && layer is not null;

        var add = new MenuItem { Header = Strings.Get("editor.layer.add"), IsEnabled = idle };
        add.Click += (_, _) => _ = AddLayerClickedAsync();
        MnuLayer.Items.Add(add);

        var sizes = new MenuItem { Header = Strings.Get("editor.layer.size"), IsEnabled = some };
        foreach (var preset in LayerSizes)
        {
            var value = preset;
            var item = new MenuItem
            {
                Header = value.ToString("P0", Strings.Culture),
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = layer is not null && Math.Abs(layer.Scale - value) < 0.005
            };
            item.Click += (_, _) => ChangeLayer(index, l => l with { Scale = value });
            sizes.Items.Add(item);
        }

        MnuLayer.Items.Add(sizes);

        var positions = new MenuItem { Header = Strings.Get("editor.layer.position"), IsEnabled = some };
        foreach (var (key, x, y) in LayerPositions)
        {
            var (px, py) = (x, y);
            var item = new MenuItem
            {
                Header = Strings.Get(key),
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = layer is not null && Math.Abs(layer.X - px) < 0.005 && Math.Abs(layer.Y - py) < 0.005
            };
            item.Click += (_, _) => ChangeLayer(index, l => l with { X = px, Y = py });
            positions.Items.Add(item);
        }

        MnuLayer.Items.Add(positions);

        var green = new MenuItem
        {
            Header = Strings.Get("editor.layer.green"),
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = layer?.ChromaKey == true,
            IsEnabled = some
        };
        green.Click += (_, _) => ChangeLayer(index, l => l with { ChromaKey = !l.ChromaKey });
        MnuLayer.Items.Add(green);

        var here = new MenuItem { Header = Strings.Get("editor.layer.to-playhead"), IsEnabled = some };
        here.Click += (_, _) => MoveLayer(index, Timeline.RoundToFrame(Timeline.Playhead));
        MnuLayer.Items.Add(here);

        var remove = new MenuItem { Header = Strings.Get("editor.layer.remove"), IsEnabled = some };
        remove.Click += (_, _) => RemoveLayer(index);
        MnuLayer.Items.Add(remove);
        MnuLayer.IsEnabled = model is not null;
    }

    private async Task AddLayerClickedAsync()
    {
        if (_model is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Get("editor.layer.add"),
            AllowMultiple = false
        }).ConfigureAwait(true);
        if (files.Select(file => file.TryGetLocalPath()).FirstOrDefault(path => path is not null) is { } path) await AddLayerAsync(path).ConfigureAwait(true);
    }
}
