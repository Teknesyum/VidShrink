Tema: psikogorsel ayar · kaynak: psikogorsel-ayar.md

# Psikogörsel kodlayıcı ayarı ve oynatıcı tarafı

Yerel doğrulama ortamı: ffmpeg 9.0-full (gyan.dev), SVT-AV1 lib v4.2.0-68-gc1e79b04f, x265 4.3+2. Tarih 2026-08-22.

## Depo 3 — LAV Filters

**Ne yapıyor.** Windows DirectShow ayrıştırıcı ve kod çözücü seti (MPC-HC ve benzerlerinin altı), ffmpeg tabanlı. 9.101 yıldız, 108 açık issue, GPL-2.0, son sürüm 0.83 (2026-08-17), son push 2026-08-17 — üçü içinde en canlısı.

CHANGELOG'daki kritik satırlar: 0.81.0 (2026-01-12) `Fixed: AV1 streams with invalid metadata did not get handled correctly`, `Fixed: VVC in MP4 without CTTS did not play properly`. 0.75.1 (2021-06-21) `Changed: AV1 files with no extradata will now generate a format header...` ve `Changed: AV1 hardware decoding will more reliably engage when provided with additional stream information from the demuxer`. AV1 DXVA2/D3D11 kod çözme 0.75.0'da (2021-03-30) gelmiş; 0.80.0 (2025-06-20) `Fixed: VP9 DXVA2/D3D11 decoding could result in artifacts on some clips`.

Çıkan kural: **konteynerdeki AV1 yapılandırma kaydı (`av1C` extradata) eksikse donanım kod çözme devreye girmiyor**, oynatıcı yazılım yoluna düşüyor ya da hiç açmıyor. Kesme veya remux (`-c copy`) sırasında bu kayıt kaybolabiliyor.

**Alınacak fikir.** Doğrulama kodlayıcıda bitmiyor: üretilen dosyanın konteyner başlığı `ffprobe` ile kontrol edilmeli (AV1 için extradata/`av1C`, profil ve seviye alanları dolu mu). Boş çıkan dosya "bizde çalışıyor, izleyicinin makinesinde açılmıyor" sınıfına girer. İkincisi, uyumluluk sırası koda gömülmeli: H.264/MP4 en geniş, HEVC/MP4 orta, AV1/MP4 ve AV1/MKV yeni oynatıcı istiyor — hedef boyut için AV1'e geçildiğinde kullanıcı bunu görmeli.

**Alınmayacak.** DirectShow'a bağlanmak veya LAV'ı kurulum bağımlılığı yapmak. GPL-2.0 VidShrink'in lisansıyla çakışır; alınan şey davranış, kod değil.

**VidShrink'te nereye dokunur.** Kodlama sonrası `ffprobe` doğrulama adımı ve arayüzdeki kodlayıcı seçim uyarısı. `CodecModel.cs`'e kodlayıcı başına "uyumluluk sınıfı" alanı eklenebilir.

## Ortak bulgu — sessiz hata

Yerelde doğrulandı: `-svtav1-params` içinde tanınmayan anahtar verildiğinde ffmpeg `Error parsing option psy-rd: 1.0.` satırını basıyor ama **çıkış kodu 0** kalıyor. Uydurma anahtar (`zzznotreal=1`) için de aynı. VidShrink kodlayıcı yeteneğini çıkış koduna bakarak ölçemez; stderr'de `Error parsing option` aranmalı. Bu, "sayıları ffmpeg'e sorarak doğrula" yönteminin doğrudan açığı ve `CodecModel.cs` ile `FfmpegArguments.cs` besleyen yeteneklik sondasını ilgilendiriyor.

## Kaynaklar

- `gh api repos/psy-ex/svt-av1-psy` (yönlendirme kaynağı `gianni-rosato/svt-av1-psy`), `/releases/latest`, `Docs/Parameters.md`, `README.md`
- `gh api repos/AOMediaCodec/SVT-AV1/contents/Docs/Parameters.md` — mainline, BSD-3-Clause-Clear, son push 2026-08-09
- `gh api repos/mpv-player/mpv`, `/releases/latest`, `DOCS/man/options.rst`; issue arama: #16683, #14027, #11264, #10854
- `gh api repos/Nevcairiel/LAVFilters`, `/releases/latest`, `CHANGELOG.txt`
- Yerel ölçüm: `ffmpeg -h encoder=libsvtav1`, `testsrc2` üzerinde `-svtav1-params` ve `-x265-params` denemeleri
