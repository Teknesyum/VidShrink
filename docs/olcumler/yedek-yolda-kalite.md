# T141 — Yoklama yedek yola düştüğünde pencerenin kalitesi

Dal: `T141-yedek-yolda-kalite` · Her ölçüm `dotnet build -c Release --no-incremental`
ile derlenip `dotnet test -c Release` ile koşuldu.

Bu belgedeki her sayı bloğunun üstünde hangi fixture'dan çıktığı yazılıdır.

## K1 — İki kolun da koştuğu ölçüldü, sözleşme duruyor

Fixture: `.calisma/T141/k1-kesif.txt`. Klipler `lavfi` ile üretildi —
`testsrc2=size=320x240:rate=12:duration=8` ve `testsrc2=size=96x96:rate=12:duration=8`.
Ölçer, çağrı sayan bir `IQualityMeasurement` sahtesi.

```
HIZLI    half=(160,120) full=24 kare  quality=var   olcer cagrisi=1
HALFNULL half=null      full=24 kare  quality=NULL  olcer cagrisi=0
YEDEK    half=(161,121) full=24 kare  quality=NULL  olcer cagrisi=0
URETIM 96x96   olcer cagrisi=0  kalite kaydi=0  Measured=True
URETIM 320x240 olcer cagrisi=2  kalite kaydi=2  Measured=True
```

**Sonuç: ikisi de ölü kol değil. Sözleşme K2–K4 ile devam etti.**

### `half is null` kolu (`:737`) — üretim girdisiyle koşuyor

Tetikleyici kaynak boyutu. `RunDetailedAsync` yarım çözünürlüğü şöyle seçiyor:
`canProbeHalf`, `EvenDown(Math.Round(boyut * ComplexityProfile.ProbeScale)) >= 64`
istiyor, `ProbeScale = 0.5`. Yani 127 pikselin altındaki her kenar bu kola düşüyor.

Sınır ölçüldü, türetilmedi (fixture: aşağıdaki K5 tablosu):
126x126 kaynak bu kola düşüyor, 128x128 hızlı yola giriyor.

96x96 klip üretim girişinden geçti, `Measured=True` döndü ve **tek bir kalite
kaydı üretmedi.** Bu kol canlı ve sessiz.

### Yedek yol (`:746-748`) — üretim girdisiyle tetiklenemedi

`EvenDown` zaten çift ve `canProbeHalf` zaten `>= 64` ürettiği için üretimde
`SplitSampleAsync`e geçersiz bir yarım boyut hiç gitmiyor. Kol iç imzadan
bölünemeyen bir yarım boyut (`161x121`) verilerek zorlandı.

Üretimdeki tetikleyicileri girdi değil **ortam.** `SplitSampleAsync`in `null`
dönme şartlarını okuyup saydım, beş tane:

1. ffmpeg çıkış kodu sıfır değil
2. `ParseFrames(stderr)` sıfır ya da altı
3. `MeasureSampleBytes(fullPath)` sıfır ya da altı
4. `MeasureSampleBytes(halfPath)` sıfır ya da altı
5. istisna — `SampleTimeout` (90 sn) dolması dahil

Hiçbiri girdiye bağlı değil; diskin dolması, ffmpeg yapısının `filter_complex`
zincirini taşımaması, yavaş kaynakta zaman aşımı gibi durumlar. Bu yüzden
"ne sıklıkta koşuyor" sorusunun dürüst cevabı: **üretim girdisiyle sıfır,
ortam bozulduğunda her pencerede.** Ölü olduğunu gösteren bir ölçü yok.

### K1'in ölçüleri

`tests/VidShrink.Tests/ComplexityProbeTests.cs`, K1 commit'i `4e5b33c`. Üçü de
kolun koştuğunu gösteriyor, kalite iddiası taşımıyor:

- `YarimCozunurlukYoluKapalikenPencereHalaUretiliyor`
- `KucukKaynakUretimdeGercektenIsleniyor`
- `BolunemeyenYarimOlcuHizliYoluDusuruyorAmaPencereyiDusurmuyor`

## K2 — Kusur önce kırmızıya düştü

Kusur commit'i `1c87aaf`. Hiçbir ölçü sabit sayıya bakmıyor; her biri **aynı
girdinin iki halini** ya da **iki koşumu** karşılaştırıyor.

Fixture: `.calisma/T141/k2-kirmizi.txt`, aynı `NormalKaynak` klibi ve aynı
`1.0` sn başlangıç; küçük/normal karşılaştırması `KucukKaynak` ile `NormalKaynak`.

```
  Başarısız ComplexityProbeTests.KucukKaynakOlcerVarkenNormalKaynakKadarKaliteUretiyor
   Assert.Equal() Failure: Values differ
Expected: 2
Actual:   0
  Başarısız ComplexityProbeTests.AyniPencereHizliYoldaOlculuyorYarimYokKolundaOlculmuyor
   Assert.NotNull() Failure: Value is null
  Başarısız ComplexityProbeTests.AyniPencereHizliYoldaOlculuyorYedekYoldaOlculmuyor
   Assert.NotNull() Failure: Value is null
Başarısız! - Başarısız: 3, Başarılı: 32, Toplam: 35
```

Üçünün de ortak iddiası: pencere **tam ölçek karesi üretiyor** — yani profile
giriyor, ağırlığı sayılıyor — ama kalitesi kayıt edilmiyor ve kimse
"ölçülmedi" demiyor.

## K3 — Karar: (a) yedek yol da kaliteyi ölçsün

**Gerekçe maliyetten çıktı.** İki kol da tam ölçek örneğini **zaten kodluyor**:
`SampleAsync` dosyayı yazıyor, baytını ölçüyor ve `finally` içinde siliyor.
Eksik olan şey kodlama değil, dosyanın ölçere gösterilmesi.

Yapılan: `SampleToFileAsync` ayrıldı — dosyayı çağırana bırakıyor.
`MeasuredSampleAsync` onu kullanıp ölçeri çağırıyor ve dosyayı kendisi siliyor.
**Ek ffmpeg koşumu yok.** Maliyet pencere başına bir ölçer çağrısı; hızlı yolun
zaten ödediği maliyetin aynısı.

(b) ve (c) elendi: ikisi de `ProbeResult`a ya da `WindowQualityMeasurement`a
yeni bir alan istiyor, ikisi de `src/VidShrink.Core` altında ve `owns` dışında.
Ayrıca ölçüm zaten mümkünken "ölçemedim" işareti üretmek kusuru belgelemek
olurdu, kapatmak değil.

Kararı tutan ölçüler: `AyniPencereHizliYoldaOlculuyorYarimYokKolundaOlculmuyor`,
`AyniPencereHizliYoldaOlculuyorYedekYoldaOlculmuyor`,
`KucukKaynakOlcerVarkenNormalKaynakKadarKaliteUretiyor`.

## K4 — İki kol için karar tablosu

| kol | satır | tetikleyici | karar | gerekçe |
|---|---|---|---|---|
| `half is null` | `:737` | kaynak kenarı 127 pikselin altında | (a) ölçülsün | Tam ölçek örneği zaten kodlanıp siliniyor; ölçere göstermek ek kodlama istemiyor. |
| yedek yol | `:746-748` | `SplitSampleAsync` beş şarttan biriyle `null` dönüyor | (a) ölçülsün | Aynı sebep: elde silinmek üzere olan aynı tam ölçek örneği var. |

**İki kol aynı kararı alıyor, fark yok.** Tetikleyicileri ayrı — biri girdi
boyutundan, öbürü ffmpeg başarısızlığından — ama ikisinin de elinde aynı şey
var. Farklı karar verecek bir gerekçe bulunamadı.

## K5 — Üretimde ölçülen kalite değişti mi

Fixture: `.calisma/T141/k5-eski.txt` ve `.calisma/T141/k5-yeni.txt`. Her satır
kendi `lavfi` klibiyle `RunDetailedAsync(info, SpeedMode.Quality, true, olcer)`
koşumu; sayılar `ProbeResult.QualityMeasurements.Count`.

| kaynak | süre | kol | eski kayıt | yeni kayıt | eski `HasQuality` | yeni `HasQuality` |
|---|---|---|---|---|---|---|
| 96x96 | 8 sn | yarım yok | 0 | **2** | False | True |
| 126x126 | 8 sn | yarım yok (sınır) | 0 | **2** | False | True |
| 128x128 | 8 sn | hızlı (sınır) | 2 | 2 | True | True |
| 320x240 | 8 sn | hızlı | 2 | 2 | True | True |
| 640x360 | 8 sn | hızlı | 2 | 2 | True | True |
| 320x240 | 2 sn | hızlı, tek pencere | 1 | 1 | True | True |
| 320x240, yarım=(161,121) | tek pencere | yedek (zorlanmış) | kalite NULL | kalite **var** | — | — |

**Hiçbir girdide kayıt sayısı azalmadı.** Hızlı yol hiç değişmedi; artan tek şey
daha önce sıfır kayıt üreten iki kol.

### `QualityMeasurements`ı tüketen yerler

Kendim saydım — **altı üretim/araç tüketicisi, bir türetilmiş özellik, yedi test
kullanımı.**

| # | yer | ne yapıyor | değişimden etkilendi mi |
|---|---|---|---|
| 1 | `src/VidShrink.App/MainWindow.axaml.cs:1789` | `Comparable` olanları çapa yapıp `WithProbeQuality` | Etkilendi, yönü iyi: küçük kaynaklar artık çapa alıyor. |
| 2 | `tools/cipa-yeniden/Program.cs:20` | pencereleri JSON'a döküyor | Daha çok satır. Kırılma yok. |
| 3 | `tools/cipa-yeniden/Program.cs:32` | çapa üretimi | Aynı, yönü iyi. |
| 4 | `tools/VidShrink.Bench/Program.cs:124` | ölçümü JSON çıktısına koyuyor | Daha çok satır. Kırılma yok. |
| 5 | `tools/VidShrink.Bench/Program.cs:659` | çapa üretimi | Aynı, yönü iyi. |
| 6 | `tools/yerlesim-skoru/Program.cs:24` | çapa üretimi | Aynı, yönü iyi. |
| — | `src/VidShrink.Core/ProbeResult.cs:7` (`HasQuality`) | `Count > 0` | Küçük kaynakta `False` iken `True` oldu. |

Altısı da aynı biçimde yazılmış: `Comparable` ve `VmafNegMean` dolu olanları
süz, boş değilse çapa yap. **Hiçbiri sabit bir sayıya bağlı değil**, bu yüzden
hiçbiri kırılmadı; hepsi daha önce boş dönen girdilerde artık veri görüyor.

Test kullanımları: `ComplexityProbeTests` satır 62, 64, 65, 79, 159, 614, 616.
79 ve 159 ölçerin yok/bozuk/kıyaslanamaz olduğu durumları çiviliyor ve
değişmeden yeşil kaldı — yani "ölçer yoksa kayıt da yok" davranışı korundu.

## K6 — Mutasyon ızgarası

Fixture: `.calisma/T141/k6-mutasyon.txt`. Her mutasyon tek başına uygulandı,
`dotnet build -c Release --no-incremental` ile tam derlendi, `--no-build`
kullanılmadı.

| mutasyon | ne bozuldu | kırılan ölçüler | sonuç |
|---|---|---|---|
| M1 — `half is null` kolunda `lone.Quality` `WindowSample`a konmuyor | yarım yok kolu yine sessiz | `AyniPencereHizliYoldaOlculuyorYarimYokKolundaOlculmuyor`, `KucukKaynakOlcerVarkenNormalKaynakKadarKaliteUretiyor` | 2 kırmızı / 33 yeşil |
| M2 — yedek yolda `full.Quality` `WindowSample`a konmuyor | yedek yol yine sessiz | `AyniPencereHizliYoldaOlculuyorYedekYoldaOlculmuyor` | 1 kırmızı / 34 yeşil |
| M3 — `MeasuredSampleAsync` ölçeri çağırıp cevabını atıyor | ortak yardımcı | üçü birden | 3 kırmızı / 32 yeşil |
| mutasyonsuz | — | — | 0 kırmızı / 35 yeşil |

M1 iki ölçüyü kırıyor çünkü ikisi de **aynı düzeltmeyi** ölçüyor: biri birim
düzeyinde (`SampleWindowAsync`), öbürü üretim girişinde (`RunDetailedAsync`).
Çapraz bulaşma değil; M1 yedek yol ölçüsüne, M2 yarım yok ölçülerine hiç
dokunmuyor. M3 ortak düğüm olduğu için üçünü birden kırıyor — düğümün gerçekten
yük taşıdığının kanıtı.

**M1 ilk turda kaçağı yakaladı.** `KucukKaynakOlcerVarkenNormalKaynakKadar
KaliteUretiyor` başta ölçerin **çağrılma sayısını** karşılaştırıyordu; M1 ölçeri
yine çağırdığı için ölçü yeşil kalıyordu — yani düzeltmeyi değil sadece çağrıyı
bağlıyordu. Ölçü, iki koşumun `QualityMeasurements` sayısını karşılaştıracak
şekilde değiştirildi (`75a658a`). Hâlâ sabit yok, karşılaştırma var.

## K7 — Verify'in her kolu gerçekten test buluyor

`dotnet test -c Release --list-tests` ile sayıldı:

| kol | bulunan test |
|---|---|
| `ComplexityProbeTests` | 35 |
| `ComplexityScanTests` | 42 |
| `PlanCalculatorProbeTests` | 25 |

Sıfır bulan kol yok.

İlk sayımda `PlanCalculatorProbeTests` **sıfır** çıktı ve teslimi durdurmak
üzereydim; ham `--list-tests` çıktısına bakınca kolun değil sayma desenimin
bozuk olduğu görüldü. Gerçek sayı 25. Sayılar yukarıda `^    VidShrink.Tests\.`
deseniyle yeniden sayıldı.

Verify komutunun tamamı — `dotnet test -c Release --filter
"ComplexityProbeTests|ComplexityScanTests|PlanCalculatorProbeTests"`:
**102 geçti, 0 kaldı, 0 atlandı.**

### Tam küme yeşil bitmiyor — bu dalın kusuru değil

`dotnet test -c Release` (filtresiz) bu makinede **tamamlanmıyor**: test ana
işlemi çöküyor ve koşum "Test Çalıştırması Durduruldu" ile kesiliyor. Üç
denemede üç ayrı yerde, sırasıyla 81 / 36 / 132 test sonra. `--blame` çöküş
anında koşan testi `PanelHostTests.Devir_sinirindaki_bosluk_olculur` diye
gösterdi; o sınıf tek başına koşturulduğunda 13 ölçünün 13'ü geçiyor.

Kusurun bu dala ait olup olmadığı ölçüldü: `origin/main` (`1a60a09`) temiz bir
worktree'ye çıkarıldı, `--no-incremental` ile derlendi ve aynı filtresiz koşum
yapıldı. **Aynı çöküş orada da oldu**, bu kez
`QualityTargetTests.SearchLandsWithinTheMeasuredTolerance` üzerinde.

Sonuç: çöküş T141 öncesinden var ve bu sözleşmenin değiştirdiği hiçbir şeye
dokunmuyor — iki çöküş de arayüz/zamanlama testlerinde, `ComplexityProbe`
zincirinin dışında. K7'nin "tamamı yeşil olmadan teslim yok" şartı bu makinede
**bu dalla sağlanamıyor**; sağlanamamasının nedeni dalda değil. T0'a bildirildi.

### CI

Dalın son commit'i (`0ec4633`) için CI koşumu **`33682771946` — `completed success`**,
26 dk 32 sn. Aynı dalda daha önceki koşum `33681585349`, yeni push onu geçtiği için
`cancelled` göründü; bir başarısızlık değil, üzerine yazılmış bir koşum.

Yani filtresiz `dotnet test` **CI'da yeşil bitiyor.** Yukarıdaki çöküş bu geliştirme
makinesine özgü: burada aynı anda başka sözleşmeler koşuyor ve Avalonia arayüz
testleri yük altında test ana işlemini düşürüyor. CI temiz bir makinede tek başına
koştuğu için o çöküş orada oluşmuyor.

## Açık borç

1. **`SplitSampleAsync` ölçere kodlama son teslim tarihinin artığını veriyor.**
   Orada `token` = `linked(ct, SampleTimeout)` ve saat ffmpeg başlarken kuruluyor;
   kodlama 90 saniyenin çoğunu yerse ölçere kalan süre sıfıra yaklaşıyor ve ölçüm
   sessizce `null` döner. Yeni `MeasuredSampleAsync` bu davranışı kopyalamadı,
   ölçeri `ct` ile çağırıyor. İki yol bu noktada ayrık; birleştirilmesi ayrı iş.
2. **Ölçer patladığında hâlâ sessizlik var.** `MeasureWindowAsync` istisna atarsa
   üç yolda da `null` dönüyor ve tüketen taraf atlıyor. Bu, hızlı yolun bugünkü
   davranışı; K1–K7'nin hiçbiri istemedi, kapsam büyütülmedi. Kapatmak
   `ProbeResult`a ölçülemeyen pencere sayısı eklemeyi gerektirir ve o dosya
   `owns` dışında.
3. **Filtresiz `dotnet test` bu geliştirme makinesinde çöküyor, `main`de de çöküyor.**
   Yukarıda ölçüldü; CI'da aynı koşum yeşil bitiyor, yani kusur yükün altındaki
   arayüz testlerinde. Ayrı bir iş; T141'in değiştirdiği dosyalarla ilgisi yok.
4. **Yedek yolun üretimdeki sıklığı hâlâ bilinmiyor.** Beş tetikleyicinin hepsi
   ortam kaynaklı; ne kadar sık olduğunu söyleyecek bir alan verisi yok.
   Sayaç eklemek bu sözleşmenin işi değildi.
5. **Yedek yolun yarım örneği ölçülmüyor.** `MeasuredSampleAsync` yalnız tam
   ölçek örneğini ölçere veriyor; yarım örnek eskisi gibi `SampleAsync` ile
   alınıp siliniyor. Kalite karşılaştırması zaten tam ölçek üzerinden yapıldığı
   için iddia bundan büyük değil.
