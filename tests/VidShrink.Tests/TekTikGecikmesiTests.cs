using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal sealed class ZamanliMotor : IPlaybackEngine
{
    internal readonly List<(long An, bool Oynat)> Komutlar = new();

    public string Name => "zamanli-motor";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 3600;

    public bool HasAudio => false;

    public bool HasVideo => true;

    public MediaTags Tags => MediaTags.Empty;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached => false;

    public double PositionSeconds { get; set; }

    public double AudioVideoOffsetSeconds => double.NaN;

    public long FramesRendered => 0;

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        IsOpen = true;
        return Task.CompletedTask;
    }

    public void Play()
    {
        Komutlar.Add((Stopwatch.GetTimestamp(), true));
        IsPaused = false;
    }

    public void Pause()
    {
        Komutlar.Add((Stopwatch.GetTimestamp(), false));
        IsPaused = true;
    }

    public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        PositionSeconds = seconds;
        return Task.FromResult(new SeekResult(SeekOutcome.Shown, 1));
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy) => false;

    public void Dispose() => IsOpen = false;
}

public sealed class TekTikGecikmesiTests
{
    private const int Isinma = 30;
    private const int Olcum = 240;

    private static string Klasor
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "tek-tik-gecikmesi");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static double Ms(long fark) => fark * 1000.0 / Stopwatch.Frequency;

    private static double Yuzdelik(IReadOnlyList<double> sirali, double oran)
    {
        var konum = (sirali.Count - 1) * oran;
        var alt = (int)Math.Floor(konum);
        var ust = Math.Min(alt + 1, sirali.Count - 1);
        return sirali[alt] + (sirali[ust] - sirali[alt]) * (konum - alt);
    }

    private static string Etiket()
        => Environment.GetEnvironmentVariable("VIDSHRINK_TIK_ETIKETI") is { Length: > 0 } e ? e : "dal";

    [WindowsFact("ham girdi Win32 arka ucuna bağlı")]
    public void HamBirakistanMotoraTekTikGecikmesiOlculur()
    {
        var etiket = Etiket();
        var rapor = AppHost.Run(() =>
        {
            var motor = new ZamanliMotor();
            var view = new PlayerView { EngineFactory = () => motor };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            var dosya = Path.Combine(Klasor, "bos.mp4");
            File.WriteAllBytes(dosya, new byte[16]);
            var acilis = view.OpenAsync(dosya);
            DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
            acilis.GetAwaiter().GetResult();
            DenetimSurucu.Wait(view, 0.3);

            var yuzey = view.FindControl<Panel>("Surface")!;
            var orta = OynaticiListeTests.Merkez(pencere, yuzey);
            var noktalar = new[] { new Point(orta.X - 40, orta.Y), new Point(orta.X + 40, orta.Y) };
            foreach (var n in noktalar) OynaticiListeTests.Vurulur(pencere, n, yuzey, view);

            var ilk = motor.Komutlar.Count;
            var sureler = new List<double>();
            var kayip = 0;
            for (var i = 0; i < Isinma + Olcum; i++)
            {
                var nokta = noktalar[i % 2];
                OynaticiListeTests.Fareyle(pencere, RawPointerEventType.Move, nokta, RawInputModifiers.None);
                OynaticiListeTests.Fareyle(pencere, RawPointerEventType.LeftButtonDown, nokta, RawInputModifiers.LeftMouseButton);
                Dispatcher.UIThread.RunJobs();
                var once = motor.Komutlar.Count;
                var t0 = Stopwatch.GetTimestamp();
                OynaticiListeTests.Fareyle(pencere, RawPointerEventType.LeftButtonUp, nokta, RawInputModifiers.None);
                if (motor.Komutlar.Count != once + 1)
                {
                    kayip++;
                    continue;
                }

                if (i >= Isinma) sureler.Add(Ms(motor.Komutlar[^1].An - t0));
                Dispatcher.UIThread.RunJobs();
            }

            var sirali = sureler.OrderBy(s => s).ToList();
            var govde = new StringBuilder();
            govde.AppendLine(FormattableString.Invariant($"etiket {etiket}: {sirali.Count} tik (isinma {Isinma}), kayip {kayip}"));
            govde.AppendLine(FormattableString.Invariant($"ortanca {Yuzdelik(sirali, 0.5):0.0000} ms, p95 {Yuzdelik(sirali, 0.95):0.0000} ms, en az {sirali[0]:0.0000} ms, en cok {sirali[^1]:0.0000} ms"));
            govde.AppendLine("ham: " + string.Join(" ", sureler.Select(s => s.ToString("0.0000", CultureInfo.InvariantCulture))));
            File.WriteAllText(Path.Combine(Klasor, etiket + ".txt"), govde.ToString(), new UTF8Encoding(false));

            Assert.Equal(0, kayip);
            Assert.Equal(Olcum, sirali.Count);
            var tiklar = motor.Komutlar.Skip(ilk).ToList();
            Assert.Equal(Isinma + Olcum, tiklar.Count);
            Assert.All(tiklar.Zip(tiklar.Skip(1)), ikili => Assert.NotEqual(ikili.First.Oynat, ikili.Second.Oynat));

            pencere.Close();
            return govde.ToString();
        });

        Assert.Contains("kayip 0", rapor);
        if (Environment.GetEnvironmentVariable("VIDSHRINK_TIK_ETIKETI") is not { Length: > 0 }) Directory.Delete(Klasor, true);
    }
}
