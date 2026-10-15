using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Media;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private const string OverlayFileName = "overlay.ass";
    private const int FallbackWidth = 1920;
    private const int FallbackHeight = 1080;
    private static readonly long DefaultTextLength = EditTime.TicksPerSecond * 3;
    private static readonly long DefaultMotionLength = EditTime.TicksPerSecond / 2;
    private static readonly string[] MotionKeys = { "editor.text.motion.none", "editor.text.motion.left", "editor.text.motion.right", "editor.text.motion.bottom", "editor.text.motion.top", "editor.text.motion.typewriter" };
    private const double DragPad = 0.02;

    private int _dragIndex = -1;
    private TextLayer? _dragOrigin;
    private TextLayer? _dragText;
    private (double X, double Y) _dragGrab;

    private long _overlayId;
    private string? _overlayFonts;
    private string? _overlayRoot;
    private bool _showingText;
    private List<string>? _fonts;

    /// <summary>Onizleme katmaninin klasoru; testler kendi <c>.calisma</c> klasorunu verir.</summary>
    internal string OverlayRoot
    {
        get => _overlayRoot ??= Path.Combine(Path.GetTempPath(), "vidshrink_text_" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        set => _overlayRoot = value;
    }

    internal string OverlayPath => Path.Combine(OverlayRoot, OverlayFileName);

    internal long OverlayId => _overlayId;

    internal int OverlayWrites { get; private set; }

    internal int OverlayReloads { get; private set; }

    private void InitText()
    {
        BtnAddText.Click += (_, _) => AddText();
        Timeline.TextEdited += () => AfterTextEdit(Timeline.SelectedText);
        Timeline.TextSelectionChanged += ShowTextPanel;
        TxtTextContent.LostFocus += (_, _) => CommitTextContent();
        TxtTextContent.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.Control, CommitTextContent);
        TxtTextSize.LostFocus += (_, _) => CommitTextSize();
        TxtTextSize.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, CommitTextSize);
        TxtTextColor.LostFocus += (_, _) => CommitTextColor();
        TxtTextColor.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, CommitTextColor);
        CmbTextFont.SelectionChanged += (_, _) => { if (_showingText && CmbTextFont.SelectedItem is string font) UpdateSelectedText(t => t with { FontName = font }); };
        BtnTextBold.Click += (_, _) => { if (_showingText) UpdateSelectedText(t => t with { Bold = BtnTextBold.IsChecked == true }); };
        BtnTextItalic.Click += (_, _) => { if (_showingText) UpdateSelectedText(t => t with { Italic = BtnTextItalic.IsChecked == true }); };
        BtnTextBox.Click += (_, _) => { if (_showingText) UpdateSelectedText(t => t with { Box = BtnTextBox.IsChecked == true }); };
        TxtTextFadeIn.LostFocus += (_, _) => CommitTextFade(true);
        TxtTextFadeIn.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, () => CommitTextFade(true));
        TxtTextFadeOut.LostFocus += (_, _) => CommitTextFade(false);
        TxtTextFadeOut.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, () => CommitTextFade(false));
        TxtTextOutline.LostFocus += (_, _) => CommitTextOutline();
        TxtTextOutline.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, CommitTextOutline);
        TxtTextShadow.LostFocus += (_, _) => CommitTextShadow();
        TxtTextShadow.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, CommitTextShadow);
        TxtTextX.LostFocus += (_, _) => CommitTextPosition();
        TxtTextX.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, CommitTextPosition);
        TxtTextY.LostFocus += (_, _) => CommitTextPosition();
        TxtTextY.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, CommitTextPosition);
        BtnTextKeyframe.Click += (_, _) => { if (_showingText) ToggleTextKeyframe(BtnTextKeyframe.IsChecked == true); };
        foreach (var combo in new[] { CmbTextMotionIn, CmbTextMotionOut })
        {
            combo.Items.Clear();
            foreach (var key in MotionKeys) combo.Items.Add(new ComboBoxItem { Content = Strings.Get(key) });
        }

        CmbTextMotionIn.SelectionChanged += (_, _) => { if (_showingText && CmbTextMotionIn.SelectedIndex >= 0) CommitTextMotion(true); };
        CmbTextMotionOut.SelectionChanged += (_, _) => { if (_showingText && CmbTextMotionOut.SelectedIndex >= 0) CommitTextMotion(false); };
        TxtTextMotionIn.LostFocus += (_, _) => CommitTextMotionLength(true);
        TxtTextMotionIn.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, () => CommitTextMotionLength(true));
        TxtTextMotionOut.LostFocus += (_, _) => CommitTextMotionLength(false);
        TxtTextMotionOut.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, () => CommitTextMotionLength(false));
        AddHandler(PointerPressedEvent, OnMonitorTextPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnMonitorTextMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnMonitorTextReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, (_, _) => EndTextDrag(), RoutingStrategies.Direct | RoutingStrategies.Bubble, true);
        ShowTextPanel();
    }

    private long TextOffset(TextLayer text) => Math.Clamp(Timeline.Playhead - text.Start, 0, text.Length);

    private static bool TryReadPercent(string? input, out double fraction)
    {
        fraction = 0;
        if (!TryReadAmount(input, out var value) || value is not { } percent) return false;
        fraction = Math.Clamp(Math.Round(percent, 1), 0, 100) / 100;
        return true;
    }

    private void CommitTextPosition()
    {
        if (!_showingText) return;
        if (!TryReadPercent(TxtTextX.Text, out var x) || !TryReadPercent(TxtTextY.Text, out var y)
            || !UpdateSelectedText(t => t.WithPositionAt(TextOffset(t), x, y)))
            ShowTextPanel();
    }

    private void ToggleTextKeyframe(bool on)
    {
        var tolerance = Timeline.KeyframeTolerance;
        if (!UpdateSelectedText(t => on ? t.WithKeyframeAt(TextOffset(t)) : t.WithoutKeyframeNear(TextOffset(t), tolerance))) ShowTextPanel();
    }

    private void CommitTextMotion(bool entrance)
    {
        var motion = (TextMotion)(entrance ? CmbTextMotionIn : CmbTextMotionOut).SelectedIndex;
        if (!UpdateSelectedText(t =>
            {
                var room = t.Length - (entrance ? t.MotionOutLength : t.MotionInLength);
                var current = entrance ? t.MotionInLength : t.MotionOutLength;
                var length = motion == TextMotion.None ? 0 : Math.Min(current > 0 ? current : DefaultMotionLength, room);
                return entrance ? t with { MotionIn = motion, MotionInLength = length } : t with { MotionOut = motion, MotionOutLength = length };
            }))
            ShowTextPanel();
    }

    private void CommitTextMotionLength(bool entrance)
    {
        if (!_showingText) return;
        if (!TryReadAmount((entrance ? TxtTextMotionIn : TxtTextMotionOut).Text, out var seconds) || seconds is null)
        {
            ShowTextPanel();
            return;
        }

        var ticks = EditTime.FromSeconds(Math.Round(seconds.Value, 2));
        if (!UpdateSelectedText(t => entrance
                ? t with { MotionInLength = t.MotionIn == TextMotion.None ? 0 : Math.Min(ticks, t.Length - t.MotionOutLength) }
                : t with { MotionOutLength = t.MotionOut == TextMotion.None ? 0 : Math.Min(ticks, t.Length - t.MotionInLength) }))
            ShowTextPanel();
    }

    /// <summary>Oynatma basi degisince konum kutulari ve anahtar kare dugmesi o ana esitlenir.</summary>
    private void ShowTextPosition(bool force = false)
    {
        var index = Timeline.SelectedText;
        if (!_showingText || _model is not { } model || index < 0 || index >= model.Texts.Count) return;
        var text = _dragIndex == index && _dragText is { } dragged ? dragged : model.Texts[index];
        var offset = TextOffset(text);
        var (x, y) = text.KeyframePositionAt(offset);
        if (force || !TxtTextX.IsFocused) TxtTextX.Text = (x * 100).ToString("0.#", Strings.Culture);
        if (force || !TxtTextY.IsFocused) TxtTextY.Text = (y * 100).ToString("0.#", Strings.Culture);
        BtnTextKeyframe.IsChecked = text.HasKeyframeNear(offset, Timeline.KeyframeTolerance);
    }

    /// <summary>
    /// Monitorde secili metne basilirsa surukleme baslar. Kesirler karenin genisligine ve
    /// yuksekligine goredir; isabet kutusu yazi boyutundan kestirilir.
    /// </summary>
    internal bool BeginTextDrag(double fx, double fy)
    {
        var index = Timeline.SelectedText;
        if (!_showingText || _model is not { } model || index < 0 || index >= model.Texts.Count) return false;
        var text = model.Texts[index];
        if (!text.Covers(Timeline.Playhead)) return false;
        var offset = TextOffset(text);
        var (width, height) = Preview.Engine is { } engine ? FrameSize(engine) : (FallbackWidth, FallbackHeight);
        var (sx, sy) = text.PositionAt(offset);
        var px = AssWriter.FontPixels(text, height);
        var lines = text.Text.Split('\n');
        var widest = lines.Max(l => new StringInfo(l.TrimEnd('\r')).LengthInTextElements);
        var halfWidth = widest * 0.3 * px / width + DragPad;
        var halfHeight = lines.Length * 0.6 * px / height + DragPad;
        if (Math.Abs(fx - sx) > halfWidth || Math.Abs(fy - sy) > halfHeight) return false;

        var (kx, ky) = text.KeyframePositionAt(offset);
        _dragIndex = index;
        _dragOrigin = text;
        _dragText = null;
        _dragGrab = (fx - kx, fy - ky);
        return true;
    }

    internal bool DragTextTo(double fx, double fy)
    {
        if (_dragOrigin is not { } origin) return false;
        var x = Math.Clamp(fx - _dragGrab.X, 0, 1);
        var y = Math.Clamp(fy - _dragGrab.Y, 0, 1);
        _dragText = origin.WithPositionAt(TextOffset(origin), x, y);
        RefreshOverlay();
        ShowTextPosition();
        return true;
    }

    internal bool EndTextDrag()
    {
        if (_dragOrigin is not { } origin) return false;
        var after = _dragText;
        var index = _dragIndex;
        _dragOrigin = null;
        _dragText = null;
        _dragIndex = -1;
        if (after is null || after.Equals(origin) || index != Timeline.SelectedText || !UpdateSelectedText(_ => after))
        {
            RefreshOverlay();
            ShowTextPanel();
        }

        return true;
    }

    private bool MonitorFraction(PointerEventArgs e, out double fx, out double fy)
    {
        fx = fy = 0;
        var frame = Preview.Frame;
        var size = frame.Bounds.Size;
        if (!frame.IsVisible || size.Width <= 0 || size.Height <= 0) return false;
        var at = e.GetPosition(frame);
        fx = at.X / size.Width;
        fy = at.Y / size.Height;
        return true;
    }

    private void OnMonitorTextPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_showingText || e.Source is not Visual source || !(ReferenceEquals(source, Preview.Surface) || Preview.Surface.IsVisualAncestorOf(source))) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !MonitorFraction(e, out var fx, out var fy)) return;
        if (!BeginTextDrag(fx, fy)) return;
        e.Pointer.Capture(Preview.Surface);
        e.Handled = true;
    }

    private void OnMonitorTextMoved(object? sender, PointerEventArgs e)
    {
        if (_dragOrigin is null) return;
        if (MonitorFraction(e, out var fx, out var fy)) DragTextTo(fx, fy);
        e.Handled = true;
    }

    private void OnMonitorTextReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragOrigin is null) return;
        e.Pointer.Capture(null);
        EndTextDrag();
        e.Handled = true;
    }

    private static void CommitOnEnter(KeyEventArgs e, KeyModifiers needed, Action commit)
    {
        if (e.Key != Key.Enter || (e.KeyModifiers & needed) != needed) return;
        commit();
        e.Handled = true;
    }

    internal bool AddText()
    {
        if (_model is not { } model || model.Duration <= 0) return false;
        var length = Math.Min(DefaultTextLength, model.Duration);
        var start = Math.Clamp(Timeline.Playhead, 0, Math.Max(0, model.Duration - length));
        var index = model.AddText(new TextLayer(Strings.Get("editor.text.content"), start, start + length));
        AfterTextEdit(index);
        return true;
    }

    internal bool DeleteSelectedText()
    {
        var index = Timeline.SelectedText;
        if (_model is not { } model || index < 0 || index >= model.Texts.Count) return false;
        model.DeleteText(index);
        AfterTextEdit(Math.Min(index, model.Texts.Count - 1));
        return true;
    }

    internal bool UpdateSelectedText(Func<TextLayer, TextLayer> change)
    {
        var index = Timeline.SelectedText;
        if (_model is not { } model || index < 0 || index >= model.Texts.Count) return false;
        TextLayer after;
        try
        {
            after = change(model.Texts[index]);
            if (!model.UpdateText(index, after)) return false;
        }
        catch (ArgumentException)
        {
            ShowTextPanel();
            return false;
        }

        AfterTextEdit(index);
        return true;
    }

    private void CommitTextContent()
    {
        if (!_showingText) return;
        var text = (TxtTextContent.Text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowTextPanel();
            return;
        }

        UpdateSelectedText(t => t with { Text = text });
    }

    private void CommitTextSize()
    {
        if (!_showingText) return;
        if (!double.TryParse(TxtTextSize.Text, NumberStyles.Float, Strings.Culture, out var size)
            && !double.TryParse(TxtTextSize.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out size))
        {
            ShowTextPanel();
            return;
        }

        var clamped = Math.Clamp(Math.Round(size, 1), TextLayer.MinSize, TextLayer.MaxSize);
        if (!UpdateSelectedText(t => t with { Size = clamped })) ShowTextPanel();
    }

    private void CommitTextColor()
    {
        if (!_showingText) return;
        if (!KullaniciRengi.Coz(TxtTextColor.Text, out var rgb) || !UpdateSelectedText(t => t with { Color = rgb })) ShowTextPanel();
    }

    /// <summary>Bos girdi <c>null</c> verir; sayi degilse, sonsuzsa ya da negatifse <c>false</c>.</summary>
    private static bool TryReadAmount(string? input, out double? value)
    {
        value = null;
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text)) return true;
        if (!double.TryParse(text, NumberStyles.Float, Strings.Culture, out var number)
            && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return false;
        if (!double.IsFinite(number) || number < 0) return false;
        value = number;
        return true;
    }

    private void CommitTextFade(bool fadeIn)
    {
        if (!_showingText) return;
        if (!TryReadAmount((fadeIn ? TxtTextFadeIn : TxtTextFadeOut).Text, out var seconds))
        {
            ShowTextPanel();
            return;
        }

        var ticks = EditTime.FromSeconds(Math.Round(seconds ?? 0, 2));
        if (!UpdateSelectedText(t => fadeIn
                ? t with { FadeIn = Math.Min(ticks, t.Length - t.FadeOut) }
                : t with { FadeOut = Math.Min(ticks, t.Length - t.FadeIn) }))
            ShowTextPanel();
    }

    private void CommitTextOutline()
    {
        if (!_showingText) return;
        if (!TryReadAmount(TxtTextOutline.Text, out var outline))
        {
            ShowTextPanel();
            return;
        }

        double? value = outline is { } o ? Math.Min(Math.Round(o, 1), TextLayer.MaxOutline) : null;
        if (!UpdateSelectedText(t => t with { Outline = value })) ShowTextPanel();
    }

    private void CommitTextShadow()
    {
        if (!_showingText) return;
        if (!TryReadAmount(TxtTextShadow.Text, out var shadow))
        {
            ShowTextPanel();
            return;
        }

        var value = Math.Min(Math.Round(shadow ?? 0, 1), TextLayer.MaxShadow);
        if (!UpdateSelectedText(t => t with { Shadow = value })) ShowTextPanel();
    }

    /// <summary>
    /// Yazi tipi listesi Avalonia'nin sistem yazi tiplerinden gelir, her platformda dolar. Secili
    /// katmanin ailesi listede yoksa (baska makinede kaydedilmis proje) siraya eklenir.
    /// </summary>
    private void ShowFont(string family)
    {
        if (_fonts is null)
        {
            _fonts = FontManager.Current.SystemFonts.Select(f => f.Name)
                .Append(TextLayer.DefaultFont)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
            CmbTextFont.ItemsSource = null;
            CmbTextFont.Items.Clear();
            foreach (var font in _fonts) CmbTextFont.Items.Add(font);
        }

        var known = _fonts.FirstOrDefault(f => string.Equals(f, family, StringComparison.OrdinalIgnoreCase));
        if (known is null)
        {
            var at = _fonts.FindIndex(f => StringComparer.OrdinalIgnoreCase.Compare(f, family) > 0);
            if (at < 0) at = _fonts.Count;
            _fonts.Insert(at, family);
            CmbTextFont.Items.Insert(at, family);
            known = family;
        }

        CmbTextFont.SelectedItem = known;
    }

    private void AfterTextEdit(int select)
    {
        Timeline.RefreshTexts();
        Timeline.SelectedText = select;
        ShowTextPanel();
        RefreshToolbar();
        RefreshOverlay();
        MarkEdited();
    }

    private void ShowTextPanel()
    {
        var index = Timeline.SelectedText;
        if (_model is not { } model || index < 0 || index >= model.Texts.Count)
        {
            _showingText = false;
            TextPanel.IsVisible = false;
            return;
        }

        var text = model.Texts[index];
        _showingText = false;
        TxtTextContent.Text = text.Text;
        TxtTextSize.Text = text.Size.ToString("0.#", Strings.Culture);
        TxtTextColor.Text = KullaniciRengi.Yaz(text.Color);
        TextSwatch.Background = KullaniciRengi.Firca(text.Color);
        ShowFont(text.FontName);
        BtnTextBold.IsChecked = text.Bold;
        BtnTextItalic.IsChecked = text.Italic;
        TxtTextFadeIn.Text = EditTime.ToSeconds(text.FadeIn).ToString("0.##", Strings.Culture);
        TxtTextFadeOut.Text = EditTime.ToSeconds(text.FadeOut).ToString("0.##", Strings.Culture);
        TxtTextOutline.Text = text.Outline is { } outline ? outline.ToString("0.#", Strings.Culture) : string.Empty;
        TxtTextShadow.Text = text.Shadow.ToString("0.#", Strings.Culture);
        BtnTextBox.IsChecked = text.Box;
        CmbTextMotionIn.SelectedIndex = (int)text.MotionIn;
        CmbTextMotionOut.SelectedIndex = (int)text.MotionOut;
        TxtTextMotionIn.Text = EditTime.ToSeconds(text.MotionInLength).ToString("0.##", Strings.Culture);
        TxtTextMotionOut.Text = EditTime.ToSeconds(text.MotionOutLength).ToString("0.##", Strings.Culture);
        TxtTextMotionIn.IsEnabled = text.MotionIn != TextMotion.None;
        TxtTextMotionOut.IsEnabled = text.MotionOut != TextMotion.None;
        TextPanel.IsVisible = true;
        _showingText = true;
        ShowTextPosition(true);
    }

    /// <summary>
    /// Onizleme katmanini modele esitler. Metin yoksa iz kaldirilir ve hicbir sey yuklenmez.
    /// EDL yeniden acildiysa (<paramref name="reopened"/>) mpv dis izleri dusurmustur; iz yeniden eklenir.
    /// </summary>
    internal void RefreshOverlay(bool reopened = false)
    {
        if (reopened) _overlayId = 0;
        if (Preview.Engine is not { } engine || _driver is not { } driver || _model is not { } model) return;
        if (!model.HasText)
        {
            if (_overlayId != 0) engine.RemoveOverlay(_overlayId);
            _overlayId = 0;
            return;
        }

        var (width, height) = FrameSize(engine);
        var texts = _dragText is { } dragged && _dragIndex >= 0 && _dragIndex < model.Texts.Count
            ? model.Texts.Select((t, i) => i == _dragIndex ? dragged : t).ToList()
            : model.Texts;
        var document = AssWriter.WritePreview(texts, width, height, driver.Preview);
        try
        {
            Directory.CreateDirectory(OverlayRoot);
            File.WriteAllBytes(OverlayPath, AssWriter.Bytes(document));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        OverlayWrites++;
        var families = string.Join("\n", model.Texts.Select(t => t.FontName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
        if (_overlayId != 0 && families == _overlayFonts)
        {
            if (engine.ReloadOverlay(_overlayId))
            {
                OverlayReloads++;
                return;
            }
        }

        if (_overlayId != 0) engine.RemoveOverlay(_overlayId);
        if (families != _overlayFonts)
        {
            string? fonts = null;
            try { fonts = TextFonts.Prepare(OverlayRoot, model.Texts.Select(t => t.FontName)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            engine.SetSubtitleFontsDir(fonts);
            _overlayFonts = families;
        }

        _overlayId = engine.AddOverlay(OverlayPath);
    }

    private (int Width, int Height) FrameSize(VidShrink.Player.IPlaybackEngine engine)
    {
        if (_source is { } source && KnownInfo?.Invoke(source) is { Width: > 0, Height: > 0 } info) return (info.Width, info.Height);
        if (engine.Details is { Width: > 0, Height: > 0 } details) return (details.Width, details.Height);
        return (FallbackWidth, FallbackHeight);
    }

    private void ForgetOverlay()
    {
        _overlayId = 0;
        _overlayFonts = null;
        try
        {
            if (Directory.Exists(OverlayRoot)) Directory.Delete(OverlayRoot, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
