using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Düzenleyici çizelgesinin anahtar kare çentikleri ve küçük resim şeridi
/// (<c>Core/Editing/TimelineStrip</c>, <c>ThumbnailQueue</c>, <c>EditorTimeline.Serit</c>,
/// <c>EditorView.Serit</c>): kaynak tick'i çizelge anına iner, sık çentik seyreltilir, kareler
/// tek sırada üretilir ve önbellekte durur, görünüm ve kaynak değişince eski istek bırakılır.
/// Uzun dosya sahte okuyucuyla ölçülür; canlı kollar 3 sn 160x90 klipte.
/// </summary>
public sealed class DuzenleyiciSeritTests
{
    private const string Dal = "worktree-agent-a264e4ad0c3ff8c81";
    private const double Genislik = 1000;

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static string Kanit => Path.Combine(GirdiKanit.Root, ".calisma", Dal);

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(Kanit, "serit-" + ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Kapat(string klasor)
    {
        try { if (Directory.Exists(klasor)) Directory.Delete(klasor, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        try { if (Directory.Exists(Kanit) && !Directory.EnumerateFileSystemEntries(Kanit).Any()) Directory.Delete(Kanit); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static long[] Her(double adim, double bitis)
    {
        var liste = new List<long>();
        for (var i = 0; i * adim < bitis; i++) liste.Add(S(i * adim));
        return liste.ToArray();
    }

    private sealed class SahteKaynak : IThumbnailSource
    {
        private readonly object _kilit = new();
        private int _icerde;

        internal List<(string Kaynak, long Tick, int Yukseklik)> Cagrilar { get; } = new();

        internal List<long> Iptaller { get; } = new();

        internal int EnCokAyniAnda { get; private set; }

        internal Func<long, byte[]?> Uret { get; set; } = _ => Png;

        internal Func<long, Task>? Kapi { get; set; }

        internal long[] Tickler() { lock (_kilit) return Cagrilar.Select(c => c.Tick).ToArray(); }

        public async Task<byte[]?> ReadAsync(string source, long sourceTick, int height, CancellationToken ct)
        {
            lock (_kilit)
            {
                Cagrilar.Add((source, sourceTick, height));
                _icerde++;
                EnCokAyniAnda = Math.Max(EnCokAyniAnda, _icerde);
            }

            try
            {
                if (Kapi is { } kapi)
                {
                    var bekleme = kapi(sourceTick);
                    var iptal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    using (ct.Register(() => iptal.TrySetResult()))
                        await Task.WhenAny(bekleme, iptal.Task).ConfigureAwait(false);
                    if (ct.IsCancellationRequested)
                    {
                        lock (_kilit) Iptaller.Add(sourceTick);
                        ct.ThrowIfCancellationRequested();
                    }
                }
                else
                {
                    await Task.Yield();
                }

                return Uret(sourceTick);
            }
            finally
            {
                lock (_kilit) _icerde--;
            }
        }
    }

    private static async Task Bosalsin(ThumbnailQueue kuyruk)
    {
        var bitti = await Task.WhenAny(kuyruk.Idle, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.Same(kuyruk.Idle, bitti);
        await kuyruk.Idle;
    }

    private static async Task Bekle(Func<bool> kosul)
    {
        var saat = Stopwatch.StartNew();
        while (!kosul() && saat.Elapsed.TotalSeconds < 20) await Task.Delay(5);
        Assert.True(kosul(), "kosul 20 sn icinde gerceklesmedi");
    }

    private static int Hazir(ThumbnailQueue kuyruk, params long[] tickler) => tickler.Count(tick => kuyruk.TryGet(tick, out _));

    private static void BosalsinEsli(ThumbnailQueue kuyruk) => Assert.True(kuyruk.Idle.Wait(TimeSpan.FromSeconds(20)), "kuyruk 20 sn icinde bosalmadi");

    private static void BekleEsli(Func<bool> kosul)
    {
        var saat = Stopwatch.StartNew();
        while (!kosul() && saat.Elapsed.TotalSeconds < 20) Thread.Sleep(5);
        Assert.True(kosul(), "kosul 20 sn icinde gerceklesmedi");
    }

    [Fact]
    public void SaniyelerSiraliTekilTickOlur()
    {
        var tick = TimelineStrip.SourceTicks(new[] { 2.0, 0.5, double.NaN, -1, 2.0, 0, double.PositiveInfinity });

        Assert.Equal(new[] { 0L, 120_000L, 480_000L }, tick);
        Assert.Empty(TimelineStrip.SourceTicks(Array.Empty<double>()));
    }

    [Fact]
    public void AnahtarKareCizelgeAninaInerSilinmisKaynakGorunmez()
    {
        var model = new EditTimeline(new[] { new EditClip(S(10), S(20)), new EditClip(S(40), S(50)) }, S(60));
        var kaynak = Her(2, 60);

        var hepsi = TimelineStrip.Keyframes(model, kaynak, 0, model.Duration);
        var pencere = TimelineStrip.Keyframes(model, kaynak, S(3), S(13));

        Assert.Equal(new[] { S(0), S(2), S(4), S(6), S(8), S(10), S(12), S(14), S(16), S(18) }, hepsi);
        Assert.Equal(new[] { S(4), S(6), S(8), S(10), S(12) }, pencere);
        Assert.Empty(TimelineStrip.Keyframes(model, Array.Empty<long>(), 0, model.Duration));
        Assert.Empty(TimelineStrip.Keyframes(model, new[] { S(25), S(30), S(55) }, 0, model.Duration));
    }

    [Fact]
    public void TersVeHizliKliptekiAnahtarKareDogruAnaDuser()
    {
        var ters = new EditTimeline(new[] { new EditClip(S(10), S(20), 1m, true) }, S(60));
        var hizli = new EditTimeline(new[] { new EditClip(0, S(20), 2m) }, S(60));
        var iki = new EditTimeline(new[] { new EditClip(0, S(10)), new EditClip(0, S(10)) }, S(60));

        var tersAnlar = TimelineStrip.Keyframes(ters, Her(2, 60), 0, ters.Duration);
        var hizliAnlar = TimelineStrip.Keyframes(hizli, new[] { S(4), S(10) }, 0, hizli.Duration);
        var ikiAnlar = TimelineStrip.Keyframes(iki, new[] { S(4) }, 0, iki.Duration);

        Assert.Equal(new[] { S(2) - 1, S(4) - 1, S(6) - 1, S(8) - 1, S(10) - 1 }, tersAnlar);
        Assert.Equal(new[] { S(2), S(5) }, hizliAnlar);
        Assert.Equal(new[] { S(4), S(14) }, ikiAnlar);
    }

    [Fact]
    public void SikCentikSeyreltilirSeyrekOlanaDokunulmaz()
    {
        var anlar = Enumerable.Range(0, 100).Select(i => (long)i * 100).ToArray();

        var sik = TimelineStrip.Thin(anlar, 0.01, 4);
        var seyrek = TimelineStrip.Thin(anlar, 0.05, 4);
        var sinirda = TimelineStrip.Thin(anlar, 0.04, 4);

        Assert.Equal(25, sik.Count);
        Assert.Equal(0, sik[0]);
        for (var i = 1; i < sik.Count; i++) Assert.True((sik[i] - sik[i - 1]) * 0.01 >= 4);
        Assert.Equal(anlar, seyrek);
        Assert.Equal(anlar, sinirda);
        Assert.Same(anlar, TimelineStrip.Thin(anlar, 0, 4));
        Assert.Same(anlar, TimelineStrip.Thin(anlar, double.NaN, 4));
        Assert.Same(anlar, TimelineStrip.Thin(anlar, 0.01, 0));
    }

    [Fact]
    public void KareAdimiVeIzgarasiKaydirincaAyniAnlariVerir()
    {
        var model = new EditTimeline(new[] { new EditClip(S(10), S(16)), new EditClip(S(40), S(44)) }, S(60));

        Assert.Equal(S(2), TimelineStrip.TileStep(50.0 / S(2), 50));
        Assert.Equal(S(2) + 1, TimelineStrip.TileStep(50.0 / (S(2) + 0.5), 50));
        Assert.Equal(1, TimelineStrip.TileStep(1000, 50));
        Assert.Equal(0, TimelineStrip.TileStep(0, 50));
        Assert.Equal(0, TimelineStrip.TileStep(1, 0));
        Assert.Equal(0, TimelineStrip.TileStep(double.NaN, 50));

        var hepsi = TimelineStrip.Tiles(model, 0, S(100), S(2));
        var kaymis = TimelineStrip.Tiles(model, S(3), S(7), S(2));

        Assert.Equal(new[] { S(0), S(2), S(4), S(6), S(8) }, hepsi.Select(k => k.Time));
        Assert.Equal(new[] { S(10), S(12), S(14), S(40), S(42) }, hepsi.Select(k => k.SourceTime));
        Assert.Equal(new[] { new StripTile(S(2), S(12)), new StripTile(S(4), S(14)), new StripTile(S(6), S(40)) }, kaymis);
        Assert.Empty(TimelineStrip.Tiles(model, 0, S(100), 0));
        Assert.Empty(TimelineStrip.Tiles(model, S(5), S(5), S(2)));
    }

    [Fact]
    public async Task KarelerTekSiradaUretilirVeOnbellektenYenidenUretilmez()
    {
        var kaynak = new SahteKaynak { Kapi = _ => Task.Delay(3) };
        using var kuyruk = new ThumbnailQueue(kaynak);
        var hazir = new List<long>();
        kuyruk.Ready += tick => { lock (hazir) hazir.Add(tick); };
        kuyruk.Open("a.mp4", 32);

        kuyruk.Request(new long[] { 5, 3, 9, 3, 1 });
        await Bosalsin(kuyruk);

        Assert.Equal(new long[] { 5, 3, 9, 1 }, kaynak.Tickler());
        Assert.Equal(1, kaynak.EnCokAyniAnda);
        Assert.All(kaynak.Cagrilar, c => Assert.Equal(("a.mp4", 32), (c.Kaynak, c.Yukseklik)));
        Assert.Equal(new long[] { 5, 3, 9, 1 }, hazir);
        Assert.Equal(4, Hazir(kuyruk, 1, 3, 5, 9));
        Assert.True(kuyruk.TryGet(9, out var veri));
        Assert.Same(Png, veri);
        Assert.False(kuyruk.TryGet(7, out _));

        kuyruk.Request(new long[] { 1, 3, 5, 9 });
        await Bosalsin(kuyruk);
        Assert.Equal(4, kaynak.Cagrilar.Count);

        kuyruk.Request(new long[] { 9, 11 });
        await Bosalsin(kuyruk);
        Assert.Equal(new long[] { 5, 3, 9, 1, 11 }, kaynak.Tickler());
    }

    [Fact]
    public async Task OnbellekBaytSinirindaEnEskiKullanilaniAtar()
    {
        var kaynak = new SahteKaynak { Uret = _ => new byte[100] };
        using var kuyruk = new ThumbnailQueue(kaynak, 250);
        kuyruk.Open("a.mp4", 32);

        kuyruk.Request(new long[] { 1, 2 });
        await Bosalsin(kuyruk);
        Assert.True(kuyruk.TryGet(1, out _));
        kuyruk.Request(new long[] { 3 });
        await Bosalsin(kuyruk);

        Assert.True(kuyruk.TryGet(1, out _));
        Assert.False(kuyruk.TryGet(2, out _));
        Assert.True(kuyruk.TryGet(3, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThumbnailQueue(kaynak, 0));
    }

    [Fact]
    public async Task GorunumDegisinceBekleyenIstekBirakilirUcustakiIptalEdilir()
    {
        var kapi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var kaynak = new SahteKaynak { Kapi = tick => tick == 1 ? kapi.Task : Task.CompletedTask };
        using var kuyruk = new ThumbnailQueue(kaynak);
        kuyruk.Open("a.mp4", 32);

        kuyruk.Request(new long[] { 1, 2, 3 });
        await Bekle(() => kaynak.Tickler().Length == 1);
        kuyruk.Request(new long[] { 7, 8 });
        await Bosalsin(kuyruk);

        Assert.Equal(new long[] { 1, 7, 8 }, kaynak.Tickler());
        Assert.Equal(new long[] { 1 }, kaynak.Iptaller);
        Assert.False(kuyruk.TryGet(1, out _));
        Assert.True(kuyruk.TryGet(7, out _));
        Assert.True(kuyruk.TryGet(8, out _));

        kapi.SetResult();
        kuyruk.Request(new long[] { 1 });
        await Bosalsin(kuyruk);
        Assert.Equal(new long[] { 1, 7, 8, 1 }, kaynak.Tickler());
    }

    [Fact]
    public async Task UcustakiKareYeniListedeyseIptalEdilmez()
    {
        var kapi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var kaynak = new SahteKaynak { Kapi = tick => tick == 1 ? kapi.Task : Task.CompletedTask };
        using var kuyruk = new ThumbnailQueue(kaynak);
        kuyruk.Open("a.mp4", 32);

        kuyruk.Request(new long[] { 1, 2 });
        await Bekle(() => kaynak.Tickler().Length == 1);
        kuyruk.Request(new long[] { 4, 1 });
        kapi.SetResult();
        await Bosalsin(kuyruk);

        Assert.Empty(kaynak.Iptaller);
        Assert.Equal(new long[] { 1, 4 }, kaynak.Tickler());
        Assert.True(kuyruk.TryGet(1, out _));
    }

    [Fact]
    public async Task KaynakDegisinceOnbellekBosalirEskiOkumaYerineOturmaz()
    {
        var kapi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var kaynak = new SahteKaynak();
        using var kuyruk = new ThumbnailQueue(kaynak);
        kuyruk.Open("a.mp4", 32);
        kuyruk.Request(new long[] { 1, 2 });
        await Bosalsin(kuyruk);
        Assert.Equal(2, Hazir(kuyruk, 1, 2));

        kuyruk.Open("a.mp4", 32);
        Assert.Equal(2, Hazir(kuyruk, 1, 2));

        kaynak.Kapi = _ => kapi.Task;
        kuyruk.Request(new long[] { 3 });
        await Bekle(() => kaynak.Tickler().Length == 3);
        kuyruk.Open("b.mp4", 32);

        Assert.Equal(0, Hazir(kuyruk, 1, 2));
        await Bosalsin(kuyruk);
        Assert.Equal(new long[] { 3 }, kaynak.Iptaller);
        Assert.False(kuyruk.TryGet(3, out _));

        kaynak.Kapi = null;
        kuyruk.Request(new long[] { 1 });
        await Bosalsin(kuyruk);
        Assert.Equal(("b.mp4", 1L, 32), kaynak.Cagrilar[^1]);
        Assert.Equal(1, Hazir(kuyruk, 1, 2, 3));

        kuyruk.Open(null, 0);
        kuyruk.Request(new long[] { 5 });
        await Bosalsin(kuyruk);
        Assert.Equal(4, kaynak.Cagrilar.Count);
    }

    [Fact]
    public async Task DusenKareBosKalirAyniKaynaktaYenidenDenenmez()
    {
        var kaynak = new SahteKaynak
        {
            Uret = tick => tick switch
            {
                1 => null,
                2 => throw new IOException("okunamadi"),
                3 => throw new InvalidOperationException("ffmpeg yok"),
                _ => Png
            }
        };
        using var kuyruk = new ThumbnailQueue(kaynak);
        kuyruk.Open("a.mp4", 32);

        kuyruk.Request(new long[] { 1, 2, 3, 4 });
        await Bosalsin(kuyruk);
        kuyruk.Request(new long[] { 1, 2, 3, 4 });
        await Bosalsin(kuyruk);

        Assert.Equal(new long[] { 1, 2, 3, 4 }, kaynak.Tickler());
        Assert.Equal(1, Hazir(kuyruk, 1, 2, 3, 4));
        Assert.True(kuyruk.TryGet(4, out _));
    }

    private static (EditorTimeline Cizelge, Window Pencere) Kur(EditTimeline model)
    {
        var cizelge = new EditorTimeline();
        var pencere = new Window { Width = Genislik, Height = 400, Content = cizelge };
        pencere.Show();
        cizelge.Fps = 30;
        cizelge.Show(model);
        Dispatcher.UIThread.RunJobs();
        return (cizelge, pencere);
    }

    [Fact]
    public void SeritIzlerinAltindaBelirtecYuksekligindeYerTutar()
    {
        var olcum = AppHost.Run(() =>
        {
            var (cizelge, pencere) = Kur(EditTimeline.FromSource(S(600)));
            var sonuc = (
                Istenen: cizelge.DesiredSize.Height,
                cizelge.TracksBottom,
                cizelge.StripTop,
                cizelge.StripHeight,
                cizelge.StripBottom,
                Aralik: EditorTokens.Size(cizelge, "EditorTrackGap"),
                Serit: EditorTokens.Size(cizelge, "EditorThumbnailStripHeight"),
                Dugme: EditorTokens.Size(cizelge, "PlaybackBarButtonSize"));
            pencere.Close();
            return sonuc;
        });

        Assert.True(olcum.Serit > 0);
        Assert.Equal(olcum.Dugme, olcum.Serit, 6);
        Assert.Equal(olcum.TracksBottom + olcum.Aralik, olcum.StripTop, 6);
        Assert.Equal(olcum.Serit, olcum.StripHeight, 6);
        Assert.Equal(olcum.StripTop + olcum.Serit, olcum.StripBottom, 6);
        Assert.InRange(olcum.Istenen, olcum.StripBottom, olcum.StripBottom + 1);
    }

    [Fact]
    public void CizelgedeSikCentikSeyrelirYakinlasincaAcilirBosListeCentiksiz()
    {
        var olcum = AppHost.Run(() =>
        {
            var (cizelge, pencere) = Kur(EditTimeline.FromSource(S(600)));
            var bos = cizelge.KeyframeXs().Count;
            var aralik = EditorTokens.Size(cizelge, "EditorKeyframeMinSpacing");

            cizelge.Keyframes = Her(60, 600);
            var seyrek = cizelge.KeyframeXs().ToArray();
            var beklenen = Her(60, 600).Select(cizelge.TimeToX).ToArray();

            cizelge.Keyframes = Her(0.5, 600);
            var sik = cizelge.KeyframeXs().ToArray();
            var iz = cizelge.TrackWidth;

            cizelge.PixelsPerTick = 100.0 / S(1);
            cizelge.ViewStart = S(100);
            var yakin = cizelge.KeyframeXs().ToArray();
            var yakinBeklenen = Her(0.5, 600).Where(t => t >= cizelge.ViewStart && t <= cizelge.ViewEnd).Select(cizelge.TimeToX).ToArray();

            cizelge.Keyframes = Array.Empty<long>();
            var temiz = cizelge.KeyframeXs().Count;
            pencere.Close();
            return (bos, aralik, seyrek, beklenen, sik, iz, yakin, yakinBeklenen, temiz);
        });

        Assert.Equal(0, olcum.bos);
        Assert.True(olcum.aralik > 0);
        Assert.Equal(olcum.beklenen, olcum.seyrek);
        Assert.Equal(10, olcum.seyrek.Length);
        Assert.True(olcum.sik.Length < 1200, $"seyreltilmedi: {olcum.sik.Length}");
        Assert.InRange(olcum.sik.Length, (int)(olcum.iz / olcum.aralik / 2), (int)(olcum.iz / olcum.aralik) + 1);
        for (var i = 1; i < olcum.sik.Length; i++)
            Assert.True(olcum.sik[i] - olcum.sik[i - 1] >= olcum.aralik - 1e-6, $"{i}: {olcum.sik[i] - olcum.sik[i - 1]}");
        Assert.True(olcum.yakin.Length > 2);
        Assert.Equal(olcum.yakinBeklenen, olcum.yakin);
        Assert.Equal(0, olcum.temiz);
    }

    [Fact]
    public void SeritKareleriGeldikceOturturGelmeyenYerBosKalir()
    {
        var kapi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var kaynak = new SahteKaynak { Kapi = _ => kapi.Task };
        using var kuyruk = new ThumbnailQueue(kaynak);
        kuyruk.Open("a.mp4", 32);

        var olcum = AppHost.Run(() =>
        {
            var (cizelge, pencere) = Kur(EditTimeline.FromSource(S(600)));
            cizelge.ThumbnailAspect = 2;
            cizelge.Thumbnails = kuyruk;
            using var hedef = new RenderTargetBitmap(new PixelSize((int)Genislik, (int)Math.Ceiling(cizelge.DesiredSize.Height)));

            var kareler = cizelge.StripTiles();
            hedef.Render(cizelge);
            var bekleyenCizim = cizelge.StripTilesDrawn;
            var bekleyenHazir = kareler.Count(k => k.Ready);

            kapi.SetResult();
            BosalsinEsli(kuyruk);
            Dispatcher.UIThread.RunJobs();
            hedef.Render(cizelge);
            var gelenCizim = cizelge.StripTilesDrawn;
            var gelen = cizelge.StripTiles();
            var okunan = kaynak.Cagrilar.Count;

            hedef.Render(cizelge);
            var yeniden = kaynak.Cagrilar.Count;
            var sonuc = (kareler, bekleyenCizim, bekleyenHazir, gelenCizim, gelen, okunan, yeniden,
                Yukseklik: cizelge.StripHeight, Iz: cizelge.TrackWidth, Baslik: cizelge.HeaderWidth, Son: cizelge.TimeToX(S(600)));
            pencere.Close();
            return sonuc;
        });

        Assert.True(olcum.kareler.Count > 3);
        Assert.Equal(0, olcum.bekleyenCizim);
        Assert.Equal(0, olcum.bekleyenHazir);
        Assert.Equal(olcum.kareler.Count, olcum.gelenCizim);
        Assert.All(olcum.gelen, k => Assert.True(k.Ready));
        Assert.Equal(olcum.kareler.Count, olcum.okunan);
        Assert.Equal(olcum.okunan, olcum.yeniden);
        Assert.All(olcum.kareler, k => Assert.Equal(olcum.Yukseklik * 2, k.Width, 6));
        Assert.Equal(olcum.Baslik, olcum.kareler[0].X, 6);
        for (var i = 1; i < olcum.kareler.Count; i++)
            Assert.InRange(olcum.kareler[i].X - olcum.kareler[i - 1].X, olcum.Yukseklik * 2 - 1e-6, olcum.Yukseklik * 2 + 1);
        Assert.True(olcum.kareler[^1].X < olcum.Son);
        Assert.InRange(olcum.kareler.Count, (int)(olcum.Iz / (olcum.Yukseklik * 2 + 1)), (int)(olcum.Iz / (olcum.Yukseklik * 2)) + 1);
        Assert.True(olcum.Iz > 0);
    }

    [Fact]
    public void KaydirincaBekleyenKarelerBirakilirBozukKareCizimiDusurmez()
    {
        var kapi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var kaynak = new SahteKaynak { Kapi = _ => kapi.Task, Uret = _ => new byte[] { 1, 2, 3, 4 } };
        using var kuyruk = new ThumbnailQueue(kaynak);
        kuyruk.Open("a.mp4", 32);

        var olcum = AppHost.Run(() =>
        {
            var (cizelge, pencere) = Kur(EditTimeline.FromSource(S(600)));
            cizelge.PixelsPerTick = 100.0 / S(1);
            cizelge.Thumbnails = kuyruk;
            BekleEsli(() => kaynak.Tickler().Length == 1);
            var ilk = cizelge.StripTiles().Select(k => k.SourceTime).ToArray();

            cizelge.ViewStart = S(300);
            var ikinci = cizelge.StripTiles().Select(k => k.SourceTime).ToArray();
            kapi.SetResult();
            BosalsinEsli(kuyruk);
            Dispatcher.UIThread.RunJobs();

            using var hedef = new RenderTargetBitmap(new PixelSize((int)Genislik, (int)Math.Ceiling(cizelge.DesiredSize.Height)));
            hedef.Render(cizelge);
            var cizilen = cizelge.StripTilesDrawn;

            var once = kaynak.Tickler().Length;
            cizelge.PauseStrip();
            cizelge.ViewStart = S(100);
            BosalsinEsli(kuyruk);
            var duraklamada = kaynak.Tickler().Length - once;
            cizelge.ResumeStrip();
            BosalsinEsli(kuyruk);
            var sonuc = (ilk, ikinci, cizilen, duraklamada, Okunan: kaynak.Tickler(), Hazir: Hazir(kuyruk, ikinci));
            pencere.Close();
            return sonuc;
        });

        Assert.True(olcum.ilk.Length > 3);
        Assert.Empty(olcum.ilk.Intersect(olcum.ikinci));
        Assert.Equal(olcum.ilk[0], olcum.Okunan[0]);
        Assert.Equal(olcum.ikinci, olcum.Okunan.Skip(1).Take(olcum.ikinci.Length));
        Assert.Empty(olcum.Okunan.Skip(1).Intersect(olcum.ilk));
        Assert.Equal(0, olcum.cizilen);
        Assert.Equal(0, olcum.duraklamada);
        Assert.True(olcum.Okunan.Length > 1 + olcum.ikinci.Length);
        Assert.Equal(olcum.ikinci.Length, olcum.Hazir);
    }

    private sealed class Sahne : IDisposable
    {
        private readonly Window _pencere;

        internal Sahne(string klasor)
        {
            View = new EditorView
            {
                KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 160, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 },
                Projects = null,
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

        internal void Pompala(Func<bool> kosul) => DenetimSurucu.Pump(View.Player, kosul, 20);

        public void Dispose()
        {
            _pencere.Close();
            DuzenleyiciKapanis.Birakti(View, 30);
        }
    }

    private static string Kaynak(string klasor, string ad)
    {
        var dosya = Path.Combine(klasor, ad);
        File.WriteAllBytes(dosya, new byte[16]);
        return dosya;
    }

    [Fact]
    public void TestKonagindaVarsayilanOkumaKapaliCizelgeIsaretsizAcilir()
    {
        Assert.True(EditorView.StripDisabled);
        var klasor = Klasor("kapali");
        try
        {
            var dosya = Kaynak(klasor, "a.mp4");
            AppHost.Run(() =>
            {
                using var sahne = new Sahne(klasor);
                sahne.Ac(dosya);

                Assert.True(sahne.View.KeyframeLoad.IsCompletedSuccessfully);
                Assert.Empty(sahne.View.TimelineView.Keyframes);
                Assert.Empty(sahne.View.TimelineView.KeyframeXs());
                Assert.Null(sahne.View.ThumbnailQueue);
                Assert.True(sahne.View.TimelineView.DesiredSize.Height > 0);
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void AnahtarKarelerArkadaOkunurCizelgeBeklemedenAcilirOkumaDuserseIsaretsizKalir()
    {
        var klasor = Klasor("okuma");
        try
        {
            var dosya = Kaynak(klasor, "a.mp4");
            var bozuk = Kaynak(klasor, "bozuk.mp4");
            AppHost.Run(() =>
            {
                var kapi = new TaskCompletionSource<IReadOnlyList<double>>(TaskCreationOptions.RunContinuationsAsynchronously);
                var okunan = new List<string>();
                using var sahne = new Sahne(klasor);
                sahne.View.KeyframeReader = (yol, _) =>
                {
                    lock (okunan) okunan.Add(yol);
                    if (yol == bozuk) throw new InvalidOperationException("ffprobe failed (1)");
                    return kapi.Task;
                };

                sahne.Ac(dosya);
                var cizelge = sahne.View.TimelineView;
                var beklerken = (Bitti: sahne.View.KeyframeLoad.IsCompleted, Centik: cizelge.KeyframeXs().Count, Sure: sahne.View.Model!.Duration, Yukseklik: cizelge.DesiredSize.Height);

                kapi.SetResult(new[] { 0.0, 120, 240, 360, 480 });
                sahne.Pompala(() => sahne.View.KeyframeLoad.IsCompleted);
                var gelen = cizelge.Keyframes.ToArray();
                var centik = cizelge.KeyframeXs().ToArray();
                var beklenen = gelen.Select(cizelge.TimeToX).ToArray();

                sahne.Ac(bozuk);
                sahne.Pompala(() => sahne.View.KeyframeLoad.IsCompleted);

                Assert.False(beklerken.Bitti);
                Assert.Equal(0, beklerken.Centik);
                Assert.Equal(S(600), beklerken.Sure);
                Assert.True(beklerken.Yukseklik > 0);
                Assert.Equal(new[] { S(0), S(120), S(240), S(360), S(480) }, gelen);
                Assert.Equal(beklenen, centik);
                Assert.Equal(new[] { dosya, bozuk }, okunan);
                Assert.True(sahne.View.KeyframeLoad.IsCompletedSuccessfully);
                Assert.Empty(cizelge.Keyframes);
                Assert.Empty(cizelge.KeyframeXs());
                Assert.NotNull(sahne.View.Model);
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void KaynakDegisinceEskiAnahtarKareOkumasiIptalEdilirSonucuYazilmaz()
    {
        var klasor = Klasor("degisim");
        try
        {
            var a = Kaynak(klasor, "a.mp4");
            var b = Kaynak(klasor, "b.mp4");
            AppHost.Run(() =>
            {
                var kapi = new TaskCompletionSource<IReadOnlyList<double>>(TaskCreationOptions.RunContinuationsAsynchronously);
                CancellationToken eski = default;
                using var cagrildi = new ManualResetEventSlim();
                using var sahne = new Sahne(klasor);
                sahne.View.KeyframeReader = (yol, ct) =>
                {
                    if (yol != a) return Task.FromResult<IReadOnlyList<double>>(new[] { 7.0, 9.0 });
                    eski = ct;
                    cagrildi.Set();
                    return kapi.Task;
                };

                sahne.Ac(a);
                sahne.Pompala(() => cagrildi.IsSet);
                var ilk = sahne.View.KeyframeLoad;
                var acikken = eski.IsCancellationRequested;
                sahne.Ac(b);
                sahne.Pompala(() => sahne.View.KeyframeLoad.IsCompleted);
                var iptal = eski.IsCancellationRequested;
                kapi.SetResult(new[] { 1.0, 2.0, 3.0 });
                sahne.Pompala(() => ilk.IsCompleted);
                Dispatcher.UIThread.RunJobs();

                Assert.False(acikken);
                Assert.True(iptal);
                Assert.True(ilk.IsCompletedSuccessfully);
                Assert.Equal(new[] { S(7), S(9) }, sahne.View.TimelineView.Keyframes);
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void GorunumSeridiKaynaklaAcarKaynakDegisinceOnbellegiBosaltir()
    {
        var klasor = Klasor("gorunum");
        try
        {
            var a = Kaynak(klasor, "a.mp4");
            var b = Kaynak(klasor, "b.mp4");
            AppHost.Run(() =>
            {
                var kaynak = new SahteKaynak();
                using var sahne = new Sahne(klasor);
                sahne.View.ThumbnailSource = kaynak;

                sahne.Ac(a);
                var kuyruk = sahne.View.ThumbnailQueue!;
                BosalsinEsli(kuyruk);
                var cizelge = sahne.View.TimelineView;
                var kareler = cizelge.StripTiles();
                var ilk = (kuyruk.Source, kuyruk.Height, Hazir: kareler.Count(k => k.Ready), Okunan: kaynak.Cagrilar.Count, Oran: cizelge.ThumbnailAspect, Serit: cizelge.StripHeight);

                var kapi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                kaynak.Kapi = _ => kapi.Task;
                sahne.Ac(b);
                var degisince = (kuyruk.Source, Ayni: ReferenceEquals(kuyruk, sahne.View.ThumbnailQueue), Hazir: cizelge.StripTiles().Count(k => k.Ready));
                kapi.SetResult();
                BosalsinEsli(kuyruk);
                var ikinci = kaynak.Cagrilar.Skip(ilk.Okunan).ToArray();

                Assert.Equal(a, ilk.Source);
                Assert.Equal((int)Math.Ceiling(ilk.Serit), ilk.Height);
                Assert.Equal(2, ilk.Oran, 6);
                Assert.True(kareler.Count > 3);
                Assert.Equal(kareler.Count, ilk.Hazir);
                Assert.Equal(kareler.Count, ilk.Okunan);
                Assert.All(kareler, k => Assert.True(k.Ready));
                Assert.All(kaynak.Cagrilar.Take(ilk.Okunan), c => Assert.Equal(a, c.Kaynak));
                Assert.Equal(b, degisince.Source);
                Assert.True(degisince.Ayni);
                Assert.Equal(0, degisince.Hazir);
                Assert.All(cizelge.StripTiles(), k => Assert.True(k.Ready));
                Assert.Equal(kareler.Count, ikinci.Length);
                Assert.All(ikinci, c => Assert.Equal(b, c.Kaynak));
                Assert.Equal(kareler.Select(k => k.SourceTime), ikinci.Select(c => c.Tick));
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void UreticiArgumaniTekKareKucukBoyTekIsParcacigi()
    {
        var arguman = FfmpegThumbnailSource.Arguments(@"C:\v\a, ş.mp4", S(12.5), 32).ToArray();

        Assert.Equal("12.5", arguman[Array.IndexOf(arguman, "-ss") + 1]);
        Assert.True(Array.IndexOf(arguman, "-ss") < Array.IndexOf(arguman, "-i"));
        Assert.Equal(@"C:\v\a, ş.mp4", arguman[Array.IndexOf(arguman, "-i") + 1]);
        Assert.Equal("1", arguman[Array.IndexOf(arguman, "-frames:v") + 1]);
        Assert.Equal("scale=-2:32,format=rgb24", arguman[Array.IndexOf(arguman, "-vf") + 1]);
        Assert.Equal(2, arguman.Count(a => a == "-threads"));
        Assert.All(Enumerable.Range(0, arguman.Length).Where(i => arguman[i] == "-threads"), i => Assert.Equal("1", arguman[i + 1]));
        Assert.Contains("-an", arguman);
        Assert.Contains("-nostdin", arguman);
        Assert.Equal("pipe:1", arguman[^1]);
        Assert.Equal("0", FfmpegThumbnailSource.Arguments("a.mp4", -5, 32)[Array.IndexOf(arguman, "-ss") + 1]);
    }

    private static string Klip(string klasor)
    {
        var yol = Path.Combine(klasor, "klip, ş.mp4");
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, new[]
        {
            "-y", "-hide_banner", "-threads", "2", "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=24", "-t", "3",
            "-pix_fmt", "yuv420p", "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2",
            "-g", "12", "-keyint_min", "12", "-sc_threshold", "0", "-bf", "0", yol
        });
        Assert.True(kod == 0, $"ffmpeg klip uretemedi: {hata[Math.Max(0, hata.Length - 400)..]}");
        return yol;
    }

    [FfmpegFact]
    public async Task OnIkiKarelikGopKlibindeAnahtarKareZamanlariOkunur()
    {
        var klasor = Klasor("canli-gop");
        try
        {
            var klip = Klip(klasor);

            var okunan = EditExport.ParseKeyframes(await EditExportRunner.ProbeKeyframesAsync(klip, CancellationToken.None)).Keyframes;
            var tick = TimelineStrip.SourceTicks(okunan);
            var anlar = TimelineStrip.Keyframes(EditTimeline.FromSource(S(3)), tick, 0, S(3));

            Assert.Equal(new[] { S(0), S(0.5), S(1), S(1.5), S(2), S(2.5) }, tick);
            Assert.Equal(tick, anlar);
            await Assert.ThrowsAsync<InvalidOperationException>(() => EditExportRunner.ProbeKeyframesAsync(Path.Combine(klasor, "yok.mp4"), CancellationToken.None));
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [FfmpegFact]
    public async Task GercekKlipteKucukResimIstenenYukseklikteGelir()
    {
        var klasor = Klasor("canli-kare");
        try
        {
            var klip = Klip(klasor);
            var uretici = new FfmpegThumbnailSource(() => ToolLocator.Ffmpeg);
            var oncelik = ProcessPriorityClass.Normal;
            uretici.Started = surec => { try { oncelik = surec.PriorityClass; } catch (InvalidOperationException) { } };
            using var kuyruk = new ThumbnailQueue(uretici);
            kuyruk.Open(klip, 32);

            kuyruk.Request(new[] { S(1) });
            await Bosalsin(kuyruk);
            var yok = await uretici.ReadAsync(Path.Combine(klasor, "yok.mp4"), 0, 32, CancellationToken.None);

            Assert.True(kuyruk.TryGet(S(1), out var png));
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4));
            Assert.Equal(56, (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19]);
            Assert.Equal(32, (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);
            Assert.Equal(ProcessPriorityClass.BelowNormal, oncelik);
            Assert.Null(yok);
            Assert.Equal(new[] { Path.GetFileName(klip) }, Directory.GetFiles(klasor).Select(Path.GetFileName));
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [FfmpegFact]
    public async Task IptalEdilenKareSureciOldurur()
    {
        var klasor = Klasor("canli-iptal");
        try
        {
            var klip = Klip(klasor);
            using var iptal = new CancellationTokenSource();
            var pid = 0;
            var uretici = new FfmpegThumbnailSource(() => ToolLocator.Ffmpeg)
            {
                Started = surec =>
                {
                    pid = surec.Id;
                    iptal.Cancel();
                }
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => uretici.ReadAsync(klip, S(1), 32, iptal.Token));

            Assert.NotEqual(0, pid);
            var yasiyor = true;
            try
            {
                using var surec = Process.GetProcessById(pid);
                yasiyor = !surec.HasExited;
            }
            catch (ArgumentException)
            {
                yasiyor = false;
            }

            Assert.False(yasiyor);
        }
        finally
        {
            Kapat(klasor);
        }
    }
}
