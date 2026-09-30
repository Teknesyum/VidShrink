using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>Kayıt bitince kendiliğinden koşan eylem. Varsayılan hiçbir şey: pano sorulmadan ezilmez.</summary>
internal enum RecorderFinishAction
{
    Nothing,
    CopyFile,
    ShrinkAndCopy,
    UploadAndCopyLink
}

/// <summary>
/// Kaydın bittiği anda panoya giden dosya ya da bağlantı. Pano bu arayüzün arkasında;
/// ölçüler sahtesini verir, gerçek panoya yazmaz.
/// </summary>
internal interface IRecordingClipboard
{
    Task SetFileAsync(string path);

    Task SetTextAsync(string text);
}

/// <summary>Pencerenin panosu. Dosya depolama sağlayıcısından öğe olarak verilir; Gezgin ve Discord dosya olarak yapıştırır.</summary>
internal sealed class TopLevelRecordingClipboard : IRecordingClipboard
{
    private readonly Control _owner;

    internal TopLevelRecordingClipboard(Control owner) => _owner = owner;

    public async Task SetFileAsync(string path)
    {
        var top = Top();
        var file = await top.StorageProvider.TryGetFileFromPathAsync(path)
                   ?? throw new FileNotFoundException(path, path);
        await (top.Clipboard ?? throw Missing()).SetFileAsync(file);
    }

    public async Task SetTextAsync(string text)
        => await (Top().Clipboard ?? throw Missing()).SetTextAsync(text);

    private TopLevel Top() => TopLevel.GetTopLevel(_owner) ?? throw Missing();

    private static InvalidOperationException Missing()
        => new(VidShrink.App.LanguageCatalog.Display(Localization.Strings.Get("recorder.finish.no-clipboard")));
}

/// <summary>
/// "Bitince" seçimi. Eylem yalnız tam ve teslim edilmiş kayıtta koşar: yarım, başarısız, GIF'e
/// çevrilemeyen ya da teslim kabına taşınamayan kayıtta hiçbir şey çağrılmaz. Küçültme ana
/// pencerenin kuyruğundan (<see cref="ShrinkForFinish"/>), yükleme <c>ShareFlow</c>'dan geçer.
/// Sonuç sonuç panelinde tek satırdır; hata <c>StatusError</c> ile yazılır, yutulmaz.
/// </summary>
internal partial class RecorderView
{
    private IRecordingClipboard? _finishClipboard;

    internal IRecordingClipboard FinishClipboard
    {
        get => _finishClipboard ??= new TopLevelRecordingClipboard(this);
        set => _finishClipboard = value;
    }

    internal Func<string, Task<string?>>? ShrinkForFinish { get; set; }

    internal Task FinishRun { get; private set; } = Task.CompletedTask;

    internal string FinishStatusText => TxtFinishStatus.IsVisible ? TxtFinishStatus.Text ?? string.Empty : string.Empty;

    internal bool FinishStatusIsError { get; private set; }

    internal RecorderFinishAction SelectedFinishAction
    {
        get => Enum.IsDefined((RecorderFinishAction)CmbFinishAction.SelectedIndex)
            ? (RecorderFinishAction)CmbFinishAction.SelectedIndex
            : RecorderFinishAction.Nothing;
        set => CmbFinishAction.SelectedIndex = (int)value;
    }

    private void InitBitince()
    {
        CmbFinishAction.ItemsSource = FinishLabels();
        CmbFinishAction.SelectedIndex = (int)_settings.FinishAction;
        CmbFinishAction.SelectionChanged += (_, _) =>
        {
            if (CmbFinishAction.SelectedIndex < 0) return;
            _settings.FinishAction = SelectedFinishAction;
            _settings.Save(_settingsPath);
        };
    }

    private static List<string> FinishLabels()
        => Enum.GetValues<RecorderFinishAction>().Select(action => Say(FinishLabelKey(action))).ToList();

    internal static string FinishLabelKey(RecorderFinishAction action) => action switch
    {
        RecorderFinishAction.CopyFile => "recorder.finish.copy",
        RecorderFinishAction.ShrinkAndCopy => "recorder.finish.shrink-copy",
        RecorderFinishAction.UploadAndCopyLink => "recorder.finish.upload-copy",
        _ => "recorder.finish.nothing"
    };

    private void RefreshFinishLabels()
    {
        var selected = CmbFinishAction.SelectedIndex;
        CmbFinishAction.ItemsSource = FinishLabels();
        CmbFinishAction.SelectedIndex = selected >= 0 ? selected : 0;
    }

    /// <summary>Teslim edilen kayıt eylemi hak ediyor mu. Karar kayıt sonucundan, dosya diskte durmalı.</summary>
    internal static bool FinishAllowed(RecordResult result)
        => result.Ok && !result.Partial && result.MissingGif is null && result.DeliveryError is null;

    private void StartFinishAction(RecordResult result)
    {
        var action = _settings.FinishAction;
        if (action == RecorderFinishAction.Nothing || !FinishAllowed(result) || Delivered() is not { } path) return;
        FinishRun = FinishAsync(action, path);
    }

    internal async Task FinishAsync(RecorderFinishAction action, string path)
    {
        HideFinish();
        try
        {
            switch (action)
            {
                case RecorderFinishAction.CopyFile:
                    await FinishClipboard.SetFileAsync(path);
                    ShowFinish(Say("recorder.finish.copied"), error: false);
                    break;
                case RecorderFinishAction.ShrinkAndCopy:
                    await ShrinkAndCopyAsync(path);
                    break;
                case RecorderFinishAction.UploadAndCopyLink:
                    await UploadAndCopyAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowFinish(Say("recorder.finish.failed", ex.Message), error: true);
        }
    }

    private async Task ShrinkAndCopyAsync(string path)
    {
        if (ShrinkForFinish is not { } shrink)
        {
            ShowFinish(Say("recorder.finish.shrink-failed"), error: true);
            return;
        }

        ShowFinish(Say("recorder.finish.shrinking"), error: false);
        var output = await shrink(path);
        if (string.IsNullOrWhiteSpace(output) || !File.Exists(output))
        {
            ShowFinish(Say("recorder.finish.shrink-failed"), error: true);
            return;
        }

        await FinishClipboard.SetFileAsync(output);
        ShowFinish(Say("recorder.finish.shrunk-copied"), error: false);
    }

    private async Task UploadAndCopyAsync()
    {
        if (await ShareCoreAsync(null) is { Ok: true, Link: { } link })
        {
            await FinishClipboard.SetTextAsync(link.Url);
            ShowFinish(Say("recorder.finish.link-copied"), error: false);
            return;
        }

        ShowFinish(Say("recorder.finish.upload-failed"), error: true);
    }

    private void ShowFinish(string text, bool error)
    {
        if (Application.Current?.TryFindResource(error ? "StatusError" : "StatusSuccess", out var kaynak) == true && kaynak is ControlTheme tema)
            TxtFinishStatus.Theme = tema;
        FinishStatusIsError = error;
        TxtFinishStatus.Text = text;
        TxtFinishStatus.IsVisible = text.Length > 0;
    }

    private void HideFinish() => ShowFinish(string.Empty, error: false);
}
