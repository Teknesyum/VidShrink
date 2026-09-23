using VidShrink.Cli;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// CLI plan metni iz notlarını yazıyor: arayüzün gerekçe satırında görünen her
/// <see cref="StreamNote"/> CLI'da da bir satır. Not adı tek yerde
/// (<see cref="StreamNotes.Slug"/>); iki yüzün anahtarı ondan türüyor.
/// </summary>
public sealed class CliIzNotuTests : IDisposable
{
    private readonly string _klasor = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        ".calisma", "test-ciktilari", "cli-iz-notu", Guid.NewGuid().ToString("N")[..8]);

    public CliIzNotuTests() => Directory.CreateDirectory(_klasor);

    public void Dispose()
    {
        try { Directory.Delete(Path.GetFullPath(_klasor), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static MediaInfo Kaynak(int sesIzi) => new()
    {
        FilePath = "kaynak.mkv",
        FileSizeBytes = 400_000_000L,
        DurationSeconds = 600,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        Streams = new[] { new SourceStream(0, StreamKind.Video, "h264") }
            .Concat(Enumerable.Range(1, sesIzi).Select(i => new SourceStream(i, StreamKind.Audio, "aac", Channels: 2)))
            .ToArray(),
    };

    private async Task<string> PlanMetni(int sesIzi)
    {
        var servisler = new CliServices
        {
            MissingTool = () => null,
            Probe = (_, _) => Task.FromResult(Kaynak(sesIzi)),
            Availability = () => null,
            UserPresets = () => Array.Empty<PresetProfile>(),
        };
        var dosya = Path.Combine(_klasor, "kaynak.mkv");
        await File.WriteAllTextAsync(dosya, "x");
        var stdout = new StringWriter();
        var exit = await CliApp.RunAsync(new[] { "plan", dosya, "--olcumsuz", "--hedef", "25" }, stdout, new StringWriter(),
            CliText.ForLanguage("tr"), servisler, CancellationToken.None);
        Assert.Equal(0, exit);
        return stdout.ToString();
    }

    /// <summary>İki ses izli kaynakta düşen iz satırı yazılıyor; tek izde satır yok (olumsuz kontrol).</summary>
    [Fact]
    public async Task DusenIzPlanMetnindeYaziyor()
    {
        var metin = CliText.ForLanguage("tr");
        var satir = metin.Format("plan.stream-note", metin["plan.stream." + StreamNotes.Slug(StreamNote.ExtraAudioDropped)]);

        var iki = await PlanMetni(2);
        var tek = await PlanMetni(1);

        Assert.Contains(satir, iki, StringComparison.Ordinal);
        Assert.DoesNotContain("İzler:", tek, StringComparison.Ordinal);
    }

    /// <summary>Her not iki CLI dilinde de metin taşıyor; arayüz anahtarı aynı addan türüyor.</summary>
    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    public void HerNotunIkiYuzdeMetniVar(string dil)
    {
        var metin = CliText.ForLanguage(dil);
        var arayuz = Locales.Domain(dil, "main");
        foreach (var note in Enum.GetValues<StreamNote>())
        {
            var anahtar = "plan.stream." + StreamNotes.Slug(note);
            Assert.False(string.IsNullOrWhiteSpace(metin[anahtar]) || metin[anahtar] == anahtar, $"{dil}: {anahtar}");
            Assert.True(arayuz.ContainsKey(VidShrink.App.MainWindow.StreamNoteKey(note)), $"{dil}: {note}");
        }
        Assert.Equal(Enum.GetValues<StreamNote>().Length,
            Enum.GetValues<StreamNote>().Select(StreamNotes.Slug).Distinct().Count());
    }

    /// <summary>CLI'da olmayan "İzleri koru" kutusu CLI metninde açılacak bir seçenek gibi anlatılmıyor.</summary>
    [Theory]
    [InlineData("en", "turn on")]
    [InlineData("tr", "açın")]
    public void CliMetniOlmayanSecenegiIstemiyor(string dil, string emir)
    {
        var metin = CliText.ForLanguage(dil);
        foreach (var note in Enum.GetValues<StreamNote>())
            Assert.DoesNotContain(emir, metin["plan.stream." + StreamNotes.Slug(note)], StringComparison.Ordinal);
    }
}
