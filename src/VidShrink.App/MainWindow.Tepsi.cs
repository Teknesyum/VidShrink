using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App;

internal interface IMainWindowTray
{
    void Show(string tip);

    void Remove();
}

/// <summary>
/// AYR-22: simge durumuna küçültmenin tepsiye gidip gitmediği ve tepsi simgesinin ipucu. Kapatma
/// düğmesi tepsiye gitmez; seçenek yalnız küçültmeyi değiştirir.
/// </summary>
internal static class TepsiKarari
{
    internal static bool Gizle(bool secenek, WindowState durum) => secenek && durum == WindowState.Minimized;

    /// <summary>
    /// Ana penceredeki işin ve açık kuyrukların ilerlemesi, sırasıyla. İş yoksa yalnız uygulama adı.
    /// </summary>
    internal static string Ipucu(double? isKesri, IReadOnlyList<double> kuyruklar, Func<string, object?[], string> say, CultureInfo kultur)
    {
        var parcalar = new List<string>();
        if (isKesri is { } kesir) parcalar.Add(say("main.tray.tip.job", new object?[] { Bicim.Yuzde.Isaretli(Math.Clamp(kesir, 0, 1), kultur) }));
        foreach (var kuyruk in kuyruklar)
            parcalar.Add(say("main.tray.tip.queue", new object?[] { Bicim.Yuzde.Isaretli(Math.Clamp(kuyruk, 0, 1), kultur) }));
        return parcalar.Count == 0 ? Core.AppIdentity.Name : Core.AppIdentity.Name + " · " + string.Join(" · ", parcalar);
    }
}

internal sealed class MainWindowTrayHost : IMainWindowTray
{
    private readonly TrayIconHost _host;
    private readonly Func<WindowIcon?> _icon;
    private readonly Func<NativeMenu> _menu;

    internal MainWindowTrayHost(Action clicked, Func<WindowIcon?> icon, Func<NativeMenu> menu)
    {
        _host = new TrayIconHost(clicked);
        _icon = icon;
        _menu = menu;
    }

    public void Show(string tip) => _host.Show(_host.Visible ? null : _icon(), tip, _host.Visible ? null : _menu());

    public void Remove() => _host.Remove();
}

public partial class MainWindow
{
    private IMainWindowTray? _tepsi;
    private DispatcherTimer? _tepsiSaati;
    private double? _isKesri;
    private WindowState _tepsiOncesi = WindowState.Maximized;

    internal IMainWindowTray Tepsi
    {
        get => _tepsi ??= new MainWindowTrayHost(TepsidenGeriGetir, () => Icon ?? App.LoadAppIcon(), TepsiMenusu);
        set => _tepsi = value;
    }

    internal bool TepsideGizli { get; private set; }

    internal bool TepsiyeKucult => ChkMinimizeToTray.IsChecked == true;

    internal string TepsiIpucu { get; private set; } = "";

    private void TepsiyiIzle()
    {
        PropertyChanged += (_, change) =>
        {
            if (change.Property != WindowStateProperty || TepsideGizli) return;
            if (WindowState != WindowState.Minimized) _tepsiOncesi = WindowState;
            if (TepsiKarari.Gizle(TepsiyeKucult, WindowState)) TepsiyeGonder();
        };
    }

    private void TepsiyeGonder()
    {
        TepsideGizli = true;
        TepsiyiTazele();
        Hide();
        _tepsiSaati ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => TepsiyiTazele());
        _tepsiSaati.Start();
    }

    internal void TepsidenGeriGetir()
    {
        if (!TepsideGizli) return;
        TepsideGizli = false;
        _tepsiSaati?.Stop();
        Tepsi.Remove();
        Show();
        if (WindowState == WindowState.Minimized) WindowState = _tepsiOncesi;
        Activate();
    }

    private void TepsiyiTazele()
    {
        if (!TepsideGizli) return;
        TepsiIpucu = TepsiKarari.Ipucu(_isKesri, KuyrukKesirleri(), Say, Strings.Culture);
        Tepsi.Show(TepsiIpucu);
    }

    internal void TepsiyiTazeleForTest() => TepsiyiTazele();

    private static IReadOnlyList<double> KuyrukKesirleri()
        => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app
            ? app.Windows.OfType<ShrinkJobWindow>().Select(w => w.TepsiKesri).OfType<double>().ToList()
            : Array.Empty<double>();

    private NativeMenu TepsiMenusu()
    {
        var goster = new NativeMenuItem(Say("main.tray.show"));
        goster.Click += (_, _) => TepsidenGeriGetir();
        var cik = new NativeMenuItem(Say("main.tray.quit"));
        cik.Click += (_, _) =>
        {
            TepsidenGeriGetir();
            Close();
        };
        var menu = new NativeMenu();
        menu.Add(goster);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(cik);
        return menu;
    }

    private void TepsiyiKaldir()
    {
        _tepsiSaati?.Stop();
        if (!TepsideGizli) return;
        TepsideGizli = false;
        _tepsi?.Remove();
    }
}
