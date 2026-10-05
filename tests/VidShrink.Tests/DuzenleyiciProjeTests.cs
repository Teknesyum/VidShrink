using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Duzenleyici proje kaydi (<c>Core/Editing/EditProject</c>, <c>EditorView.Proje</c>): surumlu
/// JSON gidis-donusu, bozuk ve gelecek surumlu dosyanin firlatmadan dusmesi, kaynagi degisen
/// kaydin yuklenmemesi (degismeyen olumsuz kontrol), atomik yazim, tavan budamasi. Arayuz kolu
/// sahte <c>KlipMotoru</c> ile: duzenlemeden sonra otomatik kayit gecikmeyle diske iner, yeni
/// gorunumde cizelge geri gelir ve Geri Al kaynagin dokunulmamis haline doner. Otomatik kayit
/// test surecinde kapali (<c>EditProjectStore.Disabled</c>); her olcu kendi klasorunu verir.
/// Kanit <c>.calisma/worktree-agent-af63d73b1ba5e5a33/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciProjeTests
{
    private const string Dal = "worktree-agent-af63d73b1ba5e5a33";

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string Kanit => Path.Combine(GirdiKanit.Root, ".calisma", Dal);

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(Kanit, "proje-" + ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Kapat(string klasor)
    {
        KanitKapanisi.Kapat(Kanit, Path.GetFileName(klasor));
    }

    private static string Kaynak(string klasor, string ad = "kaynak.mp4", int bayt = 16)
    {
        var dosya = Path.Combine(klasor, ad);
        File.WriteAllBytes(dosya, new byte[bayt]);
        return dosya;
    }

    private static EditTimeline Dolu()
    {
        var model = EditTimeline.FromSource(S(600));
        Assert.True(model.Split(S(100)));
        Assert.True(model.Split(S(300)));
        Assert.True(model.DeleteRange(S(120), S(180)));
        Assert.True(model.SetSpeed(0, -1.5m));
        Assert.True(model.SetEffects(new[] { 1 }, e => e with { CropLeft = 0.1, Rotation = 90, FlipH = true, VolumeDb = -6, FadeIn = S(0.5) }));
        Assert.True(model.Move(0, 2));
        model.AddText(new TextLayer("Merhaba \"dünya\" {x}\\N", S(1), S(4))
        {
            Size = 48,
            Color = 0xFF3366,
            Bold = true,
            FadeIn = S(0.2),
            Keyframes = new[] { new TextKeyframe(0, 0.25, 0.75), new TextKeyframe(S(2), 0.5, 0.5) },
        });
        return model;
    }

    [Fact]
    public void GidisDonusAyniCizelgeyiVerir()
    {
        var model = Dolu();
        var damga = new SourceStamp(@"C:\videolar\çekim (1).mp4", 123456789012, 638000000000000000);
        var json = EditProject.From(damga, model, "Full").ToJson();
        var okunan = EditProject.Parse(json);

        Assert.NotNull(okunan);
        Assert.Equal(damga, okunan!.Source);
        Assert.Equal(S(600), okunan.SourceDuration);
        Assert.Equal("Full", okunan.ExportMode);
        Assert.Equal(model.Clips, okunan.Clips);
        Assert.Equal(model.Texts, okunan.Texts);
        Assert.Contains(okunan.Clips, k => k.Reversed && k.Speed == 1.5m);
        Assert.Contains(okunan.Clips, k => k.Effects.Rotation == 90 && k.Effects.FlipH);
        Assert.Equal(2, okunan.Texts[0].Keyframes.Count);
        Assert.Equal(EditProject.CurrentVersion, JsonDocument.Parse(json).RootElement.GetProperty("version").GetInt32());

        var bos = EditTimeline.FromSource(S(600));
        Assert.NotEqual(model.Clips, bos.Clips);
        Assert.True(bos.Restore(okunan.Clips, okunan.Texts));
        Assert.Equal(model.Clips, bos.Clips);
        Assert.Equal(model.Texts, bos.Texts);
        Assert.False(bos.Restore(okunan.Clips, okunan.Texts));
        Assert.True(bos.Undo());
        Assert.Equal(new[] { new EditClip(0, S(600)) }, bos.Clips);
        Assert.Empty(bos.Texts);
        Assert.False(bos.Undo());
    }

    public static IEnumerable<object[]> Bozuklar()
    {
        var saglam = EditProject.From(new SourceStamp(@"C:\a.mp4", 16, 1), Dolu()).ToJson();
        string Degis(string eski, string yeni)
        {
            Assert.Contains(eski, saglam);
            return saglam.Replace(eski, yeni);
        }

        yield return new object[] { "bos", "" };
        yield return new object[] { "json degil", "bu bir proje degil" };
        yield return new object[] { "yarim", saglam[..(saglam.Length / 2)] };
        yield return new object[] { "dizi", "[1,2,3]" };
        yield return new object[] { "gelecek surum", Degis("\"version\": 1", "\"version\": 2") };
        yield return new object[] { "sifir surum", Degis("\"version\": 1", "\"version\": 0") };
        yield return new object[] { "surum metin", Degis("\"version\": 1", "\"version\": \"1\"") };
        yield return new object[] { "surum yok", Degis("\"version\": 1,", "") };
        yield return new object[] { "kaynak yok", Degis("\"source\"", "\"kaynak\"") };
        yield return new object[] { "klip yok", Degis("\"clips\"", "\"klipler\"") };
        yield return new object[] { "ters aralik", "{\"version\":1,\"source\":{\"path\":\"a\",\"size\":1,\"modifiedUtcTicks\":1},\"sourceDuration\":10,\"clips\":[{\"start\":8,\"end\":2,\"speed\":1,\"reversed\":false}]}" };
        yield return new object[] { "kaynaktan uzun", "{\"version\":1,\"source\":{\"path\":\"a\",\"size\":1,\"modifiedUtcTicks\":1},\"sourceDuration\":10,\"clips\":[{\"start\":0,\"end\":20,\"speed\":1,\"reversed\":false}]}" };
        yield return new object[] { "hiz sinir disi", "{\"version\":1,\"source\":{\"path\":\"a\",\"size\":1,\"modifiedUtcTicks\":1},\"sourceDuration\":10,\"clips\":[{\"start\":0,\"end\":10,\"speed\":900,\"reversed\":false}]}" };
    }

    [Theory]
    [MemberData(nameof(Bozuklar))]
    public void BozukVeGelecekSurumluDosyaFirlatmadanDuser(string ad, string json)
    {
        Assert.True(EditProject.Parse(json) is null, ad);

        var klasor = Klasor("bozuk-" + Math.Abs(ad.GetHashCode(StringComparison.Ordinal)));
        var dosya = Path.Combine(klasor, "p" + EditProject.Extension);
        File.WriteAllText(dosya, json);
        Assert.Null(EditProject.Read(dosya));
        Assert.Null(EditProject.Read(Path.Combine(klasor, "yok" + EditProject.Extension)));
        Kapat(klasor);
    }

    [Fact]
    public void EnKucukGecerliDosyaOkunur()
    {
        var proje = EditProject.Parse("{\"version\":1,\"source\":{\"path\":\"a\",\"size\":1,\"modifiedUtcTicks\":1},\"sourceDuration\":10,\"clips\":[{\"start\":0,\"end\":10,\"speed\":1,\"reversed\":false}],\"bilinmeyen\":true}");

        Assert.NotNull(proje);
        Assert.Single(proje!.Clips);
        Assert.Empty(proje.Texts);
        Assert.Null(proje.ExportMode);
    }

    [Fact]
    public void AtomikYazimYarimDosyaBirakmaz()
    {
        var klasor = Klasor("atomik");
        var hedef = Path.Combine(klasor, "p" + EditProject.Extension);
        var eski = "eski icerik"u8.ToArray();
        var yeni = "yeni ve daha uzun icerik"u8.ToArray();
        File.WriteAllBytes(hedef, eski);

        byte[]? aradaki = null;
        string? gecici = null;
        var dustu = EditProject.WriteAtomic(hedef, yeni, yol =>
        {
            gecici = yol;
            aradaki = File.ReadAllBytes(hedef);
            Assert.Equal(yeni, File.ReadAllBytes(yol));
            throw new IOException("disk doldu");
        });

        Assert.False(dustu);
        Assert.Equal(eski, aradaki);
        Assert.NotNull(gecici);
        Assert.NotEqual(Path.GetFullPath(hedef), Path.GetFullPath(gecici!), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(Path.GetFullPath(klasor), Path.GetDirectoryName(Path.GetFullPath(gecici!)), ignoreCase: true);
        Assert.Equal(eski, File.ReadAllBytes(hedef));
        Assert.Equal(new[] { Path.GetFileName(hedef) }, Directory.GetFiles(klasor).Select(Path.GetFileName));

        Assert.True(EditProject.WriteAtomic(hedef, yeni, yol => aradaki = File.ReadAllBytes(hedef)));
        Assert.Equal(eski, aradaki);
        Assert.Equal(yeni, File.ReadAllBytes(hedef));
        Assert.Equal(new[] { Path.GetFileName(hedef) }, Directory.GetFiles(klasor).Select(Path.GetFileName));
        Kapat(klasor);
    }

    [Fact]
    public void TavanEnEskiKayitlariSiler()
    {
        var klasor = Klasor("tavan");
        var genis = new EditProjectStore(Path.Combine(klasor, "kayit"), cap: 10);
        var depo = new EditProjectStore(genis.Folder, cap: 3);
        var model = EditTimeline.FromSource(S(10));
        var an = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var yollar = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var kaynak = Path.Combine(klasor, $"k{i}.mp4");
            Assert.True(genis.Save(EditProject.From(new SourceStamp(kaynak, 1, 1), model)));
            var yol = genis.PathFor(kaynak);
            File.SetLastWriteTimeUtc(yol, an.AddMinutes(4 - i));
            yollar.Add(yol);
        }

        Assert.Equal(5, yollar.Distinct().Count());
        Assert.Equal(new[] { true, true, true, true, true }, yollar.Select(File.Exists));
        Assert.Equal(2, depo.Prune());
        Assert.Equal(new[] { true, true, true, false, false }, yollar.Select(File.Exists));

        var altinci = Path.Combine(klasor, "k5.mp4");
        Assert.True(depo.Save(EditProject.From(new SourceStamp(altinci, 1, 1), model)));
        Assert.True(File.Exists(depo.PathFor(altinci)));
        Assert.Equal(new[] { true, true, false, false, false }, yollar.Select(File.Exists));

        var yabanci = Path.Combine(depo.Folder, "not.txt");
        File.WriteAllText(yabanci, "x");
        File.SetLastWriteTimeUtc(yabanci, an.AddYears(-1));
        Assert.Equal(0, depo.Prune());
        Assert.True(File.Exists(yabanci));

        Assert.Equal(EditProjectStore.DefaultCap, new EditProjectStore(depo.Folder).Cap);
        Assert.Equal(50, EditProjectStore.DefaultCap);
        Assert.Equal(0, new EditProjectStore(depo.Folder).Prune());
        Assert.Equal(3, Directory.GetFiles(depo.Folder, "*" + EditProject.Extension).Length);
        Kapat(klasor);
    }

    [Fact]
    public void DepoYalnizKendiKaynagininKaydiniVerir()
    {
        var klasor = Klasor("depo");
        var depo = new EditProjectStore(Path.Combine(klasor, "kayit"));
        var a = Kaynak(klasor, "a.mp4");
        var b = Kaynak(klasor, "b.mp4");
        Assert.True(depo.Save(EditProject.From(SourceStamp.Of(a)!, Dolu())));

        Assert.NotNull(depo.Load(a));
        Assert.Null(depo.Load(b));
        Assert.NotEqual(depo.PathFor(a), depo.PathFor(b));
        Assert.EndsWith(EditProject.Extension, depo.PathFor(a), StringComparison.Ordinal);
        Assert.DoesNotContain("a.mp4", Path.GetFileName(depo.PathFor(a)), StringComparison.OrdinalIgnoreCase);

        File.Copy(depo.PathFor(a), depo.PathFor(b));
        Assert.Null(depo.Load(b));
        Kapat(klasor);
    }

    [Fact]
    public void VarsayilanKlasorTestKokundeVeSurecteKapali()
    {
        Assert.True(EditProjectStore.Disabled);
        Assert.Null(EditProjectStore.Default);
        Assert.Equal(Path.Combine(Path.GetFullPath(TestAyarYolu.Klasor), EditProjectStore.FolderName), Path.GetFullPath(EditProjectStore.DefaultFolder), ignoreCase: true);
        var gercek = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VidShrink");
        Assert.False(Path.GetFullPath(EditProjectStore.DefaultFolder).StartsWith(Path.GetFullPath(gercek), StringComparison.OrdinalIgnoreCase));

        EditProjectStore.Disabled = false;
        try
        {
            Assert.Equal(EditProjectStore.DefaultFolder, EditProjectStore.Default!.Folder);
        }
        finally
        {
            EditProjectStore.Disabled = true;
        }
    }

    [Fact]
    public void SifirlamaOtomatikKayitlariSiler()
    {
        var klasor = Klasor("sifirla");
        var depo = new EditProjectStore(Path.Combine(klasor, EditProjectStore.FolderName));
        Assert.True(depo.Save(EditProject.From(new SourceStamp(Path.Combine(klasor, "a.mp4"), 1, 1), EditTimeline.FromSource(S(10)))));
        Assert.True(depo.Save(EditProject.From(new SourceStamp(Path.Combine(klasor, "b.mp4"), 1, 1), EditTimeline.FromSource(S(10)))));

        var silinen = AppDataReset.Run(klasor);
        Assert.Equal(2, silinen.Count);
        Assert.False(Directory.Exists(depo.Folder));

        Assert.True(depo.Save(EditProject.From(new SourceStamp(Path.Combine(klasor, "a.mp4"), 1, 1), EditTimeline.FromSource(S(10)))));
        var yabanci = Path.Combine(depo.Folder, "not.txt");
        File.WriteAllText(yabanci, "x");
        Assert.Single(AppDataReset.Run(klasor));
        Assert.True(File.Exists(yabanci));
        Kapat(klasor);
    }

    private sealed class Sahne : IDisposable
    {
        private readonly Window _pencere;

        internal Sahne(string klasor, EditProjectStore? depo)
        {
            View = new EditorView
            {
                KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 },
                Projects = depo,
                AutosaveDelay = TimeSpan.FromMilliseconds(40),
            };
            View.Player.EngineFactory = () => new KlipMotoru();
            View.OverlayRoot = Path.Combine(klasor, "katman");
            _pencere = new Window { Width = 900, Height = 600, Content = View };
            _pencere.Show();
        }

        internal EditorView View { get; }

        internal void Ac(string dosya)
        {
            var acilis = View.OpenSourceAsync(dosya);
            DenetimSurucu.Pump(View.Player, () => acilis.IsCompleted, 10);
            acilis.GetAwaiter().GetResult();
            Assert.NotNull(View.Model);
        }

        internal bool Bekle(Func<bool> kosul)
        {
            var saat = Stopwatch.StartNew();
            while (!kosul() && saat.Elapsed.TotalSeconds < 30)
            {
                using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
                Dispatcher.UIThread.MainLoop(dilim.Token);
                View.Player.RenderLatest();
            }

            return kosul();
        }

        internal bool Kes(double saniye)
        {
            View.TimelineView.Playhead = S(saniye);
            return View.Split();
        }

        public void Dispose() => _pencere.Close();
    }

    private static (long Start, long End)[] Kesimler(EditTimeline model) => model.Clips.Select(k => (k.SourceStart, k.SourceEnd)).ToArray();

    [Fact]
    public void DuzenlemeDiskeInerVeYeniGorunumdeGeriGelir()
    {
        var klasor = Klasor("otomatik");
        var dosya = Kaynak(klasor);
        var depo = new EditProjectStore(Path.Combine(klasor, "kayit"));
        var kayit = depo.PathFor(dosya);
        AppHost.Run(() =>
        {
            using (var ilk = new Sahne(klasor, depo))
            {
                ilk.Ac(dosya);
                Assert.False(ilk.View.AutosavePending);
                Assert.NotEqual(Strings.Get("editor.project.restored"), ilk.View.ExportStatusText);

                ilk.View.AutosaveDelay = TimeSpan.FromHours(1);
                Assert.True(ilk.Kes(100));
                Assert.True(ilk.View.AutosavePending);
                Assert.False(File.Exists(kayit), "gecikme dolmadan yazildi");
                Assert.Equal(0, ilk.View.Autosaves);

                ilk.View.AutosaveDelay = TimeSpan.FromMilliseconds(40);
                Assert.True(ilk.Kes(300));
                Assert.True(ilk.Bekle(() => ilk.View.Autosaves == 1), "otomatik kayit gelmedi");
                Assert.False(ilk.View.AutosavePending);
                Assert.Equal(new[] { (S(0), S(100)), (S(100), S(300)), (S(300), S(600)) }, EditProject.Read(kayit)!.Clips.Select(k => (k.SourceStart, k.SourceEnd)));
                Assert.False(ilk.View.FlushProject());

                ilk.View.TimelineView.SelectedIndex = 1;
                Assert.True(ilk.View.DeleteSelected());
                Assert.True(ilk.View.AddText());
                ilk.View.SelectedExportMode = ExportMode.Full;
                Assert.True(ilk.Bekle(() => ilk.View.Autosaves >= 2 && !ilk.View.AutosavePending), $"ikinci kayit gelmedi: {ilk.View.Autosaves} {ilk.View.AutosavePending}");
                Assert.Equal(new[] { Path.GetFileName(kayit) }, Directory.GetFiles(depo.Folder).Select(Path.GetFileName));
            }

            using (var ikinci = new Sahne(klasor, depo))
            {
                ikinci.Ac(dosya);
                var model = ikinci.View.Model!;
                Assert.Equal(new[] { (S(0), S(100)), (S(300), S(600)) }, Kesimler(model));
                Assert.Single(model.Texts);
                Assert.Equal(ExportMode.Full, ikinci.View.SelectedExportMode);
                Assert.Equal(Strings.Get("editor.project.restored"), ikinci.View.ExportStatusText);
                Assert.True(ikinci.View.TxtExportStatus.IsEffectivelyVisible);
                Assert.Equal(Tema("StatusSuccess"), ikinci.View.TxtExportStatus.Theme);
                Assert.False(ikinci.View.AutosavePending);

                Assert.True(ikinci.View.Undo());
                Assert.Equal(new[] { (S(0), S(600)) }, Kesimler(model));
                Assert.Empty(model.Texts);
                Assert.False(ikinci.View.Undo());
                Assert.True(ikinci.View.FlushProject());
            }

            using (var ucuncu = new Sahne(klasor, depo))
            {
                ucuncu.Ac(dosya);
                Assert.Equal(new[] { (S(0), S(600)) }, Kesimler(ucuncu.View.Model!));
                Assert.NotEqual(Strings.Get("editor.project.restored"), ucuncu.View.ExportStatusText);
            }

            using (var kapali = new Sahne(klasor, null))
            {
                kapali.Ac(dosya);
                Assert.True(kapali.Kes(50));
                Assert.False(kapali.View.AutosavePending);
                Assert.False(kapali.View.FlushProject());
            }
        });
        Assert.Equal(new[] { (S(0), S(600)) }, EditProject.Read(kayit)!.Clips.Select(k => (k.SourceStart, k.SourceEnd)));
        Kapat(klasor);
    }

    private static Avalonia.Styling.ControlTheme Tema(string ad)
    {
        Assert.True(Avalonia.Application.Current!.TryFindResource(ad, out var tema), ad);
        return Assert.IsType<Avalonia.Styling.ControlTheme>(tema);
    }

    [Fact]
    public void KaynakDegisinceKayitYuklenmezVeUyariSoylenir()
    {
        var klasor = Klasor("bayat");
        var dosya = Kaynak(klasor);
        var depo = new EditProjectStore(Path.Combine(klasor, "kayit"));
        AppHost.Run(() =>
        {
            using (var ilk = new Sahne(klasor, depo))
            {
                ilk.Ac(dosya);
                Assert.True(ilk.Kes(200));
                Assert.True(ilk.View.FlushProject());
            }

            using (var ayni = new Sahne(klasor, depo))
            {
                ayni.Ac(dosya);
                Assert.Equal(2, ayni.View.Model!.Clips.Count);
                Assert.Equal(Strings.Get("editor.project.restored"), ayni.View.ExportStatusText);
            }

            File.SetLastWriteTimeUtc(dosya, File.GetLastWriteTimeUtc(dosya).AddSeconds(5));
            using (var zaman = new Sahne(klasor, depo))
            {
                zaman.Ac(dosya);
                Assert.Single(zaman.View.Model!.Clips);
                Assert.Equal(Strings.Get("editor.project.stale"), zaman.View.ExportStatusText);
                Assert.True(zaman.View.TxtExportStatus.IsEffectivelyVisible);
                Assert.Equal(Tema("StatusWarning"), zaman.View.TxtExportStatus.Theme);
                Assert.False(zaman.View.Undo());

                var baska = Kaynak(klasor, "baska.mp4");
                zaman.Ac(baska);
                Assert.NotEqual(Strings.Get("editor.project.stale"), zaman.View.ExportStatusText);
                Assert.False(zaman.View.TxtExportStatus.IsEffectivelyVisible);
            }

            Assert.Equal(2, depo.Load(dosya)!.Clips.Count);
            File.WriteAllBytes(dosya, new byte[32]);
            File.SetLastWriteTimeUtc(dosya, new DateTime(depo.Load(dosya)!.Source.ModifiedUtcTicks, DateTimeKind.Utc));
            using (var boy = new Sahne(klasor, depo))
            {
                boy.Ac(dosya);
                Assert.Single(boy.View.Model!.Clips);
                Assert.Equal(Strings.Get("editor.project.stale"), boy.View.ExportStatusText);
            }
        });
        Kapat(klasor);
    }

    [Fact]
    public void ElleKaydedilenProjeAcilirBozuguVeBayatiSoylenir()
    {
        var klasor = Klasor("elle");
        var dosya = Kaynak(klasor);
        var oteki = Kaynak(klasor, "oteki.mp4");
        var proje = Path.Combine(klasor, "kurgu" + EditProject.Extension);
        AppHost.Run(() =>
        {
            Assert.Equal(EditProjectStore.AutosaveDelay, new EditorView().AutosaveDelay);
            using var sahne = new Sahne(klasor, null);
            var view = sahne.View;
            Assert.False(view.SaveProjectTo(proje));
            Assert.False(File.Exists(proje));

            sahne.Ac(dosya);
            Assert.True(sahne.Kes(100));
            Assert.True(sahne.Kes(400));
            Assert.True(view.SaveProjectTo(proje));
            Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.project.saved"), proje), view.ExportStatusText);
            Assert.Equal(3, EditProject.Read(proje)!.Clips.Count);

            Assert.False(view.SaveProjectTo(Path.Combine(klasor, "kurgu" + EditProject.Extension, "alt.json")));
            Assert.Equal(Tema("StatusError"), view.TxtExportStatus.Theme);
            Assert.StartsWith(Strings.Get("editor.project.failed").Replace("{0}", string.Empty), view.ExportStatusText, StringComparison.Ordinal);

            Assert.True(view.Undo());
            Assert.True(view.Undo());
            Assert.Single(view.Model!.Clips);
            var ayni = view.OpenProjectAsync(proje);
            Assert.True(sahne.Bekle(() => ayni.IsCompleted));
            Assert.True(ayni.Result);
            Assert.Equal(new[] { (S(0), S(100)), (S(100), S(400)), (S(400), S(600)) }, Kesimler(view.Model!));
            Assert.Equal(Strings.Get("editor.project.restored"), view.ExportStatusText);
            Assert.True(view.Undo());
            Assert.Single(view.Model!.Clips);

            sahne.Ac(oteki);
            Assert.Single(view.Model!.Clips);
            var baska = view.OpenProjectAsync(proje);
            Assert.True(sahne.Bekle(() => baska.IsCompleted));
            Assert.True(baska.Result);
            Assert.Equal(3, view.Model!.Clips.Count);
            Assert.Equal(Strings.Get("editor.project.restored"), view.ExportStatusText);

            var bozuk = Path.Combine(klasor, "bozuk" + EditProject.Extension);
            File.WriteAllText(bozuk, File.ReadAllText(proje).Replace("\"version\": 1", "\"version\": 99"));
            var okunmaz = view.OpenProjectAsync(bozuk);
            Assert.True(sahne.Bekle(() => okunmaz.IsCompleted));
            Assert.False(okunmaz.Result);
            Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.project.unreadable"), bozuk), view.ExportStatusText);
            Assert.Equal(Tema("StatusError"), view.TxtExportStatus.Theme);
            Assert.Equal(3, view.Model!.Clips.Count);

            File.WriteAllBytes(dosya, new byte[48]);
            var bayat = view.OpenProjectAsync(proje);
            Assert.True(sahne.Bekle(() => bayat.IsCompleted));
            Assert.False(bayat.Result);
            Assert.Equal(Strings.Get("editor.project.stale"), view.ExportStatusText);
            Assert.Equal(Tema("StatusWarning"), view.TxtExportStatus.Theme);

            File.Delete(dosya);
            var kayip = view.OpenProjectAsync(proje);
            Assert.True(sahne.Bekle(() => kayip.IsCompleted));
            Assert.False(kayip.Result);
            Assert.Equal(Strings.Get("editor.project.stale"), view.ExportStatusText);
        });
        Kapat(klasor);
    }

    [Fact]
    public void KomutlarCizelgeMenusundeVeTeslimKaydiSilmez()
    {
        var kok = GirdiKanit.Root;
        var axaml = File.ReadAllText(Path.Combine(kok, "src", "VidShrink.App", "Editing", "EditorView.axaml"));
        var menu = axaml[axaml.IndexOf("x:Name=\"TimelineMenu\"", StringComparison.Ordinal)..];
        menu = menu[..menu.IndexOf("</ContextMenu>", StringComparison.Ordinal)];
        Assert.Contains("x:Name=\"MnuProjectSave\" Header=\"{loc:Text editor.project.save}\"", menu, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MnuProjectOpen\" Header=\"{loc:Text editor.project.open}\"", menu, StringComparison.Ordinal);

        var teslim = File.ReadAllText(Path.Combine(kok, "src", "VidShrink.App", "Editing", "EditorView.Teslim.cs"));
        Assert.Contains("_projectNotice = false", teslim, StringComparison.Ordinal);
        foreach (var yasak in new[] { "Projects", "ForgetProject", "_stamp", "EditProjectStore" })
            Assert.DoesNotContain(yasak, teslim, StringComparison.Ordinal);
        var proje = File.ReadAllText(Path.Combine(kok, "src", "VidShrink.App", "Editing", "EditorView.Proje.cs"));
        Assert.DoesNotContain("File.Delete", proje, StringComparison.Ordinal);
    }

    [Fact]
    public void YediAnahtarHerDildeVeYerTutucuKorunur()
    {
        string[] anahtarlar =
        [
            "editor.project.save", "editor.project.open", "editor.project.saved", "editor.project.failed",
            "editor.project.unreadable", "editor.project.restored", "editor.project.stale",
        ];
        var kok = Path.Combine(GirdiKanit.Root, "src", "VidShrink.App", "Locales");
        var diller = Directory.GetDirectories(kok).Where(d => File.Exists(Path.Combine(d, "editor.json"))).ToList();
        Assert.Equal(42, diller.Count);
        var en = Oku(Path.Combine(kok, "en", "editor.json"));
        foreach (var dil in diller)
        {
            var ad = Path.GetFileName(dil);
            var sozluk = Oku(Path.Combine(dil, "editor.json"));
            foreach (var anahtar in anahtarlar)
            {
                Assert.True(sozluk.TryGetValue(anahtar, out var metin) && !string.IsNullOrWhiteSpace(metin), $"{ad}: {anahtar}");
                Assert.True(en[anahtar].Contains("{0}", StringComparison.Ordinal) == metin!.Contains("{0}", StringComparison.Ordinal), $"{ad}: {anahtar} yer tutucu");
                if (ad != "en") Assert.True(metin != en[anahtar], $"{ad}: {anahtar} cevrilmemis");
            }
        }
    }

    private static Dictionary<string, string> Oku(string dosya)
    {
        using var belge = JsonDocument.Parse(File.ReadAllText(dosya));
        return belge.RootElement.EnumerateObject().Where(o => o.Value.ValueKind == JsonValueKind.String).ToDictionary(o => o.Name, o => o.Value.GetString()!);
    }
}
