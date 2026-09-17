using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class FiltreYoklamaTests
{
    private const string Kaynak = "testsrc2=size=640x360:rate=50";

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "a1", "filtre");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static async Task Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-hide_banner", "-nostdin", "-y", "-loglevel", "error" }.Concat(args)) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var err = p.StandardError.ReadToEndAsync();
        _ = p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        Assert.True(p.ExitCode == 0, await err);
    }

    private static async Task<MediaInfo> Uret(string ad, string vf)
    {
        var yol = Path.Combine(Kanit, ad);
        await Ffmpeg("-f", "lavfi", "-i", Kaynak, "-t", "3", "-vf", vf,
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-threads", "2", yol);
        return await FfprobeClient.ProbeAsync(yol);
    }

    [FfmpegFact]
    public async Task IdetTaramaliKaynagiTaramaliProgressiveKaynagiProgressiveOkur()
    {
        var taramali = await Uret("taramali.mkv", "tinterlace=mode=interleave_top,setfield=tff");
        var duz = await Uret("duz.mkv", "fps=25");
        Assert.True(taramali.Height == 360 && duz.Height == 360, $"{taramali.Width}x{taramali.Height}");

        var t = await InterlaceProbe.RunAsync(taramali);
        var d = await InterlaceProbe.RunAsync(duz);
        Assert.NotNull(t);
        Assert.NotNull(d);
        Assert.True(VideoFilterChain.IdetSaysInterlaced(t!.Value), t.ToString());
        Assert.False(VideoFilterChain.IdetSaysInterlaced(d!.Value), d.ToString());

        var belirsiz = taramali with { IsInterlaced = false, FieldOrder = null };
        Assert.Equal(DeinterlaceMode.On, (await InterlaceProbe.ResolveAsync(belirsiz, VideoFilterOptions.Default)).Deinterlace);
        Assert.Equal(DeinterlaceMode.Off, (await InterlaceProbe.ResolveAsync(duz with { FieldOrder = null }, VideoFilterOptions.Default)).Deinterlace);
    }

    [FfmpegFact]
    public async Task BwdifZinciriTaramayiGiderirVeKareHiziniKorur()
    {
        var taramali = await Uret("taramali-kaynak.mkv", "tinterlace=mode=interleave_top,setfield=tff");
        var yol = Path.Combine(Kanit, "bwdif.mkv");
        await Ffmpeg("-i", taramali.FilePath, "-vf", VideoFilterChain.DeinterlaceChain,
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-threads", "2", yol);
        var cikti = await FfprobeClient.ProbeAsync(yol);

        Assert.Equal(taramali.Fps, cikti.Fps, 1);
        Assert.Equal(360, cikti.Height);
        var sayim = await InterlaceProbe.RunAsync(cikti);
        Assert.NotNull(sayim);
        Assert.False(VideoFilterChain.IdetSaysInterlaced(sayim!.Value), sayim.ToString());
    }

    [FfmpegFact]
    public async Task CropdetectSiyahBantliKaynaktaDogruDikdortgeniBulur()
    {
        var bantli = await Uret("bantli.mkv", "fps=25,pad=640:480:0:60:black");
        Assert.Equal(480, bantli.Height);
        var sonuc = await CropProbe.RunAsync(bantli);
        Assert.Equal(new CropRect(640, 360, 0, 60), sonuc.Rect);
        Assert.True(sonuc.Samples.Count >= 8, sonuc.Samples.Count.ToString());

        var duz = await Uret("bantsiz.mkv", "fps=25");
        Assert.Null((await CropProbe.RunAsync(duz)).Rect);
    }

    [FfmpegFact]
    public async Task ZincirCiktisiFfprobedaBeklenenBoyuttaOkunur()
    {
        var bantli = await Uret("zincir-kaynak.mkv", "fps=25,pad=640:480:0:60:black");
        var filtre = VideoFilterChain.Parse("crop=640:360:0:60,rotate=clock,gray,pad=4:4:0:0,sharpen=light,deblock,denoise=hqdn3d:light,deband,colorspace=bt601");
        Assert.Empty(VideoFilterChain.Validate(bantli, filtre));
        var hedef = VideoFilterChain.PlannedSource(bantli, filtre);
        var plan = new EncodePlan { Codec = "libx264", Mode = "crf", Width = hedef.Width, Height = hedef.Height, Fps = hedef.Fps, Filters = filtre };
        var zincir = string.Join(',', VideoFilterChain.Filters(bantli, plan));

        var yol = Path.Combine(Kanit, "zincir.mkv");
        await Ffmpeg("-i", bantli.FilePath, "-t", "1", "-vf", zincir,
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-threads", "2", yol);
        var cikti = await FfprobeClient.ProbeAsync(yol);

        Assert.Equal(360, cikti.Width);
        Assert.Equal(648, cikti.Height);
    }
}
