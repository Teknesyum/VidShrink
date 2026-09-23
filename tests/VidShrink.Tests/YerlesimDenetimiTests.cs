using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Performance;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Başsız yerleşim denetçisi. Pencere dil × boyut kolunda açılır, her sekme (gizli Gelişmiş
/// dahil) seçilir, katlanır bölümler açılır ve ağacın tamamı dört soruya karşı taranır:
///
/// <list type="bullet">
/// <item><b>kesik</b> — tek satırlık metnin doğal genişliği kendi yerinden büyük, çok satırlı
/// metnin yüksekliği kutusuna sığmıyor, ya da metin <c>ClipToBounds</c> açık bir atanın
/// dışına düşüyor. Üç noktayla kısalıp tam halini balonda taşıyan metin T194 kararı gereği
/// kesik sayılmaz, ayrı "balonlu" satırına yazılır. İstisna <see cref="SutunIzgara"/>: o
/// panel sütun sayısını içerik sığsın diye seçer, orada üç nokta kusurdur (2026-09-23:
/// "balonlu" satırlarının tamamı bilgi ızgarasındaydı).</item>
/// <item><b>çakışma</b> — aynı panelin görünür iki kardeşi 1 pikselden fazla kesişiyor.</item>
/// <item><b>taşma</b> — çocuk ebeveyninin sınırından dışarı çıkıyor.</item>
/// <item><b>şerit</b> — sayfa üst şeridin altına giriyor; kaydırılan içerik saydam sekme
/// düğmelerinin arasından görünüyor.</item>
/// <item><b>başlık</b> — yalnız tr ve en: başlık, etiket ve düğme yazısında küçük harfle
/// başlayan içerik sözcüğü (Title Case kuralı; diğer dillerin kendi büyük harf kuralı var).</item>
/// </list>
///
/// <para>Dikdörtgenler <see cref="Visual.TranslatePoint"/> ile değil <see cref="Visual.Bounds"/>
/// zinciriyle bulunur: giriş canlandırmasının <c>translateY</c>'si başsız koşumda hiç geri
/// alınmıyor ve <c>TranslatePoint</c> onu hesaba kattığı için yalan söylüyor. Kardeşler aynı
/// ebeveynin uzayında durduğundan <c>Bounds</c> kesişimi doğrudan karşılaştırılabilir.
/// Pencereye ayrıca <c>reduced-motion</c> sınıfı verilir ve dönüşümler silinir.</para>
///
/// <para>Dil açıkça <see cref="Strings.Use"/> ile kurulur ve sekme başlığından geri okunur;
/// başsız pencere <c>OnWindowLoaded</c> görmediği için kendiliğinden İngilizce kalıyordu.</para>
/// </summary>
public sealed class YerlesimDenetimiTests
{
    private readonly ITestOutputHelper _output;

    public YerlesimDenetimiTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Türkçe, İngilizce ve dil dosyalarındaki metinlerin karakter toplamına göre en uzun üç
    /// dil (it 49981, el 49937, fr 49436; en 42610, tr 40806 — 2026-09-22 sayımı).
    /// </summary>
    internal static readonly string[] Diller = { "tr", "en", "it", "el", "fr" };

    /// <summary>
    /// Dar kol makineden bağımsız: pencerenin bildirdiği taban 1136x720, ama açılış onu çalışma
    /// alanına indiriyor (<c>MainWindow.StartupFit</c>) ve 1024 genişlikli ekranda taban 1024 oluyor.
    /// CI koşucusunun ekranı 1024 genişlikte; yerel ekran 1136'yı verdiği için kusurlar yalnız CI'da çıkıyordu.
    /// </summary>
    internal static readonly Size DarBoyut = new(1024, 720);

    private const string SamplePath = @"C:\Kayitlar\tatil-cekimi-2160p60.mkv";

    /// <summary>
    /// Bilerek üst üste duran katmanlar. Ad → gerekçe. Ad, çakışan çiftin ebeveyn panelinin
    /// adıdır; kural adsız <see cref="Panel"/> türünü zaten katman sayar.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> KatmanIstisnalari =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PART_ItemsPresenter"] = "sekme şeridi TabControl şablonunda içerikle aynı gözde katman (Kesit B)",
        };

    /// <summary>
    /// Başlık kuralının dışında bilerek kalan dil anahtarları. Anahtar → gerekçe.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> BaslikIstisnalari =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["shell.menu.open"] = "Windows sağ tık menüsünün etiketi; kurucu ve ShellMenu aynı metni yazar, kabuk menüsü cümle düzeninde",
        };

    private sealed record Kusur(string Tur, string Sekme, string Yer, string Ayrinti)
    {
        public override string ToString() => $"{Tur} · {Sekme} · {Yer} · {Ayrinti}";
    }

    private sealed class Denetim
    {
        public List<Kusur> Kusurlar { get; } = new();
        public List<string> Balonlu { get; } = new();
        public HashSet<string> Gorulen { get; } = new(StringComparer.Ordinal);
        public int Metin { get; set; }
        public int Panel { get; set; }
        public int Denetim_ { get; set; }
        public int Baslik { get; set; }
        public int Sekme { get; set; }
        public string Dil { get; set; } = "";
        public HashSet<Visual> Disarida { get; } = new();

        /// <summary>
        /// Görünür ve seçili sekmenin ağacında. Geçiş denetimi önceki sayfayı canlandırma
        /// bitene dek ağaçta tutuyor; başka sekmenin sayfası bu sekmenin kusuru sayılmaz.
        /// </summary>
        public bool Kapsamda(Visual gorsel)
        {
            if (!gorsel.IsShown()) return false;
            for (Visual? d = gorsel; d is not null; d = d.GetVisualParent())
                if (Disarida.Contains(d)) return false;
            return true;
        }

        public void Ekle(Kusur kusur)
        {
            if (Gorulen.Add(kusur.Tur + "|" + kusur.Yer + "|" + kusur.Ayrinti)) Kusurlar.Add(kusur);
        }
    }

    public static TheoryData<string, bool> Kollar()
    {
        var kollar = new TheoryData<string, bool>();
        foreach (var dil in Diller)
            foreach (var dar in new[] { true, false })
                kollar.Add(dil, dar);
        return kollar;
    }

    [Theory]
    [MemberData(nameof(Kollar))]
    [MemberData(nameof(KalanDilDarKollari))]
    public void SekmelerdeKesikCakismaTasmaYok(string dil, bool dar) => Denetle(dil, dar, "", null);

    /// <summary>
    /// İş bitti durumu: aşım sorusu kırpma seçenekleri açık, klasörde göster ve paylaş
    /// düğmeleri, paylaşım bağlantısı satırı. Yüklü-boş taramada bunların hiçbiri görünmüyor.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kollar))]
    [MemberData(nameof(KalanDilDarKollari))]
    public void SonucDurumundaKesikCakismaTasmaYok(string dil, bool dar) => Denetle(dil, dar, "-sonuc", pencere =>
    {
        var kesimler = new[]
        {
            new TrimPlan(TrimSide.End, 0, 137.2, 187.5, 16_000_000),
            new TrimPlan(TrimSide.Start, 50.3, 187.5, 187.5, 16_000_000),
            new TrimPlan(TrimSide.Both, 25.1, 162.3, 187.5, 16_000_000)
        };
        pencere.ResetShareForTest(true);
        _ = pencere.ShowRetryAskForTest(new RetryPrompt(3, 3, 16, 16.4, TimeSpan.FromSeconds(30), true, 15.2, kesimler));
        pencere.BtnRetryTrim.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        pencere.TxtShareLink.Text = "https://example.invalid/d/7f3a9c2e41b8/tatil-cekimi-2160p60-vidshrink.mp4";
        pencere.ShareLinkRow.IsVisible = true;
    });

    /// <summary>
    /// Kaynak yok: ilk açılış. Bırakma alanı, boş plan ve çıktı kartı "-" değerleriyle; yüklü
    /// taramada bunların yerini dolu metinler alıyor.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kollar))]
    [MemberData(nameof(KalanDilDarKollari))]
    public void BosPenceredeKesikCakismaTasmaYok(string dil, bool dar) => Denetle(dil, dar, "-bos", null, yukle: false);

    /// <summary>
    /// Kodlama sürüyor: aşama satırı motorun iki geçişli biçimiyle ("pass 2/2 (attempt 3)"),
    /// kalan süre saatli, çıktı boyutu dolu.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kollar))]
    [MemberData(nameof(KalanDilDarKollari))]
    public void KodlamaSurerkenKesikCakismaTasmaYok(string dil, bool dar) => Denetle(dil, dar, "-kosuyor", pencere =>
        pencere.ShowEncodeProgressForTest(new EncodeProgress(0.42, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(71), 12.3, "pass 2/2 (attempt 3)")));

    /// <summary>
    /// Ayrıntı yüzeyleri: dönüştürme sonucu ve Göster düğmesi, güncelleme bildirimi ile
    /// kurulum günlüğü (altı satır, çubuk dolu), donanım başarım sonucu. Hepsi sekme
    /// dolaşımında boş duruyordu.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kollar))]
    [MemberData(nameof(KalanDilDarKollari))]
    public void AyrintiYuzeylerindeKesikCakismaTasmaYok(string dil, bool dar) => Denetle(dil, dar, "-ayrinti", pencere =>
    {
        pencere.TxtConvertResult.Text = string.Format(Strings.Get("main.run.converted"), "812.4", "96.1");
        pencere.BtnConvertReveal.IsVisible = true;
        pencere.UpdateNotice.IsVisible = true;
        pencere.TxtNoticeVersion.Text = "0.4.3";
        var ilerleme = new InstallProgress();
        pencere.ShowUpdateProgress(ilerleme);
        ilerleme.Step(0, 10, "Sürüm listesi alınıyor");
        ilerleme.Step(12, 20, "Sürüm 0.4.3: 6 dosya");
        foreach (var (ad, sira) in new[] { "VidShrink.App.dll", "VidShrink.Core.dll", "Avalonia.Base.dll", "libmpv-2.dll" }.Select((a, i) => (a, i + 1)))
            ilerleme.Step(20 + 70.0 * sira / 6, 20 + 70.0 * (sira + 1) / 6, ad + "  " + sira + "/6");
        for (var i = 0; i < 90; i++) pencere.UpdateFrame(TimeSpan.FromMilliseconds(InstallProgress.FrameMilliseconds));
        pencere.ShowPerformanceResult(PerformanceCheck.Evaluate(
            new EncoderCost("libx264", true, 3100, 3200, 6000),
            new EncoderCost("h264_nvenc", true, 120, 375, 6000),
            new EncoderCost("h264_nvenc", true, 120, 360, 6000),
            logicalCores: 16, elapsedMs: 9_400, budgetMs: 20_000, hardwareEncoderPresent: true));
    });

    /// <summary>
    /// İz paneli dolu: kaynakta iki metin ve bir görüntü altyazısı, yakma seçimi yapılmış, iki
    /// dış altyazı dosyası — biri uzun adlı (balonlu kırpılmalı). Örnek kaynakta altyazı yok,
    /// o yüzden bu satırlar öteki kollarda hiç çizilmiyor.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kollar))]
    [MemberData(nameof(KalanDilDarKollari))]
    public void IzPanelindeKesikCakismaTasmaYok(string dil, bool dar)
    {
        var klasor = Path.Combine(TipSources.Root, ".calisma", "yerlesim-denetimi", "altyazi");
        Directory.CreateDirectory(klasor);
        var dosyalar = new[] { "film.tr.srt", "Konferans_kaydi_oturum_3_soru_cevap_bolumu_duzenlenmemis.en.vtt" }
            .Select(ad => Path.Combine(klasor, ad)).ToArray();
        foreach (var dosya in dosyalar) File.WriteAllText(dosya, "1\n00:00:01,000 --> 00:00:02,000\nmerhaba\n");
        Denetle(dil, dar, "-izler", pencere =>
        {
            pencere.LoadWithoutProbing(SamplePath, Sample() with
            {
                Streams = new[]
                {
                    new SourceStream(0, StreamKind.Video, "hevc"),
                    new SourceStream(1, StreamKind.Audio, "aac", "tur", Channels: 2),
                    new SourceStream(2, StreamKind.Subtitle, "hdmv_pgs_subtitle", "eng"),
                    new SourceStream(3, StreamKind.Subtitle, "subrip", "tur", Title: "Türkçe — yönetmen yorumlu tam altyazı"),
                    new SourceStream(4, StreamKind.Subtitle, "ass", "eng")
                }
            });
            pencere.AddSubtitleFiles(dosyalar);
            pencere.CmbBurnSubtitle.SelectedIndex = 1;
            pencere.ChkAudioLoudnorm.IsChecked = true;
        });
    }

    /// <summary>
    /// Adı "Toggle" ile bitmeyen düğmelerin açtığı yüzeyler: AI ve başarım ayrıntıları, ayar
    /// sıfırlama onayı, sabit çözünürlük satırı, HDR ilkesi. Sekme dolaşımı bunları açmıyor.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kollar))]
    [MemberData(nameof(KalanDilDarKollari))]
    public void AcilanAyrintilardaKesikCakismaTasmaYok(string dil, bool dar) => Denetle(dil, dar, "-acilan", pencere =>
    {
        pencere.AiDetails.IsVisible = true;
        pencere.PerformanceDetails.IsVisible = true;
        pencere.ResetSettingsConfirm.IsVisible = true;
        pencere.ChkResolution.IsChecked = false;
        pencere.HdrPolicyPanel.IsVisible = true;
    });

    /// <summary>
    /// Kaydedici sekmesi kayıt bitmiş halde: yarım kayıt uyarısı, uzun yollu sonuç, kaynak
    /// satırlarının hepsi ve bütçe notu açık. Sekme dolaşımında kaydedici boş kuruluyor.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kollar))]
    [MemberData(nameof(KalanDilDarKollari))]
    public void KaydediciSonucundaKesikCakismaTasmaYok(string dil, bool dar) => Denetle(dil, dar, "-kayit", pencere =>
    {
        var kaydedici = pencere.RecorderPaneForTest;
        kaydedici.ShowResult(new RecordResult(true,
            Path.Combine(TipSources.Root, ".calisma", "yerlesim-denetimi", "Ekran_kaydi_2026-09-23_toplanti_sunumu_uzun_ad.mkv"),
            812.4, true, 0, string.Empty, 3));
        foreach (var satir in new Control[] { kaydedici.RowScreen, kaydedici.RowWindow, kaydedici.RowRegion, kaydedici.RowRegionTools, kaydedici.TxtBudgetNote })
            satir.IsVisible = true;
        kaydedici.TxtBudgetNote.Text = Strings.Get("recorder.output.partial");
    });

    /// <summary>
    /// Önizleme ve oynatıcının kodla açılan katmanları: sağ perde metni, yaklaşıklık rozeti,
    /// kodlama çipi, bilgi paneli, takılma metni, küçük resim çipi.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kollar))]
    [MemberData(nameof(KalanDilDarKollari))]
    public void OynaticiKatmanlarindaKesikCakismaTasmaYok(string dil, bool dar) => Denetle(dil, dar, "-katman", pencere =>
    {
        pencere.Preview.SetRightNotice("playback.panel.pending");
        pencere.Preview.SetRightBadge(Strings.Get("main.preview.temsili"));
        pencere.Preview.Controls.SetEncodeProgress(0.4, 2, 2, 3);
        pencere.Player.InfoPanel.IsVisible = true;
        pencere.Player.TxtInfo.Text = "3840×2160 · 59.94 fps · hevc · aac 192 kbit/s";
        pencere.Player.TxtStall.IsVisible = true;
        pencere.Player.TxtStall.Text = Strings.Get("main.player.seekfailed");
        pencere.Player.ThumbChip.IsVisible = true;
        pencere.Player.ThumbTime.Text = "01:02:03";
    });

    /// <summary>
    /// Taban ile tercih boyutu arasındaki ve üstündeki pencereler: sarılan satırlar ve Auto
    /// sütunlar başka genişlikte başka yerden kırılıyor; tam ekran 1080p ve 1440p dahil.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoyutKollari))]
    public void AraVeBuyukBoyuttaKesikCakismaTasmaYok(string dil, int en, int boy) =>
        Denetle(dil, false, $"-{en}x{boy}", null, zorla: new Size(en, boy));

    public static TheoryData<string, int, int> BoyutKollari()
    {
        var kollar = new TheoryData<string, int, int>();
        foreach (var dil in Diller)
            foreach (var (en, boy) in new[] { (1097, 590), (1280, 688), (1280, 800), (1920, 1080), (2560, 1440) })
                kollar.Add(dil, en, boy);
        return kollar;
    }

    /// <summary>
    /// Öbür 37 dil. Beş dil karakter toplamıyla seçildi, ama genişlik karakter sayısı değil:
    /// Tamil ve Devanagari glifleri geniş, Almanca ve Fince bileşik sözcük tek parça kalıyor.
    /// Dil × boyut kollarının her biri bu dillerde dar pencerede de koşar
    /// (<see cref="KalanDilDarKollari"/>), tek pencereli kollar <see cref="KalanDilKollari"/> ile.
    /// </summary>
    public static TheoryData<string, bool> KalanDilDarKollari()
    {
        var kollar = new TheoryData<string, bool>();
        foreach (var dil in KalanDilKollari()) kollar.Add(dil, true);
        return kollar;
    }

    public static TheoryData<string> KalanDilKollari()
    {
        var kollar = new TheoryData<string>();
        foreach (var klasor in Directory.GetDirectories(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales")).Order(StringComparer.Ordinal))
        {
            var dil = Path.GetFileName(klasor);
            if (!Diller.Contains(dil)) kollar.Add(dil);
        }
        return kollar;
    }

    private void Denetle(string dil, bool dar, string durum, Action<MainWindow>? hazirla, bool yukle = true, Size? zorla = null)
    {
        var (denetim, boyut) = Ac(dil, dar, null, zorla, hazirla: hazirla, yukle: yukle);
        var klasor = Path.Combine(TipSources.Root, ".calisma", "yerlesim-denetimi");
        var ad = $"{dil}-{(dar ? "taban" : "varsayilan")}{durum}.txt";
        Directory.CreateDirectory(klasor);

        var dokum = new StringBuilder()
            .AppendLine($"{dil} {boyut.Width:0}x{boyut.Height:0}: sekme {denetim.Sekme}, metin {denetim.Metin}, panel {denetim.Panel}, denetim {denetim.Denetim_}, baslik {denetim.Baslik}")
            .AppendLine($"kusur {denetim.Kusurlar.Count}")
            .AppendJoin(Environment.NewLine, denetim.Kusurlar).AppendLine()
            .AppendLine($"balonlu {denetim.Balonlu.Count}")
            .AppendJoin(Environment.NewLine, denetim.Balonlu).AppendLine()
            .ToString();
        File.WriteAllText(Path.Combine(klasor, ad), dokum);
        _output.WriteLine(dokum);

        Assert.True(denetim.Sekme >= 6, $"yalnız {denetim.Sekme} sekme dolaşıldı");
        Assert.True(denetim.Metin > 300, $"yalnız {denetim.Metin} metin ölçüldü");
        Assert.True(denetim.Panel > 100, $"yalnız {denetim.Panel} panel ölçüldü");
        Assert.True(denetim.Kusurlar.Count == 0, dokum);

        KanitKapanisi.Kapat(klasor, ad);
    }

    /// <summary>
    /// Olumlu kontrol: denetçi kör değil. Pencere tabanın altındaki bir genişlikte (600 px)
    /// açılır; sarmasız bir etiket bilerek dar bir kutuya konur, bir yığın kardeşi eksi kenar
    /// payıyla öncekinin üstüne itilir ve bir çocuk ebeveyninden geniş yapılır. Üç tür de
    /// adıyla yakalanmalı.
    /// </summary>
    [Fact]
    public void DenetciBilinenKusurlariYakalar()
    {
        var (denetim, _) = Ac("tr", true, pencere =>
        {
            var dil = pencere.GetVisualDescendants().OfType<TextBlock>()
                .First(b => b.Text == LanguageCatalog.Display(Strings.Get("settings-tab.general.title")) || b.Text == Strings.Get("settings-tab.general.title"));
            dil.Tag = "OlumluKesik";
            dil.Width = 12;

            var yigin = pencere.FindControl<StackPanel>("AboutStatus")!;
            var ikinci = (Control)yigin.Children[1];
            ikinci.Tag = "OlumluCakisma";
            ikinci.Margin = new Thickness(0, -30, 0, 0);

            var genis = (Control)yigin.Children[2];
            genis.Tag = "OlumluTasma";
            genis.Width = 5000;

            var sarilan = (TextBlock)yigin.Children[3];
            sarilan.Tag = "OlumluBolunme";
            sarilan.Text = "Karşılaştırmalarımızdan";
            sarilan.Width = 70;
        }, new Size(600, 720));

        var metin = string.Join(Environment.NewLine, denetim.Kusurlar);
        _output.WriteLine(metin);

        Assert.Contains(denetim.Kusurlar, k => k.Tur == "kesik" && k.Yer.Contains("OlumluKesik", StringComparison.Ordinal));
        Assert.Contains(denetim.Kusurlar, k => k.Tur == "çakışma" && k.Yer.Contains("OlumluCakisma", StringComparison.Ordinal));
        Assert.Contains(denetim.Kusurlar, k => k.Tur == "taşma" && k.Yer.Contains("OlumluTasma", StringComparison.Ordinal));
        Assert.Contains(denetim.Kusurlar, k => k.Tur == "bolunmus" && k.Yer.Contains("OlumluBolunme", StringComparison.Ordinal));
    }

    /// <summary>
    /// Başlık denetiminin olumlu kontrolü: küçük harfle yazılmış içerik sözcüğü yakalanır,
    /// bağlaç ve birim yakalanmaz.
    /// </summary>
    [Theory]
    [InlineData("tr", "Ne çıkacak", true)]
    [InlineData("tr", "Kalite ve Uyumluluk", false)]
    [InlineData("tr", "OpenSubtitles Hesabı (İndirme İçin Gerekli)", false)]
    [InlineData("tr", "Ses Hedefi (kbps)", false)]
    [InlineData("en", "OpenSubtitles account (needed for downloads)", true)]
    [InlineData("en", "OpenSubtitles Account (Needed for Downloads)", false)]
    [InlineData("tr", "Arayüz dilini değiştirir.", false)]
    public void BaslikKuraliKucukSozcuguYakalar(string dil, string metin, bool kusurlu)
        => Assert.Equal(kusurlu, KucukSozcuk(metin, dil) is not null);

    private sealed class SahteSonEylem : IQueueEndActions
    {
        public void Reveal(string path) { }
        public void Sleep() { }
        public void PowerOff() { }
    }

    public static TheoryData<string> DilKollari()
    {
        var kollar = new TheoryData<string>();
        foreach (var dil in Diller) kollar.Add(dil);
        return kollar;
    }

    /// <summary>
    /// Kuyruk penceresi: sabit genişlik, içeriğe göre yükseklik. İki durum taranır — uzun adlı
    /// üç bekleyenle duraklatılmış sıra, ve sıra boşalınca kapatma geri sayımı. Sistem eylemi
    /// sahtedir; pompa duraklatıldığı için hiçbir dosya kodlanmaz. Pencere dilini ayar
    /// dosyasından okur; başka bir testin ortak dosyaya yazdığı dil kolu saptırmasın diye
    /// kurulum anında ayar yolu olmayan bir dosyaya çevrilir ve dil kültürden gelir.
    /// </summary>
    [Theory]
    [MemberData(nameof(DilKollari))]
    [MemberData(nameof(KalanDilKollari))]
    public void KuyrukPenceresindeKesikCakismaTasmaYok(string dil)
    {
        var denetim = AppHost.Run(() =>
        {
            var kultur = System.Globalization.CultureInfo.CurrentUICulture;
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(dil);
            Strings.Use(dil);
            var yollar = new[]
            {
                @"C:\Videolar\2026-09-22 Yaz tatili — Kapadokya balon turu, gün doğumu (tam uzunluk).mp4",
                @"C:\Videolar\kisa.mov",
                @"C:\Videolar\Konferans_kaydi_oturum_3_soru_cevap_bolumu_duzenlenmemis_ham_goruntu.mkv"
            };
            var ayar = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", Path.Combine(TipSources.Root, ".calisma", "yerlesim-denetimi", "yok", "settings.json"));
            ShrinkJobWindow pencere;
            try { pencere = new ShrinkJobWindow(yollar, new PlanOptions { TargetMb = 25 }, false, null) { Actions = new SahteSonEylem() }; }
            finally { Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", ayar); }
            try
            {
                pencere.Classes.Add("reduced-motion");
                var d = new Denetim { Dil = dil };
                pencere.SetPaused(true);
                pencere.Begin();
                KuyruguTara(pencere, "kuyruk-duraklatildi", d);
                while (pencere.Pending.Count > 0) pencere.RemovePending(0);
                pencere.SetPaused(false);
                pencere.WhenDone = QueueEndChoice.PowerOff;
                pencere.QueueDrained();
                KuyruguTara(pencere, "kuyruk-geri-sayim", d);
                pencere.CancelCountdown();
                Assert.Equal(dil, pencere.Language);
                return d;
            }
            finally
            {
                pencere.Close();
                System.Globalization.CultureInfo.CurrentUICulture = kultur;
                Strings.Use("en");
            }
        });

        var dokum = $"{dil} kuyruk: metin {denetim.Metin}, panel {denetim.Panel}{Environment.NewLine}kusur {denetim.Kusurlar.Count}{Environment.NewLine}"
            + string.Join(Environment.NewLine, denetim.Kusurlar);
        _output.WriteLine(dokum);
        Assert.True(denetim.Metin > 10, $"yalnız {denetim.Metin} metin ölçüldü");
        Assert.True(denetim.Kusurlar.Count == 0, dokum);
    }

    /// <summary>
    /// Kaydedicinin mini şeridi: içeriğe göre boyutlanan çerçevesiz pencere. Kayıt, duraklatma
    /// ve geri sayım durumları; sayaç en geniş biçimde ("10:59:59").
    /// </summary>
    [Theory]
    [MemberData(nameof(DilKollari))]
    [MemberData(nameof(KalanDilKollari))]
    public void MiniSeritteKesikCakismaTasmaYok(string dil)
    {
        var denetim = AppHost.Run(() =>
        {
            Strings.Use(dil);
            var mini = new VidShrink.App.Recorder.RecorderMini();
            try
            {
                mini.Classes.Add("reduced-motion");
                var d = new Denetim { Dil = dil };
                mini.Follow(RecorderState.Running, "10:59:59");
                KuyruguTara(mini, "mini-kayit", d, double.PositiveInfinity);
                mini.Follow(RecorderState.Paused, "10:59:59");
                KuyruguTara(mini, "mini-duraklatildi", d, double.PositiveInfinity);
                mini.Follow(RecorderState.Stopped, "00:00", 10);
                KuyruguTara(mini, "mini-geri-sayim", d, double.PositiveInfinity);
                return d;
            }
            finally
            {
                mini.Close();
                Strings.Use("en");
            }
        });

        var dokum = $"{dil} mini: metin {denetim.Metin}, panel {denetim.Panel}{Environment.NewLine}kusur {denetim.Kusurlar.Count}{Environment.NewLine}"
            + string.Join(Environment.NewLine, denetim.Kusurlar);
        _output.WriteLine(dokum);
        Assert.True(denetim.Metin >= 3, $"yalnız {denetim.Metin} metin ölçüldü");
        Assert.True(denetim.Kusurlar.Count == 0, dokum);
    }

    /// <summary>
    /// Açılır pencereler ana ağaçta çizilmiyor: ön ayar kaydetme (en uzun bildirim ve 60
    /// harflik ad dolu) ve kaydedicinin seçenekleri. İçerik kendi <see cref="FlyoutPresenter"/>
    /// temasıyla, içeriğe göre boyutlanan bir pencerede taranır; genişliği temanın sınırı belirler.
    /// </summary>
    [Theory]
    [MemberData(nameof(DilKollari))]
    [MemberData(nameof(KalanDilKollari))]
    public void AcilirPencerelerdeKesikCakismaTasmaYok(string dil)
    {
        var denetim = AppHost.Run(() =>
        {
            Strings.Use(dil);
            var ana = new MainWindow();
            var mini = new VidShrink.App.Recorder.RecorderMini();
            var d = new Denetim { Dil = dil };
            try
            {
                ana.TxtPresetName.Text = new string('W', 60);
                ana.TxtPresetNotice.Text = Strings.Get("main.preset.error.hand-brake-file") + " " + string.Format(Strings.Get("main.preset.overwrite"), new string('W', 60));
                ana.TxtPresetNotice.IsVisible = true;
                AcilirTara((Flyout)ana.ChipAddPreset.Flyout!, "onayar-kaydet", d);
                AcilirTara((Flyout)mini.BtnOptions.Flyout!, "kaydedici-secenek", d);
                return d;
            }
            finally
            {
                mini.Close();
                ana.Close();
                Strings.Use("en");
            }
        });

        var dokum = $"{dil} açılır: metin {denetim.Metin}, panel {denetim.Panel}{Environment.NewLine}kusur {denetim.Kusurlar.Count}{Environment.NewLine}"
            + string.Join(Environment.NewLine, denetim.Kusurlar);
        _output.WriteLine(dokum);
        Assert.True(denetim.Metin >= 6, $"yalnız {denetim.Metin} metin ölçüldü");
        Assert.True(denetim.Kusurlar.Count == 0, dokum);
    }

    /// <summary>
    /// "Varsayılan uygulama değil" önerisi bildirim yığınında. Şerit yalnız gerçek açılışta
    /// (<c>OnWindowLoaded</c>) ekleniyor, başsız taramada hiç yoktu.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kollar))]
    [MemberData(nameof(KalanDilDarKollari))]
    public void OneriSeridindeKesikCakismaTasmaYok(string dil, bool dar) => Denetle(dil, dar, "-oneri", pencere =>
    {
        var ayar = Path.Combine(TestPaths.OutputRoot, "yerlesim-oneri", "settings.json");
        ((Panel)pencere.AppliedNotice.Parent!).Children.Add(new VidShrink.App.Integration.DefaultAppSuggestionBar(ayar));
    });

    /// <summary>
    /// Balonlar: her sekmede görünen denetimlerin balonu (düz metin ve zengin içerik) kendi
    /// <see cref="ToolTip"/> temasıyla, içeriğe göre boyutlanan bir pencerede taranır.
    /// Balon ancak üstüne gelinince açıldığı için sekme taramasında hiç çizilmiyordu.
    /// </summary>
    [Theory]
    [MemberData(nameof(DilKollari))]
    [MemberData(nameof(KalanDilKollari))]
    public void BalonlardaKesikCakismaTasmaYok(string dil)
    {
        var (denetim, sayi) = AppHost.Run(() =>
        {
            Strings.Use(dil);
            var pencere = new MainWindow();
            var kap = new Window { SizeToContent = SizeToContent.WidthAndHeight };
            kap.Classes.Add("reduced-motion");
            var d = new Denetim { Dil = dil };
            try
            {
                pencere.Classes.Add("reduced-motion");
                pencere.LoadWithoutProbing(SamplePath, Sample());
                pencere.SettleFades();
                pencere.TabAdvanced.IsVisible = true;
                var boyut = new Size(Belirtec(pencere, "WindowPreferredWidth"), Belirtec(pencere, "WindowPreferredHeight"));
                var balonlar = new List<(string Sahip, object Icerik)>();
                var gorulen = new HashSet<object>();
                var sekmeler = pencere.Tabs;
                Yerlestir(pencere, boyut);
                for (var sira = 0; sira < sekmeler.ItemCount; sira++)
                {
                    if (sekmeler.ContainerFromIndex(sira) is not TabItem { IsVisible: true }) continue;
                    sekmeler.SelectedIndex = sira;
                    Yerlestir(pencere, boyut);
                    foreach (var sahip in pencere.GetVisualDescendants().OfType<Control>())
                        if (ToolTip.GetTip(sahip) is { } icerik && icerik is not string { Length: 0 } && gorulen.Add(icerik))
                            balonlar.Add(($"balon {Ad(sahip)}", icerik));
                }

                foreach (var (sahip, icerik) in balonlar)
                {
                    var balon = new ToolTip { Content = icerik };
                    kap.Content = balon;
                    try { KuyruguTara(kap, sahip, d, double.PositiveInfinity); }
                    finally
                    {
                        balon.Content = null;
                        kap.Content = null;
                    }
                }
                return (d, balonlar.Count);
            }
            finally
            {
                kap.Close();
                pencere.Close();
                Strings.Use("en");
            }
        });

        var dokum = $"{dil} balon: {sayi} balon, metin {denetim.Metin}, panel {denetim.Panel}{Environment.NewLine}kusur {denetim.Kusurlar.Count}{Environment.NewLine}"
            + string.Join(Environment.NewLine, denetim.Kusurlar);
        var klasor = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".calisma", "yerlesim-denetimi"));
        Directory.CreateDirectory(klasor);
        File.WriteAllText(Path.Combine(klasor, $"{dil}-balonlar.txt"), dokum);
        _output.WriteLine(dokum);
        Assert.True(sayi >= 50, $"yalnız {sayi} balon bulundu");
        Assert.True(denetim.Kusurlar.Count == 0, dokum);
    }

    private static void AcilirTara(Flyout acilir, string durum, Denetim denetim)
    {
        var icerik = (Control)acilir.Content!;
        acilir.Content = null;
        var kap = new Window { SizeToContent = SizeToContent.WidthAndHeight, Content = new FlyoutPresenter { Content = icerik } };
        kap.Classes.Add("reduced-motion");
        try { KuyruguTara(kap, durum, denetim, double.PositiveInfinity); }
        finally
        {
            kap.Content = null;
            kap.Close();
        }
    }

    private static void KuyruguTara(Window pencere, string durum, Denetim denetim, double? sinir = null)
    {
        var genislik = sinir ?? Belirtec(pencere, "TipMaxWidth");
        pencere.Measure(new Size(genislik, double.PositiveInfinity));
        pencere.Arrange(new Rect(pencere.DesiredSize));
        Dispatcher();
        var kok = (Layoutable)pencere.GetVisualChildren().Single();
        for (var tur = 0; tur < 3; tur++)
        {
            foreach (var dugum in pencere.GetVisualDescendants().OfType<Layoutable>()) dugum.InvalidateMeasure();
            kok.InvalidateMeasure();
            kok.Measure(new Size(genislik, double.PositiveInfinity));
            kok.Arrange(new Rect(new Size(double.IsInfinity(genislik) ? kok.DesiredSize.Width : genislik, kok.DesiredSize.Height)));
        }
        foreach (var dugum in pencere.GetVisualDescendants().OfType<Visual>()) dugum.RenderTransform = null;
        Metinler(pencere, durum, denetim);
        Kardesler(pencere, durum, denetim);
        Tasmalar(pencere, durum, denetim);
    }

    private static (Denetim, Size) Ac(string dil, bool dar, Action<MainWindow>? boz, Size? zorla = null, Action<MainWindow>? hazirla = null, bool yukle = true) =>
        AppHost.Run(() =>
        {
            Strings.Use(dil);
            var pencere = new MainWindow();
            try
            {
                pencere.Classes.Add("reduced-motion");
                if (yukle) pencere.LoadWithoutProbing(SamplePath, Sample());
                pencere.SettleFades();
                pencere.TabAdvanced.IsVisible = true;
                hazirla?.Invoke(pencere);

                var boyut = zorla ?? (dar
                    ? DarBoyut
                    : new Size(Belirtec(pencere, "WindowPreferredWidth"), Belirtec(pencere, "WindowPreferredHeight")));

                var denetim = new Denetim { Dil = dil };
                Tara(pencere, boyut, denetim, boz);
                return (denetim, boyut);
            }
            finally
            {
                pencere.Close();
                Strings.Use("en");
            }
        });

    private static double Belirtec(Window pencere, string anahtar)
    {
        Assert.True(pencere.TryFindResource(anahtar, out var deger), $"{anahtar} belirteci yok");
        return (double)deger!;
    }

    private static MediaInfo Sample() => new()
    {
        FilePath = SamplePath,
        FileSizeBytes = 420L * 1024 * 1024,
        DurationSeconds = 187.5,
        Width = 3840,
        Height = 2160,
        Fps = 59.94,
        VideoCodec = "hevc",
        TotalBitrateBps = 18_800_000,
        AudioCodec = "aac",
        AudioBitrateBps = 192_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    /// <summary>
    /// Ağaç iki kez tümden geçersizlenip ölçülür. Tek geçişte bir <see cref="ScrollViewer"/>'ın
    /// dikey çubuğu ölçü sırasında görünür olur; ebeveyn Grid o an ölçmekte olduğu için
    /// <c>Auto</c> sütun 0 genişlikte kalır ve çubuk içerikle çakışır (it 1136x720 Dönüştür,
    /// 5x666). Gerçek pencerede düzen yöneticisi bir sonraki geçişte bunu düzeltir; ikinci
    /// geçiş aynı şeyi yapar. Geçersizleme kaldırılınca 10/18 kol kırmızı döner (ölçüldü).
    /// </summary>
    private static void Yerlestir(MainWindow pencere, Size boyut)
    {
        pencere.Width = double.NaN;
        pencere.Height = double.NaN;
        pencere.Measure(boyut);
        pencere.Arrange(new Rect(boyut));
        pencere.UpdateLayout();
        Dispatcher();
        pencere.SettleFades();

        var kok = (Layoutable)pencere.GetVisualChildren().Single();
        for (var tur = 0; tur < 2; tur++)
        {
            foreach (var dugum in pencere.GetVisualDescendants().OfType<Layoutable>()) dugum.InvalidateMeasure();
            kok.InvalidateMeasure();
            kok.Measure(boyut);
            kok.Arrange(new Rect(boyut));
        }

        foreach (var dugum in pencere.GetVisualDescendants().OfType<Visual>()) dugum.RenderTransform = null;
    }

    private static void Dispatcher() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static void Tara(MainWindow pencere, Size boyut, Denetim denetim, Action<MainWindow>? boz)
    {
        Yerlestir(pencere, boyut);
        var sekmeler = pencere.Tabs;
        var acilan = new HashSet<Button>();

        for (var sira = 0; sira < sekmeler.ItemCount; sira++)
        {
            if (sekmeler.ContainerFromIndex(sira) is not TabItem { IsVisible: true }) continue;
            sekmeler.SelectedIndex = sira;
            Yerlestir(pencere, boyut);

            foreach (var dugme in pencere.GetVisualDescendants().OfType<Button>()
                         .Where(d => d.IsShown() && d.Name is { } ad && ad.EndsWith("Toggle", StringComparison.Ordinal) && acilan.Add(d))
                         .ToList())
                dugme.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Yerlestir(pencere, boyut);
        }

        for (var sira = 0; sira < sekmeler.ItemCount; sira++)
        {
            if (sekmeler.ContainerFromIndex(sira) is not TabItem { IsVisible: true } oge) continue;
            sekmeler.SelectedIndex = sira;
            Yerlestir(pencere, boyut);

            if (boz is not null && oge == pencere.TabSettings) boz(pencere);
            Yerlestir(pencere, boyut);
            Yerlestir(pencere, boyut);

            var baslik = MainWindow.TabHeaderText(oge);
            if (sira == 1 || oge == pencere.TabSettings)
                Assert.Equal(LanguageCatalog.Display(Strings.Get(oge == pencere.TabSettings ? "main.tab.settings" : "main.tab.shrink")), baslik);

            denetim.Sekme++;
            denetim.Disarida.Clear();
            foreach (var diger in sekmeler.Items.OfType<TabItem>())
                if (diger != oge && diger.Content is Visual sayfa) denetim.Disarida.Add(sayfa);
            Metinler(pencere, baslik, denetim);
            Kardesler(pencere, baslik, denetim);
            Tasmalar(pencere, baslik, denetim);
            YarimSatirlar(pencere, baslik, denetim);
            Serit(pencere, oge, baslik, denetim);
            if (denetim.Dil is "tr" or "en") Basliklar(pencere, baslik, denetim);
        }
    }

    private static string Ad(Control denetim)
    {
        var kendi = string.IsNullOrEmpty(denetim.Name) ? denetim.GetType().Name : denetim.GetType().Name + "#" + denetim.Name;
        if (denetim.Tag is string etiket && etiket.StartsWith("Olumlu", StringComparison.Ordinal)) kendi += "@" + etiket;
        var ata = denetim.GetVisualAncestors().OfType<Control>().FirstOrDefault(c => !string.IsNullOrEmpty(c.Name));
        return ata is null ? kendi : $"{kendi} < {ata.Name}";
    }

    private static string Kisalt(string? metin)
    {
        var tek = (metin ?? string.Empty).Replace('\n', '⏎');
        return tek.Length > 60 ? tek[..60] + "…" : tek;
    }

    private static double GerekenGenislik(TextBlock blok, string metin) =>
        new Avalonia.Media.TextFormatting.TextLayout(
            metin,
            new Typeface(blok.FontFamily, blok.FontStyle, blok.FontWeight, blok.FontStretch),
            blok.FontSize,
            Brushes.Black,
            lineHeight: blok.LineHeight,
            letterSpacing: blok.LetterSpacing).WidthIncludingTrailingWhitespace;

    /// <summary>
    /// <see cref="TextBlock"/> kendi sınırını kırpar (<c>ClipToBounds</c> varsayılanı açık).
    /// Satır kutusu alttaki iniş payını da taşıdığı için yalnız yüksekliğe bakmak "?" gibi
    /// inişsiz tek bir işaretin sığdığı rozeti kesik sayıyordu. İki ölçü: son satırın taban
    /// çizgisi kutunun altına düşüyorsa harfin gövdesi kesilir; satır kutusu
    /// <see cref="DikeyPay"/>'dan fazla taşıyorsa iniş harfleri de kesilir.
    /// </summary>
    private const double DikeyPay = 3;

    private static (double Yukseklik, double Taban) GerekenYukseklik(TextBlock blok, string metin, double genislik)
    {
        var yerlesim = new Avalonia.Media.TextFormatting.TextLayout(
            metin,
            new Typeface(blok.FontFamily, blok.FontStyle, blok.FontWeight, blok.FontStretch),
            blok.FontSize,
            Brushes.Black,
            textWrapping: blok.TextWrapping,
            maxWidth: blok.TextWrapping == TextWrapping.NoWrap ? double.PositiveInfinity : genislik,
            lineHeight: blok.LineHeight,
            letterSpacing: blok.LetterSpacing);
        var satirlar = yerlesim.TextLines;
        var ust = satirlar.Take(satirlar.Count - 1).Sum(satir => satir.Height);
        return (yerlesim.Height, ust + satirlar[^1].Baseline);
    }

    /// <summary>Bir görselin <paramref name="ata"/> uzayındaki konumu, yalnız <see cref="Visual.Bounds"/> toplanarak.</summary>
    private static Point Konum(Visual dugum, Visual ata)
    {
        double x = 0, y = 0;
        for (var d = dugum; d is not null && d != ata; d = d.GetVisualParent())
        {
            x += d.Bounds.X;
            y += d.Bounds.Y;
        }

        return new Point(x, y);
    }

    private static void Metinler(Window pencere, string sekme, Denetim denetim)
    {
        foreach (var blok in pencere.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!denetim.Kapsamda(blok) || blok.Bounds.Width <= 0 || string.IsNullOrWhiteSpace(blok.Text)) continue;
            if (blok.FindAncestorOfType<TextBox>() is not null) continue;
            if (blok.Opacity <= 0) continue;
            denetim.Metin++;

            var metin = blok.Text!;
            var yer = blok.Bounds.Width - blok.Padding.Left - blok.Padding.Right;
            var yukseklikYeri = blok.Bounds.Height - blok.Padding.Top - blok.Padding.Bottom;
            var gereken = blok.TextWrapping == TextWrapping.NoWrap ? GerekenGenislik(blok, metin) : Math.Min(yer, GerekenGenislik(blok, metin));
            var balonda = blok.TextTrimming != TextTrimming.None && ToolTip.GetTip(blok) is string tip && tip.Contains(metin, StringComparison.Ordinal);

            var cizilen = blok.TextLayout.Width;
            if (cizilen - yer > 0.5)
            {
                denetim.Ekle(new Kusur("kesik", sekme, $"{Ad(blok)} [{Kisalt(metin)}]", $"çizilen {cizilen:0.#} kutudan geniş, yer {yer:0.#}"));
                continue;
            }

            if (BolunenSozcuk(blok, metin, denetim.Dil) is { } bolunen)
            {
                denetim.Ekle(new Kusur("bolunmus", sekme, $"{Ad(blok)} [{Kisalt(metin)}]", $"sözcük satır sonunda bölündü: {bolunen}, yer {yer:0.#}"));
                continue;
            }
            if (blok.TextWrapping == TextWrapping.NoWrap && gereken - yer > 0.5)
            {
                if (balonda && blok.FindAncestorOfType<SutunIzgara>() is null) denetim.Balonlu.Add($"{sekme} · {Ad(blok)} [{Kisalt(metin)}] gereken {gereken:0.#}, yer {yer:0.#}");
                else denetim.Ekle(new Kusur("kesik", sekme, $"{Ad(blok)} [{Kisalt(metin)}]", $"genişlik gereken {gereken:0.#}, yer {yer:0.#}"));
                continue;
            }

            var (dikey, taban) = GerekenYukseklik(blok, metin, yer);
            if (taban - yukseklikYeri > 0.5 || dikey - yukseklikYeri > DikeyPay)
            {
                denetim.Ekle(new Kusur("kesik", sekme, $"{Ad(blok)} [{Kisalt(metin)}]", $"yükseklik gereken {dikey:0.#} (taban çizgisi {taban:0.#}), yer {yukseklikYeri:0.#}"));
                continue;
            }

            var ust = blok.Padding.Top;
            var alt = blok.Padding.Top + Math.Max(yukseklikYeri, taban);
            var sol = blok.Padding.Left;
            var sag = blok.Padding.Left + Math.Max(yer, gereken);
            foreach (var ata in blok.GetVisualAncestors().OfType<Control>())
            {
                if (ata is TopLevel) break;
                if (!ata.ClipToBounds)
                {
                    if (ata is ScrollViewer) break;
                    continue;
                }

                var konum = Konum(blok, ata);
                var kaydirma = ata is ScrollContentPresenter or ScrollViewer;
                var solTasma = -(konum.X + sol);
                var sagTasma = konum.X + sag - ata.Bounds.Width;
                var ustTasma = kaydirma ? 0 : -(konum.Y + ust);
                var altTasma = kaydirma ? 0 : konum.Y + alt - ata.Bounds.Height;
                var tasma = Math.Max(Math.Max(solTasma, sagTasma), Math.Max(ustTasma, altTasma));
                if (tasma > 1)
                {
                    var eksen = Math.Max(ustTasma, altTasma) >= Math.Max(solTasma, sagTasma) ? "dikey" : "yatay";
                    denetim.Ekle(new Kusur("kesik", sekme, $"{Ad(blok)} [{Kisalt(metin)}]", $"{Ad(ata)} kırpıyor, {eksen} taşma {tasma:0.#} (metin {dikey:0.#} px, kutu {yukseklikYeri:0.#} px)"));
                    break;
                }

                if (ata is ScrollViewer) break;
            }
        }
    }

    private static bool AyniGoz(Control a, Control b)
    {
        static (int, int, int, int) Goz(Control c) =>
            (Grid.GetRow(c), Grid.GetRow(c) + Math.Max(1, Grid.GetRowSpan(c)), Grid.GetColumn(c), Grid.GetColumn(c) + Math.Max(1, Grid.GetColumnSpan(c)));

        var (r1, r2, c1, c2) = Goz(a);
        var (s1, s2, d1, d2) = Goz(b);
        return r1 < s2 && s1 < r2 && c1 < d2 && d1 < c2;
    }

    private static void Kardesler(Window pencere, string sekme, Denetim denetim)
    {
        foreach (var panel in pencere.GetVisualDescendants().OfType<Panel>())
        {
            if (!denetim.Kapsamda(panel)) continue;
            if (panel.GetType() == typeof(Panel) || panel is Canvas) continue;
            if (panel.Name is { } ad && KatmanIstisnalari.ContainsKey(ad)) continue;
            denetim.Panel++;

            var cocuklar = panel.Children.Where(c => c.IsVisible && c.Opacity > 0 && c.Bounds.Width > 0 && c.Bounds.Height > 0).ToList();
            for (var i = 0; i < cocuklar.Count; i++)
                for (var j = i + 1; j < cocuklar.Count; j++)
                {
                    var a = cocuklar[i];
                    var b = cocuklar[j];
                    if (panel is Grid && AyniGoz(a, b)) continue;
                    var kesisim = a.Bounds.Intersect(b.Bounds);
                    if (kesisim.Width <= 1 || kesisim.Height <= 1) continue;
                    denetim.Ekle(new Kusur("çakışma", sekme, $"{Ad(a)} ∩ {Ad(b)}",
                        $"{panel.GetType().Name}{(string.IsNullOrEmpty(panel.Name) ? "" : "#" + panel.Name)} içinde {kesisim.Width:0.#}x{kesisim.Height:0.#}"));
                }
        }
    }

    private static void Tasmalar(Window pencere, string sekme, Denetim denetim)
    {
        foreach (var cocuk in pencere.GetVisualDescendants().OfType<Control>())
        {
            if (!denetim.Kapsamda(cocuk) || cocuk.Bounds.Width <= 0 || cocuk.Bounds.Height <= 0) continue;
            if (cocuk.GetVisualParent() is not Control ebeveyn || ebeveyn is TopLevel) continue;
            if (ebeveyn is Canvas || ebeveyn is Viewbox || cocuk is Popup) continue;
            denetim.Denetim_++;

            var b = cocuk.Bounds;
            var yatay = Math.Max(-b.X, b.Right - ebeveyn.Bounds.Width);
            var dikey = Math.Max(-b.Y, b.Bottom - ebeveyn.Bounds.Height);

            if (ebeveyn is ScrollContentPresenter sunucu)
            {
                if (sunucu.FindAncestorOfType<ScrollViewer>() is { } kaydirici
                    && kaydirici.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled) continue;
                dikey = 0;
            }

            var tasma = Math.Max(yatay, dikey);
            if (tasma <= 1) continue;
            denetim.Ekle(new Kusur("taşma", sekme, Ad(cocuk) + (cocuk is TextBlock yazi ? $" [{Kisalt(yazi.Text)}]" : ""),
                $"{ebeveyn.GetType().Name}{(string.IsNullOrEmpty(ebeveyn.Name) ? "" : "#" + ebeveyn.Name)} [{ebeveyn.Bounds.Width:0.#}x{ebeveyn.Bounds.Height:0.#}] dışına {(yatay >= dikey ? "yatay" : "dikey")} {tasma:0.#}"));
        }
    }

    /// <summary>
    /// Kaydırılan metin kutusunda görünür alan satır sınırında biter; yarım satır görünmez.
    /// Kutunun iç boşluğu kaydırılan içeriğin parçasıyken (<c>TextPresenter.Margin</c>) görünür
    /// alan boşluğu da kapsıyor ve <c>MaxLines</c>'ın kestiği satır alt boşlukta yarısıyla
    /// görünüyordu (Gelişmiş, FFmpeg komut satırı).
    /// </summary>
    private static void YarimSatirlar(Window pencere, string sekme, Denetim denetim)
    {
        foreach (var kutu in pencere.GetVisualDescendants().OfType<TextBox>())
        {
            if (!denetim.Kapsamda(kutu)) continue;
            var sunucu = kutu.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault();
            var yazi = kutu.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
            if (sunucu is null || yazi is null || sunucu.Bounds.Height <= 0) continue;
            if (yazi.TranslatePoint(default, sunucu) is not { } ust) continue;
            var alt = sunucu.Bounds.Height;
            var satirUstu = ust.Y;
            foreach (var satir in yazi.TextLayout.TextLines)
            {
                var satirAlti = satirUstu + satir.Height;
                if (satirUstu < alt - 1 && satirAlti > alt + 1)
                {
                    denetim.Ekle(new Kusur("yarım satır", sekme, Ad(kutu),
                        $"görünür alan {alt:0.#} px, satır {satirUstu:0.#}-{satirAlti:0.#} arasında kesiliyor"));
                    break;
                }
                satirUstu = satirAlti;
            }
        }
    }

    /// <summary>
    /// Oynatıcı sayfası tüm alanı kaplar (T185) ve şerit onun üstünde kendini gizler; bu yüzden
    /// ölçülmez. Öteki her sayfanın kaydırıcısı şeridin altında başlamalı.
    /// </summary>
    private static void Serit(MainWindow pencere, TabItem oge, string sekme, Denetim denetim)
    {
        if (oge == pencere.TabPlayer) return;
        if (oge.Content is not Control sayfa || !sayfa.IsShown()) return;
        var ust = Konum(sayfa, pencere).Y;
        var seritAlti = Konum(pencere.TitleBar, pencere).Y + pencere.TitleBar.Bounds.Height;
        if (ust + 0.5 < seritAlti)
            denetim.Ekle(new Kusur("şerit", sekme, Ad(sayfa), $"sayfa {ust:0.#}'de başlıyor, şerit {seritAlti:0.#}'de bitiyor"));
    }

    /// <summary>
    /// Satırı hece ya da harf arasından kıran yazılar: Unicode satır kırma kuralında Hangul
    /// heceleri arasında kırılma serbesttir (CSS'in `word-break: normal`'ı da öyle), Korece
    /// "2패|스" kusur değil.
    /// </summary>
    private static readonly HashSet<string> HarfArasiKirilanDiller = ["ja", "zh-Hans", "th", "ko"];

    /// <summary>
    /// Sarılan metnin bir satırı harfle bitip sonraki harfle başlıyorsa sözcük ortadan
    /// bölünmüştür ("Estimat / ed Time"): taşma yok, kesik denetimi görmez, ama okunmaz.
    /// Sözcük aralığı boşluk olmayan yazılar muaf.
    /// </summary>
    internal static string? BolunenSozcuk(TextBlock blok, string metin, string dil)
    {
        if (blok.TextWrapping == TextWrapping.NoWrap || HarfArasiKirilanDiller.Contains(dil)) return null;
        var satirlar = blok.TextLayout.TextLines;
        for (var i = 0; i < satirlar.Count - 1; i++)
        {
            var son = satirlar[i].FirstTextSourceIndex + satirlar[i].Length;
            if (son <= 0 || son >= metin.Length) continue;
            if (char.IsLetterOrDigit(metin[son - 1]) && char.IsLetterOrDigit(metin[son]))
            {
                var bas = son - 1;
                while (bas > 0 && char.IsLetterOrDigit(metin[bas - 1])) bas--;
                var bit = son;
                while (bit < metin.Length && char.IsLetterOrDigit(metin[bit])) bit++;
                return $"{metin[bas..son]}|{metin[son..bit]}";
            }
        }
        return null;
    }
    private static readonly IReadOnlyDictionary<string, HashSet<string>> KucukSozcukler =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["tr"] = new(StringComparer.Ordinal) { "ve", "veya", "ile", "ki", "da", "de", "ya", "mi", "mı", "mu", "mü" },
            ["en"] = new(StringComparer.Ordinal) { "a", "an", "the", "and", "or", "but", "nor", "of", "to", "in", "on", "at", "by", "for", "from", "with", "as", "vs", "per", "via" },
        };

    private static readonly HashSet<string> BaslikTemalari = new(StringComparer.Ordinal) { "H1", "H2", "H3", "Label", "PlanFactLabel" };

    /// <summary>
    /// Başlık kuralını bozan ilk sözcük; yoksa <c>null</c>. Cümle işaretli metin gövdedir, sorulmaz.
    /// Rakamla başlayan parça (<c>50.3s</c>, <c>1080p</c>) sözcük değil sayıdır; <see cref="LanguageCatalog.Title"/>
    /// de harfle başlamayanı olduğu gibi bırakıyor.
    /// Sayının hemen ardındaki birim (<c>50,3 sn</c>) de öyle: <see cref="LanguageCatalog.IsUnitAfterNumber"/>
    /// onu büyütmüyor.
    /// </summary>
    internal static string? KucukSozcuk(string metin, string dil)
    {
        if (LanguageCatalog.Brands.ContainsKey(metin)) return null;
        for (var i = 0; i < metin.Length; i++)
            if (metin[i] is '.' or ';' or '!' or '?' && (i + 1 == metin.Length || char.IsWhiteSpace(metin[i + 1])))
                return null;

        var kucuk = KucukSozcukler[dil];
        var ilk = true;
        string? onceki = null;
        foreach (var parca in metin.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var once = onceki;
            onceki = parca;
            if (char.IsDigit(parca[0])) { ilk = false; continue; }
            if (LanguageCatalog.IsUnitAfterNumber(parca, once)) continue;
            var bas = 0;
            while (bas < parca.Length && !char.IsLetter(parca[bas])) bas++;
            if (bas == parca.Length) continue;
            var sozcuk = parca[bas..].TrimEnd(')', ',', ':', '…');
            var yalin = sozcuk.ToLowerInvariant();
            var digeri = !ilk && kucuk.Contains(yalin);
            ilk = false;
            if (digeri || !char.IsLower(sozcuk[0])) continue;
            if (LanguageCatalog.Title(sozcuk, dil) == sozcuk) continue;
            return sozcuk;
        }

        return null;
    }

    private static void Basliklar(Window pencere, string sekme, Denetim denetim)
    {
        foreach (var blok in pencere.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!denetim.Kapsamda(blok) || blok.Bounds.Width <= 0 || string.IsNullOrWhiteSpace(blok.Text)) continue;
            if (blok.FindAncestorOfType<TextBox>() is not null || blok.FindAncestorOfType<ComboBox>() is not null) continue;

            var tema = blok.Theme is { } t ? BaslikTemasi(pencere, t) : null;
            var dugmede = blok.Theme is null && blok.FindAncestorOfType<Button>() is not null;
            if (tema is null && !dugmede) continue;
            denetim.Baslik++;

            if (BaslikIstisnalari.Keys.Any(anahtar => LanguageCatalog.Display(Strings.Get(anahtar)) == blok.Text)) continue;
            if (KucukSozcuk(blok.Text!, denetim.Dil) is { } sozcuk)
                denetim.Ekle(new Kusur("başlık", sekme, $"{Ad(blok)} [{Kisalt(blok.Text)}]", $"'{sozcuk}' küçük harfle ({tema ?? "düğme"})"));
        }
    }

    private static string? BaslikTemasi(Window pencere, ControlTheme tema)
    {
        foreach (var ad in BaslikTemalari)
            if (pencere.TryFindResource(ad, out var deger) && ReferenceEquals(deger, tema))
                return ad;
        return null;
    }
}
