using System.Drawing;
using System.Drawing.Text;
using System.Linq;

namespace Teknesyum.Theme;

/// Teknesyum Neon — WinForms/console palette. Do not change these values.
public static class Palette
{
    public static readonly Color Renk1      = ColorTranslator.FromHtml("#6FB7FF");
    public static readonly Color Renk2      = ColorTranslator.FromHtml("#CBA7D2");
    public static readonly Color Renk3      = ColorTranslator.FromHtml("#C3A3FF");
    public static readonly Color Success    = ColorTranslator.FromHtml("#66F09A");

    public static readonly Color Renk2Text  = ColorTranslator.FromHtml("#FA8CFF");
    public static readonly Color Renk3Text  = ColorTranslator.FromHtml("#AC7FFF");

    // --- semantic role layer (SKILL §2) ---
    //
    // THE ROLE WINS. Every control that reports state — error text, form
    // validation, warning box, status dot, danger button — writes these fields.
    // Brand and decoration (glow, scrollbar, hero, heading) keep writing the brand
    // field. In C# an alias is a plain assignment: the role field takes the VALUE
    // of the brand field, its hex is not copied. The one exception is `Warning` —
    // it has no counterpart in the brand triad and carries its own hex. Equality
    // is measured in the audit.
    //
    // `Success` is defined above and is already a role field; no second name was
    // given. `Info` IS DELIBERATELY ABSENT: there is no info box today, and an
    // unused token is debt. If one opens it binds to renk-1, and an info fill is
    // never used on the same screen as a primary button.
    public static readonly Color Danger     = Renk2;

    /// The TEXT role of danger. The fill renk-2 falls below §2's 7:1 threshold as text;
    /// error text writes this rather than the fill field.
    public static readonly Color DangerText = Renk2Text;

    /// `warning #FBBF24` — WARNING SURFACE ONLY: text, border, icon.
    /// NO FILL, NO BUTTON. The constraint is the same pattern as `Success`, not a
    /// new one.
    ///
    /// The ban was measured: white text on an amber fill is 1.50:1 — it collapses.
    /// WHAT REPLACES IT: warning text `Warning` (11.30:1), border
    /// `Warning50` (3.60:1 on `#101115`, clears 1.4.11's 3:1 threshold — renk-2 /50
    /// at 1.94 and renk-3 /50 at 1.92 did not carry this rung, amber does), icon
    /// the same colour. If an action is needed the button is primary (renk-1) or
    /// `Danger` (renk-2); the warning colour never enters a button.
    ///
    /// COLOUR ALONE CARRIES NO MEANING, amber included from the start: amber does
    /// not separate from `Success` under protanopia, ΔE2000 15.2
    /// A warning row carries an icon or text in
    /// addition to colour.
    ///
    /// CAVEAT: this hex is subject to the `U9` ΔE measurement.
    public static readonly Color Warning    = ColorTranslator.FromHtml("#FFD24D");
    public static readonly Color Warning50  = Color.FromArgb(0x80, 0xFF, 0xD2, 0x4D);

    public static readonly Color Surface    = ColorTranslator.FromHtml("#000000");
    public static readonly Color AppBg      = ColorTranslator.FromHtml("#000000");
    public static readonly Color AppBgFrom  = ColorTranslator.FromHtml("#000000");
    public static readonly Color AppBgTo    = ColorTranslator.FromHtml("#000000");

    public static readonly Color BorderDefault    = Color.FromArgb(0x8C, 0x6F, 0xB7, 0xFF);
    public static readonly Color BorderStrong     = Color.FromArgb(0xB3, 0x6F, 0xB7, 0xFF);
    public static readonly Color BorderDecorative = Color.FromArgb(0x33, 0x6F, 0xB7, 0xFF);

    public static readonly Color FocusRing      = ColorTranslator.FromHtml("#6FB7FF");
    public static readonly Color FocusRingInner = ColorTranslator.FromHtml("#000000");

    public static readonly Color TextBody   = ColorTranslator.FromHtml("#FFFFFF");
    public static readonly Color TextLabel  = ColorTranslator.FromHtml("#6FB7FF");

    /// A disabled control is exempt from 7:1 (SKILL §2) and there is a price: a
    /// colour-blind user cannot see the grey. This colour is never used alone —
    /// every disabled control carries a marker in addition to the grey: `ToolTip`
    /// text is MANDATORY, plus `Cursor = Cursors.No` and, where possible, an icon.
    /// A merely dimmed control is an incomplete delivery.
    ///
    /// The "muted text" role was deleted outright on 2026-08-23: it carried exactly
    /// the same value as `TextBody`, and a single value with two names eventually
    /// diverges. For secondary text the answer is not to give grey, it is to delete
    /// the text (SKILL §2, "no mid greys").
    public static readonly Color Disabled   = ColorTranslator.FromHtml("#8A8F9A");

    /// There is one radius: 6 DIP (SKILL §5, `layout.md` §5.1). Card, panel, button
    /// and cell take the same value. The one exception is the circle: `?` badge,
    /// slider thumb, status dot.
    public const int Radius = 3;

    /// Numeric tokens: the same values as the CSS `--tk-*` layer, in DIP.
    /// Tracking is em, line height a multiplier, ratios unitless, times in ms.
    public const int    WindowRadius = 3;
    public const int    BorderWidth  = 1;
    public const int    FocusWidth   = 2;
    public const int    FocusOffset  = 2;

    public const int    FontSize1 = 14;
    public const int    FontSize2 = 16;
    public const int    FontSize3 = 20;
    public const int    FontSize4 = 24;
    public const int    FontSize5 = 32;
    public const double LineHeightBody    = 1.6;
    public const double LineHeightHeading = 1.25;
    public const double LineHeightMono    = 1.5;
    public const int    MeasureCh    = 65;
    public const double TrackingLabel = 0.08;
    public const double TrackingH3    = 0.02;
    public const double TrackingH2    = 0;
    public const double TrackingHero  = -0.015;
    public const int    WeightBody = 400;
    public const int    WeightSemi = 600;
    public const int    WeightHero = 800;

    public const int Space1 = 4;
    public const int Space2 = 8;
    public const int Space3 = 12;
    public const int Space4 = 16;
    public const int Space5 = 24;
    public const int PanelPadding  = 24;
    public const int SectionGap    = 24;
    public const int RowGap        = 12;
    public const int FieldGap      = 8;
    public const int FieldStack    = 16;
    public const int InputPaddingX = 12;
    public const int ToastInset    = 24;
    public const int ToastGap      = 12;

    public const int    TargetMin             = 24;
    public const int    ScrollbarWidth        = 3;
    public const int    TitleBarHeightMin     = 28;
    public const int    TitleBarHeightMax     = 28;
    public const int    SidebarWidth          = 240;
    public const int    SidebarCollapsedWidth = 48;
    public const int    InputHeight           = 40;
    public const int    ModalWidth            = 560;
    public const double ModalMaxRatio         = 0.85;
    public const int    ToastWidth            = 360;
    public const int    ToastMax              = 3;
    public const int    ToastLifeMs           = 6000;
    public const int    IconSize1 = 14;
    public const int    IconSize2 = 16;
    public const int    IconSize3 = 22;
    public const int    IconSize4 = 56;
    public const double ButtonHeight   = 28;
    public const double ButtonPaddingX = 10;
    public const double GlassBlur      = 0;
    public const double BgAngle        = 160;
    public const bool   BgRotate       = false;
    public const string BackgroundType = "flat";
    public const string ScrollbarStyle = "fade";
    public const string ScrollBehavior = "auto";
    public static readonly Color WindowEdge          = Color.Transparent;
    public static readonly Color ScrollbarThumb      = Color.FromArgb(0xFF, 0xAC, 0x7F, 0xFF);
    public static readonly Color ScrollbarThumbHover = Color.FromArgb(0xFF, 0xCB, 0xA7, 0xD2);
    public static readonly Color ScrollbarTrack      = Color.FromArgb(0x4D, 0x00, 0x00, 0x00);

    public const double ScaleHover     = 1.02;
    public const double ScalePress     = 0.98;
    public const double ScaleIconHover = 1.1;
    public const int    EntryOffset    = 8;
    public const int    OverlayOffset  = 4;
    public const int    StaggerMs      = 40;
    public const int    StaggerMax     = 6;
    public const int    GlowMargin     = 24;
    public const int    LoadingLoopMinMs = 1400;
    public const int    FrameBudgetMs    = 16;
    public const int    BgRotateMinMs    = 40000;
    public const int    BgSweepMaxDeg    = 20;

    /// The chain's only source is SKILL §3. A WinForms `Font` takes no fallback
    /// chain, so the first installed family is selected. Atkinson Hyperlegible Next
    /// is the default and is embedded in the project (`PrivateFontCollection`); if
    /// it is not embedded it falls back to Segoe UI — that is not an acceptance, it
    /// is an incomplete delivery.
    public static string Family(params string[] candidates)
    {
        using var installed = new InstalledFontCollection();
        var names = installed.Families.Select(f => f.Name).ToHashSet();
        return candidates.FirstOrDefault(names.Contains) ?? candidates[^1];
    }

    public static readonly string SansName = Family("Atkinson Hyperlegible Next", "Segoe UI");
    public static readonly string MonoName = Family("Cascadia Mono", "Consolas");

    // Scale 1.25 major third — 14 / 16 / 20 / 24 / 30 DIP. The point equivalent at
    // 96 dpi is DIP × 0.75: 10.5 / 12 / 15 / 18 / 22.5.
    //
    // WEIGHT COMPENSATION: SKILL §3 asks for 600 (SemiBold) on headings and labels;
    // `FontStyle` knows only Regular and Bold, there is no intermediate weight.
    // Headings stay Bold here and the difference is built with size (h2 18pt,
    // h3 15pt, label 10.5pt) — with three levels separated by size, weight does not
    // need to carry the hierarchy alone. If a real 600 is wanted, load the variable
    // font's SemiBold cut with `PrivateFontCollection` instead of `GDI+`; the
    // compensation then lifts.
    //
    // Line height also cannot be set through a WinForms `Font`; when `TextRenderer`
    // draws, the line spacing is applied by hand with a 1.5 (body) / 1.2 (heading)
    // factor.
    public static readonly Font  H2         = new(SansName, 18f, FontStyle.Bold);
    public static readonly Font  H3         = new(SansName, 15f, FontStyle.Bold);
    public static readonly Font  LabelFont  = new(SansName, 10.5f, FontStyle.Bold);
    public static readonly Font  Body       = new(SansName, 12f);
    public static readonly Font  Hint       = new(SansName, 10.5f);
    public static readonly Font  Mono       = new(MonoName, 12f, FontStyle.Bold);
    public static readonly Font  Hero       = new(MonoName, 22.5f, FontStyle.Bold);

    public const string Author     = "Teknesyum";
    public const string GitHubUrl  = "https://github.com/Teknesyum";
    public const string SponsorUrl = "https://github.com/sponsors/Teknesyum";
    public const bool   SponsorActive = true;
}

/// ANSI console colours (for CLI projects such as Runly).
public static class Ansi
{
    public const string Renk1      = "[38;2;111;183;255m";
    public const string Renk2      = "[38;2;203;167;210m";
    public const string Renk3      = "[38;2;195;163;255m";
    public const string Renk2Text  = "[38;2;250;140;255m";
    public const string Renk3Text  = "[38;2;172;127;255m";
    public const string Success    = "[38;2;102;240;154m";
    // Role colours enter ANSI too; without them the terminal output drifts from the
    // palette. Danger and DangerText take the value of the brand constant, the hex
    // is not copied.
    public const string Danger     = Renk2;
    public const string DangerText = Renk2Text;
    // Warning: warning text only. A terminal has no fill anyway, so the constraint
    // holds by itself.
    public const string Warning    = "[38;2;255;210;77m";
    public const string Disabled   = "[38;2;138;143;154m";
    public const string Bold       = "[1m";
    public const string Reset      = "[0m";
}
