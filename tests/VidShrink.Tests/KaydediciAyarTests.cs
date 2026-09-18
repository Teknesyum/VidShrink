using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class KaydediciAyarTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "paket-2");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static readonly string Klasor = Path.Combine(GirdiKanit.Root, ".calisma", "paket-2", "cikis");

    internal static T Bul<T>(RecorderView view, string ad) where T : Control
        => ControlExtensions.FindControl<T>(view, ad)!;

    internal static void Yaz(RecorderView view, string ad, string metin)
    {
        Bul<TextBox>(view, ad).Text = metin;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    internal static void Sec(RecorderView view, string ad, string oge)
    {
        var kutu = Bul<ComboBox>(view, ad);
        var index = kutu.ItemsSource!.Cast<object>().Select(o => o.ToString()).ToList().IndexOf(oge);
        Assert.True(index >= 0, $"{ad} kutusunda {oge} yok");
        kutu.SelectedIndex = index;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    internal static void Elle(RecorderView view)
    {
        Bul<RadioButton>(view, "RadAdvanced").IsChecked = true;
        Bul<RadioButton>(view, "RadManual").IsChecked = true;
    }

    /// <summary>
    /// <para>Kayit oturumu acikken ana panelde degistirilen ayar diske yazilir. Kusur olculdu:
    /// <c>PersistChoices</c> kapisi <c>_session is not null</c> ile geri donuyordu, ama oturum
    /// boyunca ana panelin ayar denetimleri devre disi birakilmiyor (<c>RefreshSerit</c> yalniz
    /// dugmeleri suruyor, <c>CmbContainer</c>'in kosullu etkinlik bagi yok). Gelismis kipte kayit
    /// surerken degistirilen kap sessizce yutuluyordu; mini panel bunu yalniz kendi bes kutusu icin
    /// <c>ApplyMiniOption</c> icindeki yinelenmis kaydetme daliyla telafi ediyordu. Iki yol tek
    /// <c>SaveChoicesIfChanged</c>'e indirildi, kapidan <c>_session</c> cikti.</para>
    /// </summary>
    [Fact]
    public void OturumSurerkenDegisenAyarDiskeYazilir()
    {
        var (oturumsuz, oturumlu) = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var view = new RecorderView();
            Elle(view);
            Sec(view, "CmbContainer", "MP4");
            var once = DosyadakiDeger("containerChoice");

            var alan = typeof(RecorderView).GetField("_session",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            alan.SetValue(view, System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(VidShrink.Ffmpeg.RecorderSession)));
            try
            {
                Sec(view, "CmbContainer", "MOV");
                return (once, DosyadakiDeger("containerChoice"));
            }
            finally
            {
                alan.SetValue(view, null);
            }
        }));

        Assert.Equal("\"Mp4\"", oturumsuz);
        Assert.Equal("\"Mov\"", oturumlu);
    }

    internal static string? Deger(IReadOnlyList<string> args, string bayrak)
    {
        var i = args.ToList().IndexOf(bayrak);
        return i >= 0 && i + 1 < args.Count ? args[i + 1] : null;
    }

    private static readonly object AyarKapisi = new();

    /// <summary>
    /// Ayar dosyasini bosaltip olcumu kosar, sonra dosyayi oldugu gibi geri koyar. Dosya
    /// surec basina tek oldugu icin bosaltma-kosum-gerikoyma ucusu <see cref="AyarKapisi"/>
    /// ile boluenemez yapiliyor: xUnit sinif duzeyinde paralel kostugundan, kilitsiz
    /// surumde bir sinifin yazdigi secim baska bir sinifin "dosya bos" beklentisine
    /// karisiyordu. Kilit ayni is parcaciginda yeniden girilebilir, ic ice kullanim guvenli.
    /// </summary>
    internal static T AyarDosyasiyla<T>(Func<T> olc)
    {
        lock (AyarKapisi)
        {
            var dosya = RecorderSettings.FilePath!;
            var onceki = File.Exists(dosya) ? File.ReadAllBytes(dosya) : null;
            try
            {
                if (File.Exists(dosya)) File.Delete(dosya);
                return olc();
            }
            finally
            {
                if (onceki is null) File.Delete(dosya);
                else File.WriteAllBytes(dosya, onceki);
            }
        }
    }

    private static string? DosyadakiDeger(string anahtar)
    {
        var dosya = RecorderSettings.FilePath!;
        if (!File.Exists(dosya)) return null;
        return JsonNode.Parse(File.ReadAllText(dosya))?[anahtar]?.ToJsonString();
    }

    internal sealed record Satir(
        string Ad,
        Action<RecorderView> Degistir,
        string Anahtar,
        string Beklenen,
        Func<RecorderRequest, IReadOnlyList<string>, string, string?> Arguman,
        Func<IReadOnlyDictionary<string, string>, string?>? Probe = null);

    internal sealed record Olcu(string? Dosyada, string? Sorun, IReadOnlyList<string> Args, RecorderRequest? Istek, string? Yol);

    private static Olcu Olc(Satir satir) => AyarDosyasiyla(() => AppHost.Run(() =>
    {
        var once = new RecorderView();
        Elle(once);
        satir.Degistir(once);

        var dosyada = DosyadakiDeger(satir.Anahtar);

        var sonra = new RecorderView();
        if (sonra.PrepareRecording() is not { } hazir)
            return new Olcu(dosyada, "istek kurulmadi: " + sonra.ErrorText, Array.Empty<string>(), null, null);

        IReadOnlyList<string> args;
        try { args = RecorderArguments.Build(hazir.Request, hazir.Path); }
        catch (InvalidOperationException ex) { return new Olcu(dosyada, "arguman kurulmadi: " + ex.Message, Array.Empty<string>(), hazir.Request, hazir.Path); }
        return new Olcu(dosyada, satir.Arguman(hazir.Request, args, hazir.Path), args, hazir.Request, hazir.Path);
    }));

    private static string? Oku(IReadOnlyDictionary<string, string> p, string anahtar) => p.TryGetValue(anahtar, out var v) ? v : null;

    [Fact]
    public void AcilistaOtomatikKipBirKezOlcerElleVeBassizdaOlcmez()
    {
        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var sayac = 0;
            var otomatik = new RecorderView { OpenMeasure = () => { sayac++; return Task.CompletedTask; } };
            otomatik.MeasureOnOpenAsync(true).GetAwaiter().GetResult();
            otomatik.MeasureOnOpenAsync(true).GetAwaiter().GetResult();
            var otomatikSayi = sayac;

            sayac = 0;
            var bassiz = new RecorderView { OpenMeasure = () => { sayac++; return Task.CompletedTask; } };
            bassiz.MeasureOnOpenAsync(false).GetAwaiter().GetResult();
            var bassizSayi = sayac;

            sayac = 0;
            var elle = new RecorderView { OpenMeasure = () => { sayac++; return Task.CompletedTask; } };
            Elle(elle);
            elle.MeasureOnOpenAsync(true).GetAwaiter().GetResult();
            return (otomatikSayi, bassizSayi, elleSayi: sayac, elle.AutoMode);
        }));

        Assert.Equal((1, 0, 0, false), olcu);
    }

    internal static IEnumerable<Satir> Satirlar()
    {
        yield return new("kare hizi", v => Yaz(v, "TxtFps", "24"), "fps", "24",
            (_, a, _) => Deger(a, "-framerate") == "24" ? null : "-framerate " + Deger(a, "-framerate"));
        yield return new("kalite", v => { Sec(v, "CmbCodec", "libx264"); Yaz(v, "TxtQuality", "31"); }, "quality", "31",
            (_, a, _) => Deger(a, "-crf") == "31" ? null : "-crf " + Deger(a, "-crf"));
        yield return new("kodlayici", v => Sec(v, "CmbCodec", "libx265"), "codec", "\"libx265\"",
            (_, a, _) => Deger(a, "-c:v") == "libx265" ? null : "-c:v " + Deger(a, "-c:v"),
            p => Oku(p, "codec_name") == "hevc" ? null : "codec_name " + Oku(p, "codec_name"));
        yield return new("on ayar", v => Sec(v, "CmbPreset", "superfast"), "preset", "\"superfast\"",
            (_, a, _) => Deger(a, "-preset") == "superfast" ? null : "-preset " + Deger(a, "-preset"));
        yield return new("imlec", v => Bul<CheckBox>(v, "ChkCursor").IsChecked = !(Bul<CheckBox>(v, "ChkCursor").IsChecked ?? false),
            "showCursor", "!", (_, _, _) => null);
        yield return new("pencere hedefi", v => { Bul<ComboBox>(v, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Window; Yaz(v, "TxtWindowTitle", "Not Defteri"); },
            "windowTitle", "\"Not Defteri\"",
            (_, a, _) => Deger(a, "-i") == "title=Not Defteri" ? null : "-i " + Deger(a, "-i"));
        yield return new("bolge", v =>
        {
            Bul<ComboBox>(v, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Region;
            Yaz(v, "TxtRegionX", "10");
            Yaz(v, "TxtRegionY", "20");
            Yaz(v, "TxtRegionWidth", "640");
            Yaz(v, "TxtRegionHeight", "360");
        }, "regionWidth", "640",
            (_, a, _) => Deger(a, "-video_size") == "640x360" && Deger(a, "-offset_x") == "10" && Deger(a, "-offset_y") == "20"
                ? null : $"-video_size {Deger(a, "-video_size")} -offset_x {Deger(a, "-offset_x")} -offset_y {Deger(a, "-offset_y")}");
        yield return new("cikis klasoru", v => Yaz(v, "TxtOutputFolder", Klasor), "outputFolder", JsonSerializer.Serialize(Klasor),
            (_, _, yol) => yol.StartsWith(Klasor, StringComparison.OrdinalIgnoreCase) ? null : "yol " + yol);
        yield return new("kap", v => Sec(v, "CmbContainer", "MP4"), "containerChoice", "\"Mp4\"",
            (_, a, yol) => yol.EndsWith(".mp4", StringComparison.Ordinal) && Deger(a, "-movflags") == "+faststart" ? null : "yol " + yol,
            p => Oku(p, "format_name")?.Contains("mp4", StringComparison.Ordinal) == true ? null : "format_name " + Oku(p, "format_name"));
        yield return new("olcek", v => { Yaz(v, "TxtScaleWidth", "320"); Yaz(v, "TxtScaleHeight", "180"); }, "scaleWidth", "320",
            (_, a, _) => Deger(a, "-vf") == "scale=320:180" ? null : "-vf " + Deger(a, "-vf"),
            p => Oku(p, "width") == "320" && Oku(p, "height") == "180" ? null : $"{Oku(p, "width")}x{Oku(p, "height")}");
        yield return new("anahtar kare", v => { Yaz(v, "TxtFps", "30"); Yaz(v, "TxtKeyframe", "5"); }, "keyframeSeconds", "5",
            (_, a, _) => Deger(a, "-g") == "150" ? null : "-g " + Deger(a, "-g"));
        yield return new("profil", v => { Sec(v, "CmbCodec", "libx264"); Sec(v, "CmbProfile", "main"); }, "profile", "\"main\"",
            (_, a, _) => Deger(a, "-profile:v") == "main" ? null : "-profile:v " + Deger(a, "-profile:v"),
            p => Oku(p, "profile") == "Main" ? null : "profile " + Oku(p, "profile"));
        yield return new("tune", v => { Sec(v, "CmbCodec", "libx264"); Sec(v, "CmbTune", "film"); }, "tune", "\"film\"",
            (_, a, _) => Deger(a, "-tune") == "film" ? null : "-tune " + Deger(a, "-tune"));
        yield return new("piksel bicimi", v => { Sec(v, "CmbCodec", "libx264"); Sec(v, "CmbPixelFormat", "yuv444p"); }, "pixelFormat", "\"yuv444p\"",
            (_, a, _) => Deger(a, "-pix_fmt") == "yuv444p" ? null : "-pix_fmt " + Deger(a, "-pix_fmt"),
            p => Oku(p, "pix_fmt") == "yuv444p" ? null : "pix_fmt " + Oku(p, "pix_fmt"));
        yield return new("bit hizi", v =>
        {
            Sec(v, "CmbCodec", "libx264");
            Bul<ComboBox>(v, "CmbRateControl").SelectedIndex = (int)RecorderRateControl.Bitrate;
            Yaz(v, "TxtBitrate", "4000");
            Yaz(v, "TxtMaxBitrate", "4500");
            Yaz(v, "TxtBuffer", "9000");
        }, "bitrateKbps", "4000",
            (_, a, _) => Deger(a, "-b:v")?.StartsWith("4000", StringComparison.Ordinal) == true
                && Deger(a, "-maxrate")?.StartsWith("4500", StringComparison.Ordinal) == true
                && Deger(a, "-bufsize")?.StartsWith("9000", StringComparison.Ordinal) == true
                && Deger(a, "-crf") is null
                ? null : $"-b:v {Deger(a, "-b:v")} -maxrate {Deger(a, "-maxrate")} -bufsize {Deger(a, "-bufsize")} -crf {Deger(a, "-crf")}",
            p => new[] { Oku(p, "stream_bit_rate"), Oku(p, "bit_rate") }.Any(s => long.TryParse(s, out var bit) && bit > 2_000_000)
                ? null : $"bit_rate {Oku(p, "stream_bit_rate")} / {Oku(p, "bit_rate")}");
        yield return new("renk uzayi", v => Sec(v, "CmbColorSpace", "bt709"), "colorSpace", "\"bt709\"",
            (_, a, _) => Deger(a, "-colorspace") == "bt709" ? null : "-colorspace " + Deger(a, "-colorspace"),
            p => Oku(p, "color_space") == "bt709" ? null : "color_space " + Oku(p, "color_space"));
        yield return new("renk araligi", v => Sec(v, "CmbColorRange", "pc"), "colorRange", "\"pc\"",
            (_, a, _) => Deger(a, "-color_range") == "pc" ? null : "-color_range " + Deger(a, "-color_range"),
            p => Oku(p, "color_range") == "pc" ? null : "color_range " + Oku(p, "color_range"));
        yield return new("sure siniri", v => Yaz(v, "TxtMaxDuration", "3"), "maxDurationSeconds", "3",
            (_, a, _) => Deger(a, "-t") == "3" ? null : "-t " + Deger(a, "-t"),
            p => double.TryParse(Oku(p, "duration"), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d is > 2.5 and < 3.5
                ? null : "duration " + Oku(p, "duration"));
        yield return new("sureyle bolme", v => Yaz(v, "TxtSplitSeconds", "2"), "splitSeconds", "2",
            (r, _, _) => r.Split?.Duration == TimeSpan.FromSeconds(2) ? null : "split " + r.Split);
        yield return new("boyutla bolme", v => Yaz(v, "TxtSplitMegabytes", "5"), "splitMegabytes", "5",
            (r, _, _) => r.Split?.Megabytes == 5 ? null : "split " + r.Split);
    }

    [Fact]
    public void HerAyarBaslatmadanDosyayaVeSonrakiKaydinArgumaninaGecer()
    {
        var sorunlar = new List<string>();
        var satirlar = new List<string>();
        var bos = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var once = new RecorderView();
            Elle(once);
            var sonra = new RecorderView();
            var hazir = sonra.PrepareRecording()!.Value;
            var dosya = JsonNode.Parse(File.ReadAllText(RecorderSettings.FilePath!))!.AsObject();
            return (Args: RecorderArguments.Build(hazir.Request, hazir.Path), hazir.Request, hazir.Path, Dosya: dosya);
        }));

        foreach (var satir in Satirlar())
        {
            var olcu = Olc(satir);
            var beklenen = satir.Beklenen == "!"
                ? (bos.Dosya[satir.Anahtar]!.GetValue<bool>() ? "false" : "true")
                : satir.Beklenen;

            if (olcu.Dosyada != beklenen) sorunlar.Add($"{satir.Ad}: dosyada {olcu.Dosyada ?? "yok"}, beklenen {beklenen}");
            if (olcu.Sorun is { } s) sorunlar.Add($"{satir.Ad}: {s}");
            if (bos.Dosya[satir.Anahtar]?.ToJsonString() == beklenen)
                sorunlar.Add($"{satir.Ad}: degistirilmeyen ayar da {beklenen}, olcu ayirt etmiyor");
            if (satir.Beklenen != "!" && satir.Arguman(bos.Request, bos.Args, bos.Path) is null)
                sorunlar.Add($"{satir.Ad}: degistirilmeyen kayit da argumani geciriyor, olcu ayirt etmiyor");

            satirlar.Add($"{satir.Ad} | {satir.Anahtar}={olcu.Dosyada} | {olcu.Sorun ?? "arguman tamam"} | {string.Join(' ', olcu.Args)}");
        }

        var imlec = Olc(Satirlar().Single(s => s.Anahtar == "showCursor"));
        var bosImlec = Deger(bos.Args, "-draw_mouse");
        if (Deger(imlec.Args, "-draw_mouse") == bosImlec)
            sorunlar.Add($"imlec: -draw_mouse degismedi ({bosImlec})");

        satirlar.Add($"degistirilmeyen | {string.Join(' ', bos.Args)}");
        File.WriteAllLines(Path.Combine(Kanit, "ayar-arguman.txt"), satirlar);
        Assert.True(sorunlar.Count == 0, string.Join(Environment.NewLine, sorunlar));
    }

    [Fact]
    public void SesKollariSonrakiAcilisinSesPlaninaGecer()
    {
        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var once = new RecorderView();
            Elle(once);
            Bul<ComboBox>(once, "CmbAudioLayout").SelectedIndex = (int)AudioTrackLayout.SeparateTracks;
            Yaz(once, "TxtAudioGain", "-3.5");
            Bul<CheckBox>(once, "ChkNoiseGate").IsChecked = true;
            Bul<CheckBox>(once, "ChkNoiseSuppression").IsChecked = true;
            var dosya = File.ReadAllText(RecorderSettings.FilePath!);
            var sonra = new RecorderView();
            return (dosya, sonra.Settings.AudioLayout, sonra.Settings.AudioFilters,
                Kutular: (Bul<ComboBox>(sonra, "CmbAudioLayout").SelectedIndex, Bul<TextBox>(sonra, "TxtAudioGain").Text,
                    Bul<CheckBox>(sonra, "ChkNoiseGate").IsChecked, Bul<CheckBox>(sonra, "ChkNoiseSuppression").IsChecked));
        }));

        File.WriteAllText(Path.Combine(Kanit, "ayar-ses.txt"), olcu.dosya);
        Assert.Equal(AudioTrackLayout.SeparateTracks, olcu.AudioLayout);
        Assert.Equal(new AudioFilterOptions(-3.5, true, true), olcu.AudioFilters);
        Assert.Equal(((int)AudioTrackLayout.SeparateTracks, (string?)"-3.5", (bool?)true, (bool?)true), olcu.Kutular);
    }

    [Fact]
    public void GeriSayimVeKipSonrakiAcilistaKalir()
    {
        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var once = new RecorderView();
            var ilk = (once.SelectedCountdown, once.AdvancedMode, once.ManualMode);
            Elle(once);
            Bul<ComboBox>(once, "CmbCountdown").SelectedIndex = 2;
            var sonra = new RecorderView();
            return (ilk, son: (sonra.SelectedCountdown, sonra.AdvancedMode, sonra.ManualMode));
        }));

        Assert.NotEqual(olcu.ilk, olcu.son);
        Assert.Equal((RecorderSettings.CountdownChoices[2], true, true), olcu.son);
    }

    internal static RecorderAutoChoice Aday()
        => RecorderAutoPlan.Candidates(new RecorderMachine(1920, 1080, 60, 8, Array.Empty<string>()))[0];

    [Theory]
    [InlineData("MP4", ".mp4")]
    [InlineData("MOV", ".mov")]
    [InlineData("GIF", ".gif")]
    public void OtomatikKipKullanicininKabiniKorurVeKayitDogrulanir(string kap, string uzanti)
    {
        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var once = new RecorderView();
            Bul<RadioButton>(once, "RadAdvanced").IsChecked = true;
            Sec(once, "CmbContainer", kap);
            Bul<CheckBox>(once, "ChkCursor").IsChecked = false;
            Yaz(once, "TxtMaxDuration", "4");

            var sonra = new RecorderView();
            sonra.AutoChoice = Aday() with { Fps = 60 };
            var hazir = sonra.PrepareRecording();
            return (sonra.AutoMode, Hazir: hazir, sonra.ErrorText,
                Hatalar: hazir is { } h ? RecorderArguments.Validate(h.Request, h.Path) : Array.Empty<string>());
        }));

        Assert.True(olcu.AutoMode);
        Assert.True(olcu.Hazir is not null, "istek kurulmadi: " + olcu.ErrorText);
        var (istek, yol) = olcu.Hazir!.Value;
        Assert.EndsWith(uzanti, yol);
        Assert.Empty(olcu.Hatalar);
        Assert.Equal(Aday().VideoCodec, istek.VideoCodec);
        Assert.False(istek.ShowCursor);
        Assert.Equal(TimeSpan.FromSeconds(4), istek.MaxDuration);
        Assert.Equal(kap == "GIF" ? GifPalette.MaxFps : 60, istek.Fps);
    }

    [Fact]
    public void OlcumKosmadanOtomatikKipIlkAdayiYazar()
    {
        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var once = new RecorderView();
            Elle(once);
            Sec(once, "CmbCodec", "libx265");
            Yaz(once, "TxtFps", "7");
            var elle = once.BuildRequest()!;
            Bul<RadioButton>(once, "RadSimple").IsChecked = true;

            var sonra = new RecorderView();
            var beklenen = RecorderAutoPlan.Candidates(sonra.Machine())[0];
            return (elle, sonra.AutoMode, sonra.AdvancedMode, otomatik: sonra.BuildRequest()!, beklenen);
        }));

        Assert.Equal(("libx265", 7), (olcu.elle.VideoCodec, olcu.elle.Fps));
        Assert.True(olcu.AutoMode);
        Assert.False(olcu.AdvancedMode);
        Assert.Equal((olcu.beklenen.VideoCodec, olcu.beklenen.Fps), (olcu.otomatik.VideoCodec, olcu.otomatik.Fps));
    }

    [Fact]
    public void OtomatikKipteYalnizPrograminYazdigiKollarGizli()
    {
        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var view = new RecorderView();
            Elle(view);
            var elle = Gorunenler(view);
            view.AutoChoice = Aday();
            view.SkipAutoMeasure = true;
            Bul<RadioButton>(view, "RadAuto").IsChecked = true;
            return (elle, otomatik: Gorunenler(view));
        }));

        Assert.Equal((true, true, true, true, true), olcu.elle);
        Assert.Equal((false, false, true, true, true), olcu.otomatik);
    }

    private static (bool Kodlama, bool Elle, bool Kap, bool Imlec, bool Sure) Gorunenler(RecorderView v)
        => (Bul<StackPanel>(v, "PanelAdvancedEncoding").IsVisible,
            Bul<StackPanel>(v, "PanelManualOptions").IsVisible,
            Gorunur(v, Bul<ComboBox>(v, "CmbContainer")),
            Gorunur(v, Bul<CheckBox>(v, "ChkCursor")),
            Gorunur(v, Bul<TextBox>(v, "TxtMaxDuration")));

    private static bool Gorunur(RecorderView view, Control kontrol)
    {
        for (Avalonia.Visual? v = kontrol; v is not null && !ReferenceEquals(v, view); v = Avalonia.VisualTree.VisualExtensions.GetVisualParent(v))
            if (!v.IsVisible) return false;
        Avalonia.StyledElement? e = kontrol;
        for (; e is not null && !ReferenceEquals(e, view); e = e.Parent)
            if (e is Control c && !c.IsVisible) return false;
        return true;
    }

    [Fact]
    public void OtomatikKipteHedefBoyutBitHizinaVeSureyeGecer()
    {
        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var once = new RecorderView();
            Bul<RadioButton>(once, "RadAdvanced").IsChecked = true;
            Yaz(once, "TxtTargetSeconds", "30");
            Yaz(once, "TxtTargetMegabytes", "10");
            var sonra = new RecorderView();
            sonra.AutoChoice = Aday();
            var hazir = sonra.PrepareRecording()!.Value;
            return RecorderArguments.Build(hazir.Request, hazir.Path);
        }));

        Assert.Equal("30", Deger(olcu, "-t"));
        Assert.NotNull(Deger(olcu, "-b:v"));
        Assert.Null(Deger(olcu, "-crf"));
    }

    private static IReadOnlyList<string> Lavfi(IReadOnlyList<string> args)
    {
        var liste = args.ToList();
        var gdigrab = liste.IndexOf("gdigrab");
        var girdi = gdigrab < 0 ? -1 : liste.IndexOf("-i", gdigrab);
        Assert.True(gdigrab > 0 && girdi > gdigrab, "gdigrab girdisi yok: " + string.Join(' ', args));
        liste.RemoveRange(2, girdi);
        liste.InsertRange(2, new[] { "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=30" });
        if (!liste.Contains("-t")) liste.InsertRange(liste.Count - 1, new[] { "-t", "1" });
        return liste;
    }

    private static Dictionary<string, string> Ffprobe(string dosya)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffprobe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
                 {
                     "-hide_banner", "-v", "error", "-select_streams", "v:0",
                     "-show_entries", "stream=codec_name,width,height,pix_fmt,profile,r_frame_rate,avg_frame_rate,color_space,color_range,bit_rate",
                     "-show_entries", "format=duration,bit_rate,format_name",
                     "-of", "default=nw=1", dosya
                 })
            psi.ArgumentList.Add(arg);

        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var metin = surec.StandardOutput.ReadToEnd();
        surec.WaitForExit(10_000);
        _ = hata.Result;

        var sonuc = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var satir in metin.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()))
        {
            var esit = satir.IndexOf('=');
            if (esit <= 0) continue;
            var anahtar = satir[..esit];
            if (anahtar == "bit_rate" && !sonuc.ContainsKey("stream_bit_rate") && !sonuc.ContainsKey("duration")) anahtar = "stream_bit_rate";
            sonuc.TryAdd(anahtar, satir[(esit + 1)..]);
        }

        return sonuc;
    }

    [KayitFact]
    public async Task HerAyarinEtkisiCiktidaFfprobeIleGorunur()
    {
        var klasor = Path.Combine(Kanit, "cikti");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);

        var kosular = new List<(string Ad, IReadOnlyList<string> Args, Func<IReadOnlyDictionary<string, string>, string?> Probe)>();
        foreach (var satir in Satirlar().Where(s => s.Probe is not null))
        {
            var olcu = Olc(satir with { Degistir = v => { satir.Degistir(v); Yaz(v, "TxtOutputFolder", klasor); } });
            Assert.True(olcu.Istek is not null, satir.Ad + ": " + olcu.Sorun);
            kosular.Add((satir.Ad, Lavfi(olcu.Args), satir.Probe!));
        }

        var bos = Olc(new Satir("degistirilmeyen", v => Yaz(v, "TxtOutputFolder", klasor), "outputFolder", "", (_, _, _) => null));
        var bosArgs = Lavfi(bos.Args);

        var kanit = new List<string>();
        var sorunlar = new List<string>();

        var bosKosu = await FfmpegRunner.RunAsync(bosArgs);
        Assert.True(bosKosu.Ok, bosKosu.StandardError);
        var bosProbe = Ffprobe(bosArgs[^1]);
        kanit.Add("degistirilmeyen | " + string.Join(' ', bosArgs) + " | " + string.Join(' ', bosProbe.Select(p => p.Key + "=" + p.Value)));

        foreach (var (ad, args, probe) in kosular)
        {
            var kosu = await FfmpegRunner.RunAsync(args);
            var bilgi = kosu.Ok ? Ffprobe(args[^1]) : new Dictionary<string, string>();
            var sonuc = kosu.Ok ? probe(bilgi) : "ffmpeg: " + kosu.StandardError;
            if (sonuc is not null) sorunlar.Add($"{ad}: {sonuc}");
            if (probe(bosProbe) is null) sorunlar.Add($"{ad}: degistirilmeyen cikti da geciyor, olcu ayirt etmiyor");
            kanit.Add($"{ad} | {sonuc ?? "ffprobe tamam"} | {string.Join(' ', args)} | {string.Join(' ', bilgi.Select(p => p.Key + "=" + p.Value))}");
        }

        File.WriteAllLines(Path.Combine(Kanit, "ayar-ffprobe.txt"), kanit);
        Directory.Delete(klasor, true);
        Assert.True(sorunlar.Count == 0, string.Join(Environment.NewLine, sorunlar));
    }

    [KayitFact]
    public async Task ArayuzdenKurulanGercekKayitAyarlariTasir()
    {
        var klasor = Path.Combine(Kanit, "canli");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);

        var hazir = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var once = new RecorderView();
            Elle(once);
            Bul<ComboBox>(once, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Region;
            Yaz(once, "TxtRegionX", "0");
            Yaz(once, "TxtRegionY", "0");
            Yaz(once, "TxtRegionWidth", "640");
            Yaz(once, "TxtRegionHeight", "360");
            Sec(once, "CmbCodec", "libx264");
            Sec(once, "CmbPreset", "ultrafast");
            Yaz(once, "TxtFps", "15");
            Yaz(once, "TxtScaleWidth", "320");
            Yaz(once, "TxtScaleHeight", "180");
            Yaz(once, "TxtSplitSeconds", "2");
            Yaz(once, "TxtOutputFolder", klasor);
            return new RecorderView().PrepareRecording()!.Value;
        }));

        var oturum = await RecorderSession.StartAsync(hazir.Request, hazir.Path);
        await Task.Delay(4500);
        var sonuc = await oturum.StopAsync();

        var dosyalar = sonuc.Files ?? new[] { sonuc.OutputPath };
        var kanit = new List<string> { string.Join(' ', RecorderArguments.Build(hazir.Request, hazir.Path)), $"ok={sonuc.Ok} parca={sonuc.Segments}" };
        var olculer = dosyalar.Select(d => (Dosya: d, Bilgi: Ffprobe(d))).ToList();
        kanit.AddRange(olculer.Select(o => o.Dosya + " | " + string.Join(' ', o.Bilgi.Select(p => p.Key + "=" + p.Value))));
        File.WriteAllLines(Path.Combine(Kanit, "ayar-canli.txt"), kanit);

        Assert.True(sonuc.Ok, sonuc.StandardError);
        Assert.True(dosyalar.Count >= 2, $"2 sn bolme 4,5 sn kayitta en az iki dosya vermeli, {dosyalar.Count} verdi");
        foreach (var (dosya, bilgi) in olculer)
        {
            Assert.StartsWith(klasor, dosya, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(("h264", "320", "180"), (Oku(bilgi, "codec_name"), Oku(bilgi, "width"), Oku(bilgi, "height")));
        }
        Assert.Equal("15", Deger(RecorderArguments.Build(hazir.Request, hazir.Path), "-framerate"));

        Directory.Delete(klasor, true);
    }
}
