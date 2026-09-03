# T159 — Kestirim doğruluğu ile plan kalitesi arasında bağ var mı

Ölçüm aracı: `tools/kestirim-plan/`. Test: `tests/VidShrink.Tests/KestirimPlanTests.cs`.
`src/` altına satır yazılmadı (bkz. K5 sonunda `git diff --stat -- src/` kanıtı).

## K1 — Enjeksiyon noktası ve x1,00 kimlik kanıtı

Enjeksiyon tek satırda: `tools/kestirim-plan/Program.cs:99`

```csharp
var enjekteProfil = m == 1.0 ? tabanProfil : tabanProfil with { ReferenceBppf = tabanProfil.ReferenceBppf * m };
```

`m == 1.0` için `tabanProfil` nesnesinin kendisi (aynı referans) kullanılıyor — kopya bile
alınmıyor. Ölçüm/kestirim yolu (`ComplexityProbe.RunDetailedAsync` → `WithProbeQuality` →
iki turluk `CalibrationProbe.RunAsync` kalibrasyon döngüsü, `CalibrationRounds = 2`) production'ın
`MainWindow.axaml.cs:1795-1900`teki `ProbeWithMeasuredQualityAsync`/`MeasureComplexityAsync` akışıyla
birebir aynı çağrı sırasını ve aynı `PlanOptions` alan değerlerini (`CurrentOptions()` varsayılanları:
Intent.Sharing, HdrPolicy.Preserve, FillPolicy.FillTarget, SpeedMode.Quality) kullanıyor.
`PlanCalculator.BuildDetailed` saf/deterministik olduğu için (bkz. K5, `SameProfileAlwaysProducesTheIdenticalPlan`)
x1,00 kolu production'ın üreteceği planla matematiksel olarak özdeştir — enjeksiyon hattı bu kolda
hiçbir değeri değiştirmiyor.

Ham kanıt (parça-1, gerçek kestirim `ReferenceBppf=0,049847`, hedef 20 MB):

```
--- parca-1 x1.00 (carpan=1) ---
  kodlandi: 19.33 MB, VMAF-NEG ort=68.811 p10=42.762 (ok)
```

`SonWidth=1882 SonHeight=1058 SonMode=2pass SonVideoBitrateK=2695` — enjeksiyonsuz koldur, üretim
planıyla parametre bazında (aynı Options, aynı profil, aynı çağrı sırası) örtüşür.

## K2 — Üç kaynak × yedi çarpan, gerçek kodlama + gerçek VMAF-NEG

Hedef boyut her üç kaynakta ve tüm kollarda sabit: **20 MB**. Kodlama gerçek (`EncodeRunner.RunAsync`),
kalite ölçümü gerçek (`QualityMeter`). HDR kaynaklar (üçü de `hdr=True`) `CodecPreference.Compatible`
→ `libx264` seçtiği ve libx264 HDR10 desteklemediği için `HdrResolver` sessizce SDR'ye tonemap ediyor;
bu yüzden karşılaştırma `QualityMeter.MeasureTonemappedReferenceAsync` ile yapıldı (bkz. "Yol açıklaması").

### parça-1 (ReferenceBppf taban = 0,049847)

| Çarpan | Enjekte Ref | Çözünürlük | Mod | VideoK | Çıkış MB | VMAF-NEG ort | p10 |
|---|---|---|---|---|---|---|---|
| x0.50 | 0.024924 | 1920x1080 | 2pass | 2695 | 19.32 | 74.511 | 45.411 |
| x0.70 | 0.034893 | 1920x1080 | 2pass | 2695 | 19.31 | 74.579 | 45.565 |
| x0.85 | 0.042370 | 1920x1080 | 2pass | 2695 | 19.32 | 74.608 | 45.483 |
| x1.00 | 0.049847 | 1882x1058 | 2pass | 2695 | 19.33 | 68.811 | 42.762 |
| x1.20 | 0.059817 | 1804x1014 | 2pass | 2695 | 19.28 | 69.054 | 44.620 |
| x1.50 | 0.074771 | 1690x950  | 2pass | 2695 | 19.26 | 68.751 | 46.576 |
| x2.00 | 0.099695 | 1574x886  | 2pass | 2695 | 19.19 | 68.414 | 48.876 |

### parça-2 (ReferenceBppf taban = 0,006353)

| Çarpan | Enjekte Ref | Çözünürlük | Mod | Crf | VideoK | Çıkış MB | VMAF-NEG ort | p10 |
|---|---|---|---|---|---|---|---|---|
| x0.50 | 0.003176 | 1920x1080 | 2pass | — | 2613 | 19.79 | 95.334 | 94.744 |
| x0.70 | 0.004447 | 1920x1080 | 2pass | — | 2613 | 19.82 | 95.348 | 94.752 |
| x0.85 | 0.005400 | 1920x1080 | crf | 21 | 1276 | 19.45 | 95.322 | 94.722 |
| x1.00 | 0.006353 | 1920x1080 | crf | 23 | 1081 | 19.45 | 95.323 | 94.754 |
| x1.20 | 0.007623 | 1920x1080 | crf | 24 | 887  | 19.46 | 95.326 | 94.717 |
| x1.50 | 0.009529 | 1920x1080 | crf | 25 | 973  | 19.46 | 95.329 | 94.728 |
| x2.00 | 0.012706 | 1920x1080 | crf | 24 | 1366 | 19.49 | 95.326 | 94.739 |

### parça-3 (ReferenceBppf taban = 0,102405)

| Çarpan | Enjekte Ref | Çözünürlük | Mod | VideoK | Çıkış MB | VMAF-NEG ort | p10 |
|---|---|---|---|---|---|---|---|
| x0.50 | 0.051202 | 1842x1036 | 2pass | 2565 | 19.59 | 40.374 | 35.099 |
| x0.70 | 0.071683 | 1690x950  | 2pass | 2565 | 19.55 | 42.771 | 37.557 |
| x0.85 | 0.087044 | 1612x906  | 2pass | 2565 | 19.52 | 44.578 | 39.322 |
| x1.00 | 0.102405 | 1536x864  | 2pass | 2565 | 19.51 | 46.291 | 41.153 |
| x1.20 | 0.122886 | 1458x820  | 2pass | 2565 | 19.49 | 45.731 | 40.636 |
| x1.50 | 0.153607 | 1382x778  | 2pass | 2565 | 19.48 | 47.296 | 42.309 |
| x2.00 | 0.204809 | 1306x734  | 2pass | 2565 | 19.45 | 48.601 | 43.653 |

Süreç sayaçları — sayaç dosyası koşum başına sıfırlanıyor, aracın kendi yazdığı ham sayılar:

- Tek hücrelik duman testi (parça-1 x1,00, sahne haritası + kalibrasyon + kodlama + ölçüm dahil):
  **70 ffmpeg, 17 ffprobe süreci**.
- parça-3 tek başına tam koşum (7 hücre, aynı kapsam × 7): **106 ffmpeg, 41 ffprobe süreci**.
- parça-1+parça-2'yi kapsayan ilk koşum ortam tarafından öldürüldüğü için (bkz. Borçlar) kendi
  sayaç özetini hiç yazdıramadı; o iki kaynağın süreç sayısı bu yüzden ayrı raporlanamıyor,
  yalnız parça-3'ün tek başına ölçümü güvenilir.

Tüm 21 hücre `Calibrated=true`, `KodlamaBasarili=true`, `KodlamaDeneme=1` — hiçbir hücre yeniden
denemeye düşmedi.

## K3 — Plan kalitesi, kestirim doğruluğuna göre monotonik mi

**Hayır, ters/karışık.** Üç kaynağın hiçbiri "doğru kestirime (x1,00) yaklaştıkça kalite düzelir,
uzaklaştıkça düzgünce bozulur" örüntüsünü göstermiyor:

- **parça-1 — ters yönlü:** x1,00 (gerçek kestirim, "doğru" nokta) VMAF-NEG=68,811 üretiyor; bu,
  x0,50/x0,70/x0,85'in (kasıtlı olarak yanlış, düşük kestirimler) hepsinden **düşük**
  (74,5-74,6 aralığı). Yani kestirim kasıtlı olarak yanlış (düşük) tutulduğunda plan daha
  kaliteli çıkıyor; doğru kestirim burada zararlı. x1,20-x2,00 arası ise kabaca düz (68,4-69,1),
  yön değiştirmiyor. Bu, T103'ün parça-1 anomalisini tekil bir olay değil, genel bir örüntü
  olarak doğruluyor.
- **parça-2 — düz:** 7 çarpan boyunca VMAF-NEG 95,32-95,35 arasında, gürültü seviyesinde sabit.
  Çözünürlük hiçbir kolda değişmiyor (hep 1920x1080); kestirim doğruluğunun kaliteye hiçbir
  ölçülebilir etkisi yok.
- **parça-3 — ters yönlü (aşırı tahmin lehine):** VMAF-NEG, çarpan büyüdükçe (kestirim
  "gerçekten" daha da uzaklaşırken) **artıyor**: 40,4 (x0,50) → 48,6 (x2,00), neredeyse
  monotonik artış. x1,00 (46,291) doğru nokta olmasına rağmen x1,50 (47,296) ve x2,00 (48,601)
  ondan daha kaliteli — kasıtlı aşırı tahmin burada da kestirimi "iyileştiriyor".

Üç kaynakta da doğru kestirim noktası (x1,00) en iyi kaliteyi vermiyor; iki kaynakta (parça-1,
parça-3) yanlış kestirim daha iyi VMAF-NEG üretiyor, birinde (parça-2) ölçülebilir hiçbir bağ yok.
Tablo yeniden okundu, cümle tabloyla çelişmiyor.

## K4 — Karar noktası: mekanizma nerede kırılıyor

Kaynak satırı: **`src/VidShrink.Core/ComplexityProfile.cs:296`**

```csharp
public double RequiredBppf(string codec, double scale, double fps, double sourceFps)
{
    var detail = ScaleFactor(scale);
    return ReferenceBppf * CodecModel.RelativeBitrateNeed(codec) * detail * TemporalFactor(fps, sourceFps);
}
```

Bu değer `src/VidShrink.Core/PlanCalculator.cs:772`de (`SearchLayout` içinde,
`var required = complexity.RequiredBppf(codec, effectiveScale, fps, info.Fps);`) her
`(fps, scale)` adayı için çağrılıyor; `Decompose`/`LayoutScore` (`PlanCalculator.cs:730-745`)
bu `required`i `provided` (verilen bit/piksel) ile karşılaştırıp bir skor üretiyor — enjekte
edilen `ReferenceBppf` büyüdükçe aynı çözünürlük için `required` büyüyor, skor düşüyor, arama
daha küçük `scale`'e kayıyor.

**T103'ün "çözünürlük merdiveni bir basamak düşüyor" gözlemi — kısmen doğrulandı, çerçevesi
düzeltildi.** Arama ayrık isimli çözünürlükler (1080p/720p gibi) arasında değil,
`ScaleCandidates` (`PlanCalculator.cs:89,795-805`) üzerinden **1,0'dan başlayıp `%2` adımlarla
(`ScaleStep = 0,02`) sürekli tarıyor**. parça-1'in x0,85→x1,00 geçişi bunun en temiz kanıtı:

```
x0.85  ref=0.042370  1920x1080  (scale = 1,00)
x1.00  ref=0.049847  1882x1058  (scale = 1058/1080 = 0,9796 ≈ 0,98 — taramanın İLK adımı)
```

Yani "bir basamak düşme" gerçek ama basamak, isimli bir çözünürlük değil, taramanın 2%'lik
adımlarından biri. parça-3'te ise (kestirim değeri zaten büyük, arama alanının derininde)
komşu çarpanlar arası geçişler tek adım değil, 2-6 adım birden atlıyor (`PlanCalculator.cs:89`daki
sabit adım aynı, ama başlangıç noktası daha derin). Ek bulgu: parça-2'de x0,70→x0,85 arası,
çözünürlük yerine **mod** karar noktası kırılıyor — `2pass`'ten `crf`'e geçiyor (`TaslakMode`/`SonMode`
alanları), bu da aynı `RequiredBppf` sinyalinin yalnız çözünürlüğü değil, kodlama modu seçimini
de etkilediğini gösteriyor.

## K5 — Karar noktasının mutasyon testiyle pimlenmesi

`tests/VidShrink.Tests/KestirimPlanTests.cs`, ffmpeg'siz, saf `PlanCalculator`/`ComplexityProfile`
çağrılarıyla üç test içeriyor. `--list-tests` ile doğrulanmış üç gerçek test (K6):

```
VidShrink.Tests.KestirimPlanTests.RequiredBppfIsDirectlyProportionalToReferenceBppf
VidShrink.Tests.KestirimPlanTests.HigherReferenceBppfNeverPicksALargerResolutionThanLower
VidShrink.Tests.KestirimPlanTests.SameProfileAlwaysProducesTheIdenticalPlan
```

Mutasyon: `ComplexityProfile.cs:296`deki `ReferenceBppf *` çarpanı kaldırıldı
(`return CodecModel.RelativeBitrateNeed(codec) * detail * TemporalFactor(...)`).
`dotnet build -c Release --no-incremental` (asla `--no-build`) ile yeniden derlenip
filtrelenmiş süit koşuldu:

```
Başarısız VidShrink.Tests.KestirimPlanTests.RequiredBppfIsDirectlyProportionalToReferenceBppf [< 1 ms]
  Hata İletisi:
   Assert.Equal() Failure: Values are not within 9 decimal places
Expected: 2 (rounded from 2)
Actual:   1 (rounded from 1)
Başarısız! - Başarısız: 1, Başarılı: 2, Atlanan: 0, Toplam: 3
```

**Dürüst not:** diğer iki test (`HigherReferenceBppfNeverPicksALargerResolutionThanLower`,
`SameProfileAlwaysProducesTheIdenticalPlan`) bu mutasyonda kırmızıya düşmedi. Sebebi,
`ComplexityProfile.FloorAdaptation` adlı ayrı bir özelliğin — `RequiredBppf`'ten bağımsız,
`ReferenceBppf`i doğrudan `FloorBppf` üzerinden kullanan ikinci bir yol — planı hâlâ
`ReferenceBppf`e duyarlı tutması. Yani :296 satırı tek başına "tüm `ReferenceBppf` etkisi"nin
değil, `RequiredBppf`'in kendisinin pimidir; birinci test bu satırı tam isabetle pinliyor,
diğer ikisi mekanizmanın genel yönünü (daha büyük referans → eşit ya da küçük çözünürlük,
determinizm) pinliyor.

Geri alma sonrası, temiz derleme ve yeniden koşum:

```
git diff --stat -- src/    → (bos cikti, degisiklik yok)
Basarili! - Basarisiz: 0, Basarili: 3, Atlanan: 0, Toplam: 3, Sure: 11 ms
```

## K6 — Test sayısı ve CI

`--list-tests` ile doğrulanan gerçek test sayısı: **3** (yukarıdaki üç isim). Vstest'in alt-dize
filtre boş-eşleşme tuzağına düşülmedi.

CI koşum kimliği: **bu bölüm dal `origin`'e itildikten sonra `gh run list` ile doldurulacak** —
henüz push edilmedi (rapor push öncesi yazılıyor).

## Yol açıklaması — HDR/SDR karşılaştırma tuzağı

İlk koşumda 21 hücrenin tamamı `VMAF-NEG=null, "Renk uzayı uyuşmuyor; HDR ve SDR/tonemap
edilmiş görüntü karşılaştırılamaz."` ile başarısız oldu. Sebep: üç kaynak da `IsHdr=True`;
`CodecPreference.Compatible` her zaman `libx264` seçiyor (`PlanCalculator.cs:892`); libx264
HDR10 desteklemediği için `HdrResolver.Resolve` (`HdrResolver.cs:32-36`) `HdrPolicy.Preserve`
istenmesine rağmen sessizce `TonemapToSdr`e düşüyor. `QualityMeter.MeasureAsync` ham HDR
referans ile SDR test dosyasını doğrudan karşılaştıramadığı için `Comparable=false` dönüyordu.
Düzeltme: `info.IsHdr` doğruysa `QualityMeter.MeasureTonemappedReferenceAsync(kaynakYolu,
sonuc.OutputPath, ct)` çağrılıyor (`tools/kestirim-plan/Program.cs:154-156`) — bu, üretim
kodunda zaten var olan, referansı da aynı bt709 uzayına tonemap edip normalize eden hazır yol.
Duman testinde doğrulandı: `VMAF-NEG ort=68,769 p10=42,454 (ok)`. Bu yolun tek sınırlaması:
`MeasureTonemappedReferenceAsync` sahne haritası (`SceneMap`) parametresi almıyor — bu üç
ölçümde `VmafNegWorstScene` alanı boş kaldı, ama `ort`/`p10` (K2'nin istediği alanlar) etkilenmedi.

## Borçlar

- CI koşum kimliği push sonrası eklenecek.
- `VmafNegWorstScene`/sahne-ağırlıklı en kötü pencere, HDR kaynaklarda
  `MeasureTonemappedReferenceAsync`in sahne haritası almaması nedeniyle ölçülemedi (SDR
  kaynak olsaydı ölçülebilirdi — bu üç kaynağın hepsi HDR olduğu için K2 kapsamı dışında kaldı,
  contract yalnız ort/p10 istiyor).
- İlk tam koşum (parça-1+parça-2 tamamlandı, parça-3 x1,20'de kesildi) ortam tarafından
  öldürüldü (kullanıcı/ajan eylemi değil); parça-3 tek başına yeniden koşularak tamamlandı —
  ffmpeg/ffprobe süreç sayaçları bu yüzden koşum başına verildi, tek bir toplam sayı yerine.
