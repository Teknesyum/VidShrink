# Sahne haritası — eşik, kestirim değeri, maliyet (T96)

Kaynak: `.calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4` (1920x1080 hevc 10-bit HDR,
60 fps, 1036,17 sn, oyun görüntüsü). ffmpeg 9.0 (gyan.dev), 2026-09-02.
Makine paylaşımlıydı; paralelde başka ajanların ölçümleri koşuyordu.

## Sahne kesimi eşiği

Tek geçişte `select='gte(scene,0.05)',metadata=print` ile 531 aday toplandı;
eşikler aynı adaylar üzerinde 1 sn asgari sahne aralığıyla tarandı.

| Eşik | Kesim sayısı |
|------|-------------|
| 0.05 | 209 |
| 0.10 | 82 |
| 0.15 | 47 |
| 0.20 | 23 |
| 0.30 | 6 |

ffmpeg çıktısından örnek (kesimler `pts_time` + `lavfi.scene_score` çifti olarak okunur):

```
[Parsed_metadata_1 @ 00000238f5c7b180] frame:0    pts:31583   pts_time:0.350922
[Parsed_metadata_1 @ 00000238f5c7b180] lavfi.scene_score=0.111772
[Parsed_metadata_1 @ 00000238f5c7b180] frame:1    pts:40594   pts_time:0.451044
[Parsed_metadata_1 @ 00000238f5c7b180] lavfi.scene_score=0.056520
```

Gözle doğrulama: 28 aday için kesim öncesi/sonrası kareler çıkarılıp bakıldı.
≥0.20'deki 23 adayın **23'ü gerçek kesim** (diyalog açı değişimi, oyun→menü,
karartma). 0.15–0.20 bandından 5 örneklemin 4'ü gerçek, 1'i sahte
(t=128,2: aynı çekimde kamera kayması, skor 0.156).

**Seçilen eşik 0.20** — gözle bakılan örneklemde yanlış pozitif sıfır, hemen
altındaki bantta sahte kesim başlıyor. `SceneMap.DefaultThreshold = 0.2`.

## Sahne başına karmaşıklık

İlk deneme kaynak paket boyutuydu (bit/sn, ffprobe): kestirim **zayıftı,
Spearman 0.119** — menü/eğitim ekranlarında kaynak kodlayıcı bol bit harcarken
yeniden kodlama neredeyse bedava, sıralama çöküyor.

Yerine geçen sinyal: aynı geçişte 640 piksele küçültülmüş görüntü
`libx264 ultrafast crf 23` ile null'a kodlanır, kare başına kodlanmış boyut
`-vstats_file` üzerinden okunur. Sahnenin karmaşıklığı = sahnenin sonda
kodlama bit/sn'sinin tüm harita ortalamasına oranı. Tarama ve sonda tek
decode paylaşır (`split` filtresi).

## Kestirim değeri (K3)

Haritanın 24 sahnesinden 8'i seçildi (karmaşıklık aralığını ve süre çeşitliliğini
kapsayacak şekilde), her biri ayrı ayrı `libx264 veryfast crf 23 yuv420p` ile
tam çözünürlükte kodlandı; gerçek bit oranı çıktı paketlerinden ölçüldü.

| Sahne | Süre (sn) | Harita karmaşıklığı | Ölçülen bit/sn (CRF 23) |
|-------|-----------|--------------------|--------------------------|
| 17 | 12,2 | 1,559 | 12.796.401 |
| 10 | 39,8 | 1,457 | 10.643.348 |
| 12 | 5,6 | 1,015 | 8.132.302 |
| 3 | 14,9 | 0,794 | 3.450.082 |
| 21 | 7,5 | 0,765 | 4.477.049 |
| 23 | 22,5 | 0,489 | 3.133.854 |
| 16 | 2,8 | 0,408 | 1.510.243 |
| 14 | 28,5 | 0,129 | 596.712 |

**Spearman sıra korelasyonu: 0,976** (`SceneMap.Spearman`, 8 sahne; tek sıra
kayması 3↔21 komşu çifti). Kaynak paket sinyalinin 0,119'una karşılık sonda
kodlama sinyali sıralamayı taşıyor. Sekiz sahnelik örneklemde ölçüldü; farklı
içerik türlerinde genellenmedi.

## Çıkarma maliyeti (K4)

`SceneDetector.BuildMapAsync` tam kaynakta **107,3 sn** sürdü — kaynağın
**%10,4'ü** (17 dk video için ~1,8 dk). Makine paylaşımlıydı, sayı iyimser
sayılmamalı. Kabul edilebilir sınırın üstüne çıkarsa ilk aday, sonda
kodlamaya kare atlatmak (ör. 2 karede 1) ya da haritayı asıl kodlamanın
ilk geçişiyle birleştirmek.

## Ölçülmeyenler

- Farklı içerik (film, konuşan kafa, ekran kaydı) üzerinde eşik ve korelasyon.
- Harita→plan bağlaması (T96 kapsam dışı, `PlanCalculator` T89'da).
- Sonda kodlamanın süre kararlılığı (tek koşum, paylaşımlı makine).
