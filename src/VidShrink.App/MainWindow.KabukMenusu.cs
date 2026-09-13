using System;
using System.Security;
using Avalonia.Controls.Primitives;

namespace VidShrink.App;

/// <summary>
/// Sağ tık menüsünün Ayarlar sekmesindeki kolu. Kullanıcı menüyü açıp kapatmak için
/// kurulum betiğine ve komut satırına dönmüyor; kutu işaretleniyor, girdiler yazılıyor,
/// kutu boşalıyor, girdiler siliniyor. Etiketler arayüz dilini izliyor: dil değiştiğinde
/// menü kuruluysa aynı anda yeniden yazılıyor.
/// </summary>
public partial class MainWindow
{
    private bool _shellMenuBusy;

    private void SetupShellMenu()
    {
        ShellMenuPanel.IsVisible = ShellMenu.Supported;
        if (!ShellMenu.Supported) return;

        _shellMenuBusy = true;
        ChkShellMenu.IsChecked = ShellMenuInstalled();
        _shellMenuBusy = false;
        Watch(ChkShellMenu, ToggleButton.IsCheckedProperty, OnShellMenuToggled);
    }

    private static bool ShellMenuInstalled()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try { return ShellMenu.Installed(); }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException) { return false; }
    }

    private void OnShellMenuToggled()
    {
        if (_shellMenuBusy || !OperatingSystem.IsWindows()) return;

        var wanted = ChkShellMenu.IsChecked == true;
        try
        {
            if (wanted)
            {
                var executable = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executable)) throw new InvalidOperationException(nameof(Environment.ProcessPath));
                var written = ShellMenu.Install(executable, Say("shell.menu.open"), Say("shell.menu.shrink"));
                ShowShellMenuStatus(Say("settings-tab.shell-menu.done", written));
            }
            else
            {
                ShellMenu.Remove();
                ShowShellMenuStatus(Say("settings-tab.shell-menu.removed"));
            }
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowShellMenuStatus(Say("settings-tab.shell-menu.error", e.Message));
            _shellMenuBusy = true;
            ChkShellMenu.IsChecked = !wanted;
            _shellMenuBusy = false;
        }
    }

    /// <summary>Dil değişince kurulu menünün etiketleri yeni dile göre yeniden yazılır.</summary>
    private void RelabelShellMenu()
    {
        if (!OperatingSystem.IsWindows() || !ShellMenu.Supported) return;
        if (ChkShellMenu.IsChecked != true || !ShellMenuInstalled()) return;

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return;

        try { ShellMenu.Install(executable, Say("shell.menu.open"), Say("shell.menu.shrink")); }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException)
        {
            ShowShellMenuStatus(Say("settings-tab.shell-menu.error", e.Message));
        }
    }

    private void ShowShellMenuStatus(string text)
    {
        TxtShellMenuStatus.Text = text;
        TxtShellMenuStatus.IsVisible = text.Length > 0;
    }
}
