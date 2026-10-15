using System;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>
/// Kayıt bitince köşede açılan sonuç kartı (<see cref="RecorderResultCard"/>). Varsayılan açık;
/// yalnız tam ve teslim edilmiş kayıtta açılır. Küçük resim <see cref="ResultThumbnails"/>'tan
/// gelir, ölçüler sahtesini verir. Kart kapanınca okuma iptal edilir, ffmpeg dosyayı tutmaya devam etmez. "Bir daha gösterme" ayarı kapatır, ayar kutusu geri açar.
/// </summary>
internal partial class RecorderView
{
    private RecorderResultCard? _resultCard;
    private IThumbnailSource? _resultThumbnails;

    internal IThumbnailSource ResultThumbnails
    {
        get => _resultThumbnails ??= new FfmpegThumbnailSource(() => ToolLocator.Ffmpeg);
        set => _resultThumbnails = value;
    }

    internal TimeSpan? ResultCardHold { get; set; }

    internal RecorderResultCard? ResultCard => _resultCard;

    internal Task ResultCardRun { get; private set; } = Task.CompletedTask;

    private void InitSonucKarti()
    {
        ChkResultCard.IsChecked = _settings.ShowResultCard;
        ChkResultCard.IsCheckedChanged += (_, _) =>
        {
            _settings.ShowResultCard = ChkResultCard.IsChecked ?? false;
            _settings.Save(_settingsPath);
        };
    }

    private void ShowResultCard(RecordResult result)
    {
        if (!_settings.ShowResultCard || !FinishAllowed(result) || Delivered() is not { } path) return;
        _resultCard?.Close();
        var card = new RecorderResultCard(path);
        var reading = new CancellationTokenSource();
        if (ResultCardHold is { } hold) card.Hold = hold;
        card.NeverAgain += () => ChkResultCard.IsChecked = false;
        card.Closed += (_, _) =>
        {
            reading.Cancel();
            if (ReferenceEquals(_resultCard, card)) _resultCard = null;
        };
        _resultCard = card;
        card.Show();
        ResultCardRun = LoadThumbnailAsync(card, path, reading);
    }

    private async Task LoadThumbnailAsync(RecorderResultCard card, string path, CancellationTokenSource reading)
    {
        byte[]? image;
        try
        {
            var height = (int)Math.Ceiling(OverlayPlace.Size(card, "RecorderResultCardThumbHeight") * card.RenderScaling);
            image = await ResultThumbnails.ReadAsync(path, 0, Math.Max(1, height), reading.Token);
        }
        catch (Exception)
        {
            return;
        }

        if (image is not null && card.IsVisible) card.SetThumbnail(image);
    }
}
