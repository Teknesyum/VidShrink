using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.Core;
using VidShrink.Player;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    private const string TitleFilled = "dolu";
    private const string TitleMenuClass = "menude";

    private MenuFlyout? _titleMenu;

    internal event Action? MediaTitleMenuChanged;

    internal string? MediaTitleText => MediaTitle.Classes.Contains(TitleFilled) ? TxtMediaTitle.Text : null;

    internal bool MediaTitleShown => MediaTitle.IsEffectivelyVisible;

    internal bool MediaTitleMenuOpen => _titleMenu?.IsOpen ?? false;

    internal MenuFlyout? MediaTitleMenu => _titleMenu;

    private const int ListTitleMaxBytes = 64 * 1024;
    private static readonly HttpClient ListTitleClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>Test konağı uzak pls listesini okumaz; okuyan ölçü <see cref="ListTitleReader"/>'ı kendisi verir.</summary>
    internal static bool ListTitleDisabled { get; set; }

    internal Func<string, CancellationToken, Task<string?>>? ListTitleReader { get; set; } = ListTitleDisabled ? null : ReadListTitleAsync;

    private void ShowMediaTitle(string path, IPlaybackEngine? engine = null)
    {
        var stream = StreamTitle(path, engine);
        SetMediaTitle(stream ?? MediaName(path));
        ToolTip.SetTip(MediaTitle, MediaAddress.WithoutQuery(path));
        if (stream is null && IsRemotePls(path) && ListTitleReader is { } reader) _ = FetchListTitleAsync(path, reader);
    }

    private void SetMediaTitle(string name)
    {
        TxtMediaTitle.Text = name;
        AutomationProperties.SetName(MediaTitle, name);
        MediaTitle.Classes.Set(TitleFilled, name.Length > 0);
    }

    private static bool IsRemotePls(string path)
        => MediaAddress.IsAddress(path) && string.Equals(Path.GetExtension(MediaAddress.WithoutQuery(path)), ".pls", StringComparison.OrdinalIgnoreCase);

    private async Task FetchListTitleAsync(string path, Func<string, CancellationToken, Task<string?>> reader)
    {
        string? title;
        try
        {
            title = await reader(path, CancellationToken.None);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
        {
            return;
        }

        if (title is not { Length: > 0 }) return;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_path == path && MediaTitle.Classes.Contains(TitleFilled)) SetMediaTitle(title);
        });
    }

    internal static async Task<string?> ReadListTitleAsync(string path, CancellationToken token)
        => await ReadListHeadAsync(path, token) is { } head ? PlaylistFile.PlsTitle(head) : null;

    private static async Task<byte[]?> ReadListHeadAsync(string path, CancellationToken token)
    {
        using var response = await ListTitleClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) return null;
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var buffer = new byte[ListTitleMaxBytes];
        var read = 0;
        while (read < buffer.Length && await stream.ReadAsync(buffer.AsMemory(read), token) is var count and > 0) read += count;
        return buffer[..read];
    }

    internal static string? StreamTitle(string path, IPlaybackEngine? engine)
        => engine is not null && MediaAddress.IsAddress(path) && PlaylistFile.IsPlaylist(MediaAddress.WithoutQuery(path))
            && engine.MediaTitle is { Length: > 0 } title
            ? title
            : null;

    internal static string MediaName(string path) => MediaAddress.IsAddress(path) ? MediaAddress.Name(path) : Path.GetFileName(path);

    private void HideMediaTitle()
    {
        CloseTitleMenu();
        MediaTitle.Classes.Remove(TitleFilled);
        TxtMediaTitle.Text = null;
        ToolTip.SetTip(MediaTitle, null);
        AutomationProperties.SetName(MediaTitle, null);
    }

    internal bool OverMediaTitle(PointerEventArgs e)
        => MediaTitle.IsEffectivelyVisible && new Rect(MediaTitle.Bounds.Size).Contains(e.GetPosition(MediaTitle));

    private bool InMediaTitle(object? source)
        => source is Visual visual && (ReferenceEquals(visual, MediaTitle) || MediaTitle.IsVisualAncestorOf(visual));

    private bool TitlePress(PointerPressedEventArgs e, PlayerButton button)
    {
        if (!InMediaTitle(e.Source)) return false;
        if (button == PlayerButton.Left) return true;
        if (button != PlayerButton.Right) return false;
        OpenTitleMenu();
        return true;
    }

    internal MenuFlyout BuildTitleMenu(string path)
    {
        var flyout = new MenuFlyout();
        flyout.Items.Add(ItemRow("copy-name", "player.title.copy-name", true, () => _ = PathCopier(TopLevel.GetTopLevel(this), MediaName(path))));
        flyout.Items.Add(ItemRow("copy-path", "player.list.item.copy-path", true, () => _ = PathCopier(TopLevel.GetTopLevel(this), IsAddress(path) ? path : Path.GetFullPath(path))));
        flyout.Items.Add(new Separator());
        flyout.Items.Add(RevealRow());
        flyout.Items.Add(ActionRow(Keymap.Info));
        return flyout;
    }

    internal void OpenTitleMenu()
    {
        if (_path is not { } path || !MediaTitle.IsEffectivelyVisible) return;
        CloseMenus();
        var flyout = BuildTitleMenu(path);
        _titleMenu = flyout;
        _itemMenu = flyout;
        flyout.Opened += (_, _) => TitleMenuState(flyout, true);
        flyout.Closed += (_, _) => TitleMenuState(flyout, false);
        try
        {
            flyout.ShowAt(MediaTitle, true);
            _trace.Add("titlemenu " + Path.GetFileName(path));
        }
        catch (InvalidOperationException)
        {
            _titleMenu = null;
            _itemMenu = null;
        }
    }

    private void TitleMenuState(MenuFlyout flyout, bool open)
    {
        if (!ReferenceEquals(_titleMenu, flyout)) return;
        MediaTitle.Classes.Set(TitleMenuClass, open);
        MediaTitleMenuChanged?.Invoke();
    }

    private void CloseTitleMenu()
    {
        var menu = _titleMenu;
        if (menu is null) return;
        if (ReferenceEquals(_itemMenu, menu)) _itemMenu = null;
        menu.Hide();
        _titleMenu = null;
        MediaTitle.Classes.Remove(TitleMenuClass);
        MediaTitleMenuChanged?.Invoke();
    }
}
