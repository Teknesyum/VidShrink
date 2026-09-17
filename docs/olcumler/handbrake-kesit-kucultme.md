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

`--kes 8-14` (6 saniyelik pencere), iki hedefte. Ham çıktının tamamı
`docs/olcumler/handbrake-kesit-kucultme-ham.txt` içinde; aşağıdakiler o dosyadan
kesilmiş, kısaltılmamış parçalar.

| koşum | hedef | kip | kodlayıcı | çıktı süresi | çıktı boyutu | öngörülen kalite |
| --- | --- | --- | --- | --- | --- | --- |
| kesit 6 sn | 3 MB | passthrough (akış kopyası) | kopya (h264 dokunulmadı) | 6,056 sn | 2 919 051 bayt | 100,0 |
| kesit 6 sn | 1 MB | iki geçiş | `libx264 -preset slow -b:v 1239k` | 6,000 sn | 1 030 069 bayt | 89,38 |
| tüm video 20 sn | 1 MB | iki geçiş | `libsvtav1 -preset 6 -b:v 329k` | 20,000 sn | 1 026 857 bayt | 83,07 |

Kullanıcıya dokunan fark son iki satırda: **aynı 1 MB** bütçe, kesitte 6 saniyeye
harcanınca öngörülen kalite 83,07'den 89,38'e çıkıyor — 6,3 puan.

**Kodlayıcı iki satırda aynı değil ve bu farkın bir yan ürünü değil, sonucu.**
Kesit süresi 20 saniyeden 6'ya indiği için saniye başına düşen bütçe 329k'dan
1239k'ya çıkıyor; motor bu bit hızında AV1'in yavaşlığına katlanmak yerine
`libx264 -preset slow`'u seçiyor (S2: rejim de, kodlayıcı da kesit süresinden
türer). 6,3 puanın tamamı bütçeye yazılamaz — bir kısmı bütçenin açtığı kodlayıcı
seçiminden gelir; ikisi aynı kararın iki yüzü.

Süre farkı: iki geçişte pencere kare hassas (6,000 sn), akış kopyasında anahtar
kareye yuvarlanıyor (6,056 sn). S1'in melez araması bunun bilinen bedeli.

### Kaynak ve kesitli koşumların ham çıktısı

```
$ ffprobe -v error -show_entries format=duration,size -of default=noprint_wrappers=1 kaynak.mp4
duration=20.000000
size=10409036
```

3 MB hedefi, kesit passthrough'a düşüyor:

```
$ vidshrink kucult kaynak.mp4 --hedef 3MB --kes 8-14 --cikti kesit3.mp4 --json
  "plan": {
    "codec": "h264",
    "mode": "passthrough",
    "videoBitrateK": 4036,
    "preset": "copy",
  "estimate": {
    "expectedMb": 2.978,
    "predictedQuality": 100,
    "basis": "measured"

$ ffprobe ... kesit3.mp4
codec_name=h264
duration=6.055782
size=2919051
```

1 MB hedefi, kesitli:

```
$ vidshrink kucult kaynak.mp4 --hedef 1MB --kes 8-14 --cikti kesit1.mp4 --json
  "plan": {
    "codec": "libx264",
    "mode": "2pass",
    "videoBitrateK": 1239,
    "preset": "slow",
    "audioBitrateK": 96,
  "estimate": {
    "expectedMb": 0.96,
    "predictedQuality": 89.38,
    "basis": "measured"

$ ffprobe ... kesit1.mp4
codec_name=h264
duration=6.000000
size=1030069
```

1 MB hedefi, tüm video:

```
$ vidshrink kucult kaynak.mp4 --hedef 1MB --cikti tam1.mp4 --json
  "plan": {
    "codec": "libsvtav1",
    "mode": "2pass",
    "videoBitrateK": 329,
    "preset": "6",
    "audioBitrateK": 72,
  "estimate": {
    "expectedMb": 0.961,
    "predictedQuality": 83.07,
    "basis": "measured"

$ ffprobe ... tam1.mp4
codec_name=av1
duration=20.000000
size=1026857
```

Kesitli koşumun ffmpeg komutunda melez arama ve bölüm işaretlerinin düşmesi
görünüyor; tüm video koşumunda ikisi de yok:

```
kesit:     ... -i kaynak.mp4 -ss 8 -t 6 -c:v libx264 -preset slow -b:v 1239k ... -map_chapters -1 ...
tüm video: ... -i kaynak.mp4 -c:v libsvtav1 -preset 6 -b:v 329k ... -map_chapters 0 ...
```

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
alındı. İlk beşinin filtresi `FullyQualifiedName~KucultmeAraligi` (31 ölçü);
denetim sonrası eklenen D1–D4 dokunulan alanın tamamında koşuldu (153 ölçü).

| mutasyon | ne bozuluyor | kırmızı |
| --- | --- | --- |
| M1 | girdi sonrası `-ss` girdiden önce yazılıyor | 4 |
| M2 | `-t` yerine `-to EndSeconds` | 3 |
| M3 | kaynak künyesi kesit oranıyla ölçeklenmiyor | 5 |
| M4 | kesitte `-map_chapters 0` kalıyor | 1 |
| M5 | kesitli passthrough yine dosya kopyalıyor | 1 |
| D1 | `TrimWindow.SeekLeadSeconds` 10,0 → 5,0 | 5 |
| D2 | `OvershootTrim.Offered`'daki `plan.Trim is null` koruması kalkıyor | 2 |
| D3 | `EncodePlan.EffectiveDurationSeconds` kesiti yok sayıyor | 3 |
| D4 | `TrimWindow.Of`'un kaynak sonuna kırpması kalkıyor | 2 |

Geri alınmış temiz ağaç: 153/153 yeşil. D1–D4 denetimin sağ kalan mutasyonlarıydı;
ilk üçü totolojik ya da pimsiz bırakılmış davranışları açığa çıkardı.

## Ölçülmeli kalan borç

- Melez aramanın 10 saniyelik kalanı (`TrimWindow.SeekLeadSeconds`) gerçek
  kaynakların I-kare aralığına göre ölçülmedi; fable S1'de bunu borç yazdı.
- VBR'de süre oranı sezgiselinin bedeli ölçülmedi (S2 borcu).
