Tema: ffmpeg dagitimi · kaynak: ffmpeg-dagitimi.md

# Windows için ffmpeg yapıları ve dağıtımı

| Depo (tarama 2026-08-22, `gh api`) | Lisans | Yıldız | Açık issue | Son push | Son etiketli sürüm |
|---|---|---|---|---|---|
| BtbN/FFmpeg-Builds | MIT (betikler) | 11.526 | 36 | 2026-08-22 | `latest`, `autobuild-2026-08-22-12-58` |
| GyanD/codexffmpeg | lisans dosyası yok | 2.432 | 8 | 2026-08-20 | `9.0.1` (2026-08-12) |
| rdp/ffmpeg-windows-build-helpers | GPL-3.0 | 1.165 | 56 | 2026-03-16 | **release/tag yok** |

## rdp/ffmpeg-windows-build-helpers

**Ne yapıyor:** Linux/WSL'de çalışan tek bash betiği; mingw-w64 zincirini sıfırdan kurup ffmpeg ve yan araçları derliyor. QSV `--build-intel-qsv=y`
ile opsiyonel, LGPL için ayrı seçenek var (README). **Alınacak fikir:** Yapıyı sorulu değil parametreli kurmak — hangi kodlayıcının açık olduğu
argümanda görünsün.

**Alınmayacak:** Bağımlılık olarak alınamaz. Tag yok, 56 açık issue, son push 2026-03-16, README'de "custom builds, price negotiable" ücretli hizmet
daveti; derleme "yaklaşık 2 saat" (README iddiası, **doğrulanamadı**) ve Linux/WSL gerektiriyor. Betik GPL-3.0. **Nereye dokunur:** şimdilik hiçbir
yere.

**Kaynaklar** — `gh api`: `repos/BtbN/FFmpeg-Builds` (+`/releases`, `/contents/scripts.d`), `repos/GyanD/codexffmpeg` (+`/releases/latest`),
`repos/rdp/ffmpeg-windows-build-helpers` (+`/tags` boş, `/contents/README.md`), `repos/microsoft/winget-pkgs/.../g/Gyan/FFmpeg/9.0` ·
https://www.gyan.dev/ffmpeg/builds/

## Kodlayıcı ve lisans matrisi

BtbN `scripts.d/` içinde varyanta göre kapatılan tek şey `50-x264.sh` ve `50-x265.sh` (lgpl'de kapalı); `50-fdk-aac.sh` sadece nonfree'de açık.
`50-amf.sh`, `50-ffnvcodec.sh` (nvenc), `50-onevpl.sh` (qsv), `50-svtav1.sh` varyanta bakmıyor. **lgpl**: nvenc + qsv + amf + libsvtav1 var, x264/x265
yok. **gpl**: hepsi + x264/x265, ürün GPLv3 olur. **nonfree**: fdk-aac ekler, dağıtılamaz. **Gyan**: tüm yapılar GPLv3, `essentials` x264/x265 içerir
ama libsvtav1 içermez — libsvtav1 yalnız `full`'de.

VidShrink bugün `Install-VidShrink.ps1:98` ile WinGet `Gyan.FFmpeg` kuruyor; winget-pkgs manifesti (`manifests/g/Gyan/FFmpeg/9.0`)
`ffmpeg-9.0-full_build.zip` gösteriyor → **GPLv3 full build**, uygulamanın `LICENSE` dosyası ise MIT. Ayrı süreç çağrısı olduğu için kod birleşmesi
yok, ama ikiliyi `tools/ffmpeg` altında yeniden dağıtmak GPLv3'ün kaynak sunma ve lisans iletme yükümlülüğünü getiriyor.
