using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Duzenleyici teslim ayarlari ve on ayarlari (DZB-54, DZB-55, DZB-58; <see cref="EditExportSettings"/>,
/// <see cref="EditExportPresetLibrary"/>, <c>EditorView.TeslimAyar.cs</c>). Varsayilan ayarla teslimin argumanlari
/// bugunku diziyle harfi harfine ayni (Tam, sessiz Tam, birlestirme; Hizli ve Akilli kip yerinde kalir); ayar
/// verilince tuval, <c>fps=</c>, kodek, CRF ve ses bit hizi argumana iner, Hizli/Akilli Tam'a duser ve not yazilir;
/// yalniz ses teslimi <c>-vn</c> ile m4a/mp3 yazar, sessiz kaynakta istisna atar. Canli kol 1 sn 320x240 klibi
/// 1280x720, 24 kare, HEVC'ye ve yalniz sese (m4a, mp3) cevirir. Kanit <c>.calisma/worktree-agent-a89ed6f47c31808ec/teslim-ayar-*</c>, test siler.
/// </summary>
public sealed class DuzenleyiciTeslimAyarTests
{
    private const long Gb = 1_000_000_000;

    private static readonly double[] Kareler = { 0.0, 2, 4, 6, 8 };

    private static string Kok => Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-a89ed6f47c31808ec");

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(Kok, "teslim-ayar-" + ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Kapat(string klasor)
    {
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        KanitKapanisi.Kapat(Kok);
    }

    private static MediaInfo Bilgi(string yol, int en, int boy, double fps, bool ses, double sure = 10) => new()
    {
        FilePath = yol,
        FileSizeBytes = 1_000_000,
        DurationSeconds = sure,
        Width = en,
        Height = boy,
        Fps = fps,
        VideoCodec = "h264",
        AudioCodec = ses ? "aac" : null,
        TotalBitrateBps = 1_000_000,
        PixelFormat = "yuv420p",
        BitDepth = 8,
        FormatName = "mov,mp4,m4a,3gp,3g2,mj2"
    };

    private static EditExportSettings Ayar720 => new()
    {
        Width = 1280, Height = 720, Fps = 24, VideoCodec = "libx265", Crf = 23, AudioKbps = 128
    };

    private static string[] Argumanlar(ExportPlan plan) => Assert.Single(plan.Steps).Args.ToArray();

    private static string Suzgec(ExportPlan plan)
    {
        var args = Argumanlar(plan);
        var i = Array.IndexOf(args, "-filter_complex");
        Assert.True(i >= 0, string.Join(' ', args));
        return args[i + 1];
    }

    private static string[] Kuyruk(ExportPlan plan)
    {
        var args = Argumanlar(plan);
        return args.Skip(Array.IndexOf(args, "-filter_complex") + 2).ToArray();
    }

    private static ExportPlan Tek(ExportMode kip, string cikti = "cikti.mp4", EditExportSettings? ayar = null, bool ses = true, EditTimeline? model = null)
        => EditExport.Build(model ?? EditTimeline.FromSource(S(10)), Bilgi("kaynak.mp4", 320, 240, 30, ses), Kareler, 0, kip, cikti, "is", 8 * Gb,
            kip == ExportMode.Smart ? DuzenleyiciAkilliKodekTests.Sinirlar(Kareler) : null, _ => true, settings: ayar);

    private static Dictionary<int, ExportSource> IkiKaynak(bool ikinciSesli = false) => new()
    {
        [0] = new ExportSource(Bilgi("a.mp4", 320, 240, 30, true), new[] { 0.0 }, 0),
        [1] = new ExportSource(Bilgi("b.mp4", 160, 120, 25, ikinciSesli, 4), new[] { 0.0 }, 0)
    };

    private static EditTimeline IkiKaynakliModel()
    {
        var model = EditTimeline.FromSource(S(10));
        Assert.Equal(1, model.AddSource(S(4)));
        return model;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VarsayilanTamTeslimBugunkuArgumanlariYazar(bool varsayilanVerilir)
    {
        var plan = Tek(ExportMode.Full, ayar: varsayilanVerilir ? EditExportSettings.Default : null);

        Assert.Equal(new[]
        {
            "-hide_banner", "-nostdin", "-y", "-i", "kaynak.mp4",
            "-filter_complex", "[0:v]trim=start=0:end=10,setpts=PTS-STARTPTS[v0];[0:a]atrim=start=0:end=10,asetpts=PTS-STARTPTS[a0];[v0][a0]concat=n=1:v=1:a=1[vout][aout]",
            "-map", "[vout]", "-map", "[aout]",
            "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", "cikti.mp4"
        }, Argumanlar(plan));
        Assert.False(plan.SettingsForcedFull);
        Assert.Equal(ExportMode.Full, plan.Effective);
    }

    [Fact]
    public void VarsayilanSessizMkvTeslimBugunkuArgumanlariYazar()
    {
        var plan = Tek(ExportMode.Full, "cikti.mkv", EditExportSettings.Default, ses: false);

        Assert.Equal(new[]
        {
            "-hide_banner", "-nostdin", "-y", "-i", "kaynak.mp4",
            "-filter_complex", "[0:v]trim=start=0:end=10,setpts=PTS-STARTPTS[v0];[v0]concat=n=1:v=1:a=0[vout]",
            "-map", "[vout]", "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p", "cikti.mkv"
        }, Argumanlar(plan));
    }

    [Theory]
    [InlineData(ExportMode.Fast)]
    [InlineData(ExportMode.Smart)]
    public void VarsayilanAyarHizliVeAkilliKipiYerindeBirakir(ExportMode kip)
    {
        var bos = Tek(kip);
        var varsayilan = Tek(kip, ayar: EditExportSettings.Default);

        Assert.Equal(kip, varsayilan.Effective);
        Assert.False(varsayilan.SettingsForcedFull);
        Assert.Equal(bos.Steps.Select(s => string.Join('\u0001', s.Args)), varsayilan.Steps.Select(s => string.Join('\u0001', s.Args)));
        Assert.DoesNotContain(varsayilan.Steps.SelectMany(s => s.Args), a => a == "-filter_complex");
    }

    [Fact]
    public void VarsayilanBirlestirmeBugunkuKodlamaArgumanlariniYazar()
    {
        var bos = EditExport.Build(IkiKaynakliModel(), IkiKaynak(), ExportMode.Full, "cikti.mp4", "is", 8 * Gb);
        var plan = EditExport.Build(IkiKaynakliModel(), IkiKaynak(), ExportMode.Full, "cikti.mp4", "is", 8 * Gb, settings: EditExportSettings.Default);

        Assert.Equal(Argumanlar(bos), Argumanlar(plan));
        Assert.Equal(new[]
        {
            "-map", "[vout]", "-map", "[aout]",
            "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", "cikti.mp4"
        }, Kuyruk(plan));
        Assert.Contains(",fps=30,format=yuv420p", Suzgec(plan));
        Assert.Contains("320", Suzgec(plan));
    }

    [Theory]
    [InlineData("libx264", "medium")]
    [InlineData("libx265", "medium")]
    [InlineData("libsvtav1", "8")]
    public void AyarlarTamTeslimeTuvalKareHiziKodekKaliteVeSesOlarakIner(string kodek, string onAyar)
    {
        var plan = Tek(ExportMode.Full, ayar: Ayar720 with { VideoCodec = kodek });
        var suzgec = Suzgec(plan);

        Assert.Equal(new[]
        {
            "-map", "[vout]", "-map", "[aout]",
            "-c:v", kodek, "-preset", onAyar, "-crf", "23", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart", "cikti.mp4"
        }, Kuyruk(plan));
        Assert.Contains("1280", suzgec);
        Assert.Contains("720", suzgec);
        var fps = suzgec.IndexOf(",fps=24", StringComparison.Ordinal);
        Assert.True(fps > suzgec.IndexOf("setpts=PTS-STARTPTS", StringComparison.Ordinal), suzgec);
        Assert.True(fps < suzgec.IndexOf("[v0]", StringComparison.Ordinal), suzgec);
        Assert.DoesNotContain(",fps=", Suzgec(Tek(ExportMode.Full, ayar: Ayar720 with { Fps = 0 })));
    }

    [Fact]
    public void AyarlarBirlestirmedeTuvaliKareHiziniVeKodegiDegistirir()
    {
        var plan = EditExport.Build(IkiKaynakliModel(), IkiKaynak(), ExportMode.Smart, "cikti.mp4", "is", 8 * Gb, settings: Ayar720);
        var suzgec = Suzgec(plan);

        Assert.True(plan.MergeForcedFull);
        Assert.Contains(",fps=24,format=yuv420p", suzgec);
        Assert.DoesNotContain(",fps=30", suzgec);
        Assert.Contains("1280", suzgec);
        Assert.Equal(new[]
        {
            "-map", "[vout]", "-map", "[aout]",
            "-c:v", "libx265", "-preset", "medium", "-crf", "23", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart", "cikti.mp4"
        }, Kuyruk(plan));
    }

    [Theory]
    [InlineData(ExportMode.Fast, true)]
    [InlineData(ExportMode.Smart, true)]
    [InlineData(ExportMode.Full, false)]
    public void AyarHizliVeAkilliKipiTamaGecirirVeNotYazar(ExportMode kip, bool zorlandi)
    {
        var eski = Strings.Language;
        try
        {
            Strings.Use("tr");
            var plan = Tek(kip, ayar: new EditExportSettings { Crf = 23 });

            Assert.Equal(ExportMode.Full, plan.Effective);
            Assert.Equal(kip, plan.Requested);
            Assert.Equal(zorlandi, plan.SettingsForcedFull);
            Assert.Equal(zorlandi, EditorView.Notes(new[] { plan }).Contains(Strings.Get("editor.export.settings-full"), StringComparison.Ordinal));
            Assert.DoesNotContain(Strings.Get("editor.export.settings-full"), EditorView.Notes(new[] { Tek(kip) }));
        }
        finally
        {
            Strings.Use(eski);
        }
    }

    [Fact]
    public void ParcalarAyarZorlamasiniButunCizelgedenTasir()
    {
        var model = EditTimeline.FromSource(S(10));
        Assert.True(model.Split(S(5)));
        var bilgi = Bilgi("kaynak.mp4", 320, 240, 30, true);

        var parcalar = EditExport.BuildSegments(model, bilgi, Kareler, 0, ExportMode.Fast, new[] { "p1.mp4", "p2.mp4" }, new[] { "i1", "i2" }, 8 * Gb,
            settings: new EditExportSettings { Width = 1280, Height = 720 });
        Assert.Equal(2, parcalar.Count);
        Assert.All(parcalar, p =>
        {
            Assert.True(p.SettingsForcedFull);
            Assert.Equal(ExportMode.Fast, p.Requested);
            Assert.Equal(ExportMode.Full, p.Effective);
            Assert.Contains("1280", Suzgec(p));
        });

        var varsayilan = EditExport.BuildSegments(model, bilgi, Kareler, 0, ExportMode.Fast, new[] { "p1.mp4", "p2.mp4" }, new[] { "i1", "i2" }, 8 * Gb,
            settings: EditExportSettings.Default);
        Assert.All(varsayilan, p => Assert.False(p.SettingsForcedFull));
    }

    [Theory]
    [InlineData("cikti.m4a", "aac", true)]
    [InlineData("cikti.mp3", "libmp3lame", false)]
    [InlineData("cikti.mkv", "aac", false)]
    public void YalnizSesTeslimiGoruntusuzKodlar(string cikti, string kodek, bool faststart)
    {
        var plan = Tek(ExportMode.Fast, cikti, new EditExportSettings { AudioOnly = true, AudioKbps = 160 });

        var beklenen = new List<string>
        {
            "-hide_banner", "-nostdin", "-y", "-i", "kaynak.mp4",
            "-filter_complex", "[0:a]atrim=start=0:end=10,asetpts=PTS-STARTPTS[a0];[a0]concat=n=1:v=0:a=1[aout]",
            "-map", "[aout]", "-vn", "-c:a", kodek, "-b:a", "160k"
        };
        if (faststart) beklenen.AddRange(new[] { "-movflags", "+faststart" });
        beklenen.Add(cikti);

        Assert.Equal(beklenen, Argumanlar(plan));
        Assert.True(plan.SettingsForcedFull);
        Assert.Equal(ExportMode.Full, plan.Effective);
    }

    [Fact]
    public void YalnizSesSessizKaynaktaIstisnaAtar()
    {
        var ayar = new EditExportSettings { AudioOnly = true };

        Assert.Throws<EditExportNoAudioException>(() => Tek(ExportMode.Full, "cikti.m4a", ayar, ses: false));
        Assert.Equal("editor.export.audio-only.no-audio", EditExportNoAudioException.LocaleKey);
        var sessiz = new Dictionary<int, ExportSource>
        {
            [0] = new ExportSource(Bilgi("a.mp4", 320, 240, 30, false), new[] { 0.0 }, 0),
            [1] = new ExportSource(Bilgi("b.mp4", 160, 120, 25, false, 4), new[] { 0.0 }, 0)
        };
        Assert.Throws<EditExportNoAudioException>(() => EditExport.Build(IkiKaynakliModel(), sessiz, ExportMode.Full, "cikti.m4a", "is", 8 * Gb, settings: ayar));
        Assert.NotNull(Tek(ExportMode.Full, "cikti.m4a", ayar));
    }

    [Fact]
    public void YalnizSesBirlestirmedeSessizParcayaSessizlikKoyar()
    {
        var plan = EditExport.Build(IkiKaynakliModel(), IkiKaynak(), ExportMode.Full, "cikti.m4a", "is", 8 * Gb,
            settings: new EditExportSettings { AudioOnly = true });
        var suzgec = Suzgec(plan);

        Assert.Equal(
            "[0:a]atrim=start=0:end=10,asetpts=PTS-STARTPTS," + EditExport.MergeAudioFormat + "[a0];"
            + EditExport.MergeSilence + ",atrim=duration=4[a1];[a0][a1]concat=n=2:v=0:a=1[aout]", suzgec);
        Assert.Equal(new[] { "-map", "[aout]", "-vn", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", "cikti.m4a" }, Kuyruk(plan));
        Assert.Equal(new[] { "a.mp4", "b.mp4" }, Argumanlar(plan).Select((a, i) => (a, i)).Where(x => x.a == "-i").Select(x => Argumanlar(plan)[x.i + 1]));
    }

    [Fact]
    public void HazirOnAyarlarVeDosyaGidisDonusu()
    {
        Assert.Equal(new[] { "1080p", "720p", "vertical" }, EditExportPresetLibrary.BuiltIn.Select(p => p.Id));
        Assert.Equal(new[] { (1920, 1080), (1280, 720), (1080, 1920) }, EditExportPresetLibrary.BuiltIn.Select(p => (p.Settings.Width, p.Settings.Height)));
        Assert.All(EditExportPresetLibrary.BuiltIn, p => Assert.Equal(EditExportSettings.Default with { Width = p.Settings.Width, Height = p.Settings.Height }, p.Settings));
        Assert.True(EditExportSettings.Default.IsDefault);
        Assert.False((EditExportSettings.Default with { AudioKbps = 128 }).IsDefault);

        var klasor = Klasor("dosya");
        try
        {
            var yol = Path.Combine(klasor, EditExportPresetLibrary.UserFileName);
            Assert.Empty(EditExportPresetLibrary.LoadUser(yol));

            var benim = new EditExportPreset { Id = "Benim", Name = "Benim", Settings = new EditExportSettings { Fps = 30, VideoCodec = "libx265", Crf = 28, AudioKbps = 128, AudioOnly = true } };
            EditExportPresetLibrary.SaveUser(benim, yol);
            var metin = File.ReadAllText(yol);
            Assert.Contains("\"kind\": \"" + EditExportPresetLibrary.FileKind + "\"", metin);
            Assert.Equal(benim, Assert.Single(EditExportPresetLibrary.LoadUser(yol)));

            var yeni = benim with { Id = "benim", Settings = EditExportSettings.Default };
            var liste = EditExportPresetLibrary.SaveUser(yeni, yol);
            Assert.Equal(yeni, Assert.Single(liste));
            Assert.Equal(yeni, Assert.Single(EditExportPresetLibrary.LoadUser(yol)));

            var kucult = PresetLibrary.Serialize(Array.Empty<PresetProfile>());
            Assert.Equal("kind", Assert.Throws<PresetFileException>(() => EditExportPresetLibrary.Parse(kucult)).Detail);

            Assert.Equal("presets[0].id", Assert.Throws<PresetFileException>(() => EditExportPresetLibrary.SaveUser(benim with { Id = "720P" }, yol)).Detail);
            Assert.Equal("presets[0].settings.width", Assert.Throws<PresetFileException>(() => EditExportPresetLibrary.SaveUser(benim with { Settings = new EditExportSettings { Width = 1281, Height = 720 } }, yol)).Detail);
            Assert.Equal("presets[0].settings.fps", Assert.Throws<PresetFileException>(() => EditExportPresetLibrary.SaveUser(benim with { Settings = new EditExportSettings { Fps = 500 } }, yol)).Detail);
            Assert.Equal("presets[0].settings.videoCodec", Assert.Throws<PresetFileException>(() => EditExportPresetLibrary.SaveUser(benim with { Settings = new EditExportSettings { VideoCodec = "uydurma" } }, yol)).Detail);
            Assert.Equal(yeni, Assert.Single(EditExportPresetLibrary.LoadUser(yol)));

            var elle = metin.Replace("\"crf\": 28", "\"crf\": 99", StringComparison.Ordinal);
            Assert.NotEqual(metin, elle);
            Assert.Equal("presets[0].settings.crf", Assert.Throws<PresetFileException>(() => EditExportPresetLibrary.Parse(elle)).Detail);
        }
        finally
        {
            Kapat(klasor);
        }
    }

    private static EditorView Gorunum()
    {
        var view = new EditorView
        {
            KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", AudioCodec = "aac", TotalBitrateBps = 1 },
            Projects = null,
            PeakReader = (_, _) => Task.FromResult(AudioPeaks.Empty),
            KeyframeReader = (_, _) => Task.FromResult<IReadOnlyList<double>>(Array.Empty<double>()),
        };
        view.Player.EngineFactory = () => new KlipMotoru();
        return view;
    }

    private static void Bekle(EditorView view, Task gorev)
    {
        DenetimSurucu.Pump(view.Player, () => gorev.IsCompleted, 10);
        Assert.True(gorev.IsCompleted, "gorev 10 sn icinde bitmedi");
        gorev.GetAwaiter().GetResult();
    }

    private static string Anahtar(EditorView view)
    {
        var yontem = typeof(EditorView).GetMethod("SaveKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)yontem.Invoke(view, new object[] { view.Model! })!;
    }

    private static void Sec(ComboBox kutu, string etiket)
        => kutu.SelectedItem = kutu.Items.OfType<ComboBoxItem>().Single(i => i.Tag as string == etiket);

    [Fact]
    public void ArayuzOnAyarUygularElleDegisiklikOzeleDonerKayitHatalariniSoyler()
    {
        var klasor = Klasor("arayuz");
        var eski = Strings.Language;
        try
        {
            Strings.Use("tr");
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            File.WriteAllBytes(kaynak, new byte[16]);
            var dosya = Path.Combine(klasor, EditExportPresetLibrary.UserFileName);

            AppHost.Run(() =>
            {
                var view = Gorunum();
                view.ExportPresetPathOverride = dosya;
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    Bekle(view, view.OpenSourceAsync(kaynak));
                    Assert.True(view.ExportSettings.IsDefault);
                    Assert.Equal(string.Empty, view.ExportPresetId);
                    var ilk = Anahtar(view);

                    view.ExportPresetId = "720p";
                    Assert.Equal(EditExportPresetLibrary.BuiltIn[1].Settings, view.ExportSettings);
                    Assert.Equal("720p", view.ExportPresetId);
                    var hazir = Anahtar(view);
                    Assert.NotEqual(ilk, hazir);

                    Sec(view.CmbExportFps, "24");
                    Assert.Equal(string.Empty, view.ExportPresetId);
                    Assert.Equal(EditExportPresetLibrary.BuiltIn[1].Settings with { Fps = 24 }, view.ExportSettings);
                    Assert.NotEqual(hazir, Anahtar(view));

                    view.ExportSettings = EditExportSettings.Default;
                    Assert.Equal(ilk, Anahtar(view));

                    Assert.False(view.SaveExportPreset("  "));
                    Assert.Equal(Strings.Get("main.preset.name-empty"), view.ExportPresetError);
                    Assert.False(view.SaveExportPreset("720P"));
                    Assert.Equal(string.Format(Strings.Culture, Strings.Get("main.preset.error.reserved-id"), "720P"), view.ExportPresetError);
                    Assert.False(File.Exists(dosya));

                    Sec(view.CmbExportQuality, "28");
                    Assert.True(view.SaveExportPreset("Benim"));
                    Assert.Equal(string.Empty, view.ExportPresetError);
                    Assert.Equal("Benim", view.ExportPresetId);
                    Assert.Equal(28, Assert.Single(EditExportPresetLibrary.LoadUser(dosya)).Settings.Crf);

                    Sec(view.CmbExportQuality, "23");
                    Assert.False(view.SaveExportPreset("benim"));
                    Assert.Equal(string.Format(Strings.Culture, Strings.Get("main.preset.overwrite"), "benim"), view.ExportPresetError);
                    Assert.Equal(28, Assert.Single(EditExportPresetLibrary.LoadUser(dosya)).Settings.Crf);
                    Assert.True(view.SaveExportPreset("benim"));
                    Assert.Equal(23, Assert.Single(EditExportPresetLibrary.LoadUser(dosya)).Settings.Crf);

                    view.ExportPresetId = "720p";
                    Assert.Equal(18, view.ExportSettings.Crf);
                    view.ExportPresetId = "benim";
                    Assert.Equal(23, view.ExportSettings.Crf);

                    Assert.Equal(".mp4", Path.GetExtension(view.DefaultOutput()));
                    view.ChkExportAudioOnly.IsChecked = true;
                    Assert.True(view.ExportSettings.AudioOnly);
                    Assert.Equal(".m4a", Path.GetExtension(view.DefaultOutput()));
                    view.ChkExportAudioOnly.IsChecked = false;
                    Assert.Equal(".mp4", Path.GetExtension(view.DefaultOutput()));
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }

                var ikinci = Gorunum();
                ikinci.ExportPresetPathOverride = dosya;
                Assert.Equal("benim", Assert.Single(ikinci.UserExportPresets).Id);
                Assert.True(ikinci.ExportSettings.IsDefault);
            });
        }
        finally
        {
            Strings.Use(eski);
            Kapat(klasor);
        }
    }

    [Fact]
    public void TeslimAyarMetinleri42DildeVar()
    {
        var anahtarlar = new[]
        {
            "editor.export.preset", "editor.export.preset.custom", "editor.export.preset.1080p", "editor.export.preset.720p",
            "editor.export.preset.vertical", "editor.export.preset.name", "editor.export.resolution", "editor.export.fps",
            "editor.export.codec", "editor.export.quality", "editor.export.quality.high", "editor.export.quality.balanced",
            "editor.export.quality.small", "editor.export.audio-bitrate", "editor.export.source", "editor.export.audio-only",
            "editor.export.audio-only.tip", "editor.export.audio-only.no-audio", "editor.export.settings-full"
        };

        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var mevcut = Strings.KeysOf(dil).ToHashSet(StringComparer.Ordinal);
            foreach (var anahtar in anahtarlar)
            {
                Assert.True(mevcut.Contains(anahtar), $"{dil}: {anahtar} yok");
                var metin = Strings.GetIn(dil, anahtar);
                Assert.False(string.IsNullOrWhiteSpace(metin), $"{dil}: {anahtar} bos");
                Assert.DoesNotContain("{0}", metin);
            }
        }
    }

    private static void Ffmpeg(params string[] args) => Calistir("ffmpeg", args);

    private static string Calistir(string arac, params string[] args)
    {
        var psi = new ProcessStartInfo(arac) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(60000))
        {
            surec.Kill(true);
            Assert.Fail(arac + " 60 sn icinde bitmedi.");
        }

        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
        return cikti.GetAwaiter().GetResult();
    }

    private static string Akislar(string dosya, string tur)
        => Calistir("ffprobe", "-v", "error", "-select_streams", tur, "-show_entries", "stream=codec_name", "-of", "csv=p=0", dosya).Trim();

    private static ExportPlan IkiIsParcacigi(ExportPlan plan)
    {
        var adim = Assert.Single(plan.Steps);
        var args = adim.Args.Take(adim.Args.Count - 1).Concat(new[] { "-threads", "2" }).Append(adim.Args[^1]).ToArray();
        return plan with { Steps = new[] { adim with { Args = args } } };
    }

    [FfmpegFact]
    public async Task CanliAyarlarVeYalnizSesDosyayaIner()
    {
        var klasor = Klasor("canli");
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            Ffmpeg("-hide_banner", "-y",
                "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=1",
                "-f", "lavfi", "-i", "sine=f=440:d=1",
                "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-threads", "2", "-c:a", "aac", "-shortest", kaynak);
            var bilgi = await FfprobeClient.ProbeAsync(kaynak);
            var model = EditTimeline.FromSource(EditTime.FromSeconds(bilgi.DurationSeconds));

            var video = Path.Combine(klasor, "ayarli.mp4");
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, model, ExportMode.Smart, video, 8 * Gb, settings: Ayar720);
            Assert.True(plan.SettingsForcedFull);
            await EditExportRunner.RunAsync(IkiIsParcacigi(plan), null);
            var sonuc = await FfprobeClient.ProbeAsync(video);
            Assert.Equal(1280, sonuc.Width);
            Assert.Equal(720, sonuc.Height);
            Assert.Equal(24.0, sonuc.Fps, 1);
            Assert.Equal("hevc", sonuc.VideoCodec);
            Assert.True(sonuc.HasAudio);

            foreach (var (uzanti, ses) in new[] { (".m4a", "aac"), (".mp3", "mp3") })
            {
                var cikti = Path.Combine(klasor, "ses" + uzanti);
                var sesPlan = await EditExportRunner.PrepareAsync(new[] { kaynak }, model, ExportMode.Fast, cikti, 8 * Gb,
                    settings: new EditExportSettings { AudioOnly = true, AudioKbps = 128 });
                await EditExportRunner.RunAsync(IkiIsParcacigi(sesPlan), null);
                Assert.Equal(string.Empty, Akislar(cikti, "v"));
                Assert.Equal(ses, Akislar(cikti, "a"));
            }

            Assert.Empty(Directory.GetFiles(klasor, "vidshrink_partial_*"));
        }
        finally
        {
            Kapat(klasor);
        }
    }
}
