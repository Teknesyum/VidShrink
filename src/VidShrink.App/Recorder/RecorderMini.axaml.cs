using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Automation;
using Avalonia.Media;
using VidShrink.App.Localization;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

internal enum MiniOptionKind
{
    ShowClicks,
    ClickSound,
    ShowKeys,
    OpenFolder,
    Cursor
}

internal readonly record struct MiniOption(MiniOptionKind Kind, bool Value);

/// <summary>
/// TinyTask ölçüsünde kayıt şeridi. Saha taramasında ölçülen 22 kompakt yüzeyin
/// hepsinde üç şey ortak: durdurma düğmesi, tek düğmeye indirilmiş duraklat/sürdür ve
/// geçen süre. Bu pencere tam olarak onları taşıyor.
///
/// <para><b>Hep üstte yalnız kayıt sürerken.</b> LICEcap kayıt başlarken
/// <c>HWND_TOPMOST</c>, durunca <c>HWND_NOTOPMOST</c> yazıyor; sürekli üstte duran bir
/// pencere kayıt yokken yoldan çekilmiyor. Avalonia karşılığı <see cref="Window.Topmost"/>
/// ve <see cref="Follow"/> her hal değişiminde onu tazeliyor.</para>
///
/// <para><b>Kadrajın dışında duruyor.</b> gdigrab bölgeyi yakalarken bu pencere kadrajın
/// içine düşerse kayda karışır; <see cref="PlaceOutside"/> pencereyi seçili bölgenin
/// altına, yer yoksa üstüne koyuyor. Bölge tam ekransa dışarısı yok — o durumda
/// çağıran taraf kullanıcıyı uyarıyor.</para>
/// </summary>
internal partial class RecorderMini : Window
{
    /// <summary>Kadraj ile şerit arasındaki pay. ShareX çubuğu bölgenin 3 px altına koyuyor.</summary>
    internal const int Clearance = 8;

    internal event EventHandler? ToggleRequested;

    internal event EventHandler? StopRequested;

    internal event EventHandler? ExpandRequested;

    internal event EventHandler<MiniOption>? OptionChanged;

    private bool _showingOptions;

    public RecorderMini()
    {
        InitializeComponent();
        foreach (var (box, option) in OptionBoxes())
            box.IsCheckedChanged += (_, _) =>
            {
                if (!_showingOptions) OptionChanged?.Invoke(this, option with { Value = box.IsChecked ?? false });
            };
    }

    private (CheckBox Box, MiniOption Option)[] OptionBoxes() => new[]
    {
        (ChkShowClicks, new MiniOption(MiniOptionKind.ShowClicks, false)),
        (ChkClickSound, new MiniOption(MiniOptionKind.ClickSound, false)),
        (ChkShowKeys, new MiniOption(MiniOptionKind.ShowKeys, false)),
        (ChkOpenFolder, new MiniOption(MiniOptionKind.OpenFolder, false)),
        (ChkCursor, new MiniOption(MiniOptionKind.Cursor, false))
    };

    internal void ShowOptions(bool showClicks, bool clickSound, bool showKeys, bool openFolder, bool cursor, bool recording)
    {
        _showingOptions = true;
        try
        {
            ChkShowClicks.IsChecked = showClicks;
            ChkClickSound.IsChecked = clickSound;
            ChkShowKeys.IsChecked = showKeys;
            ChkOpenFolder.IsChecked = openFolder;
            ChkCursor.IsChecked = cursor;
            ChkCursor.IsEnabled = !recording;
        }
        finally
        {
            _showingOptions = false;
        }
    }

    /// <summary>Şeridin yüzünü oturumun haline uyduruyor; hep üstte kalma da buradan sürülüyor.</summary>
    internal void Follow(RecorderState state, string elapsed, int countdown = 0)
    {
        var running = state == RecorderState.Running;
        var paused = state == RecorderState.Paused;
        var counting = countdown > 0;

        TxtElapsed.Text = counting ? RecorderView.CountdownDigits(countdown) : elapsed;
        LiveDot.IsVisible = running;

        BtnStop.IsVisible = running || paused || counting;

        if (this.FindResource(running ? "IconPause" : "IconPlay") is Geometry glyph)
            ToggleGlyph.Data = glyph;

        var label = LanguageCatalog.Display(Strings.Get(
            running ? "recorder.strip.pause" : paused ? "recorder.strip.resume" : "recorder.strip.start"));
        ToolTip.SetTip(BtnToggle, label);
        AutomationProperties.SetName(BtnToggle, label);

        Topmost = running || paused || counting;
    }

    /// <summary>
    /// Şeridi kayıt alanının dışına koyuyor: önce altına, ekranda yer kalmadıysa üstüne.
    /// Bölge verilmemişse pencere çalışma alanının sağ alt köşesine oturuyor.
    /// </summary>
    internal void PlaceOutside(PixelRect? region)
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;

        var work = screen.WorkingArea;
        var size = PixelSize.FromSize(MiniStrip.Bounds.Size, screen.Scaling);
        if (size.Width <= 0 || size.Height <= 0)
            size = new PixelSize((int)Width, (int)Height);

        if (region is not { } frame)
        {
            Position = new PixelPoint(
                work.Right - size.Width - Clearance,
                work.Bottom - size.Height - Clearance);
            return;
        }

        var below = frame.Bottom + Clearance;
        var top = below + size.Height <= work.Bottom
            ? below
            : Math.Max(work.Y, frame.Y - Clearance - size.Height);

        var left = Math.Clamp(
            frame.X + (frame.Width - size.Width) / 2,
            work.X,
            Math.Max(work.X, work.Right - size.Width));

        Position = new PixelPoint(left, top);
    }

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void OnToggle(object? sender, RoutedEventArgs e) => ToggleRequested?.Invoke(this, EventArgs.Empty);

    private void OnStop(object? sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);

    private void OnExpand(object? sender, RoutedEventArgs e) => ExpandRequested?.Invoke(this, EventArgs.Empty);
}
