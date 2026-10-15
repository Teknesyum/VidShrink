using System.Diagnostics;
using Avalonia.Controls;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Pencere teslim yolu (<see cref="EditorView.ExportToAsync"/>) cizelgenin muzigini kopyaya tasir: muzikli modelde plan
/// Tam'a duser (<see cref="ExportPlan.MusicForcedFull"/>) ve ffmpeg argumanlarinda muzik dosyasi ek girdidir; muziksiz es
/// bayrak tasimaz. Kanit <c>.calisma/pencere-muzik/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciPencereMuzikTeslimTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "pencere-muzik");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    [Fact]
    public void PencereTeslimiMuzigiPlanaTasir()
    {
        string[] dosyalar = { "kaynak.mp4", "muzik.m4a", "muzikli.mp4", "muziksiz.mp4" };
        KanitKapanisi.Onceki(Kanit, dosyalar);
        var kaynak = Path.Combine(Kanit, "kaynak.mp4");
        var muzik = Path.Combine(Kanit, "muzik.m4a");
        Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=3", "-f", "lavfi", "-i", "sine=f=440:d=3",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-threads", "2", "-c:a", "aac", "-shortest", kaynak);
        Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", "sine=f=3000:d=3", "-c:a", "aac", "-threads", "2", muzik);

        var s = AppHost.Run(() =>
        {
            var view = new EditorView { RevealFolder = _ => { } };
            view.Player.EngineFactory = () => new YolMotoru(sure: 3);
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                var ac = view.OpenSourceAsync(kaynak);
                DenetimSurucu.Pump(view.Player, () => ac.IsCompleted, 10);
                Assert.True(ac.IsCompleted);

                var muziksiz = view.ExportToAsync(Path.Combine(Kanit, "muziksiz.mp4"));
                DenetimSurucu.Pump(view.Player, () => muziksiz.IsCompleted, 60);
                Assert.True(muziksiz.IsCompleted);
                var once = view.LastPlan;

                Assert.True(view.Model!.SetMusic(new EditMusic(muzik, EditTime.FromSeconds(3), EditTime.FromSeconds(1), -6)));
                var muzikli = view.ExportToAsync(Path.Combine(Kanit, "muzikli.mp4"));
                DenetimSurucu.Pump(view.Player, () => muzikli.IsCompleted, 60);
                Assert.True(muzikli.IsCompleted);
                return (Once: once, Sonra: view.LastPlan, Ok: muzikli.Result);
            }
            finally
            {
                view.Player.Close();
                window.Close();
            }
        });

        Assert.True(s.Ok);
        Assert.False(s.Once!.MusicForcedFull);
        Assert.DoesNotContain(s.Once.Steps.SelectMany(st => st.Args), a => a == Path.Combine(Kanit, "muzik.m4a"));
        Assert.True(s.Sonra!.MusicForcedFull);
        Assert.Contains(s.Sonra.Steps.SelectMany(st => st.Args), a => a == Path.Combine(Kanit, "muzik.m4a"));
        KanitKapanisi.Kapat(Kanit, dosyalar);
    }

    private static void Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(30000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 30 sn icinde bitmedi.");
        }

        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
    }
}
