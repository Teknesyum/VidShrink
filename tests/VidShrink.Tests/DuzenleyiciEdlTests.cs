using System.Text;
using VidShrink.App.Playback;
using VidShrink.Core.Editing;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal sealed class EdlMotoru : IPlaybackEngine
{
    internal List<double> Aramalar { get; } = new();

    internal List<double> Hizlar { get; } = new();

    public string Name => "edl-motoru";

    public bool IsOpen { get; private set; }

    public string? Acilan { get; private set; }

    public double DurationSeconds => 0;

    public bool HasAudio => false;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached => false;

    public double PositionSeconds { get; set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered => 0;

    public double Speed { get; private set; } = 1;

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        Acilan = path;
        IsOpen = true;
        return Task.CompletedTask;
    }

    public void Play() => IsPaused = false;

    public void Pause() => IsPaused = true;

    public void SetSpeed(double speed)
    {
        Speed = speed;
        Hizlar.Add(speed);
    }

    public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        Aramalar.Add(seconds);
        PositionSeconds = seconds;
        return Task.FromResult(new SeekResult(SeekOutcome.Shown, 0));
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy) => false;

    public void Dispose()
    {
    }
}

public sealed class DuzenleyiciEdlTests
{
    private const long Sn = EditTime.TicksPerSecond;

    private static EditTimeline Cizelge(params EditClip[] parcalar) => new(parcalar);

    private static string Oku(string uri, out List<(string Dosya, string Bas, string Uzunluk)> girdiler)
    {
        Assert.StartsWith("edl://", uri, StringComparison.Ordinal);
        var bayt = Encoding.UTF8.GetBytes(uri["edl://".Length..]);
        girdiler = new List<(string, string, string)>();
        var i = 0;
        while (i < bayt.Length)
        {
            var alanlar = new List<string>();
            while (true)
            {
                string deger;
                if (bayt[i] == (byte)'%')
                {
                    var son = Array.IndexOf(bayt, (byte)'%', i + 1);
                    var n = int.Parse(Encoding.ASCII.GetString(bayt, i + 1, son - i - 1));
                    deger = Encoding.UTF8.GetString(bayt, son + 1, n);
                    i = son + 1 + n;
                }
                else
                {
                    var bas = i;
                    while (i < bayt.Length && bayt[i] != (byte)',' && bayt[i] != (byte)';') i++;
                    deger = Encoding.UTF8.GetString(bayt, bas, i - bas);
                }

                alanlar.Add(deger);
                if (i >= bayt.Length || bayt[i] == (byte)';')
                {
                    i++;
                    break;
                }

                Assert.Equal((byte)',', bayt[i]);
                i++;
            }

            Assert.Equal(3, alanlar.Count);
            girdiler.Add((alanlar[0], alanlar[1], alanlar[2]));
        }

        return uri;
    }

    [Fact]
    public void TekKlipTekGirdiOlur()
    {
        var onizleme = new EdlPreview(@"C:\v\a.mp4", EditTimeline.FromSource(Sn));

        Assert.Equal(@"edl://%10%C:\v\a.mp4,0,1", onizleme.Uri);
        Assert.Equal("# mpv EDL v0\n%10%C:\\v\\a.mp4,0,1\n", onizleme.Document);
        Assert.Equal(Sn, onizleme.EdlDuration);
        Assert.Equal(Sn, onizleme.TimelineDuration);
        Assert.Equal(Sn / 2, onizleme.ToEdl(Sn / 2));
        Assert.Equal(Sn / 2, onizleme.ToTimeline(Sn / 2));
        Assert.Equal(Sn, onizleme.ToEdl(Sn));
    }

    [Fact]
    public void BosCizelgeReddedilir()
    {
        var cizelge = EditTimeline.FromSource(Sn);
        cizelge.Delete(0);

        Assert.Throws<ArgumentException>(() => new EdlPreview(@"C:\v\a.mp4", cizelge));
    }

    [Fact]
    public void CokKlipSiraylaDizilirVeZamanEslenir()
    {
        var onizleme = new EdlPreview(@"C:\v\a.mp4", Cizelge(new EditClip(4 * Sn, 5 * Sn), new EditClip(Sn, Sn * 5 / 2), new EditClip(1, Sn / 3)));

        Oku(onizleme.Uri, out var girdiler);
        Assert.Equal(new[] { ("4", "1"), ("1", "1.5"), ("0.000004167", "0.333329167") }, girdiler.Select(g => (g.Bas, g.Uzunluk)).ToArray());
        Assert.All(girdiler, g => Assert.Equal(@"C:\v\a.mp4", g.Dosya));
        Assert.Equal(new long[] { 0, Sn, Sn * 5 / 2 }, onizleme.Parts.Select(p => p.EdlStart).ToArray());
        Assert.Equal(Sn * 5 / 2 + Sn / 3 - 1, onizleme.EdlDuration);

        Assert.Equal(Sn + Sn / 4, onizleme.ToEdl(Sn + Sn / 4));
        Assert.Equal(Sn + Sn / 4, onizleme.ToTimeline(Sn + Sn / 4));
        Assert.Equal(1, onizleme.PartAtSeconds(1.25).Index);
        Assert.Equal(2, onizleme.PartAtSeconds(2.5).Index);
        Assert.Equal(0, onizleme.PartAtSeconds(double.NaN).Index);
        for (long t = 0; t < onizleme.TimelineDuration; t += 7919)
            Assert.Equal(t, onizleme.ToTimeline(onizleme.ToEdl(t)));
    }

    [Theory]
    [InlineData(@"D:\x,y;z\ş.mp4", 15)]
    [InlineData(@"C:\a=b\%1%!c.mkv", 16)]
    [InlineData("/home/u/klip, son; ver.mov", 26)]
    public void OzelKarakterliYolUzunlukOnekiyleKacar(string yol, int bayt)
    {
        var onizleme = new EdlPreview(yol, Cizelge(new EditClip(0, Sn), new EditClip(2 * Sn, 3 * Sn)));

        Assert.StartsWith($"edl://%{bayt}%{yol},0,1;%{bayt}%{yol},2,1", onizleme.Uri, StringComparison.Ordinal);
        Assert.Equal(bayt, Encoding.UTF8.GetByteCount(yol));
        Oku(onizleme.Uri, out var girdiler);
        Assert.Equal(2, girdiler.Count);
        Assert.All(girdiler, g => Assert.Equal(yol, g.Dosya));
        Assert.Equal("%0%", EdlPreview.Escape(""));
    }

    [Fact]
    public void MotorEdlAdresiniOlduguGibiVerir()
    {
        var uri = new EdlPreview(@"C:\v\a,b.mp4", EditTimeline.FromSource(Sn)).Uri;

        Assert.Equal(uri, MpvEngine.Target(uri));
        Assert.Equal(Path.GetFullPath("klip.mp4"), MpvEngine.Target("klip.mp4"));
    }

    [Fact]
    public async Task HizliKlipKaynakUzunluguylaGirerSuruculHiziYazar()
    {
        var onizleme = new EdlPreview(@"C:\v\a.mp4", Cizelge(new EditClip(0, 2 * Sn, 2m), new EditClip(2 * Sn, 3 * Sn), new EditClip(3 * Sn, 4 * Sn, 0.5m)));

        Oku(onizleme.Uri, out var girdiler);
        Assert.Equal(new[] { ("0", "2"), ("2", "1"), ("3", "1") }, girdiler.Select(g => (g.Bas, g.Uzunluk)).ToArray());
        Assert.Equal(4 * Sn, onizleme.EdlDuration);
        Assert.Equal(4 * Sn, onizleme.TimelineDuration);
        Assert.Equal(Sn, onizleme.ToEdl(Sn / 2));
        Assert.Equal(Sn / 2, onizleme.ToTimeline(Sn));
        Assert.Equal(Sn, onizleme.ToTimeline(2 * Sn));
        Assert.Equal(3 * Sn, onizleme.ToEdl(2 * Sn));

        var motor = new EdlMotoru();
        using var surucu = new EdlPreviewDriver(motor, onizleme, TimeSpan.FromHours(1));
        surucu.Play();
        motor.PositionSeconds = 0.5;
        await surucu.TickAsync(0.05);
        Assert.Equal(2, motor.Speed);
        motor.PositionSeconds = 2.1;
        await surucu.TickAsync(0.05);
        Assert.Equal(1, motor.Speed);
        motor.PositionSeconds = 3.2;
        await surucu.TickAsync(0.05);
        Assert.Equal(0.5, motor.Speed);
        await surucu.TickAsync(0.05);
        Assert.Equal(new[] { 2, 1, 0.5 }, motor.Hizlar);
        Assert.Empty(motor.Aramalar);
        Assert.False(motor.IsPaused);
    }

    [Fact]
    public async Task GeriKlipIleriGirerSurucuGeriyeArar()
    {
        var onizleme = new EdlPreview(@"C:\v\a.mp4", Cizelge(new EditClip(0, Sn), new EditClip(Sn, 2 * Sn, 1m, true), new EditClip(3 * Sn, 4 * Sn)));

        Oku(onizleme.Uri, out var girdiler);
        Assert.Equal(new[] { ("0", "1"), ("1", "1"), ("3", "1") }, girdiler.Select(g => (g.Bas, g.Uzunluk)).ToArray());
        Assert.Equal(2 * Sn - 1, onizleme.ToEdl(Sn));
        Assert.Equal(2 * Sn - 1 - Sn / 4, onizleme.ToEdl(Sn + Sn / 4));
        Assert.Equal(Sn + Sn / 4, onizleme.ToTimeline(2 * Sn - 1 - Sn / 4));
        Assert.Equal(2 * Sn - 1, onizleme.ToTimeline(Sn));

        var motor = new EdlMotoru();
        using var surucu = new EdlPreviewDriver(motor, onizleme, TimeSpan.FromHours(1));
        surucu.Play();
        motor.PositionSeconds = 1.02;
        await surucu.TickAsync(0.05);
        Assert.True(motor.IsPaused);
        Assert.Equal(1, surucu.ReversingPart!.Index);
        Assert.Equal(EditTime.ToSeconds(2 * Sn - 1), motor.Aramalar[^1], 9);
        Assert.Equal(Sn, surucu.TimelinePosition);

        await surucu.TickAsync(0.4);
        await surucu.TickAsync(0.4);
        Assert.Equal(3, motor.Aramalar.Count);
        Assert.True(motor.Aramalar.Zip(motor.Aramalar.Skip(1)).All(p => p.Second < p.First));
        Assert.True(motor.IsPaused);
        Assert.Equal(Sn + Sn * 4 / 5, surucu.TimelinePosition);

        surucu.Pause();
        await surucu.TickAsync(0.4);
        Assert.Equal(3, motor.Aramalar.Count);
        surucu.Play();
        Assert.True(motor.IsPaused);

        await surucu.TickAsync(0.4);
        Assert.Null(surucu.ReversingPart);
        Assert.Equal(2, motor.Aramalar[^1], 9);
        Assert.False(motor.IsPaused);
        Assert.Equal(2 * Sn, surucu.TimelinePosition);

        await surucu.TickAsync(0.05);
        Assert.Null(surucu.ReversingPart);
        Assert.Equal(4, motor.Aramalar.Count);
    }

    [Fact]
    public async Task SurucuEdlAdresiniAcarVeCizelgeAnindanArar()
    {
        var onizleme = new EdlPreview(@"C:\v\a.mp4", Cizelge(new EditClip(0, Sn), new EditClip(Sn, 2 * Sn, 2m, true)));
        var motor = new EdlMotoru();
        using var surucu = new EdlPreviewDriver(motor, onizleme, TimeSpan.FromHours(1));

        await surucu.OpenAsync();
        Assert.Equal(onizleme.Uri, motor.Acilan);

        await surucu.SeekAsync(Sn + Sn / 4);
        Assert.Equal(EditTime.ToSeconds(2 * Sn - 1 - Sn / 2), motor.Aramalar[^1], 9);
        Assert.Equal(1, surucu.ReversingPart!.Index);
        Assert.Equal(2, motor.Speed);

        await surucu.SeekAsync(Sn / 2);
        Assert.Null(surucu.ReversingPart);
        Assert.Equal(1, motor.Speed);
        Assert.Equal(0.5, motor.Aramalar[^1], 9);
    }
}
