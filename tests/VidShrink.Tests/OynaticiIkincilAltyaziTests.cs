using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

internal static class IkincilKanit
{
    internal const string Birinci = "Birinci satir";
    internal const string Ikinci = "Second line";

    internal static string Kok => Path.Combine(GirdiKanit.Root, ".calisma", "ikincil-altyazi");

    internal static string Temiz(string ad)
    {
        var yol = Path.Combine(Kok, ad);
        if (Directory.Exists(yol)) Directory.Delete(yol, true);
        Directory.CreateDirectory(yol);
        return yol;
    }

    internal static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Kok, adlar);

    private static string Srt(string metin) => "1\r\n00:00:00,200 --> 00:00:02,800\r\n" + metin + "\r\n\r\n";

    /// <summary>İki gömülü srt izli, 3 sn, 160x90 mkv; ses yok.</summary>
    internal static string Klip(string kok)
    {
        Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli; bu test ffmpeg olmadan kirmizi kalir");
        var bir = Path.Combine(kok, "bir.srt");
        var iki = Path.Combine(kok, "iki.srt");
        File.WriteAllText(bir, Srt(Birinci), new UTF8Encoding(false));
        File.WriteAllText(iki, Srt(Ikinci), new UTF8Encoding(false));

        var klip = Path.Combine(kok, "iki-altyazi.mkv");
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "-y", "-hide_banner",
            "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=25:duration=3",
            "-i", bir, "-i", iki,
            "-map", "0:v", "-map", "1:s", "-map", "2:s",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-threads", "2",
            "-c:s", "srt", "-metadata:s:s:0", "language=tur", "-metadata:s:s:1", "language=eng",
            klip
        }) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("iki altyazili klip 60 sn icinde uretilmedi");
        }

        stdout.GetAwaiter().GetResult();
        var log = stderr.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, $"ffmpeg klibi uretemedi (kod {process.ExitCode}): {log[Math.Max(0, log.Length - 600)..]}");
        return klip;
    }

    internal static async Task<bool> Bekle(Func<bool> kosul, double saniye = 5)
    {
        var saat = Stopwatch.StartNew();
        while (saat.Elapsed.TotalSeconds < saniye)
        {
            if (kosul()) return true;
            await Task.Delay(20);
        }

        return kosul();
    }
}

/// <summary>
/// İz listesi ve iki altyazı sırası elle kurulan motor. İkincil seçim <see cref="Dispose"/>
/// ile silinmez: aynı motor ikinci dosyayı açtığında seçim libmpv seçeneği gibi taşınır,
/// "yeni dosyada kapalı" kuralını görünüm kendi sıfırlamasıyla tutmak zorunda kalır.
/// </summary>
internal sealed class IkincilMotoru : IPlaybackEngine
{
    private readonly List<PlaybackTrack> _izler = new();

    internal IkincilMotoru(int altyazi)
    {
        for (var i = 1; i <= altyazi; i++)
            _izler.Add(new PlaybackTrack(i, PlaybackTrackKind.Subtitle, null, i == 1 ? "tur" : "eng", false, false));
    }

    internal List<long> BirincilCagrilari { get; } = new();

    internal List<long> IkincilCagrilari { get; } = new();

    public string Name => "ikincil-motoru";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 600;

    public bool HasAudio => false;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached => false;

    public double PositionSeconds { get; private set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered => 0;

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public IReadOnlyList<PlaybackTrack> Tracks => _izler;

    public long SubtitleTrack { get; private set; }

    public long SecondarySubtitleTrack { get; private set; }

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        IsOpen = true;
        return Task.CompletedTask;
    }

    public void Play() => IsPaused = false;

    public void Pause() => IsPaused = true;

    public void SetSubtitleTrack(long id)
    {
        BirincilCagrilari.Add(id);
        SubtitleTrack = id;
    }

    public void SetSecondarySubtitleTrack(long id)
    {
        IkincilCagrilari.Add(id);
        SecondarySubtitleTrack = id;
    }

    public bool AddSubtitle(string path)
    {
        if (!IsOpen || !File.Exists(path)) return false;
        var id = _izler.Count + 1;
        _izler.Add(new PlaybackTrack(id, PlaybackTrackKind.Subtitle, Path.GetFileName(path), null, true, true));
        SubtitleTrack = id;
        return true;
    }

    public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        PositionSeconds = seconds;
        return Task.FromResult(new SeekResult(SeekOutcome.Shown, 1));
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy) => false;

    public void Dispose() => IsOpen = false;
}

public sealed class OynaticiIkincilAltyaziTests
{
    private readonly ITestOutputHelper _cikti;

    public OynaticiIkincilAltyaziTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static PlayerView Ac(IkincilMotoru motor, out Window pencere, string yol)
    {
        var view = new PlayerView { EngineFactory = () => motor };
        pencere = new Window { Width = 640, Height = 360, Content = view };
        pencere.Show();
        Yukle(view, yol);
        return view;
    }

    private static void Yukle(PlayerView view, string yol)
    {
        var acilis = view.OpenAsync(yol);
        DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
        acilis.GetAwaiter().GetResult();
    }

    private static string Film(string kok, string ad = "film.mp4")
    {
        var film = Path.Combine(kok, ad);
        File.WriteAllBytes(film, new byte[16]);
        return film;
    }

    private static MenuItem IkincilMenu(PlayerView view)
        => view.BuildSubtitleItems().OfType<MenuItem>().Single(m => m.Header as string == Strings.Get("player.subtitle.secondary"));

    private static List<MenuItem> Satirlar(MenuItem menu) => menu.Items.OfType<MenuItem>().ToList();

    private static void Tikla(MenuItem satir) => satir.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    /// <summary>
    /// Alt listenin satırı motora kendi iz kimliğini gönderir, Kapalı satırı 0 gönderir
    /// (motorda <c>no</c>); işaret motorun bildirdiği ikincil izi izler.
    /// </summary>
    [Fact]
    public void SecimMotoraIzKimligiyleGider()
    {
        var rapor = AppHost.Run(() =>
        {
            var kok = IkincilKanit.Temiz("secim");
            var motor = new IkincilMotoru(3);
            var view = Ac(motor, out var pencere, Film(kok));
            view.SelectSubtitle(1);

            var basliklar = Satirlar(IkincilMenu(view)).Select(s => s.Header as string).ToList();
            Tikla(Satirlar(IkincilMenu(view))[3]);
            var ucuncu = motor.SecondarySubtitleTrack;
            var isaretUc = Satirlar(IkincilMenu(view)).Select(s => s.IsChecked).ToList();
            Tikla(Satirlar(IkincilMenu(view))[2]);
            var ikinci = motor.SecondarySubtitleTrack;
            Tikla(Satirlar(IkincilMenu(view))[0]);
            var kapali = motor.SecondarySubtitleTrack;
            var isaretKapali = Satirlar(IkincilMenu(view)).Select(s => s.IsChecked).ToList();
            var birincil = motor.SubtitleTrack;
            pencere.Close();
            return (Basliklar: basliklar, Ucuncu: ucuncu, IsaretUc: isaretUc, Ikinci: ikinci, Kapali: kapali, IsaretKapali: isaretKapali, Cagrilar: motor.IkincilCagrilari.ToList(), Birincil: birincil);
        });

        Assert.Equal(4, rapor.Basliklar.Count);
        Assert.Equal(Strings.Get("player.subtitle.off"), rapor.Basliklar[0]);
        Assert.Equal(3, rapor.Ucuncu);
        Assert.Equal(new[] { false, false, false, true }, rapor.IsaretUc);
        Assert.Equal(2, rapor.Ikinci);
        Assert.Equal(0, rapor.Kapali);
        Assert.Equal(new[] { true, false, false, false }, rapor.IsaretKapali);
        Assert.Equal(new long[] { 0, 3, 2, 0 }, rapor.Cagrilar);
        Assert.Equal(1, rapor.Birincil);

        IkincilKanit.Kapat("secim");
    }

    /// <summary>
    /// Birincilde duran izin satırı etkisizdir ve tıklansa da ikincile yazılmaz; öteki iz
    /// yazılır (olumsuz kontrol). İkincildeki iz birincile alınınca ikincil kapanır, birincil
    /// kapatılınca ikincil yerinde kalır.
    /// </summary>
    [Fact]
    public void BirincilleAyniIzIkincilOlmaz()
    {
        var rapor = AppHost.Run(() =>
        {
            var kok = IkincilKanit.Temiz("ayni-iz");
            var motor = new IkincilMotoru(2);
            var view = Ac(motor, out var pencere, Film(kok));
            view.SelectSubtitle(1);

            var etkin = Satirlar(IkincilMenu(view)).Select(s => s.IsEnabled).ToList();
            var once = motor.IkincilCagrilari.Count;
            Tikla(Satirlar(IkincilMenu(view))[1]);
            var ayniSonrasi = motor.SecondarySubtitleTrack;
            var ayniCagri = motor.IkincilCagrilari.Count - once;
            Tikla(Satirlar(IkincilMenu(view))[2]);
            var farkli = motor.SecondarySubtitleTrack;

            view.SelectSubtitle(0);
            var birincilKapaliyken = (motor.SubtitleTrack, motor.SecondarySubtitleTrack);

            view.SelectSubtitle(2);
            var terfi = (motor.SubtitleTrack, motor.SecondarySubtitleTrack);
            var etkinSonra = Satirlar(IkincilMenu(view)).Select(s => s.IsEnabled).ToList();
            pencere.Close();
            return (Etkin: etkin, AyniSonrasi: ayniSonrasi, AyniCagri: ayniCagri, Farkli: farkli, BirincilKapaliyken: birincilKapaliyken, Terfi: terfi, EtkinSonra: etkinSonra);
        });

        Assert.Equal(new[] { true, false, true }, rapor.Etkin);
        Assert.Equal(0, rapor.AyniSonrasi);
        Assert.Equal(0, rapor.AyniCagri);
        Assert.Equal(2, rapor.Farkli);
        Assert.Equal((0L, 2L), rapor.BirincilKapaliyken);
        Assert.Equal((2L, 0L), rapor.Terfi);
        Assert.Equal(new[] { true, true, false }, rapor.EtkinSonra);

        IkincilKanit.Kapat("ayni-iz");
    }

    /// <summary>
    /// Tek altyazı izli dosyada alt liste etkisiz, iki izlide etkin (olumsuz kontrol). Dış
    /// altyazı yüklenince iz listeye girer ve tek izli dosyanın alt listesi açılır.
    /// </summary>
    [Fact]
    public void IkidenAzIzdeListeEtkisiz()
    {
        var rapor = AppHost.Run(() =>
        {
            var kok = IkincilKanit.Temiz("tek-iz");
            var iki = new IkincilMotoru(2);
            var ikiView = Ac(iki, out var ikiPencere, Film(kok, "iki.mp4"));
            var ikiEtkin = IkincilMenu(ikiView).IsEnabled;
            ikiPencere.Close();

            var sifir = new IkincilMotoru(0);
            var sifirView = Ac(sifir, out var sifirPencere, Film(kok, "sifir.mp4"));
            var sifirEtkin = IkincilMenu(sifirView).IsEnabled;
            sifirPencere.Close();

            var tek = new IkincilMotoru(1);
            var view = Ac(tek, out var pencere, Film(kok, "tek.mp4"));
            var tekEtkin = IkincilMenu(view).IsEnabled;

            var dis = Path.Combine(kok, "dis-altyazi.srt");
            File.WriteAllText(dis, "1\r\n00:00:00,000 --> 00:00:01,000\r\ndis\r\n\r\n");
            var yuklendi = view.LoadSubtitle(dis);
            var menu = IkincilMenu(view);
            var satirlar = Satirlar(menu);
            var sonra = (menu.IsEnabled, satirlar.Count, satirlar.Select(s => s.IsEnabled).ToList());
            Tikla(satirlar[1]);
            var secilen = tek.SecondarySubtitleTrack;
            pencere.Close();
            return (IkiEtkin: ikiEtkin, SifirEtkin: sifirEtkin, TekEtkin: tekEtkin, Yuklendi: yuklendi, Sonra: sonra, Secilen: secilen, Birincil: tek.SubtitleTrack);
        });

        Assert.True(rapor.IkiEtkin);
        Assert.False(rapor.SifirEtkin);
        Assert.False(rapor.TekEtkin);
        Assert.True(rapor.Yuklendi);
        Assert.True(rapor.Sonra.Item1);
        Assert.Equal(3, rapor.Sonra.Item2);
        Assert.Equal(new[] { true, true, false }, rapor.Sonra.Item3);
        Assert.Equal(2, rapor.Birincil);
        Assert.Equal(1, rapor.Secilen);

        IkincilKanit.Kapat("tek-iz");
    }

    /// <summary>
    /// Seçim dosyayla birlikte biter: motor ikincil izi taşısa da yeni dosya ikincil kapalı
    /// açılır. İlk dosyada seçimin gerçekten yapıldığı ayrıca okunur.
    /// </summary>
    [Fact]
    public void YeniDosyadaIkincilKapaliBaslar()
    {
        var rapor = AppHost.Run(() =>
        {
            var kok = IkincilKanit.Temiz("yeni-dosya");
            var motor = new IkincilMotoru(2);
            var view = Ac(motor, out var pencere, Film(kok, "ilk.mp4"));
            var acilista = (motor.SecondarySubtitleTrack, motor.IkincilCagrilari.ToList());
            view.SelectSubtitle(1);
            view.SelectSecondarySubtitle(2);
            var secili = motor.SecondarySubtitleTrack;

            Yukle(view, Film(kok, "sonraki.mp4"));
            var sonra = motor.SecondarySubtitleTrack;
            var isaret = Satirlar(IkincilMenu(view)).Select(s => s.IsChecked).ToList();
            pencere.Close();
            return (Acilista: acilista, Secili: secili, Sonra: sonra, Isaret: isaret, Son: motor.IkincilCagrilari[^1]);
        });

        Assert.Equal(0, rapor.Acilista.Item1);
        Assert.Equal(new long[] { 0 }, rapor.Acilista.Item2);
        Assert.Equal(2, rapor.Secili);
        Assert.Equal(0, rapor.Sonra);
        Assert.Equal(0, rapor.Son);
        Assert.Equal(new[] { true, false, false }, rapor.Isaret);

        IkincilKanit.Kapat("yeni-dosya");
    }

    /// <summary>
    /// Gerçek libmpv: iki srt izli klipte ikincil iz <c>secondary-sid</c>'e yazılır ve motordan
    /// geri okunur, iki sıranın metni ayrı ayrı gelir, ikincildeki iz birincile istenince libmpv
    /// yerinden oynatmaz (görünümün önce ikincili kapatmasının gerekçesi), birincil kapanınca
    /// ikincil kalır, Kapalı <c>no</c> yazar.
    /// </summary>
    [Fact]
    public async Task IkincilIzMotordaAyarlanipGeriOkunur()
    {
        var kok = IkincilKanit.Temiz("motor");
        var klip = IkincilKanit.Klip(kok);

        using (var engine = new MpvEngine())
        {
            engine.SetProperty("ao", "null");
            await engine.OpenAsync(klip);

            var altyazilar = engine.Tracks.Where(t => t.Kind == PlaybackTrackKind.Subtitle).Select(t => t.Id).ToList();
            _cikti.WriteLine($"izler {string.Join(",", altyazilar)}; acilista sid {engine.GetProperty("sid")} secondary-sid {engine.GetProperty("secondary-sid")}");
            Assert.Equal(new long[] { 1, 2 }, altyazilar);
            Assert.Equal(0, engine.SecondarySubtitleTrack);

            engine.SetSubtitleTrack(1);
            engine.SetSecondarySubtitleTrack(2);
            Assert.True(await IkincilKanit.Bekle(() => engine.SecondarySubtitleTrack == 2), "ikincil iz 2'ye oturmadi");
            Assert.Equal("2", engine.GetProperty("secondary-sid"));
            Assert.Equal(1, engine.SubtitleTrack);

            await engine.SeekAsync(1.0, SeekPrecision.Exact);
            var ikisi = await IkincilKanit.Bekle(() => engine.GetProperty("sub-text") == IkincilKanit.Birinci && engine.GetProperty("secondary-sub-text") == IkincilKanit.Ikinci);
            _cikti.WriteLine($"sub-text '{engine.GetProperty("sub-text")}' secondary-sub-text '{engine.GetProperty("secondary-sub-text")}'");
            Assert.True(ikisi, "iki siranin metni birlikte gelmedi");

            engine.SetSubtitleTrack(2);
            _cikti.WriteLine($"ikincildeki iz birincile istenince: sid {engine.GetProperty("sid")} secondary-sid {engine.GetProperty("secondary-sid")} birincil {engine.SubtitleTrack} ikincil {engine.SecondarySubtitleTrack}");
            Assert.Equal(1, engine.SubtitleTrack);
            Assert.Equal(2, engine.SecondarySubtitleTrack);

            engine.SetSubtitleTrack(0);
            Assert.True(await IkincilKanit.Bekle(() => engine.SubtitleTrack == 0), "birincil kapanmadi");
            Assert.Equal("2", engine.GetProperty("secondary-sid"));
            Assert.Equal(2, engine.SecondarySubtitleTrack);

            engine.SetSecondarySubtitleTrack(0);
            Assert.True(await IkincilKanit.Bekle(() => engine.SecondarySubtitleTrack == 0), "ikincil kapanmadi");
            Assert.Equal("no", engine.GetProperty("secondary-sid"));
        }

        IkincilKanit.Kapat("motor");
    }

    /// <summary>Alt listenin başlığı kırk iki dilde dolu.</summary>
    [Fact]
    public void BaslikButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
            Assert.True(Locales.Domain(dil, "tracks").TryGetValue("player.subtitle.secondary", out var deger) && !string.IsNullOrWhiteSpace(deger), dil);
    }
}
