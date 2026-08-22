# CPU algoritması check-up

Tarih: 2026-08-20 · İnceleyen: T0 · Kapsam: `PlanCalculator`, `ComplexityProfile`,
`ComplexityProbe`, `CalibrationProbe`, `FfmpegArguments`, `EncodeRunner`

Sıralama önem sırasına göre. Her madde ya ölçülerek ya kod okunarak doğrulandı;
doğrulanmamış olanlar açıkça işaretli.

---

## 1. NVENC iki geçişi sahte — ölçüldü, doğrulandı

`FfmpegArguments.cs:65-69` iki geçişli modda kodlayıcıya bakmadan `-pass 1` / `-pass 2`
ve `-passlogfile` veriyor. NVENC bu bayrakları yok sayıyor.

Bu makinede koşturulan doğrulama: `h264_nvenc` ile `-pass 1` çalıştırıldığında istatistik
dosyası **0 bayt** üretiyor. Yani ikinci geçişin okuyacağı hiçbir bilgi yok.

Sonuç: `Fast - NVENC` seçildiğinde dosya **iki kez** kodlanıyor, birincisi tamamen çöpe
gidiyor. Kodlama süresi bedelsiz iki katına çıkıyor, karşılığında hiçbir isabet kazancı yok.

Doğrusu tek geçişte `-rc vbr -multipass fullres`. NVENC'in kendi iki geçişi budur ve
tek ffmpeg koşusunda biter.

## 2. GPU kodlayıcı varlığı gerçekten sınanmıyor

`EncoderCapabilities` yalnızca `ffmpeg -encoders` çıktısını okuyor. Bu liste NVIDIA
kartı olmayan makinede de `h264_nvenc` içerir — ffmpeg derlemesinde var demek, kart
var demek değil. Kullanıcı `Fast` seçerse kodlama ffmpeg hatasıyla düşer, sebebi
anlaşılmaz.

Gereken: bir kez, tek karelik gerçek kodlama denemesi (`lavfi` kaynağıyla, ~50 ms) ve
sonucun oturum boyunca saklanması.

## 3. `av1_nvenc` motorda hiç yok — ölçüm zaten mevcut

`docs/gpu-kodlama-bulgusu.md` içindeki ölçüm: aynı boyutta `av1_nvenc` p7, `libx265`
slow'un yalnızca **0,8 VMAF** gerisinde ve **7 kat hızlı**. `hevc_nvenc` ise 4,4 VMAF
geride — arayüzdeki "megabayt başına belirgin kalite kaybettirir" uyarısı yalnızca
onun için doğru.

`PlanParser.AllowedCodecs`, `CodecModel` ve `PlanCalculator.PickCodec` `av1_nvenc`'i
tanımıyor. Hızlı mod bu kodlayıcı üzerine kurulmalı; kalite uyarısı kodlayıcı bazında
ayrışmalı.

## 4. Karmaşıklık probu seri koşuyor, kalibrasyon paralel

`ComplexityProbe.RunAsync` pencereleri `foreach` içinde tek tek bekliyor
(`ComplexityProbe.cs:41-53`); `CalibrationProbe` aynı işi `Task.WhenAll` ile paralel
yapıyor. Aynı dosyada iki farklı disiplin.

Ayrıca her pencere **iki kez çözülüyor**: bir kez tam ölçek, bir kez yarı ölçek. Tek
ffmpeg koşusunda `split` filtresiyle iki çıkışa aynı anda kodlanabilir — çözme maliyeti
yarıya iner.

İkisi birlikte, ölçüm süresini kabaca üçte bire indirir. *(Rakam kod okumasından
türetilmiş tahmindir, ölçülmedi.)*

## 5. Prob maliyeti dosyaya göre uyarlanmıyor

Tam ölçek probu `libx264 -preset medium` kullanıyor. 1080p'de bu kabul edilebilir;
4K60 bir kaynakta 3×2 saniyelik pencere tek başına onlarca saniye sürer. Pencere
sayısı ve preset çözünürlükten bağımsız sabit.

`-hwaccel` de hiç kullanılmıyor. Ölçülen kazanç: `-hwaccel cuda` ile çözme %19 hızlanıyor
ve kaliteyi hiç etkilemiyor, çünkü yalnızca girdinin çözülmesini GPU'ya alır.

## 6. NVENC hedef bitrate'i tutturamıyor — doluluk bandını kırar

Ölçüm: 1 876 kbit/sn istendiğinde NVENC hedefin **%12 altında** dosya üretti. T3'te
kurulan doluluk bandının alt sınırı %5-8 aralığında. Yani hızlı modda motor kendi
bandının dışına düşer ve `EncodeRunner` her seferinde alt-taşma düzeltme turu açar —
hız kazancının bir kısmı orada geri verilir.

Donanım yolunda ya band gevşetilmeli ya sapma kalibrasyondan öğrenilip bitrate önceden
yukarı itilmeli. İkincisi doğrusu.

## 7. Ölçek cezası ölçülmüyor, sabit

Motor detayın ölçekle nasıl düştüğünü **ölçüyor** (`DetailExponent`) ama düşürmenin
*algısal bedelini* sabit bir eğriden alıyor (`CodecModel.ScalePenaltyScale = 10.0`,
`ScalePenaltyExponent = 1.1`). README'nin öne çıkardığı "ölçerek karar ver" iddiası
kararın yarısı için geçerli.

Aynı şey zamansal tarafta da var: `FpsBitrateExponent` sabit 0,75. Bu, açık duran
**T4** sözleşmesinin konusu.

## 8. `PickPreset` QSV için geçersiz değer üretiyor — gizli hata

`PlanCalculator.cs:452` `CodecModel.UsesCq(codec)` doğruysa `"p5"` döndürüyor. `UsesCq`
`qsv` için de doğru, ama QSV presetleri `veryfast…veryslow`. `"p5"` geçersiz.

Bugün canlı değil, çünkü `PickCodec` hiçbir zaman QSV döndürmüyor ve yapıştırılan AI
planı `IsValidPreset` denetiminden geçiyor. Ama QSV/AMF desteklenir desteklenmez canlıya
çıkar. Hızlı mod tam olarak bunu yapacak.

## 9. `-hwaccel` yokluğu CPU yolunda da maliyetli

Yazılım kodlayıcı seçilse bile **çözme** GPU'ya alınabilir; kalite hiç etkilenmez.
4K H.264/HEVC kaynakta kazanç belirgin. Bugün hiçbir yerde kullanılmıyor.

## 10. Küçük notlar

- `ParseVideoBytes` ffmpeg özet satırındaki `video: NNNkB` değerini okuyor; bu kB'a
  yuvarlanmış. Tarama yolundaki `vstats` okuması daha hassas. İki yol arasında
  tutarsızlık var, ölçüm hatası küçük.
- `MinScale 0.25` + `ScaleStep 0.02` → 38 ölçek × 7 fps = 266 aday. Salt aritmetik,
  maliyeti yok. Sorun değil.
- `EncodeRunner.MaxAttempts = 3` ve `Correct()` her koşulda `2pass`'e geçiyor. Donanım
  yolunda bu, 1. maddedeki sahte iki geçişe düşürür.

---

## Sıralanmış iş listesi

| # | Ne | Nerede | Kazanç |
|---|---|---|---|
| 1 | Sahte NVENC iki geçişini kaldır | `FfmpegArguments` | Donanım yolunda süre yarıya |
| 2 | GPU'yu gerçekten yokla | `EncoderCapabilities` | Anlaşılmaz ffmpeg hatası biter |
| 3 | `av1_nvenc` ekle | `CodecModel`, `PlanParser`, `PlanCalculator` | x265 kalitesi, 7 kat hız |
| 4 | Probu paralelleştir + tek çözme | `ComplexityProbe` | Ölçüm süresi ~1/3 |
| 5 | `-hwaccel` çözme | `FfmpegArguments`, problar | %19 çözme hızı, kalite sabit |
| 6 | NVENC bitrate sapmasını öğren | `PlanCalculator` | Gereksiz düzeltme turu biter |
| 7 | QSV preset hatası | `PlanCalculator.PickPreset` | Gizli hata kapanır |
| 8 | Ölçek/fps cezasını ölçüme bağla | T4 sözleşmesi | Ayrı iş |
