using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
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

    internal App(string? startupFile, Integration.ForwardedFiles? files)
    {
        _startupFile = startupFile;
        _files = files;
    }

    private readonly Integration.ForwardedFiles? _files;

    private readonly bool _selfTest;

    /// <summary>Kaydedici oz sinamasi (<c>--recorder-selftest</c>): yalniz renk karti acilir.</summary>
    internal App(bool selfTest) => _selfTest = selfTest;

    internal App(ShellShrinkStartup startup, ShrinkRequestQueue? queue)
    {
        _shrink = startup;
        _queue = queue;
    }

    /// <summary>
    /// <c>App.axaml</c>'ı açar: kaynak sözlükleri, FluentTheme ve <c>Controls.axaml</c>.
    /// İşaret <c>cerceve</c>'den önce düşüyor, yani Avalonia'nın kendi kuruluşuyla
    /// biçimlemenin payı ayrı okunabiliyor; taban ölçümünde ikisi tek aralıktaydı.
    /// </summary>
    public override void Initialize()
    {
        AcilisIzi.Yaz("app-init");
        AvaloniaXamlLoader.Load(this);
        Localization.YonYalitimi.Kur();
        AcilisIzi.Yaz("app-xaml");
    }

    /// <summary>
    /// Acilis ayari. Elle ffmpeg yolu pencereden once yururluge girer: kuyruk penceresi ve
    /// ana pencerenin ilk yoklamasi ayni ffmpeg'i gorur.
    /// </summary>
    internal static AppSettings LoadStartupSettings()
    {
        var ayar = AppSettings.Load();
        ayar.ApplyFfmpegPath();
        return ayar;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AcilisIzi.Yaz("cerceve");

        // Temizlik gecici klasoru tarar; acilista arayuz is parcaciginda durdugu icin ilk
        // karenin onune olculebilir bir bekleme koyuyordu. Isin kendisi ilk kareyle ilgisiz:
        // silinen artiklar olu kosumlarin kalintisi, yeni kosum kendi kiralamasini zaten
        // yaziyor. Arka plana alindi; sonucu bekleyen kimse yok.
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            var izlenim = "gecici-temizlik";
            try { TempCleanup.CleanupStaleArtifacts(Path.GetTempPath()); }
            catch (Exception ex) { izlenim = "gecici-temizlik-hata=" + ex.GetType().Name; }
            AcilisIzi.Yaz(izlenim);
        });

        // Palet pencereden once yurutuluyor: sonra uygulanirsa program bir kare
        // varsayilan renklerle cizilir ve acilista goz alan bir sicrama olur.
        try
        {
            var ayar = LoadStartupSettings();
            AcilisIzi.Yaz("ayar-okundu");
            MotionPreference.UserReduced = ayar.ReduceMotion;
            PaletteCatalog.Use(PaletteCatalog.Pick(ayar.Theme, ayar.FollowSystemTheme,
                ayar.FollowSystemTheme ? new PlatformThemeSource(PlatformSettings).IsLight : null,
                ayar.LightTheme, ayar.DarkTheme));
        }
        catch { }
        AcilisIzi.Yaz("palet");

        if (_selfTest && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime card)
        {
            card.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            card.MainWindow = Recorder.RecorderSelfTest.CardWindow(card);
            base.OnFrameworkInitializationCompleted();
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!NewWindow.Active)
            {
                RegisterFileTypes();
                MacUpdate.Begin();
                desktop.Exit += (_, _) => MacUpdate.Finish();
            }

            if (_shrink is not null) desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

            var window = StartupWindow();
            window.Icon = LoadAppIcon();
            desktop.MainWindow = window;
            AcilisIzi.Yaz("pencere-kuruldu");

            if (window is MainWindow main)
            {
                if (!NewWindow.Active)
                {
                    try { main.PencereKonumunuGeriYukle(); }
                    catch (Exception) { }
                    desktop.Exit += (_, _) =>
                    {
                        try { main.KapanistaGuncelle(); }
                        catch (Exception) { }
                    };
                }
                var files = _files ?? new Integration.ForwardedFiles();
                main.AcceptForwardedFiles(files);
                main.AcceptControl(Integration.ControlBridge.Shared);
                main.Opened += (_, _) => RestoreQueue();
                main.Opened += (_, _) => StartMpris();
                desktop.Exit += (_, _) => _mpris?.Dispose();
                if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
                    activatable.Activated += (_, activation) => OpenActivatedFiles(files, activation);
            }
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
        if (!OperatingSystem.IsWindows() || PortableMode.Active) return;

        try
        {
            var executable = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(executable))
                Integration.FileAssociationSetup.Ensure(Integration.FileAssociation.LaunchTarget(executable));
            if (Integration.RegistryWriteGate.Allows(executable))
                VidShrink.Core.Setup.ShellRegistration.TrimDefaultAppList(Integration.FileAssociation.ClassesRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    internal static bool OpenActivatedFiles(Integration.ForwardedFiles files, ActivatedEventArgs activation)
    {
        if (activation is not FileActivatedEventArgs opened) return false;

        var paths = new List<string>();
        foreach (var item in opened.Files)
        {
            var local = item.TryGetLocalPath();
            if (!string.IsNullOrEmpty(local)) paths.Add(local);
        }

        return paths.Count > 0 && files.Receive(paths);
    }

    /// <summary>SIK-P9: ölmüş süreçten kalan kuyruk açılışta bir kez, sorusuz ve duraklatılmış açılır.</summary>
    private void RestoreQueue()
    {
        if (_kuyrukGeriGeldi || VidShrink.Core.QueueJournal.Shared is not { } journal) return;
        _kuyrukGeriGeldi = true;
        if (ShrinkJobWindow.Restore(journal, VidShrink.Core.QueueJournal.IsAlive) is not { } restored) return;
        restored.Icon = LoadAppIcon();
        restored.Show();
    }

    private bool _kuyrukGeriGeldi;

    private Integration.MprisServer? _mpris;

    /// <summary>OYB-137: Linux masaüstünün medya denetimi. Veriyolu yoksa ya da ad alınamazsa sessizce açılmaz.</summary>
    private void StartMpris()
    {
        if (_mpris is not null || !Mpris.Enabled(OperatingSystem.IsLinux(), Environment.GetEnvironmentVariable(Mpris.SwitchVariable))) return;
        _mpris = new Integration.MprisServer(Integration.ControlBridge.Shared.ExecuteAsync, Integration.MprisServer.RaiseMainWindow);
        _ = _mpris.StartAsync(Tmds.DBus.Protocol.DBusAddress.Session, NewWindow.Active);
    }

    internal static WindowIcon? LoadAppIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(OperatingSystem.IsWindows()
                ? "avares://VidShrinkPlayer.App/Assets/VidShrink.ico"
                : "avares://VidShrinkPlayer.App/Assets/VidShrink.png"));
            return new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }
}
