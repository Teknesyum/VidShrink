# HandBrake Açığı Madde 54 — Küçültmede Aralık Ölçümü

Tarih: 2026-09-18. Dal: `t0/hb-acik-kalan`. Karar kaynağı:
`docs/danisma/2026-09-18-fable-kucultmede-aralik.md` (S1–S6).

Ölçüm bu makinede, kısa ve sıralı koşumlarla alındı; ızgara yok, yapay yük yok.

## Kaynak

Sentetik `testsrc2` + `sine`, libx264 veryfast, 4000 kbit/sn hedef bit hızı.

| alan | değer |
| --- | --- |
| süre | 20,000 sn |
| boyut | 10 409 036 bayt (9,93 MB) |
| çözünürlük | 1280x720, 30 fps |

## Kesitin bütçeye girmesi

`--kes 8-14` (6 saniyelik pencere), iki hedefte.

| koşum | hedef | kip | çıktı süresi | çıktı boyutu | öngörülen kalite |
| --- | --- | --- | --- | --- | --- |
| kesit 6 sn | 3 MB | passthrough (akış kopyası) | 6,056 sn | 2 919 051 bayt | 100,0 (ölçüldü) |
| kesit 6 sn | 1 MB | iki geçiş | 6,000 sn | 1 036 237 bayt | 89,4 (ölçüldü) |
| tüm video 20 sn | 1 MB | iki geçiş | 20,000 sn | 1 026 519 bayt | 83,1 (ölçüldü) |

Kullanıcıya dokunan fark son iki satırda: **aynı 1 MB** bütçe, kesitte 6 saniyeye
harcanınca öngörülen kalite 83,1'den 89,4'e çıkıyor — 6,3 puan. Kesit bir kırpma
bayrağı değil, bütçenin girdisi (S2): rejim, oran ve bit hızı kesit süresinden
türüyor, bu yüzden 3 MB hedefinde plan passthrough'a düşüyor.

Süre farkı: iki geçişte pencere kare hassas (6,000 sn), akış kopyasında anahtar
kareye yuvarlanıyor (6,056 sn). S1'in melez araması bunun bilinen bedeli.

## Passthrough açığı

İlk koşum aralığı tamamen kaybetti: kesitli plan passthrough'a düşünce motor
kaynağı olduğu gibi kopyalıyordu.

| koşum | çıktı süresi | çıktı boyutu |
| --- | --- | --- |
| düzeltme öncesi (dosya kopyası) | 20,000 sn | 10 409 036 bayt |
| düzeltme sonrası (akış kopyası) | 6,056 sn | 2 919 051 bayt |

Düzeltme `FfmpegArguments.BuildTrimCopy`: aynı melez arama, `-c copy`,
`-map_chapters -1`. Pimi canlı ve ilişki ölçüyor — iki ayrı pencere iki ayrı süre
vermek zorunda.

## Mutasyon kanıtı

Her mutasyon commit'li ağaca uygulandı, kırmızı okundu, `git checkout` ile geri
alındı. Filtre `FullyQualifiedName~KucultmeAraligi` (31 ölçü).

| mutasyon | ne bozuluyor | kırmızı |
| --- | --- | --- |
| M1 | girdi sonrası `-ss` girdiden önce yazılıyor | 4 |
| M2 | `-t` yerine `-to EndSeconds` | 3 |
| M3 | kaynak künyesi kesit oranıyla ölçeklenmiyor | 5 |
| M4 | kesitte `-map_chapters 0` kalıyor | 1 |
| M5 | kesitli passthrough yine dosya kopyalıyor | 1 |

Geri alınmış temiz ağaç: 31/31 yeşil.

## Ölçülmeli kalan borç

- Melez aramanın 10 saniyelik kalanı (`TrimWindow.SeekLeadSeconds`) gerçek
  kaynakların I-kare aralığına göre ölçülmedi; fable S1'de bunu borç yazdı.
- VBR'de süre oranı sezgiselinin bedeli ölçülmedi (S2 borcu).
