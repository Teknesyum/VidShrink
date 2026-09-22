# NVENC 5: Tune Uhq Ve Preset P7

Makine RTX 5070 Ti (sürücü 616.56, NVENC API 13.1), ffmpeg 9.0-full_build (gyan.dev).
23 Eylül 2026. Defter satırı: "NVENC 2: aynı baytta kalitede HandBrake'in önüne geç".
Denenmiş kollar (GOP 10 sn, tam kare, lookahead 20, bütçe doldurma, spatial-aq,
tf_level, lookahead_level) dışında kalan iki kol: `-tune uhq` ve `-preset p7`.
`-highbitdepth 1` kullanıcı kararıyla kapsam dışı.

Ürün bugün `--speed quality` ile hevc_nvenc'te `p4`, av1_nvenc'te `p6` kullanıyor
(`FfmpegArguments.DefaultPreset`, `PlanCalculator.PickPreset`); `-tune` NVENC satırında yok
(ffmpeg varsayılanı `hq`).

## Kapı

Ölçümden önce yazıldı, sonra değiştirilmedi. Bir kol ürüne önerilir ancak:

1. 18 hücrenin (3 kesit × 2 kodek × 3 hedef) **≥ 15**'inde VMAF-NEG ortalaması ≥ taban + 0,10,
2. hiçbir hücrede taban − 0,30'dan kötü değil,
3. kodlama süresi ≤ taban × 2,0,
4. hiçbir hücrede tavan aşımı (teslim > hedef) yok.

## 1. Kabul Turu

`tools/nvenc-5/kabul.sh`: 1 sn `testsrc2` 640x360, `-b:v 1000k`, `-threads 4`. "ürün satırı" =
`-rc vbr -multipass fullres -maxrate 2000k -bufsize 2000k -rc-lookahead 20 -lookahead_level 3`
ile ürünün preset'i. Ölçü çıkış kodu, `pix_fmt`/`profile` ve bayt; iki negatif kontrol.

| kodek | kol | sonuç | çıktı |
|---|---|---|---|
| hevc_nvenc | taban | kabul | Main, yuv420p, 179 086 bayt |
| hevc_nvenc | `-tune uhq` | kabul | Main, yuv420p, 190 399 bayt |
| hevc_nvenc | `-preset p7` | kabul | Main, yuv420p, 179 385 bayt |
| hevc_nvenc | ürün satırı (p4) | kabul | Main, yuv420p, 180 060 bayt |
| hevc_nvenc | ürün satırı + uhq | kabul | Main, yuv420p, 176 902 bayt |
| hevc_nvenc | ürün satırı, p7 | kabul | Main, yuv420p, 179 971 bayt |
| hevc_nvenc | ürün satırı + uhq, p7 | kabul | Main, yuv420p, 171 576 bayt |
| hevc_nvenc | negatif: `-tune zipzop` | RED | `Undefined constant or missing '(' in 'zipzop'` |
| hevc_nvenc | negatif: `-tune 9` | RED | `Value 9.000000 for parameter 'tune' out of range [1 - 5]` |
| av1_nvenc | taban | kabul | Main, yuv420p, 190 238 bayt |
| av1_nvenc | `-tune uhq` | kabul | Main, yuv420p, 190 385 bayt |
| av1_nvenc | `-preset p7` | kabul | Main, yuv420p, 187 249 bayt |
| av1_nvenc | ürün satırı (p6) | kabul | Main, yuv420p, 180 534 bayt |
| av1_nvenc | ürün satırı + uhq | kabul | Main, yuv420p, 176 748 bayt |
| av1_nvenc | ürün satırı, p7 | kabul | Main, yuv420p, 177 986 bayt |
| av1_nvenc | ürün satırı + uhq, p7 | kabul | Main, yuv420p, 177 029 bayt |
| av1_nvenc | negatif: `-tune zipzop` | RED | `Undefined constant or missing '(' in 'zipzop'` |
| av1_nvenc | negatif: `-tune 9` | RED | `Value 9.000000 for parameter 'tune' out of range [1 - 5]` |

Taban baytları `nvenc-kalite-kollari.md` §1 ile birebir aynı (179 086 / 190 238): düzenek
belirlenimci. Kabul edilen her kolun baytı kendi karşılığından farklı, yani hiçbiri sessizce
yutulmadı; `-loglevel verbose` ürün satırı + uhq + p7'de uyarı basmıyor. İki kol da iki kodekte
kabul — kalite turuna dört kol giriyor: taban, +uhq, +p7, +uhq+p7.
