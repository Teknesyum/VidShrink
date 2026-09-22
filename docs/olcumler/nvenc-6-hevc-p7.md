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

## Düzenek

Makine RTX 5070 Ti (sürücü 616.56), ffmpeg 9.0-full_build (gyan.dev). Kapı commit'i `58fdd8be`,
ölçüm ondan sonra koştu. Betikler `tools/nvenc-6/` (`kesit`, `derle`, `kalite`, `hiz`, `ozet`),
ham veri `nvenc-6-ham.json`. Bench ikilileri `derle.ps1` ile yamalı kopyadan: p7 kolunda yalnız
hevc satırı `p4` → `p7`, yama tam bir kez eşleşmezse durur. Her hücrenin günlüğünde kolun
preset'i bulundu (taban `-preset p4`, p7 `-preset p7`). Ayar dosyası `.calisma` altında.

## Hücreler

VMAF-NEG ortalaması, fark 2 ondalığa yuvarlanmış değerlerden.

| Kesit | kbit | taban | p7 | fark | teslim taban / p7 | deneme taban / p7 | p10 taban / p7 |
|---|---|---|---|---|---|---|---|
| gren | 1000 | 80,67 | 81,12 | +0,45 | 0,980 / 0,969 | 1 / 2 | 78,27 / 78,65 |
| gren | 2000 | 85,10 | 85,75 | +0,65 | 0,962 / 0,999 | 3 / 1 | 82,96 / 83,61 |
| gren | 3500 | 87,06 | 87,23 | +0,17 | 0,978 / 0,979 | 3 / 3 | 85,34 / 85,54 |
| gradyan | 1000 | 96,00 | 95,88 | **−0,12** | 0,864 / 0,919 | 3 / 3 | 95,42 / 95,24 |
| gradyan | 2000 | 96,29 | 96,30 | +0,01 | 0,584 / 0,643 | 2 / 2 | 95,89 / 95,93 |
| gradyan | 3500 | 96,27 | 96,29 | +0,02 | 0,329 / 0,366 | 2 / 2 | 95,85 / 95,90 |
| hayat | 1000 | 8,58 | 8,64 | +0,06 | 0,963 / 0,954 | 4 / 4 | 2,46 / 2,46 |
| hayat | 2000 | 15,94 | 16,20 | +0,26 | 0,996 / 0,998 | 1 / 1 | 11,20 / 11,08 |
| hayat | 3500 | 47,84 | 48,48 | +0,64 | 0,969 / 0,956 | 3 / 3 | 27,10 / 27,43 |

## Saf Kodlayıcı Süresi

`hiz.ps1`: ham `.y4m`'den 2000k ürün satırı, `-f null`, üç tekrarın ortancası (240 kare).

| Kesit | taban (p4) | p7 | oran |
|---|---|---|---|
| gren | 0,975 sn | 1,953 sn | ×2,003 |
| gradyan | 1,020 sn | 1,927 sn | ×1,89 |
| hayat | 1,066 sn | 1,917 sn | ×1,80 |

Ürün süresi (`EncodeSeconds` toplamı, deneme sayısı dahil) p7/taban ×1,18.

## Kapı Hükmü

| Şart | Ölçülen | Sonuç |
|---|---|---|
| 1. ≥ 7/9 hücrede ≥ taban + 0,10 | 5 / 9 | **KALDI** |
| 2. hiçbir hücre taban − 0,10'dan kötü değil | 1 hücre (gradyan 1000, −0,12) | **KALDI** |
| 3. tavan aşımı yok | 0 / 9 | geçti |
| 4. saf süre ≤ taban × 2,0, her kesitte | gren ×2,003 | **KALDI** |

**Kapı kaldı: p7 ürüne önerilmez.** Dört şarttan üçü tutmadı; tek tutan tavan şartı.

## Okuma

- Önceki turun "hevc'te 8/9" bulgusu yeni içerikte tekrar etmedi: 5/9. Kazanç dokulu içerikte
  (gren üçü de, hayat 2000/3500) duruyor, ortalama fark +0,24.
- gradyan kesiti bütçeyi dolduramıyor (teslim 0,33..0,92); kodlayıcı hedefe değil kendi tavanına
  kodluyor, iki kol ~96'da doyuyor. 1000'de p7 daha çok bayt harcayıp −0,12 verdi.
- hayat 1000 iki kolda da ~8,6'da; bu hedefte içerik ölçünün tabanına oturuyor, fark anlamsız.
- Süre şartı sınırda kaldı (×2,003, üç tekrarın ortancası); gürültü payı içinde olsa da kapı
  yazıldığı gibi okunur.
