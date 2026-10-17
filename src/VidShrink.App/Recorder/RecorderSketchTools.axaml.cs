using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

/// <summary>
/// later.md #42, EKB-04: çizimin araç paneli. Kalem, ok, kutu, numara ve yazı; dört mürekkep; geri al, temizle, kapat.
/// Çizim penceresinden ayrı penceredir: çizim kayda girer, panel <see cref="CaptureAffinity"/> ile kayıttan çıkarılır ve
/// kayıt bölgesinin dışına, üstüne konur (yer yoksa altına, o da yoksa içine). Seçim modelden okunur; tuşla seçilen araç
/// da panelde işaretlenir. Panele basılan tuş çizime gider, düğmeye basıldıktan sonra odak çizime döner.
/// </summary>
internal sealed partial class RecorderSketchTools : Window
{
    private readonly RecorderSketch _sketch;
    private readonly (ToggleButton Button, SketchTool Tool)[] _tools;
    private readonly (ToggleButton Button, SketchInk Ink)[] _inks;

    public RecorderSketchTools() : this(new RecorderSketch())
    {
    }

    internal RecorderSketchTools(RecorderSketch sketch)
    {
        InitializeComponent();
        _sketch = sketch;
        _tools = new[]
        {
            (BtnPen, SketchTool.Pen), (BtnArrow, SketchTool.Arrow), (BtnBox, SketchTool.Box),
            (BtnNumber, SketchTool.Number), (BtnText, SketchTool.Text)
        };
        _inks = new[] { (BtnEmber, SketchInk.Ember), (BtnBlue, SketchInk.Blue), (BtnPink, SketchInk.Pink), (BtnGreen, SketchInk.Green) };
        foreach (var (button, tool) in _tools) button.Click += (_, _) => Choose(tool);
        for (var i = 0; i < _inks.Length; i++)
        {
            var (button, ink) = _inks[i];
            var name = string.Format(Strings.Culture, LanguageCatalog.Display(Strings.Get("recorder.sketch.ink")), i + 1);
            AutomationProperties.SetName(button, name);
            ToolTip.SetTip(button, name);
            button.Click += (_, _) => Choose(ink);
        }

        BtnUndo.Click += (_, _) => Back(() => _sketch.Sketch.Undo());
        BtnClear.Click += (_, _) => Back(_sketch.Sketch.Clear);
        BtnClose.Click += (_, _) => _sketch.Press(Key.Escape, KeyModifiers.None);
        KeyDown += (_, e) => e.Handled = _sketch.Press(e.Key, e.KeyModifiers);
        _sketch.Sketch.Selected += Sync;
        Opened += (_, _) =>
        {
            CaptureExcluded = ExcludeFromCapture && CaptureAffinity.Supported
                              && TryGetPlatformHandle() is { } handle && CaptureAffinity.Exclude(handle.Handle);
        };
        Closed += (_, _) => _sketch.Sketch.Selected -= Sync;
        Sync();
    }

    internal bool ExcludeFromCapture { get; init; } = true;

    /// <summary>Panel yakalamadan çıkarıldı mı; çağrı yapılmadıysa ya da düştüyse yanlış.</summary>
    internal bool CaptureExcluded { get; private set; }

    internal IReadOnlyList<ToggleButton> ToolButtons => _tools.Select(t => t.Button).ToArray();

    internal IReadOnlyList<ToggleButton> InkButtons => _inks.Select(t => t.Button).ToArray();

    internal ToggleButton ButtonOf(SketchTool tool) => _tools.First(t => t.Tool == tool).Button;

    internal ToggleButton ButtonOf(SketchInk ink) => _inks.First(t => t.Ink == ink).Button;

    internal Button UndoButton => BtnUndo;

    internal Button ClearButton => BtnClear;

    internal Button CloseButton => BtnClose;

    internal void Choose(SketchTool tool) => Back(() => _sketch.Sketch.Tool = tool);

    internal void Choose(SketchInk ink) => Back(() => _sketch.Sketch.Ink = ink);

    /// <summary>Paneli kayıt bölgesinin dışına koyar; <paramref name="area"/> ekran pikseli.</summary>
    internal void Place(PixelRect area)
    {
        var screen = Screens.ScreenFromPoint(area.Center) ?? Screens.Primary;
        var scaling = screen?.Scaling ?? 1;
        Measure(Size.Infinity);
        var bar = new PixelSize((int)Math.Ceiling(DesiredSize.Width * scaling), (int)Math.Ceiling(DesiredSize.Height * scaling));
        var gap = (int)Math.Round((this.TryFindResource("RecorderRegionToolbarGap", out var value) && value is double d ? d : 0) * scaling);
        Position = RegionEdit.Toolbar(area, bar, gap, screen?.Bounds ?? area);
    }

    private void Back(Action act)
    {
        act();
        Sync();
        _sketch.Activate();
        _sketch.Focus();
    }

    private void Sync()
    {
        foreach (var (button, tool) in _tools) button.IsChecked = _sketch.Sketch.Tool == tool;
        foreach (var (button, ink) in _inks) button.IsChecked = _sketch.Sketch.Ink == ink;
    }
}
