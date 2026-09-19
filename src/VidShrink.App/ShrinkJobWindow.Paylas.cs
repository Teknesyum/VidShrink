using System;
using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.Core;
using CoreShare = VidShrink.Core.Share;
using VidShrink.App.Share;

namespace VidShrink.App;

/// <summary>
/// Kabuk menüsünden açılan küçültme penceresinin paylaşımı. Yükleme kodu burada değil:
/// hedef tablosu <c>paylasim-hedefleri.json</c>'dan, yükleme <see cref="ShareFlow"/> ve
/// <c>Core/Share</c> katmanından geliyor — ana penceredeki küçültme sekmesinin kullandığı
/// yolun aynısı.
///
/// <para>Bu pencerede hedef seçtiren bir kutu yok: ayarlardaki seçim ana pencerenin işi,
/// burada tablonun varsayılanı geçerli. Teslim edilen yol <see cref="ShrinkJobWindow._outputs"/>
/// alanından okunur, ekrandaki metin kutusundan değil.</para>
/// </summary>
public partial class ShrinkJobWindow
{
    private ShareFlow? _shareFlow;
    private CoreShare.ShareTargetTable? _shareTargets;
    private CoreShare.IHttpTransport? _shareTransport;

    /// <summary>Son paylaşımın adresi. Ölçüm kendi gördüğünü okuyabilsin diye açık.</summary>
    internal string ShareLinkText => ShareLinkRow.IsVisible ? TxtShareLink.Text ?? string.Empty : string.Empty;

    /// <summary>Paylaşım satırının son sözü; görünmüyorsa boş.</summary>
    internal string ShareStatusText => TxtShareStatus.IsVisible ? TxtShareStatus.Text ?? string.Empty : string.Empty;

    /// <summary>Paylaş düğmesinin görünürlüğü; ölçü gerçek durumu piksele değil buna bakar.</summary>
    internal bool ShareButtonVisibleForTest => BtnShare.IsVisible;

    internal void ResetShareForTest(bool fileReady) => ResetShare(fileReady);

    /// <summary>Ölçünün gerçek bir kodlama koşturmadan bir çıktı teslim etmiş gibi kurması içindir.</summary>
    internal void AddOutputForTest(string path) => _outputs.Add(path);

    private void ResetShare(bool fileReady)
    {
        BtnShare.IsVisible = fileReady;
        BtnShare.IsEnabled = fileReady;
        BtnShareCancel.IsVisible = false;
        ShareProgress.IsVisible = false;
        ShareProgress.Value = 0;
        ShareLinkRow.IsVisible = false;
        TxtShareLink.Text = string.Empty;
        ShowShareStatus(string.Empty);
    }

    private void ShowShareStatus(string text)
    {
        TxtShareStatus.Text = text;
        TxtShareStatus.IsVisible = text.Length > 0;
    }

    private string? LastOutput() => _outputs.Count > 0 ? _outputs[^1] : null;

    private CoreShare.ShareTarget? Endpoint()
    {
        try { _shareTargets ??= CoreShare.ShareTargetTable.Load(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
        return _shareTargets.DefaultTarget;
    }

    private ShareFlow Share() => _shareFlow ??= new ShareFlow(target =>
        CoreShare.ShareProviderFactory.Create(
            target,
            _shareTransport ??= new CoreShare.HttpClientTransport(),
            _shareTargets));

    private async void OnShare(object? sender, RoutedEventArgs e)
    {
        if (LastOutput() is not { } path)
        {
            ShowShareStatus(Say("settings.share.nothing"));
            return;
        }

        if (Endpoint() is not { } target)
        {
            ShowShareStatus(Say("settings.share.targets-missing", CoreShare.ShareTargetTable.FileName));
            return;
        }

        var flow = Share();
        if (flow.Running) return;

        BtnShare.IsEnabled = false;
        BtnShareCancel.IsVisible = true;
        ShareProgress.IsVisible = true;
        ShareProgress.Value = 0;
        ShareLinkRow.IsVisible = false;
        ShowShareStatus(Say("settings.share.uploading"));

        var progress = new Progress<CoreShare.UploadProgress>(step => ShareProgress.Value = step.Fraction);
        var result = await flow.ShareAsync(target, path, target.DefaultRetentionDays, progress);

        BtnShare.IsEnabled = true;
        BtnShareCancel.IsVisible = false;
        ShareProgress.IsVisible = false;
        ShowShareResult(result);
    }

    private void ShowShareResult(CoreShare.ShareResult result)
    {
        if (result.Ok && result.Link is { } link)
        {
            TxtShareLink.Text = link.Url;
            ShareLinkRow.IsVisible = true;
            ShowShareStatus(link.ExpiresAt is { } expires
                ? Say("settings.share.shared-until", Bicim.Damga(expires, Strings.CultureOf(_language)))
                : Say("settings.share.shared"));
            return;
        }

        ShareLinkRow.IsVisible = false;
        ShowShareStatus(result.Failure == CoreShare.ShareFailure.Cancelled
            ? Say("settings.share.cancelled")
            : $"{Say("settings.share.failed")}: {ShareMessage.Of(result)}");
    }

    private void OnShareCancel(object? sender, RoutedEventArgs e) => _shareFlow?.Cancel();

    private async void OnCopyShareLink(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
            await clipboard.SetTextAsync(TxtShareLink.Text ?? string.Empty);
        }
        catch (Exception)
        {
        }
    }
}
