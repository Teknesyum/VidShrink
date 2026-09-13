using System;
using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using CoreShare = VidShrink.Core.Share;

namespace VidShrink.App.Recorder;

/// <summary>
/// Kayıt sonrası paylaşım. Yükleme kodu burada değil: hedef tablosu
/// <c>paylasim-hedefleri.json</c>'dan, yükleme <see cref="ShareFlow"/> ve
/// <c>Core/Share</c> katmanından geliyor — küçültme sekmesinin kullandığı yolun aynısı.
///
/// <para>Kaydedicide hedef seçtiren bir kutu yok: ayarlardaki seçim küçültme sekmesinin
/// işi, burada tablonun varsayılanı geçerli. Tablo bulunamazsa düğme sessizce başarısız
/// olmuyor, hangi dosyanın aranıp bulunamadığını yazıyor.</para>
/// </summary>
internal partial class RecorderView
{
    private ShareFlow? _shareFlow;
    private CoreShare.ShareTargetTable? _shareTargets;
    private CoreShare.IHttpTransport? _shareTransport;

    /// <summary>Son paylaşımın adresi. Ölçüm kendi gördüğünü okuyabilsin diye açık.</summary>
    internal string ShareLinkText => RecShareLinkRow.IsVisible ? TxtRecShareLink.Text ?? string.Empty : string.Empty;

    /// <summary>Paylaşım satırının son sözü; görünmüyorsa boş.</summary>
    internal string ShareStatusText => TxtRecShareStatus.IsVisible ? TxtRecShareStatus.Text ?? string.Empty : string.Empty;

    private void ResetShare()
    {
        BtnRecShare.IsEnabled = true;
        BtnRecShareCancel.IsVisible = false;
        RecShareProgress.IsVisible = false;
        RecShareProgress.Value = 0;
        RecShareLinkRow.IsVisible = false;
        TxtRecShareLink.Text = string.Empty;
        ShowShareStatus(string.Empty);
    }

    private void ShowShareStatus(string text)
    {
        TxtRecShareStatus.Text = text;
        TxtRecShareStatus.IsVisible = text.Length > 0;
    }

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
        if (Delivered() is not { } path)
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

        BtnRecShare.IsEnabled = false;
        BtnRecShareCancel.IsVisible = true;
        RecShareProgress.IsVisible = true;
        RecShareProgress.Value = 0;
        RecShareLinkRow.IsVisible = false;
        ShowShareStatus(Say("settings.share.uploading"));

        var progress = new Progress<CoreShare.UploadProgress>(step => RecShareProgress.Value = step.Fraction);
        var result = await flow.ShareAsync(target, path, target.DefaultRetentionDays, progress);

        BtnRecShare.IsEnabled = true;
        BtnRecShareCancel.IsVisible = false;
        RecShareProgress.IsVisible = false;
        ShowShareResult(result);
    }

    private void ShowShareResult(CoreShare.ShareResult result)
    {
        if (result.Ok && result.Link is { } link)
        {
            TxtRecShareLink.Text = link.Url;
            RecShareLinkRow.IsVisible = true;
            ShowShareStatus(link.ExpiresAt is { } expires
                ? Say("settings.share.shared-until", expires.ToLocalTime().ToString("d MMMM HH:mm", Strings.Culture))
                : Say("settings.share.shared"));
            return;
        }

        RecShareLinkRow.IsVisible = false;
        ShowShareStatus(result.Failure == CoreShare.ShareFailure.Cancelled
            ? Say("settings.share.cancelled")
            : $"{Say("settings.share.failed")}: {result.Message}");
    }

    private void OnShareCancel(object? sender, RoutedEventArgs e) => _shareFlow?.Cancel();

    private async void OnCopyShareLink(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
            await clipboard.SetTextAsync(TxtRecShareLink.Text ?? string.Empty);
        }
        catch (Exception)
        {
        }
    }
}
