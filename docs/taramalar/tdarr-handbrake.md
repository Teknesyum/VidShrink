Tema: transcode sunucu · kaynak: transcode-sunucu.md

# Transcode sunucularında donanım kodlayıcı tespiti (2026-08-22)

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
