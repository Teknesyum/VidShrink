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
