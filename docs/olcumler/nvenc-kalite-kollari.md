# NVENC Kalite Kolları — Kabul Turu

19 Eylül 2026. ffmpeg 9.0-full_build (gyan.dev), yerel NVIDIA kodlayıcı. Betikler
`.calisma/nvenc-2/kabul.sh` ve `.calisma/nvenc-2/kollar.sh`.

`.calisma/nvenc-kollar/rapor.md` dört aday anahtar bıraktı ve dördü için de **beklenen puan
yazmadı**: hiçbiri ölçülmemişti. Ürünün NVENC satırında bugün hâlâ psy/AQ bloğu yok.

## 1. Kabul turu — anahtar gerçekten geçiyor mu

Kalite ölçmeden önce her anahtar 1 sn'lik `testsrc2` üstünde denendi. Ölçü yalnız çıkış kodu
değil: kabul edilen dosyanın `pix_fmt`/`profile`'ı ve baytı okundu, ve iki negatif kontrol kondu —
uydurma anahtar ile sınır dışı değer. SVT-AV1 tanımadığı anahtarı sessizce yutuyordu, NVENC'in
öyle yapmadığı burada ölçüldü.

| kodek | kol | sonuç | çıktı |
|---|---|---|---|
| hevc_nvenc | taban | kabul | Main, yuv420p, 179 086 bayt |
| hevc_nvenc | `-highbitdepth 1` | kabul | **Main 10, yuv420p10le**, 180 598 bayt |
| hevc_nvenc | `-tf_level 4` | **RED** | `InitializeEncoder failed: invalid param (8)` |
| hevc_nvenc | `-rc-lookahead 20 -lookahead_level 3` | kabul | Main, yuv420p, 180 133 bayt |
| hevc_nvenc | `-spatial-aq 1 -aq-strength 4` | kabul | Main, yuv420p, 178 609 bayt |
| hevc_nvenc | negatif: `-zipzop_level 4` | RED | `Unrecognized option 'zipzop_level'` |
| hevc_nvenc | negatif: `-aq-strength 99` | RED | `out of range [1 - 15]` |
| av1_nvenc | taban | kabul | Main, yuv420p, 190 238 bayt |
| av1_nvenc | `-highbitdepth 1` | kabul | **yuv420p10le**, 189 669 bayt |
| av1_nvenc | `-tf_level 4` | kabul | Main, yuv420p, 189 856 bayt |
| av1_nvenc | `-rc-lookahead 20 -lookahead_level 3` | kabul | Main, yuv420p, 181 239 bayt |
| av1_nvenc | `-spatial-aq 1 -aq-strength 4` | kabul | Main, yuv420p, 189 808 bayt |
| av1_nvenc | negatif: `-zipzop_level 4` | RED | `Unrecognized option 'zipzop_level'` |
| av1_nvenc | negatif: `-aq-strength 99` | RED | `out of range [1 - 15]` |

**İlk bulgu:** `-tf_level` `ffmpeg -h encoder=hevc_nvenc` çıktısında **listeleniyor** ama sürücü
HEVC'te reddediyor; yalnız AV1'de kuruluyor. Rapor onu iki kodek için de aday yazmıştı.

Kabul edilen her kolun baytı tabandan farklı — yani hiçbiri sessizce yutulmadı.
