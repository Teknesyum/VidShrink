# PlanCalculator.cs — kapsam incelemesi

Kapsam: `src/VidShrink.Core/PlanCalculator.cs` (530 satır). Referanslar: `ComplexityProfile.cs`,
`CodecModel.cs`, `CompressionStrategy.cs`, `src/VidShrink.Ffmpeg/EncodeRunner.cs`.

## 1. Ne yapıyor

1. Rejim + kodek seçimi (`:56-66`), HDR çözümü (`:69`), ses bütçesi (`:93`), video bütçesi (`:94`).
2. `SearchLayout` (`:343`) ölçek×fps ızgarasını tarar, kalite skorunu en yükselten yerleşimi seçer.
3. Bütçenin karşılığı CRF (`:133`) ile niyetin şeffaflık tavanı (`:134`) karşılaştırılır; bütçe
   tavandan cömertse tek geçişli CRF, değilse iki geçişli VBR (`:141` / `:200`).
4. CRF yolunda `FillTarget` açıksa `FillBand` (`:19`) merkezine inecek CRF hesaplanır (`:168-198`).
5. Kodlama sonrası `Correct` (`:278`) bitrate'i düzeltir; `MeasuredEncoderEfficiency` (`:257`)
   önceki denemeden kodlayıcı verimini ölçer, `RetryAimMb` (`:267`) hedefi belirler.

## 2. Doğruluk kusurları

**K1 — `RetryAimMb` ölçümsüz dalda bandın altına nişan alıyor (`:273-275`).** `ceilingAim = T/1,04 =
0,96154·T`, ≥50 MB'da band alt kenarı `0,972·T` (`:24`); `Math.Min` daima bandın **dışını** seçer —
T=180'de nişan 173,1 MB, band 174,96–180. Ölçümsüz deneme yapısal olarak bandı tutturamaz (<50 MB'da
sorun yok). Test kaçırıyor: `FillBandTests.cs:215-228` yalnız `> HardFloor` ve `<= target` iddia ediyor.

**K2 — CRF girişi bandın altını garanti ediyor (`:141` + `:171`).**
Giriş koşulu `ceilingSizeMb <= 0,94·T`, band alt kenarı `0,972·T`. `0,94 < 0,972` ⇒ `:171`'deki
`ceilingSizeMb < band.LowerMb` ≥50 MB'da **her zaman doğru**; doldurma dalı koşulsuz çalışır, `:171` ölü kontrol.

**K3 — Ölçülmemiş profille CRF doldurma yapılıyor (`:141`, `:180`).**
`Measured == false` iken tahmin bandı ±%32 (`ComplexityProfile.cs:48`) — %2,8'lik banda ±%32 belirsizlikle
nişan alınıyor. Girdi: prob çalışmamış herhangi bir kaynak + ≥50 MB hedef. Karar `EstimateBand`'i band
yarı genişliğiyle hiç karşılaştırmıyor.

**K4 — `Correct` taban bitrate'i 1 kbps (`:297`).** `Math.Max(1, ...)`, oysa `Build` tabanı 48 (`:42`, `:94`).
Girdi: çok aşan ilk deneme (500 MB kaynak → 1 MB hedef). Üretilen: 1–47 kbps video; banda girer, izlenemez.

**K5 — Dejenere kaynakta sıfıra bölme (`:132`, `:351`, `:369`).** `Width/Height == 0` (probe başarısız)
veya tek sayı yükseklik < 240 (ör. 426x239) olduğunda `:351-352` tüm adayları eler, `:369` yedek yerleşimi
döner. `:132`'deki `videoK*1000/(W*H*fps)` korumasız — `BitsPerPixel` (`:310`) `Math.Max(1.0, …)` kullanıyor,
burada yok. Sonuç `Infinity` → `budgetCrf = −∞` → CRF dalı + `Width=0` planı; ayrıca yedek yerleşim
`Score = 0` döner ve `:212` "predicted quality 0/100" yazar.

**K6 — Kurtarılan yerleşim bayat skor taşıyor (`:340`).** `best with { Score = fallback.Score }`;
`RecoverLayoutAtCeiling` çözünürlüğü yükseltir ama skor küçük yerleşiminki kalır. `:212`,
`PlanResult.PredictedQuality` ve `StrategyAdvice.PredictedQuality` yanlış olur.

**K7 — Donanım yanlılığı tahmini de şişiriyor (`:191-192`, `:208` + `:245`).** `HardwareUncalibratedBias
= 1,06` bitrate isteğini %6 yükseltir; `Estimate` (`:245`) beklenen boyutu **yükseltilmiş** bitrate'ten
hesaplar. "Encoder bitrate'in altına düşüyor" düzeltmesi arayüzdeki tahmini aynı yönde %6 yanlışlar.

**K9 — Ses tabanı bütçe payını deliyor (`:439-449`).** `cap` uygulanır (`:441`), sonra `:449` koşulsuz
`Math.Max(24, audioK)` yapar; `:443` düşürmesi yalnız `totalK < 96` iken çalışır. `totalK ∈ [96,133]` +
Extreme rejim (pay 0,12) girdisinde cap ≈ 12–16 kbps, ses 24'e çıkar — ilan edilen %12 yerine bütçenin
%18–25'i. Fark videodan çalınır, dosya hedefi aşar.

**K10 — `PickFastCodec` availability bilinmiyorken `av1_nvenc` döner (`:486`).** Listenin en egzotik üyesi
seçilir, çoğu makinede çalışmaz; yazılım yedeğine düşmeliydi. `:77` aynı varsayımla her fast planında
yanıltıcı `EncoderFallback` notu üretir.

**K11-K15 — daha küçükleri.** `Crf` yuvarlanırken `VideoBitrateK` yuvarlanmamış CRF'ten gelir
(`:165-166`, `:182-183`); `UsesCq` donanım kodeklerinde plan kendi içinde çelişir. `Correct` orijinal `ReasonCodes`'u tamamen siler (`:302`); HDR tonemap
ve encoder fallback notları ilk denemede kaybolur. `TransparencyCrf` (`:424`) sabit `CrfHalvingStep`
(6/7) ile ölçekler, oysa `BppfAtCrf`/`CrfForBppf` kalibre `HalvingStep` kullanır
(`ComplexityProfile.cs:140,149`) — adım 4 çıkarsa "3 CRF kademe" niyeti %50 sapar. `rate` yalnız
üstten sınırlı (`:359`), Extreme rejimde skor negatife düşer ve `:212` "predicted quality −12,4/100"
yazar. `CostsQualityInHardware` (`:492-495`) av1_nvenc/av1_qsv'yi muaf tutar ama `FastHardwareOrder`
listesindeki av1_amf'i (`:47`) tutmaz; dayanağı yok.

## 3. Sabitler

**Ölçüldüğü belgelenmiş (2 adet):** `ContainerOverhead = 0,995` (`:35`) — `docs/implementation-report.md:303`,
0,97'den ölçümle düzeltilmiş. libx264 iki geçiş verimi 0,9815 — `tests/VidShrink.Tests/FillBandTests.cs:193`.

**Ölçülmemiş / dosyada ve `docs/` altında dayanağı yok:**

- `CrfFitMargin = 0,94` (`:36`) — K2'nin yarısı bu sayı. `TwoPassUncertainty = 0,04` (`:37`) —
  projenin kendi ölçümü %1,85 sapma diyor; %4 iki kat temkinli ve K1'in doğrudan sebebi.
- `FillBand` çarpanları 0,972 / 0,944 / 0,95 / 0,90 / 0,92 / 0,85 ve 50 MB, 10 MB eşikleri (`:24-26`).
- `HardwareUncalibratedBias = 1,06` (`:43`) — hangi kodek, hangi bitrate, belirsiz.
- `MinScale 0,25`, `ScaleStep 0,02`, `MinHeight 240`, `MinFps 12`, `MinVideoBitrateK 48` (`:38-42`),
  `Dimensions` 0,985 eşiği (`:401`), preset'ler `"6"`/`"medium"`/`"slow"` (`:519-520`).
- `FpsPenalty` içine gömülü 12,0 ve 8,0 (`:417`) — `CodecModel`'e taşınmamış; 20 fps altı uçurumu 12 puan.
- Ses tabanları 128/96/160/112, cap sonrası 24 tabanı, mono eşiği 56, düşürme eşikleri 16 ve 96 (`:434-453`).
- `MeasuredEncoderEfficiency` kabul aralığı `>0,5 ve <2,0` (`:264`) — dışı sessizce atılır.
- `FastHardwareOrder` sıralaması (`:47`) — ölçüme değil isme dayanıyor.

## 4. ≥50 MB band sorunu — kök neden

1. `:24` — `lowerFactor = 0,972` ⇒ band genişliği `%2,8`.
2. `:141` — CRF girişi `<= 0,94·T`; `0,94 < 0,972` ⇒ `:171` daima doğru, doldurma dalı koşulsuz çalışır.
3. `:178` — `fillCrf` sürekli sayı olarak band merkezine (`0,986·T`) çözülür.
4. `:182` — `(int)Math.Round(fillCrf)`, yuvarlama hatası ±0,5 CRF.
5. `ComplexityProfile.cs:142` — `bppf = reference · 2^((refCrf − crf)/step)`. `step = 6` (h264/hevc) ⇒
   **1 CRF adımı = %12,25 boyut**; `step = 7` (av1) ⇒ %10,4; kalibre en iyi durumda `HalvingStep ≤ 12`
   (`ComplexityProfile.cs:39`) ⇒ %5,9. Izgara her koşulda %2,8'lik banddan geniş; isabet ≈ **%23**.
6. `EncodeRunner.cs:128` — `actualMb < band.LowerMb` ⇒ yeniden deneme. `:295` `Correct` 2pass'e geçer ama K1
   yüzünden `0,96154·T` ister; verim 0,9815 ile teslim `0,9437·T`, hard floor'un (`0,944·T`) da altı.
   2. deneme yalnız "verim ölç" işine yarayan tam bir kodlamadır.
7. 3. deneme `RetryAimMb` ölçümlü dalına (`:271`) girer, `0,986·T` ister, tutturur; `EncodeRunner.cs:64`
   gereği 4. deneme yok. Tipik maliyet: **3 tam kodlama**.

### Çözüm seçenekleri

| # | Değişiklik | Yan etki |
|---|---|---|
| A | `:273` `ceilingAim = Math.Max(band.LowerMb, T/(1+TwoPassUncertainty))`, sonucu `[LowerMb, UpperMb]`'ye kıstır | 2. denemede aşma riski artar; ama son denemede aşma `EncodeRunner.cs:95-99` ile saklanan band-altı dosyaya düşer, hata değil. En küçük diff; 3 deneme 2'ye iner. |
| B | `:180` koşuluna ızgara kontrolü ekle: band genişliği `2^(1/step)−1`'den darsa doğrudan 2pass (`:189`) | CRF'in hız avantajı ≥50 MB'da gider (1→2 geçiş), ama 3 kodlama yerine 1 kalır — net kazanç. `QualityCeiling` politikası etkilenmez. |
| C | `TwoPassUncertainty` 0,04 → ölçülmüş 0,0185; kodek başına ayır | Donanım kodeklerde aşma artar, `HardwareUncalibratedBias` telafi etmeli. Doğru çözüm, yeni ölçüm ister. |
| D | `:24` `lowerFactor`'ı bir CRF adımına genişlet (≈0,88) | 100 MB hedefte 88 MB kabul edilir olur — dar bandın amacına aykırı. Önerilmez. |
| E | `:182` `Math.Round` → `Math.Floor` | CRF sistematik yukarı taşar, `CeilingExceeded` hatası riski. Önerilmez. |

Önerilen: **B + A**. B ≥50 MB'da CRF ızgarasını devre dışı bırakır, A ölçümsüz denemeyi banda sokar; C bir sonraki ölçüm turunda.

## 5. Sadeleştirme

- `:288-297` — `previousVideoK * factor` ile `videoBudgetK` cebirsel olarak **özdeş** (verim tanımından türediği için); `efficiency != null` iken `Math.Min` no-op, yalnız null dalında iş görür. İki dal ayrılmalı.
- `:163-166`, `:189-192`, `:205-208` — `NewPlan`+`Mode`+`VideoBitrateK` üçlüsü üç kez tekrar; tek `ApplyMode` yeter.
- `:149-153` — not ekle/sil/yeniden ekle dansı; notları layout kesinleştikten **sonra** bir kez üretmek hem kısa hem K6'yı kapatır.
- `:77` vs `:465` — "fast preferred codec" iki farklı değer (av1_nvenc / h264_nvenc). Tek kaynağa inmeli.
- `:317-341` ile `:343-371` — iki döngü gövdesi neredeyse aynı; fark yalnız skor fonksiyonu ve `scale < fallback.Scale` filtresi. Ortak `EnumerateLayouts` + skor delegesiyle tek gövdeye iner.
- `:132`, `:138`, `:177`, `:251`, `:332`, `:356` — aynı `bppf ↔ videoK` dönüşümü altı kez elle yazılmış;
  korumalı `BitsPerPixel` (`:310`) zaten var, hepsi ona bağlanmalı — K5 de kapanır.
