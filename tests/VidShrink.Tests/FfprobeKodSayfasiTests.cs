using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// ffprobe ve ffmpeg metni UTF-8 okunur (<see cref="ToolLocator.StartInfo"/>). Canli kol
/// <c>[FfmpegFact]</c>: 2 sn 160x90 lavfi klibine (<c>-threads 2</c>) ffmetadata ile Turkce ve
/// Japonca bolum adi yazilir, adin UTF-8 baytlari dosyada aranir (olumlu kontrol) ve
/// <see cref="FfprobeClient.ProbeAsync"/>'in okudugu ad bayt bayt karsilastirilir. Ikinci kol
/// stderr'i olcer: olmayan, ASCII disi adli girdinin adi hata metninde aynen gecer.
/// Kanit <c>.calisma/ffprobe-kod-sayfasi/</c>, test siler.
/// </summary>
public sealed class FfprobeKodSayfasiTests
{
    private static readonly string[] Adlar = { "Giriş", "Bölüm Üç ğüşıöçİĞ", "日本語の章 第三" };

    private static string Klasor()
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "ffprobe-kod-sayfasi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static void Sil(string klasor)
    {
        try { Directory.Delete(klasor, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string Meta()
    {
        var metin = new StringBuilder(";FFMETADATA1\n");
        for (var i = 0; i < Adlar.Length; i++)
            metin.Append("[CHAPTER]\nTIMEBASE=1/1000\nSTART=").Append(i * 600).Append("\nEND=").Append((i + 1) * 600)
                .Append("\ntitle=").Append(Adlar[i]).Append('\n');
        return metin.ToString();
    }

    [FfmpegFact]
    public async Task BolumAdiBaytBaytAyniOkunur()
    {
        var klasor = Klasor();
        try
        {
            var meta = Path.Combine(klasor, "bolumler.txt");
            File.WriteAllText(meta, Meta(), new UTF8Encoding(false));
            var dosya = Path.Combine(klasor, "bolumlu.mkv");
            var uret = await FfmpegRunner.RunAsync(new[]
            {
                "-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "testsrc=size=160x90:rate=15:duration=2",
                "-f", "ffmetadata", "-i", meta, "-map", "0:v", "-map_chapters", "1",
                "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", dosya
            });
            Assert.True(uret.Ok, uret.StandardError);

            var ham = File.ReadAllBytes(dosya);
            Assert.All(Adlar, ad => Assert.True(ham.AsSpan().IndexOf(Encoding.UTF8.GetBytes(ad)) >= 0, ad));
            Assert.All(Adlar, ad => Assert.Contains(ad, c => c > 127));

            var info = await FfprobeClient.ProbeAsync(dosya);

            Assert.Equal(Adlar.Length, info.Chapters.Count);
            for (var i = 0; i < Adlar.Length; i++)
                Assert.Equal(Encoding.UTF8.GetBytes(Adlar[i]), Encoding.UTF8.GetBytes(info.Chapters[i].Title!));
        }
        finally { Sil(klasor); }
    }

    [FfmpegFact]
    public async Task HataMetnindeAsciiDisiDosyaAdiBozulmaz()
    {
        var klasor = Klasor();
        try
        {
            var olmayan = Path.Combine(klasor, "yok-Bölüm-Üç-日本語.mkv");

            var kos = await FfmpegRunner.RunAsync(new[] { "-hide_banner", "-nostdin", "-threads", "2", "-i", olmayan, "-f", "null", "-" });

            Assert.False(kos.Ok);
            Assert.Contains("yok-Bölüm-Üç-日本語.mkv", kos.StandardError);
        }
        finally { Sil(klasor); }
    }
}
