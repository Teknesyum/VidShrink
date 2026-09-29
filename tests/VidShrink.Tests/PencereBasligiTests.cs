using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VidShrink.App;
using Xunit;

namespace VidShrink.Tests;

public sealed class PencereBasligiTests
{
    /// <summary>
    /// Tam ekranda fare tepeye değince Avalonia'nın açtığı 30 px'lik "VidShrink" başlığı
    /// ikinci bir üst panel gibi görünüyordu. Katman ağaçta durur ama içi boş kalmalı.
    /// </summary>
    [Fact]
    public void TamEkranBaslikKatmaniBos()
    {
        if (!OperatingSystem.IsWindows()) return;

        var sonuc = AppHost.Run(() =>
        {
            var kok = Path.Combine(YolKanit.Folder, "pencere-basligi");
            Directory.CreateDirectory(kok);
            var window = new MainWindow { SettingsPathOverride = Path.Combine(kok, "settings.json"), Width = 1280, Height = 800, WindowState = WindowState.Normal };
            window.Show();
            DenetimSurucu.Wait(window.PlayerTab, 0.3);
            window.WindowState = WindowState.FullScreen;
            DenetimSurucu.Wait(window.PlayerTab, 0.3);
            var katman = TopLevelHostOf(window)?.GetVisualChildren()
                .FirstOrDefault(v => v is Control c && AutomationProperties.GetAutomationId(c) == "PopoverWindowChrome");
            var cocuk = katman?.GetVisualDescendants().Count() ?? -1;
            window.Close();
            return (katman is not null, cocuk);
        });

        Assert.True(sonuc.Item1, "tam ekran başlık katmanı bulunamadı");
        Assert.Equal(0, sonuc.cocuk);
    }

    private static Visual? TopLevelHostOf(Window window)
    {
        Visual? v = window;
        while (v?.GetVisualParent() is { } ust) v = ust;
        return v;
    }
}
