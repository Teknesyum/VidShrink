using System;
using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using VidShrink.App.Localization;

namespace VidShrink.App.Playback;

/// <summary>
/// Oynaticinin alt denetim seridi: oynat/duraklat, -10 sn, +10 sn, ses, hiz ve sure
/// okumasi.
///
/// <para>Serit kendi gorunurluk kuralini yazmiyor. Karsilastirma panelinin serdi ile ayni
/// <see cref="HoverZone"/> nesnesini ve ayni iki belirteci kullaniyor
/// (<c>PlaybackStripShowDelay</c> / <c>PlaybackStripHideDelay</c>): fare panonun alt
/// bandina girince beklemeden belirir, cikinca 360 ms sonra kaybolur. Duraklatilmisken ya
/// da fare seridin uzerindeyken hic kaybolmaz.</para>
///
/// <para>Dugmeler kendi islerini yapmiyor, <see cref="PlayerView.Apply"/> yoluna giriyor:
/// klavye, menu, fare ve serit ayni tek komut yolundan geciyor, dolayisiyla motora ulasan
/// sey de motordan geri okunan sey de tek yerden geliyor.</para>
/// </summary>
internal partial class PlayerView
{
    private HoverZone? _serit;
    private bool _seritWired;
    private bool _pointerOnSerit;
    private bool _seritSliding;

    /// <summary>Seridin gorunurluk bolgesi. Olcum kendi saatini buraya takar.</summary>
    internal HoverZone SeritZone
    {
        get
        {
            EnsureSerit();
            return _serit!;
        }
    }

    internal bool SeritRevealed => _serit?.IsVisible ?? false;

    internal string SeritTimeText => TxtSeritTime?.Text ?? "";

    internal string SeritVolumeText => TxtSeritVolume?.Text ?? "";

    internal string SeritSpeedText => TxtSeritSpeed?.Text ?? "";

    /// <summary>
    /// 7b/K2 — sure okumasi: gecen sure ve <b>kalan</b> sure, <c>00:12 / -01:18</c>.
    /// Kalanin onundeki eksi isareti yonu soyluyor; sayilar degismez bicimde yaziliyor,
    /// yani dil degisince kaymiyor. Kaynak bir saati geciyorsa iki taraf da
    /// <c>01:02:12</c> bicimine gecer. Saat bicimi seritten aliniyor, ikinci kopya yok.
    /// </summary>
    internal static string ClockPair(double positionSeconds, double durationSeconds)
    {
        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0) return "--:-- / --:--";
        var scale = TimeSpan.FromSeconds(durationSeconds);
        var at = TimeSpan.FromSeconds(Math.Clamp(positionSeconds, 0, durationSeconds));
        return ControlStrip.Clock(at, scale) + " / -" + ControlStrip.Clock(scale - at, scale);
    }

    private void OnSeritLoaded(object? sender, RoutedEventArgs e)
    {
        EnsureSerit();
        RefreshSerit();
    }

    /// <summary>
    /// Seridi bir kez kurar. Yapiciya degil ilk kullanima bagli: bu kol
    /// <c>PlayerView.axaml.cs</c>'e dokunmuyor, kurulum koke asilan <c>Loaded</c>
    /// olayindan ve olcumun okudugu <see cref="SeritZone"/> kapisindan geliyor.
    /// </summary>
    private void EnsureSerit()
    {
        if (_seritWired) return;
        _seritWired = true;

        _serit = new HoverZone(
            share: Resource("PlaybackHoverZoneShare"),
            showDelay: () => Span("PlaybackStripShowDelay"),
            hideDelay: () => Span("PlaybackStripHideDelay"),
            apply: RevealSerit);

        StripBar.Transitions = HoverZone.MotionReduced
            ? null
            : new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = Span("MotionFast"),
                    Easing = new CubicEaseOut()
                }
            };

        StripBar.PointerEntered += (_, _) => { _pointerOnSerit = true; HoldSerit(); };
        StripBar.PointerExited += (_, _) => { _pointerOnSerit = false; HoldSerit(); };
        Surface.PointerMoved += OnSeritPointer;
        Surface.PointerExited += (_, _) => _serit?.PointerGone();

        BtnSeritPlay.Click += (_, _) => Apply(Keymap.PlayPause.ToCommand());
        BtnSeritBack.Click += (_, _) => Apply(new PlayerCommand(PlayerCommandKind.Seek, -Keymap.SeekSmall));
        BtnSeritForward.Click += (_, _) => Apply(new PlayerCommand(PlayerCommandKind.Seek, Keymap.SeekSmall));
        BtnSeritMute.Click += (_, _) => Apply(Keymap.Mute.ToCommand());
        BtnSeritFullScreen.Click += (_, _) => Apply(Keymap.Fullscreen.ToCommand());

        SliderSeritSpeed.Minimum = Keymap.MinimumSpeed;
        SliderSeritSpeed.Maximum = Keymap.MaximumSpeed;
        SliderSeritVolume.PropertyChanged += OnSeritSliderChanged;
        SliderSeritSpeed.PropertyChanged += OnSeritSliderChanged;

        HoldSerit();
    }

    /// <summary>Panodaki fare konumu. Alt bandin icindeyse serit belirir.</summary>
    private void OnSeritPointer(object? sender, PointerEventArgs e)
        => _serit?.PointerAt(e.GetPosition(Surface).Y, Surface.Bounds.Height);

    /// <summary>
    /// Seridi acik tutan sebepler: fare seridin uzerinde ya da oynatma duraklamis. Biri
    /// bile dogruyken gecikmeli kaybolma calismaz.
    /// </summary>
    private void HoldSerit() => _serit?.Hold(_pointerOnSerit || !_playing);

    private void RevealSerit(bool shown)
    {
        StripBar.Opacity = shown ? 1 : 0;
        StripBar.IsHitTestVisible = shown;
    }

    /// <summary>
    /// Seridin yuzu. <see cref="RefreshWindowState"/> uzerinden her durum degisiminde
    /// cagriliyor, yani seritteki ses ve hiz okumasi komut yolundan gecen degerin
    /// kendisidir.
    /// </summary>
    private void RefreshSerit()
    {
        if (TxtSeritTime is null) return;

        GlyphSeritPlay.Data = Icon(_playing ? "IconPause" : "IconPlay");
        GlyphSeritVolume.Data = Icon(_volume <= 0 ? "IconVolumeMute" : "IconVolume");

        AutomationProperties.SetName(BtnSeritPlay,
            Strings.Get(_playing ? "playback.control.pause" : "playback.control.play"));
        AutomationProperties.SetName(BtnSeritBack, Strings.Get("main.player.menu.seek", -Keymap.SeekSmall));
        AutomationProperties.SetName(BtnSeritForward, Strings.Get("main.player.menu.seek", Keymap.SeekSmall));
        AutomationProperties.SetName(BtnSeritMute, Strings.Get(Keymap.Mute.LabelKey));
        AutomationProperties.SetName(BtnSeritFullScreen, Strings.Get(Keymap.Fullscreen.LabelKey));

        TxtSeritTime.Text = ClockPair(_seek.Target, _seek.Duration);
        TxtSeritVolume.Text = _volume.ToString("0", CultureInfo.InvariantCulture);
        TxtSeritSpeed.Text = _speed.ToString("0.##", CultureInfo.InvariantCulture) + "x";

        _seritSliding = true;
        SliderSeritVolume.Maximum = VolumeCeiling();
        SliderSeritVolume.Value = _volume;
        SliderSeritSpeed.Value = Math.Clamp(_speed, Keymap.MinimumSpeed, Keymap.MaximumSpeed);
        _seritSliding = false;

        HoldSerit();
    }

    /// <summary>
    /// Kesit B: ses ve hiz artik dugme cifti degil kaydirici. Kaydirici komut yolunu
    /// atlamiyor — degeri okunup <see cref="PlayerView.Apply"/>'a <b>fark</b> olarak
    /// veriliyor, boylece klavye, menu ve seritle ayni tek yoldan geciyor. Bayrak,
    /// <see cref="RefreshSerit"/>'in kaydiriciyi geri yazmasinin yeni bir komut
    /// dogurmasini engelliyor.
    /// </summary>
    private void OnSeritSliderChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_seritSliding || e.Property != RangeBase.ValueProperty) return;

        if (ReferenceEquals(sender, SliderSeritVolume))
        {
            var fark = SliderSeritVolume.Value - _volume;
            if (Math.Abs(fark) > double.Epsilon) Apply(new PlayerCommand(PlayerCommandKind.Volume, fark));
            return;
        }

        var hizFarki = SliderSeritSpeed.Value - _speed;
        if (Math.Abs(hizFarki) > double.Epsilon) Apply(new PlayerCommand(PlayerCommandKind.Speed, hizFarki));
    }

    private Geometry? Icon(string key)
        => this.TryFindResource(key, out var value) ? value as Geometry : null;

    /// <summary>Sure belirteci. Belirtec yoksa bekleme yoktur; kod sayi uydurmaz.</summary>
    private TimeSpan Span(string key)
        => this.TryFindResource(key, out var value) && value is TimeSpan span ? span : TimeSpan.Zero;
}
