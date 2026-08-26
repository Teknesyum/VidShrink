# Motor doğrulama raporu

T2-T4'te yapılan değişikliklerin gerçek dosyalar üzerinde ölçülmüş karşılaştırması.
Bu rapor kod değiştirmez; bulguları yazar.

## Kurulum

| | |
|---|---|
| Makine | DESKTOP-0J80KVV · AMD Ryzen 7 9700X (8 çekirdek) · 64 GB |
| ffmpeg | 9.0-full_build-www.gyan.dev |
| Eski motor | `d900042` — T4 birleştirmesi `ab08dde`'nin doğrudan ebeveyni |
| Yeni motor | bugünkü `main` |
| Fill kipi | `FillTarget` (ana koşu) |

T0 referans olarak `a2c87f1` demişti. `ab08dde`'nin doğrudan ebeveyni `d900042`; ikisi
arasındaki tek fark `c8cbb72` "Drop the status mirror nobody read" ve bu commit motora
dokunmuyor. Ölçüm `d900042` ile yapıldı.

Eski sürüm `git archive` ile ayrı bir klasöre çıkarıldı ve orada ayrı derlendi; `git stash`
kullanılmadı. İki motor **aynı** bench kaynağıyla derlendi, yani ölçüm farkı yalnız
`src/` farkından geliyor.

## Ölçüm yöntemi

- **VMAF NEG**: `libvmaf=model=version=vmaf_v0.6.1neg`, test akışı `zscale` ile kaynak
  çözünürlüğüne çıkarılıyor. Kare başına skorlardan harmonik ortalama ve p10 alınıyor.
- **XPSNR**: ffmpeg'in `xpsnr` filtresi tek toplam skor basmıyor, yalnız y/u/v veriyor.
  Tek skor `(4y + u + v) / 6` ağırlıklı ortalamasıyla türetiliyor — 4:2:0'da y düzleminin
  piksel sayısı u ve v'nin dört katı olduğu için. **Bu bir tercihtir**; XPSNR
  spesifikasyonunda birebir böyle tanımlandığı doğrulanamadı. Tablolarda y/u/v ayrı ayrı da
  veriliyor ki okuyan kendi ağırlığını uygulayabilsin.
- Ölçüm kodu bench içine alındı. `QualityMeter` ile aynı sayıyı verdiği doğrulandı:
  k4-ekrankaydi / 8 MB vakasında iki yol da VMAF-NEG harmonik 96,75 ve XPSNR 60,02 dedi.

## Harness'ta kapatılan boşluklar

Ölçüme başlamadan önce `tools/VidShrink.Bench` üç yerde gerçek uygulamadan farklı
davranıyordu. Üçü de kapatıldı, yoksa rapor yanlış motoru ölçmüş olurdu.

1. **`fillPolicy` geçirilmiyordu.** T3 fark etmişti. `bench shrink` artık
   `--fill filltarget|qualityceiling` alıyor ve bunu hem `PlanOptions.FillPolicy`'ye hem
   `EncodeRunner.RunAsync`'e veriyor. Her sonuç satırı hangi kipte ölçüldüğünü taşıyor.
2. **`CalibrationProbe` hiç çağrılmıyordu.** Kalibrasyonu yalnız uygulama yapıyordu; bench
   kalibrasyonsuz motoru ölçüyordu. Kalibrasyonsuz profilde `EstimateBand` 0,32, kalibre
   profilde 0,05 — yani bant tutturma ölçümü anlamsız çıkardı. Uygulamanın iki turlu
   döngüsü bench'e taşındı, `--no-calibrate` ile kapatılabiliyor.
3. **Taşınan döngüde kendi hatam vardı.** Uygulama kalibre profili planlamada tutuyor,
   `WithoutCalibration()`'ı yalnız bir sonraki turun *girdisi* olarak kullanıyor. İlk
   taşımada ikisini tek değişkende birleştirmiştim; tur sayısı dolunca kalibrasyon
   atılıyordu ve her satır `kalibre=hayır` görünüyordu.

### Kalibrasyon neden düşük görünüyordu

Sentetik kliplerde kalibrasyonun içerik yüzünden düştüğü **doğru değil**. Üçüncü maddedeki
port hatasıydı; düzeltildikten sonra aynı vaka `kalibre=hayır` → `kalibre=evet` oldu.

İçerik ihtimali ölçerek elendi. `ComplexityProfile.Calibrate`'in tek içerik bağımlı reddi
`lowBppf <= highBppf`, yani iki CRF noktası arasında dosya boyutunun düşmemesi.
`CalibrationProbe`'un örnekleme adımı birebir taklit edildi (2 saniyelik üç pencere,
plan çözünürlüğü, libx264/slow) ve k4-ekrankaydi'nde altı CRF çiftinde de tepki tekdüze:

| crf çifti | düşük CRF bayt | yüksek CRF bayt | düşüyor mu |
|---|---|---|---|
| 20 / 24 | 659 456 | 441 344 | evet |
| 28 / 32 | 301 056 | 229 376 | evet |
| 36 / 40 | 189 440 | 169 984 | evet |
| 38 / 42 | 179 200 | 128 000 | evet |
| 42 / 46 | 128 000 | 71 680 | evet |
| 46 / 50 | 71 680 | 54 272 | evet |

`bench panel --only o3` örneklerin çalıştığını ayrıca doğruladı: k4 için 576 kare / 219 fps,
k1 için 576 kare / 78 fps. Yani örnekler ne başarısız oluyordu ne de tepki düzdü.

## Klip kümesi

| klip | kaynak | boyut | çözünürlük | süre |
|---|---|---|---|---|
| gothic2026-08-15 14-01-29 | kullanıcının gerçek dosyası | 830 MB | 1920x1080@48 | 52,6 s |
| k1-gradyan | lavfi `gradients`, kolay gradyan | 176 MB | 1920x1080@48 | 50 s |
| k2-testdeseni | lavfi `testsrc2`, standart test deseni | 129 MB | 1920x1080@48 | 50 s |
| k3-gurultu | lavfi `color`+`noise`, saf gürültü | 2647 MB | 1920x1080@48 | 50 s |
| k4-ekrankaydi | lavfi, düşük hareketli ekran kaydı benzeri | 24 MB | 1920x1080@48 | 50 s |

Hedefler: 180 / 100 / 25 / 16 / 8 / 3 / 1 MB. Klip başına 7, motor başına 35, toplam 70 vaka.

Beş vakada hedef kaynaktan büyük olduğu için motor `passthrough` seçti ve dosyayı kopyaladı:
k1/180, k2/180, k4/180, k4/100, k4/25. Bu vakalarda bant ölçütü **uygulanmaz** — dosya zaten
hedefin altında, motorun küçültecek bir şeyi yok. Tablolarda oldukları gibi duruyorlar ama
kapı sayımında ayrı tutuldular. Gerekçe aşağıda.

Passthrough vakalarında XPSNR `-` görünüyor: çıktı kaynakla birebir aynı, `xpsnr` filtresi
sonsuz basıyor ve sayı olarak okunamıyor. Ölçülemedi, tahmin yazılmadı.

## Kapılar

| kapı | eşik | eski | yeni | sonuç |
|---|---|---|---|---|
| Hedef aşımı | 0 vaka | 0 / 35 | 0 / 35 | **geçti** |
| Sert taban ihlali | 0 vaka | 0 / 30 gerçek vaka | 0 / 30 gerçek vaka | **geçti** |
| ≥50 MB bant içi | tüm vakalar | 7 / 7 | 7 / 7 | **geçti** |
| 1 MB'de VMAF NEG harmonik | yeni > eski | — | 5 klipten 2'sinde | **kaldı** |
| Toplam ek süre | ≤ %25 | 1431,9 s | 1255,8 s (**%12,3 daha hızlı**) | **geçti** |

Sert taban ve bant sayımı: ham sayım her iki motorda da 3 "ihlal" veriyor — k2/180, k4/180,
k4/100. Üçü de passthrough; hedef kaynaktan büyük olduğu için dosya doğal olarak sert tabanın
altında kalıyor. Motorun kararı doğru, ölçüt uygulanamaz. Passthrough dışındaki 30 vakada
her iki motorda da hedef aşımı ve sert taban ihlali **sıfır**.

≥50 MB hedeflerde passthrough dışında 7 vaka var (gothic 180/100, k1 100, k2 100,
k3 180/100 ve k1 180 — sonuncusu passthrough, sayımdan düştü). Hepsi bant içinde,
doluluk %97,5-%99,2 aralığında. Bant %2,8 genişliğinde ve iki motor da tutturuyor.

## 1 MB vakası — kapının kaldığı yer

Kullanıcının şikâyet ettiği senaryo. T4 burada davranışı değiştirdi: eski motor çözünürlüğü
kırpıp kare hızını 48'de tutuyordu, yeni motor kare hızını da ölçüp kesiyor.

| klip | eski plan | yeni plan | VMAF eski | VMAF yeni | fark | XPSNR eski | XPSNR yeni |
|---|---|---|---|---|---|---|---|
| gothic | 1190x670@48 | 422x238@12 | 1,27 | 1,05 | **-0,22** | 19,56 | 22,77 |
| k1-gradyan | 1266x712@48 | 576x324@32 | 90,87 | 91,99 | **+1,12** | 33,71 | 29,39 |
| k2-testdeseni | 960x540@48 | 576x324@10 | 13,42 | 7,85 | **-5,57** | 19,14 | 15,88 |
| k3-gurultu | 960x540@48 | 498x280@6 | 4,32 | 5,26 | **+0,95** | 8,41 | 12,26 |
| k4-ekrankaydi | 1190x670@48 | 806x454@32 | 74,57 | 53,80 | **-20,77** | 32,85 | 28,47 |

Kapı beş klipte de yeninin yüksek olmasını istiyordu; iki klipte oldu, üçünde olmadı.
**Kapı kaldı ve bu rapor bunu gizlemiyor.**

İki metrik aynı yönü gösteriyor, yani sonuç metrik seçiminden gelmiyor: gothic ve k3'te yeni
motor hem VMAF hem XPSNR'de kazanıyor, k1/k2/k4'te ikisinde de kaybediyor. Ayrım içerik
zorluğunda: karmaşık kaynakta (gerçek oyun görüntüsü, saf gürültü) sert kesme işe yarıyor,
kolay kaynakta (gradyan, test deseni, durağan ekran kaydı) fazla kesiyor. En büyük kayıp
k4-ekrankaydi'nde: 1 MB bütçesi zaten yetiyorken kare hızı 48'den 32'ye, çözünürlük
1190x670'ten 806x454'e indiriliyor ve VMAF 20,8 puan düşüyor.

Ölçüm uyarısı: VMAF referansı 48 fps kaynak. 12 veya 6 fps'e inen çıktıda kareler
tekrarlanıyor ve VMAF bunu ağır cezalandırıyor; VMAF zamansal kaliteyi iyi modellemez.
Yani gothic ve k2'deki farkın bir kısmı metrik kaynaklı olabilir. Ama k4'te kare hızı yalnız
48→32 düşüyor ve kayıp yine 20,8 puan — orada açıklama kare hızı değil, gereksiz çözünürlük
kırpması. Bu vaka metrik uyarısıyla savunulamaz.

**Bulgu (düzeltme bu sözleşmenin işi değil):** T4'ün rejim tabanlı ceza ağırlıkları
(`CompressionStrategy.PenaltyWeights`, `FloorsFor`) karmaşık içerikte doğru, kolay içerikte
fazla agresif. Kesme kararı karmaşıklık profiline bağlanmalı; hedef/kaynak oranı tek başına
yetmiyor. En net kanıt k4-ekrankaydi / 1 MB.

## Süre maliyeti

Toplam iş süresi (prob + plan/kalibrasyon + kodlama, klip başına prob bir kez sayıldı):
eski 1431,9 s, yeni 1255,8 s. Yeni motor **%12,3 daha hızlı**. Kapı %25 ek süreye izin
veriyordu; ek süre yok, tersine kazanç var.

Kalibrasyonun kendisi ücretsiz değil: plan süresi vaka başına 0,4-19,4 s ve bu tamamen
`CalibrationProbe`'un iki turlu örneklemesinden geliyor (kalibrasyonsuz plan 0,1 s'nin
altında). Ama kalibrasyon kodlamayı isabetli yaptığı için `EncodeRunner`'ın düzeltme
turlarını eledi ve net etki eksi çıktı.

Kalibrasyon 70 vakanın 60'ında tuttu (motor başına 30/35). Tutmayan beş vaka her motorda da
passthrough vakaları — orada kodlama olmadığı için kalibrasyon zaten çalıştırılmıyor.

**Uyarı:** iki motor aynı makinede **aynı anda** koştu, yani mutlak saniyeler tek başına
koşan bir sürümden yüksek. Kapı bir oran olduğu ve yük iki motora eşit bindiği için
karşılaştırma geçerli; mutlak saniyeler mutlak değer olarak kullanılmamalı.

## T3'ten devredilen soru: büyük hedeflerde ilk plan iki geçiş mi olsun

T3, tamsayı CRF adımının ~%12 sıçradığını, ≥50 MB doluluk bandının ise %2,8 geniş olduğunu
ve tek geçişli CRF yolunun bu bandı tutturamayacağını söylemişti.

Ölçüm bu soruyu konusuz bırakıyor: **70 vakanın hiçbirinde tek geçişli CRF seçilmedi.**
Passthrough dışındaki 60 vakanın 60'ı `2pass`. `FillTarget` kipinde `PlanCalculator`
`gridIsCoarserThanBand` kontrolüyle bandın CRF adımından dar olduğunu görüp doğrudan iki
geçişe geçiyor (`ReasonCode.FillTwoPassBandTooNarrowForCrf`), yani T3'ün önerdiği davranış
zaten yürürlükte. `tests/VidShrink.Tests/SpeedModeTests.cs` altın anlık görüntüsünü
değiştirmeye gerek yok.

Bu sonuç `FillTarget` kipi için geçerli. `QualityCeiling` kipi ayrı ölçüldü, aşağıda.

## QualityCeiling kipi

Ana koşu `FillTarget` kipinde. Sözleşme bench'in her iki kipi de koşturabilmesini istiyordu;
`--fill qualityceiling` eklendi ve yeni motorda gothic üzerinde ayrıca ölçüldü.

| hedef MB | gerçek MB | doluluk % | plan | mod | bant | sert taban | VMAF-NEG harm | XPSNR |
|---|---|---|---|---|---|---|---|---|
| 180 | 126,70 | 70,4 | 1920x1080@48 | crf 20 | dışında | altında | 94,36 | 40,23 |
| 100 | 94,22 | 94,2 | 1766x994@48 | crf 20 | dışında | altında | 84,83 | 37,92 |
| 25 | 20,79 | 83,2 | 844x474@48 | crf 20 | dışında | altında | 53,94 | 32,25 |
| 16 | 12,76 | 79,7 | 652x366@48 | crf 20 | dışında | altında | 39,24 | 30,56 |
| 8 | 7,82 | 97,7 | 614x346@48 | 1090k 2pass | içinde | uygun | 27,72 | 29,36 |
| 3 | 2,98 | 99,5 | 614x346@48 | 402k 2pass | içinde | uygun | 11,59 | 26,79 |
| 1 | 0,98 | 98,4 | 422x238@12 | 128k 2pass | içinde | uygun | 1,05 | 22,75 |

Dört büyük hedefte tek geçişli CRF seçiliyor ve dosya hedefin belirgin altında kalıyor.
**Bu ihlal değil, kipin tanımı**: `QualityCeiling` bütçeyi doldurmayı değil, şeffaflık
tavanında durmayı amaçlar. Bant ve sert taban ölçütleri bu kipte uygulanmaz; tablo yine de
sayıları veriyor ki fark görünsün. 180 MB hedefinde 126,7 MB'a durup VMAF 94,36 alması
kipin çalıştığının kanıtı: 53 MB daha küçük dosya, `FillTarget`'a göre 2,35 puan VMAF farkı
(94,36'ya karşı 96,71).

Bütçe tavanın altına düştüğünde (8 MB ve aşağısı) kip kendiliğinden iki geçişe dönüyor ve
`FillTarget` ile aynı sonucu veriyor.

Bu koşu tek klip (gothic) ve tek motor (yeni) üzerinde yapıldı. Eski motorda `QualityCeiling`
**ölçülmedi**; kipler arası A/B sözleşmenin kabul kriterlerinde yoktu, süre bütçesi de
yetmezdi.

## Ölçülmeyenler

Açıkça yazılıyor ki rapor eksiğini gizlemesin.

- **Passthrough vakalarında XPSNR.** Çıktı kaynakla birebir aynı, `xpsnr` sonsuz basıyor,
  sayı okunamıyor. Beş vaka: k1/180, k2/180, k4/180, k4/100, k4/25.
- **Eski motorda `QualityCeiling`.** Yalnız yeni motorda, yalnız gothic'te ölçüldü.
- **Donanım kodlayıcıları.** Bütün koşu `libx264` ile yapıldı. `CodecModel.HardwareFloorFactor`
  ve `HardwareBitrateYield` yolları sınanmadı.
- **HDR ve tonemap yolu.** Klip kümesinde HDR kaynak yok.
- **Gözle bakma.** Çıktılar masaüstüne yazıldı ama görsel değerlendirme yapılmadı; rapordaki
  bütün yargılar ölçüm sayılarından.
- **Kalibrasyonun tek başına süre payı.** Plan süresi kalibrasyonu içeriyor ama aynı vaka
  kalibrasyonlu ve kalibrasyonsuz iki kez koşulmadı, yani "kalibrasyon şu kadar saniye
  ekledi, şu kadar düzeltme turu eledi" ayrıştırması yapılamadı. Net etki ölçüldü (%12,3
  kazanç), bileşenleri ölçülmedi.

## Çıktı dosyaları

Kullanıcının gözle bakabilmesi için bütün çıktılar masaüstünde:

- `C:\Users\Administrator\Desktop\VidShrink-T5\eski\` — eski motor, 35 dosya
- `C:\Users\Administrator\Desktop\VidShrink-T5\yeni\` — yeni motor, 35 dosya
- `C:\Users\Administrator\Desktop\VidShrink-T5\yeni-tavan\` — QualityCeiling, 7 dosya

Dosya adları `<klip>_<hedef>mb.mp4` kalıbında. Şikâyet edilen vaka için doğrudan
karşılaştırma: `gothic2026-08-15 14-01-29_1mb.mp4` — eski klasörde 1190x670@48,
yeni klasörde 422x238@12.

Ham sayılar: `bench/results/baseline.json` ve `bench/results/after.json`.
Karşılaştırma komutu:

```
dotnet run --project tools/VidShrink.Bench -- compare bench/results/baseline.json bench/results/after.json
```

## Ham tablolar

### Bant tablosu

| klip | hedef MB | motor | gercek MB | doluluk % | bant alt MB | sert taban MB | bant ici | taban ihlali | hedef asimi |
|---|---|---|---|---|---|---|---|---|---|
| gothic | 180 | eski | 178.35 | 99.1 | 174.96 | 169.92 | evet | hayir | hayir |
| gothic | 180 | yeni | 178.34 | 99.1 | 174.96 | 169.92 | evet | hayir | hayir |
| gothic | 100 | eski | 99.16 | 99.2 | 97.20 | 94.40 | evet | hayir | hayir |
| gothic | 100 | yeni | 99.16 | 99.2 | 97.20 | 94.40 | evet | hayir | hayir |
| gothic | 25 | eski | 24.63 | 98.5 | 23.75 | 22.50 | evet | hayir | hayir |
| gothic | 25 | yeni | 24.59 | 98.4 | 23.75 | 22.50 | evet | hayir | hayir |
| gothic | 16 | eski | 15.81 | 98.8 | 15.20 | 14.40 | evet | hayir | hayir |
| gothic | 16 | yeni | 15.77 | 98.6 | 15.20 | 14.40 | evet | hayir | hayir |
| gothic | 8 | eski | 7.85 | 98.1 | 7.36 | 6.80 | evet | hayir | hayir |
| gothic | 8 | yeni | 7.82 | 97.7 | 7.36 | 6.80 | evet | hayir | hayir |
| gothic | 3 | eski | 2.98 | 99.4 | 2.76 | 2.55 | evet | hayir | hayir |
| gothic | 3 | yeni | 2.98 | 99.5 | 2.76 | 2.55 | evet | hayir | hayir |
| gothic | 1 | eski | 0.97 | 97.4 | 0.92 | 0.85 | evet | hayir | hayir |
| gothic | 1 | yeni | 0.98 | 98.4 | 0.92 | 0.85 | evet | hayir | hayir |
| k1-gradyan | 180 | eski | 176.10 | 97.8 | 174.96 | 169.92 | evet | hayir | hayir |
| k1-gradyan | 180 | yeni | 176.10 | 97.8 | 174.96 | 169.92 | evet | hayir | hayir |
| k1-gradyan | 100 | eski | 97.97 | 98.0 | 97.20 | 94.40 | evet | hayir | hayir |
| k1-gradyan | 100 | yeni | 97.90 | 97.9 | 97.20 | 94.40 | evet | hayir | hayir |
| k1-gradyan | 25 | eski | 24.26 | 97.1 | 23.75 | 22.50 | evet | hayir | hayir |
| k1-gradyan | 25 | yeni | 24.27 | 97.1 | 23.75 | 22.50 | evet | hayir | hayir |
| k1-gradyan | 16 | eski | 15.44 | 96.5 | 15.20 | 14.40 | evet | hayir | hayir |
| k1-gradyan | 16 | yeni | 15.44 | 96.5 | 15.20 | 14.40 | evet | hayir | hayir |
| k1-gradyan | 8 | eski | 7.71 | 96.4 | 7.36 | 6.80 | evet | hayir | hayir |
| k1-gradyan | 8 | yeni | 7.71 | 96.4 | 7.36 | 6.80 | evet | hayir | hayir |
| k1-gradyan | 3 | eski | 2.90 | 96.6 | 2.76 | 2.55 | evet | hayir | hayir |
| k1-gradyan | 3 | yeni | 2.91 | 96.9 | 2.76 | 2.55 | evet | hayir | hayir |
| k1-gradyan | 1 | eski | 0.96 | 95.6 | 0.92 | 0.85 | evet | hayir | hayir |
| k1-gradyan | 1 | yeni | 0.97 | 97.0 | 0.92 | 0.85 | evet | hayir | hayir |
| k2-testdeseni | 180 | eski | 129.36 | 71.9 | 174.96 | 169.92 | hayir | evet | hayir |
| k2-testdeseni | 180 | yeni | 129.36 | 71.9 | 174.96 | 169.92 | hayir | evet | hayir |
| k2-testdeseni | 100 | eski | 97.57 | 97.6 | 97.20 | 94.40 | evet | hayir | hayir |
| k2-testdeseni | 100 | yeni | 97.52 | 97.5 | 97.20 | 94.40 | evet | hayir | hayir |
| k2-testdeseni | 25 | eski | 24.26 | 97.0 | 23.75 | 22.50 | evet | hayir | hayir |
| k2-testdeseni | 25 | yeni | 24.24 | 97.0 | 23.75 | 22.50 | evet | hayir | hayir |
| k2-testdeseni | 16 | eski | 15.50 | 96.9 | 15.20 | 14.40 | evet | hayir | hayir |
| k2-testdeseni | 16 | yeni | 15.51 | 97.0 | 15.20 | 14.40 | evet | hayir | hayir |
| k2-testdeseni | 8 | eski | 7.68 | 96.0 | 7.36 | 6.80 | evet | hayir | hayir |
| k2-testdeseni | 8 | yeni | 7.67 | 95.9 | 7.36 | 6.80 | evet | hayir | hayir |
| k2-testdeseni | 3 | eski | 2.89 | 96.5 | 2.76 | 2.55 | evet | hayir | hayir |
| k2-testdeseni | 3 | yeni | 2.88 | 96.0 | 2.76 | 2.55 | evet | hayir | hayir |
| k2-testdeseni | 1 | eski | 0.95 | 94.7 | 0.92 | 0.85 | evet | hayir | hayir |
| k2-testdeseni | 1 | yeni | 0.96 | 96.1 | 0.92 | 0.85 | evet | hayir | hayir |
| k3-gurultu | 180 | eski | 176.46 | 98.0 | 174.96 | 169.92 | evet | hayir | hayir |
| k3-gurultu | 180 | yeni | 175.74 | 97.6 | 174.96 | 169.92 | evet | hayir | hayir |
| k3-gurultu | 100 | eski | 98.01 | 98.0 | 97.20 | 94.40 | evet | hayir | hayir |
| k3-gurultu | 100 | yeni | 98.07 | 98.1 | 97.20 | 94.40 | evet | hayir | hayir |
| k3-gurultu | 25 | eski | 24.25 | 97.0 | 23.75 | 22.50 | evet | hayir | hayir |
| k3-gurultu | 25 | yeni | 24.43 | 97.7 | 23.75 | 22.50 | evet | hayir | hayir |
| k3-gurultu | 16 | eski | 15.53 | 97.1 | 15.20 | 14.40 | evet | hayir | hayir |
| k3-gurultu | 16 | yeni | 15.59 | 97.4 | 15.20 | 14.40 | evet | hayir | hayir |
| k3-gurultu | 8 | eski | 7.64 | 95.6 | 7.36 | 6.80 | evet | hayir | hayir |
| k3-gurultu | 8 | yeni | 7.71 | 96.4 | 7.36 | 6.80 | evet | hayir | hayir |
| k3-gurultu | 3 | eski | 2.90 | 96.5 | 2.76 | 2.55 | evet | hayir | hayir |
| k3-gurultu | 3 | yeni | 2.89 | 96.3 | 2.76 | 2.55 | evet | hayir | hayir |
| k3-gurultu | 1 | eski | 0.92 | 92.1 | 0.92 | 0.85 | evet | hayir | hayir |
| k3-gurultu | 1 | yeni | 0.96 | 95.9 | 0.92 | 0.85 | evet | hayir | hayir |
| k4-ekrankaydi | 180 | eski | 24.01 | 13.3 | 174.96 | 169.92 | hayir | evet | hayir |
| k4-ekrankaydi | 180 | yeni | 24.01 | 13.3 | 174.96 | 169.92 | hayir | evet | hayir |
| k4-ekrankaydi | 100 | eski | 24.01 | 24.0 | 97.20 | 94.40 | hayir | evet | hayir |
| k4-ekrankaydi | 100 | yeni | 24.01 | 24.0 | 97.20 | 94.40 | hayir | evet | hayir |
| k4-ekrankaydi | 25 | eski | 24.01 | 96.1 | 23.75 | 22.50 | evet | hayir | hayir |
| k4-ekrankaydi | 25 | yeni | 24.01 | 96.1 | 23.75 | 22.50 | evet | hayir | hayir |
| k4-ekrankaydi | 16 | eski | 15.64 | 97.8 | 15.20 | 14.40 | evet | hayir | hayir |
| k4-ekrankaydi | 16 | yeni | 15.65 | 97.8 | 15.20 | 14.40 | evet | hayir | hayir |
| k4-ekrankaydi | 8 | eski | 7.91 | 98.9 | 7.36 | 6.80 | evet | hayir | hayir |
| k4-ekrankaydi | 8 | yeni | 7.92 | 99.0 | 7.36 | 6.80 | evet | hayir | hayir |
| k4-ekrankaydi | 3 | eski | 2.93 | 97.7 | 2.76 | 2.55 | evet | hayir | hayir |
| k4-ekrankaydi | 3 | yeni | 2.93 | 97.5 | 2.76 | 2.55 | evet | hayir | hayir |
| k4-ekrankaydi | 1 | eski | 0.97 | 96.7 | 0.92 | 0.85 | evet | hayir | hayir |
| k4-ekrankaydi | 1 | yeni | 1.00 | 99.6 | 0.92 | 0.85 | evet | hayir | hayir |

### Kalite tablosu

| klip | hedef MB | motor | cozunurluk | fps | codec/mod | crf/bitrate | VMAF-NEG harm | VMAF-NEG p10 | XPSNR | y | u | v |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| gothic | 180 | eski | 1920x1080 | 48 | libx264/2pass | 28010k | 96.70 | 93.81 | 41.54 | 39.60 | 45.82 | 45.05 |
| gothic | 180 | yeni | 1920x1080 | 48 | libx264/2pass | 28010k | 96.71 | 93.82 | 41.54 | 39.60 | 45.82 | 45.05 |
| gothic | 100 | eski | 1766x994 | 48 | libx264/2pass | 15504k | 85.24 | 81.85 | 38.06 | 35.56 | 43.63 | 42.51 |
| gothic | 100 | yeni | 1766x994 | 48 | libx264/2pass | 15504k | 85.25 | 81.87 | 38.06 | 35.56 | 43.62 | 42.51 |
| gothic | 25 | eski | 1190x670 | 48 | libx264/2pass | 3737k | 62.54 | 57.20 | 33.48 | 30.50 | 40.22 | 38.64 |
| gothic | 25 | yeni | 806x454 | 48 | libx264/2pass | 3737k | 55.23 | 49.86 | 32.36 | 29.02 | 39.85 | 38.22 |
| gothic | 16 | eski | 1190x670 | 48 | libx264/2pass | 2345k | 52.26 | 45.95 | 32.08 | 29.06 | 38.94 | 37.27 |
| gothic | 16 | yeni | 652x366 | 48 | libx264/2pass | 2345k | 42.88 | 37.15 | 30.94 | 27.51 | 38.69 | 36.89 |
| gothic | 8 | eski | 1190x670 | 48 | libx264/2pass | 1090k | 33.36 | 27.59 | 29.20 | 26.08 | 36.35 | 34.53 |
| gothic | 8 | yeni | 614x346 | 48 | libx264/2pass | 1090k | 27.75 | 22.24 | 29.36 | 26.00 | 37.04 | 35.11 |
| gothic | 3 | eski | 1190x670 | 48 | libx264/2pass | 402k | 12.11 | 7.71 | 24.72 | 21.44 | 32.25 | 30.30 |
| gothic | 3 | yeni | 614x346 | 48 | libx264/2pass | 402k | 11.61 | 7.70 | 26.79 | 23.41 | 34.58 | 32.52 |
| gothic | 1 | eski | 1190x670 | 48 | libx264/2pass | 128k | 1.27 | 0.00 | 19.56 | 16.09 | 27.90 | 25.14 |
| gothic | 1 | yeni | 422x238 | 12 | libx264/2pass | 128k | 1.05 | 0.00 | 22.77 | 18.77 | 32.67 | 28.86 |
| k1-gradyan | 180 | eski | 1920x1080 | 48 | h264/passthrough | 29544k | 98.83 | 98.22 | - | - | - | - |
| k1-gradyan | 180 | yeni | 1920x1080 | 48 | h264/passthrough | 29544k | 98.83 | 98.22 | - | - | - | - |
| k1-gradyan | 100 | eski | 1920x1080 | 48 | libx264/2pass | 16460k | 97.11 | 96.56 | 51.49 | 52.08 | 50.56 | 50.05 |
| k1-gradyan | 100 | yeni | 1920x1080 | 48 | libx264/2pass | 16460k | 97.11 | 96.55 | 51.48 | 52.07 | 50.56 | 50.04 |
| k1-gradyan | 25 | eski | 1920x1080 | 48 | libx264/2pass | 4069k | 96.24 | 95.77 | 47.64 | 48.66 | 46.15 | 45.04 |
| k1-gradyan | 25 | yeni | 1920x1080 | 48 | libx264/2pass | 4069k | 96.23 | 95.78 | 47.64 | 48.65 | 46.16 | 45.05 |
| k1-gradyan | 16 | eski | 1920x1080 | 48 | libx264/2pass | 2604k | 95.86 | 95.44 | 46.58 | 47.56 | 45.02 | 44.25 |
| k1-gradyan | 16 | yeni | 1920x1080 | 48 | libx264/2pass | 2604k | 95.86 | 95.44 | 46.58 | 47.55 | 45.02 | 44.24 |
| k1-gradyan | 8 | eski | 1690x950 | 48 | libx264/2pass | 1282k | 94.79 | 94.40 | 44.58 | 45.12 | 43.71 | 43.29 |
| k1-gradyan | 8 | yeni | 1690x950 | 48 | libx264/2pass | 1282k | 94.79 | 94.40 | 44.58 | 45.12 | 43.70 | 43.29 |
| k1-gradyan | 3 | eski | 1266x712 | 48 | libx264/2pass | 481k | 93.74 | 93.22 | 43.05 | 43.58 | 42.24 | 41.73 |
| k1-gradyan | 3 | yeni | 960x540 | 48 | libx264/2pass | 481k | 94.13 | 93.73 | 43.60 | 43.95 | 43.10 | 42.73 |
| k1-gradyan | 1 | eski | 1266x712 | 48 | libx264/2pass | 160k | 90.87 | 89.99 | 33.71 | 33.73 | 34.24 | 33.09 |
| k1-gradyan | 1 | yeni | 576x324 | 32 | libx264/2pass | 160k | 91.99 | 91.51 | 29.39 | 28.44 | 33.63 | 28.94 |
| k2-testdeseni | 180 | eski | 1920x1080 | 48 | h264/passthrough | 21703k | 99.37 | 98.99 | - | - | - | - |
| k2-testdeseni | 180 | yeni | 1920x1080 | 48 | h264/passthrough | 21703k | 99.37 | 98.99 | - | - | - | - |
| k2-testdeseni | 100 | eski | 1920x1080 | 48 | libx264/2pass | 16460k | 97.27 | 96.66 | 51.25 | 51.26 | 50.85 | 51.63 |
| k2-testdeseni | 100 | yeni | 1920x1080 | 48 | libx264/2pass | 16460k | 97.27 | 96.66 | 51.24 | 51.24 | 50.84 | 51.62 |
| k2-testdeseni | 25 | eski | 1382x778 | 48 | libx264/2pass | 4069k | 81.20 | 80.04 | 29.96 | 31.99 | 27.02 | 24.75 |
| k2-testdeseni | 25 | yeni | 1382x778 | 48 | libx264/2pass | 4069k | 81.17 | 80.01 | 29.96 | 31.99 | 27.02 | 24.75 |
| k2-testdeseni | 16 | eski | 1190x670 | 48 | libx264/2pass | 2604k | 77.19 | 75.90 | 28.25 | 30.25 | 25.49 | 23.02 |
| k2-testdeseni | 16 | yeni | 1190x670 | 48 | libx264/2pass | 2604k | 77.15 | 75.83 | 28.25 | 30.24 | 25.49 | 23.02 |
| k2-testdeseni | 8 | eski | 960x540 | 48 | libx264/2pass | 1282k | 72.26 | 70.84 | 26.94 | 28.78 | 24.85 | 21.68 |
| k2-testdeseni | 8 | yeni | 922x518 | 48 | libx264/2pass | 1282k | 70.91 | 69.51 | 25.98 | 27.97 | 23.15 | 20.81 |
| k2-testdeseni | 3 | eski | 960x540 | 48 | libx264/2pass | 481k | 67.59 | 65.82 | 26.12 | 27.82 | 24.41 | 21.02 |
| k2-testdeseni | 3 | yeni | 882x496 | 12 | libx264/2pass | 481k | 15.44 | 9.58 | 16.56 | 18.15 | 14.07 | 12.71 |
| k2-testdeseni | 1 | eski | 960x540 | 48 | libx264/2pass | 160k | 13.42 | 9.61 | 19.14 | 19.79 | 19.09 | 16.61 |
| k2-testdeseni | 1 | yeni | 576x324 | 10 | libx264/2pass | 160k | 7.85 | 3.97 | 15.88 | 17.43 | 13.51 | 12.08 |
| k3-gurultu | 180 | eski | 960x540 | 48 | libx264/2pass | 29627k | 10.26 | 8.74 | 19.39 | 19.37 | 19.42 | 19.44 |
| k3-gurultu | 180 | yeni | 922x518 | 48 | libx264/2pass | 29627k | 11.45 | 11.13 | 19.39 | 19.37 | 19.42 | 19.44 |
| k3-gurultu | 100 | eski | 960x540 | 48 | libx264/2pass | 16460k | 7.36 | 6.90 | 18.37 | 18.34 | 18.41 | 18.42 |
| k3-gurultu | 100 | yeni | 882x496 | 25 | libx264/2pass | 16460k | 9.87 | 9.77 | 18.55 | 18.57 | 18.50 | 18.50 |
| k3-gurultu | 25 | eski | 960x540 | 48 | libx264/2pass | 4069k | 5.32 | 5.08 | 12.98 | 12.98 | 12.99 | 13.00 |
| k3-gurultu | 25 | yeni | 730x410 | 6 | libx264/2pass | 4069k | 9.03 | 8.93 | 17.54 | 17.57 | 17.46 | 17.47 |
| k3-gurultu | 16 | eski | 960x540 | 48 | libx264/2pass | 2604k | 5.34 | 5.01 | 12.14 | 12.15 | 12.11 | 12.12 |
| k3-gurultu | 16 | yeni | 576x324 | 6 | libx264/2pass | 2604k | 7.72 | 7.67 | 16.17 | 16.21 | 16.09 | 16.10 |
| k3-gurultu | 8 | eski | 960x540 | 48 | libx264/2pass | 1282k | 5.41 | 5.02 | 10.39 | 10.41 | 10.34 | 10.35 |
| k3-gurultu | 8 | yeni | 498x280 | 6 | libx264/2pass | 1282k | 6.63 | 6.80 | 15.33 | 15.36 | 15.26 | 15.26 |
| k3-gurultu | 3 | eski | 960x540 | 48 | libx264/2pass | 481k | 4.68 | 4.38 | 8.36 | 8.40 | 8.28 | 8.29 |
| k3-gurultu | 3 | yeni | 498x280 | 6 | libx264/2pass | 481k | 6.02 | 5.99 | 14.25 | 14.29 | 14.19 | 14.19 |
| k3-gurultu | 1 | eski | 960x540 | 48 | libx264/2pass | 160k | 4.32 | 3.40 | 8.41 | 8.46 | 8.33 | 8.34 |
| k3-gurultu | 1 | yeni | 498x280 | 6 | libx264/2pass | 160k | 5.26 | 5.10 | 12.26 | 12.30 | 12.19 | 12.19 |
| k4-ekrankaydi | 180 | eski | 1920x1080 | 48 | h264/passthrough | 4029k | 97.54 | 97.51 | - | - | - | - |
| k4-ekrankaydi | 180 | yeni | 1920x1080 | 48 | h264/passthrough | 4029k | 97.54 | 97.51 | - | - | - | - |
| k4-ekrankaydi | 100 | eski | 1920x1080 | 48 | h264/passthrough | 4029k | 97.54 | 97.51 | - | - | - | - |
| k4-ekrankaydi | 100 | yeni | 1920x1080 | 48 | h264/passthrough | 4029k | 97.54 | 97.51 | - | - | - | - |
| k4-ekrankaydi | 25 | eski | 1920x1080 | 48 | h264/passthrough | 4029k | 97.54 | 97.51 | - | - | - | - |
| k4-ekrankaydi | 25 | yeni | 1920x1080 | 48 | h264/passthrough | 4029k | 97.54 | 97.51 | - | - | - | - |
| k4-ekrankaydi | 16 | eski | 1920x1080 | 48 | libx264/2pass | 2604k | 97.38 | 97.30 | 72.35 | 73.19 | 70.64 | 70.69 |
| k4-ekrankaydi | 16 | yeni | 1920x1080 | 48 | libx264/2pass | 2604k | 97.38 | 97.30 | 72.35 | 73.19 | 70.66 | 70.71 |
| k4-ekrankaydi | 8 | eski | 1920x1080 | 48 | libx264/2pass | 1282k | 96.75 | 96.54 | 60.05 | 60.85 | 58.78 | 58.14 |
| k4-ekrankaydi | 8 | yeni | 1920x1080 | 48 | libx264/2pass | 1282k | 96.75 | 96.54 | 60.06 | 60.85 | 58.75 | 58.21 |
| k4-ekrankaydi | 3 | eski | 1266x712 | 48 | libx264/2pass | 481k | 81.16 | 80.58 | 33.75 | 34.66 | 33.71 | 30.16 |
| k4-ekrankaydi | 3 | yeni | 1266x712 | 48 | libx264/2pass | 481k | 81.15 | 80.55 | 33.74 | 34.65 | 33.71 | 30.16 |
| k4-ekrankaydi | 1 | eski | 1190x670 | 48 | libx264/2pass | 160k | 74.57 | 72.21 | 32.85 | 33.98 | 32.43 | 28.75 |
| k4-ekrankaydi | 1 | yeni | 806x454 | 32 | libx264/2pass | 160k | 53.80 | 48.80 | 28.47 | 29.67 | 27.43 | 24.68 |

### Süre tablosu (saniye)

| klip | hedef MB | eski kodlama | yeni kodlama | fark % | eski plan | yeni plan | eski prob | yeni prob |
|---|---|---|---|---|---|---|---|---|
| gothic | 180 | 119.9 | 99.9 | -16.7 | 19.35 | 17.11 | 19.8 | 27.4 |
| gothic | 100 | 78.6 | 70.7 | -10.1 | 11.24 | 14.03 | 19.8 | 27.4 |
| gothic | 25 | 30.5 | 43.6 | +42.9 | 5.69 | 11.33 | 19.8 | 27.4 |
| gothic | 16 | 43.8 | 18.3 | -58.3 | 9.27 | 16.18 | 19.8 | 27.4 |
| gothic | 8 | 18.8 | 13.0 | -31.2 | 4.73 | 5.62 | 19.8 | 27.4 |
| gothic | 3 | 15.2 | 11.9 | -22.0 | 4.79 | 4.67 | 19.8 | 27.4 |
| gothic | 1 | 14.3 | 12.1 | -14.9 | 4.18 | 3.69 | 19.8 | 27.4 |
| k1-gradyan | 180 | 0.1 | 0.1 | +73.4 | 1.15 | 0.39 | 7.4 | 5.9 |
| k1-gradyan | 100 | 61.7 | 54.5 | -11.7 | 6.36 | 5.90 | 7.4 | 5.9 |
| k1-gradyan | 25 | 46.2 | 45.7 | -0.9 | 4.96 | 9.17 | 7.4 | 5.9 |
| k1-gradyan | 16 | 45.4 | 41.1 | -9.6 | 3.99 | 5.29 | 7.4 | 5.9 |
| k1-gradyan | 8 | 33.2 | 37.3 | +12.3 | 6.32 | 8.48 | 7.4 | 5.9 |
| k1-gradyan | 3 | 17.3 | 14.5 | -15.8 | 1.99 | 3.70 | 7.4 | 5.9 |
| k1-gradyan | 1 | 16.3 | 6.6 | -59.7 | 2.18 | 2.18 | 7.4 | 5.9 |
| k2-testdeseni | 180 | 0.0 | 0.0 | +39.0 | 0.75 | 0.69 | 6.0 | 6.5 |
| k2-testdeseni | 100 | 24.7 | 25.1 | +1.4 | 3.95 | 4.00 | 6.0 | 6.5 |
| k2-testdeseni | 25 | 13.9 | 14.1 | +1.7 | 2.21 | 2.28 | 6.0 | 6.5 |
| k2-testdeseni | 16 | 10.6 | 11.2 | +5.7 | 3.93 | 4.09 | 6.0 | 6.5 |
| k2-testdeseni | 8 | 43.1 | 7.7 | -82.2 | 12.31 | 3.63 | 6.0 | 6.5 |
| k2-testdeseni | 3 | 8.5 | 4.6 | -46.0 | 1.79 | 2.26 | 6.0 | 6.5 |
| k2-testdeseni | 1 | 19.8 | 5.3 | -73.2 | 1.67 | 2.11 | 6.0 | 6.5 |
| k3-gurultu | 180 | 112.9 | 67.6 | -40.1 | 20.34 | 37.17 | 76.2 | 61.7 |
| k3-gurultu | 100 | 96.6 | 40.6 | -57.9 | 19.63 | 30.75 | 76.2 | 61.7 |
| k3-gurultu | 25 | 37.7 | 41.0 | +8.9 | 9.13 | 52.30 | 76.2 | 61.7 |
| k3-gurultu | 16 | 35.2 | 26.2 | -25.4 | 9.05 | 24.18 | 76.2 | 61.7 |
| k3-gurultu | 8 | 30.0 | 22.3 | -25.7 | 9.17 | 9.54 | 76.2 | 61.7 |
| k3-gurultu | 3 | 24.1 | 32.1 | +33.3 | 9.12 | 10.66 | 76.2 | 61.7 |
| k3-gurultu | 1 | 64.8 | 22.6 | -65.1 | 10.23 | 10.97 | 76.2 | 61.7 |
| k4-ekrankaydi | 180 | 0.0 | 0.0 | +81.4 | 0.39 | 0.45 | 3.2 | 3.7 |
| k4-ekrankaydi | 100 | 0.0 | 0.0 | -38.8 | 0.41 | 0.35 | 3.2 | 3.7 |
| k4-ekrankaydi | 25 | 0.0 | 0.0 | -10.2 | 0.41 | 0.34 | 3.2 | 3.7 |
| k4-ekrankaydi | 16 | 20.3 | 20.5 | +1.2 | 1.39 | 1.65 | 3.2 | 3.7 |
| k4-ekrankaydi | 8 | 10.0 | 10.1 | +1.0 | 2.88 | 3.33 | 3.2 | 3.7 |
| k4-ekrankaydi | 3 | 6.8 | 9.1 | +33.6 | 1.04 | 1.32 | 3.2 | 3.7 |
| k4-ekrankaydi | 1 | 12.2 | 9.2 | -24.4 | 0.92 | 1.88 | 3.2 | 3.7 |
