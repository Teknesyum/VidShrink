---
name: dagitim-imza-smartscreen
description: Windows medya araçlarının dağıtım/imza gerçeği — MPC-HC dahil emsaller imzasız yayınlıyor, VidShrink için SmartScreen stratejisi
metadata:
  type: project
---

Windows medya aracı nişinde kod imzalama norm değil. 2026-08-22'de doğruladım:
clsid2/mpc-hc 2.8.0 x64 kurucusu (15,5 bin yıldız, 97.526 indirme) Authenticode
**imzasız** — `Get-AuthenticodeSignature` `NotSigned` döndürdü. Depoda tam imza
altyapısı var ama sertifika argümanları `signinfo.txt` ile depo dışında tutuluyor ve
dosya yoksa derleme sessizce imzasız devam ediyor. shinchiro/mpv-winbuild-cmake ve
m-ab-s/media-autobuild_suite'te imza adımı hiç yok.

**Why:** VidShrink kurulumu `LOCALAPPDATA\Programs` altına yerleşiyor ve SmartScreen
"bilinmeyen yayıncı" uyarısı alması bekleniyor. Sertifika almanın maliyeti sorgulanırken
emsalin ne yaptığı belirleyici oldu.

**How to apply:** SmartScreen sorunu gündeme geldiğinde önce ucuz olanı öner —
README'de uyarının geleceğini önceden yazmak, tek sabit indirme adresi, yayın
varlığının yanında SHA-256. Kod imzalama sertifikası bu üçü yapılmadan önerilmemeli.
Ayrıntılı tarama: `docs/taramalar/mpc-hc.md`, `docs/taramalar/media-autobuild-suite.md`, `docs/taramalar/mpv-winbuild-cmake.md`.
Bkz. [[vidshrink-kurulum-zinciri]].
