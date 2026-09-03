# VideoToolbox bağlama — satıcı tanındı, donanım kapısı kapalı kaldı

**Tarih:** 02.09.2026 · **Sözleşme:** `.claude/relay/contracts/T149.md` · **Dal:** `T149-videotoolbox-satici`

Dayandığı ölçüm `docs/olcumler/videotoolbox.md` (Serkan, Apple M1, 2 Eylül 2026).
Bu belge kod değişikliğinin belgesi; **burada yeni bir ffmpeg ölçümü yok**. Aşağıdaki
sayıların hepsi test koşumlarından ve her biri ham çıktısıyla birlikte veriliyor.

Bu turda yazılan üç dosya: `src/VidShrink.Core/CodecModel.cs`,
`tests/VidShrink.Tests/CodecModelTests.cs` (yeni),
`tests/VidShrink.Tests/PlanParserTests.cs`. `src/VidShrink.Core/PlanParser.cs`
sözleşmenin `owns` listesindeydi ama **değişmedi** — nedeni K4'te.

## K1 — Kusur önce ölçüldü

Kusur commit'i `6e7bdb4`, düzeltmelerden ayrı. İki ölçü de o commit'te kırmızıydı:

```
[xUnit.net]     VidShrink.Tests.CodecModelTests.VideoToolboxIsNotFiledUnderSoftware(codec: "hevc_videotoolbox") [FAIL]
  Hata İletisi:
   Assert.NotEqual() Failure: Values are equal
Expected: Not Software
Actual:       Software

[xUnit.net]     VidShrink.Tests.CodecModelTests.VideoToolboxIsNotFiledUnderSoftware(codec: "h264_videotoolbox") [FAIL]
  Hata İletisi:
   Assert.NotEqual() Failure: Values are equal
Expected: Not Software
Actual:       Software

[xUnit.net]     VidShrink.Tests.PlanParserTests.ParserAcceptsVideoToolboxEncoders(codec: "hevc_videotoolbox") [FAIL]
  Hata İletisi:
   Unsupported codec: hevc_videotoolbox | Preset 'slow' is invalid for codec 'hevc_videotoolbox'.

[xUnit.net]     VidShrink.Tests.PlanParserTests.ParserAcceptsVideoToolboxEncoders(codec: "h264_videotoolbox") [FAIL]
  Hata İletisi:
   Unsupported codec: h264_videotoolbox | Preset 'slow' is invalid for codec 'h264_videotoolbox'.

Başarısız! - Başarısız:     4, Başarılı:     3, Atlanan:     0, Toplam:     7, Süre: 45 ms - VidShrink.Tests.dll (net8.0)
```

`IsHardware`ın da bugün `false` verdiği ayrıca gösteriliyor; o ölçü bugünkü değeri
pimlediği için kırmızı değil, K2'de.

Parser kırmızısının hata iletisi bu turun en belirleyici tek satırı: kodek
kapısının yanında **ikinci bir kapı** daha var — `Preset 'slow' is invalid`.
İkisi de aynı `Parse` çağrısında birikiyor. Sonuçları K4'te.

## K2 — Karar: satıcı tanınır, kapı açılmaz

Sözleşmenin sunduğu üç cevaptan **(3)** seçildi.

**Yapılan.** `EncoderVendor`e `VideoToolbox` eklendi (listenin sonuna; mevcut
üyelerin sayısal değerleri oynamadı). `Vendor()` artık `videotoolbox` dizgisini
`nvenc` / `qsv` / `amf` ile aynı biçimde tanıyor.

**Yapılmayan.** `IsHardware` eskiden `Vendor(codec) != EncoderVendor.Software`
diyordu; öyle bıraksaydım yeni satıcı kapıyı **kendiliğinden** açardı. Bu yüzden
kapı artık "yazılım değil" diye sormuyor, sabitlerin taşındığı satıcıları adıyla
sayıyor:

```csharp
public static bool IsHardware(string codec) => Vendor(codec) switch
{
    EncoderVendor.Nvenc or EncoderVendor.Qsv or EncoderVendor.Amf => true,
    _ => false
};
```

**Gerekçe.** Kapının arkasındaki sabitler NVENC üzerinde ölçüldü:
`HardwareFloorFactor` 1,52 (`CodecModel.cs:19`), `HardwareBitrateYield` 0,877
(`:18`), `HardwareDeliveryReserveK` 11 (`PlanCalculator.cs:116`),
`HardwarePeakCeiling` 1,10 (`FfmpegArguments.cs:150`). `bppf-tabani.md` §5.3 bu
çarpanın ölçülmemiş QSV ve AMF'ye de uygulandığını kendi yazıyor:
"`IsHardware` çarpanı QSV ve AMF'ye de uyguluyor, oysa ölçülen yalnız NVENC".
VideoToolbox'ı aynı kapının arkasına koymak bu kusuru dördüncü kez tekrar ederdi.

Cevap (2) — VideoToolbox'a **ayrı** sabit kümesi — de bugün yazılamaz. Serkan'ın
raporu bunu açıkça söylüyor: kol başına tek bit hızı (5500k) ölçüldü, taban/diz
eğrisi için kol başına sekiz nokta gerekir; üstelik tek makine, tek kuşak (M1).

**Pimleyen ölçü:** `CodecModelTests.VideoToolboxStaysOffTheHardwarePath`.
`IsHardware`ın `false` olduğunu doğrudan tutmakla yetinmiyor, kapının arkasındaki
beş davranışı **yazılım ikizine eşit, donanım ikizine eşitsiz** olarak tutuyor:
`FloorBppf`, `QualityLimit`, `MinBitrateK`, `FfmpegArguments.NeedsTwoPasses`,
`CostsQualityInHardware`. Sabit yazılı değil; kıyas ikizler üzerinden yapılıyor,
böylece ölçü sabitin değerini değil **kararı** pimliyor.

Bu turda **hiçbir yeni sayısal sabit eklenmedi** — ne `CodecModel.cs`e ne başka
bir dosyaya.

## K3 — `RelativeBitrateNeed` tablosuna girmedi

Sayı değiştirilmedi, tabloya satır eklenmedi. Bugün `hevc_videotoolbox` ve
`h264_videotoolbox` `_ => 1.0` dalına düşüyor (`CodecModel.cs:108`).

**Neden değiştirilmedi — ölçülmedi.** 1,0 ölçülmüş bir sayı değil, tablonun
dışında kalmanın sonucu. Yerine konacak bir oran türetmek için kol başına tek
bit hızı yetmiyor; `bppf-tabani.md`nin düzeneği kol başına sekiz nokta istiyor,
Serkan'ın ölçümünde kol başına bir nokta var (5500k). Yön doğru olabilir —
`hevc_videotoolbox` aynı bit hızında `libx265`in p10'unun 6,383 / 17,365 / 33,357
puan altında kaldı — ama yönü bilmek sayıyı vermiyor, ve o üç sayının kendisi
beş kattan geniş bir aralık.

**Pimleyen ölçü:** `CodecModelTests.VideoToolboxHasNoRowInTheBitrateNeedTable`.
Ölçü 1,0 sabitini yazmıyor; iki kodeğin de **tabloda hiç olmayan** bir kodekle
(`an_encoder_that_does_not_exist`) aynı değeri döndürdüğünü, `libx265` ve
`hevc_nvenc`ten farklı döndürdüğünü tutuyor. Tabloya bir VideoToolbox satırı
eklendiği anda kırmızı olur; K5'in M4'ü bunu ölçüyor.

## K4 — `AllowedCodecs` genişletilmedi; etkisi sayıldı

Genişletme **denendi, ölçüldü ve geri alındı**. Listeye
`"hevc_videotoolbox", "h264_videotoolbox"` eklenip (12 → 14 kodek) tam süit
koşuldu:

```
Başarısız! - Başarısız:     4, Başarılı:  1466, Atlanan:    17, Toplam:  1487, Süre: 17 m 37 s - VidShrink.Tests.dll (net8.0)
```

Düşen dört testin ham listesi:

```
  Başarısız VidShrink.Tests.WindowLayoutTests.ThePageContentStaysAtItsPinnedHeight(loaded: True, narrow: True, least: 1002, most: 1102) [2 s]
  Başarısız VidShrink.Tests.PreviewSegmentTests.Planlayicinin_kabul_ettigi_her_kodlayici_siniflandirilmis [1 ms]
  Başarısız VidShrink.Tests.PlanParserTests.ParserStillRejectsVideoToolboxEncoders(codec: "h264_videotoolbox") [< 1 ms]
  Başarısız VidShrink.Tests.PlanParserTests.ParserStillRejectsVideoToolboxEncoders(codec: "hevc_videotoolbox") [< 1 ms]
```

**Genişletmeden etkilenen test sayısı: 3.** Dördüncüsü değil; ayrımı aşağıda ölçtüm.

| Test | Etkilendi mi | Ne diyor |
|---|---|---|
| `PreviewSegmentTests.Planlayicinin_kabul_ettigi_her_kodlayici_siniflandirilmis` | **evet** | `PlanParser.AllowedCodecs`'te olup `PreviewSegment`'in kalite ölçeğini bilmediği kodlayıcı: `hevc_videotoolbox`, `h264_videotoolbox` |
| `PlanParserTests.ParserStillRejectsVideoToolboxEncoders(hevc_videotoolbox)` | **evet** | bu turda yazılan pim; genişletme tuttuğu kararı bozuyor |
| `PlanParserTests.ParserStillRejectsVideoToolboxEncoders(h264_videotoolbox)` | **evet** | aynı |
| `WindowLayoutTests.ThePageContentStaysAtItsPinnedHeight(True, True)` | **hayır** | `Assert.InRange()` 956, beklenen 1002–1102 |

Son satırın genişletmeyle ilgisi olmadığı tahmin değil, ölçüldü: `src/` altındaki
bütün değişikliklerim geri alınıp (`git diff origin/main -- src` boş çıktı)
yeniden koşuldu, aynı testin aynı kolu aynı sayıyla düştü:

```
Actual: 956
Başarısız! - Başarısız:     1, Başarılı:     3, Atlanan:     0, Toplam:     4, Süre: 1 s - VidShrink.Tests.dll (net8.0)
```

Bu kırmızı T149'dan önce geliyor. Bu turda ne düzeltildi ne dokunuldu — borç
olarak aşağıda.

### Karar: yapılmadı, bildiriliyor

Sözleşme "bir test 'geçersiz kodek' beklerken artık geçerli sayıyorsa **yapma,
bildir**" diyor. Tam olarak bu oldu, iki ayrı sebeple.

**1. Kırılan şey parser değil, testin varsayımı — ve varsayım doğru.**
`PreviewSegment.ModelledCodecs` (`PreviewSegment.cs:48-54`) on iki kodek sayıyor,
hiçbiri VideoToolbox değil. Testi yeşile döndürmenin iki yolu vardı: `PreviewSegment`e
VideoToolbox için bir kalite ölçeği eklemek — **ölçülmemiş sabit**, bu sözleşmenin
tek yasağı — ya da ölçüyü zayıflatmak. İkisi de yapılmadı.

**2. Genişletme tek başına zaten işe yaramıyor.** Kodek kapısını geçen plan bu
sefer bir sonraki kapıda duruyor. Mutasyon koşumunun ham iletisi:

```
   Assert.Contains() Failure: Filter not matched in collection
Collection: ["Preset 'slow' is invalid for codec 'hevc_videotool"···]
```

`FfmpegArguments.Presets` on üç kodek tanıyor (`FfmpegArguments.cs:25-40`),
hiçbiri VideoToolbox değil; `DefaultPreset` VideoToolbox'ı tanımadığı için
`_ => "slow"` dalına düşüyor ve `IsValidPreset` onu reddediyor. `FfmpegArguments.cs`
bu sözleşmenin `owns` listesinde değil. Yani `AllowedCodecs`i genişletmek planı
geçerli kılmıyor, yalnız **reddin yerini** kodek kapısından ön ayar kapısına
taşıyor.

Bunun sonucu olarak K1'in ikinci kırmızısı (`ParserAcceptsVideoToolboxEncoders`)
**geri çekildi ve dosyadan çıkarıldı**; yerine bugünkü reddi tutan
`ParserStillRejectsVideoToolboxEncoders` yazıldı. `PlanParser.cs` bu turda hiç
değişmedi.

## K5 — Mutasyon ızgarası

Her mutasyon tek başına uygulandı, `dotnet build -c Release --no-incremental` ile
derlendi, sonra iki kollu verify koşuldu. Hiçbir mutasyonda `--no-build` ile
atlanan bir derleme yok; `--no-build` yalnız hemen öncesindeki `--no-incremental`
derlemesinin çıktısını koşmak için kullanıldı.

| # | Geri alınan düzeltme | Kırmızıya dönen ölçü | Sonuç |
|---|---|---|---|
| M1 | `Vendor()` içindeki `videotoolbox` dalı silindi | `VideoToolboxIsNotFiledUnderSoftware` (2 kol) | Başarısız 2 / Başarılı 9 / Toplam 11 |
| M2 | `IsHardware` yeniden `Vendor(codec) != EncoderVendor.Software` | `VideoToolboxStaysOffTheHardwarePath` (2 kol) | Başarısız 2 / Başarılı 9 / Toplam 11 |
| M3 | `AllowedCodecs`e iki VideoToolbox kodeği eklendi | `ParserStillRejectsVideoToolboxEncoders` (2 kol) | Başarısız 2 / Başarılı 9 / Toplam 11 |
| M4 | `RelativeBitrateNeed` tablosuna iki VideoToolbox satırı eklendi | `VideoToolboxHasNoRowInTheBitrateNeedTable` (2 kol) | Başarısız 2 / Başarılı 9 / Toplam 11 |

Dört mutasyonun dördü de yakalandı ve her biri **yalnız kendi ölçüsünü** kırdı.
Ham çıktı:

```
### M1: Vendor() videotoolbox dizgisini tanimiyor
  Başarısız VidShrink.Tests.CodecModelTests.VideoToolboxIsNotFiledUnderSoftware(codec: "hevc_videotoolbox") [2 ms]
  Başarısız VidShrink.Tests.CodecModelTests.VideoToolboxIsNotFiledUnderSoftware(codec: "h264_videotoolbox") [< 1 ms]
Başarısız! - Başarısız:     2, Başarılı:     9, Atlanan:     0, Toplam:    11, Süre: 64 ms - VidShrink.Tests.dll (net8.0)

### M2: IsHardware yeniden 'Software degil' kapisi
  Başarısız VidShrink.Tests.CodecModelTests.VideoToolboxStaysOffTheHardwarePath(codec: "h264_videotoolbox", softwareTwin: "libx264", hardwareTwin: "h264_nvenc") [< 1 ms]
  Başarısız VidShrink.Tests.CodecModelTests.VideoToolboxStaysOffTheHardwarePath(codec: "hevc_videotoolbox", softwareTwin: "libx265", hardwareTwin: "hevc_nvenc") [< 1 ms]
Başarısız! - Başarısız:     2, Başarılı:     9, Atlanan:     0, Toplam:    11, Süre: 55 ms - VidShrink.Tests.dll (net8.0)

### M3: AllowedCodecs iki VideoToolbox kodegini kabul ediyor
  Başarısız VidShrink.Tests.PlanParserTests.ParserStillRejectsVideoToolboxEncoders(codec: "h264_videotoolbox") [3 ms]
  Başarısız VidShrink.Tests.PlanParserTests.ParserStillRejectsVideoToolboxEncoders(codec: "hevc_videotoolbox") [< 1 ms]
Başarısız! - Başarısız:     2, Başarılı:     9, Atlanan:     0, Toplam:    11, Süre: 61 ms - VidShrink.Tests.dll (net8.0)

### M4: RelativeBitrateNeed tablosuna iki VideoToolbox satiri
  Başarısız VidShrink.Tests.CodecModelTests.VideoToolboxHasNoRowInTheBitrateNeedTable(codec: "h264_videotoolbox") [10 ms]
  Başarısız VidShrink.Tests.CodecModelTests.VideoToolboxHasNoRowInTheBitrateNeedTable(codec: "hevc_videotoolbox") [< 1 ms]
Başarısız! - Başarısız:     2, Başarılı:     9, Atlanan:     0, Toplam:    11, Süre: 58 ms - VidShrink.Tests.dll (net8.0)
```

Hiçbir ölçü sabiti sabitle karşılaştırmıyor. `VideoToolboxStaysOffTheHardwarePath`
yazılım/donanım ikizleriyle, `VideoToolboxHasNoRowInTheBitrateNeedTable` tabloda
olmayan bir kodekle, `ParserStillRejectsVideoToolboxEncoders` üretilen hata
iletisiyle kıyaslıyor. `VideoToolboxIsNotFiledUnderSoftware` bir enum üyesiyle
karşılaştırıyor; o üye, verilen kodek adının ürettiği kararın kendisi.

## K6 — Her verify kolu gerçekten test buluyor

Verify komutu iki kollu: `--filter "CodecModelTests|PlanParserTests"`.
`--list-tests` ile kol başına sayım:

| Kol | Bulunan test | Nasıl sayıldı |
|---|---:|---|
| `CodecModelTests` | **6** | `dotnet test -c Release --no-build --list-tests --filter "CodecModelTests"` |
| `PlanParserTests` | **5** | aynı komut, `--filter "PlanParserTests"` |

Sıfır bulan kol yok. `CodecModelTests` kolunun altı testi (üç `[Theory]` × iki kodek):

```
    VidShrink.Tests.CodecModelTests.VideoToolboxIsNotFiledUnderSoftware(codec: "hevc_videotoolbox")
    VidShrink.Tests.CodecModelTests.VideoToolboxIsNotFiledUnderSoftware(codec: "h264_videotoolbox")
    VidShrink.Tests.CodecModelTests.VideoToolboxStaysOffTheHardwarePath(codec: "hevc_videotoolbox", softwareTwin: "libx265", hardwareTwin: "hevc_nvenc")
    VidShrink.Tests.CodecModelTests.VideoToolboxStaysOffTheHardwarePath(codec: "h264_videotoolbox", softwareTwin: "libx264", hardwareTwin: "h264_nvenc")
    VidShrink.Tests.CodecModelTests.VideoToolboxHasNoRowInTheBitrateNeedTable(codec: "hevc_videotoolbox")
    VidShrink.Tests.CodecModelTests.VideoToolboxHasNoRowInTheBitrateNeedTable(codec: "h264_videotoolbox")
```

`PlanParserTests` kolunun beş testi (üçü bu turdan önce vardı):

```
    VidShrink.Tests.PlanParserTests.ParserRemovesOutputCreatingExtraArgumentsAsWholePairs
    VidShrink.Tests.PlanParserTests.ParserHonorsDisabledResolutionAndFrameRateReduction
    VidShrink.Tests.PlanParserTests.ParserRejectsAspectRatioDistortion
    VidShrink.Tests.PlanParserTests.ParserStillRejectsVideoToolboxEncoders(codec: "hevc_videotoolbox")
    VidShrink.Tests.PlanParserTests.ParserStillRejectsVideoToolboxEncoders(codec: "h264_videotoolbox")
```

İki kol birlikte, düzeltmeler yerindeyken:

```
Başarılı!  - Başarısız:     0, Başarılı:    11, Atlanan:     0, Toplam:    11, Süre: 53 ms - VidShrink.Tests.dll (net8.0)
```

11 = 6 + 5; iki kolun kesişimi boş.

### Dalın tam süiti

İki kollu verify dar bir pencere, bu yüzden dal ayrıca tam süitle koşuldu:

```
Başarısız! - Başarısız:     1, Başarılı:  1469, Atlanan:    17, Toplam:  1487, Süre: 15 m 17 s - VidShrink.Tests.dll (net8.0)
```

Tek kırmızı, K4'te ölçülen ve `main`den gelen
`WindowLayoutTests.ThePageContentStaysAtItsPinnedHeight(True, True)`. Sayılar
K4'ün genişletme koşumuyla tutuyor: orada 1466 geçmişti, burada 1469 —
genişletmenin düşürdüğü üç test tam da bu farkı veriyor. Toplam iki koşumda da
1487; atlanan iki koşumda da 17, CI kapısının `-MaximumSkipped 30` sınırının
altında.

### CI koşumu — yeşil

| koşum | head | sonuç | Başarısız / Başarılı / Atlanan / Toplam |
|---|---|---|---|
| `33677212181` | `2a24a00` | **success** | 0 / 1468 / 19 / 1487 |

```
Passed!  - Failed:     0, Passed:  1468, Skipped:    19, Total:  1487, Duration: 26 m 34 s - VidShrink.Tests.dll (net8.0)
```

Tek iş (`test`) `success`; koşum kapısı `-MinimumTotal 1134 -MaximumSkipped 30`
şartıyla geçti.

Bu koşumdan önce dal kırmızıydı (`33673808806`, `7790f1d`): tek kırmızı
`WindowLayoutTests.ThePageContentStaysAtItsPinnedHeight(True, True)`, ölçülen 956,
pin 1002-1102. Kırmızı T149'dan gelmiyordu — `main`in kendi koşumu (`33670207004`,
`ae98712`) aynı testte aynı değerle düşüyordu. T0 kök nedeni kapattı: üç yerelleştirme
anahtarı hiçbir dilde yokken arayüz ham anahtar metnini basıyor, o metin dar pencerede
satıra sarıp sayfayı uzatıyordu; `b2f2c62` anahtarları getirdi, `755b78c` pini bozuk
arayüzün üzerinden alıp 906-1006'ya temellendi. Dal o `main`e rebase edildi
(`9626b68` → `2a24a00`), kod değişmedi: `git diff --stat origin/main -- src tests`
bu turun üç dosyasını gösteriyor, başka bir şey göstermiyor.

## Bu turda kapanmayan işler

- **İkinci aşama Mac gerektiriyor.** `IsHardware`ı VideoToolbox'a açmak, kol
  başına sekiz noktalı bppf eğrisi ölçülmeden yapılamaz. Serkan'ın
  `tools/videotoolbox/olc.sh`i bunun temeli.
- **`PlanCalculator.FastHardwareOrder` yedi kodlayıcı sayıyor**, hiçbiri
  VideoToolbox değil (`PlanCalculator.cs:127-130`). Dosya T139'un, dokunulmadı.
  Kapı kapalı olduğu için bugün bir davranış farkı yaratmıyor; kapı açıldığında
  bu liste de ölçüme dayanmak zorunda.
- **`FfmpegArguments.Presets` VideoToolbox tanımıyor.** `owns` dışında. Kodek
  listesi genişletilirse bu da genişlemek zorunda, yoksa plan ön ayar kapısında
  düşer.
- **`PreviewSegment.ModelledCodecs` VideoToolbox tanımıyor.** `owns` dışında.
  Kalite ölçeği ölçülmeden eklenemez.
- **`WindowLayoutTests` kırmızısı kapandı** — bu turun işi değildi, T0 `main`de
  `b2f2c62` + `755b78c` ile kapattı. Bu raporun eski sürümlerinde "`main`de kırmızı"
  diye geçen satır artık geçerli değil; dalın CI koşumu yeşil.
- **`owns` listesinde bir eksik var.** Sözleşme `src/VidShrink.Core/PlanParser.cs`i
  ve `tests/VidShrink.Tests/CodecModelTests.cs`i sayıyor ama
  `tests/VidShrink.Tests/PlanParserTests.cs`i saymıyor; öte yandan verify komutu
  `PlanParserTests` kolunu açıkça filtreliyor. K1'in ikinci kırmızısı ve K4'ün pimi
  o dosyaya yazıldı — parser testinin yeri orası. T0'ın onayına açık; alternatifi
  aynı ölçüyü `CodecModelTests.cs`e koymaktı, o da testi yanlış dosyaya koyardı.
- **CI koşum kapısının alt sınırı** `-MinimumTotal 1134` (`ci.yml`). Süit bu
  dalda 1487 test sayıyor ve bu sayı turun eklediği sekiz testi zaten içeriyor.
  Alt sınır geçiliyor ama gerçek sayının çok altında. `ci.yml` `owns` dışında.
