using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
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

    private long _overlayId;
    private string? _overlayFonts;
    private string? _overlayRoot;
    private bool _showingText;

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
        ShowTextPanel();
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
        TextPanel.IsVisible = true;
        _showingText = true;
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
        var document = AssWriter.WritePreview(model.Texts, width, height, driver.Preview);
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
