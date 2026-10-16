using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

/// <summary>
/// later.md #42, EKB-15: konuşma metnini akıtan prompter. Açılınca <see cref="CaptureAffinity"/> ile kayıttan
/// çıkarılır; çıkarılamadıysa (Windows 10 2004 öncesi, Windows dışı) pencerenin üstünde kayda gireceği yazılır.
/// İki kipi var: düzenleme kipinde metin yazılır, okuma kipinde büyük yazıyla akar. Boşluk akıtır ya da durdurur,
/// yukarı ve aşağı ok hızı değiştirir, Home başa döner; kayıt başlayınca kaydedici <see cref="Start"/>'ı çağırır.
/// </summary>
internal sealed partial class RecorderPrompter : Window
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();

    public RecorderPrompter() : this(string.Empty, PrompterScroll.DefaultStep)
    {
    }

    internal RecorderPrompter(string text, int step)
    {
        InitializeComponent();
        Script.Text = text;
        Scroll.SetStep(step);
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Advance(_clock.Elapsed));
        Scroll.Changed += Refresh;
        Stage.ScrollChanged += (_, _) => Measure();
        Stage.PointerWheelChanged += (_, _) => Dispatcher.UIThread.Post(() => Scroll.MoveTo(Stage.Offset.Y));
        BtnPlay.Click += (_, _) => TogglePlay();
        BtnSlower.Click += (_, _) => Scroll.Slower();
        BtnFaster.Click += (_, _) => Scroll.Faster();
        BtnRewind.Click += (_, _) => Scroll.Rewind();
        BtnEdit.Click += (_, _) => Edit();
        Script.TextChanged += (_, _) => Refresh();
        AddHandler(KeyDownEvent, (_, e) => e.Handled = Press(e.Key), Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Opened += (_, _) =>
        {
            CaptureExcluded = ExcludeFromCapture && CaptureAffinity.Supported
                              && TryGetPlatformHandle() is { } handle && CaptureAffinity.Exclude(handle.Handle);
            WarningRow.IsVisible = !CaptureExcluded;
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            Saved?.Invoke(Text, Scroll.Step);
        };
        Refresh();
    }

    internal bool ExcludeFromCapture { get; init; } = true;

    /// <summary>Pencere yakalamadan çıkarıldı mı; çağrı yapılmadıysa ya da düştüyse yanlış.</summary>
    internal bool CaptureExcluded { get; private set; }

    internal PrompterScroll Scroll { get; } = new();

    internal string Text => Script.Text ?? string.Empty;

    internal bool Reading => Stage.IsVisible;

    internal bool Ticking => _timer.IsEnabled;

    /// <summary>Metin ve hız basamağı; okuma kipine geçince ve pencere kapanınca.</summary>
    internal event Action<string, int>? Saved;

    /// <summary>Kayıt başladı ya da sürdü: metin varsa okuma kipine geçip akar.</summary>
    internal void Start()
    {
        if (string.IsNullOrWhiteSpace(Text)) return;
        Read();
        Scroll.Play();
    }

    /// <summary>Kayıt durdu ya da bitti: akış yerinde kalır.</summary>
    internal void Stop() => Scroll.Pause();

    internal void TogglePlay()
    {
        if (string.IsNullOrWhiteSpace(Text)) return;
        if (!Reading) Read();
        Scroll.Toggle();
    }

    internal void Read()
    {
        if (Reading) return;
        Lines.Text = Text;
        EditPane.IsVisible = false;
        Stage.IsVisible = true;
        Saved?.Invoke(Text, Scroll.Step);
        UpdateLayout();
        Measure();
        Focus();
    }

    internal void Edit()
    {
        Scroll.Pause();
        Stage.IsVisible = false;
        EditPane.IsVisible = true;
        Script.Focus();
        Refresh();
    }

    internal void Advance(TimeSpan now)
    {
        var elapsed = now - _last;
        _last = now;
        Scroll.Tick(elapsed);
    }

    private TimeSpan _last;

    internal bool Press(Key key)
    {
        if (!Reading) return false;
        switch (key)
        {
            case Key.Space: TogglePlay(); return true;
            case Key.Up: Scroll.Faster(); return true;
            case Key.Down: Scroll.Slower(); return true;
            case Key.Home: Scroll.Rewind(); return true;
            default: return false;
        }
    }

    internal void Measure()
    {
        if (!Reading) return;
        Scroll.Resize(Stage.Extent.Height, Stage.Viewport.Height);
    }

    private void Refresh()
    {
        var empty = string.IsNullOrWhiteSpace(Text);
        BtnPlay.IsEnabled = !empty;
        var label = Say(Scroll.Playing ? "recorder.prompter.pause" : "recorder.prompter.play");
        BtnPlay.Content = label;
        Avalonia.Automation.AutomationProperties.SetName(BtnPlay, label);
        BtnSlower.IsEnabled = Scroll.Step > 0;
        BtnFaster.IsEnabled = Scroll.Step < PrompterScroll.Speeds.Length - 1;
        BtnRewind.IsEnabled = Reading;
        BtnEdit.IsEnabled = Reading;
        TxtSpeed.Text = Say("recorder.prompter.speed", Scroll.Step + 1, PrompterScroll.Speeds.Length);
        if (Scroll.Playing && !_timer.IsEnabled)
        {
            _clock.Restart();
            _last = TimeSpan.Zero;
            _timer.Start();
        }
        else if (!Scroll.Playing && _timer.IsEnabled)
        {
            _timer.Stop();
        }

        if (Math.Abs(Stage.Offset.Y - Scroll.Offset) > 0.5) Stage.Offset = new Vector(0, Scroll.Offset);
    }

    private static string Say(string key, params object?[] args)
        => args.Length == 0
            ? LanguageCatalog.Display(Strings.Get(key))
            : string.Format(Strings.Culture, LanguageCatalog.Display(Strings.Get(key)), args);
}
