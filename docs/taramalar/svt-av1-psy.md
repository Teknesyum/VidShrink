Tema: psikogorsel ayar · kaynak: psikogorsel-ayar.md

# Psikogörsel kodlayıcı ayarı ve oynatıcı tarafı

Yerel doğrulama ortamı: ffmpeg 9.0-full (gyan.dev), SVT-AV1 lib v4.2.0-68-gc1e79b04f, x265 4.3+2. Tarih 2026-08-22.

## Depo 1 — svt-av1-psy

**Ne yapıyor.** SVT-AV1'in psikogörsel çatalı. `gianni-rosato/svt-av1-psy` bugün `psy-ex/svt-av1-psy`'ye yönleniyor; depo **arşivlenmiş**, son push 2026-02-12, son sürüm v3.0.2 (2025-04-20), 398 yıldız, 0 açık issue, BSD-3-Clause-Clear. README projeyi bitmiş ilan ediyor, devamı olarak `juliobbv-p/svt-av1-hdr`'yi gösteriyor. Fikirler mainline SVT-AV1'e taşınmış.

Çatalın bayrakları (Docs/Parameters.md): `--psy-rd [0.0-6.0]`, `--spy-rd [0-2]`, `--noise-norm-strength [0-4]`, `--enable-variance-boost` (vars. 1), `--variance-boost-strength [1-4]` (vars. 2), `--qp-scale-compress-strength [0-3]` (vars. 1), `--sharpness [-7..7]`, `--luminance-qp-bias [0-100]`, `--tf-strength [0-4]`, `--kf-tf-strength [0-4]`, `--tune [0-4]` vars. 2.

Mainline'da karşılıkları farklı. `--psy-rd` **yok**; yerine `--ac-bias [0.0-8.0]` vars. 0.0 — SATD−SAD enerji farkına dayanan, x264/x265 psy-rd muadili. `--enable-variance-boost` mainline'da vars. **0**, çatalda 1. `--qp-scale-compress-strength` mainline vars. **0**, çatalda 1. `--tune` mainline'da **[0-5]** vars. 1 (0=VQ, 1=PSNR, 2=SSIM, 3=IQ yalnız durağan görüntü, 4=MS-SSIM, 5=VMAF); çatalda 3=Subjective SSIM, 4=Still Picture. **Aynı sayı iki projede farklı şey demek.**

Yerelde ölçülenler: `tune=5` çalışırken `Tune VMAF: a pre-processing / unsharp masking is applied` basıyor — metrik için kaynağı değiştiriyor. `preset 12` ve `preset 13` ikisi de `Preset M13 is mapped to M11` ile M11'e katlanıyor, üstüne `Non-RTC M10+ are meant for automation tooling usage. Visual artifacts may occur otherwise.` uyarısı geliyor.

**Alınacak fikir.** Bit başına kaliteyi gerçekten artıran üçlü: `enable-variance-boost=1:variance-boost-strength=2..3` (blok bazlı AQ), `ac-bias=1.0..1.5` (doku ve tane tutma), `qp-scale-compress-strength=1..2` (mini-GOP içi kalite dalgalanmasını kısar). Üçü de hedef boyutu değiştirmez, aynı bit bütçesini yeniden dağıtır — CRF arama döngüsüne dokunmadan takılabilir. Karanlık sahne için ayrıca `luminance-qp-bias=20..40`. Üçüncüsü: preset tablosu dokümandan değil ffmpeg'den alınmalı, çünkü 12 ve 13 sahte seçenek.

**Alınmayacak.** `--psy-rd`, `--spy-rd`, `--noise-norm-strength` — mainline'da yok, ffmpeg `Error parsing option psy-rd: 1.0.` diyor; kullanıcıya psy build kurdurmak VidShrink'in kapsamı değil. `tune=5` (VMAF) unsharp mask uyguladığı için ölçüyü kandırıyor, varsayılan olamaz. Arşivlenmiş çatala bağımlılık kurmak — 2025-04-20'den beri sürüm yok. `--film-grain` varsayılan açık gelmemeli; gerekçe Depo 2 ve 3'te.

**VidShrink'te nereye dokunur.** `FfmpegArguments.cs` preset tablosu (`libsvtav1` satırından 12 ve 13 elenmeli) ve `-svtav1-params` dizesi. `CodecModel.cs` içindeki `RelativeBitrateNeed["libsvtav1"] = 0.55` — variance-boost ve ac-bias açıldığında aynı CRF'te dosya büyür, katsayı yeniden ölçülmeli.

## Ortak bulgu — sessiz hata

Yerelde doğrulandı: `-svtav1-params` içinde tanınmayan anahtar verildiğinde ffmpeg `Error parsing option psy-rd: 1.0.` satırını basıyor ama **çıkış kodu 0** kalıyor. Uydurma anahtar (`zzznotreal=1`) için de aynı. VidShrink kodlayıcı yeteneğini çıkış koduna bakarak ölçemez; stderr'de `Error parsing option` aranmalı. Bu, "sayıları ffmpeg'e sorarak doğrula" yönteminin doğrudan açığı ve `CodecModel.cs` ile `FfmpegArguments.cs` besleyen yeteneklik sondasını ilgilendiriyor.

## Kaynaklar

- `gh api repos/psy-ex/svt-av1-psy` (yönlendirme kaynağı `gianni-rosato/svt-av1-psy`), `/releases/latest`, `Docs/Parameters.md`, `README.md`
- `gh api repos/AOMediaCodec/SVT-AV1/contents/Docs/Parameters.md` — mainline, BSD-3-Clause-Clear, son push 2026-08-09
- `gh api repos/mpv-player/mpv`, `/releases/latest`, `DOCS/man/options.rst`; issue arama: #16683, #14027, #11264, #10854
- `gh api repos/Nevcairiel/LAVFilters`, `/releases/latest`, `CHANGELOG.txt`
- Yerel ölçüm: `ffmpeg -h encoder=libsvtav1`, `testsrc2` üzerinde `-svtav1-params` ve `-x265-params` denemeleri
