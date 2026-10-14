using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// PLT-P2: tıklama, tıklama sesi, tuş ve büyüteç kutuları Windows kancasına bağlı; macOS ve
/// Linux'ta görünür ama etkisizdi. Kapı <see cref="RecorderView.CapturePlatform"/>: kapalıyken
/// dört kutu ana görünümde ve mini şeritte gizli, kayıtlı ayarda açık gelen seçenek kancayı
/// ve büyüteci başlatmaz. PLT-P3: tıklama kutusu Mac'te geri açılır ve isteğe <c>ShowClicks</c> olarak
/// geçer (ffmpeg <c>-capture_mouse_clicks</c>); Windows'ta tıklamayı bindirme çizdiği için istekte yanlış kalır.
/// </summary>
public sealed class KaydediciPlatformKutuTests
{
    private static readonly string[] Kutular = { "ChkShowClicks", "ChkClickSound", "ChkShowKeys", "ChkMagnifier" };

    private sealed class SahteKanca : IInputHooks
    {
        public List<string> Cagrilar { get; } = new();

        public bool Start(bool clicks, bool moves, bool keys, Action<PixelPoint, PointerButton> clicked, Action<PixelPoint> moved, Action<uint, bool> key)
        {
            Cagrilar.Add($"basla fare={clicks} klavye={keys}");
            return true;
        }

        public void Stop() => Cagrilar.Add("dur");
    }

    private sealed class SahteBindirme : IInputOverlay
    {
        public void Style(PointerStyle style) { }
        public void Ring(PixelPoint point, PointerButton button) { }
        public void Pointer(PixelPoint point, PixelRect area) { }
        public void Keys(string text, PixelRect region) { }
        public void Hide() { }
    }

    private sealed class SahteBuyutec : IMagnifier
    {
        public bool Running { get; private set; }
        public void Start() => Running = true;
        public void Stop() => Running = false;
    }

    private sealed class SahteSes : IClickSound
    {
        public void Play() { }
    }

    private sealed record Olcu(string Ana, string Mini, string Isaretli, string Kanca, bool Buyutec, bool? IstekTiklama);

    private static Olcu Olc(RecorderPlatform platform) => AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
    {
        var once = new RecorderView(ayarYolu) { SkipAutoMeasure = true, CapturePlatform = RecorderPlatform.Windows };
        foreach (var ad in Kutular) Bul<CheckBox>(once, ad).IsChecked = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var kanca = new SahteKanca();
        var buyutec = new SahteBuyutec();
        var view = new RecorderView(ayarYolu)
        {
            SkipAutoMeasure = true,
            CapturePlatform = platform,
            InputHooks = kanca,
            InputOverlay = new SahteBindirme(),
            ClickSound = new SahteSes(),
            Magnifier = buyutec
        };
        var ana = string.Join(" ", Kutular.Select(ad => Bul<CheckBox>(view, ad).IsVisible ? 1 : 0));
        var isaretli = string.Join(" ", Kutular.Select(ad => Bul<CheckBox>(view, ad).IsChecked == true ? 1 : 0));

        view.ShrinkToMini();
        var m = view.Mini!;
        var mini = string.Join(" ", new[] { m.ChkShowClicks, m.ChkClickSound, m.ChkShowKeys, m.ChkMagnifier }.Select(k => k.IsVisible ? 1 : 0));
        view.ExpandFromMini();

        var istekTiklama = view.BuildRequest(applyAuto: false)?.ShowClicks;
        view.SyncInput(true);
        var olcu = new Olcu(ana, mini, isaretli, string.Join(" | ", kanca.Cagrilar), buyutec.Running, istekTiklama);
        view.SyncInput(false);
        return olcu;
    }));

    [Theory]
    [InlineData(RecorderPlatform.Linux)]
    [InlineData(RecorderPlatform.MacOs)]
    public void KapiKapaliykenDortKutuGizliVeKayitliAyarIsteGecmez(RecorderPlatform platform)
    {
        var olcu = Olc(platform);

        Assert.Equal("1 1 1 1", olcu.Isaretli);
        var mac = platform == RecorderPlatform.MacOs;
        Assert.Equal(mac ? "1 0 0 0" : "0 0 0 0", olcu.Ana);
        Assert.Equal(mac ? "1 0 0 0" : "0 0 0 0", olcu.Mini);
        Assert.Equal(string.Empty, olcu.Kanca);
        Assert.False(olcu.Buyutec);
        Assert.Equal(mac, olcu.IstekTiklama);
    }

    [Fact]
    public void WindowstaDortKutuGorunurVeKancaKurulur()
    {
        var olcu = Olc(RecorderPlatform.Windows);

        Assert.Equal("1 1 1 1", olcu.Ana);
        Assert.Equal("1 1 1 1", olcu.Mini);
        Assert.Equal("basla fare=True klavye=True", olcu.Kanca);
        Assert.True(olcu.Buyutec);
        Assert.False(olcu.IstekTiklama);
    }
}
