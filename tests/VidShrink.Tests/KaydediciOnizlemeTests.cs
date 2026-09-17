using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// Paket 2b T13, canlı önizleme: kayıt süreci ikinci bir çıkışa saniyede bir 320 px genişliğinde
/// <c>image2 -update 1</c> jpg yazar, şeridin altındaki resim onu okur. Boyut sınırıyla birlikte
/// reddedilir: <c>-fs</c> kaydı kapatınca ffmpeg önizleme çıkışı için çalışmayı sürdürüyor (ölçüldü,
/// 12 sn sonra öldürüldü). Canlı kol 3 sn'lik lavfi kaydında jpg'nin genişliğini ffprobe'la okur;
/// önizlemesiz argüman negatif kontrol. Kanıt <c>.calisma/paket-2b/onizleme/</c>.
/// </summary>
public sealed class KaydediciOnizlemeTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "paket-2b", "onizleme");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static RecorderRequest Istek() => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Fps = 15,
        Region = new RecorderRegion(0, 0, 640, 360),
        Container = RecorderContainer.Mkv,
        Preset = "ultrafast"
    };

    [Fact]
    public void OnizlemeKayittanSonraIkinciCikisOlurBoyutSiniriylaReddedilir()
    {
        var jpg = Path.Combine("k", "on.jpg");
        Assert.Empty(RecorderArguments.Validate(Istek() with { PreviewPath = jpg, MaxDuration = TimeSpan.FromSeconds(3) }, "a.mkv"));
        var yok = RecorderArguments.Build(Istek(), "a.mkv");
        var var = RecorderArguments.Build(Istek() with { PreviewPath = jpg, MaxDuration = TimeSpan.FromSeconds(3) }, "a.mkv");

        Assert.Empty(RecorderArguments.PreviewArgs(Istek()));
        Assert.DoesNotContain("image2", yok);
        Assert.Equal("a.mkv", yok[^1]);

        var kayitSonu = var.ToList().IndexOf("a.mkv");
        Assert.Equal(
            new[] { "-map", "0:v", "-vf", "fps=1,scale=320:-2", "-an", "-t", "3", "-f", "image2", "-update", "1", "-q:v", "6", jpg },
            var.Skip(kayitSonu + 1));
        Assert.Equal("3", Deger(var.Take(kayitSonu).ToList(), "-t"));

        var mac = RecorderArguments.PreviewArgs(Istek() with { Platform = RecorderPlatform.MacOs, Region = new RecorderRegion(10, 20, 640, 360), PreviewPath = jpg });
        Assert.Equal("crop=640:360:10:20,fps=1,scale=320:-2", Deger(mac, "-vf"));

        Assert.Empty(RecorderArguments.Validate(Istek() with { PreviewPath = jpg }, "a.mkv"));
        Assert.Contains(RecorderArguments.Validate(Istek() with { PreviewPath = jpg, MaxMegabytes = 5 }, "a.mkv"), e => e.Contains("size limit"));
        Assert.Contains(RecorderArguments.Validate(Istek() with { PreviewPath = Path.Combine("k", "on.png") }, "a.mkv"), e => e.Contains(".jpg"));
    }

    private static List<string> LavfiRe(IReadOnlyList<string> args)
    {
        var liste = args.ToList();
        var gdigrab = liste.IndexOf("gdigrab");
        var girdi = liste.IndexOf("-i", gdigrab);
        Assert.True(gdigrab > 0 && girdi > gdigrab, string.Join(' ', args));
        liste.RemoveRange(gdigrab - 1, girdi - gdigrab + 3);
        liste.InsertRange(gdigrab - 1, new[] { "-re", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=15" });
        liste.InsertRange(liste.IndexOf("-c:v"), new[] { "-threads", "1" });
        return liste;
    }

    internal static void Kos(IReadOnlyList<string> args, int ms)
    {
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(ms))
        {
            surec.Kill(true);
            Assert.Fail($"ffmpeg {ms} ms icinde bitmedi: " + string.Join(' ', args));
        }

        surec.WaitForExit();
        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
    }

    internal static string Probe(string dosya, string alan)
    {
        var psi = new ProcessStartInfo("ffprobe", $"-v error -select_streams v:0 -show_entries {alan} -of csv=p=0 \"{dosya}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var surec = Process.Start(psi)!;
        var metin = surec.StandardOutput.ReadToEnd();
        Assert.True(surec.WaitForExit(15000));
        return metin.Trim();
    }

    [Fact]
    public void CanliKayitOnizlemeKaresiniYazarOnizlemesizYazmaz()
    {
        foreach (var eski in Directory.GetFiles(Kanit)) File.Delete(eski);
        var jpg = Path.Combine(Kanit, "onizleme.jpg");
        var kayit = Path.Combine(Kanit, "kayit.mkv");
        var bos = Path.Combine(Kanit, "bos.mkv");
        var sure = TimeSpan.FromSeconds(3);

        var args = LavfiRe(RecorderArguments.Build(Istek() with { PreviewPath = jpg, MaxDuration = sure }, kayit));
        var bosArgs = LavfiRe(RecorderArguments.Build(Istek() with { MaxDuration = sure }, bos));
        File.WriteAllLines(Path.Combine(Kanit, "args.txt"), new[] { string.Join(' ', args), string.Join(' ', bosArgs) });

        Kos(args, 20000);
        Kos(bosArgs, 20000);

        var genislik = File.Exists(jpg) ? Probe(jpg, "stream=width") : "yok";
        var kayitSure = Probe(kayit, "format=duration");
        File.WriteAllLines(Path.Combine(Kanit, "olcu.txt"), new[]
        {
            $"onizleme genislik={genislik} bayt={(File.Exists(jpg) ? new FileInfo(jpg).Length : 0)}",
            $"kayit sure={kayitSure}",
            $"onizlemesiz jpg sayisi={Directory.GetFiles(Kanit, "*.jpg").Length - (File.Exists(jpg) ? 1 : 0)}"
        });

        Assert.Equal("320", genislik);
        Assert.InRange(double.Parse(kayitSure, CultureInfo.InvariantCulture), 2.5, 3.5);
        Assert.Single(Directory.GetFiles(Kanit, "*.jpg"));
        Assert.True(File.Exists(bos));
    }

    [Fact]
    public void KutuIsaretliyseIstegeYolGirerResimOkunurBozukKareEskisiniKorur()
    {
        var kaynak = Path.Combine(Kanit, "kaynak.jpg");
        if (!File.Exists(kaynak))
            Kos(new[] { "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=1", "-frames:v", "1", kaynak }, 15000);
        var jpg = Path.Combine(Kanit, "arayuz.jpg");

        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var kapali = new RecorderView { PreviewLocation = () => jpg };
            var kapaliYol = kapali.PrepareRecording()?.Request.PreviewPath;
            Bul<CheckBox>(kapali, "ChkLivePreview").IsChecked = true;

            var acik = new RecorderView { PreviewLocation = () => jpg };
            var kutu = Bul<CheckBox>(acik, "ChkLivePreview").IsChecked;
            var acikYol = acik.PrepareRecording()?.Request.PreviewPath;
            Yaz(acik, "TxtTargetMegabytes", "5");
            var sinirliYol = acik.BuildRequest()?.PreviewPath;
            Yaz(acik, "TxtTargetMegabytes", string.Empty);

            acik.PreparePreview(jpg);
            var dosyaYokken = acik.RefreshPreview();
            File.Copy(kaynak, jpg, true);
            var okundu = acik.RefreshPreview();
            var ilk = acik.PreviewImage;
            var genislik = ilk?.PixelSize.Width;
            File.WriteAllBytes(jpg, new byte[] { 1, 2, 3 });
            var bozuk = acik.RefreshPreview();
            var ayni = ReferenceEquals(ilk, acik.PreviewImage);
            return (kapaliYol, kutu, acikYol, sinirliYol, dosyaYokken, okundu, genislik, gorunur: acik.PreviewVisible, bozuk, ayni);
        }));

        Assert.Null(olcu.kapaliYol);
        Assert.True(olcu.kutu);
        Assert.Equal(jpg, olcu.acikYol);
        Assert.Null(olcu.sinirliYol);
        Assert.False(olcu.dosyaYokken);
        Assert.True(olcu.okundu);
        Assert.Equal(320, olcu.genislik);
        Assert.True(olcu.gorunur);
        Assert.False(olcu.bozuk);
        Assert.True(olcu.ayni);
    }
}
