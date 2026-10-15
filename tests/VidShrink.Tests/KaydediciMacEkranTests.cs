using System;
using System.Collections.Generic;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// later 155: macOS'ta bölge kaydı seçimin bulunduğu ekrandan kesilir.
///
/// <para><c>avfoundation</c> ekranı adla açar (<c>Capture screen N</c>) ve bölge o ekranın
/// karesinden <c>crop</c> ile alınır. Bölge masaüstü koordinatında geldiği için ekran sırası
/// bölgenin en çok örtüştüğü ekrandan seçilir, kırpma o ekranın kökenine göre yerelleşir.
/// Maskeler masaüstü koordinatında kaldığı için <see cref="RecorderRequest.Region"/> değişmez.</para>
///
/// <para>Yalnız argüman ölçüsü: iki ekranlı düzen sahte. Gerçek çok ekranlı Mac ölçülmedi.</para>
/// </summary>
public sealed class KaydediciMacEkranTests
{
    private static readonly ScreenBounds[] IkiEkran =
    {
        new(0, 0, 0, 1920, 1080),
        new(1, 1920, 0, 2560, 1440)
    };

    private static RecorderRequest MacBolge(RecorderRegion bolge, int ekran, IReadOnlyList<ScreenBounds> ekranlar) => new()
    {
        Platform = RecorderPlatform.MacOs,
        Target = RecorderTargetKind.Region,
        Region = bolge,
        ScreenIndex = ekran,
        Screens = ekranlar,
        Container = RecorderContainer.Mov
    };

    [Fact]
    public void BolgeninEkraniEnCokOrtustuguEkrandir()
    {
        Assert.Equal(1, RecorderArguments.ScreenOf(IkiEkran, new RecorderRegion(2000, 100, 640, 480)));
        Assert.Equal(0, RecorderArguments.ScreenOf(IkiEkran, new RecorderRegion(100, 100, 640, 480)));
        Assert.Equal(1, RecorderArguments.ScreenOf(IkiEkran, new RecorderRegion(1800, 0, 400, 300)));
        Assert.Equal(0, RecorderArguments.ScreenOf(IkiEkran, new RecorderRegion(1700, 0, 400, 300)));
        Assert.Equal(0, RecorderArguments.ScreenOf(Array.Empty<ScreenBounds>(), new RecorderRegion(2000, 100, 640, 480)));
        Assert.Equal(0, RecorderArguments.ScreenOf(IkiEkran, new RecorderRegion(9000, 9000, 640, 480)));
    }

    [Fact]
    public void IkinciEkrandakiBolgeOEkranindanYerelKirpmaylaGirer()
    {
        var args = RecorderArguments.Build(MacBolge(new RecorderRegion(2000, 100, 640, 480), 1, IkiEkran), "/tmp/b.mov");

        Assert.Equal("Capture screen 1:none", Deger(args, "-i"));
        Assert.Equal("crop=640:480:80:100", Deger(args, "-vf"));
    }

    [Fact]
    public void EkranListesiYokkenKirpmaMasaustuKoordinatindaKalir()
    {
        var args = RecorderArguments.Build(MacBolge(new RecorderRegion(200, 100, 640, 480), 0, Array.Empty<ScreenBounds>()), "/tmp/b.mov");

        Assert.Equal("Capture screen 0:none", Deger(args, "-i"));
        Assert.Equal("crop=640:480:200:100", Deger(args, "-vf"));
    }

    [Fact]
    public void SoldakiEkrandaNegatifBolgeYerelKirpmaylaKabulEdilir()
    {
        var solda = new[] { new ScreenBounds(0, 0, 0, 1920, 1080), new ScreenBounds(1, -1920, 0, 1920, 1080) };
        var istek = MacBolge(new RecorderRegion(-1800, 40, 640, 480), 1, solda);

        Assert.Empty(RecorderArguments.Validate(istek, "/tmp/b.mov"));
        var args = RecorderArguments.Build(istek, "/tmp/b.mov");
        Assert.Equal("Capture screen 1:none", Deger(args, "-i"));
        Assert.Equal("crop=640:480:120:40", Deger(args, "-vf"));

        var listesiz = MacBolge(new RecorderRegion(-1800, 40, 640, 480), 0, Array.Empty<ScreenBounds>());
        Assert.NotEmpty(RecorderArguments.Validate(listesiz, "/tmp/b.mov"));
    }

    [Fact]
    public void GorunumMacteBolgeninEkraniniIsteyeYazar()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            RecorderView Kur(RecorderPlatform platform)
            {
                var view = new RecorderView(ayarYolu)
                {
                    SkipAutoMeasure = true,
                    CapturePlatform = platform,
                    ScreenSource = () => IkiEkran
                };
                Elle(view);
                view.FindControl<ComboBox>("CmbTarget")!.SelectedIndex = (int)RecorderTargetKind.Region;
                view.FindControl<TextBox>("TxtRegionX")!.Text = "2000";
                view.FindControl<TextBox>("TxtRegionY")!.Text = "100";
                view.FindControl<TextBox>("TxtRegionWidth")!.Text = "640";
                view.FindControl<TextBox>("TxtRegionHeight")!.Text = "480";
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                return view;
            }

            var mac = Kur(RecorderPlatform.MacOs).BuildRequest(applyAuto: false);
            var windows = Kur(RecorderPlatform.Windows).BuildRequest(applyAuto: false);
            return (mac, windows);
        }));

        Assert.Equal(1, olcu.mac!.ScreenIndex);
        Assert.Equal(new RecorderRegion(2000, 100, 640, 480), olcu.mac.Region);
        Assert.Equal(0, olcu.windows!.ScreenIndex);
    }
}
