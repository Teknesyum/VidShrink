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

## 2. Kalite Turu: Düzenek

`nvenc-4-teslim.md` düzeneği: `.calisma/nvenc-2`'deki üç 10 sn FFV1 kesit (1920x818, 24 fps),
hevc_nvenc + av1_nvenc, 1000 / 2000 / 3500 kbit (1,22 / 2,44 / 4,27 MB), ürünün kendisi
`VidShrink.Bench shrink --speed quality --no-measure --lock-codec` ile — plan, kalibrasyon,
düzeltme denemeleri ve 0,97 bütçe doldurma dahil. VMAF-NEG `measure-pair` ile, her hücre ölçüldü.

- Kollar ürün kodu değiştirilmeden, `.calisma` altında yamalı kopyadan derlenen ayrı Bench
  ikilileriyle koştu (`tools/nvenc-5/derle.ps1`): `uhq` NVENC lookahead satırına `-tune uhq`
  ekler, `p7` `DefaultPreset`'te hevc `p4`→`p7`, av1 `p6`→`p7`, `uhqp7` ikisi birden. Yama
  tam bir kez eşleşmezse derleme durur.
- Kol seçimi pimli: `tools/nvenc-5/kalite.ps1` her hücrenin günlüğündeki ffmpeg komutunda kolun
  bayrağını arar, yoksa (ya da tabanda varsa) durur.
- Taban 18 dosyası `nvenc-4-ham.json`'daki `k097` koluyla (bugünkü ürün) **18/18 bayt aynı**.
- Yük sınırı: betik süreç eşleşimini 4 mantıksal çekirdeğe (0xF) kilitler, ffmpeg ve libvmaf
  alt süreçleri bunu miras alır; hücreler sırayla.
- Bir kesinti: `uhqp7 parlak hevc` ilk koşumda 2000 kbit'te `OpenEncodeSessionEx failed:
  incompatible client key (21)` ile düştü (sürücü oturum açamadı, geçici). Hücre baştan koşuldu;
  bitmiş 1000 kbit dosyası aynı puanı (84,99) verdi.

## 3. Kapı Hükmü

| Kol | ≥ taban + 0,10 | taban − 0,30'dan kötü | En kötü fark | Ort. fark | Süre oranı (ürün, toplam) | Süre oranı (deneme başına) | Tavan aşımı | HB önünde (ort) |
|---|---|---|---|---|---|---|---|---|
| taban | – | – | – | – | 1,00 | 1,00 | 0 | 9 / 18 |
| +uhq | 3 / 18 | 2 | −1,04 | −0,088 | 0,79 | 0,95 | 0 | 7 / 18 |
| +p7 | **8 / 18** | 0 | −0,02 | **+0,142** | 0,97 | 0,97 | 0 | **11 / 18** |
| +uhq+p7 | 4 / 18 | 1 | −0,40 | +0,083 | 0,79 | 0,98 | 0 | 11 / 18 |

**Hiçbir kol kapıdan geçmedi.** Şart 1 (≥ 15/18) üçünde de tutmadı; uhq ve uhq+p7 şart 2'yi de
kırdı. Süre ve tavan şartları hepsinde tuttu.

"HB önünde": `nvenc-3-lookahead-handbrake.md` tablosundaki HandBrake ortalamasından büyük olan
hücre. HB baytı o koşumda ürünün teslim ettiğinden −%8,9 .. +%6,6 sapıyor; bu sütun o kıyasın
adaletsizliğini miras alır.

## 4. Hücreler

VMAF-NEG ort (tabana fark) / teslim oranı.

| Kesit | Kodek | kbit | HB | taban | +uhq | +p7 | +uhq+p7 |
|---|---|---|---|---|---|---|---|
| karanlik | hevc | 1000 | 85,34 | 86,10 / 0,987 | 85,06 (−1,04) / 0,944 | 86,71 (+0,61) / 0,990 | 85,70 (−0,40) / 0,946 |
| karanlik | hevc | 2000 | 94,24 | 94,07 / 0,960 | 94,20 (+0,13) / 0,990 | 94,46 (+0,39) / 0,963 | 94,57 (+0,50) / 0,991 |
| karanlik | hevc | 3500 | 97,94 | 97,96 / 0,968 | 97,95 (−0,01) / 0,986 | 98,08 (+0,12) / 0,967 | 98,16 (+0,20) / 0,988 |
| karanlik | av1 | 1000 | 86,79 | 87,00 / 0,949 | 87,13 (+0,13) / 0,945 | 87,08 (+0,08) / 0,950 | 87,16 (+0,16) / 0,947 |
| karanlik | av1 | 2000 | 95,19 | 95,88 / 0,989 | 95,89 (+0,01) / 0,987 | 95,91 (+0,03) / 0,988 | 95,92 (+0,04) / 0,990 |
| karanlik | av1 | 3500 | 98,53 | 98,57 / 0,969 | 98,60 (+0,03) / 0,974 | 98,58 (+0,01) / 0,969 | 98,60 (+0,03) / 0,972 |
| parlak | hevc | 1000 | 84,09 | 83,83 / 0,885 | 84,79 (+0,96) / 0,974 | 83,93 (+0,10) / 0,887 | 84,99 (+1,16) / 0,980 |
| parlak | hevc | 2000 | 91,02 | 90,63 / 0,957 | 90,52 (−0,11) / 0,977 | 90,79 (+0,16) / 0,964 | 90,68 (+0,05) / 0,978 |
| parlak | hevc | 3500 | 94,09 | 93,98 / 0,969 | 93,81 (−0,17) / 0,987 | 94,13 (+0,15) / 0,972 | 93,98 (+0,00) / 0,990 |
| parlak | av1 | 1000 | 86,39 | 86,97 / 0,984 | 87,00 (+0,03) / 0,987 | 87,04 (+0,07) / 0,983 | 87,06 (+0,09) / 0,991 |
| parlak | av1 | 2000 | 92,62 | 91,96 / 0,975 | 91,82 (−0,14) / 0,976 | 91,96 (+0,00) / 0,975 | 91,83 (−0,13) / 0,976 |
| parlak | av1 | 3500 | 94,95 | 94,69 / 0,972 | 94,51 (−0,18) / 0,976 | 94,71 (+0,02) / 0,971 | 94,53 (−0,16) / 0,976 |
| hareketli | hevc | 1000 | 88,74 | 89,29 / 0,999 | 88,56 (−0,73) / 0,971 | 89,84 (+0,55) / 1,000 | 89,26 (−0,03) / 0,974 |
| hareketli | hevc | 2000 | 96,75 | 96,38 / 0,971 | 96,16 (−0,22) / 0,957 | 96,58 (+0,20) / 0,971 | 96,43 (+0,05) / 0,962 |
| hareketli | hevc | 3500 | 98,73 | 98,77 / 0,990 | 98,65 (−0,12) / 0,967 | 98,84 (+0,07) / 0,987 | 98,72 (−0,05) / 0,964 |
| hareketli | av1 | 1000 | 90,66 | 90,58 / 0,996 | 90,47 (−0,11) / 0,993 | 90,56 (−0,02) / 0,993 | 90,59 (+0,01) / 0,999 |
| hareketli | av1 | 2000 | 97,16 | 97,26 / 0,996 | 97,16 (−0,10) / 0,990 | 97,27 (+0,01) / 0,993 | 97,18 (−0,08) / 0,988 |
| hareketli | av1 | 3500 | 98,88 | 98,87 / 0,967 | 98,92 (+0,05) / 0,965 | 98,87 (+0,00) / 0,965 | 98,93 (+0,06) / 0,966 |

## 5. Kodlama Süresi

İki ölçü. **Ürün süresi** `results.json`'daki `EncodeSeconds` (bütün denemeler, FFV1 çözme ve
ölçekleme dahil); deneme sayısı kola göre değiştiği için deneme başına oran da yazıldı (§3).
**Saf kodlayıcı süresi** `tools/nvenc-5/hiz.ps1`: kesit önce `.y4m`'e çözülür, sonra kaynak
çözünürlükte ürün satırıyla (2000k, `-g 240`) `-f null`'a kodlanır, üç tekrarın ortancası
(ffmpeg açılışı dahil, 240 kare).

| Kesit | Kodek | taban | +uhq | +p7 | +uhq+p7 |
|---|---|---|---|---|---|
| karanlik | hevc | 0,834 sn | 0,971 sn ×1,16 | 1,494 sn ×1,79 | 1,577 sn ×1,89 |
| parlak | hevc | 0,874 sn | 1,007 sn ×1,15 | 1,549 sn ×1,77 | 1,486 sn ×1,70 |
| hareketli | hevc | 0,880 sn | 1,007 sn ×1,14 | 1,502 sn ×1,71 | 1,475 sn ×1,68 |
| karanlik | av1 | 1,068 sn | 1,144 sn ×1,07 | 1,081 sn ×1,01 | 1,211 sn ×1,13 |
| parlak | av1 | 1,049 sn | 1,126 sn ×1,07 | 1,061 sn ×1,01 | 1,183 sn ×1,13 |
| hareketli | av1 | 1,061 sn | 1,159 sn ×1,09 | 1,089 sn ×1,03 | 1,194 sn ×1,13 |

Ürün içinde fark görünmüyor (deneme başına ×0,95..0,98): 4 çekirdekte FFV1 çözme ve ölçekleme
darboğaz, kodlayıcı onu beklemiyor. Saf ölçüde hevc p4→p7 ×1,7..1,8, hâlâ 2,0 sınırının altında;
av1 p6→p7 neredeyse bedava.

## 6. Okuma

- **uhq hız denetimini değiştiriyor, kaliteyi değil.** Teslim oranı kaydıkça puan kayıyor:
  parlak hevc 1000'de 0,885 → 0,974 (+0,96), karanlık hevc 1000'de 0,987 → 0,944 (−1,04).
  Aynı bayta indirgendiğinde yön yok; ortalama −0,09.
- **p7 aynı baytta temiz bir kazanç, ama yalnız hevc'te.** Teslim oranı tabanla ±0,007 içinde
  (en büyük sapma parlak hevc 2000, +0,007). hevc'in 9 hücresinde fark +0,07 .. +0,61, 8'i
  ≥ +0,10; av1'de p6→p7 −0,02 .. +0,08, hiçbiri ≥ +0,10. 18 hücrelik kapı av1 yüzünden
  düşüyor.
- HandBrake'e karşı p7 11/18 hücrede önde (taban 9/18); bu sütun §3'teki bayt sapmasını
  taşıyor.

## Hüküm

Kapı açılmadı; ürüne öneri yok. Kapının dışında kalan tek bulgu: **hevc_nvenc'te p4 → p7**
aynı baytta 9 hücrenin 8'inde ≥ +0,10, hiçbirinde kayıp yok, saf kodlayıcıda ×1,7..1,8
yavaş, ürün süresinde görünmüyor. Bu, 18 hücrelik kapı değil; yalnız hevc'e sınırlı bir kapıyla
yeniden değerlendirilmesi ayrı bir karar.
