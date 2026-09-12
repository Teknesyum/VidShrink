using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class AracKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "dalga4b");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));

    internal static string Gecici(string ad)
    {
        var path = Path.Combine(Folder, "gecici", ad + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    internal static double Medyan(IReadOnlyList<double> degerler)
    {
        var sirali = degerler.OrderBy(d => d).ToList();
        if (sirali.Count == 0) return double.NaN;
        return sirali.Count % 2 == 1
            ? sirali[sirali.Count / 2]
            : (sirali[sirali.Count / 2 - 1] + sirali[sirali.Count / 2]) / 2;
    }

    internal static double Yuzde95(IReadOnlyList<double> degerler)
    {
        var sirali = degerler.OrderBy(d => d).ToList();
        if (sirali.Count == 0) return double.NaN;
        var index = (int)Math.Ceiling(sirali.Count * 0.95) - 1;
        return sirali[Math.Clamp(index, 0, sirali.Count - 1)];
    }

    internal static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

/// <summary>
/// Kucuk resim icin elle surulen motor. Gercek libmpv olmadan da gosterimin kendisi
/// olculebilsin diye var: CI'da da kosar. <c>basarili=false</c> negatif kontroldur.
/// </summary>
internal sealed class SahteOnizleme : IPlaybackEngine
{
    private readonly bool _basarili;
    private readonly byte[] _piksel = new byte[4 * 16 * 16];

    internal SahteOnizleme(bool basarili = true)
    {
        _basarili = basarili;
        for (var i = 0; i < _piksel.Length; i++) _piksel[i] = (byte)(i % 251);
    }

    internal int AramaSayisi { get; private set; }

    internal SeekPrecision SonHassasiyet { get; private set; } = SeekPrecision.Exact;

    internal PlaybackOptions? Secenekler { get; init; }

    public string Name => "sahte-onizleme";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 25;

    public bool HasAudio => false;

    public bool IsPaused { get; private set; }

    public bool EndReached => false;

    public double PositionSeconds { get; private set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered { get; private set; }

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        IsOpen = true;
        return Task.CompletedTask;
    }

    public void Play() => IsPaused = false;

    public void Pause() => IsPaused = true;

    public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        AramaSayisi++;
        SonHassasiyet = precision;
        PositionSeconds = seconds;
        FramesRendered++;
        return Task.FromResult(new SeekResult(_basarili ? SeekOutcome.Shown : SeekOutcome.Failed, 1));
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy)
    {
        if (!_basarili || seen == FramesRendered) return false;
        seen = FramesRendered;
        var handle = GCHandle.Alloc(_piksel, GCHandleType.Pinned);
        try
        {
            copy(handle.AddrOfPinnedObject(), 16, 16, 64);
        }
        finally
        {
            handle.Free();
        }

        return true;
    }

    public void Dispose() => IsOpen = false;
}

/// <summary>
/// Yerel HTTP sunucusu. <c>HttpListener</c> yerine <c>TcpListener</c>: geri donus
/// arayuzunde onek ayirtmak yonetici izni isteyebiliyor, ham yuva istemiyor.
/// Range istekleri desteklenir, cunku libmpv dosyayi parca parca ister.
/// </summary>
internal sealed class MiniSunucu : IDisposable
{
    private readonly TcpListener _yuva;
    private readonly byte[] _govde;
    private readonly CancellationTokenSource _iptal = new();

    internal MiniSunucu(string dosya)
    {
        _govde = File.ReadAllBytes(dosya);
        _yuva = new TcpListener(IPAddress.Loopback, 0);
        _yuva.Start();
        _ = Task.Run(DinleAsync);
    }

    internal int Istekler;

    internal string Adres => FormattableString.Invariant(
        $"http://127.0.0.1:{((IPEndPoint)_yuva.LocalEndpoint).Port}/klip.mp4");

    private async Task DinleAsync()
    {
        while (!_iptal.IsCancellationRequested)
        {
            TcpClient istemci;
            try
            {
                istemci = await _yuva.AcceptTcpClientAsync(_iptal.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = Task.Run(() => YanitlaAsync(istemci));
        }
    }

    private async Task YanitlaAsync(TcpClient istemci)
    {
        using (istemci)
        {
            try
            {
                using var akis = istemci.GetStream();
                var tampon = new byte[8192];
                var okunan = await akis.ReadAsync(tampon, _iptal.Token);
                if (okunan <= 0) return;
                Interlocked.Increment(ref Istekler);

                var istek = Encoding.ASCII.GetString(tampon, 0, okunan);
                var (bas, son) = Aralik(istek);
                var uzunluk = son - bas + 1;
                var basliklar = new StringBuilder();
                basliklar.Append(bas == 0 && uzunluk == _govde.Length ? "HTTP/1.1 200 OK\r\n" : "HTTP/1.1 206 Partial Content\r\n");
                basliklar.Append("Content-Type: video/mp4\r\n");
                basliklar.Append(CultureInfo.InvariantCulture, $"Content-Length: {uzunluk}\r\n");
                basliklar.Append("Accept-Ranges: bytes\r\n");
                if (bas != 0 || uzunluk != _govde.Length)
                    basliklar.Append(CultureInfo.InvariantCulture, $"Content-Range: bytes {bas}-{son}/{_govde.Length}\r\n");
                basliklar.Append("Connection: close\r\n\r\n");

                await akis.WriteAsync(Encoding.ASCII.GetBytes(basliklar.ToString()), _iptal.Token);
                if (!istek.StartsWith("HEAD", StringComparison.Ordinal))
                    await akis.WriteAsync(_govde.AsMemory(bas, uzunluk), _iptal.Token);
                await akis.FlushAsync(_iptal.Token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
            {
            }
        }
    }

    private (int Bas, int Son) Aralik(string istek)
    {
        var at = istek.IndexOf("Range: bytes=", StringComparison.OrdinalIgnoreCase);
        if (at < 0) return (0, _govde.Length - 1);

        var satir = istek[(at + "Range: bytes=".Length)..];
        var sonu = satir.IndexOf('\r');
        if (sonu >= 0) satir = satir[..sonu];
        var parcalar = satir.Split('-');
        if (!int.TryParse(parcalar[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bas)) return (0, _govde.Length - 1);
        var son = parcalar.Length > 1 && int.TryParse(parcalar[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bitis)
            ? bitis
            : _govde.Length - 1;
        bas = Math.Clamp(bas, 0, _govde.Length - 1);
        son = Math.Clamp(son, bas, _govde.Length - 1);
        return (bas, son);
    }

    public void Dispose()
    {
        _iptal.Cancel();
        try { _yuva.Stop(); } catch (SocketException) { }
        _iptal.Dispose();
    }
}

public sealed class OynaticiAracTests
{
    private static string Ayar(string ad) => Path.Combine(AracKanit.Gecici(ad), "history.json");

    [Fact]
    public void KucukResimAnahtarKaredenGosterilirBasarisizAramaGostermez()
    {
        var clip = MotorKlipleri.Kucuk;
        var history = Ayar("kucukresim");

        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var view = DenetimSurucu.Ac(clip, out var window, history);

            var motor = new SahteOnizleme();
            view.PreviewFactory = () => motor;
            var sureler = new List<double>();
            foreach (var an in new[] { 2.0, 6.0, 11.0, 17.0, 23.0 })
            {
                var is_ = view.ShowThumbnailAsync(an);
                DenetimSurucu.Pump(view, () => is_.IsCompleted, 10);
                sureler.Add(is_.GetAwaiter().GetResult());
                body.AppendLine($"an {AracKanit.N(an)} -> {AracKanit.N(sureler[^1])} ms, gorunur {view.ThumbnailVisible}");
            }

            var resim = view.FindControl<Image>("ThumbImage");
            var gorunur = view.ThumbnailVisible;
            var kaynakVar = resim?.Source is not null;
            var hassasiyet = motor.SonHassasiyet;
            var aramalar = motor.AramaSayisi;
            var anaKonum = view.Seek.Target;

            view.HideThumbnail();
            var gizlendi = view.ThumbnailVisible;

            var bozuk = new SahteOnizleme(false);
            view.PreviewFactory = () => bozuk;
            view.Close();
            var yeniden = view.OpenAsync(clip);
            DenetimSurucu.Pump(view, () => yeniden.IsCompleted, 20);
            yeniden.GetAwaiter().GetResult();
            var kotu = view.ShowThumbnailAsync(5);
            DenetimSurucu.Pump(view, () => kotu.IsCompleted, 10);
            var kotuSure = kotu.GetAwaiter().GetResult();
            var kotuGorunur = view.ThumbnailVisible;

            body.AppendLine($"gorunur {gorunur}, kaynak {kaynakVar}, hassasiyet {hassasiyet}, arama {aramalar}");
            body.AppendLine($"ana motor konumu {AracKanit.N(anaKonum)} (kucuk resim ana motoru oynatmaz)");
            body.AppendLine($"gizlendi -> gorunur {gizlendi}");
            body.AppendLine($"NEGATIF basarisiz arama -> sure {kotuSure}, gorunur {kotuGorunur}");
            body.AppendLine($"medyan {AracKanit.N(AracKanit.Medyan(sureler))} ms, p95 {AracKanit.N(AracKanit.Yuzde95(sureler))} ms");

            view.Close();
            window.Close();
            return (body.ToString(), gorunur, kaynakVar, hassasiyet, aramalar, gizlendi, kotuSure, kotuGorunur);
        });

        AracKanit.Write("kucuk-resim-gosterim.txt", rapor.Item1);
        Assert.True(rapor.gorunur, "kucuk resim gorunmedi");
        Assert.True(rapor.kaynakVar, "kucuk resmin kaynagi bos");
        Assert.Equal(SeekPrecision.Keyframe, rapor.hassasiyet);
        Assert.Equal(5, rapor.aramalar);
        Assert.False(rapor.gizlendi, "fare cikinca kucuk resim gizlenmedi");
        Assert.True(double.IsNaN(rapor.kotuSure), "basarisiz arama sure dondurdu");
        Assert.False(rapor.kotuGorunur, "basarisiz aramada kucuk resim gosterildi");
    }

    [HedefMakineFact]
    public void KucukResimUcYuzMilisaniyeIcindeGelir()
    {
        var clip = MotorKlipleri.Kucuk;
        var history = Ayar("kucukresim-esik");

        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var view = DenetimSurucu.Ac(clip, out var window, history);
            DenetimSurucu.Duraklat(view);

            var isinma = view.ShowThumbnailAsync(2);
            DenetimSurucu.Pump(view, () => isinma.IsCompleted, 30);
            isinma.GetAwaiter().GetResult();

            var sureler = new List<double>();
            foreach (var an in new[] { 4.0, 8.0, 12.0, 16.0, 20.0, 22.0, 6.0, 14.0, 18.0, 10.0 })
            {
                var is_ = view.ShowThumbnailAsync(an);
                DenetimSurucu.Pump(view, () => is_.IsCompleted, 20);
                var sure = is_.GetAwaiter().GetResult();
                if (!double.IsNaN(sure)) sureler.Add(sure);
                body.AppendLine($"an {AracKanit.N(an)} -> {AracKanit.N(sure)} ms");
            }

            var medyan = AracKanit.Medyan(sureler);
            var p95 = AracKanit.Yuzde95(sureler);
            body.AppendLine($"olcum {sureler.Count}, medyan {AracKanit.N(medyan)} ms, p95 {AracKanit.N(p95)} ms, esik 300 ms");

            view.Close();
            window.Close();
            return (body.ToString(), medyan, p95, sureler.Count);
        });

        AracKanit.Write("kucuk-resim-sure.txt", rapor.Item1);
        Assert.True(rapor.Item4 >= 8, $"yeterli olcum yok: {rapor.Item4}");
        Assert.True(rapor.medyan <= 300, $"kucuk resim medyani {AracKanit.N(rapor.medyan)} ms");
        Assert.True(rapor.p95 <= 300, $"kucuk resim p95 {AracKanit.N(rapor.p95)} ms");
    }

    [Fact]
    public async Task KlipVeGifUretilirGecersizAralikUretmez()
    {
        var clip = MotorKlipleri.Kucuk;
        var klasor = AracKanit.Gecici("klip");
        var body = new StringBuilder();

        var klipHedef = Path.Combine(klasor, "klip.mp4");
        var klipSonuc = await ClipExport.RunAsync(new ClipRequest(clip, klipHedef, 5, 3, ClipKind.Video));
        var klipSure = double.Parse(GorunumKanit.Probe(klipHedef, "", "duration"), CultureInfo.InvariantCulture);
        var klipBoyut = new FileInfo(klipHedef).Length;
        body.AppendLine($"klip {Path.GetFileName(klipHedef)} -> ok {klipSonuc.Ok}, sure {AracKanit.N(klipSure)} sn, {klipBoyut} bayt");
        body.AppendLine("  istenen 3 sn; akis kopyasi anahtar kareye hizalar, kaynak -g 60 @30 fps = 2 sn aralik, ust sinir 3+2 sn");

        var gifHedef = Path.Combine(klasor, "klip.gif");
        var gifSonuc = await ClipExport.RunAsync(new ClipRequest(clip, gifHedef, 5, 2, ClipKind.Gif, 10, 160));
        var gifOlcu = GorunumKanit.Boyut(gifHedef);
        var gifBoyut = new FileInfo(gifHedef).Length;
        var gifSure = double.TryParse(GorunumKanit.Probe(gifHedef, "", "duration"), NumberStyles.Float, CultureInfo.InvariantCulture, out var gs) ? gs : double.NaN;
        body.AppendLine($"gif {Path.GetFileName(gifHedef)} -> ok {gifSonuc.Ok}, {gifOlcu.Genislik}x{gifOlcu.Yukseklik}, sure {AracKanit.N(gifSure)} sn, {gifBoyut} bayt");

        var kotuHedef = Path.Combine(klasor, "olmaz.mp4");
        var kotuSonuc = await ClipExport.RunAsync(new ClipRequest(clip, kotuHedef, 5, 0, ClipKind.Video));
        body.AppendLine($"NEGATIF sifir sureli aralik -> ok {kotuSonuc.Ok}, hata '{kotuSonuc.Error}', dosya {File.Exists(kotuHedef)}");

        var yokSonuc = await ClipExport.RunAsync(new ClipRequest(Path.Combine(klasor, "yok.mp4"), Path.Combine(klasor, "yok-cikti.mp4"), 0, 1, ClipKind.Video));
        body.AppendLine($"NEGATIF olmayan kaynak -> ok {yokSonuc.Ok}, dosya {File.Exists(Path.Combine(klasor, "yok-cikti.mp4"))}");

        var isaretli = ClipExport.Range(4, 9, 20, 10, 25);
        var isaretsiz = ClipExport.Range(double.NaN, double.NaN, 20, 10, 25);
        body.AppendLine($"A-B 4..9 -> bas {AracKanit.N(isaretli.Start)} sure {AracKanit.N(isaretli.Duration)}");
        body.AppendLine($"isaretsiz 20. sn -> bas {AracKanit.N(isaretsiz.Start)} sure {AracKanit.N(isaretsiz.Duration)}");

        AracKanit.Write("klip-gif.txt", body.ToString());

        Assert.True(klipSonuc.Ok, klipSonuc.Error);
        Assert.InRange(klipSure, 2.9, 5.1);
        Assert.True(klipBoyut > 0);
        Assert.True(gifSonuc.Ok, gifSonuc.Error);
        Assert.Equal(160, gifOlcu.Genislik);
        Assert.Equal(90, gifOlcu.Yukseklik);
        Assert.InRange(gifSure, 1.7, 2.3);
        Assert.True(gifBoyut > 0);
        Assert.False(kotuSonuc.Ok);
        Assert.False(File.Exists(kotuHedef));
        Assert.False(yokSonuc.Ok);
        Assert.Equal((4d, 5d), (isaretli.Start, isaretli.Duration));
        Assert.Equal((20d, 5d), (isaretsiz.Start, isaretsiz.Duration));
    }

    [Fact]
    public void MiniModGirisCikisOncekiPencereyiGeriKor()
    {
        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var view = new PlayerView();
            var window = new Window
            {
                Width = 900,
                Height = 600,
                WindowDecorations = WindowDecorations.Full,
                Topmost = false,
                Content = view
            };
            window.Show();

            var oncekiGenislik = window.Width;
            var oncekiYukseklik = window.Height;
            var oncekiCerceve = window.WindowDecorations;
            var oncekiUstte = window.Topmost;
            body.AppendLine($"once {oncekiGenislik}x{oncekiYukseklik}, cerceve {oncekiCerceve}, ustte {oncekiUstte}");

            GirdiSurucu.Key(view, Key.M, KeyModifiers.Control);
            var miniGenislik = window.Width;
            var miniYukseklik = window.Height;
            var miniCerceve = window.WindowDecorations;
            var miniUstte = window.Topmost;
            var miniDurum = view.IsMiniMode;
            body.AppendLine($"mini {miniGenislik}x{miniYukseklik}, cerceve {miniCerceve}, ustte {miniUstte}, iz {view.Trace[^1]}");

            GirdiSurucu.Key(view, Key.M, KeyModifiers.Control);
            var geriGenislik = window.Width;
            var geriYukseklik = window.Height;
            var geriCerceve = window.WindowDecorations;
            var geriUstte = window.Topmost;
            var geriDurum = view.IsMiniMode;
            body.AppendLine($"sonra {geriGenislik}x{geriYukseklik}, cerceve {geriCerceve}, ustte {geriUstte}, iz {view.Trace[^1]}");

            var bosCikis = new MiniModeSwitch().Leave();
            body.AppendLine($"NEGATIF hic girmeden cikis -> {(bosCikis is null ? "null" : "deger")}");

            window.Close();
            return (body.ToString(), oncekiGenislik, oncekiYukseklik, oncekiCerceve, oncekiUstte,
                miniGenislik, miniCerceve, miniUstte, miniDurum,
                geriGenislik, geriYukseklik, geriCerceve, geriUstte, geriDurum, bosCikis);
        });

        AracKanit.Write("mini-mod.txt", rapor.Item1);
        Assert.True(rapor.miniDurum, "mini moda girilmedi");
        Assert.Equal(ToolsOptions.DefaultMiniWidth, rapor.miniGenislik);
        Assert.Equal(WindowDecorations.None, rapor.miniCerceve);
        Assert.True(rapor.miniUstte, "mini modda pencere ustte degil");
        Assert.False(rapor.geriDurum, "mini moddan cikilmadi");
        Assert.Equal(rapor.oncekiGenislik, rapor.geriGenislik);
        Assert.Equal(rapor.oncekiYukseklik, rapor.geriYukseklik);
        Assert.Equal(rapor.oncekiCerceve, rapor.geriCerceve);
        Assert.Equal(rapor.oncekiUstte, rapor.geriUstte);
        Assert.Null(rapor.bosCikis);
    }

    [Fact]
    public void HttpAdresiAcilirYerelYolAdresSayilmaz()
    {
        var clip = MotorKlipleri.Kucuk;
        var history = Ayar("adres");
        using var sunucu = new MiniSunucu(clip);
        var adres = sunucu.Adres;

        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var view = new PlayerView
            {
                HistoryPath = () => history,
                EngineFactory = () =>
                {
                    var engine = new MpvEngine();
                    engine.SetProperty("ao", "null");
                    return engine;
                }
            };
            var window = new Window { Width = 640, Height = 480, Content = view };

            var kabul = view.OpenAddress(adres);
            DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted, 30);
            DenetimSurucu.Pump(view, () => view.Engine is { IsOpen: true }, 10);

            var acildi = view.Engine is { IsOpen: true };
            var yol = view.LoadedPath;
            var sonAdres = view.Tools.LastUrl;
            var sonAcilanlar = view.Recent.Items.Count;
            body.AppendLine($"adres {adres} -> kabul {kabul}, acildi {acildi}, yol {yol}");
            body.AppendLine($"istek sayisi {sunucu.Istekler}, ayarda saklanan adres {sonAdres}");
            body.AppendLine($"son acilanlar {sonAcilanlar} (adres listeye girmez)");

            var yerel = view.OpenAddress(clip);
            var ftp = view.OpenAddress("ftp://ornek/klip.mp4");
            var bos = view.OpenAddress("   ");
            body.AppendLine($"NEGATIF yerel yol -> {yerel}, ftp -> {ftp}, bos -> {bos}");
            body.AppendLine($"MpvEngine.Target adres -> {MpvEngine.Target(adres)}");
            body.AppendLine($"MpvEngine.Target yerel -> {(MpvEngine.Target(clip) == Path.GetFullPath(clip) ? "tam yol" : "degisti")}");

            view.Close();
            window.Close();
            return (body.ToString(), kabul, acildi, yol, sonAdres, sonAcilanlar, yerel, ftp, bos);
        });

        AracKanit.Write("adres-ac.txt", rapor.Item1);
        Assert.True(rapor.kabul, "adres kabul edilmedi");
        Assert.True(rapor.acildi, "adres acilmadi");
        Assert.Equal(adres, rapor.yol);
        Assert.Equal(adres, rapor.sonAdres);
        Assert.Equal(0, rapor.sonAcilanlar);
        Assert.False(rapor.yerel, "yerel yol adres sayildi");
        Assert.False(rapor.ftp, "ftp adres sayildi");
        Assert.False(rapor.bos, "bos metin adres sayildi");
        Assert.Equal(adres, MpvEngine.Target(adres));
        Assert.True(sunucu.Istekler > 0, "yerel sunucuya istek gelmedi");
    }

    [Fact]
    public void AracAyarlariYazilirOkunurSifirlanir()
    {
        var klasor = AracKanit.Gecici("ayar");
        var dosya = Path.Combine(klasor, ToolsOptions.FileName);
        var body = new StringBuilder();

        var ayar = new ToolsOptions();
        ayar.UseClipSeconds(7.5);
        ayar.UseGifFps(15);
        ayar.UseGifWidth(241);
        ayar.UseMiniSize(640, 360);
        ayar.UseUrl("  https://ornek/yayin.m3u8  ");
        ayar.Save(dosya);
        body.AppendLine($"yazildi: {ayar.Describe()}");
        body.AppendLine(File.ReadAllText(dosya));

        var okunan = ToolsOptions.Load(dosya);
        var okunanSure = okunan.ClipSeconds;
        var okunanFps = okunan.GifFps;
        var okunanGenislik = okunan.GifWidth;
        var okunanMiniGenislik = okunan.MiniWidth;
        var okunanMiniYukseklik = okunan.MiniHeight;
        var okunanUrl = okunan.LastUrl;
        body.AppendLine($"okundu: {okunan.Describe()}");

        okunan.Reset();
        body.AppendLine($"sifirlandi: {okunan.Describe()}");

        var sinir = new ToolsOptions();
        sinir.UseClipSeconds(double.NaN);
        sinir.UseGifFps(999);
        sinir.UseGifWidth(1);
        body.AppendLine($"NEGATIF sinir disi degerler -> {sinir.Describe()}");

        var bozukDosya = Path.Combine(klasor, "bozuk.json");
        File.WriteAllText(bozukDosya, "{ bu json degil", new UTF8Encoding(false));
        var bozuk = ToolsOptions.Load(bozukDosya);
        var yokDosya = ToolsOptions.Load(Path.Combine(klasor, "yok.json"));
        body.AppendLine($"NEGATIF bozuk dosya -> {bozuk.Describe()}");
        body.AppendLine($"NEGATIF olmayan dosya -> {yokDosya.Describe()}");

        AracKanit.Write("arac-ayarlari.txt", body.ToString());

        Assert.Equal(7.5, okunanSure);
        Assert.Equal(15, okunanFps);
        Assert.Equal(242, okunanGenislik);
        Assert.Equal(640, okunanMiniGenislik);
        Assert.Equal(360, okunanMiniYukseklik);
        Assert.Equal("https://ornek/yayin.m3u8", okunanUrl);

        Assert.Equal(ToolsOptions.DefaultClipSeconds, okunan.ClipSeconds);
        Assert.Equal(ToolsOptions.DefaultGifFps, okunan.GifFps);
        Assert.Equal(ToolsOptions.DefaultGifWidth, okunan.GifWidth);
        Assert.Equal("", okunan.LastUrl);

        Assert.Equal(ToolsOptions.DefaultClipSeconds, sinir.ClipSeconds);
        Assert.Equal(ToolsOptions.MaximumGifFps, sinir.GifFps);
        Assert.Equal(ToolsOptions.MinimumGifWidth, sinir.GifWidth);

        Assert.Equal(ToolsOptions.DefaultClipSeconds, bozuk.ClipSeconds);
        Assert.Equal("", bozuk.LastUrl);
        Assert.Equal(ToolsOptions.DefaultGifWidth, yokDosya.GifWidth);
    }

    [Fact]
    public void AraclarAltMenusuDortSatirTasirVeDilDegisincePesindenGelir()
    {
        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var view = GirdiSurucu.Kur(out var window);
            var basliklar = new List<string>();

            foreach (var dil in new[] { "en", "tr" })
            {
                Strings.Use(dil);
                var menu = view.BuildMenu().Items.OfType<MenuItem>().ToList();
                var araclar = menu.Single(item => (string?)item.Header == Strings.Get("player.tools.menu"));
                var satirlar = araclar.Items.OfType<MenuItem>().ToList();
                basliklar.Add((string)araclar.Header!);
                body.AppendLine($"[{dil}] {araclar.Header}: {string.Join(" | ", satirlar.Select(s => s.Header))}");
                Assert.Equal(4, satirlar.Count);
                Assert.Equal(menu.Count - 1, menu.IndexOf(araclar));
                    Assert.All(satirlar, satir => Assert.NotNull(satir.Tag));
            }

            Strings.Use("en");
            window.Close();
            return (body.ToString(), basliklar);
        });

        AracKanit.Write("araclar-menusu.txt", rapor.Item1);
        Assert.Equal(2, rapor.basliklar.Distinct().Count());
    }
}
