using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using VidShrink.App.Themes;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

public partial class App : Application
{
    private readonly string? _startupFile;
    private readonly ShellShrinkStartup? _shrink;
    private readonly ShrinkRequestQueue? _queue;

    public App() : this(null)
    {
    }

    public App(string? startupFile) => _startupFile = startupFile;

    internal App(ShellShrinkStartup startup, ShrinkRequestQueue? queue)
    {
        _shrink = startup;
        _queue = queue;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        try { TempCleanup.CleanupStaleArtifacts(Path.GetTempPath()); } catch { }

        // Palet pencereden once yurutuluyor: sonra uygulanirsa program bir kare
        // varsayilan renklerle cizilir ve acilista goz alan bir sicrama olur.
        try { PaletteCatalog.Use(AppSettings.Load().Theme); } catch { }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            RegisterFileTypes();
            MacUpdate.Begin();
            desktop.Exit += (_, _) => MacUpdate.Finish();

            if (_shrink is not null) desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

            var window = StartupWindow();
            window.Icon = LoadAppIcon();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Baslangicta acilan pencere. Ayri durmasinin sebebi olculebilir olmasi: kabuk istegi
    /// varken burasi <see cref="MainWindow"/> dondururse ana pencere geri gelir ve
    /// <c>KabukIstegiTests.KabukIstegiAnaPencereyiAcmaz</c> kirmizi olur.
    /// </summary>
    internal Window StartupWindow()
        => _shrink is not null
            ? new ShrinkJobWindow(_shrink, _queue)
            : new MainWindow(_startupFile);

    /// <summary>
    /// "Birlikte ac" kaydini uretimde tetikleyen tek nokta. Uygulama nasil acilirsa acilsin
    /// — ana pencere ya da kabuk istegi — burasi kosar. Cagri masaustu omru kolunun icinde
    /// durur: omur kurulmadan calisan bir konak (olcumdeki bassiz kurulum gibi) kayit
    /// defterine hic dokunmaz. Kayit yalnizca <c>HKEY_CURRENT_USER</c> altina yazilir,
    /// yonetici hakki istemez ve <see cref="Integration.FileAssociationSetup"/> ayni yol
    /// icin bir kez yazdigi icin her acilista kayit defterine dokunulmaz.
    /// </summary>
    private static void RegisterFileTypes()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var executable = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(executable)) Integration.FileAssociationSetup.Ensure(executable);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    private static WindowIcon? LoadAppIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://VidShrink.App/Assets/VidShrink.png"));
            return new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }
}
