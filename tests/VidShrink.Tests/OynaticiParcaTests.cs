using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class ParcaKanit
{
    internal const string Gomulu = "Gömülü satır";
    internal const string Turkce = "ğ ş ı İ ç ö ü";

    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "dalga2");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));

    internal static string Srt(string text) => "1\r\n00:00:00,500 --> 00:00:05,500\r\n" + text + "\r\n\r\n";

    internal static string Klip
    {
        get
        {
            var path = Path.Combine(Folder, "parca-2ses-1altyazi.mkv");
            if (File.Exists(path) && new FileInfo(path).Length > 0) return path;

            Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli; bu test ffmpeg olmadan kirmizi kalir");
            var srt = Path.Combine(Folder, "gomulu.srt");
            File.WriteAllText(srt, Srt(Gomulu), new UTF8Encoding(false));

            var partial = Path.Combine(Folder, "parca-2ses-1altyazi.part.mkv");
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
                "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=25:duration=6",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=6",
                "-f", "lavfi", "-i", "sine=frequency=880:sample_rate=48000:duration=6",
                "-i", srt,
                "-map", "0:v", "-map", "1:a", "-map", "2:a", "-map", "3:s",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
                "-c:a", "aac", "-b:a", "64k", "-ac", "1", "-c:s", "srt",
                "-metadata:s:a:0", "language=tur", "-metadata:s:a:1", "language=eng", "-metadata:s:s:0", "language=tur",
                partial
            }) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120_000))
            {
                try { process.Kill(true); } catch { }
                throw new TimeoutException("parca klibi 120 sn icinde uretilmedi");
            }

            stdout.GetAwaiter().GetResult();
            var log = stderr.GetAwaiter().GetResult();
            Assert.True(process.ExitCode == 0, $"ffmpeg parca klibini uretemedi (kod {process.ExitCode}): {log[Math.Max(0, log.Length - 600)..]}");
            File.Move(partial, path, true);
            return path;
        }
    }

    internal static byte[] Cp1254Srt()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1254).GetBytes(Srt(Turkce));
    }

    internal static bool GecerliUtf8(byte[] bytes)
    {
        try
        {
            new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    internal static async Task<MpvEngine> AcAsync()
    {
        var engine = new MpvEngine();
        engine.SetProperty("ao", "null");
        await engine.OpenAsync(Klip);
        return engine;
    }

    internal static async Task<string?> AltyaziBekle(MpvEngine engine, string expected, double seconds = 5)
    {
        var saat = Stopwatch.StartNew();
        string? text = null;
        while (saat.Elapsed.TotalSeconds < seconds)
        {
            text = engine.GetProperty("sub-text");
            if (text == expected) return text;
            await Task.Delay(20);
        }

        return text;
    }

    internal static IStorageItem Dosya(string path)
    {
        var lookup = new Window().StorageProvider.TryGetFileFromPathAsync(new Uri(Path.GetFullPath(path)));
        var saat = Stopwatch.StartNew();
        while (!lookup.IsCompleted && saat.Elapsed.TotalSeconds < 5)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        return lookup.GetAwaiter().GetResult() ?? throw new InvalidOperationException("yerel dosya depolama ogesine donmedi: " + path);
    }

    internal static string Liste(IPlaybackEngine engine)
        => string.Join(Environment.NewLine, engine.Tracks.Select(t => $"  {t.Kind} id {t.Id} lang {t.Language ?? "-"} title {t.Title ?? "-"} dis {t.External} secili {t.Selected}"));
}

public sealed class OynaticiParcaTests
{
    [Fact]
    public async Task IkiSesVeGomuluAltyaziMotordanSecilipGeriOkunur()
    {
        using var engine = await ParcaKanit.AcAsync();
        var body = new StringBuilder();
        body.AppendLine("klip: " + ParcaKanit.Klip);
        body.AppendLine("parcalar:");
        body.AppendLine(ParcaKanit.Liste(engine));

        var sesler = engine.Tracks.Where(t => t.Kind == PlaybackTrackKind.Audio).Select(t => t.Id).ToList();
        var altyazilar = engine.Tracks.Where(t => t.Kind == PlaybackTrackKind.Subtitle).Select(t => t.Id).ToList();
        Assert.Equal(new long[] { 1, 2 }, sesler);
        Assert.Equal(new long[] { 1 }, altyazilar);
        Assert.Equal(1, engine.AudioTrack);
        Assert.Equal(0, engine.SubtitleTrack);

        engine.SetAudioTrack(2);
        body.AppendLine($"SetAudioTrack(2) -> AudioTrack {engine.AudioTrack}, aid {engine.GetProperty("aid")}");
        Assert.Equal(2, engine.AudioTrack);
        Assert.Equal("2", engine.GetProperty("aid"));
        Assert.True(engine.Tracks.Single(t => t is { Kind: PlaybackTrackKind.Audio, Id: 2 }).Selected);

        engine.SetSubtitleTrack(1);
        await engine.SeekAsync(1.5, SeekPrecision.Exact);
        var metin = await ParcaKanit.AltyaziBekle(engine, ParcaKanit.Gomulu);
        body.AppendLine($"SetSubtitleTrack(1) -> SubtitleTrack {engine.SubtitleTrack}, sid {engine.GetProperty("sid")}, sub-text '{metin}'");
        Assert.Equal(1, engine.SubtitleTrack);
        Assert.Equal("1", engine.GetProperty("sid"));
        Assert.Equal(ParcaKanit.Gomulu, metin);

        engine.SetSubtitleTrack(0);
        engine.SetAudioTrack(1);
        body.AppendLine($"SetSubtitleTrack(0), SetAudioTrack(1) -> sid {engine.GetProperty("sid")}, aid {engine.GetProperty("aid")}");
        Assert.Equal("no", engine.GetProperty("sid"));
        Assert.Equal(0, engine.SubtitleTrack);
        Assert.Equal(1, engine.AudioTrack);

        ParcaKanit.Write("motor-parca-secimi.txt", body.ToString());
    }

    [Fact]
    public async Task GecikmeBoyutKonumVeKodlamaMotordaGeriOkunur()
    {
        using var engine = await ParcaKanit.AcAsync();
        var body = new StringBuilder();
        body.AppendLine($"once: sub-delay {engine.GetProperty("sub-delay")} audio-delay {engine.GetProperty("audio-delay")} sub-scale {engine.GetProperty("sub-scale")} sub-pos {engine.GetProperty("sub-pos")} sub-codepage {engine.GetProperty("sub-codepage")}");

        engine.SetSubtitleDelay(0.75);
        engine.SetAudioDelay(-0.2);
        engine.SetSubtitleScale(1.4);
        engine.SetSubtitlePosition(80);
        engine.SetSubtitleCodepage("cp1254");
        body.AppendLine($"sonra: sub-delay {engine.GetProperty("sub-delay")} audio-delay {engine.GetProperty("audio-delay")} sub-scale {engine.GetProperty("sub-scale")} sub-pos {engine.GetProperty("sub-pos")} sub-codepage {engine.GetProperty("sub-codepage")}");
        ParcaKanit.Write("motor-gecikme.txt", body.ToString());

        Assert.Equal(0.75, MotorKanit.ReadDouble(engine, "sub-delay"), 6);
        Assert.Equal(-0.2, MotorKanit.ReadDouble(engine, "audio-delay"), 6);
        Assert.Equal(0.75, engine.SubtitleDelaySeconds, 6);
        Assert.Equal(-0.2, engine.AudioDelaySeconds, 6);
        Assert.Equal(1.4, engine.SubtitleScale, 6);
        Assert.Equal(80, engine.SubtitlePosition, 6);
        Assert.Equal("cp1254", engine.SubtitleCodepage);

        engine.SetSubtitleDelay(0);
        engine.SetAudioDelay(0);
        Assert.Equal(0, MotorKanit.ReadDouble(engine, "sub-delay"), 6);
        Assert.Equal(0, MotorKanit.ReadDouble(engine, "audio-delay"), 6);
    }

    [Fact]
    public async Task Cp1254AltyaziTurkceKarakterleriBozmadanGosterilir()
    {
        var bytes = ParcaKanit.Cp1254Srt();
        Assert.False(ParcaKanit.GecerliUtf8(bytes), "cp1254 baytlari UTF-8 olarak da gecerli; ayirt edici degil");
        var srt = Path.Combine(ParcaKanit.Folder, "turkce-cp1254.srt");
        File.WriteAllBytes(srt, bytes);

        using var engine = await ParcaKanit.AcAsync();
        engine.SetSubtitleCodepage("cp1254");
        Assert.True(engine.AddSubtitle(srt), "sub-add basarisiz");
        await engine.SeekAsync(1.5, SeekPrecision.Exact);
        var metin = await ParcaKanit.AltyaziBekle(engine, ParcaKanit.Turkce);

        var dis = engine.Tracks.Single(t => t is { Kind: PlaybackTrackKind.Subtitle, External: true });
        ParcaKanit.Write("cp1254-dogru.txt",
            $"dosya: {srt}\nbaytlar: {Convert.ToHexString(bytes)}\nsub-codepage: {engine.GetProperty("sub-codepage")}\nsecili altyazi: {engine.SubtitleTrack} (dis parca {dis.Id})\nsub-text: '{metin}'\nbeklenen: '{ParcaKanit.Turkce}'\n");

        Assert.Equal(dis.Id, engine.SubtitleTrack);
        Assert.Equal(ParcaKanit.Turkce, metin);
    }

    [Fact]
    public async Task YanlisKodSayfasiMetniBozarDogrusuyaGecinceDuzelir()
    {
        var bytes = ParcaKanit.Cp1254Srt();
        var srt = Path.Combine(ParcaKanit.Folder, "turkce-cp1254-negatif.srt");
        File.WriteAllBytes(srt, bytes);
        var bozuk = Encoding.GetEncoding(1252).GetString(Encoding.GetEncoding(1254).GetBytes(ParcaKanit.Turkce));
        Assert.NotEqual(ParcaKanit.Turkce, bozuk);

        using var engine = await ParcaKanit.AcAsync();
        engine.SetSubtitleCodepage("cp1252");
        Assert.True(engine.AddSubtitle(srt), "sub-add basarisiz");
        await engine.SeekAsync(1.5, SeekPrecision.Exact);
        var yanlis = await ParcaKanit.AltyaziBekle(engine, bozuk);
        var yanlisParca = engine.SubtitleTrack;

        engine.SetSubtitleCodepage("cp1254");
        await engine.SeekAsync(1.5, SeekPrecision.Exact);
        var dogru = await ParcaKanit.AltyaziBekle(engine, ParcaKanit.Turkce);

        ParcaKanit.Write("cp1254-negatif.txt",
            $"cp1252 ile sub-text: '{yanlis}' (parca {yanlisParca})\nbeklenen bozuk: '{bozuk}'\ncp1254'e gecis (sub-reload) sonrasi sub-text: '{dogru}' (parca {engine.SubtitleTrack})\nbeklenen: '{ParcaKanit.Turkce}'\nparcalar:\n{ParcaKanit.Liste(engine)}\n");

        Assert.Equal(bozuk, yanlis);
        Assert.NotEqual(ParcaKanit.Turkce, yanlis);
        Assert.Equal(ParcaKanit.Turkce, dogru);
    }

    [Fact]
    public void KisayolVeMenuParcaKomutlariMotoraUlasir()
    {
        var clip = ParcaKanit.Klip;
        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(clip, out var window);
            var motor = DenetimSurucu.Motor(view);
            var body = new StringBuilder();
            string Oku() => $"aid {motor.GetProperty("aid")} sid {motor.GetProperty("sid")} sub-delay {motor.GetProperty("sub-delay")} audio-delay {motor.GetProperty("audio-delay")} sub-scale {motor.GetProperty("sub-scale")} sub-pos {motor.GetProperty("sub-pos")} sub-codepage {motor.GetProperty("sub-codepage")}";
            body.AppendLine("acilis: " + Oku());

            var adimlar = new (string Ad, Action Is)[]
            {
                ("A", () => GirdiSurucu.Key(view, Key.A)),
                ("S", () => GirdiSurucu.Key(view, Key.S)),
                (">", () => GirdiSurucu.Key(view, Key.OemPeriod, KeyModifiers.Shift, ">")),
                (">", () => GirdiSurucu.Key(view, Key.OemPeriod, KeyModifiers.Shift, ">")),
                ("<", () => GirdiSurucu.Key(view, Key.OemComma, KeyModifiers.Shift, "<")),
                ("Ctrl+.", () => GirdiSurucu.Key(view, Key.OemPeriod, KeyModifiers.Control)),
                ("Ctrl+.", () => GirdiSurucu.Key(view, Key.OemPeriod, KeyModifiers.Control)),
                ("Ctrl+,", () => GirdiSurucu.Key(view, Key.OemComma, KeyModifiers.Control)),
                ("buyut x2", () => view.ResizeSubtitle(2)),
                ("yukari x2", () => view.MoveSubtitle(-2)),
                ("kodlama cp1254", () => view.UseCodepage("cp1254"))
            };
            foreach (var (ad, adim) in adimlar)
            {
                adim();
                DenetimSurucu.Wait(view, 0.05);
                body.AppendLine($"{ad,-16} iz {view.Trace[^1],-24} {Oku()}");
            }

            var sonuc = (
                Aid: motor.GetProperty("aid"),
                Sid: motor.GetProperty("sid"),
                SubDelay: MotorKanit.ReadDouble(motor, "sub-delay"),
                AudioDelay: MotorKanit.ReadDouble(motor, "audio-delay"),
                Scale: MotorKanit.ReadDouble(motor, "sub-scale"),
                Pos: MotorKanit.ReadDouble(motor, "sub-pos"),
                Codepage: motor.GetProperty("sub-codepage"));

            GirdiSurucu.Key(view, Key.S);
            var kapali = motor.GetProperty("sid");
            body.AppendLine($"S (ikinci) -> sid {kapali}");

            var menu = view.BuildMenu().Items.OfType<MenuItem>().Where(item => item.Tag is null).ToList();
            var ses = menu.Single(item => (string?)item.Header == Strings.Get("player.tracks.audio"));
            var altyazi = menu.Single(item => (string?)item.Header == Strings.Get("player.subtitle.menu"));
            var sesSecenek = ses.Items.OfType<MenuItem>().Where(item => item.ToggleType == MenuItemToggleType.Radio).ToList();
            var altyaziSecenek = altyazi.Items.OfType<MenuItem>().Where(item => item.ToggleType == MenuItemToggleType.Radio).ToList();
            body.AppendLine("ses alt menusu: " + string.Join(" | ", ses.Items.OfType<MenuItem>().Select(item => $"{item.Header}{(item.IsChecked ? " *" : "")}")));
            body.AppendLine("altyazi alt menusu: " + string.Join(" | ", altyazi.Items.OfType<MenuItem>().Select(item => $"{item.Header}{(item.IsChecked ? " *" : "")}")));

            var isaretli = sesSecenek.FindIndex(item => item.IsChecked);
            sesSecenek[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            var menudenSes = motor.GetProperty("aid");
            body.AppendLine("menuden ses 1 -> aid " + menudenSes);
            var durum = view.FindControl<TextBlock>("TxtControls")?.Text ?? "";
            var beklenenDurum = new[]
            {
                Strings.Get("player.subtitle.delay", SubtitleOptions.Signed(0.5)),
                Strings.Get("player.tracks.delay", SubtitleOptions.Signed(0.1))
            };
            body.AppendLine("denetim satiri: " + durum);

            view.Close();
            window.Close();
            return (body.ToString(), sonuc, kapali, sesSecenek.Count, altyaziSecenek.Count, isaretli, menudenSes, durum, beklenenDurum);
        });

        ParcaKanit.Write("gorunum-parca-kisayol.txt", rapor.Item1);
        Assert.Equal("2", rapor.sonuc.Aid);
        Assert.Equal("1", rapor.sonuc.Sid);
        Assert.Equal(0.5, rapor.sonuc.SubDelay, 6);
        Assert.Equal(0.1, rapor.sonuc.AudioDelay, 6);
        Assert.Equal(1.2, rapor.sonuc.Scale, 6);
        Assert.Equal(90, rapor.sonuc.Pos, 6);
        Assert.Equal("cp1254", rapor.sonuc.Codepage);
        Assert.Equal("no", rapor.kapali);
        Assert.Equal(2, rapor.Item4);
        Assert.Equal(2, rapor.Item5);
        Assert.Equal(1, rapor.isaretli);
        Assert.Equal("1", rapor.menudenSes);
        foreach (var parca in rapor.beklenenDurum) Assert.Contains(parca, rapor.durum);
    }

    [Fact]
    public void AltyaziDosyasiBirakilincaYuklenirVideoBirakmaOynaticidaKalir()
    {
        var clip = ParcaKanit.Klip;
        var srt = Path.Combine(ParcaKanit.Folder, "birakilan.srt");
        File.WriteAllText(srt, ParcaKanit.Srt("Bırakılan satır"), new UTF8Encoding(false));

        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();

            DragEventArgs Birak(PlayerView view, string path)
            {
                var transfer = new DataTransfer();
                transfer.Add(DataTransferItem.CreateFile(ParcaKanit.Dosya(path)));
                var args = new DragEventArgs(DragDrop.DropEvent, transfer, view, new Point(1, 1), KeyModifiers.None);
                view.RaiseEvent(args);
                return args;
            }

            var bos = GirdiSurucu.Kur(out var bosPencere);
            var bosBirakma = Birak(bos, srt);
            body.AppendLine($"videosuz altyazi birakma: handled {bosBirakma.Handled}, uyari {bos.TrackNotice}, iz {bos.Trace[^1]}");
            var bosSonuc = (bosBirakma.Handled, bos.TrackNotice);
            bos.Close();
            bosPencere.Close();

            var view = DenetimSurucu.Ac(clip, out var window);
            var pencereyeUlasan = new List<string>();
            window.AddHandler(DragDrop.DropEvent, (_, e) => pencereyeUlasan.Add(e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.Name ?? "?"));
            window.Show();

            var altyazi = Birak(view, srt);
            DenetimSurucu.Wait(view, 0.1);
            var motor = DenetimSurucu.Motor(view);
            var dis = motor.Tracks.Where(t => t is { Kind: PlaybackTrackKind.Subtitle, External: true }).ToList();
            body.AppendLine($"altyazi birakma: handled {altyazi.Handled}, pencereye ulasan {pencereyeUlasan.Count}, sid {motor.GetProperty("sid")}, dis parca {string.Join(",", dis.Select(t => t.Id))}, iz {view.Trace[^1]}");
            var altyaziSonuc = (altyazi.Handled, Pencere: pencereyeUlasan.Count, Sid: motor.SubtitleTrack, Dis: dis.Select(t => t.Id).ToList(), view.TrackNotice);

            var video = Birak(view, clip);
            body.AppendLine($"video birakma: handled {video.Handled}, pencereye ulasan {pencereyeUlasan.Count} ({string.Join(",", pencereyeUlasan)})");
            var videoSonuc = (video.Handled, Pencere: pencereyeUlasan.Count);

            view.Close();
            window.Close();
            return (body.ToString(), bosSonuc, altyaziSonuc, videoSonuc);
        });

        ParcaKanit.Write("birakma.txt", rapor.Item1);
        Assert.True(rapor.bosSonuc.Handled);
        Assert.Equal("player.subtitle.novideo", rapor.bosSonuc.TrackNotice);
        Assert.True(rapor.altyaziSonuc.Handled);
        Assert.Equal(0, rapor.altyaziSonuc.Pencere);
        Assert.Single(rapor.altyaziSonuc.Dis);
        Assert.Equal(rapor.altyaziSonuc.Dis[0], rapor.altyaziSonuc.Sid);
        Assert.Null(rapor.altyaziSonuc.TrackNotice);
        Assert.True(rapor.videoSonuc.Handled);
        Assert.Equal(0, rapor.videoSonuc.Pencere);
    }
}
