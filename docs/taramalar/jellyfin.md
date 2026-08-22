Tema: transcode sunucu · kaynak: transcode-sunucu.md

# Transcode sunucularında donanım kodlayıcı tespiti (2026-08-22)

## Depo: jellyfin/jellyfin
GPL-2.0 · 56.048 yıldız · 598 açık issue · push 2026-08-22 · sürüm v10.11.11 (2026-06-06).
**Ne yapıyor.** Canlı transcode eden medya sunucusu; tespit `MediaBrowser.MediaEncoding/Encoder/`
altında `EncoderValidator.cs` + `MediaEncoder.cs`. Gerçek kodlama probu **yok**: `-encoders`,
`-decoders`, `-hwaccels`, `-h filter=X`, `-h bsf=X` çıktıları regex'le ayrıştırılıp sabit "gerekli"
listeyle kesiştirilir. Süreç açan tek yerler yan yetenekler: VAAPI sürücü kimliği
(`-init_hw_device vaapi=va:<node>`, stderr'de sürücü adı aranır), Vulkan/DRM interop
(`-init_hw_device drm=dr:<node> -init_hw_device vulkan=vk@dr`), `-hwaccel_flags +low_priority` ile
`nullsrc=s=1x1` üzerinden çıkış kodu. **Zaman aşımı yok** — `WaitForExit()` argümansız. Önbellek:
`SetFFmpegPath` bir kez koşar, sonuç süreç ömrü boyunca alanda. Geri çekilme sırası yok; kullanıcı
tek `HardwareAccelerationType` seçer, HW iş kaçarsa yazılıma otomatik düşmez. `-hwaccel` seçimi
kod başına tabloyla (`GetHwaccelType`), prob sonucuyla değil.

**Alınacak fikir.** Prob süreçlerinde stdout **ve** stderr'i eşzamanlı boşalt — Jellyfin bunu boru
tıkanması yaşadıktan sonra ekledi (`EncoderValidator.cs`: "Drain both streams concurrently to
prevent pipe hanging, see #17429"). VidShrink'in 4 sn zaman aşımı bunu maskeler ama her probu 4 sn'ye
yayar. Maliyet: iki `ReadToEndAsync`.
**Alınmayacak.** `-encoders` listesine güvenmek: liste derleme zamanı yeteneğidir, `h264_nvenc`
sürücü yokken de listelenir. Zaman aşımsız `WaitForExit` de alınmaz — asılı ffmpeg açılışı kilitler.
**Nereye dokunur.** `src/VidShrink.Ffmpeg/EncoderCapabilities.cs` — `ProbeEncoder`, `RunCapture`.

## Kaynaklar
`gh api repos/<owner>/<repo>` + `/releases/latest`, dördü için 2026-08-22 · jellyfin master
`EncoderValidator.cs`, `MediaEncoder.cs`, `EncodingHelper.cs` · jellyfin-ffmpeg master kök listesi,
`Dockerfile.win64.make` · Tdarr master `LICENSE.md`, `README.md` · HandBrake master `libhb/*`. **Doğrulanamadı:** Tdarr çalışma zamanı GPU tespiti (kaynak kapalı); "1.000.000 dosyalık
kütüphanede test edildi" (Tdarr README, kendi beyanı).
