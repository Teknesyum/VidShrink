using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using VidShrink.App.Localization;
using VidShrink.Player;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    internal const int ThumbnailWidth = 128;
    internal const int ThumbnailHeight = 72;

    private readonly MiniModeSwitch _mini = new();
    private readonly List<double> _thumbnailMs = new();

    private ToolsOptions _tools = new();
    private bool _toolsRead;
    private string? _toolsFolder;
    private IPlaybackEngine? _preview;
    private string? _previewPath;
    private WriteableBitmap? _thumbBitmap;
    private long _thumbShown;
    private bool _thumbBusy;
    private double _thumbPending = double.NaN;
    private Task _thumbWork = Task.CompletedTask;
    private Task<ClipResult> _lastExport = Task.FromResult(new ClipResult(false, "", "", TimeSpan.Zero));
    private Task<bool> _lastAddress = Task.FromResult(false);

    internal ToolsOptions Tools
    {
        get
        {
            EnsureTools();
            return _tools;
        }
    }

    internal MiniModeSwitch Mini => _mini;

    internal bool IsMiniMode => _mini.IsMini;

    internal IReadOnlyList<double> ThumbnailLatenciesMs => _thumbnailMs.ToArray();

    internal Task ThumbnailWork => _thumbWork;

    internal Task<ClipResult> LastExport => _lastExport;

    internal Task<bool> LastAddress => _lastAddress;

    internal bool ThumbnailVisible => ThumbChip?.IsVisible ?? false;

    internal Func<IPlaybackEngine>? PreviewFactory { get; set; }

    internal void SaveTools() => _tools.Save(ToolsFile());

    private void InitTools()
    {
        SeekBar.AddHandler(PointerMovedEvent, OnThumbnailHover, RoutingStrategies.Bubble, handledEventsToo: true);
        SeekBar.AddHandler(PointerExitedEvent, OnThumbnailLeave, RoutingStrategies.Bubble);
    }

    private bool ApplyTools(PlayerCommand command)
    {
        switch (command.Kind)
        {
            case PlayerCommandKind.ClipExport:
                _trace.Add("clip -> " + StartExport(ClipKind.Video));
                return true;
            case PlayerCommandKind.GifExport:
                _trace.Add("gif -> " + StartExport(ClipKind.Gif));
                return true;
            case PlayerCommandKind.MiniMode:
                ToggleMiniMode();
                _trace.Add("mini -> " + _mini.IsMini);
                return true;
            case PlayerCommandKind.OpenUrl:
                _trace.Add("url -> " + PromptAddress());
                return true;
            default:
                return false;
        }
    }

    private void EnsureTools()
    {
        var folder = SettingsFolder();
        if (_toolsRead && string.Equals(folder, _toolsFolder, StringComparison.Ordinal)) return;
        _toolsRead = true;
        _toolsFolder = folder;
        _tools = ToolsOptions.Load(ToolsFile());
    }

    private string? ToolsFile() => SettingsFolder() is { } folder ? Path.Combine(folder, ToolsOptions.FileName) : null;

    private void AppendToolsMenu(MenuFlyout flyout)
    {
        EnsureTools();
        var children = new List<Control>
        {
            ToolsRow(ToolsOptions.Clip),
            ToolsRow(ToolsOptions.Gif),
            new Separator(),
            ToolsRow(ToolsOptions.MiniMode),
            ToolsRow(ToolsOptions.OpenUrl)
        };

        if (children[3] is MenuItem mini)
        {
            mini.ToggleType = MenuItemToggleType.CheckBox;
            mini.IsChecked = _mini.IsMini;
        }

        flyout.Items.Add(new Separator());
        flyout.Items.Add(Submenu(Strings.Get("player.tools.menu"), children));
    }

    private MenuItem ToolsRow(PlayerAction action)
    {
        var item = new MenuItem { Header = Strings.Get(action.LabelKey), Tag = action };
        if (Keymap.FirstKeyRow(action) is { } row)
            item.InputGesture = new KeyGesture(row.Input.Key, row.Input.Modifiers);
        item.Click += OnToolsRow;
        return item;
    }

    private void OnToolsRow(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: PlayerAction action }) Apply(action.ToCommand());
    }

    /// <summary>
    /// Kucuk resim: fare zaman cubugunda gezerken o anin karesi. Kareyi ikinci bir motor
    /// uretir — sessiz (<c>Audio=false</c>), kucuk render olculu ve anahtar kareye aranan.
    /// Ana motor duraklamaz, konumu degismez.
    ///
    /// <para>7b: onizleme artik <b>suruklerken de</b> fareyi takip ediyor. Surukleme yolu
    /// olayi <c>Handled</c> isaretliyor, o yuzden bu isleyici <c>handledEventsToo</c> ile
    /// asiliyor — yoksa ayni denetimdeki ikinci isleyici hic cagrilmaz. Istekler
    /// <see cref="QueueThumbnail"/> icinde birlestiriliyor: motor mesgulken yalniz
    /// <b>son</b> konum bekliyor, dolayisiyla hizli surukleme kuyruk biriktirmiyor.</para>
    /// </summary>
    private void OnThumbnailHover(object? sender, PointerEventArgs e)
    {
        var duration = _seek.Duration;
        if (_engine is null || !double.IsFinite(duration) || duration <= 0)
        {
            HideThumbnail();
            return;
        }

        QueueThumbnail(SeekMarks.SecondsAt(e.GetPosition(SeekBar).X, SeekBar.Bounds.Width, duration));
    }

    private void OnThumbnailLeave(object? sender, PointerEventArgs e) => HideThumbnail();

    internal void HideThumbnail()
    {
        if (ThumbChip is not null) ThumbChip.IsVisible = false;
    }

    internal void QueueThumbnail(double seconds)
    {
        if (_thumbBusy)
        {
            _thumbPending = seconds;
            return;
        }

        _thumbBusy = true;
        _thumbWork = PumpThumbnailAsync(seconds);
    }

    private async Task PumpThumbnailAsync(double seconds)
    {
        var at = seconds;
        while (true)
        {
            await ShowThumbnailAsync(at).ConfigureAwait(true);
            if (double.IsNaN(_thumbPending))
            {
                _thumbBusy = false;
                return;
            }

            at = _thumbPending;
            _thumbPending = double.NaN;
        }
    }

    internal async Task<double> ShowThumbnailAsync(double seconds)
    {
        if (_path is not { } media) return double.NaN;
        var watch = Stopwatch.StartNew();
        IPlaybackEngine? engine;
        try
        {
            engine = await PreviewEngineAsync(media).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is PlaybackOpenException or PlaybackEngineUnavailableException or IOException or ObjectDisposedException)
        {
            return double.NaN;
        }

        if (engine is null) return double.NaN;

        try
        {
            var result = await engine.SeekAsync(seconds, SeekPrecision.Keyframe).ConfigureAwait(true);
            if (result.Outcome is SeekOutcome.Failed or SeekOutcome.TimedOut) return double.NaN;
            engine.TryCopyLatest(ref _thumbShown, DrawThumbnail);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            return double.NaN;
        }

        watch.Stop();
        var elapsed = watch.Elapsed.TotalMilliseconds;
        _thumbnailMs.Add(elapsed);
        PlaceThumbnail(seconds);
        return elapsed;
    }

    private async Task<IPlaybackEngine?> PreviewEngineAsync(string media)
    {
        if (_preview is { IsOpen: true } && string.Equals(_previewPath, media, StringComparison.Ordinal)) return _preview;

        ClosePreview();
        var engine = PreviewFactory is { } factory
            ? factory()
            : new MpvEngine(new PlaybackOptions
            {
                Audio = false,
                RenderWidth = ThumbnailWidth,
                RenderHeight = ThumbnailHeight
            });

        try
        {
            await engine.OpenAsync(media).ConfigureAwait(true);
        }
        catch
        {
            engine.Dispose();
            throw;
        }

        engine.Pause();
        _preview = engine;
        _previewPath = media;
        _thumbShown = 0;
        return engine;
    }

    private void ClosePreview()
    {
        _preview?.Dispose();
        _preview = null;
        _previewPath = null;
        _thumbShown = 0;
    }

    private void DrawThumbnail(IntPtr pixels, int width, int height, int stride)
    {
        if (ThumbImage is null) return;
        var fresh = _thumbBitmap is null || _thumbBitmap.PixelSize.Width != width || _thumbBitmap.PixelSize.Height != height;
        if (fresh)
        {
            _thumbBitmap?.Dispose();
            _thumbBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
        }

        using (var buffer = _thumbBitmap!.Lock())
            FramePixels.CopyRows(pixels, stride, buffer.Address, buffer.RowBytes, Math.Min(height, buffer.Size.Height));

        ThumbImage.Source = _thumbBitmap;
        ThumbImage.InvalidateVisual();
    }

    private void PlaceThumbnail(double seconds)
    {
        if (ThumbChip is null || ThumbTime is null) return;
        ThumbTime.Text = TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        ThumbChip.IsVisible = true;

        var duration = _seek.Duration;
        var width = Surface.Bounds.Width;
        if (!double.IsFinite(duration) || duration <= 0 || width <= 0) return;
        var share = Math.Clamp(seconds / duration, 0, 1);
        var chipWidth = ThumbChip.Bounds.Width > 0 ? ThumbChip.Bounds.Width : ThumbnailWidth;
        var chipHeight = ThumbChip.Bounds.Height > 0 ? ThumbChip.Bounds.Height : ThumbnailHeight;
        var margin = Resource("PlaybackBadgeGap");

        // Zaman cubugu artik panonun alt kenarinda degil, seridin icinde duruyor: yonga
        // cubugun kendi kenarlarina gore konumlanmali, panonun tamamina gore degil.
        var anchor = SeekBar.Bounds.Width > 0
            ? SeekBar.TranslatePoint(new Point(share * SeekBar.Bounds.Width, 0), Surface)
            : null;
        var centre = anchor?.X ?? share * width;
        var top = anchor is { } point
            ? point.Y - chipHeight - margin
            : Surface.Bounds.Height - chipHeight - margin;

        Canvas.SetLeft(ThumbChip, Math.Clamp(centre - chipWidth / 2, 0, Math.Max(0, width - chipWidth)));
        Canvas.SetTop(ThumbChip, Math.Max(0, top));
    }

    /// <summary>
    /// Klip ve GIF. Aralik once A-B isaretlerinden, yoksa bulunulan andan ayarlanan
    /// sure kadar alinir. Video akis kopyasiyla (<c>-c copy</c>), GIF iki gecisli
    /// palet suzgeciyle uretilir.
    /// </summary>
    private string StartExport(ClipKind kind)
    {
        if (_path is not { } media || IsAddress(media))
        {
            _notice = Strings.Get("player.tools.novideo");
            RefreshState();
            return "no";
        }

        EnsureSettings();
        EnsureTools();

        var range = ClipExport.Range(_loopStart, _loopEnd, CurrentPosition(), _tools.ClipSeconds, _seek.Duration);
        if (range.Duration <= 0) return "no";

        var extension = kind == ClipKind.Gif ? ToolsOptions.GifExtension : ToolsOptions.ClipExtension;
        var folder = _settings.ResolveFolder(media);
        var stem = _settings.FileStem(media, range.Start);
        var target = Path.Combine(folder, stem + extension);
        for (var index = 2; File.Exists(target); index++)
            target = Path.Combine(folder, stem + "_" + index.ToString(CultureInfo.InvariantCulture) + extension);

        var request = new ClipRequest(media, target, range.Start, range.Duration, kind, _tools.GifFps, _tools.GifWidth);
        _lastExport = RunExportAsync(request);
        return Path.GetFileName(target);
    }

    private async Task<ClipResult> RunExportAsync(ClipRequest request)
    {
        var result = await ClipExport.RunAsync(request).ConfigureAwait(true);
        _notice = result.Ok
            ? Strings.Get("player.tools.saved", result.Target)
            : Strings.Get("player.tools.failed");
        RefreshState();
        return result;
    }

    /// <summary>
    /// Mini mod: cerceveleri kalkmis, hep ustte duran kucuk pencere. Cikista onceki
    /// durum, konum, boy, cerceve ve "hep ustte" degeri geri konur.
    /// </summary>
    internal void ToggleMiniMode()
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        EnsureTools();

        if (_mini.IsMini)
        {
            var restore = _mini.Leave();
            if (window is not null && restore is { } snapshot)
            {
                window.WindowDecorations = (WindowDecorations)_mini.PreviousDecorations;
                window.Topmost = _mini.PreviousTopmost;
                window.WindowState = (WindowState)snapshot.State;
                window.Position = new PixelPoint((int)snapshot.X, (int)snapshot.Y);
                window.Width = snapshot.Width;
                window.Height = snapshot.Height;
            }

            _topmost = _mini.PreviousTopmost;
            return;
        }

        var current = window is null
            ? new WindowSnapshot(0, 0, 0, 0, 0, CurrentTabIndex())
            : new WindowSnapshot(
                (int)window.WindowState,
                window.Position.X,
                window.Position.Y,
                window.Width,
                window.Height,
                CurrentTabIndex());

        var next = _mini.Enter(
            current,
            window is null ? (int)WindowDecorations.Full : (int)window.WindowDecorations,
            window?.Topmost ?? _topmost,
            _tools.MiniWidth,
            _tools.MiniHeight);

        if (window is not null)
        {
            window.WindowState = WindowState.Normal;
            window.WindowDecorations = WindowDecorations.None;
            window.Topmost = true;
            window.Width = next.Width;
            window.Height = next.Height;
        }

        _topmost = true;
        SelectTab?.Invoke(PlayerTabIndex());
    }

    /// <summary>
    /// Adres acma. libmpv http/https/rtsp gibi semalari kendi acar; yerel dosya yolu
    /// buraya girmez. yt-dlp PATH'teyse mpv'nin kendi kancasi devreye girer.
    /// </summary>
    internal static bool IsAddress(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
           && MpvEngine.RemoteSchemes.Any(scheme => string.Equals(scheme, uri.Scheme, StringComparison.OrdinalIgnoreCase));

    internal bool OpenAddress(string? value)
    {
        if (!IsAddress(value))
        {
            _notice = Strings.Get("player.tools.url-invalid");
            RefreshState();
            return false;
        }

        EnsureTools();
        var address = value!.Trim();
        _tools.UseUrl(address);
        SaveTools();
        _notice = null;
        _navigation = OpenQuietlyAsync(address);
        return true;
    }

    private string PromptAddress()
    {
        if (TopLevel.GetTopLevel(this) is not Window { IsVisible: true } window) return "no";
        _lastAddress = AskAddressAsync(window);
        return "dialog";
    }

    private async Task<bool> AskAddressAsync(Window owner)
    {
        EnsureTools();
        var gap = Resource("SpaceMd");
        var pad = Resource("SpaceLg");

        var box = new TextBox
        {
            Text = _tools.LastUrl,
            PlaceholderText = Strings.Get("player.tools.url-hint"),
            MinWidth = Resource("PanelMinHeight")
        };

        var open = new Button { Content = Strings.Get("player.tools.open"), IsDefault = true };
        var cancel = new Button { Content = Strings.Get("player.tools.cancel"), IsCancel = true };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = gap,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, open }
        };

        var dialog = new Window
        {
            Title = Strings.Get("player.tools.url"),
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            Content = new StackPanel
            {
                Spacing = gap,
                Margin = new Thickness(pad),
                Children =
                {
                    new TextBlock { Text = Strings.Get("player.tools.url-hint") },
                    box,
                    buttons
                }
            }
        };

        open.Click += (_, _) => dialog.Close(box.Text);
        cancel.Click += (_, _) => dialog.Close(null);

        string? answer;
        try
        {
            answer = await dialog.ShowDialog<string?>(owner);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return answer is not null && OpenAddress(answer);
    }
}
