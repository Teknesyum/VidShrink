# Handoff — 2026-09-11 16:10

Önce task, sonra durum oku. İlk bitmemiş parçadan sür.

## task
"izin verildi önce avalonia son sürüme güncelleme tamamlansın sonra v1 versiyon için gereksinimler önerdiğin şekilde yapılsın ... gerekirse fable a sor fable ne derse onu uygula v1 e koşalım"

## durum
- Avalonia 12.1.2 main'de (2beed839), CI yeşil (run 34600169690). Pilot 1 kapandı.
- Pilot 2 (libmpv SW render) main'de: docs/olcumler/libmpv-sw-render.md, tools/VidShrink.MpvBench. Fable 011: libmpv kalır, ek ölçüm yok; eşik 1080p ≤60 ms, 4K ≤200 ms; sürükle keyframes / bırak exact; hwdec=no.
- docs/plan.md + tahmin-isabet.md güncellendi (bc71f4c1), main CI izleniyor.
- 0. dalga iki ajanla koşuyor (worktree, opus):
  - A: osx-arm64 libmpv gömme kapısı, macOS CI workflow'u, rapor docs/olcumler/libmpv-macos-gomme.md. KALDI ise her platform LibVLC.
  - B: src/VidShrink.Player + IPlaybackEngine + PlayerView, win-x64, ci.yml libmpv indirme+sha256.

## next_action
Ajan dönüşlerini oku → dalların CI'sına bak → A GEÇTİ ise B'yi birleştir, tahmin-isabet satırlarını doldur → dalga 1.
A KALDI ise B'nin IPlaybackEngine'ine LibVLC uygulaması.

## bekleyen (kullanıcı)
Uzak eski dalların silinmesi: T177, T181-T185, T188, T189, T191-T193, t0/readme, yerel codex/astra-motor-inceleme.
