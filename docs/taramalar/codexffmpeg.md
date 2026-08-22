Tema: ffmpeg dagitimi · kaynak: ffmpeg-dagitimi.md

# Windows için ffmpeg yapıları ve dağıtımı

| Depo (tarama 2026-08-22, `gh api`) | Lisans | Yıldız | Açık issue | Son push | Son etiketli sürüm |
|---|---|---|---|---|---|
| BtbN/FFmpeg-Builds | MIT (betikler) | 11.526 | 36 | 2026-08-22 | `latest`, `autobuild-2026-08-22-12-58` |
| GyanD/codexffmpeg | lisans dosyası yok | 2.432 | 8 | 2026-08-20 | `9.0.1` (2026-08-12) |
| rdp/ffmpeg-windows-build-helpers | GPL-3.0 | 1.165 | 56 | 2026-03-16 | **release/tag yok** |

## GyanD/codexffmpeg

**Ne yapıyor:** gyan.dev yapılarının release barındırıcısı ve issue kanalı, kod yok; `essentials`/`full`/`full-shared` üçlüsünü sürüm etiketiyle
yayınlıyor.

**Alınacak fikir:** Sürümün release tag'i olması sabitlemeyi tek satıra indiriyor — `winget install --id Gyan.FFmpeg --version 9.0.1`, ek altyapı
gerekmez.

**Alınmayacak:** Depoda lisans dosyası yok (`license: null`), tüm yapılar GPLv3 — MIT bir uygulamanın kurulum dizinine kopyalanması sessiz yükümlülük.
`essentials`'a kaçmak çözmez: yine GPLv3, üstüne libsvtav1 yok. Yapı içeriği yalnız web sitesinde, depoda makine-okunur manifest yok; otomatik
doğrulama kurulamaz. **Nereye dokunur:** `Install-VidShrink.ps1:98`, `README.md` kurulum bölümü.

## Kodlayıcı ve lisans matrisi

BtbN `scripts.d/` içinde varyanta göre kapatılan tek şey `50-x264.sh` ve `50-x265.sh` (lgpl'de kapalı); `50-fdk-aac.sh` sadece nonfree'de açık.
`50-amf.sh`, `50-ffnvcodec.sh` (nvenc), `50-onevpl.sh` (qsv), `50-svtav1.sh` varyanta bakmıyor. **lgpl**: nvenc + qsv + amf + libsvtav1 var, x264/x265
yok. **gpl**: hepsi + x264/x265, ürün GPLv3 olur. **nonfree**: fdk-aac ekler, dağıtılamaz. **Gyan**: tüm yapılar GPLv3, `essentials` x264/x265 içerir
ama libsvtav1 içermez — libsvtav1 yalnız `full`'de.

VidShrink bugün `Install-VidShrink.ps1:98` ile WinGet `Gyan.FFmpeg` kuruyor; winget-pkgs manifesti (`manifests/g/Gyan/FFmpeg/9.0`)
`ffmpeg-9.0-full_build.zip` gösteriyor → **GPLv3 full build**, uygulamanın `LICENSE` dosyası ise MIT. Ayrı süreç çağrısı olduğu için kod birleşmesi
yok, ama ikiliyi `tools/ffmpeg` altında yeniden dağıtmak GPLv3'ün kaynak sunma ve lisans iletme yükümlülüğünü getiriyor.
