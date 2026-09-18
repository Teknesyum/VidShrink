# HandBrake Açık Envanterinin Tamlığı — 18 Eylül 2026

Envanterin kendisi denetlendi: `docs/handbrake/acik-analizi.md` ve
`docs/handbrake/acik-durumu-2026-09-17.md`, HandBrakeCLI'nin ham bayrak listesiyle
hiç karşılaştırılmamıştı. `docs/handbrake/README.md` bu borcu zaten itiraf ediyordu.

## Kaynak

`HandBrakeCLI.exe --version` → **HandBrake 1.11.2**, ölçümlerin koştuğu ikilinin
ta kendisi. `--help` çıktısı 737 satır; uzun bayraklar `--[a-zA-Z0-9-]+` deseniyle
çıkarılıp tekilleştirildi. Ağa çıkılmadı; web dokümanı sürümden bağımsız ve
derleme zamanı özelliklerini (nvenc 13.1 var, VCE ve QSV yok) göstermiyor.

## Sayı

| Karşılaştırılan | Ham bayrak | Envanterde geçen | Eksik |
|---|---|---|---|
| Açık envanteri (`docs/handbrake/*.md`) | 159 | 5 | **154** |
| Ham döküm (`docs/olcumler/handbrake-cli-yetenek.md`) | 159 | 159 | 0 |

Envanterde literal geçen beş bayrak: `--all-audio`, `--audio-copy-mask`,
`--audio-lang-list`, `--help`, `--json`.

Sayı olduğu gibi okunmamalı: envanter konuları Türkçe kavram adıyla anıyor —
kırpma (20 geçiş), altyazı (27), ön ayar (25), turbo (14), gürültü giderme (19),
taramasızlaştırma (12), keskinleştirme (10), döndürme/dolgu (14), meta veri (10).
Kavram düzeyinde dokunulan bayrak sayısı kabaca 60-70; **hiçbir düzeyde geçmeyen
yaklaşık 85 bayrak** gerçek boşluktur.

## Envanterde adı hiç geçmeyen 154 bayrak

- `--ab`
- `--ac`
- `--adither`
- `--aencoder`
- `--align-av`
- `--all-subtitles`
- `--aname`
- `--angle`
- `--aq`
- `--arate`
- `--audio`
- `--audio-fallback`
- `--auto-anamorphic`
- `--automatic-naming-behaviour`
- `--bwdif`
- `--cfr`
- `--chapters`
- `--chroma-smooth`
- `--chroma-smooth-tune`
- `--color-matrix`
- `--color-range`
- `--colorspace`
- `--comb-detect`
- `--crop`
- `--crop-mode`
- `--crop-threshold-frames`
- `--crop-threshold-pixels`
- `--custom-anamorphic`
- `--deblock`
- `--deblock-tune`
- `--decomb`
- `--deinterlace`
- `--denoise`
- `--detelecine`
- `--disable-hw-decoding`
- `--display-width`
- `--drc`
- `--enable-hw-decoding`
- `--encoder`
- `--encoder-level`
- `--encoder-level-list`
- `--encoder-preset`
- `--encoder-preset-list`
- `--encoder-profile`
- `--encoder-profile-list`
- `--encoder-tune`
- `--encoder-tune-list`
- `--encopts`
- `--first-audio`
- `--first-subtitle`
- `--format`
- `--gain`
- `--grayscale`
- `--hdr-dynamic-metadata`
- `--height`
- `--hqdn3d`
- `--inline-parameter-sets`
- `--input`
- `--ipod-atom`
- `--itu-par`
- `--keep-aname`
- `--keep-display-aspect`
- `--keep-duplicate-titles`
- `--keep-metadata`
- `--keep-subname`
- `--lapsharp`
- `--lapsharp-tune`
- `--loose-anamorphic`
- `--main-feature`
- `--markers`
- `--max-duration`
- `--maxHeight`
- `--maxWidth`
- `--min-duration`
- `--mixdown`
- `--modulus`
- `--multi-pass`
- `--native-dub`
- `--native-language`
- `--nlmeans`
- `--nlmeans-tune`
- `--no-bwdif`
- `--no-chroma-smooth`
- `--no-comb-detect`
- `--no-deblock`
- `--no-decomb`
- `--no-deinterlace`
- `--no-detelecine`
- `--no-dvdnav`
- `--no-grayscale`
- `--no-hdr-dynamic-metadata`
- `--no-hqdn3d`
- `--no-ipod-atom`
- `--no-itu-par`
- `--no-keep-aname`
- `--no-keep-display-aspect`
- `--no-keep-subname`
- `--no-lapsharp`
- `--no-markers`
- `--no-metadata`
- `--no-multi-pass`
- `--no-nlmeans`
- `--no-optimize`
- `--no-turbo`
- `--no-unsharp`
- `--non-anamorphic`
- `--normalize-mix`
- `--optimize`
- `--output`
- `--pad`
- `--pfr`
- `--pixel-aspect`
- `--preset`
- `--preset-export`
- `--preset-export-description`
- `--preset-export-file`
- `--preset-import-file`
- `--preset-import-gui`
- `--preset-list`
- `--previews`
- `--quality`
- `--queue-import-file`
- `--rate`
- `--rotate`
- `--scan`
- `--srt-burn`
- `--srt-codeset`
- `--srt-default`
- `--srt-file`
- `--srt-lang`
- `--srt-offset`
- `--ssa-burn`
- `--ssa-default`
- `--ssa-file`
- `--ssa-lang`
- `--ssa-offset`
- `--start-at`
- `--start-at-preview`
- `--stop-at`
- `--subname`
- `--subtitle`
- `--subtitle-burned`
- `--subtitle-default`
- `--subtitle-forced`
- `--subtitle-lang-list`
- `--title`
- `--turbo`
- `--unsharp`
- `--unsharp-tune`
- `--vb`
- `--verbose`
- `--version`
- `--vfr`
- `--width`
