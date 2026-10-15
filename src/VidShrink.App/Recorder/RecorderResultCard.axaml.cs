using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace VidShrink.App.Recorder;

/// <summary>
/// Kayıt bitince çalışma alanının sağ alt köşesinde açılan küçük kart: küçük resim, dosya adı,
/// "bir daha gösterme". Karttan dosya başka uygulamaya sürüklenir; sürükleme verisi depolama
/// sağlayıcısının dosya öğesidir, Gezgin ve sohbet uygulamaları dosya olarak alır. Kart odak
/// çalmaz, <c>RecorderResultCardHold</c> sonunda kendiliğinden kapanır, imleç üstündeyken
/// kapanmaz. Açılınca <see cref="CaptureAffinity"/> ile sonraki kayıtlardan çıkarılır.
/// </summary>
internal partial class RecorderResultCard : Window
{
    private readonly string _path;
    private Task<IDataTransfer?>? _drag;
    private Bitmap? _thumbnail;
    private int _turn;

    internal bool ExcludeFromCapture { get; init; } = true;

    /// <summary>Kart penceresi yakalamadan çıkarıldı mı; çağrı yapılmadıysa ya da düştüyse yanlış.</summary>
    internal bool CaptureExcluded { get; private set; }

    internal event Action? NeverAgain;

    public RecorderResultCard() : this(string.Empty)
    {
    }

    internal RecorderResultCard(string path)
    {
        _path = path;
        InitializeComponent();
        TxtName.Text = Path.GetFileName(path);
        Hold = OverlayPlace.Hold(this, "RecorderResultCardHold", TimeSpan.FromSeconds(6));
        Opened += (_, _) =>
        {
            CaptureExcluded = ExcludeFromCapture && CaptureAffinity.Supported
                              && TryGetPlatformHandle() is { } handle && CaptureAffinity.Exclude(handle.Handle);
            Place();
            _drag = DragData();
            Restart();
        };
        Closed += (_, _) =>
        {
            _turn++;
            ImgThumb.Source = null;
            _thumbnail?.Dispose();
            _thumbnail = null;
        };
        Card.PointerEntered += (_, _) => _turn++;
        Card.PointerExited += (_, _) => Restart();
        Card.PointerPressed += OnPressed;
        BtnNeverAgain.Click += (_, _) =>
        {
            NeverAgain?.Invoke();
            Close();
        };
        BtnCloseCard.Click += (_, _) => Close();
    }

    internal TimeSpan Hold { get; set; }

    internal string FileName => TxtName.Text ?? string.Empty;

    internal Bitmap? Thumbnail => _thumbnail;

    internal void SetThumbnail(byte[] image)
    {
        Bitmap next;
        try
        {
            using var stream = new MemoryStream(image);
            next = new Bitmap(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NullReferenceException)
        {
            return;
        }

        var old = _thumbnail;
        _thumbnail = next;
        ImgThumb.Source = next;
        old?.Dispose();
    }

    /// <summary>Sürüklenen veri: teslim edilen dosyanın depolama öğesi. Sağlayıcı dosyayı bulamazsa boş.</summary>
    internal async Task<IDataTransfer?> DragData()
    {
        if (await StorageProvider.TryGetFileFromPathAsync(_path) is not { } file) return null;
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateFile(file));
        return data;
    }

    private async void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Card).Properties.IsLeftButtonPressed || e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is not null) return;
        if (_drag is not { IsCompletedSuccessfully: true, Result: { } data }) return;
        _turn++;
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);
        Restart();
    }

    private void Place()
    {
        if ((Screens.ScreenFromWindow(this) ?? Screens.Primary) is not { } screen) return;
        var area = screen.WorkingArea;
        var size = PixelSize.FromSize(Bounds.Size, screen.Scaling);
        var gap = (int)Math.Ceiling(OverlayPlace.Size(this, "RecorderResultCardGap") * screen.Scaling);
        Position = new PixelPoint(area.Right - size.Width - gap, area.Bottom - size.Height - gap);
    }

    private void Restart()
    {
        var turn = ++_turn;
        _ = CloseLater(Hold, () =>
        {
            if (turn == _turn && IsVisible) Close();
        });
    }

    private static async Task CloseLater(TimeSpan hold, Action done)
    {
        await Task.Delay(hold).ConfigureAwait(false);
        Dispatcher.UIThread.Post(done);
    }
}
