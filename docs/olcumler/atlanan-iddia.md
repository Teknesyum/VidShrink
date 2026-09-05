# Atlanan iddia gecmis sayiliyor - T158

Atlandi() cagirilan bir test yesil donuyordu: butce dolduysa, donanim bacagi
alinamadiysa ya da sayac guvenilir okumadiysa, iddia hic kurulmuyor ve kosum bunu basarili
sayiyordu. Bu belge sekiz cagri yerinin tumunu sayar, mekanizmayi olcer, secilen yolu
gerekcelendirir ve mutasyon izgarasini tasir.

Butun kosumlar Windows 11, 16 mantiksal cekirdek, xunit 2.9.2, .NET 8.0, -c Release.

## K1 - Bugun kac tanesi gercekten atlaniyor?

Bu makinede, izole tek kosum (.calisma/t63/olcum.txt sifirlanip
dotnet test -c Release --filter "PerformanceCheckTests" tek basina kosturuldu):

    21:[atlandi] bos kosumda donanim bacagi alinamadi (kodlayici listede var ama gecisi basarisiz dondu), donanim iddiasi kurulmadi

1 satir. Ozet: 21 basarili, 1 atlanan (xunit-duzeyinde SKIP,
DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu, HardwareEncoderFact gecidi -
bu makinede h264_nvenc yok), 0 basarisiz, toplam 22.

CI'nin gunlugunden (gh run view --job=101341904857 --log, main'deki son basarili
kosum, run 33979447267): tam .calisma/T158/k1-ci-full-log.txt indirildi ve
grep atlandi sifir eslesme verdi. Bunun sebebi bir fark degil, vstest konsol
kosucusunun varsayilan davranisi: ITestOutputHelper.WriteLine ciktisi yalniz basarisiz
testler icin konsola yaziliyor; Atlandi() cagrilan test basarili dondugu icin
[atlandi] satiri hicbir zaman CI konsol gunlugune dusmuyor. CI gunlugunde gorulen tek sey
xunit'in kendi [SKIP] satirlari - bunlar Atlandi() degil, discovery-time Skip gecitleri
(asagida K2):

    [xUnit.net 00:01:41.98]     VidShrink.Tests.PerformanceCheckTests.YukAltindaKararHafiflemiyorMu [SKIP]
      Skipped VidShrink.Tests.PerformanceCheckTests.YukAltindaKararHafiflemiyorMu [1 ms]
    [xUnit.net 00:03:27.27]     VidShrink.Tests.PerformanceCheckTests.DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu [SKIP]
      Skipped VidShrink.Tests.PerformanceCheckTests.DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu [1 ms]

Kosumun ozeti: Passed! - Failed: 0, Passed: 1704, Skipped: 19, Total: 1723 (tum suit,
filtresiz - CI PerformanceCheckTests'e filtrelemiyor).

Fark asil bulgu: yerel makinede [atlandi] satirini dogrudan gorebiliyoruz (ham
olcum gunlugu sayesinde); CI'da ayni bilgi hicbir zaman konsola dusmuyor, cunku
testler basarili donuyor ve vstest basarili testin ITestOutputHelper ciktisini
basmiyor. Yani CI'da su ana kadar kac Atlandi() cagrisi oldugu - donanim hep yok
oldugu icin muhtemelen her kosumda en az bir tane - hicbir yerde gorunmuyordu. Bu,
sozlesmenin ana iddiasini dogrudan dogruluyor: atlanan iddia sessizce geciyordu.
Asagidaki K2 duzeltmesi CI'da da gorunur bir sinir koyuyor: esik asilirsa CI konsolunda
[FAIL] cikacak (konsol basarisiz testin ciktisini basar).

Ham dosyalar: .calisma/T158/k1-yerel-tek-kosum-olcum.txt,
.calisma/T158/k1-ci-full-log.txt, .calisma/T158/k1-ci-atlandi.txt.

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
(.calisma/T158/probe/, xunit 2.9.2, ayni surum) bir test govdesi basladiktan sonra
istisna firlatti:

    [Fact]
    public void MidTestSkipDenemesi()
    {
        Sayac.AtlananSayisi++;
        throw new Exception("dinamik atlama denemesi - xunit v2 bunu Skipped sayar mi?");
    }

Sonuc [FAIL], Skipped degil:

    [xUnit.net 00:00:00.19]     Probe.DinamikAtlamaDenemesi.MidTestSkipDenemesi [FAIL]
      Hata Iletisi:
       System.Exception : dinamik atlama denemesi - xunit v2 bunu Skipped sayar mi?

Karar: mekanizma kosum sirasinda atlayamiyor. xunit 2.9.2 (bu depoda kurulu surum)
dinamik/govde-ici atlamayi desteklemiyor (bu xunit v3'te var, v2'de yok; depoda
Xunit.SkippableFact gibi bir yama paketi de kurulu degil - ~/.nuget/packages altinda
yalniz cekirdek xunit.* paketleri var). Atlandi() cagrilarinin hepsi olcum
tamamlandiktan sonra, sonuca gore karar veriyor; bu yuzden K1'deki sekiz yer hicbiri
[Fact(Skip=...)]e baglanamaz.

Secilen yol: B - sessiz yesili bitir, esik asilinca kirmiziya don. Uygulama:

- Atlandi() her cagrildiginda sinif-duzeyinde statik bir sayaci (_atlananSayisi)
  Interlocked.Increment ile artiriyor.
- BasarimOlculeri (koleksiyon tanimi) artik ICollectionFixture<PerformanceCheckTests.AtlananIddiaGuvencesi>;
  bu armagan koleksiyonun son testinden sonra Dispose ile cagrilir ve
  _atlananSayisi > Esik ise Xunit.Sdk.XunitException firlatir.
- Bir fixture Dispose'unun firlatmasi butun koleksiyonu "Test Collection Cleanup
  Failure" ile kirmiziya cevirir ve dotnet test cikis kodu 1 olur - bu deneyle
  dogrulandi (.calisma/T158/probe, ayni CollectionFixture deseni, esik asilinca
  3 gercek test de "cleanup failure" ile basarisiz gorundu, dotnet test cikis kodu 1).
- Esik: 6 - AtlananIddiaGuvencesi.Esik. Bu sayi K1'deki sekiz cagri yerinin (K5'te
  sabitlenen sayi) bir kosumda yapisal olarak ulasabildigi tavan (asagida K3'te
  gerekce), sezgisel bir tampon degil.

## K3 - Yanlis kirmizi uretme: hangi atlama mesru, hangisi degil

Esigin 6 olmasinin sebebi yapisal: sekiz cagri yeri bes test metoduna dagiliyor ve
her metot icindeki dallar birbirini disliyor (erken return ya da if/else), yani tek
bir kosumda metot basina ulasilabilecek en fazla sayi:

| Metot | O metottaki cagri yerleri | Tek kosumda azami |
|---|---|---|
| BuMakinedeKodlamaNereyeDusuyor | :336 | 1 |
| OlcumYukAltindaYalnizAgirlasiyor | :447, :467, :481 to :586, :493 to :586 | 2 (yazilim dali 1 + donanim dali 1; :447 donerse digerleri 0'a duser) |
| YukAltindaKararHafiflemiyorMu | :545, :556 | 1 (birbirini dislar) |
| ButceGercektenBagliyorVeSebebiniSoyluyor | :641 | 1 |
| IslemciZamaniSayaciDogruOkuyorMu | :773 | 1 |
| Toplam | | 6 |

Bu tavan makine yukune bakmiyor: yuk sadece hangi dallarin secildigini etkiler,
metot basina birden fazla cagriyi degil (kod akisi buna izin vermiyor). Yani "makine
mesgulken butce dolmasi normal" iddiasi ile "esik 6'yi asarsa olcumun kendisi bozuk"
iddiasi celismiyor - esik, mesguliyetin urete bilecegi en yuksek sayidan asagida
degil, tam onun ustunde durur.

Ayrica sekiz cagri yerinin hepsi zaten kendi Assert'iyle korunuyor (ornek: :336
oncesinde Assert.True(result.BudgetExhausted, ...), :586'daki DonanimAtla de
Assert.True(sebep.Length > 0, ...) ile sebepsiz kaybolusu zaten kirmiziya ceviriyor).
Esik bu korumalarin ustune eklenen tek bir toplu sinir: tek tek her cagri zaten
mesru sebebe bagli, esik ise "kac tanesi mesru sayilsin" sorusuna cevap veriyor.

Kanit - iki kosum, ham cikti:

Sakin makine (arka planda baska ajan yok):

    [xUnit.net 00:02:48.06] ... DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu [SKIP]
    Basarili!  - Basarisiz: 0, Basarili: 23, Atlanan: 1, Toplam: 24
    [atlandi] satiri: 1

Yuklu makine (15 adet [math]::Sqrt dongusu tum mantiksal cekirdekleri doldururken,
(Get-CimInstance Win32_Processor).LoadPercentage = 100):

    [xUnit.net 00:01:09.62] ... YukAltindaKararHafiflemiyorMu [SKIP]  (QuietMachineFact gecidi makine bos degil dedi)
    [xUnit.net 00:04:28.43] ... DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu [SKIP]
    Basarili!  - Basarisiz: 0, Basarili: 22, Atlanan: 2, Toplam: 24
    [atlandi] satiri: 1

Iki kosumda da 0 basarisiz, [atlandi] sayisi esigin (6) cok altinda kaldi.
Ham dosyalar: .calisma/T158/k3-sakin-kosum-degisiklik-sonrasi.txt,
.calisma/T158/k3-yuklu-kosum.txt, .calisma/T158/k3-yuklu-olcum.txt.

## K4 - Mutasyon izgarasi

Her kolda once dotnet build -c Release --no-incremental calistirildi (--no-build
kullanilmadi), sonra ilgili tek test filtrelenerek kosturuldu.

| # | Mutasyon | Kirilan olcu | Sonuc |
|---|---|---|---|
| a | AtlananIddiaGuvencesi.Kontrol icindeki if (sayi > Esik) - if (false && sayi > Esik) (korumayi devre disi birak) | AtlananIddiaEsigiAsilincaKirmiziyaDoner | FAIL - Assert.Throws() Failure: No exception was thrown |
| b | :403'teki Atlandi(...) cagrisi silinip yerine sessiz return birakildi | AtlandiCagriYeriSayisiSabitMi | FAIL - Atlandi cagri yeri sayisi 7, beklenen 8 |

Iki mutasyon da geri alindi (git diff temiz); dosya mutasyon oncesiyle birebir ayni
(diff ile dogrulandi). Ham ciktilar: .calisma/T158/k4-mutasyon-a.txt,
.calisma/T158/k4-mutasyon-b.txt.

## K5 - Kol sayisi

    dotnet test -c Release --filter "PerformanceCheckTests" --list-tests

Degisiklik oncesi 22 kol, degisiklik sonrasi 24 kol (iki yeni olcu:
AtlananIddiaEsigiAsilincaKirmiziyaDoner, AtlandiCagriYeriSayisiSabitMi). Sifir
eslesen kol yok. Bu worktree'deki dogrulama kosumu (T158-atlanan-iddia dali,
origin/main tepesinden acildi): Basarili!  - Basarisiz: 0, Basarili: 23, Atlanan: 1, Toplam: 24.

CI kimligi (bu sozlesmenin degisikligi oncesi, main'deki en son basarili kosum):
gh run view 33979447267 - main, ci, completed / success, is test
(ID 101341904857), Passed! - Failed: 0, Passed: 1704, Skipped: 19, Total: 1723.
Bu dal main'e henuz birlesmedi; birlestikten sonraki kosum bu sayilarin 2 fazlasini
(1725 toplam) gormeli - T0'in birlestirme sonrasi dogrulamasina birakildi.

## Docstring guncellemesi

PerformanceCheckTests.cs:59-72 (eski :42-46) artik eski "sebep atliyor, sessiz
kalmiyor" ifadesini degil, yeni sayac+esik mekanizmasini ve gerekcesini anlatiyor;
AtlananIddiaGuvencesi sinifinin kendi docstring'i esik tavaninin nereden geldigini
(yukaridaki K3 tablosu) tasiyor.
