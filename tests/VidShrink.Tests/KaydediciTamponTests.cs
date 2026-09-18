using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// Paket 2b T13, kayıt tamponu: ffmpeg 2 sn'lik Matroska parçalarını <c>-segment_wrap</c> ile dönen
/// adlara yazar, F11 ya da düğme en yeni <see cref="ReplayBuffer.PartsFor"/> parçayı kopyalayıp
/// <c>concat -c copy</c> ile kayıt klasörüne birleştirir. Canlı kol 4 sn'lik tamponu 9 sn gerçek
/// zamanlı lavfi kaynağıyla döndürür: kaydedilen dosya 9 sn değil son saniyelerdir ve klasördeki parça
/// sayısı sarma sınırını geçmez. Arayüz sahte <see cref="IReplayBuffer"/> ile. Kanıt <c>.calisma/paket-2b/tampon/</c>.
/// </summary>
public sealed class KaydediciTamponTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "paket-2b", "tampon");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    /// <summary>Son asertten sonra çağrılır; kuralı <see cref="KanitKapanisi"/> anlatıyor.</summary>
    private static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Kanit, adlar);

    private static RecorderRequest Istek() => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Fps = 15,
        Region = new RecorderRegion(0, 0, 640, 360),
        Container = RecorderContainer.Mkv,
        Preset = "ultrafast"
    };

    [Fact]
    public void ParcaSayisiSecimVeListeSonSaniyeleriTutar()
    {
        Assert.Equal(3, ReplayBuffer.PartsFor(4));
        Assert.Equal(9, ReplayBuffer.PartsFor(15));
        Assert.Equal(16, ReplayBuffer.PartsFor(30));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReplayBuffer.PartsFor(0));

        var t0 = new DateTime(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);
        var parcalar = Enumerable.Range(0, 6)
            .Select(i => new ReplayPart(Path.Combine("b", $"replay_{(i + 3) % 6:000}.mkv"), t0.AddSeconds(2 * i), 100))
            .Append(new ReplayPart(Path.Combine("b", "replay_009.mkv"), t0.AddSeconds(30), 0))
            .Append(new ReplayPart(Path.Combine("b", "list.txt"), t0.AddSeconds(40), 50))
            .Reverse()
            .ToList();

        Assert.Equal(
            new[] { "replay_000.mkv", "replay_001.mkv", "replay_002.mkv" },
            ReplayBuffer.Pick(parcalar, 4).Select(Path.GetFileName));
        Assert.Equal(6, ReplayBuffer.Pick(parcalar, 120).Count);
        Assert.Empty(ReplayBuffer.Pick(Array.Empty<ReplayPart>(), 30));

        Assert.Equal("file 'C:\\a b\\replay_000.mkv'\nfile 'C:\\o'\\''n\\replay_001.mkv'\n",
            ReplayBuffer.ConcatList(new[] { "C:\\a b\\replay_000.mkv", "C:\\o'n\\replay_001.mkv" }));
    }

    [Fact]
    public void YakalamaParcaYazarKaydetmeKopyaylaBirlestirir()
    {
        var klasor = Path.Combine("t", "tampon");
        var args = ReplayBuffer.BuildCapture(Istek() with { MaxDuration = TimeSpan.FromSeconds(9), MaxMegabytes = 5, PreviewPath = "p.jpg", Container = RecorderContainer.Mp4 }, klasor, 30);
        var kayit = RecorderArguments.Build(Istek() with { Container = RecorderContainer.Mkv }, "x.mkv");

        Assert.Equal(kayit.Take(kayit.Count - 1), args.Take(kayit.Count - 1));
        Assert.Equal(
            new[] { "-force_key_frames", "expr:gte(t,n_forced*2)", "-f", "segment", "-segment_time", "2", "-segment_wrap", "17", "-segment_format", "matroska", "-reset_timestamps", "1", Path.Combine(klasor, "replay_%03d.mkv") },
            args.Skip(kayit.Count - 1));
        Assert.DoesNotContain("-t", args);
        Assert.DoesNotContain("-fs", args);
        Assert.DoesNotContain("image2", args);
        Assert.DoesNotContain("+faststart", args);

        Assert.Equal(
            new[] { "-hide_banner", "-y", "-nostdin", "-f", "concat", "-safe", "0", "-i", "l.txt", "-map", "0", "-c", "copy", "son.mkv" },
            ReplayBuffer.BuildSave("l.txt", "son.mkv"));
        Assert.Equal(new[] { "-movflags", "+faststart", "son.mp4" }, ReplayBuffer.BuildSave("l.txt", "son.mp4").TakeLast(3));
        Assert.Throws<ArgumentException>(() => ReplayBuffer.BuildCapture(Istek(), " ", 30));
    }

    private static List<string> LavfiRe(IReadOnlyList<string> args)
    {
        var liste = args.ToList();
        var gdigrab = liste.IndexOf("gdigrab");
        var girdi = liste.IndexOf("-i", gdigrab);
        Assert.True(gdigrab > 0 && girdi > gdigrab, string.Join(' ', args));
        liste.RemoveRange(gdigrab - 1, girdi - gdigrab + 3);
        liste.InsertRange(gdigrab - 1, new[] { "-re", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=15" });
        liste.InsertRange(liste.IndexOf("-c:v"), new[] { "-threads", "1" });
        return liste;
    }

    [Fact]
    public async Task CanliTamponSonSaniyeleriKaydederEskiParcalarSarilir()
    {
        var kok = Kanit;
        foreach (var eski in Directory.GetFiles(kok)) File.Delete(eski);
        var parcalar = Path.Combine(kok, "parcalar");
        var hedef = Path.Combine(kok, "son-saniyeler.mkv");
        const int saniye = 4;
        var args = LavfiRe(ReplayBuffer.BuildCapture(Istek(), parcalar, saniye));
        File.WriteAllText(Path.Combine(kok, "args.txt"), string.Join(' ', args));

        var tampon = await ReplayRecorder.StartAsync(args, parcalar, saniye);
        ReplaySaveResult sonuc;
        int enCokParca = 0;
        try
        {
            for (var i = 0; i < 18; i++)
            {
                await Task.Delay(500);
                enCokParca = Math.Max(enCokParca, ReplayRecorder.Parts(parcalar).Count);
            }

            sonuc = await tampon.SaveAsync(hedef);
        }
        finally
        {
            await tampon.StopAsync();
        }

        var sure = File.Exists(hedef) ? double.Parse(KaydediciOnizlemeTests.Probe(hedef, "format=duration"), CultureInfo.InvariantCulture) : double.NaN;
        File.WriteAllLines(Path.Combine(kok, "olcu.txt"), new[]
        {
            $"tampon={saniye} sn, calisma=9 sn, sarma={ReplayBuffer.PartsFor(saniye) + 1}",
            $"en cok parca={enCokParca}",
            $"sonuc={sonuc}",
            $"kaydedilen sure={sure.ToString(CultureInfo.InvariantCulture)}",
            $"durduktan sonra klasor var mi={Directory.Exists(parcalar)}"
        });

        Assert.True(sonuc.Ok, sonuc.Error);
        Assert.Equal(ReplayBuffer.PartsFor(saniye), sonuc.Parts);
        Assert.InRange(enCokParca, 2, ReplayBuffer.PartsFor(saniye) + 1);
        Assert.InRange(sure, saniye - 1.0, ReplayBuffer.SegmentSeconds * ReplayBuffer.PartsFor(saniye) + 0.5);
        Assert.False(Directory.Exists(parcalar));

        Kapat("args.txt", "olcu.txt", "son-saniyeler.mkv");
    }

    private sealed class SahteTampon : IReplayBuffer
    {
        public int Seconds { get; init; } = 15;

        public List<string> Hedefler { get; } = new();

        public int Durdurma { get; private set; }

        public Func<string, ReplaySaveResult> Kaydet { get; set; } = t => new ReplaySaveResult(true, t, 8, string.Empty);

        public Task<ReplaySaveResult> SaveAsync(string target, CancellationToken ct = default)
        {
            Hedefler.Add(target);
            return Task.FromResult(Kaydet(target));
        }

        public Task StopAsync()
        {
            Durdurma++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void ArayuzTamponuAcarF11KaydederKapatirSecimAyardaKalir()
    {
        var sahte = new SahteTampon();
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var once = new RecorderView(ayarYolu);
            Bul<ComboBox>(once, "CmbReplaySeconds").SelectedIndex = Array.IndexOf(ReplayBuffer.SecondsChoices, 15);

            var view = new RecorderView(ayarYolu);
            var secilen = view.SelectedReplaySeconds;
            var f11Kapali = view.RunHotkeyAsync(HotkeyAction.ReplaySave).GetAwaiter().GetResult();
            var kaydetKapali = view.ReplaySaveVisible;
            var acmaMetni = view.ReplayToggleText;

            RecorderRequest? verilen = null;
            int verilenSaniye = 0;
            view.ReplayStarter = (istek, _, s) => { verilen = istek; verilenSaniye = s; return Task.FromResult<IReplayBuffer>(sahte); };
            var acildi = view.StartReplayAsync().GetAwaiter().GetResult();
            var calisiyorNot = view.NoticeText;
            var baslatGorunur = Bul<Button>(view, "BtnStart").IsVisible;
            var kaydetGorunur = view.ReplaySaveVisible;
            var kaydetMetni = view.ReplaySaveText;
            var kutuKilitli = !Bul<ComboBox>(view, "CmbReplaySeconds").IsEnabled;
            var kapatMetni = view.ReplayToggleText;

            var f11 = view.RunHotkeyAsync(HotkeyAction.ReplaySave).GetAwaiter().GetResult();
            var kaydedildi = view.NoticeText;
            sahte.Kaydet = _ => new ReplaySaveResult(false, null, 0, "birlesmedi");
            view.SaveReplayAsync().GetAwaiter().GetResult();
            var hata = view.ErrorText;

            view.StopReplayAsync().GetAwaiter().GetResult();
            return (secilen, f11Kapali, kaydetKapali, acmaMetni, acildi, verilen, verilenSaniye, calisiyorNot, baslatGorunur, kaydetGorunur,
                kaydetMetni, kutuKilitli, kapatMetni, f11, kaydedildi, hata, calisiyorSonra: view.ReplayRunning,
                baslatSonra: Bul<Button>(view, "BtnStart").IsVisible, kaydetSonra: view.ReplaySaveVisible);
        }));

        Assert.Equal(15, olcu.secilen);
        Assert.False(olcu.f11Kapali);
        Assert.False(olcu.kaydetKapali);
        Assert.True(olcu.acildi);
        Assert.Equal(15, olcu.verilenSaniye);
        Assert.Equal(RecorderContainer.Mkv, olcu.verilen!.Container);
        Assert.Null(olcu.verilen.PreviewPath);
        Assert.Contains("15", olcu.calisiyorNot);
        Assert.False(olcu.baslatGorunur);
        Assert.True(olcu.kaydetGorunur);
        Assert.Contains("15", olcu.kaydetMetni);
        Assert.True(olcu.kutuKilitli);
        Assert.NotEqual(olcu.acmaMetni, olcu.kapatMetni);
        Assert.True(olcu.f11);
        Assert.Contains(sahte.Hedefler[0], olcu.kaydedildi);
        Assert.Equal(2, sahte.Hedefler.Count);
        Assert.Contains("birlesmedi", olcu.hata);
        Assert.Equal(1, sahte.Durdurma);
        Assert.False(olcu.calisiyorSonra);
        Assert.True(olcu.baslatSonra);
        Assert.False(olcu.kaydetSonra);
    }
}
