# NVENC 6: Hevc_nvenc'te Preset P4 → P7

23 Eylül 2026. Önceki tur `nvenc-5-uhq-p7.md`: 18 hücrelik kapı düştü, hevc'in 9 hücresinde
p7 8/9 ≥ +0,10. Danışma `docs/danisma/011-fable-nvenc-hevc-p7.md`: sonradan daraltılan kapı
geçersiz; hipotez yeni, önceden yazılmış kapıyla yeni içerikte test edilir. Bu tur o testtir.

## Kapı

Ölçümden önce yazıldı ve ölçümden önce commit'lendi; commit'ten sonra değiştirilmez.

Kapsam: yalnız hevc_nvenc, iki kol — **taban** (bugünkü ürün, `--speed quality` → `-preset p4`)
ve **p7** (`FfmpegArguments.DefaultPreset`'te hevc `p4` → `p7`, başka hiçbir değişiklik yok).
3 yeni kesit × 3 hedef (1000 / 2000 / 3500 kbit) = **9 hücre**. Önceki turların
`karanlik` / `parlak` / `hareketli` kesitlerinden hiçbiri girmez.

p7 ürüne önerilir ancak dört şartın dördü de tutarsa:

1. 9 hücrenin **≥ 7**'sinde VMAF-NEG ortalaması ≥ taban + 0,10,
2. hiçbir hücrede VMAF-NEG ortalaması taban − 0,10'dan kötü değil,
3. hiçbir hücrede tavan aşımı (teslim > hedef) yok,
4. saf kodlayıcı süresi ≤ taban × 2,0 — her kesitte ayrı ayrı.

Ölçü tanımları:

- VMAF-NEG ortalaması `VidShrink.Bench measure-pair` çıktısından, 2 ondalığa yuvarlanmış;
  fark bu yuvarlanmış değerlerden alınır.
- Hücre = ürünün kendisi, `VidShrink.Bench shrink --speed quality --no-measure --lock-codec
  hevc_nvenc` (plan, kalibrasyon, düzeltme denemeleri, bütçe doldurma dahil). Kollar ürün kodu
  değiştirilmeden `.calisma` altında yamalı kopyadan derlenen ayrı Bench ikilileriyle koşar.
  Her hücrenin günlüğündeki ffmpeg komutunda kolun preset'i (`-preset p4` / `-preset p7`)
  aranır; yoksa koşum durur.
- Tavan: `results.json`'daki `ActualMb > TargetMb`.
- Saf kodlayıcı süresi: kesit önce ham `.y4m`'e çözülür, sonra ürün satırıyla (2000k, `-g 240`,
  `-rc-lookahead 20 -lookahead_level 3`) `-f null`'a kodlanır; üç tekrarın ortancası.
- Yük sınırı: süreç eşleşimi 4 mantıksal çekirdek (`0xF`), ffmpeg `-threads 4`, hücreler sırayla.

## Kesitler

İndirme yok; üçü de ffmpeg lavfi ile üretilir (`tools/nvenc-6/kesit.ps1`), 1920x1080, 24 fps,
10 sn, yuv420p, FFV1 mkv.

| Kesit | Karakter | Kaynak |
|---|---|---|
| gren | gren / gürültü | `testsrc2` + `noise=alls=20:allf=t+u` |
| gradyan | düz yüzey / gradyan | `gradients` (4 renk, yavaş dönüş) |
| hayat | yüksek hareket | `life` 480x270, `scale` 4× en yakın komşu, her karede değişir |
