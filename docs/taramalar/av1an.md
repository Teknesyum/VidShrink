Tema: AV1 ve parcali hedef-kalite kodlama · kaynak: av1-parcali.md

# AV1 ve parçalı hedef-kalite kodlama taraması

Sapma: `master-of-zen/Av1an` artık `rust-av/Av1an`'a yönleniyor (GitHub API kalıcı yönlendirme). Aynı depo, yeni sahip adı.
Rakamlar 2026-08-22'de `gh api` ile çekildi. Kaynağı verilmeyen hiçbir sayı yazılmadı.

## Ne yapıyor

**Av1an** — videoyu sahne kesmesiyle parçalara böler, her parçayı ayrı işlemde paralel kodlar, sonda birleştirir. Hedef BOYUT yok, hedef KALİTE var (`--target-quality`). Köprü: her parça için kalite metriğini (VMAF / SSIMULACRA2 / Butteraugli / XPSNR) hedefe getiren quantizer'ı arar. Arama şöyle: 1-2. deneme min/max quantizer aralığının orta noktası (saf ikili arama), 3. deneme geçmişteki (skor, q) çiftleri üzerinden doğrusal enterpolasyon, 4. deneme kuadratik, 5+ PCHIP/Catmull-Rom/Akima gibi monoton eğri — enterpolasyon başarısız olursa ikili aramaya düşer. Varsayılan deneme sayısı **4**. Hedef tek sayı değil **aralık** (`FLOAT-FLOAT`); skor aralığa girince erken çıkar. Her başarısız denemeden sonra aralık daraltılır (q ± adım; x264/x265/SVT-AV1 için adım 0.25, diğerlerinde 1.0). Aralığa giren birden çok aday varsa **en yüksek quantizer** seçilir — yani hedefi tutturan en ucuz olan. Hiçbiri girmezse aralık ortasına en yakın olan. `--min-q`/`--max-q` uçlarında erken çıkış var. Prob'lar varsayılan olarak `veryfast` ve tam çözünürlükte, `--probing-rate` ile kare atlanabilir. Skor toplama yöntemi seçilebilir; varsayılan `auto`, eski varsayılan `percentile=1`.

## Depo

| Depo | Lisans | Son push | Son etiket | Açık issue | Yıldız |
|---|---|---|---|---|---|
| rust-av/Av1an | GPL-3.0 | 2026-08-19 | v0.5.2 (2026-01-04) | 156 | 1972 |
| AOMediaCodec/SVT-AV1 | BSD-3-Clause-Clear | 2026-08-09 | v4.2.0 (GitHub'da release yok, sadece tag) | 3 | 69 |
| xiph/rav1e | BSD-2-Clause | 2026-08-19 | v0.8.1 (2025-06-16) | 257 | 4143 |

SVT-AV1'in kanonik deposu GitLab; GitHub aynası olduğu için 3 açık issue ve 69 yıldız yanıltıcı — issue/tartışma orada.

## Alınacak fikir

1. **Enterpolasyonlu arama, saf ikili arama değil** (Av1an). İlk 1-2 ölçüm ikili arama, 3.'den sonra ölçülen (CRF, boyut) çiftlerinden log uzayında enterpolasyon. VidShrink zaten kalibrasyon probundan bir verim ölçüyor; bunu tek katsayı yerine **eğri** olarak biriktirmek düzeltme turlarını 3'ten 2'ye indirir. Maliyet: orta — `PlanCalculator.Correct` bir tur geçmişi (liste) almalı, tek `actualMb` değil.
2. **Kalibrasyon probu final ayarlarla koşsun** (SVT-AV1). Probun hızlı preset'te olması sorun değil ama **GOP yapısı/rate control modu final kodlamayla aynı olmalı**; SVT-AV1 belgesi rate matching kazancını doğrudan buna bağlıyor. Maliyet: düşük — `CalibrationProbe` argüman üretiminde preset dışındaki bayrakların final plandan miras alınması.
3. **Banda giren birden çok aday varsa en iyi kaliteli değil, hedefe en yakın olanı seç** (Av1an'ın tersi yönü). Av1an banda girenler arasından en yüksek q'yu (en ucuzu) alır çünkü kalite hedefi tabandır; VidShrink'te hedef tavandır, dolayısıyla banda girenler arasından **en büyük boyutlu** olan seçilmeli. Şu an `Correct` son denemeyi kullanıyor; tüm denemeleri saklayıp aralarından seçmek bedava bir kazanç. Maliyet: düşük — `EncodeResult.Trace` zaten var, seçim mantığı eksik.

## Alınmayacak

- **Parçalı paralel kodlama.** Av1an'ın çekirdek fikri; VidShrink tek dosya + tek ffmpeg süreci ile çalışıyor, sahne bölme + birleştirme WhatsApp boyutundaki klipte kazanç getirmez, ama VapourSynth/L-SMASH bağımlılık yüzeyi getirir.
- **Sahne başına ayrı CRF.** Av1an bunu yapar, VidShrink yapmamalı: donanım kodlayıcı yolunda (NVENC/QSV/AMF) parça başına yeniden başlatma maliyeti ve GOP sınırında görünür kalite sıçraması riski var.
- **Algısal metrik hedefleme (VMAF/SSIMULACRA2/Butteraugli).** VidShrink'in sözleşmesi boyut; metrik hedeflemek libvmaf'lı ffmpeg + model dosyası + VapourSynth eklentileri demek. Gizli kurulum maliyeti kabul edilemez.
- **rav1e'nin ibpp tablosu.** Katsayıları rav1e'nin kendi quantizer ölçeğine ve AV1'e bağlı; x264/x265/NVENC'e taşınamaz, taşınırsa sessizce yanlış çalışır.
- **SVT-AV1'in kare içi yeniden kodlama döngüsü.** ffmpeg CLI üzerinden erişilemez; ancak kütüphaneye bağlanınca anlamlı.

## VidShrink'te nereye dokunur

- `src/VidShrink.Core/PlanCalculator.cs` — `FillBand`, `RetryAimMb`, `Correct`: enterpolasyon geçmişi ve aday seçimi (fikir 1 ve 3).
- `src/VidShrink.Ffmpeg/CalibrationProbe.cs` — probun final plandan bayrak mirası (fikir 2).
- `src/VidShrink.Ffmpeg/EncodeRunner.cs` — `EncodeResult.Trace` üzerinden tur geçmişini `Correct`'e taşımak.
- `src/VidShrink.Core/EncodePlan.cs` — plana ölçülen (CRF, boyut) çiftlerini taşıyacak alan.
- `tests/VidShrink.Tests/FillBandTests.cs`, `CalibrationProbeTests.cs` — üç fikrin de testi burada.

## Kaynaklar

- `gh api repos/rust-av/Av1an`, `.../releases/latest` — meta veriler
- rust-av/Av1an: `site/src/Features/TargetQuality.md`, `site/src/Cli/target_quality.md`, `av1an-core/src/target_quality.rs`, `av1an-core/src/interpol.rs`
- AOMediaCodec/SVT-AV1: `Docs/Appendix-Rate-Control.md`
- xiph/rav1e: `src/rate.rs`
