# NVENC Lookahead Kolunun Mutasyon Dökümü

Ölçüm `docs/olcumler/nvenc-kalite-kollari.md`, danışma
`docs/danisma/2026-09-19-fable-nvenc-kalite-kollari.md`. Pim
`tests/VidShrink.Tests/NvencLookaheadTests.cs`, koşucu `tools/nvenc-2/mutasyon.py`.

Filtre `NvencLookaheadTests|FfmpegArgumentsTests`. Taban **0 kırmızı / 85 yeşil**;
her kesimden sonra dosya bellekteki özgün baytlarla geri kondu (`git checkout` yok).

| # | kesim | kırmızı |
|---|---|---|
| M1 | satıcı kapısı düşürüldü — her kodlayıcı lookahead alıyor | 4 |
| M2 | kabul yoklaması düşürüldü — koşulsuz yazılıyor | 9 |
| M3 | seviye 3 yerine 1 yazılıyor | 5 |
| M4 | kol hiç yazılmıyor | 5 |
| M5 | `-rc-lookahead` penceresi 20 yerine 40 | 4 |
| M6 | belgedeki kabul cümlesi "alınmadı"ya çevrildi | 1 |

**6/6 kırmızı.** Kesimden sonra taban yine 0/85.

M6 kodu değil belgeyi kesiyor: `YazilanSeviyeOlcumBelgesindekiKazanan` ürünün yazdığı
seviyeyi ölçüm belgesinden okuyor. Kod ile belge ayrışırsa — seviye elle değiştirilse ya da
belge ölçümü geri alsa — ölçü kırmızı olur. Sayı testte elle yazılı değil.

M2 en geniş kesim (9 kırmızı): yoklama kapısı düşünce kol sürücünün reddettiği kodeklerde de
yazılıyor. Kapının kendisi `-tf_level`in aynı turda düşmesiyle gerekçelendi — listelenen
bayrak kabul edilen bayrak değil.
