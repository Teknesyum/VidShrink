using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    internal Func<double?, Task<PixelRect?>> DrawRegion { get; set; } = RecorderRegionPicker.PickAsync;

    internal Func<IReadOnlyList<string>> ListWindows { get; set; } = RecorderWindows.Titles;

    internal Func<IReadOnlyList<ScreenBounds>>? ScreenSource { get; set; }

    private void InitSecici()
    {
        CmbAspect.ItemsSource = AspectLabels();
        CmbAspect.SelectedIndex = Math.Max(0, Array.IndexOf(RegionDraw.Aspects, _settings.RegionAspect));
        CmbRegionSize.ItemsSource = SizeLabels();
        CmbRegionSize.SelectedIndex = 0;
        RefreshScreens();

        BtnDrawRegion.Click += async (_, _) => await DrawRegionAsync();
        BtnWindowRefresh.Click += (_, _) => RefreshWindowList();
        CmbWindow.SelectionChanged += (_, _) =>
        {
            if (CmbWindow.SelectedItem is string title) TxtWindowTitle.Text = title;
        };
        CmbRegionSize.SelectionChanged += (_, _) => ApplyPresetSize();
        CmbAspect.SelectionChanged += (_, _) => PersistChoices();
        CmbScreen.SelectionChanged += (_, _) => PersistChoices();
    }

    internal string SelectedAspect
        => CmbAspect.SelectedIndex >= 0 && CmbAspect.SelectedIndex < RegionDraw.Aspects.Length
            ? RegionDraw.Aspects[CmbAspect.SelectedIndex]
            : RegionDraw.Free;

    private static List<string> AspectLabels()
        => RegionDraw.Aspects.Select(a => a == RegionDraw.Free ? Say("recorder.target.aspect-free") : a).ToList();

    private static List<string> SizeLabels()
        => new[] { Say("recorder.target.size-none") }
            .Concat(RegionDraw.Sizes.Select(s => string.Format(CultureInfo.InvariantCulture, "{0} × {1}", s.Width, s.Height)))
            .ToList();

    private void RefreshSeciciLabels()
    {
        var aspect = CmbAspect.SelectedIndex;
        CmbAspect.ItemsSource = AspectLabels();
        CmbAspect.SelectedIndex = aspect;
        CmbRegionSize.ItemsSource = SizeLabels();
        CmbRegionSize.SelectedIndex = 0;
        RefreshScreens();
    }

    internal IReadOnlyList<ScreenBounds> Monitors() => ScreenSource?.Invoke() ?? MonitorBounds();

    internal void RefreshScreens()
        => Quietly(() =>
        {
            var monitors = Monitors();
            var selected = CmbScreen.SelectedIndex >= 0 ? CmbScreen.SelectedIndex : _settings.ScreenIndex;
            CmbScreen.ItemsSource = monitors
                .Select(m => Say("recorder.target.screen-item", m.Index + 1, m.Width, m.Height))
                .ToList();
            CmbScreen.SelectedIndex = monitors.Count == 0 ? -1 : Math.Clamp(selected, 0, monitors.Count - 1);
        });

    private int ChosenScreen => CmbScreen.SelectedIndex >= 0 ? CmbScreen.SelectedIndex : _settings.ScreenIndex;

    internal void RefreshWindowList()
        => Quietly(() =>
        {
            IReadOnlyList<string> titles;
            try { titles = ListWindows(); }
            catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
            {
                titles = Array.Empty<string>();
            }

            CmbWindow.ItemsSource = titles;
            CmbWindow.SelectedIndex = TxtWindowTitle.Text is { Length: > 0 } current
                ? titles.ToList().IndexOf(current)
                : -1;
        });

    internal async Task<bool> DrawRegionAsync()
    {
        if (_session is not null || CountingDown) return false;

        var host = TopLevel.GetTopLevel(this) as Window;
        var state = host?.WindowState ?? WindowState.Normal;
        if (host is not null) host.WindowState = WindowState.Minimized;

        PixelRect? drawn;
        try { drawn = await DrawRegion(RegionDraw.Ratio(SelectedAspect)); }
        finally
        {
            if (host is not null) host.WindowState = state;
        }

        if (drawn is not { } rect || !RegionDraw.Usable(rect)) return false;

        rect = new PixelRect(rect.X, rect.Y, rect.Width - rect.Width % 2, rect.Height - rect.Height % 2);
        Quietly(() =>
        {
            TxtRegionX.Text = rect.X.ToString(CultureInfo.InvariantCulture);
            TxtRegionY.Text = rect.Y.ToString(CultureInfo.InvariantCulture);
            TxtRegionWidth.Text = rect.Width.ToString(CultureInfo.InvariantCulture);
            TxtRegionHeight.Text = rect.Height.ToString(CultureInfo.InvariantCulture);
        });
        CmbTarget.SelectedIndex = (int)RecorderTargetKind.Region;
        StoreChoices();
        return true;
    }

    private void ApplyPresetSize()
    {
        var index = CmbRegionSize.SelectedIndex - 1;
        if (index < 0 || index >= RegionDraw.Sizes.Length) return;
        var (width, height) = RegionDraw.Sizes[index];
        Quietly(() =>
        {
            TxtRegionWidth.Text = width.ToString(CultureInfo.InvariantCulture);
            TxtRegionHeight.Text = height.ToString(CultureInfo.InvariantCulture);
        });
        StoreChoices();
    }
}
