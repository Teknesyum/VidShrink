# Recalculate yolundaki ffmpeg yoklaması

T130/K1. Öncül T94'ün denetçisinden geldi: `Recalculate` arayüz iş parçacığında
`BuildDetailed` çağırıyor, o da canlı ffmpeg yoklaması doğuruyor. Burada iddia
doğrulanıyor ve büyüklüğü ölçülüyor.

Ölçü: `tests/VidShrink.Tests/PlanCalculatorProbeTests.cs`.
Makine: Windows 11, av1_nvenc çalışıyor. Ölçüm boş makinede, 2026-09-02.

## Zincir doğrulandı

Üç katmanda ayrı ayrı ölçüldü, üçü de iddiayı destekliyor.

**1. Çağrı dizisi.** `BuildDetailed` hızlı modda donanım adaylarını tek tek yetenek
nesnesine soruyor. Hiçbiri çalışmadığında dizi tam olarak şu:

```
works:av1_nvenc works:hevc_nvenc works:av1_qsv works:hevc_qsv
works:av1_amf works:hevc_amf works:h264_nvenc works:libx265
```

Sekizinci çağrı sözleşmede yoktu: `PickFastCodec` yedi adayda düşünce yedek kodlayıcı
`libx265` oluyor ve HDR kaynakta `HdrResolver.SupportsHdr10` onu da yetenek nesnesine
soruyor (`HdrResolver.cs:57`). Donanım çalışıyorsa tarama ilk adayda duruyor, bu kez
piksel biçimi yoklaması açılıyor: `works:av1_nvenc hdr10:av1_nvenc`.

**2. Çağrı senkron.** Yetenek nesnesi her soruda 20 ms beklediğinde `BuildDetailed`
de bekliyor; süre çağrı sayısıyla doğru orantılı artıyor.

**3. Bekleyen taraf arayüz iş parçacığı.** Gerçek `MainWindow`, gerçek yol
(`LoadWithoutProbing` → `ApplyLoaded` → `Recalculate`), arayüz iş parçacığında.
Yalnız ffmpeg'in yerinde bekleyen bir taklit duruyor ve arayüz iş parçacığı
yoklama sayısı × gecikme kadar bekliyor.

**4. Gerçek ffmpeg.** Taze bir `EncoderCapabilities` ile plan kuruluyor, o sırada
doğan ffmpeg süreçleri PID'leriyle sayılıyor.

| Durum | Süreç (beklenen) | Süreç (görülen) | Süre |
|---|---|---|---|
| SDR, önbellek soğuk | 1 | 1–3 | 173–469 ms |
| HDR, önbellek soğuk | 2 | 2–3 | 374–599 ms |
| SDR, ısıtılmış | 0 | 0 | 0 ms |
| HDR, ısıtılmış SDR önbelleğinden sonra | 1 | 1 | 181–243 ms |
| HDR, ikinci kez | 0 | 0 | 0 ms |

Süreç sayacı yukarı kayabiliyor: aynı makinede koşan başka bir ölçümün ffmpeg'i de
sayıya girer. Sıfır olması gereken satırlar dört koşumda da sıfır çıktı; asıl yükü
taşıyan satırlar bunlar.

## Öncülün üç düzeltmesi

**Tekrar koşulu.** "T94 zaman aşımını önbelleğe yazmadığı için bu her `Recalculate`'te
tekrarlanıyor" cümlesi koşullu doğru. `EncoderCapabilities.cs:89` şunu diyor:

```csharp
var (result, timedOut) = ProbeEncoder(codec);
if (!timedOut) _probed[codec] = result;
```

Zaman aşımına **uğramayan** sonuç süreç ömrü boyunca önbellekte. Yani tekrar yalnız
yoklama 15 s'yi aştığında oluyor; altında kalan yoklama bir kez koşuyor. Tabloda
"ikinci kez 0 süreç" satırı bunu gösteriyor.

**Büyüklük.** Sözleşmenin verdiği 15–45 s'lik donma bu makinede ölçülmedi. Ölçülen
en kötü durum 599 ms. 3625–14855 ms rakamı `handbrake-acigi.md:246`'dan geliyor ve
**dokuz ajan koşarken** alınmış bir yoklama süresi; boş makinede aynı komut saniyenin
altında. 15–45 s tavanı yüklü makine + askıda kalan donanım yoklaması gerektiriyor.
Kusur gerçek, tavan koşullu.

**Isıtma zaten var.** Açılışta `ProbeHardwareEncodersAsync` `Task.Run` içinde
`PlanCalculator.Build(HardwareProbeSource, …)` çağırıyor (`MainWindow.axaml.cs:1219`).
Bu, hızlı yolun SDR yoklamasını arka planda ısıtıyor. `HardwareProbeSource` HDR
değil, dolayısıyla **piksel biçimi yoklaması hiç ısıtılmıyor**: kullanıcı ilk HDR
dosyayı yüklediğinde o yoklama arayüz iş parçacığına düşüyor.

Arayüz iş parçacığında kalan gerçek açık iki tane:

1. İlk HDR yüklemesindeki piksel biçimi yoklaması (ısıtılmamış).
2. Zaman aşımına uğrayan her yoklama (önbelleğe girmiyor, her `Recalculate`'te
   yeniden koşuyor).

Açılış ısıtması bitmeden dosya yüklenirse SDR yoklaması da arayüze düşer.

## İkinci yol: aynı yoklama HDR kararını da veriyor

T125'in A/B düzeneği bu sözleşmeden habersiz aynı yoklamaya çarptı. Arka arkaya iki
özdeş koşum, aynı komut, aynı ikili, aynı yerleşim (882x496):

| koşum | piksel | HDR | renk kapısı | harm |
|---|---|---|---|---|
| serbest (1) | yuv420p | SDR'a tonemap | ReferenceTransformed | 25,85 |
| serbest (2) | p010le | korundu | Direct | 27,57 |

Fark 1,72 puan; ölçerin bilinen sürüm ofseti 0,86. Yani kararsızlık ölçüm gürültüsünün
iki katı ve dönen tek şey HDR yolu.

Sebep K1'in ölçtüğü zincirin ikinci ucu. `HdrResolver.cs:24` HDR korumayı
`SupportsHdr10`'a soruyor, o da yazılım kodlayıcılarda (`libx265`, `libsvtav1`)
doğrudan `WorksAsEncoder`a bakıyor. Yoklama bir cevap üretemezse `false` dönüyor ve
ilke tonemap'e düşüyor. Kodek seçimi ayrı yoldan (`HasEncoder`) karar verdiği için
kodek yine `libsvtav1` kalıyor: aynı dosya iki kez sıkıştırıldığında biri HDR biri SDR
çıkabiliyor.

**Öncülün düzeltmeleri.** İki sayı bu ağaçta doğrulanmadı.

*4 saniyelik bütçe yok.* `origin/main`de öldürme sınırı `EncoderCapabilities.ProbeKillMs
= 15000` (`EncoderCapabilities.cs:103`). `src/` içinde kalan tek `4000`,
`HardwareVerdict.cs:48`'deki **eskimiş açıklama satırı**: "Yoklamanın kendi zaman aşımı
4000 ms". T94 sınırı 4000'den 15000'e çıkardı, üstündeki cümle değişmedi. Dosya T129'un
`owns`'unda; düzeltmesi oraya bırakıldı.

*Düşüş tamamen sessiz değil, ama nedeni yanlış söylüyor.* Tonemap'e düşünce
`AdviceCode.HdrTonemapped` ve `ReasonCode.HdrTonemapped` kullanıcıya bir satır
gösteriyor (`PlanCalculator.cs:176-178`). Satırın metni — `main.reason.hdr-tonemapped`:
"seçilen kodlayıcı 10 bit koruyamıyor". Yoklama öldürüldüğünde bu cümle **yanlış**:
kodlayıcının 10 bit taşıyıp taşımadığı bilinmiyor. Kusur "uyarı yok" değil, "uyarı
yanlış nedeni gösteriyor".

*Yoklama süresi.* `libsvtav1` yoklaması bu makinede taze `EncoderCapabilities` ile
24 tekrar: **75–232 ms**. Öldürme sınırının %1,5'i. Boş makinede sınırı aşmak mümkün
değil.

Sınırın **aşıldığı** durum bu sözleşmede ölçülmedi ama başkası ölçtü: T123'ün ölçümü
(T129 sözleşmesinde aktarılıyor) yük altında `h264_nvenc` yoklamasının 15 000 ms sınırını
sekizinci yük basamağında kırdığını, on iki örneğin dokuzunun yanlış cevap verdiğini
söylüyor. Burada yeniden üretilmedi. Sonuç aynı yere çıkıyor: sınır hiçbir sonlu değerde
güvenli değil, o yüzden bu sözleşme sınırı büyütmedi — aşıldığında **cevabı bilinmeyen
saydı**.

## Düzeltme

Depoda zaten olan ayrımın aynısı kuruldu: `FfmpegArguments.CachedPsychovisualArgs`
(saf okuma) ile `WarmPsychovisual` (ölçer, süreç doğurur) nasıl ayrıysa, kodlayıcı
yeteneği de öyle ayrıldı.

`MainWindow.DeferredEncoderAvailability` — arayüz yolunun gördüğü geçit. Okuma tarafı
süreç doğurmaz; sorulan kodlayıcı ölçülmemişse `IEncoderMeasurementState` üzerinden
**"ölçülmedi"** der, "çalışmıyor" demez, ölçümü arka planda kuyruğa alır ve bitince
hesabı yeniler. Kodlama yolu (`DisplayedEncodeArguments`, önizleme) gerçek yetenek
nesnesini görmeye devam ediyor.

Üçüncü durum `PlanResult.HardwareNotMeasured` ve `HdrResolution.NotMeasured` ile
taşınıyor. `IEncoderMeasurementState` **geçici**: T129 `EncoderProbeResult`'a aynı
durumu açıyor, oraya devredilecek.

Yerleşmeyen yoklama (≥ 2000 ms) ölçüm sayılmıyor: en çok bir kez daha deneniyor,
sonrasında deneme duruyor ama cevap bilinmeyen kalıyor ve `main.error.probe` satırıyla
arayüze taşınıyor. Yerleşmeyeni "ölçüldü" saymak T94'ün kaldırdığı kusuru geri
getirirdi.

K2'nin dört yasağının hiçbiri kullanılmadı: zaman aşımı küçültülmedi (`ProbeKillMs`
elle tutulmadı), yoklama silinmedi, `.Result`/`.Wait()` yok, sessizce tonemap'e
düşülmüyor.

## K6 — mutasyon

Düzeltme altı yerden tek tek geri alındı, her seferinde ölçü koşuldu, sonra geri konuldu
(kaynak dosyaların sha256'sı her turdan sonra tabanla karşılaştırıldı).

| # | geri alınan | kırılan ölçü |
|---|---|---|
| M1 | `Recalculate` yine gerçek yetenek nesnesini görüyor (K2) | `TheWindowThreadNoLongerWaitsForTheProbe`, `RepeatedRecalculatesDoNotRepeatTheProbe` |
| M2 | donanım kodlayıcısında "ölçülmedi" kontrolü kalkıyor (K3) | **ilk turda hiçbiri** |
| M3 | yazılım kodlayıcısında "ölçülmedi" kontrolü kalkıyor (K3) | `AnUnmeasuredEncoderDoesNotBecomeATonemapVerdict`, `TheSameSourceFlipsBetweenHdrAndSdrWhenOnlyTheProbeAnswerChanges` |
| M4 | `PickFastCodec` ölçülmemişi "yok" sayıyor (K3) | `AnUnmeasuredFastPathDoesNotBecomeANoHardwareVerdict` |
| M5 | yerleşmeyen yoklama "ölçüldü" sayılıyor (K2, dördüncü yasak) | `AnUnsettledProbeIsNeverPromotedToAMeasurement` |
| M6 | geçit aynı soruyu tekrar yoklamaya gönderiyor (K4) | `ASettledProbeIsReadWithoutSpawningAgain`, `RepeatedRecalculatesDoNotRepeatTheProbe` |

**M2 kırılmadı ve bu bir bulgu.** HDR kararı iki ayrı daldan geçiyor: yazılım kodlayıcısı
(`libsvtav1`, `libx265`) `WorksAsEncoder`a, donanım kodlayıcısı (`av1_nvenc` …)
`Hdr10PixelFormat`e soruyor. Ölçüler yalnız birincisini tutuyordu — T125'in yakaladığı
koşum `libsvtav1` olduğu için. İkinci dal ölçüsüzdü: `av1_nvenc` ile HDR bir kaynakta
yoklama cevap üretemezse sessizce tonemap'e düşerdi ve hiçbir ölçü bunu görmezdi.

`AnUnmeasuredHardwareEncoderDoesNotBecomeATonemapVerdict` eklendi, M2 tekrar koşuldu ve
kırıldı. Tablodaki M2 satırının kalıcı hâli budur.

**Mutasyona girmeyen ölçüler.** Süreç sayan dört gerçek ffmpeg ölçüsü bu turda atlandı:
makinede paralel koşan başka ajanların ffmpeg süreçleri PID sayacına giriyor. Ölçü artık
makine sessiz değilse kırmızı vermiyor, atlıyor ve atladığını kanıt dosyasına yazıyor —
yanlış kırmızı, ölçülmemiş olmaktan kötüdür. Belgedeki süreç sayıları boş makinede
alınmış olanlardır.
