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
    private ShareRetryBinder? _shareRetry;

    private ShareRetryBinder Retry() => _shareRetry ??= new ShareRetryBinder(
        BtnRecShareRetry, () => _shareTargets, id => ShareOnce(id));

    /// <summary>Yeniden deneme düğmesinin o anki hali; ölçü piksele değil buna bakar.</summary>
    internal ShareRetryPrompt ShareRetryPromptForTest => Retry().Current;

    /// <summary>Son paylaşımın adresi. Ölçüm kendi gördüğünü okuyabilsin diye açık.</summary>
    internal string ShareLinkText => RecShareLinkRow.IsVisible ? TxtRecShareLink.Text ?? string.Empty : string.Empty;

    /// <summary>Paylaşım satırının son sözü; görünmüyorsa boş.</summary>
    internal string ShareStatusText => TxtRecShareStatus.IsVisible ? TxtRecShareStatus.Text ?? string.Empty : string.Empty;

    private void ResetShare()
    {
        BtnRecShare.IsEnabled = true;
        BtnRecShareCancel.IsVisible = false;
        Retry().Hide();
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

    internal Func<ShareFlow>? CreateShareFlow { get; set; }

    private ShareFlow Share() => _shareFlow ??= CreateShareFlow?.Invoke() ?? new ShareFlow(target =>
        CoreShare.ShareProviderFactory.Create(
            target,
            _shareTransport ??= new CoreShare.HttpClientTransport(),
            _shareTargets));

    private void OnShare(object? sender, RoutedEventArgs e) => ShareOnce(null);

    private async void ShareOnce(string? targetIdOverride)
    {
        if (Delivered() is not { } path)
        {
            ShowShareStatus(Say("settings.share.nothing"));
            return;
        }

        var target = targetIdOverride is null
            ? Endpoint()
            : _shareTargets?.Find(targetIdOverride) ?? Endpoint();

        if (target is null)
        {
            ShowShareStatus(Say("settings.share.targets-missing", CoreShare.ShareTargetTable.FileName));
            return;
        }

        var flow = Share();
        if (flow.Running) return;

        BtnRecShare.IsEnabled = false;
        BtnRecShareCancel.IsVisible = true;
        Retry().Hide();
        RecShareProgress.IsVisible = true;
        RecShareProgress.Value = 0;
        RecShareLinkRow.IsVisible = false;
        TxtRecShareLink.Text = string.Empty;
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
            Retry().Hide();
            ShowShareStatus(link.ExpiresAt is { } expires
                ? Say("settings.share.shared-until", Bicim.Damga(expires, Strings.Culture))
                : Say("settings.share.shared"));
            return;
        }

        RecShareLinkRow.IsVisible = false;
        TxtRecShareLink.Text = string.Empty;
        Retry().Show(result);
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
            await clipboard.SetTextAsync(TxtRecShareLink.Text ?? string.Empty);
        }
        catch (Exception)
        {
        }
    }
}
