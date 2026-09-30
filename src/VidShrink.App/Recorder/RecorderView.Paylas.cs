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
    private ShareSession? _shareSession;
    private ShareRetryBinder? _shareRetry;

    private ShareSession Sharing() => _shareSession ??= new ShareSession(() => CreateShareFlow);

    private ShareRetryBinder Retry() => _shareRetry ??= new ShareRetryBinder(
        BtnRecShareRetry, () => Sharing().Targets, id => ShareOnce(id));

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

    internal Func<ShareFlow>? CreateShareFlow { get; set; }

    private void OnShare(object? sender, RoutedEventArgs e) => ShareOnce(null);

    private async void ShareOnce(string? targetIdOverride) => await ShareCoreAsync(targetIdOverride);

    /// <summary>Tek paylaşım; sonucu "bitince" eylemi de okur. Başlamadıysa <c>null</c>, neden paylaşım satırında.</summary>
    private async System.Threading.Tasks.Task<CoreShare.ShareResult?> ShareCoreAsync(string? targetIdOverride)
    {
        if (Delivered() is not { } path)
        {
            ShowShareStatus(ShareSession.Nothing());
            return null;
        }

        var session = Sharing();
        if (session.Target(targetIdOverride) is not { } target)
        {
            ShowShareStatus(ShareSession.MissingTargets());
            return null;
        }

        if (session.Running) return null;

        BtnRecShare.IsEnabled = false;
        BtnRecShareCancel.IsVisible = true;
        Retry().Hide();
        RecShareProgress.IsVisible = true;
        RecShareProgress.Value = 0;
        RecShareLinkRow.IsVisible = false;
        TxtRecShareLink.Text = string.Empty;
        ShowShareStatus(ShareSession.Uploading());

        var progress = new Progress<CoreShare.UploadProgress>(step => RecShareProgress.Value = step.Fraction);
        var result = await session.ShareAsync(target, path, progress);

        BtnRecShare.IsEnabled = true;
        BtnRecShareCancel.IsVisible = false;
        RecShareProgress.IsVisible = false;
        ShowShareResult(result);
        return result;
    }

    private void ShowShareResult(CoreShare.ShareResult result)
    {
        if (result.Ok && result.Link is { } link)
        {
            TxtRecShareLink.Text = link.Url;
            RecShareLinkRow.IsVisible = true;
            Retry().Hide();
            ShowShareStatus(ShareSession.Status(result));
            return;
        }

        RecShareLinkRow.IsVisible = false;
        TxtRecShareLink.Text = string.Empty;
        Retry().Show(result);
        ShowShareStatus(ShareSession.Status(result));
    }

    private void OnShareCancel(object? sender, RoutedEventArgs e) => _shareSession?.Cancel();

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
