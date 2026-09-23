# HDR10+ Taşıma — ffmpeg 9.0 Sarmalayıcıları

23 Eylül 2026, dal `t0/hdr10plus-tasima` (`main` f8a0d73d tabanlı). Hedef: HandBrake durum tablosu
satır 29 (`docs/handbrake/acik-durumu-2026-09-17.md:29`, önceki ölçüm `docs/olcumler/b4-hdr-dinamik.md`).

Araç: ffmpeg 9.0 (`9.0-full_build-www.gyan.dev`), libx265, libsvtav1 (SVT-AV1 v4.2.0-68). Kaynak sentetik,
b4'teki yolla üretildi; internetten dosya indirilmedi.

## Sonuç

| Kol | HDR10+ (ilk 3 kare) | Mastering (ilk 3 kare) |
| --- | --- | --- |
| kaynak (x265 + `dhdr10-info`) | 3/3 | 3/3 |
| libx265, VidShrink'in bugünkü `-x265-params` biçimi | **0/3** | 3/3 |
| libx265 + `-udu_sei 1` | **0/3** | 3/3 |
| libsvtav1 | **0/3** | 3/3 |
| libx265 + `dhdr10-info=<json>` | 3/3 | 3/3 |

**Hüküm: `HdrResolver` içinde bir bayrakla taşıma mümkün değil.** ffmpeg 9.0'ın ne libx265 ne libsvtav1
sarmalayıcısı `AV_FRAME_DATA_DYNAMIC_HDR_PLUS` yan verisini okuyor. Bunu açan bir seçenek de yok.
`HdrDynamicMetadataDropped` gerekçesi doğru kalıyor. Kod değişmedi.

x265'te tek yol kodlayıcının kendi `dhdr10-info` JSON'u. Kareler JSON'a kaynaktan çıkarılıp yazılmalı
(aşağıda "Açık Yol"). SVT-AV1 için ffmpeg üzerinden hiçbir yol yok.

## Kaynak Kodda Kanıt

FFmpeg `n9.0` etiketi (`5b9252abfe2fda0ccb71ed45cd7a954cc45d2b6f`), GitHub aynası. Dosyada
`HDR_PLUS`, `hdr10`, `2094`, `dynamic_hdr` araması sıfır satır döndü.

`libavcodec/libx265.c`:

- 229, 233: `handle_side_data` yalnız `AV_FRAME_DATA_CONTENT_LIGHT_LEVEL` ve
  `AV_FRAME_DATA_MASTERING_DISPLAY_METADATA` okuyor. Statik HDR10 bu yüzden geçiyor.
- 816-845: kare yan verisinden x265 `userSEI`'ye giden tek döngü `if (ctx->udu_sei)` altında. 822. satır
  `AV_FRAME_DATA_SEI_UNREGISTERED` dışındaki her türü atlıyor, 842. satır yük tipini
  `SEI_TYPE_USER_DATA_UNREGISTERED` olarak sabitliyor. HDR10+ kayıtlı T.35 SEI'dir, bu döngüden geçemez.
- 848: `AV_FRAME_DATA_DOVI_METADATA`, yalnız DV RPU'su.

`libavcodec/libsvtav1.c`:

- 192, 196: `handle_side_data` yalnız CLL ve mastering.
- 598-606: kareden kodlayıcıya giden tek metadata `AV_FRAME_DATA_DOVI_METADATA` →
  `EB_AV1_METADATA_TYPE_ITUT_T35`. HDR10+ için eşi yok.

Okuma komutu (dosya diske yazılmadı):

```
gh api "repos/FFmpeg/FFmpeg/contents/libavcodec/libx265.c?ref=n9.0" -H "Accept: application/vnd.github.raw" | Select-String -Pattern 'HDR_PLUS|hdr10|2094|dynamic_hdr|MASTERING_DISPLAY|CONTENT_LIGHT|DOVI_METADATA|SEI_UNREGISTERED|itut_t35|userSEI'
229: avctx->nb_decoded_side_data, AV_FRAME_DATA_CONTENT_LIGHT_LEVEL);
233: AV_FRAME_DATA_MASTERING_DISPLAY_METADATA);
703: x265_sei *sei = &pic->userSEI;
741: sei = &x265pic.userSEI;
822: if (side_data->type != AV_FRAME_DATA_SEI_UNREGISTERED)
848: sd = av_frame_get_side_data(pic, AV_FRAME_DATA_DOVI_METADATA);
860: "without AV_FRAME_DATA_DOVI_METADATA");
```

Yerel ikilide seçenekler (`ffmpeg -h encoder=libx265` / `libsvtav1`): HDR ile ilgili yalnız `-dolbyvision`,
`-udu_sei`, `-a53cc`, `-x265-params`, `-svtav1-params`. HDR10+ seçeneği yok.

## Ölçüm

Tek iş parçacığına yakın ve kısa: 0,5 sn, 12 kare, 320x180, `-threads 2`, `pools=2:frame-threads=1`,
`lp=2`. Çalışma klasörü `.calisma/t0-hdr10plus-tasima/`. `hdr10plus.json`, b4'teki JSON'un 12 sahnelik
kısaltması (`HDR10plusProfile B`, sahne başına `LuminanceParameters`/`BezierCurveData`).

Kaynak:

```
ffmpeg -hide_banner -v error -y -f lavfi -i testsrc2=size=320x180:rate=24:duration=0.5 \
  -vf "format=yuv420p10le,setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc:range=tv" \
  -c:v libx265 -b:v 3M -x265-params "hdr10=1:hdr10-opt=1:master-display=G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1):max-cll=1000,400:pools=2:frame-threads=1:log-level=error:dhdr10-info=hdr10plus.json" \
  kaynak.mkv
```

Kollar:

```
ffmpeg -hide_banner -v error -y -i kaynak.mkv -threads 2 -c:v libx265 -b:v 1M -x265-params "hdr10-opt=1:pools=2:frame-threads=1:log-level=error" a-x265.mkv
ffmpeg -hide_banner -v error -y -i kaynak.mkv -threads 2 -c:v libx265 -b:v 1M -udu_sei 1 -x265-params "hdr10-opt=1:pools=2:frame-threads=1:log-level=error" b-x265-udu.mkv
ffmpeg -hide_banner -v error -y -i kaynak.mkv -threads 2 -c:v libsvtav1 -b:v 1M -svtav1-params lp=2 c-av1.mkv
ffmpeg -hide_banner -v error -y -i kaynak.mkv -threads 2 -c:v libx265 -b:v 1M -x265-params "hdr10-opt=1:pools=2:frame-threads=1:log-level=error:dhdr10-info=hdr10plus.json" d-x265-json.mkv
```

Dördü de çıkış kodu 0. Sayım, her dosya için:

```
ffprobe -hide_banner -v error -select_streams v:0 -read_intervals "%+#3" -show_frames -show_entries frame_side_data=side_data_type -of compact <dosya>
```

Çıktıda `SMPTE2094-40` ve `Mastering display` geçişleri sayıldı:

```
kaynak.mkv       hdr10+/3=3 mdm/3=3
a-x265.mkv       hdr10+/3=0 mdm/3=3
b-x265-udu.mkv   hdr10+/3=0 mdm/3=3
c-av1.mkv        hdr10+/3=0 mdm/3=3
d-x265-json.mkv  hdr10+/3=3 mdm/3=3
```

Kaynağın ilk karesi (`-read_intervals %+#1`, compact):
`side_datum/hdr_dynamic_metadata_smpte2094_40__hdr10__:side_data_type=HDR Dynamic Metadata SMPTE2094-40 (HDR10+)`.

## Açık Yol: x265 İçin JSON Köprüsü

d kolu x265'in HDR10+'yı JSON'dan yazdığını gösteriyor. ffprobe `-show_frames -show_entries frame_side_data
-of json` kare başına bütün alanları veriyor: `targeted_system_display_maximum_luminance`, üç `maxscl`,
`average_maxrgb`, dokuz `distribution_maxrgb_percentage`/`percentile` çifti, `knee_point_x/y`, dokuz
`bezier_curve_anchors`. JSON'da anahtarlar tekrarlanıyor (`maxscl` üç kez), ayrıştırıcı özellikleri sırayla
gezmeli. Bu köprü kurulursa x265 kolu taşıyabilir. Ölçülmedi: ffprobe'dan çıkarılıp yazılan JSON'un
kaynaktaki değerleri bire bir geri vermesi (d kolu kaynağın üretildiği JSON'u kullandı).

Köprünün maliyeti ve açık kararları:

- Kaynağın **bütün karelerini** çözen ek bir ffprobe geçişi. Uzun 4K kaynakta dakikalar sürer.
- Kesitte (`-ss`/`-t`) ve fps değişiminde JSON kare dizini çıktıya göre yeniden hizalanmalı.
- `-x265-params` içinde Windows yolu `:` yüzünden bozuluyor (b4). Göreli yol ve çalışma klasörü gerekir.
- İki geçişli kodlamada JSON yalnız ikinci geçişe girer mi, ölçülmedi.
- SVT-AV1 kolu bu yolla da taşımaz, not düşmeye devam eder.

Beş dosyadan fazlasına dokunur (`FfprobeClient`, `MediaInfo`, `HdrResolver`, `FfmpegArguments`, kodlama
koşucusu, testler). Ek geçişin maliyetine değer mi, karar kullanıcının.
