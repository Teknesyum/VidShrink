using System;
using System.Globalization;
using System.Linq;
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

using VidShrink.Core;

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
    private double _seritOncekiHiz;
    private bool _seritPlaying;
    private double _seritPointerX = double.NaN;
    private double _seritSpreadCentre = 0.5;

    internal event EventHandler? PlayingChanged;

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

    /// <summary>P14: alt şeridin açıldığı derinlik; üst bar aynı sayıyı kullanır.</summary>
    internal double RevealBand => SeritZone.Band(Surface.Bounds.Height);

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

        Transitions = HoverZone.MotionReduced
            ? null
            : new Transitions
            {
                new DoubleTransition
                {
                    Property = SeritSpreadProperty,
                    Duration = Span("MotionInstant"),
                    Easing = new CubicEaseOut()
                }
            };
        PropertyChanged += (_, e) =>
        {
            if (e.Property == SeritSpreadProperty) ApplySeritMask();
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
        BtnSeritSpeedReset.Click += (_, _) => ToggleSeritSpeed();

        SliderSeritSpeed.Minimum = Keymap.MinimumSpeed;
        SliderSeritSpeed.Maximum = Keymap.MaximumSpeed;
        SliderSeritVolume.PropertyChanged += OnSeritSliderChanged;
        SliderSeritSpeed.PropertyChanged += OnSeritSliderChanged;

        HoldSerit();
    }

    /// <summary>Panodaki fare konumu. Alt bandin icindeyse serit belirir.</summary>
    private void OnSeritPointer(object? sender, PointerEventArgs e)
    {
        _seritPointerX = e.GetPosition(StripBar).X;
        _serit?.PointerAt(e.GetPosition(Surface).Y, Surface.Bounds.Height);
    }

    /// <summary>
    /// Seridi acik tutan sebepler: fare seridin uzerinde ya da oynatma duraklamis. Biri
    /// bile dogruyken gecikmeli kaybolma calismaz.
    /// </summary>
    private void HoldSerit() => _serit?.Hold(_pointerOnSerit || !_playing);

    internal static KeymapRow? SeekRow(double seconds)
        => Keymap.Rows.FirstOrDefault(r => r.Input.Kind == PlayerInputKind.Key && r.Action.Command == PlayerCommandKind.Seek && r.Action.Amount == seconds);

    private static void SeritLabel(Control control, string name, KeymapRow? row)
    {
        AutomationProperties.SetName(control, name);
        ToolTip.SetTip(control, row is null ? name : name + " (" + Keymap.Gesture(row.Input) + ")");
    }

    private void RevealSerit(bool shown)
    {
        if (shown && StripBar.Opacity < 0.001) SpreadSerit();
        StripBar.Opacity = shown ? 1 : 0;
        StripBar.IsHitTestVisible = shown;
    }

    /// <summary>
    /// P26: kapali serit acilirken once fareye yakin kismi gorunur, <c>MotionInstant</c>
    /// icinde iki yana yayilarak tamami acilir. Maske yalniz yayilma surerken var; bitince
    /// kalkar, gerisi masrafsiz. Hareket azaltilmissa yayilma yok, serit dogrudan acilir.
    /// </summary>
    internal static readonly StyledProperty<double> SeritSpreadProperty =
        AvaloniaProperty.Register<PlayerView, double>(nameof(SeritSpread), 1);

    internal double SeritSpread
    {
        get => GetValue(SeritSpreadProperty);
        set => SetValue(SeritSpreadProperty, value);
    }

    internal double SeritPointerX => _seritPointerX;

    private void SpreadSerit()
    {
        var transitions = Transitions;
        if (transitions is null)
        {
            SeritSpread = 1;
            return;
        }

        var width = StripBar.Bounds.Width;
        _seritSpreadCentre = double.IsFinite(_seritPointerX) && width > 0 ? Math.Clamp(_seritPointerX / width, 0, 1) : 0.5;
        Transitions = null;
        SeritSpread = 0;
        Transitions = transitions;
        SeritSpread = 1;
    }

    /// <summary>Yayilmanin o anki maskesi: fare konumundan iki yana acilan tam opak bant.</summary>
    private void ApplySeritMask()
    {
        var spread = Math.Clamp(SeritSpread, 0, 1);
        var width = StripBar.Bounds.Width;
        if (spread >= 1 || width <= 0)
        {
            StripBar.OpacityMask = null;
            return;
        }

        var centre = _seritSpreadCentre;
        var reach = Math.Max(centre, 1 - centre) * spread;
        var left = Math.Clamp(centre - reach, 0, 1);
        var right = Math.Clamp(centre + reach, 0, 1);
        StripBar.OpacityMask = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Colors.Transparent, left),
                new GradientStop(Colors.Black, left),
                new GradientStop(Colors.Black, right),
                new GradientStop(Colors.Transparent, right)
            }
        };
    }

    /// <summary>
    /// Seridin yuzu. <see cref="RefreshWindowState"/> uzerinden her durum degisiminde
    /// cagriliyor, yani seritteki ses ve hiz okumasi komut yolundan gecen degerin
    /// kendisidir.
    /// </summary>
    private void RefreshSerit()
    {
        if (TxtSeritTime is null) return;

        if (_seritPlaying != _playing)
        {
            _seritPlaying = _playing;
            PlayingChanged?.Invoke(this, EventArgs.Empty);
        }

        GlyphSeritPlay.Data = Icon(_playing ? "IconPause" : "IconPlay");
        GlyphSeritVolume.Data = Icon(_muted || _volume <= 0 ? "IconVolumeMute" : "IconVolume");

        SeritLabel(BtnSeritPlay, Strings.Get(_playing ? "playback.control.pause" : "playback.control.play"), Keymap.FirstKeyRow(Keymap.PlayPause));
        SeritLabel(BtnSeritBack, Strings.Get("main.player.menu.seek", "−" + Keymap.SeekSmall.ToString(Strings.Culture)), SeekRow(-Keymap.SeekSmall));
        SeritLabel(BtnSeritForward, Strings.Get("main.player.menu.seek", "+" + Keymap.SeekSmall.ToString(Strings.Culture)), SeekRow(Keymap.SeekSmall));
        TxtSeritBack.Text = "−" + Keymap.SeekSmall.ToString(Strings.Culture);
        TxtSeritForward.Text = "+" + Keymap.SeekSmall.ToString(Strings.Culture);
        SeritLabel(BtnSeritMute, Strings.Get(Keymap.Mute.LabelKey), Keymap.FirstKeyRow(Keymap.Mute));
        SeritLabel(BtnSeritFullScreen, Strings.Get(Keymap.Fullscreen.LabelKey), Keymap.FirstKeyRow(Keymap.Fullscreen));
        SeritLabel(BtnSeritSpeedReset, Strings.Get(Keymap.NormalSpeed.LabelKey), Keymap.FirstKeyRow(Keymap.NormalSpeed));

        TxtSeritTime.Text = ClockPair(_seek.Target, _seek.Duration);
        TxtSeritVolume.Text = _volume.ToString("0", CultureInfo.InvariantCulture);
        TxtSeritSpeed.Text = _speed.ToString("0.00", CultureInfo.InvariantCulture) + "×";

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

    /// <summary>
    /// Hiz simgesi bir dugme: x hizdayken basinca 1'e doner, 1'deyken basinca en son
    /// kullanilan x hiza geri gider. Komut yolu atlanmiyor — degisim yine
    /// <see cref="PlayerView.Apply"/>'a fark olarak veriliyor. Bir kez bile x hiza
    /// gidilmemisse 1'deki basis hicbir sey yapmaz; kod bir hiz uydurmuyor.
    /// </summary>
    private void ToggleSeritSpeed()
    {
        if (Math.Abs(_speed - 1) > double.Epsilon)
        {
            _seritOncekiHiz = _speed;
            Apply(Keymap.NormalSpeed.ToCommand());
            return;
        }

        if (_seritOncekiHiz <= 0) return;
        Apply(new PlayerCommand(PlayerCommandKind.Speed, _seritOncekiHiz - _speed));
    }

    private Geometry? Icon(string key)
        => this.TryFindResource(key, out var value) ? value as Geometry : null;

    /// <summary>Sure belirteci. Belirtec yoksa bekleme yoktur; kod sayi uydurmaz.</summary>
    private TimeSpan Span(string key)
        => this.TryFindResource(key, out var value) && value is TimeSpan span ? span : TimeSpan.Zero;
}
