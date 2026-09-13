using System;
using System.Security;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace VidShrink.App;

/// <summary>
/// Sağ tık menüsünün Ayarlar sekmesindeki kolu. Kullanıcı menüyü açıp kapatmak için
/// kurulum betiğine ve komut satırına dönmüyor; kutu işaretleniyor, girdiler yazılıyor,
/// kutu boşalıyor, girdiler siliniyor.
///
/// <para>İki girdi iki ayrı kutu: açma girdisini isteyip küçültme alt menüsünü istememek
/// (ya da tersi) kullanıcının hakkı, tek kutu ikisini birden dayatıyordu. Her kutunun
/// durumu kendi kayıt defteri kolundan okunuyor, ayrı bir ayar dosyası tutulmuyor.</para>
///
/// <para>Etiketler arayüz dilini izliyor: dil değiştiğinde kurulu olan girdiler aynı anda
/// yeniden yazılıyor ve seçili dildeki metin kabuk uzantısının okuduğu
/// <see cref="ShellMenu.LabelKey"/> altına da düşüyor.</para>
/// </summary>
public partial class MainWindow
{
    private bool _shellMenuBusy;

    private void SetupShellMenu()
    {
        ShellMenuPanel.IsVisible = ShellMenu.Supported;
        if (!ShellMenu.Supported) return;

        _shellMenuBusy = true;
        ChkShellMenuOpen.IsChecked = ShellMenuInstalled(ShellMenu.MenuKey);
        ChkShellMenuShrink.IsChecked = ShellMenuInstalled(ShellMenu.ShrinkMenuKey);
        _shellMenuBusy = false;
        Watch(ChkShellMenuOpen, ToggleButton.IsCheckedProperty, OnShellMenuOpenToggled);
        Watch(ChkShellMenuShrink, ToggleButton.IsCheckedProperty, OnShellMenuShrinkToggled);
        RelabelShellMenu();
    }

    private static bool ShellMenuInstalled(string menu)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try { return ShellMenu.Installed(menu); }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException) { return false; }
    }

    private void OnShellMenuOpenToggled()
    {
        if (!OperatingSystem.IsWindows()) return;
        ApplyShellMenu(ChkShellMenuOpen, "shell.menu.open", ShellMenu.InstallOpen, ShellMenu.RemoveOpen);
    }

    private void OnShellMenuShrinkToggled()
    {
        if (!OperatingSystem.IsWindows()) return;
        ApplyShellMenu(ChkShellMenuShrink, "shell.menu.shrink", ShellMenu.InstallShrink, ShellMenu.RemoveShrink);
    }

    private void ApplyShellMenu(CheckBox box, string labelKey, Func<string, string, int> install, Func<int> remove)
    {
        if (_shellMenuBusy || !OperatingSystem.IsWindows()) return;

        var wanted = box.IsChecked == true;
        try
        {
            if (wanted)
            {
                var executable = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executable)) throw new InvalidOperationException(nameof(Environment.ProcessPath));
                var written = install(executable, Say(labelKey));
                ShowShellMenuStatus(Say("settings-tab.shell-menu.done", written));
            }
            else
            {
                remove();
                ShowShellMenuStatus(Say("settings-tab.shell-menu.removed"));
            }
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowShellMenuStatus(Say("settings-tab.shell-menu.error", e.Message));
            _shellMenuBusy = true;
            box.IsChecked = !wanted;
            _shellMenuBusy = false;
        }
    }

    /// <summary>Dil değişince kurulu olan girdilerin etiketleri yeni dile göre yeniden yazılır.</summary>
    private void RelabelShellMenu()
    {
        if (!OperatingSystem.IsWindows() || !ShellMenu.Supported) return;

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return;

        try
        {
            if (ChkShellMenuOpen.IsChecked == true && ShellMenuInstalled(ShellMenu.MenuKey))
                ShellMenu.InstallOpen(executable, Say("shell.menu.open"));
            if (ChkShellMenuShrink.IsChecked == true && ShellMenuInstalled(ShellMenu.ShrinkMenuKey))
                ShellMenu.InstallShrink(executable, Say("shell.menu.shrink"));
        }
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
