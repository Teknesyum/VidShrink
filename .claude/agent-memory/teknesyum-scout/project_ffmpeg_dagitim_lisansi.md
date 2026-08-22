---
name: ffmpeg-dagitim-lisansi
description: VidShrink MIT lisanslı ama dağıttığı ffmpeg ikilisi GPLv3 full build; lgpl varyantı nvenc/qsv/amf/libsvtav1'i kaybetmeden bunu çözüyor
metadata:
  type: project
---

VidShrink `Install-VidShrink.ps1` içinde WinGet `Gyan.FFmpeg` kuruyor. winget-pkgs manifesti
`ffmpeg-<sürüm>-full_build.zip` gösteriyor; gyan.dev'in bütün yapıları GPLv3. Uygulamanın kendi
`LICENSE` dosyası MIT. İkili `tools/ffmpeg` altına kopyalanıp dağıtıldığı için GPLv3 yükümlülüğü
doğuyor.

BtbN/FFmpeg-Builds `lgpl` varyantı bu çelişkiyi kapatıyor: `scripts.d/` içinde varyanta göre
kapatılan tek şey x264 ve x265; nvenc, qsv (onevpl), amf ve libsvtav1 lgpl'de de var.
`nonfree` varyantı (fdk-aac) hiçbir koşulda dağıtılamaz.

**Why:** Tarama 2026-08-22'de yapıldı, bulgu `docs/taramalar/codexffmpeg.md` ve `docs/taramalar/ffmpeg-builds.md` dosyasında.
Lisans uyumsuzluğu sessizdi — kimse kararla seçmemişti, WinGet varsayılanından geldi.

**How to apply:** ffmpeg dağıtımı, kurulum betiği veya kodlayıcı seçimi konuşulduğunda bunu
hatırlat. lgpl varyantına geçilirse yazılım geri dönüşü libx264 olamaz — libsvtav1/openh264
olmalı. Sürüm sabitleme `latest` etiketiyle yapılmamalı, `n<sürüm>-latest` veya aylık autobuild
etiketi kullanılmalı (BtbN son 14 günlük yapıyı tutuyor).
