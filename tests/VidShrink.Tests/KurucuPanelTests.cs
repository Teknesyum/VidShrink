using VidShrink.Core;
using VidShrink.Core.Setup;
using Xunit;

namespace VidShrink.Tests;

public sealed class KurucuPanelTests
{
    private static readonly TimeSpan Kare = TimeSpan.FromMilliseconds(InstallProgress.FrameMilliseconds);

    [Fact]
    public void IlkKareHizliAkarVeCubukGeriGitmez()
    {
        var p = new SetupPanelProgress();
        p.Step(4, 10, "hazirlaniyor");
        var ilk = p.Advance(Kare).Bar;
        Assert.Equal(4 * InstallProgress.BurstApproach, ilk, 6);

        var onceki = ilk;
        p.Step(2, 5, "geri yazan adim");
        for (var i = 0; i < 400; i++)
        {
            var bar = p.Advance(Kare).Bar;
            Assert.True(bar >= onceki, $"{onceki} -> {bar}");
            onceki = bar;
        }

        Assert.Equal(4, p.Percent);
        Assert.True(onceki > 4 && onceki <= 10, onceki.ToString());
    }

    [Fact]
    public void YuzdeDuruncaTavanaSurunurAmaGecmez()
    {
        var p = new SetupPanelProgress();
        p.Step(18, 52, "indiriliyor");
        for (var i = 0; i < 2000; i++) p.Advance(Kare);
        var bar = p.Read().Bar;
        Assert.True(bar > 18 && bar <= 52, bar.ToString());

        for (var i = 0; i < 20000; i++) p.Advance(Kare);
        Assert.True(p.Read().Bar <= 52);
    }

    [Fact]
    public void AdimYuzdesiMonotonVeKirpilir()
    {
        var p = new SetupPanelProgress();
        p.Step(50, 60, "a");
        p.Step(30, 40, "b");
        Assert.Equal(50, p.Percent);
        Assert.True(p.Ceiling >= p.Percent);
        Assert.Equal("b", p.Read().Step);

        p.Step(250, 900, "c");
        Assert.Equal(100, p.Percent);
        Assert.Equal(100, p.Ceiling);

        var q = new SetupPanelProgress();
        q.Step(-20, -5, "d");
        Assert.Equal(0, q.Percent);
    }

    [Fact]
    public void BasaridaCubukHemenYuzeOturur()
    {
        var p = new SetupPanelProgress();
        p.Step(56, 74, "araclar");
        p.Advance(Kare);
        p.Complete("bitti");

        var son = p.Read();
        Assert.Equal(InstallState.Done, son.State);
        Assert.Equal(100, son.Bar);
        Assert.Equal("bitti", son.Step);

        p.Step(10, 20, "gec gelen adim");
        Assert.Equal("bitti", p.Read().Step);
    }

    [Fact]
    public void HatadaCubukKaliyorVeSatirGunlugeDusuyor()
    {
        var disk = new List<string>();
        var p = new SetupPanelProgress(disk.Add, () => new DateTime(2026, 9, 25, 7, 5, 9));
        p.Step(52, 56, "dogrulaniyor");
        for (var i = 0; i < 200; i++) p.Advance(Kare);
        var once = p.Read().Bar;

        p.Fail("yarida kaldi");

        var son = p.Read();
        Assert.Equal(InstallState.Failed, son.State);
        Assert.Equal(once, son.Bar);
        Assert.Equal("yarida kaldi", son.Step);
        Assert.Equal("07:05:09  yarida kaldi", son.Lines[^1]);
        Assert.Equal(son.Lines[^1], disk[^1]);
    }

    [Fact]
    public void EkrandaDokuzSatirKalirDiskeHepsiGider()
    {
        var disk = new List<string>();
        var p = new SetupPanelProgress(disk.Add, () => new DateTime(2026, 1, 1, 13, 28, 34));
        for (var i = 1; i <= 14; i++) p.Log("satir " + i);

        var satirlar = p.Read().Lines;
        Assert.Equal(SetupPanelProgress.VisibleLines, satirlar.Count);
        Assert.Equal(9, satirlar.Count);
        Assert.Equal("13:28:34  satir 6", satirlar[0]);
        Assert.Equal("13:28:34  satir 14", satirlar[^1]);
        Assert.Equal(14, disk.Count);

        Assert.Equal(new[] { "b", "c" }, SetupPanelProgress.Tail(new[] { "a", "b", "c" }, 2));
        Assert.Equal(new[] { "a" }, SetupPanelProgress.Tail(new[] { "a" }, 9));
    }

    [Fact]
    public void SatirTekSatiraIner()
    {
        var at = new DateTime(2026, 9, 25, 23, 59, 1);
        Assert.Equal("23:59:01  bir iki uc", SetupPanelProgress.FormatLine(at, "bir\r\niki\nuc"));
    }

    [Fact]
    public void YazilamayanGunlukIsiBozmaz()
    {
        var p = new SetupPanelProgress(_ => throw new IOException("disk dolu"));
        p.Log("yine de ekranda");
        Assert.EndsWith("yine de ekranda", p.Read().Lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void GunlukBellekteUcYuzSatirdaDurur()
    {
        var disk = new List<string>();
        var p = new SetupPanelProgress(disk.Add);
        for (var i = 1; i <= SetupPanelProgress.KeptLines + 50; i++) p.Log("satir " + i);

        Assert.Equal(300, p.KeptCount);
        Assert.Equal(350, disk.Count);
        Assert.EndsWith("satir 350", p.Read().Lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void DugmeSirasiKurKuruluyorKapatProgramiAc()
    {
        var hazir = SetupPanelChoices.For(SetupPanelPhase.Ready, false, false);
        Assert.Equal(new[] { SetupPanelButton.Install }, hazir.Buttons);
        Assert.Equal(SetupPanelButton.Install, hazir.Focus);

        var suruyor = SetupPanelChoices.For(SetupPanelPhase.Running, false, true);
        Assert.Equal(new[] { SetupPanelButton.Installing }, suruyor.Buttons);
        Assert.False(SetupPanelChoices.Enabled(SetupPanelButton.Installing));
        Assert.Null(suruyor.Focus);
        Assert.True(SetupPanelChoices.Enabled(SetupPanelButton.Install));

        var basari = SetupPanelChoices.For(SetupPanelPhase.Done, false, true);
        Assert.Equal(new[] { SetupPanelButton.Close, SetupPanelButton.OpenApp }, basari.Buttons);
        Assert.Equal(SetupPanelButton.OpenApp, basari.Primary);
        Assert.Equal(SetupPanelButton.OpenApp, basari.Focus);

        Assert.Equal(new[] { SetupPanelButton.Close }, SetupPanelChoices.For(SetupPanelPhase.Done, true, true).Buttons);
        Assert.Equal(new[] { SetupPanelButton.Close }, SetupPanelChoices.For(SetupPanelPhase.Done, false, false).Buttons);

        var hata = SetupPanelChoices.For(SetupPanelPhase.Failed, false, true);
        Assert.Equal(new[] { SetupPanelButton.OpenLog, SetupPanelButton.Retry }, hata.Buttons);
        Assert.Equal(SetupPanelButton.Retry, hata.Primary);
        Assert.Equal(SetupPanelButton.Retry, hata.Focus);

        Assert.False(SetupPanelChoices.CanClose(SetupPanelPhase.Running));
        Assert.True(SetupPanelChoices.CanClose(SetupPanelPhase.Ready));
        Assert.True(SetupPanelChoices.CanClose(SetupPanelPhase.Done));
        Assert.True(SetupPanelChoices.CanClose(SetupPanelPhase.Failed));
        Assert.True(SetupPanelChoices.CanChangeLocation(SetupPanelPhase.Ready));
        Assert.True(SetupPanelChoices.CanChangeLocation(SetupPanelPhase.Failed));
        Assert.False(SetupPanelChoices.CanChangeLocation(SetupPanelPhase.Running));
        Assert.False(SetupPanelChoices.CanChangeLocation(SetupPanelPhase.Done));

        Assert.Equal(SetupPanelPhase.Running, SetupPanelStages.PhaseOf(InstallState.Running));
        Assert.Equal(SetupPanelPhase.Done, SetupPanelStages.PhaseOf(InstallState.Done));
        Assert.Equal(SetupPanelPhase.Failed, SetupPanelStages.PhaseOf(InstallState.Failed));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(61.9, 0)]
    [InlineData(62, 1)]
    [InlineData(69, 1)]
    [InlineData(70, 2)]
    [InlineData(86, 3)]
    [InlineData(92, 3)]
    [InlineData(93, 4)]
    [InlineData(100, 4)]
    public void YuzdeAdimaDuser(double yuzde, int adim)
    {
        Assert.Equal(adim, SetupPanelStages.IndexOf(yuzde));
    }

    [Fact]
    public void AdimDurumlariEvreyiIzler()
    {
        const SetupStageState B = SetupStageState.Pending, S = SetupStageState.Running, T = SetupStageState.Done,
            H = SetupStageState.Failed, A = SetupStageState.Skipped;

        Assert.Equal(new[] { B, B, B, B, B }, SetupPanelStages.States(SetupPanelPhase.Ready, 0, false));
        Assert.Equal(new[] { S, B, B, B, B }, SetupPanelStages.States(SetupPanelPhase.Running, 18, false));
        Assert.Equal(new[] { T, T, S, B, B }, SetupPanelStages.States(SetupPanelPhase.Running, 70, false));
        Assert.Equal(new[] { T, H, B, B, B }, SetupPanelStages.States(SetupPanelPhase.Failed, 62, false));
        Assert.Equal(new[] { T, T, T, T, T }, SetupPanelStages.States(SetupPanelPhase.Done, 100, false));
        Assert.Equal(new[] { T, T, T, A, A }, SetupPanelStages.States(SetupPanelPhase.Done, 100, true));
        Assert.Equal(new[] { B, B, B, A, A }, SetupPanelStages.States(SetupPanelPhase.Ready, 0, true));

        var adimlar = SetupPanelStages.All;
        Assert.Equal(5, adimlar.Count);
        Assert.Equal(SetupPanelStages.Starts, adimlar.Select(a => a.Start));
        Assert.All(adimlar, a => Assert.False(string.IsNullOrWhiteSpace(a.Name + a.Detail)));
        Assert.Equal(adimlar.Count, adimlar.Select(a => a.Name).Distinct().Count());
    }

    [Fact]
    public void KurulumYeriProgramsAltindaKalir()
    {
        var yerel = Path.Combine(TestPaths.OutputRoot, "kurucu-panel", "yer");
        var programs = SetupPanelLocation.ProgramsOf(yerel);
        Assert.Equal(Path.Combine(yerel, "Programs"), programs);

        Assert.Equal(Path.Combine(programs, "VidShrink"), SetupPanelLocation.Resolve(programs, yerel));
        Assert.Equal(Path.Combine(programs, "Araclar", "VidShrink"), SetupPanelLocation.Resolve(Path.Combine(programs, "Araclar"), yerel));
        Assert.Equal(Path.Combine(programs, "vidshrink"), SetupPanelLocation.Resolve(Path.Combine(programs, "vidshrink") + Path.DirectorySeparatorChar, yerel));

        Assert.Null(SetupPanelLocation.Resolve(yerel, yerel));
        Assert.Null(SetupPanelLocation.Resolve(Path.Combine(yerel, "Baska"), yerel));
        Assert.Null(SetupPanelLocation.Resolve(Path.Combine(yerel, "ProgramsX"), yerel));
        Assert.Null(SetupPanelLocation.Resolve("", yerel));
        Assert.Null(SetupPanelLocation.Resolve(null, yerel));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("--no-launch", false)]
    [InlineData("--tag v1.2.3", false)]
    [InlineData("--console", false)]
    [InlineData("--panel --tag x", true)]
    [InlineData("--PANEL", true)]
    [InlineData("--panel --uninstall", false)]
    [InlineData("--panel --help", false)]
    [InlineData("--panel --console", false)]
    [InlineData("--uninstall", false)]
    public void ArgumanPaneliYaDaKonsoluSeciyor(string satir, bool panel)
    {
        var args = satir.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(panel, SetupLaunch.UsePanel(args));
    }

    [Fact]
    public void ProvaGeciciKokeKurarKabugaDokunmaz()
    {
        var scratch = Path.Combine(TestPaths.OutputRoot, "kurucu-panel", "prova");
        var options = new SetupOptions
        {
            InstallRoot = @"C:\gercek\Programs\VidShrink",
            LocalAppData = @"C:\gercek",
            WorkRoot = scratch,
            MenuLanguage = "tr",
            Tag = "v1.2.3",
            ShortcutDirectory = @"C:\gercek\kisayol"
        };
        Assert.True(options.DefaultRegistry);

        var prova = SetupLaunch.Rehearsal(options, scratch);

        Assert.Equal(scratch, prova.LocalAppData);
        Assert.Equal(Path.Combine(scratch, "Programs", "VidShrink"), prova.InstallRoot);
        Assert.True(SetupRunner.UnderPrograms(prova.InstallRoot, prova.LocalAppData));
        Assert.False(prova.DefaultRegistry);
        Assert.True(prova.SkipShortcuts);
        Assert.True(prova.NoLaunch);
        Assert.Null(prova.ShortcutDirectory);
        Assert.Equal("v1.2.3", prova.Tag);

        Assert.True(SetupLaunch.ForPanel(options).NoLaunch);
        Assert.False(SetupLaunch.RehearsalRequested(null));
        Assert.False(SetupLaunch.RehearsalRequested(" "));
        Assert.True(SetupLaunch.RehearsalRequested("1"));
        Assert.Equal(Path.Combine(scratch, "VidShrink", "kurulum.log"), SetupLaunch.LogPath(scratch));
    }
}
