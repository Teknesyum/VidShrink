using System.ComponentModel;
using Avalonia.Controls;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Linux turu: <c>xdg-open</c> kurulu olmayan Linux'ta <c>Platform.Reveal</c>'in
/// <c>Process.Start</c>'ı <see cref="Win32Exception"/> atıyor. Kaydedicinin ve düzenleyicinin
/// "Klasörde göster" düğmesi bunu yakalamıyordu (olay işleyicisinde çökme), kaydedicinin
/// "bitince klasörü aç" kolu teslimi yarıda kesip bitince eylemini koşturmuyordu. Artık hata
/// satıra yazılır; açılan klasör hata yazdırmaz (olumsuz kontrol).
/// </summary>
public sealed class KlasorAcmaHatasiTests
{
    private static Exception Yok() => new Win32Exception(2, "xdg-open bulunamadı");

    [Fact]
    public void KaydediciKlasorAcamayincaCokmezHataSatirinaYazar()
    {
        var (hata, acilan, temiz) = AppHost.Run(() =>
        {
            var acilanlar = new List<string>();
            var view = new RecorderView { SkipAutoMeasure = true, RevealFolder = _ => throw Yok() };
            var window = new Window { Width = 1100, Height = 900, Content = view };
            window.Show();
            try
            {
                view.RevealSafely("/tmp/kayit.mp4");
                var h = view.ErrorText;
                var olumlu = new RecorderView { SkipAutoMeasure = true, RevealFolder = acilanlar.Add };
                olumlu.RevealSafely("/tmp/kayit.mp4");
                return (h, acilanlar.ToArray(), olumlu.ErrorText);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Contains(Strings.Get("main.error.folder"), hata);
        Assert.Contains("xdg-open", hata);
        Assert.Equal(new[] { "/tmp/kayit.mp4" }, acilan);
        Assert.Equal(string.Empty, temiz);
    }

    [Fact]
    public void DuzenleyiciKlasorAcamayincaCokmezDurumaYazar()
    {
        var (durum, acilan, temiz) = AppHost.Run(() =>
        {
            var acilanlar = new List<string>();
            var view = new EditorView { RevealFolder = _ => throw Yok() };
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                view.RevealSafely("/tmp/duzenlenmis.mp4");
                var d = view.ExportStatusText;
                var olumlu = new EditorView { RevealFolder = acilanlar.Add };
                olumlu.RevealSafely("/tmp/duzenlenmis.mp4");
                return (d, acilanlar.ToArray(), olumlu.ExportStatusText);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Contains(Strings.Get("main.error.folder"), durum);
        Assert.Equal(new[] { "/tmp/duzenlenmis.mp4" }, acilan);
        Assert.Equal(string.Empty, temiz);
    }
}
