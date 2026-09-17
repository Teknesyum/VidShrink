using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    internal bool TrimIdleVisible => BtnTrimIdle.IsVisible;

    private async void OnTrimIdle(object? sender, RoutedEventArgs e)
        => await TrimIdleAsync(path => IdleTrimmer.RunAsync(path));

    internal async Task<IdleTrimResult?> TrimIdleAsync(Func<string, Task<IdleTrimResult>> run)
    {
        if (Delivered() is not { } source) return null;

        BtnTrimIdle.IsEnabled = false;
        ClearNoticeAndError();
        ShowNotice(Say("recorder.output.trim-running"));
        IdleTrimResult result;
        try { result = await run(source); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            result = new IdleTrimResult(false, null, 0, ex.Message);
        }
        finally
        {
            BtnTrimIdle.IsEnabled = true;
        }

        ClearNoticeAndError();
        if (!result.Ok)
            ShowError(Say("recorder.output.trim-failed", result.Error));
        else if (result.Target is null)
            ShowNotice(Say("recorder.output.trim-none"));
        else
            ShowNotice(Say("recorder.output.trim-done", result.RemovedSeconds.ToString("0.0", Strings.Culture), result.Target));

        return result;
    }

    private void ClearNoticeAndError()
    {
        TxtNotice.IsVisible = false;
        TxtError.IsVisible = false;
    }
}
