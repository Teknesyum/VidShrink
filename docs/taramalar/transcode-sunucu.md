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

## Depo: jellyfin/jellyfin-ffmpeg
NOASSERTION (LGPL/GPL karışık, kökte 4 COPYING) · 849 yıldız · 21 açık issue · push 2026-08-19 ·
sürüm v7.1.4-3 (2026-06-06).
**Ne yapıyor.** FFmpeg'in yamalı çatalı. Tespit yapmaz, gereksizleştirir: Windows için
`Dockerfile.win64.in` ile sabit toolchain'den nvenc/qsv/amf/vaapi aynı bayraklarla açık tek ikili.

**Alınacak fikir.** ffmpeg'i sabitle. PATH'teki rastgele ffmpeg prob sonucunu makineden makineye
değiştirir, hata raporu yeniden üretilemez. Maliyet: ~80 MB dağıtım yükü + GPL uyum metni.
**Alınmayacak.** FFmpeg'i çatallayıp yama taşımak — Jellyfin'in tam zamanlı yayın hattı var,
VidShrink'in yok. Hazır ikiliyi al, kaynağı değil.
**Nereye dokunur.** `src/VidShrink.Ffmpeg/ToolLocator.cs` — "uygulama yanındaki ikili önce".

## Depo: HaveAGitGat/Tdarr → HandBrake/HandBrake ile değiştirildi
Tdarr'da kaynak kod yok (kökte `docker/`, `flatpak/`, `updater/`, `assets/`), `LICENSE.md` OSI onaylı
değil — tescilli EULA; 4.278 yıldız, 49 açık issue, son commit 2026-08-05, GitHub sürümü yok. Tespit
incelenemez → HandBrake: NOASSERTION (başlıklarda GPL-2.0) · 24.110 yıldız · 295 açık issue ·
push 2026-08-22 · sürüm 1.11.2 (2026-06-07).
**Ne yapıyor.** ffmpeg CLI'ye sormaz, sürücü SDK'sına sorar. `libhb/nvenc_common.c`:
`nvenc_load_functions` ile NVENC'i dinamik yükler, `NvEncodeAPIGetMaxSupportedVersion` çağırır.
`libhb/qsv_common.c`: MFX oturumu açıp her kodlayıcı/profil için `MFXVideoENCODE_Query` koşar —
kodlama yapmadan gerçek yetenek sorgusu. Önbellek `static int is_nvenc_available = -1` sentinel'i,
süreç ömrü boyunca. Üstünde `hb_is_hardware_disabled()` global anahtarı, her probun ilk satırı.

**Alınacak fikir.** (1) Global donanım kapatma anahtarı — sürücüsü çöken kullanıcı tek bayrakla tüm
HW yolunu kapatır, uygulama çalışır kalır; maliyeti bir bool + ayar girişi. (2) Kodlayıcı başına ayrı
yetenek kaydı (`has_h264` / `has_h264_10bit` / `has_hevc` / `has_av1`) — "NVENC var" ile "NVENC AV1
var" farklı sorular; hedef boyut için kodek seçerken bu ayrım gerekecek.
**Alınmayacak.** SDK'ya doğrudan bağlanmak — HandBrake nvEncodeAPI ve libmfx başlıklarına derleme
zamanı bağımlı; .NET'te P/Invoke ile taşımak üç satıcının ABI'sini üstlenmek olur.
**Nereye dokunur.** `EncoderCapabilities.cs` (kodek kırılımı) · `src/VidShrink.Core/IEncoderAvailability.cs`
(kapatma anahtarı) · `src/VidShrink.Core/CodecModel.cs` (geri çekilme sırası).

## Kaynaklar
`gh api repos/<owner>/<repo>` + `/releases/latest`, dördü için 2026-08-22 · jellyfin master
`EncoderValidator.cs`, `MediaEncoder.cs`, `EncodingHelper.cs` · jellyfin-ffmpeg master kök listesi,
`Dockerfile.win64.make` · Tdarr master `LICENSE.md`, `README.md` · HandBrake master `libhb/*`. **Doğrulanamadı:** Tdarr çalışma zamanı GPU tespiti (kaynak kapalı); "1.000.000 dosyalık
kütüphanede test edildi" (Tdarr README, kendi beyanı).
