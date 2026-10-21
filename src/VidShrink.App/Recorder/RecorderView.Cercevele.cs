using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    internal const int FrameBackdropCount = 6;

    internal const int FrameSolidIndex = 0;

    internal const int FrameImageIndex = 5;

    internal const int FrameThreads = 2;

    internal static readonly (string From, string To)[] FrameGradients =
    [
        ("NeonBlueColor", "NeonPurpleColor"),
        ("NeonPinkColor", "NeonPurpleColor"),
        ("EmberFlameColor", "EmberBlazeColor"),
        ("NeonSuccessColor", "NeonBlueColor")
    ];

    internal const string FrameSolidColor = "SurfaceToneColor";

    private Button? _btnFrame;
    private ComboBox? _cmbFrameBackdrop;
    private ComboBox? _cmbFramePadding;
    private ComboBox? _cmbFrameCorner;
    private CheckBox? _chkFrameShadow;
    private Border? _frameSwatch;
    private Button? _btnFrameImage;
    private SarilanMetin? _txtFrameImage;
    private Button? _btnFrameApply;

    internal Func<string, CancellationToken, Task<(int Width, int Height)>> FrameProbe { get; set; } = ProbeFrameSourceAsync;

    internal Func<IReadOnlyList<string>, CancellationToken, Task<FfmpegRun>> FrameRun { get; set; } = FfmpegRunner.RunAsync;

    internal Func<Task<string?>>? FramePickImage { get; set; }

    internal bool FrameVisible => _btnFrame?.IsVisible == true;

    internal Button? FrameButton => _btnFrame;

    internal static async Task<(int Width, int Height)> ProbeFrameSourceAsync(string path, CancellationToken ct)
    {
        var info = await FfprobeClient.ProbeAsync(path, ct).ConfigureAwait(false);
        return (info.Width, info.Height);
    }

    private void InitCercevele()
    {
        if (BtnQuickTrim.Parent is not Panel row) return;

        _cmbFrameBackdrop = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var key in FrameBackdropKeys()) _cmbFrameBackdrop.Items.Add(LocalizedItem(key));
        _cmbFrameBackdrop.SelectedIndex = Math.Clamp(_settings.FrameBackdrop, 0, FrameBackdropCount - 1);
        FrameName(_cmbFrameBackdrop, "recorder.output.frame-backdrop");

        _cmbFramePadding = FrameChoices(RecordingFrame.PaddingChoices, _settings.FramePadding, "recorder.output.frame-padding");
        _cmbFrameCorner = FrameChoices(RecordingFrame.CornerChoices, _settings.FrameCorner, "recorder.output.frame-corner");

        _chkFrameShadow = new CheckBox { IsChecked = _settings.FrameShadow };
        _chkFrameShadow.Bind(ContentControl.ContentProperty, LocalizedText.For("recorder.output.frame-shadow").Binding);
        FrameName(_chkFrameShadow, "recorder.output.frame-shadow");

        _frameSwatch = new Border
        {
            Height = OverlayPlace.Size(this, "RecorderQuickTrimThumbHeight"),
            CornerRadius = EditorTokens.Find(this, "RadiusControl") is CornerRadius radius ? radius : default
        };

        _btnFrameImage = new Button { Theme = EditorTokens.Theme(this, "GhostButton") };
        _btnFrameImage.Bind(ContentControl.ContentProperty, LocalizedText.For("recorder.output.frame-image-pick").Binding);
        FrameName(_btnFrameImage, "recorder.output.frame-image-pick");
        _txtFrameImage = new SarilanMetin { TextWrapping = TextWrapping.Wrap, Theme = EditorTokens.Theme(this, "TipText") };

        _btnFrameApply = new Button { Theme = EditorTokens.Theme(this, "PrimaryButton") };
        _btnFrameApply.Bind(ContentControl.ContentProperty, LocalizedText.For("recorder.output.frame-apply").Binding);
        FrameName(_btnFrameApply, "recorder.output.frame-apply");

        var panel = new StackPanel { Spacing = OverlayPlace.Size(this, "SpaceSm"), Width = OverlayPlace.Size(this, "RecorderQuickTrimWidth") };
        panel.Children.Add(FrameLabel("recorder.output.frame-backdrop"));
        panel.Children.Add(_cmbFrameBackdrop);
        panel.Children.Add(_frameSwatch);
        panel.Children.Add(_btnFrameImage);
        panel.Children.Add(_txtFrameImage);
        panel.Children.Add(FrameLabel("recorder.output.frame-padding"));
        panel.Children.Add(_cmbFramePadding);
        panel.Children.Add(FrameLabel("recorder.output.frame-corner"));
        panel.Children.Add(_cmbFrameCorner);
        panel.Children.Add(_chkFrameShadow);
        panel.Children.Add(_btnFrameApply);

        var flyout = new Flyout { Content = panel };
        flyout.Opened += (_, _) => RefreshFrameControls();

        _btnFrame = new Button
        {
            Theme = EditorTokens.Theme(this, "GhostButton"),
            Margin = EditorTokens.Find(this, "WrapItemMargin") is Thickness margin ? margin : default,
            IsVisible = false,
            Flyout = flyout
        };
        _btnFrame.Bind(ContentControl.ContentProperty, LocalizedText.For("recorder.output.frame").Binding);
        FrameName(_btnFrame, "recorder.output.frame");
        var tip = new SarilanMetin { Theme = EditorTokens.Theme(this, "TipText") };
        tip.Bind(TextBlock.TextProperty, LocalizedText.For("recorder.output.frame.tip").Binding);
        ToolTip.SetTip(_btnFrame, tip);

        row.Children.Insert(row.Children.IndexOf(BtnQuickTrim) + 1, _btnFrame);

        _cmbFrameBackdrop.SelectionChanged += (_, _) => { RememberFrame(); RefreshFrameControls(); };
        _cmbFramePadding.SelectionChanged += (_, _) => RememberFrame();
        _cmbFrameCorner.SelectionChanged += (_, _) => RememberFrame();
        _chkFrameShadow.IsCheckedChanged += (_, _) => RememberFrame();
        _btnFrameImage.Click += async (_, _) => await PickFrameImageAsync();
        _btnFrameApply.Click += async (_, _) => await FrameRecordingAsync();
        RefreshFrameControls();
    }

    private void ResetCercevele(RecordResult result)
    {
        if (_btnFrame is null) return;
        _btnFrame.IsVisible = result.Ok && !result.Partial
                              && RecorderArguments.ContainerOf(result.OutputPath) != RecorderContainer.Gif;
    }

    internal static IReadOnlyList<string> FrameBackdropKeys()
        =>
        [
            "recorder.output.frame-solid",
            "recorder.output.frame-gradient-1",
            "recorder.output.frame-gradient-2",
            "recorder.output.frame-gradient-3",
            "recorder.output.frame-gradient-4",
            "recorder.output.frame-image"
        ];

    internal int FrameBackdropIndex
    {
        get => _cmbFrameBackdrop?.SelectedIndex ?? _settings.FrameBackdrop;
        set { if (_cmbFrameBackdrop is not null) _cmbFrameBackdrop.SelectedIndex = value; }
    }

    internal bool FrameApplyEnabled => _btnFrameApply?.IsEnabled == true;

    internal bool FrameImagePickVisible => _btnFrameImage?.IsVisible == true;

    internal IBrush? FrameSwatchBrush => _frameSwatch?.Background;

    internal string? FrameImage
    {
        get => _settings.FrameImage;
        set { _settings.FrameImage = value; RefreshFrameControls(); }
    }

    internal FrameOptions? PendingFrame()
    {
        var index = FrameBackdropIndex;
        FrameBackdrop backdrop;
        if (index == FrameImageIndex)
        {
            if (string.IsNullOrWhiteSpace(_settings.FrameImage) || !File.Exists(_settings.FrameImage)) return null;
            backdrop = FrameBackdrop.Picture(_settings.FrameImage);
        }
        else if (index == FrameSolidIndex)
        {
            if (PaletteHex(FrameSolidColor) is not { } solid) return null;
            backdrop = FrameBackdrop.Solid(solid);
        }
        else if (index > 0 && index <= FrameGradients.Length)
        {
            var (from, to) = FrameGradients[index - 1];
            if (PaletteHex(from) is not { } a || PaletteHex(to) is not { } b) return null;
            backdrop = FrameBackdrop.Gradient(a, b);
        }
        else
        {
            return null;
        }

        return new FrameOptions(_settings.FramePadding, _settings.FrameCorner, _settings.FrameShadow, backdrop);
    }

    internal async Task<string?> FrameRecordingAsync()
    {
        if (Delivered() is not { } source || PendingFrame() is not { } options) return null;

        if (_btnFrameApply is not null) _btnFrameApply.IsEnabled = false;
        if (_btnFrame?.Flyout is Flyout open) open.Hide();
        ClearNoticeAndError();
        ShowNotice(Say("recorder.output.frame-running"));
        var target = RecordingFrame.TargetFor(source, File.Exists);
        string? error = null;
        try
        {
            var (width, height) = await FrameProbe(source, CancellationToken.None);
            var run = await FrameRun(RecordingFrame.Arguments(source, target, width, height, options, FrameThreads), CancellationToken.None);
            if (!run.Ok) error = FrameLastLine(run.StandardError);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = ex.Message;
        }
        finally
        {
            RefreshFrameControls();
        }

        ClearNoticeAndError();
        if (error is not null)
        {
            ShowError(Say("recorder.output.frame-failed", error));
            return null;
        }

        ShowNotice(Say("recorder.output.frame-done", target));
        return target;
    }

    private async Task PickFrameImageAsync()
    {
        var pick = FramePickImage ?? PickFrameImageFromDiskAsync;
        if (await pick() is { } path) FrameImage = path;
        _settings.Save(_settingsPath);
    }

    private async Task<string?> PickFrameImageFromDiskAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Say("recorder.output.frame-image-pick"),
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.ImageAll]
        }).ConfigureAwait(true);
        return files.Select(file => file.TryGetLocalPath()).FirstOrDefault(path => path is not null);
    }

    private void RememberFrame()
    {
        if (_cmbFrameBackdrop is null || _cmbFramePadding is null || _cmbFrameCorner is null || _chkFrameShadow is null) return;
        if (_cmbFrameBackdrop.SelectedIndex >= 0) _settings.FrameBackdrop = _cmbFrameBackdrop.SelectedIndex;
        if (_cmbFramePadding.SelectedIndex >= 0) _settings.FramePadding = RecordingFrame.PaddingChoices[_cmbFramePadding.SelectedIndex];
        if (_cmbFrameCorner.SelectedIndex >= 0) _settings.FrameCorner = RecordingFrame.CornerChoices[_cmbFrameCorner.SelectedIndex];
        _settings.FrameShadow = _chkFrameShadow.IsChecked == true;
        _settings.Save(_settingsPath);
    }

    private void RefreshFrameControls()
    {
        if (_frameSwatch is null || _btnFrameImage is null || _txtFrameImage is null || _btnFrameApply is null) return;
        var index = FrameBackdropIndex;
        var image = index == FrameImageIndex;
        _btnFrameImage.IsVisible = image;
        _txtFrameImage.IsVisible = image;
        _txtFrameImage.Text = image
            ? (string.IsNullOrWhiteSpace(_settings.FrameImage) ? Say("recorder.output.frame-image-none") : Path.GetFileName(_settings.FrameImage))
            : null;
        _frameSwatch.IsVisible = !image;
        _frameSwatch.Background = SwatchBrush(index);
        _btnFrameApply.IsEnabled = PendingFrame() is not null;
    }

    private IBrush? SwatchBrush(int index)
    {
        if (index == FrameSolidIndex) return PaletteColor(FrameSolidColor) is { } solid ? new SolidColorBrush(solid) : null;
        if (index <= 0 || index > FrameGradients.Length) return null;
        var (from, to) = FrameGradients[index - 1];
        if (PaletteColor(from) is not { } a || PaletteColor(to) is not { } b) return null;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = [new GradientStop(a, 0), new GradientStop(b, 1)]
        };
    }

    private Color? PaletteColor(string key) => EditorTokens.Find(this, key) is Color color ? color : null;

    private string? PaletteHex(string key)
        => PaletteColor(key) is { } c ? "#" + c.R.ToString("X2", CultureInfo.InvariantCulture) + c.G.ToString("X2", CultureInfo.InvariantCulture) + c.B.ToString("X2", CultureInfo.InvariantCulture) : null;

    private SarilanMetin FrameLabel(string key)
    {
        var label = new SarilanMetin { Theme = EditorTokens.Theme(this, "Label") };
        label.Bind(TextBlock.TextProperty, LocalizedText.For(key).Binding);
        return label;
    }

    private static ComboBoxItem LocalizedItem(string key)
    {
        var item = new ComboBoxItem();
        item.Bind(ContentControl.ContentProperty, LocalizedText.For(key).Binding);
        return item;
    }

    private static ComboBox FrameChoices(int[] values, int selected, string nameKey)
    {
        var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var value in values) box.Items.Add(new ComboBoxItem { Content = value.ToString(CultureInfo.InvariantCulture) + " px" });
        box.SelectedIndex = Math.Max(0, Array.IndexOf(values, selected));
        FrameName(box, nameKey);
        return box;
    }

    private static void FrameName(Control control, string key)
        => control.Bind(AutomationProperties.NameProperty, LocalizedText.For(key).Binding);

    private static string FrameLastLine(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length == 0 ? string.Empty : lines[^1];
    }
}
