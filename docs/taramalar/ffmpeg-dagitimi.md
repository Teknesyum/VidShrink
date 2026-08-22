# Windows için ffmpeg yapıları ve dağıtımı

| Depo (tarama 2026-08-22, `gh api`) | Lisans | Yıldız | Açık issue | Son push | Son etiketli sürüm |
|---|---|---|---|---|---|
| BtbN/FFmpeg-Builds | MIT (betikler) | 11.526 | 36 | 2026-08-22 | `latest`, `autobuild-2026-08-22-12-58` |
| GyanD/codexffmpeg | lisans dosyası yok | 2.432 | 8 | 2026-08-20 | `9.0.1` (2026-08-12) |
| rdp/ffmpeg-windows-build-helpers | GPL-3.0 | 1.165 | 56 | 2026-03-16 | **release/tag yok** |

## Kodlayıcı ve lisans matrisi

BtbN `scripts.d/` içinde varyanta göre kapatılan tek şey `50-x264.sh` ve `50-x265.sh` (lgpl'de kapalı); `50-fdk-aac.sh` sadece nonfree'de açık.
`50-amf.sh`, `50-ffnvcodec.sh` (nvenc), `50-onevpl.sh` (qsv), `50-svtav1.sh` varyanta bakmıyor. **lgpl**: nvenc + qsv + amf + libsvtav1 var, x264/x265
yok. **gpl**: hepsi + x264/x265, ürün GPLv3 olur. **nonfree**: fdk-aac ekler, dağıtılamaz. **Gyan**: tüm yapılar GPLv3, `essentials` x264/x265 içerir
ama libsvtav1 içermez — libsvtav1 yalnız `full`'de.

VidShrink bugün `Install-VidShrink.ps1:98` ile WinGet `Gyan.FFmpeg` kuruyor; winget-pkgs manifesti (`manifests/g/Gyan/FFmpeg/9.0`)
`ffmpeg-9.0-full_build.zip` gösteriyor → **GPLv3 full build**, uygulamanın `LICENSE` dosyası ise MIT. Ayrı süreç çağrısı olduğu için kod birleşmesi
yok, ama ikiliyi `tools/ffmpeg` altında yeniden dağıtmak GPLv3'ün kaynak sunma ve lisans iletme yükümlülüğünü getiriyor.

## BtbN/FFmpeg-Builds

**Ne yapıyor:** Docker'da çapraz derleme; her gün 12:00 UTC'de win64/linux64 statik ve shared yapı üretip release'e basıyor. Varyant + addin
(`gpl`/`lgpl`/`nonfree`, sürüm dalı, debug, lto) tek `build.sh` arayüzünde.

**Alınacak fikir:** (1) `lgpl` varyantına geçmek — VidShrink'in hız yolu nvenc/qsv/amf ve libsvtav1 üstünde, x264/x265'i bırakmak MIT dağıtımı
temizler. (2) `latest` kayan işaretçi, `n9.0-latest` sabit: sabit URL + SHA-256 ile indir. (3) Varyant adı dosya adında (`win64-lgpl`) — içerik ad
üzerinden doğrulanır.

**Alınmayacak:** `nonfree` varyantı, hiçbir koşulda. Deponun MIT lisansı **betikleri** kapsar, ikilinin lisansı varyanttan gelir — "MIT depo, ikili de
MIT" tuzağı. Saklama dar (son 14 günlük yapı + her ayın son yapısı 2 yıl); günlük etikete sabitlersen 14 gün sonra 404.

**Nereye dokunur:** `Install-VidShrink.ps1` (WinGet yerine `ffmpeg-n9.0-latest-win64-lgpl-9.0.zip` sabit indirme), `README.md` lisans bölümü,
`LICENSE` yanına üçüncü taraf bildirimi. `ToolLocator.cs` değişmez (zaten `tools/ffmpeg` ve PATH'e bakıyor); x264/x265 kaybı `VidShrink.Ffmpeg`
kodlayıcı seçimini etkiler — yazılım geri dönüşü libx264 değil libsvtav1/openh264 olmalı, ayrı karar.

## GyanD/codexffmpeg

**Ne yapıyor:** gyan.dev yapılarının release barındırıcısı ve issue kanalı, kod yok; `essentials`/`full`/`full-shared` üçlüsünü sürüm etiketiyle
yayınlıyor.

**Alınacak fikir:** Sürümün release tag'i olması sabitlemeyi tek satıra indiriyor — `winget install --id Gyan.FFmpeg --version 9.0.1`, ek altyapı
gerekmez.

**Alınmayacak:** Depoda lisans dosyası yok (`license: null`), tüm yapılar GPLv3 — MIT bir uygulamanın kurulum dizinine kopyalanması sessiz yükümlülük.
`essentials`'a kaçmak çözmez: yine GPLv3, üstüne libsvtav1 yok. Yapı içeriği yalnız web sitesinde, depoda makine-okunur manifest yok; otomatik
doğrulama kurulamaz. **Nereye dokunur:** `Install-VidShrink.ps1:98`, `README.md` kurulum bölümü.

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
