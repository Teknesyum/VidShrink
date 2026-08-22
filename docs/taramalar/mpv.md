Tema: psikogorsel ayar · kaynak: psikogorsel-ayar.md

# Psikogörsel kodlayıcı ayarı ve oynatıcı tarafı

Yerel doğrulama ortamı: ffmpeg 9.0-full (gyan.dev), SVT-AV1 lib v4.2.0-68-gc1e79b04f, x265 4.3+2. Tarih 2026-08-22.

## Depo 2 — mpv

**Ne yapıyor.** Komut satırı oynatıcı. 36.630 yıldız, 1.139 açık issue, son sürüm v0.41.0 (2025-12-21), son push 2026-08-21, lisans GitHub'da `NOASSERTION` (gövde LGPL/GPL karışımı).

Bizi ilgilendiren yer kod çözme tarafı: `--vd-lavc-film-grain=<auto|cpu|gpu>`. Belgeye göre film grain sentezi GPU'ya alınabiliyor ama **yalnız `gpu-next` VO'su destekliyor**; desteklemeyen VO'da ayar ne olursa olsun `cpu`ya düşüyor, yük izleyicinin CPU'suna biniyor. Donanım kod çözme varsayılan **kapalı** (`--hwdec=no`), gerekçesi belgede "kutudan çıktığı gibi güvenilirlik".

AV1 film grain oynatma tarafında tekrar tekrar kırılmış: #16683 `av1 film grain fails to render with d3d11 context` (2025-08-15, kapandı), #14027 `AV1 film grain with gpu-next causes SIGABRT` (2024-04-30, **hâlâ açık**), #11264 `deband=yes` ile çökme (kapandı), #10854 (kapandı). D3D11 Windows'un varsayılan yolu — VidShrink'in kullanıcısı tam oraya çıkıyor.

**Alınacak fikir.** Film grain sentezi "ücretsiz kalite" değil, **oynatma uyumluluğu riski**; VidShrink sunacaksa varsayılan kapalı ve açıldığında görüntü hatası uyarısı ile. İkincisi mpv'nin duruşu: varsayılan güvenli, hız isteyen açar — VidShrink'te de `*_nvenc/_qsv/_amf` hız seçeneğidir, kalite varsayılanı değil.

**Alınmayacak.** mpv'nin ayar yüzeyini taklit etmek. Yüzlerce bayrak ve karışık lisans; VidShrink'in oynatıcı tarafındaki tek sorusu "bu dosya oynar mı".

**VidShrink'te nereye dokunur.** `FfmpegArguments.cs` — `film-grain` eklenirse varsayılanı 0, arayüzde uyarı metni. Kodlayıcı seçim varsayılanı CPU kodlayıcı tarafında kalmalı.

## Ortak bulgu — sessiz hata

Yerelde doğrulandı: `-svtav1-params` içinde tanınmayan anahtar verildiğinde ffmpeg `Error parsing option psy-rd: 1.0.` satırını basıyor ama **çıkış kodu 0** kalıyor. Uydurma anahtar (`zzznotreal=1`) için de aynı. VidShrink kodlayıcı yeteneğini çıkış koduna bakarak ölçemez; stderr'de `Error parsing option` aranmalı. Bu, "sayıları ffmpeg'e sorarak doğrula" yönteminin doğrudan açığı ve `CodecModel.cs` ile `FfmpegArguments.cs` besleyen yeteneklik sondasını ilgilendiriyor.

## Kaynaklar

- `gh api repos/psy-ex/svt-av1-psy` (yönlendirme kaynağı `gianni-rosato/svt-av1-psy`), `/releases/latest`, `Docs/Parameters.md`, `README.md`
- `gh api repos/AOMediaCodec/SVT-AV1/contents/Docs/Parameters.md` — mainline, BSD-3-Clause-Clear, son push 2026-08-09
- `gh api repos/mpv-player/mpv`, `/releases/latest`, `DOCS/man/options.rst`; issue arama: #16683, #14027, #11264, #10854
- `gh api repos/Nevcairiel/LAVFilters`, `/releases/latest`, `CHANGELOG.txt`
- Yerel ölçüm: `ffmpeg -h encoder=libsvtav1`, `testsrc2` üzerinde `-svtav1-params` ve `-x265-params` denemeleri
