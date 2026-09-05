# Atlanan iddia gecmis sayiliyor - T158

Atlandi() cagirilan bir test yesil donuyordu: butce dolduysa, donanim bacagi
alinamadiysa ya da sayac guvenilir okumadiysa, iddia hic kurulmuyor ve kosum bunu basarili
sayiyordu. Bu belge sekiz cagri yerinin tumunu sayar, mekanizmayi olcer, secilen yolu
gerekcelendirir ve mutasyon izgarasini tasir.

Butun kosumlar Windows 11, 16 mantiksal cekirdek, xunit 2.9.2, .NET 8.0, -c Release.

**Tur 2:** denetci (ab7cbcc7cb0b62d81) tur 1 teslimini (7124b62) dort bulguyla geri
cevirdi. B1/B2 asagida K2/K3/K4'u yeniden yazdi (esik terk edildi, yerine cagri-basi
mesru kaniti geldi). B3 (mutasyonlar ana agacta kosmustu) - tur 2'nin her komutu bu
worktree'de (VidShrink-T158) kosturuldu, ana agactaki artiklar (`.calisma/T158/`)
onceden temizlenmis bulundu. B4 (CI beklenmemisti) - asagidaki K5 bu dalin kendi
tamamlanan kosumunu tasiyor.

## K1 - Bugun kac tanesi gercekten atlaniyor?

Bu makinede, izole tek kosum (`.calisma/t63/olcum.txt` sifirlanip
`dotnet test -c Release --filter "PerformanceCheckTests"` tek basina kosturuldu,
ham cikti `.calisma/T158/probe/k1-bu-makine-tur2.txt`):

    [atlandi] mesru deneme
    [atlandi] bos kosumda donanim bacagi alinamadi (kodlayici listede var ama gecisi basarisiz dondu), donanim iddiasi kurulmadi

2 satir: biri AtlandiKorumasiVeSayaciDogruCalisiyorMu korumasinin kendi olcusu
(K4b'nin karsiligi, uretim cagrisi degil), digeri gercek uretim cagrisi (bu makinede
donanim kodlayicisi h264_nvenc yok). Ozet: 23 basarili, 1 atlanan (xunit-duzeyinde SKIP,
DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu, HardwareEncoderFact gecidi),
0 basarisiz, toplam 24.

CI'nin gunlugunden (main'deki degisiklik-oncesi son basarili kosum, run 33979447267,
gh run view --job=101341904857 --log): tam log indirildi ve grep atlandi sifir
eslesme verdi. Sebep bir fark degil, vstest konsol kosucusunun varsayilan davranisi:
ITestOutputHelper.WriteLine ciktisi yalniz basarisiz testler icin konsola yaziliyor;
Atlandi() cagrilan test basarili dondugu icin [atlandi] satiri hicbir zaman CI
konsol gunlugune dusmuyor. CI gunlugunde gorulen tek sey xunit'in kendi [SKIP]
satirlari - bunlar Atlandi() degil, discovery-time Skip gecitleri (asagida K2):

    [xUnit.net 00:01:41.98]     VidShrink.Tests.PerformanceCheckTests.YukAltindaKararHafiflemiyorMu [SKIP]
      Skipped VidShrink.Tests.PerformanceCheckTests.YukAltindaKararHafiflemiyorMu [1 ms]
    [xUnit.net 00:03:27.27]     VidShrink.Tests.PerformanceCheckTests.DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu [SKIP]
      Skipped VidShrink.Tests.PerformanceCheckTests.DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu [1 ms]

Kosumun ozeti: Passed! - Failed: 0, Passed: 1704, Skipped: 19, Total: 1723 (tum suit,
filtresiz - CI PerformanceCheckTests'e filtrelemiyor).

Fark asil bulgu: yerel makinede [atlandi] satirini dogrudan gorebiliyoruz (ham
olcum gunlugu sayesinde); CI'da ayni bilgi hicbir zaman konsola dusmuyor, cunku
testler basarili donuyor ve vstest basarili testin ITestOutputHelper ciktisini
basmiyor. K2'nin altindaki mesru-kaniti duzeltmesi CI'da da gorunur bir sinir koyuyor:
kanit yoksa (mesru: false) Atlandi() kendisi Xunit.Sdk.XunitException firlatiyor,
test [FAIL] doner ve konsol basarisiz testin ciktisini basar - eskiden oldugu gibi
"esik asilirsa" degil, her tek mesru olmayan cagrida.

Ham dosyalar: .calisma/T158/k1-yerel-tek-kosum-olcum.txt,
.calisma/T158/k1-ci-full-log.txt, .calisma/T158/k1-ci-log-atlandi.txt,
.calisma/T158/probe/k1-bu-makine-tur2.txt (tur 2 dogrulamasi).

## K2 - Mekanizmayi olc, karar ver

Deney 1 - Skip discovery-time mi calisiyor mu? CalibrationProbeTests.cs:14'teki
LiveSourceFactAttribute, VIDSHRINK_LIVE_SOURCE ayarli degilken constructor'da
Skip alanini dolduruyor. Bu testi tek basina kosturunca:

    [xUnit.net]     VidShrink.Tests.CalibrationProbeTests.LiveTimeSurvivesTheTargetsThatKeepThePlanShape [SKIP]
      Atlandi VidShrink.Tests.CalibrationProbeTests.LiveTimeSurvivesTheTargetsThatKeepThePlanShape [1 ms]
    Atlandi! - Basarisiz: 0, Basarili: 0, Atlanan: 1, Toplam: 1

Sonuc gercekten Skipped (Atlanan: 1). Ama bu karar govde hic calismadan,
constructor'da aliniyor - FactAttribute.Skip sadece kesif (discovery) anindaki
sabit/hesaplanabilir bir string.

Deney 2 - govde basladiktan sonra dinamik atlama var mi? Ayri bir deneme projesinde
(.calisma/T158/probe/DinamikAtlamaDenemesi/, xunit 2.9.2, ayni surum) bir test
govdesi basladiktan sonra istisna firlatti:

    [Fact]
    public void MidTestSkipDenemesi()
    {
        throw new Exception("dinamik atlama denemesi - xunit v2 bunu Skipped sayar mi?");
    }

Sonuc [FAIL], Skipped degil (tur 2'de tekrarlandi, ham cikti
.calisma/T158/probe/k2-deney2-raw.txt):

    [xUnit.net 00:00:00.19]     Probe.DinamikAtlamaDenemesi.MidTestSkipDenemesi [FAIL]
      Hata Iletisi:
       System.Exception : dinamik atlama denemesi - xunit v2 bunu Skipped sayar mi?

Karar: mekanizma kosum sirasinda atlayamiyor. xunit 2.9.2 (bu depoda kurulu surum)
dinamik/govde-ici atlamayi desteklemiyor (bu xunit v3'te var, v2'de yok; depoda
Xunit.SkippableFact gibi bir yama paketi de kurulu degil - ~/.nuget/packages
altinda yalniz cekirdek xunit.* paketleri var). Atlandi() cagrilarinin hepsi olcum
tamamlandiktan sonra, sonuca gore karar veriyor; bu yuzden K1'deki sekiz yer hicbiri
[Fact(Skip=...)]e baglanamaz.

Secilen yol (tur 2 - B1/B2 denetimi sonrasi degisti): C - esik degil, cagri-basi
mesru kaniti. Tur 1'de secilen "B - sayac + esik" yolu denetimde cokuyordu: sekiz
cagri yerinin bes test metoduna dagilimindan, tek kosumda yapisal olarak
ulasilabilecek azami atlama sayisi da 6 idi (asagida K3), yani esik "sayi > 6"
hicbir kosumda atesleyemiyordu - 1'den 6'ya kadar her atlama sayisi sessiz yesil
kaliyordu. Esigi asagi cekmek de tek basina cozum degildi: esik "kac tanesi mesru
sayilsin" sorusuna hicbir zaman cevap vermiyordu, sekiz cagri yeri ayni sayaca
yaziyordu ve ikisi (eski :612, :708) hicbir onceden-Assert korumasi tasimiyordu.

Uygulama:

- Atlandi(string sebep, bool mesru) artik ikinci bir parametre aliyor. Cagiran
  taraf atlamanin mesru oldugunu (butce dolmus, donanim yok, makine olcumu bozmus
  gibi zaten olculmus/dogrulanmis bir olguyu) kanitlamak zorunda.
- mesru false ise Atlandi() kendisi Xunit.Sdk.XunitException firlatir - test
  o an [FAIL] doner, esik ya da koleksiyon kapanisi beklemez.
- mesru true ise sinif-duzeyinde statik _atlananSayisi Interlocked.Increment
  ile artar. Bu sayac savunmanin kendisi degil, K1'in "kac tanesi atlaniyor"
  sorusuna cevap veren bir olcum; savunma her cagrida tekrarlanan kanit
  zorunlulugunda.
- Koruma dogrudan sinaniyor: AtlandiKorumasiVeSayaciDogruCalisiyorMu hem
  mesru: false'in XunitException firlattigini (Assert.Throws), hem
  mesru: true'nun sayaci gercekten arttirdigini (Assert.Equal(oncesi + 1,
  _atlananSayisi)) sinif-ici erisimle dogrudan okuyarak sinar - eski Kontrol
  testinin aksine sabiti sabitle degil, gercek cagriyi gercek sayacla.

## K3 - Yanlis kirmizi uretme: hangi atlama mesru, hangisi degil

Ayrim artik esikte degil, her cagrinin kendi mesru: argumaninda - her sekiz cagri
yeri, cagirdigi anda elindeki olculmus bir gercekle (BudgetExhausted, donanim
bulgu kodu, sabit is-parcacigi-guvenilirligi olgusu gibi) kendi mesruiyetini
kanitliyor:

| Cagri yeri (uretim, satir) | mesru kaniti |
|---|---|
| :392 BuMakinedeKodlamaNereyeDusuyor | result.BudgetExhausted (ayrica Assert.True ile onceden de sinanir) |
| :505 OlcumYukAltindaYalnizAgirlasiyor (bos okuma) | okumalar.All(r => r.BudgetExhausted) |
| :526 ayni metot (yuklu, yazilim olculemedi) | yuklu.BudgetExhausted |
| :605 YukAltindaKararHafiflemiyorMu (bos taban) | bos.SoftwareMeasured ise hep mesru, degilse bos.BudgetExhausted - tur 1'de bu yerin hic korumasi yoktu |
| :617 ayni metot (yuklu) | yuklu.BudgetExhausted (ayrica Assert.True ile onceden de sinanir) |
| :648 DonanimAtla (paylasilan yardimci) | sebep.Length > 0 (bulgu kodundan turetilen aciklama bos degil; ayrica Assert.True ile onceden de sinanir) |
| :705 ButceGercektenBagliyorVeSebebiniSoyluyor (genis butce) | genis.BudgetExhausted - tur 1'de bu yerin hic korumasi yoktu |
| :838 IslemciZamaniSayaciDogruOkuyorMu | true - onceki satirlarda zaten dogrulanmis makine-duzeyi bir olgu (sayac guvenilir degil), olculebilir bir kosul degil |

Makine yuku bu ayrimi bozamaz: yuk sadece hangi dalin secildigini etkiler
(BudgetExhausted gercekten dolup dolmadigini), mesru kanitin kendisini degil -
"makine mesgulken butce dolmasi normal" iddiasi ile "mesrusuz atlama kirmiziya
doner" iddiasi celismiyor, cunku butce gercekten dolduysa kanit zaten dogru.

Kanit - iki kosum, ham cikti:

Sakin makine (arka planda baska ajan yok gorunuyordu; ham cikti
.calisma/T158/probe/k3-sakin-kosum-tur2.txt):

    [xUnit.net 00:03:21.43]     VidShrink.Tests.PerformanceCheckTests.DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu [SKIP]
    Basarili!  - Basarisiz: 0, Basarili: 23, Atlanan: 1, Toplam: 24

Yapay yuklu makine (15 arka plan is parcaciginda 4 dakika [math]::Sqrt dongusu tum
mantiksal cekirdekleri doldururken; ham cikti
.calisma/T158/probe/k3-yuklu-kosum-tur2.txt):

    [xUnit.net 00:00:55.39]     VidShrink.Tests.PerformanceCheckTests.YukAltindaKararHafiflemiyorMu [SKIP]
    [xUnit.net 00:04:51.61]     VidShrink.Tests.PerformanceCheckTests.DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu [SKIP]
    Basarili!  - Basarisiz: 0, Basarili: 22, Atlanan: 2, Toplam: 24

Yuklu kosumda YukAltindaKararHafiflemiyorMu QuietMachineFact gecidiyle (discovery
oncesi, makine bos degil) tamamen atlandi - govdesi hic calismadi, dolayisiyla
Atlandi() hic cagrilmadi. Iki kosumda da 0 basarisiz: mesru kaniti tasiyan sekiz
cagri yeri, ne sakin ne yuklu makinede yanlis kirmizi uretmedi.

## K4 - Mutasyon izgarasi

Her kolda once dotnet build -c Release --no-incremental calistirildi (--no-build
kullanilmadi), sonra ilgili tek test filtrelenerek kosturuldu. Ham ciktilar
.calisma/T158/probe/k4-mutasyon-a.txt, .calisma/T158/probe/k4-mutasyon-b.txt.

| # | Mutasyon | Kirilan olcu | Sonuc |
|---|---|---|---|
| a | Atlandi icindeki if (!mesru) - if (false && !mesru) (korumayi devre disi birak) | AtlandiKorumasiVeSayaciDogruCalisiyorMu | FAIL - Assert.Throws() Failure: No exception was thrown |
| b | :392'deki Atlandi(...) cagrisi (ve onceki Assert.True) silinip yerine sessiz return birakildi | AtlandiCagriYeriSayisiSabitMi | FAIL - Atlandi cagri yeri sayisi 7, beklenen 8 |

Mutasyon (a), B2'nin istedigi seyi dogrudan sinar: sayaci degil, korumanin kendisini
kirar ve gercek bir olcuyu (AtlandiKorumasiVeSayaciDogruCalisiyorMu) kirmiziya
cevirir - tur 1'deki Kontrol(Esik) mutasyonunun aksine, sabiti sabitle sinamaz.
Iki mutasyon da geri alindi (.calisma/T158/PerformanceCheckTests.cs.baseline ile
diff temiz, sifir fark); dosya mutasyon oncesiyle birebir ayni.

## K5 - Kol sayisi

    dotnet test -c Release --filter "PerformanceCheckTests" --list-tests

24 kol, sifir eslesen kol yok (ham cikti .calisma/T158/probe/k5-list-tests-tur2.txt).
Bu worktree'deki dogrulama kosumu (T158-atlanan-iddia dali):
Basarili! - Basarisiz: 0, Basarili: 23, Atlanan: 1, Toplam: 24.

CI kimligi - bu dalin kendi tamamlanan kosumu (tur 2 teslimi oncesi son push,
commit b22f740): gh run view 33983975390 -> T158-atlanan-iddia, ci,
completed / success, is test (ID 101354050093), 24m32s, tum adimlar yesil
(build -warnaserror dahil, kosum-kapisi.ps1 -MinimumTotal 1134 -MaximumSkipped 30
dahil). Bu tur 2'nin kendi commit'i push'landiktan sonra dalin CI kosumu yeniden
tetiklenip sonucu bu belgeye islenecek (asagida "Kayit" bakiniz). Onceki referans -
bu sozlesmenin degisikligi oncesi, maindeki en son basarili kosum: gh run view
33979447267 - main, ci, completed / success, is test (ID 101341904857),
Passed! - Failed: 0, Passed: 1704, Skipped: 19, Total: 1723. Bu dal maine henuz
birlesmedi - onu T0 yapar.

## Docstring guncellemesi

PerformanceCheckTests.cs:47-59 artik ne eski "sebep atliyor, sessiz kalmiyor"
ifadesini ne de tur 1'in esik/sayac anlatisini tasiyor: mesru parametresinin ne
kanitlamasi gerektigini ve eski esik yaklasiminin neden terk edildigini (bes test
metodunun yapisal tavaninin 6 olup mumkun tum atlamalarla ayni sayiya denk gelmesi)
anlatiyor. :71-78'deki AtlandiKorumasiVeSayaciDogruCalisiyorMu docstring'i K4(a)
ve K4(b) mutasyonlarinin hangi olcuyu kirdigini isim vererek yaziyor.
