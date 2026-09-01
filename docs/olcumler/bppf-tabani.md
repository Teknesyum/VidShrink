# Bppf tabanı

Sözleşme: T99. Tarih: 2026-09-02. Kaynak: `.calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4`
(1920x1080, hevc, yuv420p10le, bt2020/smpte2084/bt2020nc, 60 fps, 1036,17 sn).

Bu dosya `CodecModel.FloorBppf` tabanının nereden geldiğini, eş boyutta yapılan
yerleşim taramasını ve tabanın ölçümden sonra nereye konduğunu kaydeder.

## 1. Tabanın kökeni

Aranan sabitler: `CodecModel.FloorBppf` (av1 0,020 · hevc 0,025 · diğer 0,035),
`CodecModel.HardwareFloorFactor` (1,25) ve tabanı içeriğe uyarlayan
`ComplexityProfile.FloorComplexityAnchor` / `FloorAdaptExponent` / `FloorAdaptMin,Max`.

Beşi de aynı commit'te girdi: `6fac0be` — 2026-08-25, "Measure what to keep when the
target is brutally small", T4 sözleşmesi. Commit mesajı sabitlerin ne yaptığını
anlatıyor, sayıların nereden geldiğini anlatmıyor. Sayılar kodda ilk kez burada
görünüyor ama metinde daha eskiler: `d2f1eb9` (2026-08-19) T4 sözleşmesini eklerken
onları **"başlangıç değerleri"** diye reçete ediyor.

| Sabit | Değer | Dayanak | Nerede |
|---|---:|---|---|
| `FloorBppf` diğer (h264) | 0,035 | **Dayanak bulunamadı — kabul.** T4 yapıcısının kendi beyanı: eğride diz yok, 0,035 ölçümle çelişmiyor ama ondan türetilmiş de değil, pürüzsüz bir eğri üzerinde seçilmiş politika çizgisi. | `contracts/done/T4.md:44-45`, `:204-207` |
| `FloorBppf` hevc | 0,025 | **Dayanak bulunamadı.** Hiç koşulmadı. | `contracts/done/T4.md:213` |
| `FloorBppf` av1 | 0,020 | **Dayanak bulunamadı.** Hiç koşulmadı. | `contracts/done/T4.md:213` |
| `HardwareFloorFactor` | 1,25 | **Dayanak bulunamadı.** Tek donanım koşusu yok; sözleşme yalnız NVENC derken kod QSV/AMF'ye de uyguluyor, denetim bunu kusur olarak yazmış. | `contracts/done/T4.md:45`, `:213`, `:298-299`; `docs/motor-dogrulama-raporu.md:228-229` |
| `FloorComplexityAnchor` | 0,1264 | **Ölçüm.** Tek klipte (gothic, 830,2 MB / 52,6 sn / 1920x1080@48) probun okuduğu bias düzeltilmiş referans bppf. Bağımsız olarak T7 ve T2c'de de aynı sayı. Sınırı: tek klip, ve hız moduna göre kayıyor. | `contracts/done/T4.md:172-173`; `T7.md:199-200`; `T2c.md:164` |
| `FloorAdaptExponent` | 0,5 | **Dayanak bulunamadı — seçim.** Yapıcı beyanı: karekök iki farklı içerikle ölçülmüş bir üs değil. | `contracts/done/T4.md:141-143`, `:208-209` |
| `FloorAdaptMin` / `Max` | 0,6 / 1,6 | **Dayanak bulunamadı.** Kodda yorum, sözleşmede gerekçe, `docs/` altında kayıt yok. | — |

T4'te gerçek bir ölçüm var ama tabanı seçmiyor: `libx264` ile 640x360@24'te bppf
taraması (0,010 → VMAF-NEG 21,4 · 0,035 → 47,3 · 0,090 → 74,7). Bu tablo 0,035
noktasındaki kaliteyi gösteriyor, 0,035'in neden sınır olduğunu değil. Ölçüm
`ExtremeCompressionTests.LiveQualityCurveShowsWhereTheCodecStopsCarryingThePicture`
ile üretiliyor ve hâlâ koşuyor.

`docs/olcumler/` altında bu sabitlere ait başka hiçbir dosya yok; bu dosya ilki.

Ayrıca döngüsellik: `ExtremeCompressionTests.CodecFloorsAreStatedPerCodecAndRaisedForHardware`
sabitleri birebir kopyalıyordu (`Assert.Equal(0.035, ...)`, `Assert.Equal(0.035 * 1.25, ...)`).
Bu bir doğrulama değil, sabitin ikinci kopyasıdır: sabiti bozan bir mutasyon testi de
bozar, ama davranışın bozulduğunu göstermez. Bölüm 6'ya bakınız.
