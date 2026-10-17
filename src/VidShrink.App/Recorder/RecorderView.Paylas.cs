using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// <para>Paylaş satırında hedef ve ömür kutusu ile Sil düğmesi Küçült sekmesinin parçalarıyla
/// aynı kuralı izler: ömür yalnız hedef seçtiriyorsa kutudur, seçtirmiyorsa sabit ömür yazılır;
/// Sil yalnız silme jetonu veren hedefte görünür ve elde jeton varken basılır. Seçim
/// <c>recorder-settings.json</c>'a gider. Tablo bulunamazsa kutular gizlenir, düğme sessizce
/// başarısız olmuyor, hangi dosyanın aranıp bulunamadığını yazıyor.</para>
/// </summary>
internal partial class RecorderView
{
    private ShareSession? _shareSession;
    private ShareRetryBinder? _shareRetry;
    private bool _shareSyncing;

    private ShareSession Sharing() => _shareSession ??= new ShareSession(() => CreateShareFlow);

    private ShareRetryBinder Retry() => _shareRetry ??= new ShareRetryBinder(
        BtnRecShareRetry, () => Sharing().Targets, id => ShareOnce(id));

    /// <summary>Yeniden deneme düğmesinin o anki hali; ölçü piksele değil buna bakar.</summary>
    internal ShareRetryPrompt ShareRetryPromptForTest => Retry().Current;

    /// <summary>Son paylaşımın adresi. Ölçüm kendi gördüğünü okuyabilsin diye açık.</summary>
    internal string ShareLinkText => RecShareLinkRow.IsVisible ? TxtRecShareLink.Text ?? string.Empty : string.Empty;

    /// <summary>Paylaşım satırının son sözü; görünmüyorsa boş.</summary>
    internal string ShareStatusText => TxtRecShareStatus.IsVisible ? TxtRecShareStatus.Text ?? string.Empty : string.Empty;

    internal IReadOnlyList<CoreShare.ShareTarget> ShareTargetChoices => Sharing().Targets?.Targets ?? Array.Empty<CoreShare.ShareTarget>();

    internal int ShareTargetIndex
    {
        get => CmbRecShareTarget.SelectedIndex;
        set => CmbRecShareTarget.SelectedIndex = value;
    }

    internal int ShareRetentionIndex
    {
        get => CmbRecShareRetention.SelectedIndex;
        set => CmbRecShareRetention.SelectedIndex = value;
    }

    internal bool ShareRetentionVisible => CmbRecShareRetention.IsVisible;

    internal string ShareRetentionFixedText => TxtRecShareRetentionFixed.IsVisible ? TxtRecShareRetentionFixed.Text ?? string.Empty : string.Empty;

    internal bool ShareDeleteVisible => BtnRecShareDelete.IsVisible;

    internal bool ShareDeleteEnabled => BtnRecShareDelete.IsVisible && BtnRecShareDelete.IsEnabled;

    internal CoreShare.ShareTarget? SelectedShareTarget
    {
        get
        {
            var targets = ShareTargetChoices;
            var index = CmbRecShareTarget.SelectedIndex;
            return index >= 0 && index < targets.Count ? targets[index] : Sharing().Target();
        }
    }

    internal int? SelectedShareRetentionDays
    {
        get
        {
            if (SelectedShareTarget is not { } target || target.RetentionDays.Count == 0) return null;
            var index = CmbRecShareRetention.SelectedIndex;
            return index >= 0 && index < target.RetentionDays.Count ? target.RetentionDays[index] : target.DefaultRetentionDays;
        }
    }

    private void InitPaylas()
    {
        Sharing().Target();
        var targets = ShareTargetChoices;
        _shareSyncing = true;
        CmbRecShareTarget.ItemsSource = targets.Select(target => target.DisplayName).ToList();
        var chosen = _settings.ShareTargetId is { } id ? IndexOfShareTarget(id) : -1;
        if (chosen < 0 && Sharing().Target() is { } fallback) chosen = IndexOfShareTarget(fallback.Id);
        CmbRecShareTarget.SelectedIndex = targets.Count > 0 ? Math.Max(0, chosen) : -1;
        CmbRecShareTarget.IsVisible = targets.Count > 0;
        _shareSyncing = false;
        RefreshShareTargetRow(_settings.ShareRetentionDays);

        CmbRecShareTarget.SelectionChanged += (_, _) =>
        {
            if (_shareSyncing || CmbRecShareTarget.SelectedIndex < 0) return;
            RefreshShareTargetRow(null);
            SaveShareChoice();
        };
        CmbRecShareRetention.SelectionChanged += (_, _) =>
        {
            if (_shareSyncing || CmbRecShareRetention.SelectedIndex < 0) return;
            SaveShareChoice();
        };
    }

    private int IndexOfShareTarget(string id)
    {
        var targets = ShareTargetChoices;
        for (var index = 0; index < targets.Count; index++)
            if (string.Equals(targets[index].Id, id, StringComparison.OrdinalIgnoreCase))
                return index;
        return -1;
    }

    private void SaveShareChoice()
    {
        _settings.ShareTargetId = SelectedShareTarget?.Id;
        _settings.ShareRetentionDays = SelectedShareRetentionDays;
        _settings.Save(_settingsPath);
    }

    private void RefreshShareTargetRow(int? wantedDays)
    {
        var target = CmbRecShareTarget.IsVisible ? SelectedShareTarget : null;
        _shareSyncing = true;
        var days = target?.RetentionDays ?? Array.Empty<int>();
        CmbRecShareRetention.IsVisible = days.Count > 0;
        if (days.Count > 0)
        {
            CmbRecShareRetention.ItemsSource = days
                .Select(day => day == 1 ? Say("settings.share.day-one") : Say("settings.share.days", day))
                .ToList();
            var chosen = days.ToList().IndexOf(wantedDays ?? target!.DefaultRetentionDays ?? -1);
            CmbRecShareRetention.SelectedIndex = chosen >= 0 ? chosen : 0;
        }
        else
        {
            CmbRecShareRetention.ItemsSource = null;
        }

        TxtRecShareRetentionFixed.IsVisible = target is not null && days.Count == 0;
        TxtRecShareRetentionFixed.Text = target is null || days.Count > 0
            ? string.Empty
            : target.FixedRetentionHours is { } hours
                ? hours == 1 ? Say("settings.share.hour-one-fixed") : Say("settings.share.hours-fixed", hours)
                : Say("settings.share.not-stated");
        _shareSyncing = false;

        BtnRecShareDelete.IsVisible = target?.CanDelete == true || Sharing().CanDelete;
        BtnRecShareDelete.IsEnabled = Sharing().CanDelete;
    }

    private void RefreshShareLabels()
    {
        var keep = CmbRecShareRetention.SelectedIndex;
        var target = SelectedShareTarget;
        RefreshShareTargetRow(target is not null && keep >= 0 && keep < target.RetentionDays.Count ? target.RetentionDays[keep] : null);
    }

    private void ResetShare()
    {
        BtnRecShare.IsEnabled = true;
        BtnRecShareCancel.IsVisible = false;
        Retry().Hide();
        RecShareProgress.IsVisible = false;
        RecShareProgress.Value = 0;
        RecShareLinkRow.IsVisible = false;
        TxtRecShareLink.Text = string.Empty;
        BtnRecShareDelete.IsEnabled = false;
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

    /// <summary>
    /// Tek paylaşım; sonucu "bitince" eylemi de okur. Başlamadıysa <c>null</c>, neden paylaşım satırında.
    /// Hedef ve ömür satırdaki kutulardan gelir; yeniden deneme başka bir hedef verirse o hedefin
    /// kendi varsayılan ömrü geçer. <paramref name="pathOverride"/> verilirse teslim edilen kayıt
    /// yerine o dosya yüklenir (küçült, yükle, kopyala zinciri küçültülmüş çıktıyı yükler).
    /// </summary>
    private async System.Threading.Tasks.Task<CoreShare.ShareResult?> ShareCoreAsync(string? targetIdOverride, string? pathOverride = null)
    {
        if ((pathOverride ?? Delivered()) is not { } path || !File.Exists(path))
        {
            ShowShareStatus(ShareSession.Nothing());
            return null;
        }

        var session = Sharing();
        var selected = SelectedShareTarget;
        if (session.Target(targetIdOverride ?? selected?.Id) is not { } target)
        {
            ShowShareStatus(ShareSession.MissingTargets());
            return null;
        }

        if (session.Running) return null;

        var days = selected is not null && string.Equals(selected.Id, target.Id, StringComparison.OrdinalIgnoreCase)
            ? SelectedShareRetentionDays
            : target.DefaultRetentionDays;

        BtnRecShare.IsEnabled = false;
        BtnRecShareCancel.IsVisible = true;
        BtnRecShareDelete.IsEnabled = false;
        Retry().Hide();
        RecShareProgress.IsVisible = true;
        RecShareProgress.Value = 0;
        RecShareLinkRow.IsVisible = false;
        TxtRecShareLink.Text = string.Empty;
        ShowShareStatus(ShareSession.Uploading());

        var progress = new Progress<CoreShare.UploadProgress>(step => RecShareProgress.Value = step.Fraction);
        var result = await session.ShareAsync(target, path, days, progress);

        BtnRecShare.IsEnabled = true;
        BtnRecShareCancel.IsVisible = false;
        RecShareProgress.IsVisible = false;
        ShowShareResult(result);
        return result;
    }

    private void ShowShareResult(CoreShare.ShareResult result)
    {
        BtnRecShareDelete.IsEnabled = Sharing().CanDelete;
        if (Sharing().CanDelete) BtnRecShareDelete.IsVisible = true;
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

    private async void OnShareDelete(object? sender, RoutedEventArgs e) => await DeleteShareAsync();

    /// <summary>Son paylaşımı kapatır. Kapanınca bağlantı satırı düşer; kapanmazsa düğme jeton durdukça açık kalır.</summary>
    internal async System.Threading.Tasks.Task<bool> DeleteShareAsync()
    {
        var session = Sharing();
        if (!session.CanDelete) return false;

        BtnRecShareDelete.IsEnabled = false;
        var result = await session.DeleteAsync();
        BtnRecShareDelete.IsEnabled = session.CanDelete;
        if (result.Ok)
        {
            RecShareLinkRow.IsVisible = false;
            TxtRecShareLink.Text = string.Empty;
            ShowShareStatus(ShareSession.Closed());
            return true;
        }

        ShowShareStatus(ShareSession.CloseFailed(result));
        return false;
    }

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
