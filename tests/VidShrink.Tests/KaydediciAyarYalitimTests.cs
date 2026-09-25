using System;
using System.IO;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kaydedici ayar dosyasinin sinif sinirini gecmedigini pimler.
///
/// <para><b>Kusur.</b> <c>RecorderView</c> ayar dosyasini surecin statik yolundan
/// (<c>RecorderSettings.FilePath</c>) okuyup oraya yaziyordu. Suit ici paralellik
/// kapali (<c>LanguageTests.cs</c>'teki
/// <c>[assembly: CollectionBehavior(DisableTestParallelization = true)]</c>), yani iki
/// sinif ayni anda kosmuyor; kanal <b>ertelenmis</b> islerden aciliyordu.
/// <c>PersistChoices</c> arayuz olaylariyla cagriliyor ve is tek Avalonia arayuz
/// is parcaciginin kuyrugunda bekliyor. Bir sinifin bekleyen yazmasi, kendi kapisi
/// <c>finally</c> ile dosyayi geri koyduktan <b>sonra</b> — yani bir sonraki sinifin
/// olcumu sirasinda — bosalabiliyordu. Iki olculmus yuzu:</para>
/// <list type="number">
/// <item>main CI <c>35302890518</c>:
/// <c>KaydediciOnizlemeTests.KutuIsaretliyseIstegeYolGirerResimOkunurBozukKareEskisiniKorur</c>
/// <c>Assert.Null</c>'da kirmizi, gercek deger bir jpg yolu. Kod degismeden ayni commit
/// yeniden kosuldu ve yesil dondu: gerileme degil, sira.</item>
/// <item><c>KaydediciArayuzTests.GelismisKollarIstegeVeAyaraGecer</c> kapinin icinde
/// olmasina ragmen paylasilan dosyada <c>scaleWidth: 1280</c> birakiyordu
/// (<c>docs/olcumler/aot-dalgasi.md</c> 12. bolum).</item>
/// </list>
///
/// <para><b>Cozum.</b> Yol artik ornege bagli: <c>new RecorderView(yol)</c>. Ertelenmis
/// yazma da o yola dusuyor, cunku yolu gorunum kurulurken aldi. Asagidaki iki olcu
/// kanalin iki yonunu ayri ayri pimliyor — biri okumayi, oteki yazmayi.</para>
/// </summary>
public sealed class KaydediciAyarYalitimTests
{
    private static string Klasor
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "paket-2c", "ayar-yalitimi");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    /// <summary>
    /// Paylasilan dosyaya <b>bilerek</b> dokunan olcumler icin: dosyanin baytlarini alir,
    /// olcumu kosar, dosyayi oldugu gibi geri koyar. Paylasilan yolu gercekten kullanan
    /// tek uretim yolu <c>MainWindow.TembelSekme.cs</c>; testte onu yalniz bu sinif
    /// taklit ediyor.
    /// </summary>
    private static T PaylasilaniKorurken<T>(Func<string, T> olc)
    {
        var paylasilan = RecorderSettings.FilePath!;
        var onceki = File.Exists(paylasilan) ? File.ReadAllBytes(paylasilan) : null;
        try
        {
            return olc(paylasilan);
        }
        finally
        {
            AppHost.Run(() =>
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                return 0;
            });

            if (onceki is not null) File.WriteAllBytes(paylasilan, onceki);
            else if (File.Exists(paylasilan)) File.Delete(paylasilan);
        }
    }

    /// <summary>
    /// <b>Kapinin kendisi pimli.</b> <see cref="PaylasilaniKorurken"/>'in "geri koy ya da sil"
    /// garantisi ertelenmis yazmayla deliniyordu: kirletici govdenin bekleyen
    /// <c>PersistChoices</c>'i <c>finally</c> dosyayi geri koyduktan <b>sonra</b>, yani bir
    /// sonraki sinifin olcumu sirasinda bosaliyordu. Cok sinifli kosumda paylasilan dosya
    /// Gif + <c>livePreview:true</c> ile geride kaliyordu, 2/2 tekrarlandi.
    ///
    /// <para>Olcu kirletir, kapiyi kapatir, sonra <b>bir kez daha</b> is parcacigini
    /// bosaltir: kapi bekleyen yazmayi kendisi bosaltmazsa dosya o anda yeniden kirlenir
    /// ve bu olcu kirmizi olur.</para>
    /// </summary>
    [Fact]
    public void KapiKapandiktanSonraBekleyenYazmaDosyayiKirletmiyor()
    {
        var paylasilan = RecorderSettings.FilePath!;

        PaylasilaniKorurken(_ => AppHost.Run(() =>
        {
            var kirletici = new RecorderView();
            KaydediciAyarTests.Elle(kirletici);
            SecKap(kirletici, "GIF");
            KaydediciAyarTests.Bul<CheckBox>(kirletici, "ChkLivePreview").IsChecked = true;
            return kirletici.PrepareRecording() is not null;
        }));

        var kapandiktanSonra = File.Exists(paylasilan) ? File.ReadAllBytes(paylasilan) : null;

        AppHost.Run(() =>
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            return 0;
        });

        var bosaltmadanSonra = File.Exists(paylasilan) ? File.ReadAllBytes(paylasilan) : null;

        Assert.Equal(kapandiktanSonra is null, bosaltmadanSonra is null);
        if (kapandiktanSonra is not null)
            Assert.True(
                kapandiktanSonra.AsSpan().SequenceEqual(bosaltmadanSonra),
                "Kapi kapandiktan sonra bekleyen bir yazma paylasilan dosyayi degistirdi.");
    }

    private static void SecKap(RecorderView view, string kap)
    {
        var kutu = KaydediciAyarTests.Bul<ComboBox>(view, "CmbContainer");
        var index = -1;
        var i = 0;
        foreach (var oge in kutu.ItemsSource!)
        {
            if (string.Equals(oge?.ToString(), kap, StringComparison.Ordinal)) index = i;
            i++;
        }

        Assert.True(index >= 0, $"CmbContainer kutusunda {kap} yok");
        kutu.SelectedIndex = index;
    }

    /// <summary>
    /// <b>Okuma yonu.</b> Paylasilan dosyaya baska bir govde Gif kabi ve
    /// <c>livePreview: true</c> yazar; hemen ardindan kendi yoluyla kurulan gorunum
    /// bundan etkilenmemeli. Gif'te <c>MaxMegabytes</c> null kaldigi icin
    /// <c>RecorderView.Onizleme.cs</c>'teki kural onizleme yolunu dusurmuyor — CI'da
    /// kirmiziyi ureten tam bu bileske.
    ///
    /// <para>Kirletici govde gercek bir <c>RecorderView()</c>, yani kanalin kendisi:
    /// yolu verilmemis gorunum statik yola yaziyor. Yazmanin gerceklestigi
    /// <b>pozitif denetimle</b> dogrulaniyor; o olmadan "yesil" bir sey soylemezdi.</para>
    /// </summary>
    [Fact]
    public void BaskaGovdeninPaylasilanDosyayaYazdigiAyarOlcumeGecmez()
    {
        var jpg = Path.Combine(Klasor, "yalitim.jpg");

        var olcu = PaylasilaniKorurken(paylasilan =>
            KaydediciAyarTests.AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
            {
                var kirletici = new RecorderView();
                KaydediciAyarTests.Elle(kirletici);
                SecKap(kirletici, "GIF");
                KaydediciAyarTests.Bul<CheckBox>(kirletici, "ChkLivePreview").IsChecked = true;
                var kirleticiKurdu = kirletici.PrepareRecording() is not null;

                var kap = KaydediciAyarTests.DosyadakiDeger(paylasilan, "containerFormat");
                var onizleme = KaydediciAyarTests.DosyadakiDeger(paylasilan, "livePreview");

                var olculen = new RecorderView(ayarYolu) { PreviewLocation = () => jpg };
                return (kirleticiKurdu, kap, onizleme,
                    kutu: KaydediciAyarTests.Bul<CheckBox>(olculen, "ChkLivePreview").IsChecked,
                    onizlemeYolu: olculen.PrepareRecording()?.Request.PreviewPath,
                    ozelVar: File.Exists(ayarYolu));
            })));

        Assert.True(olcu.kirleticiKurdu, "kirletici govde istek kurmadi: olcunun oncülü yok");
        Assert.Equal("\"Gif\"", olcu.kap);
        Assert.Equal("true", olcu.onizleme);

        Assert.False(olcu.kutu);
        Assert.Null(olcu.onizlemeYolu);
        Assert.True(olcu.ozelVar, "olculen gorunum kendi dosyasini yazmadi");
        KanitKapanisi.Kapat(Klasor, "yalitim.jpg");
    }

    /// <summary>
    /// <b>Yazma yonu.</b> Kendi yoluyla kurulan gorunume 1280x720 yazilip
    /// <c>PrepareRecording()</c> ile eszamanli kaydettirilir: olcek kendi dosyasinda
    /// olmali, paylasilan dosyaya <b>girmemeli</b>. Bu, <c>aot-dalgasi.md</c> 12. bolumun
    /// "kaynak acik" dedigi borcun kapisi.
    ///
    /// <para>Paylasilan dosyanin onceki icerigi olcumden once okunuyor: eski bir kosumdan
    /// kalmis 1280 varsa o satir belirsizlesir, ama mutasyonu yakalayan asil sart ozel
    /// dosyanin kendisi ve o kosulsuz.</para>
    /// </summary>
    [Fact]
    public void GorunumYalnizKendisineVerilenDosyayaYazar()
    {
        var paylasilan = RecorderSettings.FilePath!;
        var onceki = KaydediciAyarTests.DosyadakiDeger(paylasilan, "scaleWidth");

        var olcu = KaydediciAyarTests.AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var view = new RecorderView(ayarYolu);
            KaydediciAyarTests.Elle(view);
            KaydediciAyarTests.Bul<TextBox>(view, "TxtScaleWidth").Text = "1280";
            KaydediciAyarTests.Bul<TextBox>(view, "TxtScaleHeight").Text = "720";
            var kuruldu = view.PrepareRecording() is not null;

            return (kuruldu,
                ozelGenislik: KaydediciAyarTests.DosyadakiDeger(ayarYolu, "scaleWidth"),
                ozelYukseklik: KaydediciAyarTests.DosyadakiDeger(ayarYolu, "scaleHeight"),
                paylasilanGenislik: KaydediciAyarTests.DosyadakiDeger(paylasilan, "scaleWidth"));
        }));

        Assert.True(olcu.kuruldu, "istek kurulmadi: olcunun oncülü yok");
        Assert.Equal("1280", olcu.ozelGenislik);
        Assert.Equal("720", olcu.ozelYukseklik);

        if (olcu.paylasilanGenislik is not null && onceki != "1280")
            Assert.NotEqual("1280", olcu.paylasilanGenislik);
    }
}
