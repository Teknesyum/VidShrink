---
name: ffmpeg-wrapper-decision
description: VidShrink kendi ffmpeg süreç sarmalayıcısında kalıyor; FFMpegCore/FFmpeg.NET/AutoGen'e geçilmeyecek (2026-08-22 taraması)
metadata:
  type: project
---

2026-08-22'de yapılan tarama sonucu: VidShrink hazır bir .NET ffmpeg kütüphanesine
geçmeyecek, `src/VidShrink.Ffmpeg/EncodeRunner.cs` kendi sarmalayıcısı kalacak.

**Why:** EncodeRunner ilerlemeyi stdout'taki `-progress pipe:1` kanalından okuyor ve
stderr'i ayrı bir Task'te drene ediyor — FFMpegCore ve FFmpeg.NET ikisi de ilerlemeyi
stderr regex'inden okuyor, bu daha kırılgan. Ayrıca boyut hedefli çok denemeli döngü,
ölçülen encoder verimi ve atomik `.partial` çıkışı kütüphane API'lerine sığmıyor.

**How to apply:** Biri "FFMpegCore kullanalım" derse bu kararı hatırlat. Taramadan
alınmaya değer bulunan üç şey vardı ve hiçbiri bağımlılık değil: (1) iptalde `Kill()`
öncesi ffmpeg stdin'ine `q` yazıp temiz kapanış beklemek, (2) geçici kaynağı üreten
argümanın kendi temizliğinden sorumlu olması, (3) GIF paletini geçici PNG olmadan tek
`filter_complex` içinde üretmek. Ayrıntı: `docs/taramalar/dotnet-ffmpeg.md`.
