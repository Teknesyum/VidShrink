using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    internal const int ResultGifFps = 10;

    internal const int ResultGifWidth = 640;

    internal Func<IReadOnlyList<string>, Task<bool>> ResultGifRun { get; set; } = async args => (await FfmpegRunner.RunAsync(args)).Ok;

    internal bool CopyFileVisible => BtnCopyFile.IsVisible && BtnCopyFile.IsEnabled;

    internal bool GifVisible => BtnToGif.IsVisible;

    internal bool MoreVisible => BtnResultMore.IsVisible;

    internal IReadOnlyList<MenuItem> MoreItems => ((MenuFlyout)BtnResultMore.Flyout!).Items.OfType<MenuItem>().ToList();

    internal string NamePreviewText => TxtNamePreview.Text ?? string.Empty;

    internal bool NamePreviewIsError { get; private set; }

    internal string? NamePatternText
    {
        get => TxtNamePattern.Text;
        set => TxtNamePattern.Text = value;
    }

    private void InitSonucPaneli()
    {
        TxtNamePattern.Text = _settings.NamePattern ?? AdlandirmaDeseni.KayitVarsayilan;
        RefreshNamePreview();
        TxtNamePattern.TextChanged += (_, _) => OnNamePatternChanged();
        InitCercevele();
    }

    private void ResetSonucPaneli(RecordResult result)
    {
        BtnCopyFile.IsVisible = Delivered() is not null;
        BtnCopyFile.IsEnabled = !result.Partial || result.Playable != false;
        BtnToGif.IsVisible = result.Ok && !result.Partial
                             && RecorderArguments.ContainerOf(result.OutputPath) != RecorderContainer.Gif;
        BtnResultMore.IsVisible = BtnToMp4.IsVisible || BtnToGif.IsVisible;
        ResetCercevele(result);
    }

    private async void OnCopyFile(object? sender, RoutedEventArgs e) => await CopyResultFileAsync();

    internal async Task<bool> CopyResultFileAsync()
    {
        if (Delivered() is not { } path) return false;
        HideFinish();
        try
        {
            await FinishClipboard.SetFileAsync(path);
            ShowFinish(Say("recorder.finish.copied"), error: false);
            return true;
        }
        catch (Exception ex)
        {
            ShowFinish(Say("recorder.finish.failed", ex.Message), error: true);
            return false;
        }
    }

    private async void OnToGif(object? sender, RoutedEventArgs e) => await SaveAsGifAsync();

    internal async Task<string?> SaveAsGifAsync()
    {
        if (Delivered() is not { } source) return null;

        var target = RecorderArguments.SiblingTarget(source, ".gif", File.Exists);
        BtnToGif.IsEnabled = false;
        ClearNoticeAndError();
        ShowNotice(Say("recorder.output.gif-running"));
        bool ok;
        try { ok = await ResultGifRun(GifPalette.BuildFitted(source, target, ResultGifFps, ResultGifWidth)) && File.Exists(target); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            ok = false;
        }
        finally
        {
            BtnToGif.IsEnabled = true;
        }

        ClearNoticeAndError();
        if (ok)
        {
            ShowNotice(Say("recorder.output.gif-saved", target));
            return target;
        }

        try { if (File.Exists(target)) File.Delete(target); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        ShowError(Say("recorder.output.gif-convert-failed"));
        return null;
    }

    private void OnResetNamePattern(object? sender, RoutedEventArgs e)
        => TxtNamePattern.Text = AdlandirmaDeseni.KayitVarsayilan;

    private void OnNamePatternChanged()
    {
        if (!RefreshNamePreview()) return;
        var pattern = TxtNamePattern.Text!;
        _settings.NamePattern = pattern == AdlandirmaDeseni.KayitVarsayilan ? null : pattern;
        _settings.Save(_settingsPath);
    }

    private bool RefreshNamePreview()
    {
        var pattern = TxtNamePattern.Text;
        var error = AdlandirmaDeseni.KayitHata(pattern);
        NamePreviewIsError = error is not null;
        if (Application.Current?.TryFindResource(NamePreviewIsError ? "StatusError" : "Hint", out var kaynak) == true && kaynak is ControlTheme tema)
            TxtNamePreview.Theme = tema;

        if (error is not null)
        {
            TxtNamePreview.Text = Say(error == "settings-tab.output-name.bad-placeholder" ? "recorder.name.bad-placeholder" : error);
            return false;
        }

        var sample = AdlandirmaDeseni.KayitYolu(string.Empty, pattern, "." + RecorderArguments.Extension(_settings.Container),
            DateTime.Now, _settings.Target == RecorderTargetKind.Window ? _settings.WindowTitle : null, _ => false);
        TxtNamePreview.Text = Say("settings-tab.output-name.preview", sample);
        return true;
    }
}
