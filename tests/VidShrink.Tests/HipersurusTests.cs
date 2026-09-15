using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Hipersürüşün ölçülebilir yanları. Süre ölçmüyor — bu makinede süre ölçmek
/// <c>tools/acilis-hizi</c>'nin işi; burada ölçülen, kazancı üreten kararların kaynakta
/// duruyor olması: başlatıcının argümanda dosya varken uygulamayı önce doğurması, sıfır
/// noktasının çocuk sürece geçmesi, çizim saatinin ilk kareyi beklememesi.
/// </summary>
public class HipersurusTests
{
    private static string Launcher(string file) =>
        File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.Launcher", file));

    private static string Player(string file) =>
        File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Playback", file));

    /// <summary>
    /// Hızlı turda uygulama, bakım işlerinin <b>önünde</b> doğuyor. Ölçülen şey sıra:
    /// <c>StartApp</c> çağrısı dosyada <c>Updater.Run</c>'dan önce geçiyor ve o kolda
    /// bekleyen dosyaların taşınması hiç çağrılmıyor.
    /// </summary>
    [Fact]
    public void ArgumandaDosyaVarkenUygulamaOnceDoguyor()
    {
        var code = Launcher("Program.cs").Replace("\r\n", "\n");

        var kol = code.IndexOf("if (!updateNow && args.Length > 0 && File.Exists(args[0]))", StringComparison.Ordinal);
        Assert.True(kol > 0, "hızlı tur kolu bulunmalı");

        var govde = code[kol..code.IndexOf("\n            return 0;\n        }", kol, StringComparison.Ordinal)];
        Assert.Contains("StartApp(executable", govde);
        Assert.DoesNotContain("ResumePending", govde);
        Assert.True(
            govde.IndexOf("StartApp(executable", StringComparison.Ordinal) <
            govde.IndexOf("Updater.Run", StringComparison.Ordinal),
            "güncelleme uygulamadan sonra koşmalı");
    }

    /// <summary>Sıfır noktası çocuğa geçiyor: iki sürecin satırları aynı eksende okunuyor.</summary>
    [Fact]
    public void SifirNoktasiCocugaGeciyor()
    {
        Assert.Contains(
            "start.Environment[AcilisIzi.SifirDegiskeni] = AcilisIzi.SifirIsareti",
            Launcher("Program.cs"));
        Assert.Contains("VIDSHRINK_ACILIS_T0", Launcher("AcilisIzi.cs"));
        Assert.Contains(
            "VIDSHRINK_ACILIS_T0",
            File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.AcilisIzi.cs")));
    }

    /// <summary>
    /// Çizim saati ilk kareyi bir tam kare beklemiyor: saat sıkı adımla başlıyor, ilk kare
    /// düşünce olağan adıma dönüyor ve bir kare de saat kurulur kurulmaz deneniyor.
    /// </summary>
    [Fact]
    public void IlkKareSaatiBeklemiyor()
    {
        var code = Player("PlayerView.axaml.cs").Replace("\r\n", "\n");

        Assert.Contains("_render.Interval = TimeSpan.FromMilliseconds(FirstFrameMs);\n        _render.Start();\n        RenderLatest();", code);
        Assert.Contains("saat.Interval = TimeSpan.FromMilliseconds(RenderFrameMs);", code);
    }

    /// <summary>Motor artık arayüz iş parçacığında kurulmuyor.</summary>
    [Fact]
    public void MotorArayuzIsParcacigindaKurulmuyor()
    {
        var code = Player("PlayerView.axaml.cs");

        Assert.Contains("await Task.Run(EngineFactory).ConfigureAwait(true)", code);
        Assert.DoesNotContain("engine = EngineFactory();", code);
    }

    /// <summary>
    /// Son kullanılanların yazımı ve ayar okumaları oynatmanın arkasına düştü:
    /// <c>AfterOpen</c> artık <c>TogglePlay</c>'den sonra çağrılıyor.
    /// </summary>
    [Fact]
    public void AyarYazimlariOynatmanınArkasinda()
    {
        var code = Player("PlayerView.axaml.cs").Replace("\r\n", "\n");

        Assert.Contains("if (!_playing) TogglePlay();\n        AfterOpen(path, engine);", code);
    }
}

/// <summary>
/// Güncellemenin hızı iki yerden geliyor: her dosyanın kaç gidiş dönüş ettiğinden ve kaç
/// dosyanın aynı anda indiğinden. Buradaki ölçü birincisini sayıyor — sayan kaynak gerçek
/// bir zip okuyor, kaynak metne bakmıyor.
/// </summary>
public class GuncellemeHiziTests
{
    /// <summary>İstek sayan aralık kaynağı; hat yok, dosya var.</summary>
    private sealed class SayanKaynak : IRangeSource
    {
        private readonly byte[] _bytes;
        private int _reads;

        public SayanKaynak(byte[] bytes) => _bytes = bytes;

        public int Reads => Volatile.Read(ref _reads);

        public Task<long> LengthAsync(CancellationToken cancellationToken) =>
            Task.FromResult((long)_bytes.Length);

        public Task<byte[]> ReadAsync(long offset, int length, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _reads);
            var count = (int)Math.Min(length, _bytes.LongLength - offset);
            return Task.FromResult(_bytes.AsSpan((int)offset, Math.Max(0, count)).ToArray());
        }
    }

    private static byte[] Arsiv(IReadOnlyList<(string Ad, string Icerik)> dosyalar)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (ad, icerik) in dosyalar)
            {
                var entry = zip.CreateEntry(ad, CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(icerik);
            }
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Bir dosya bir istek. Eskiden yerel başlık ayrı bir aralık isteğiydi ve her dosya
    /// iki tur ediyordu; 375 dosyalık ölçülmüş bir farkta bu, inen bayttan bağımsız olarak
    /// 375 fazla gidiş dönüş demekti.
    /// </summary>
    [Fact]
    public async Task DosyaBasinaTekIstek()
    {
        var bytes = Arsiv(new[] { ("a/bir.txt", new string('x', 4096)), ("a/iki.txt", new string('y', 2048)) });
        var kaynak = new SayanKaynak(bytes);
        var zip = await RemoteZip.OpenAsync(kaynak, CancellationToken.None);
        var acilis = kaynak.Reads;

        var icerik = await zip.ExtractAsync("a/bir.txt", CancellationToken.None);

        Assert.Equal(new string('x', 4096), System.Text.Encoding.UTF8.GetString(icerik));
        Assert.Equal(1, kaynak.Reads - acilis);
    }

    /// <summary>Son girdinin sınırı merkezî dizin: o da tek istekte iniyor.</summary>
    [Fact]
    public async Task SonGirdiDeTekIstek()
    {
        var bytes = Arsiv(new[] { ("a/bir.txt", new string('x', 1024)), ("a/son.txt", new string('z', 3072)) });
        var kaynak = new SayanKaynak(bytes);
        var zip = await RemoteZip.OpenAsync(kaynak, CancellationToken.None);
        var acilis = kaynak.Reads;

        var icerik = await zip.ExtractAsync("a/son.txt", CancellationToken.None);

        Assert.Equal(new string('z', 3072), System.Text.Encoding.UTF8.GetString(icerik));
        Assert.Equal(1, kaynak.Reads - acilis);
    }

    /// <summary>Negatif kontrol: olmayan girdi hâlâ bulunamıyor ve hiç istek etmiyor.</summary>
    [Fact]
    public async Task OlmayanGirdiIstekEtmiyor()
    {
        var kaynak = new SayanKaynak(Arsiv(new[] { ("a/bir.txt", "abc") }));
        var zip = await RemoteZip.OpenAsync(kaynak, CancellationToken.None);
        var acilis = kaynak.Reads;

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => zip.ExtractAsync("a/yok.txt", CancellationToken.None));
        Assert.Equal(acilis, kaynak.Reads);
    }

    /// <summary>Dosyalar tek tek değil şeritler halinde iniyor.</summary>
    [Fact]
    public void IndirmeSeritliKosuyor()
    {
        var code = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.Launcher", "Updater.cs"));

        Assert.Contains("MaxDegreeOfParallelism = Lanes", code);
        Assert.Contains("Interlocked.Increment(ref done)", code);
        Assert.DoesNotContain("foreach (var file in changed)", code);
    }
}
