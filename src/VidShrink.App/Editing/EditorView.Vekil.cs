using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Styling;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private CancellationTokenSource? _proxyCts;

    internal string ProxyFolder { get; set; } = ProxyMedia.DefaultFolder;

    internal bool MakingProxy => _proxyCts is not null;

    internal IReadOnlyList<string> PreviewPaths => ProxyMedia.PreviewPaths(ProxyFolder, SourcePaths);

    internal string ProxyStatusText => TxtProxyStatus.Text ?? string.Empty;

    private void InitProxy()
    {
        BtnProxyCreate.Click += (_, _) => _ = CreateProxiesAsync();
        BtnProxyCancel.Click += (_, _) => _proxyCts?.Cancel();
        BtnProxyDelete.Click += (_, _) => DeleteProxies();
    }

    private void CancelProxy()
    {
        _proxyCts?.Cancel();
        TxtProxyStatus.Text = string.Empty;
    }

    private bool HasProxy(string path) => ProxyMedia.Find(ProxyFolder, path) is not null;

    private void RefreshProxy()
    {
        var sources = SourcePaths;
        var busy = MakingProxy;
        BtnProxyCreate.IsEnabled = _model is not null && !busy && !Exporting && sources.Any(path => !HasProxy(path));
        BtnProxyDelete.IsEnabled = _model is not null && !busy && sources.Any(HasProxy);
        BtnProxyCancel.IsVisible = busy;
        ProxyProgress.IsVisible = busy;
        if (busy || TxtProxyStatus.Text is { Length: > 0 }) return;
        if (_source is { } source && !HasProxy(source) && KnownInfo?.Invoke(source) is { } info && ProxyMedia.IsHeavy(info))
            ShowProxyStatus("Hint", Strings.Get("editor.proxy.suggest"));
    }

    private void ShowProxyStatus(string theme, string text)
    {
        if (this.TryFindResource(theme, out var found) && found is ControlTheme controlTheme) TxtProxyStatus.Theme = controlTheme;
        TxtProxyStatus.Text = text;
    }

    internal async Task<bool> CreateProxiesAsync()
    {
        if (_model is null || MakingProxy || Exporting) return false;
        var targets = SourcePaths.Where(path => !HasProxy(path)).ToArray();
        if (targets.Length == 0) return false;

        var cts = new CancellationTokenSource();
        _proxyCts = cts;
        ProxyProgress.Value = 0;
        RefreshProxy();
        var made = 0;
        try
        {
            for (var i = 0; i < targets.Length; i++)
            {
                var path = targets[i];
                ShowProxyStatus("Hint", string.Format(Strings.Culture, Strings.Get("editor.proxy.making"), Path.GetFileName(path)));
                var info = await ProbeSourceAsync(path).ConfigureAwait(true);
                if (cts.IsCancellationRequested) break;
                if (info is not { Width: > 0, Height: > 0 } || !(info.DurationSeconds > 0)) continue;
                var step = i;
                var progress = new Progress<EncodeProgress>(p =>
                {
                    if (ReferenceEquals(_proxyCts, cts)) ProxyProgress.Value = (step + Math.Clamp(p.Fraction, 0, 1)) / targets.Length;
                });
                await ProxyRunner.CreateAsync(path, ProxyFolder, info.DurationSeconds, info.Width, info.Height, progress, cts.Token).ConfigureAwait(true);
                made++;
            }

            if (cts.IsCancellationRequested) ShowProxyStatus("Hint", Strings.Get("editor.proxy.canceled"));
            else ShowProxyStatus("Hint", made > 0 ? Strings.Get("editor.proxy.ready") : string.Empty);
        }
        catch (OperationCanceledException)
        {
            ShowProxyStatus("Hint", Strings.Get("editor.proxy.canceled"));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            ShowProxyStatus("StatusError", string.Format(Strings.Culture, Strings.Get("editor.proxy.failed"), ex.Message));
        }
        finally
        {
            if (ReferenceEquals(_proxyCts, cts)) _proxyCts = null;
            cts.Dispose();
            RefreshProxy();
        }

        if (made > 0 && _model is not null) await ReloadAsync(Timeline.Playhead).ConfigureAwait(true);
        return made > 0 && !cts.IsCancellationRequested;
    }

    internal int DeleteProxies()
    {
        if (MakingProxy) return 0;
        var removed = SourcePaths.Sum(path => ProxyMedia.Delete(ProxyFolder, path));
        ShowProxyStatus("Hint", removed > 0 ? Strings.Get("editor.proxy.deleted") : string.Empty);
        RefreshProxy();
        if (removed > 0 && _model is not null) _ = ReloadAsync(Timeline.Playhead);
        return removed;
    }

    private (int Width, int Height) PreviewSize(EdlPreview preview, string source, int width, int height)
        => preview.SourcePaths.Count > 0 && !PathEquality.Same(preview.SourcePaths[0], source) ? ProxyMedia.Size(width, height) : (width, height);
}
