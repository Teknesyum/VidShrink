# Karar 10 — Ekran Kaydında Bant Altı Teslim Oranı

Durum: **ölçüldü, kod değişmedi.** Soru: ekran içeriğinde ürün hedefin ne kadarını dolduruyor, bant altına ne zaman
düşüyor.

## Düzenek

- Koşum **35158725446**, etiket `olcum-hb-handbrake+dusuk+social+bantlasma+turbo+hdr+ekranbant+vt--1`, commit
  `02993179`, iş `olcum (ekranbant, ekran)`; ek satırlar aynı koşumun `olcum (handbrake, ekran)` ve `vt` işlerinden.
- Kaynak: Netflix "Debugging", xiph AOM CTC `b2_scc`
  (`https://media.xiph.org/video/aomctc/test_set/b2_scc/Debugging_1920x1080_30fps_8bit_420.y4m`, sha256
  `8004BE81AEDB40F9C26DB25FF72D8725D832EF3B95747FB4C9FC8A3693EB4E8E`, CC BY 4.0), 1920x1080@30, 4,33 sn.
- `ekranbant`: kaynak döngüyle 120 sn'ye uzatıldı (`uzun-ekran.mkv`, x264 crf 4, 57,96 MB), ürün 10/25/50/100 MB
  hedefe koşuldu. İçerik tekrarlı olduğu için sonuç gerçek uzun ekran kaydının üst sınırı sayılmalı.
- Doluluk = teslim MB / hedef MB. İlk verim = ilk denemenin çıkan / hedeflenen MB'ı.

## Sonuç

| Kaynak | Codec | Hedef | Teslim | Doluluk | Deneme | Durum |
|---|---|---|---|---|---|---|
| 120 sn döngü | libx264 2pass, 1382x778 | 10 MB | 9,761 MB | %97,61 | 1 | bantta |
| 120 sn döngü | libx264 2pass | 25 MB | 24,592 MB | %98,37 | 1 | bantta |
| 120 sn döngü | libx264 2pass | 50 MB | 49,288 MB | %98,58 | 2 | bantta (1. deneme 50,18 MB tavan aşımı) |
| 120 sn döngü | libx264 passthrough | 100 MB | 57,96 MB | %58 | 1 | kaynak hedeften küçük, yeniden kodlanmadı |
| 4,33 sn kesit | libsvtav1 2pass | 600 kbit (0,32 MB) | 0,29 MB | %92,9 | 1 | bantta |
| 4,33 sn kesit | libsvtav1 | 2000 kbit (1,06 MB) | 0,87 MB | %82,3 | 3 | bant altı kabul |
| 4,33 sn kesit | hevc_videotoolbox | 2000 kbit (1,06 MB) | 0,66 MB | %62,2 | 2 | bant altı kabul |
| 4,33 sn kesit | hevc_videotoolbox | 5500 kbit (2,91 MB) | 0,69 MB | %23,9 | 2 | bant altı kabul |

- 120 sn'de 10/25/50 MB bantta, taşma yok; ilk verim 1,001, 1,009, 1,018. VMAF-NEG ort 83,98, 95,40, 97,08.
- 100 MB'da ara dosya hedeften küçük: ürün passthrough yaptı, günlükte `taban=IHLAL` yazıyor. Bu teslim beklenen
  davranış; ölçü satırı yok.
- Bant altı yalnız kısa kesitte ve yüksek bit hızında: kodlayıcı içeriğin gerektirdiğinden fazla bit harcamıyor.
  libsvtav1 2000'de 1956k → 2557k'ya çıkıldı, çıktı 0,69 → 0,78 → 0,87 MB. VT 5500'de iki denemede de 0,695 MB.
- Uzun ekran kaydında Auto codec libx264 seçti, 4,33 sn kesitte libsvtav1.

Karar: Ekran kaydında bant altı teslim süre değil bit yoğunluğu sorunu; 120 sn döngüde gerçekçi hedeflerde %97,6–98,6
dolu. Kısa kesitte %23,9'a kadar düşen teslim kalite kaybı değil (B1 ve B6'da `ekran` satırları önde), kullanılmayan
bütçe.
**Ölçülmedi:** döngüsüz uzun ekran kaydı, 100 MB'ın yeniden kodlandığı durum.

Açık kusur (yalnız raporlandı): Bench özet satırı ilk denemenin modunu yazıyor; `hb-ekran-2000-urun.log` teslim edilen
2557k 2pass çıktısı için `libsvtav1/crf, crf 25` diyor.

## Tablo

| Hedef MB | Teslim MB | Doluluk % | Taşma | Bantta | Deneme | İlk kbit | İlk hedeflenen→çıkan | İlk verim | Geometri | VMAF-NEG ort | VMAF-NEG harm | XPSNR | Kodlama sn | Hata |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 10,0 | 9,761 | 97,61 | hayır | evet | 1 | 678 | 1:in band:678k:9.75->9.761 | 1,001 | 1382x778@30 | 83,98 | 83,92 | 33,25 | 64,8 | — |
| 25,0 | 24,592 | 98,37 | hayır | evet | 1 | 1695 | 1:in band:1695k:24.375->24.592 | 1,009 | 1920x1080@30 | 95,40 | 95,39 | 48,10 | 113,4 | — |
| 50,0 | 49,288 | 98,58 | hayır | evet | 2 | 3429 | 1:over ceiling:3429k:49.3->50.18 / 2:in band:3369k:49.3->49.289 | 1,018 | 1920x1080@30 | 97,08 | 97,07 | 60,11 | 209,9 | — |
| — | — | — | — | — | — | — | — | — | — | — | — | — | — | urun ciktisi yok: ekranbant-100 |
