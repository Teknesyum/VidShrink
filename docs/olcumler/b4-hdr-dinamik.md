# B4 — HDR10+ ve Dolby Vision 8.1 Dinamik Meta Verisi

23 Eylül 2026, dal `worktree-agent-aed4bbe3bd7f2aa1e`. Karar `docs/handbrake/handbrake-yanit.md:114-116`:
DV profil 8.1 kapsamda, profil 5 dışarıda. Kapanan satır `docs/handbrake/bayrak-hukumleri-2026-09-19.md`
(`--hdr-dynamic-metadata`).

Araç: ffmpeg 9.0 (gyan.dev, CI ile aynı yapı), libx265 ve libsvtav1. Kaynaklar sentetik, 2 sn 320x180;
internetten örnek indirilmedi.

## Sonuç

| Kaynak | Kol | Önce | Sonra |
| --- | --- | --- | --- |
| HDR10+ | x265 mkv / mp4 | HDR10+ 0/3, mastering 3/3 | HDR10+ 0/3, mastering 3/3, gerekçede `HdrDynamicMetadataDropped` |
| HDR10+ | SVT-AV1 mkv / mp4 | HDR10+ 0/3, mastering 3/3 | HDR10+ 0/3, mastering 3/3, gerekçede `HdrDynamicMetadataDropped` |
| DV 8.1 | x265 mkv | dvcC yok, RPU 0/3 | **dvcC 8.1, RPU 3/3** |
| DV 8.1 | x265 mp4 | dvcC yok, RPU 0/3 | **dvcC 8.1, RPU 3/3** |
| DV 8.1 | SVT-AV1 mkv | dvcC yok, T.35 0 | **dvcC 10.1, T.35 48** |
| DV 8.1 | SVT-AV1 mp4 | dvcC yok, T.35 0 | **dvcC 10.1, T.35 48** |

"Önce" `main` üstündeki VidShrink CLI'nın, "sonra" bu dalın CLI'sının çıktısı. İkisi de aynı komutla
çalıştırıldı (`kucult --hedef 0.05 --olcumsuz`, gerçek iki geçiş). HDR10+ hâlâ taşınmıyor ama artık
kullanıcıya söyleniyor. DV 8.1 dört kolda da taşınıyor.

**Veri nerede kayboluyordu?** ffmpeg'in libx265 ve libsvtav1 sarmalayıcıları mastering ve CLL yan
verisini kareden kodlayıcıya kendisi geçiriyor, HDR10+ (SMPTE 2094-40) yan verisini geçirmiyor. DV RPU'su
için `-dolbyvision` seçeneği var (varsayılanı `auto`), ama `auto` yalnız karede DOVI yan verisi **ve**
akışta yapılandırma kaydı olunca devreye giriyor. VidShrink bu bayrağı hiç yazmıyordu, bu yüzden RPU düşüyordu.

## Kaynakların Üretimi

HDR10+ kaynağı: x265'in `dhdr10-info`'suna 48 sahnelik bir JSON verildi (`HDR10plusProfile B`, sahne başına
`LuminanceParameters`/`BezierCurveData`). x265-params içinde Windows yolu `:` yüzünden bozuluyor, bu yüzden
göreli yol ve çalışma klasörü kullanıldı.

```
ffmpeg -hide_banner -y -f lavfi -i testsrc2=size=320x180:rate=24:duration=2 \
  -vf format=yuv420p10le,setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc:range=tv \
  -c:v libx265 -b:v 3M -x265-params "hdr10=1:hdr10-opt=1:master-display=G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1):max-cll=1000,400:pools=2:frame-threads=1:dhdr10-info=hdr10plus.json" \
  kaynak-hdr10plus.mkv
```

```
ffprobe -select_streams v:0 -read_intervals %+#1 -show_frames -show_entries frame_side_data=side_data_type -of compact kaynak-hdr10plus.mkv
frame|...|side_datum/mastering_display_metadata:side_data_type=Mastering display metadata|side_datum/content_light_level_metadata:side_data_type=Content light level metadata|side_datum/hdr_dynamic_metadata_smpte2094_40__hdr10__:side_data_type=HDR Dynamic Metadata SMPTE2094-40 (HDR10+)
```

**DV 8.1 kaynağı.** RPU'yu üreten açık araç (`dovi_tool`) indirilmedi, çünkü internetten dosya indirmek
yasaktı. RPU bit düzeyinde elle yazıldı:

- `rpu_type` 2, `rpu_format` 18, `vdr_rpu_profile` 1.
- Artık katman yok, özdeşlik polinomu (üç bileşen, pivot 0-1023, katsayılar 0 ve 1).
- DM seviye 0: BT.2020 matrisleri, `source_max_pq` 3079, `source_min_pq` 62.
- CRC32/MPEG-2 ve `0x80` sonlandırıcı.
- Önleme baytı kaçışı.

Her VCL NAL'ın arkasına NAL tipi 62 (`7C 01`) olarak eklendi. İlk karede `scene_refresh_flag` 1.
ffmpeg'in HEVC çözücüsü bunu hatasız ayrıştırıp kareye `Dolby Vision RPU Data` + `Dolby Vision Metadata`
yan verisi olarak koyuyor. Bu, RPU'nun geçerli olduğunun kanıtı. Test içindeki C# kopyası `DvRpu`
(`tests/VidShrink.Tests/HdrDinamikTests.cs`).

```
ffmpeg ... -c:v libx265 -x265-params "hdr10=1:hdr10-opt=1:master-display=...:max-cll=1000,400" -f hevc ham.hevc
(RPU ekle -> ham-rpu.hevc)
ffmpeg -r 24 -i ham-rpu.hevc -c:v libx265 -b:v 3M -maxrate 3M -bufsize 6M -pix_fmt yuv420p10le -dolbyvision 1 kaynak-dv.mkv
```

`kaynak-dv.mkv`: dvcC 8.1, RPU 3/3, mastering 3/3. Yeniden kodlamada x265 iki şart koşuyor:
`Dolby Vision profile - 8.1 requires Mastering display color volume information` (ham akışta
master-display olmadan kodlayıcı açılmıyor) ve VBV (maxrate/bufsize).

## Ham Ölçüm

Satır başına: dvcC `profil.uyum`, ilk üç karede RPU / HDR10+ / mastering yan verisi, AV1 dosyasında DV T.35
başlığının (`B5 00 3B 00 00 08 00`) bayt sayımı. libdav1d AV1'deki DV T.35'i kare yan verisi olarak vermiyor,
bu yüzden AV1'de RPU bayt taramasıyla sayıldı.

```
kaynak-hdr10plus.mkv   dvcC=. rpu/3=0 hdr10+/3=3 mdm/3=3 t35=0
kaynak-dv.mkv          dvcC=8.1 rpu/3=3 hdr10+/3=0 mdm/3=3 t35=0
once-h10-x265.mkv      dvcC=. rpu/3=0 hdr10+/3=0 mdm/3=3 t35=0
once-h10-x265.mp4      dvcC=. rpu/3=0 hdr10+/3=0 mdm/3=3 t35=0
once-h10-av1.mkv       dvcC=. rpu/3=0 hdr10+/3=0 mdm/3=3 t35=0
once-h10-av1.mp4       dvcC=. rpu/3=0 hdr10+/3=0 mdm/3=3 t35=0
once-dv-x265.mkv       dvcC=. rpu/3=0 hdr10+/3=0 mdm/3=3 t35=0
once-dv-x265.mp4       dvcC=. rpu/3=0 hdr10+/3=0 mdm/3=3 t35=0
once-dv-av1.mkv        dvcC=. rpu/3=0 hdr10+/3=0 mdm/3=3 t35=0
once-dv-av1.mp4        dvcC=. rpu/3=0 hdr10+/3=0 mdm/3=3 t35=0
sonra-h10-hevc.mkv     dvcC=. rpu/3=0 hdr10+/3=0 mdm/3=3 t35=0
sonra-h10-hevc.mp4     dvcC=. rpu/3=0 hdr10+/3=0 mdm/3=3 t35=0
sonra-h10-av1.mkv      dvcC=. rpu/3=0 hdr10+/3=0 mdm/3=3 t35=0
sonra-h10-av1.mp4      dvcC=. rpu/3=0 hdr10+/3=0 mdm/3=3 t35=0
sonra-dv-hevc.mkv      dvcC=8.1 rpu/3=3 hdr10+/3=0 mdm/3=3 t35=0
sonra-dv-hevc.mp4      dvcC=8.1 rpu/3=3 hdr10+/3=0 mdm/3=3 t35=0
sonra-dv-av1.mkv       dvcC=10.1 rpu/3=0 hdr10+/3=0 mdm/3=3 t35=48
sonra-dv-av1.mp4       dvcC=10.1 rpu/3=0 hdr10+/3=0 mdm/3=3 t35=48
```

Komut: `vidshrink kucult <kaynak> --hedef 0.05 --kodek hevc|av1 --olcumsuz --lang en --cikti <ad>.<mkv|mp4>`.
Sonra kolunda plan satırı: DV kaynağında `-dolbyvision 1` var ve not yok, HDR10+ kaynağında
`Reasons: HdrDynamicMetadataDropped, ...` var. Kaynağın kopyası (`-c:v copy`) HDR10+'yı koruyor.

## MP4'te `-strict unofficial` Gerekli mi?

Ölçüldü: evet. `-dolbyvision 1` tek başına x265 mp4'te RPU'yu yazıyor ama `dvcC` kutusunu yazmıyor. ffmpeg'in
uyarısı `Requires -strict unofficial`.

```
dv1-x265.mp4    dvcC=. rpu/3=3        (-dolbyvision 1)
dv1s-x265.mp4   dvcC=8.1 rpu/3=3      (-dolbyvision 1 -strict unofficial)
dv1-av1.mp4     dvcC=. t35=48
dv1s-av1.mp4    dvcC=10.1 t35=48
w-av1.webm      dvcC=10.1 t35=24      (WebM/MKV strict istemiyor)
```

Bu yüzden `-strict unofficial` yalnız MP4 ailesinin (MP4/MOV) son geçişine yazılıyor. Birinci geçiş (`-f null`)
ve MKV/WebM bunu almıyor.

## Süzgeçlerle Uyum

`-dolbyvision 1` her karede DOVI yan verisi istiyor. Yan veriyi taşıyan süzgeçler, `-dolbyvision 1` ile x265
mkv'de dvcC 8.1 kaldı:

- scale, lanczos, crop, trim, pad, setsar
- hue=s=0 (gri), transpose
- nlmeans, hqdn3d, deband, deblock, unsharp
- fps, bwdif

İki durumda kodlama düşüyor:

- zscale renk matrisi: `received frame without DOVI metadata`.
- Ton eşleme zinciri: `could not determine profile`.

Bu yüzden:

- Ton eşleme politikasında bayrak hiç yazılmıyor. Çıktı SDR olduğu için dinamik not da yok.
- Renk matrisi dönüşümü seçiliyse DV taşınmıyor ve gerekçeye not düşüyor. Plan (`PlanCalculator`) ve
  argüman üretimi (`FfmpegArguments.CarriesDolbyVision`) ikisi de bakıyor, çünkü plandan sonra süzgeç
  değişebiliyor.

## Kod

- `MediaInfo`: `DolbyVisionProfile`, `DolbyVisionCompatibilityId`, `HasHdr10Plus`.
- `FfprobeClient`:
  - `ParseDolbyVision` akışın `DOVI configuration record` kaydını okuyor.
  - HDR kaynakta ilk kareye tek bir `-read_intervals %+#1 -show_frames` çağrısı SMPTE2094-40'a bakıyor
    (`FrameCarriesHdr10Plus`).
- `HdrResolver.CarriesDolbyVision`: profil 8, uyum 1, yazılım HDR10 kodlayıcısı (libx265, libsvtav1).
  - Donanım kodlayıcıları `-dolbyvision` seçeneğini tanımıyor.
  - `DynamicMetadataDropped` = HDR10+ var ya da DV var ama taşınmıyor.
- `FfmpegArguments.Build`: iki geçişe `-dolbyvision 1`, MP4 ailesinin son geçişine `-strict unofficial`.
  - Bayrak `HdrColorArgs`'a konmadı. Kalibrasyon sondası CRF ile VBV'siz koşuyor, oysa x265 DV için VBV istiyor.
  - Küçültmede x265 her zaman VBV alıyor (`SupportsRateLimits`).
- `ConversionArguments`: dönüştürücüde VBV yok, bu yüzden DV yalnız SVT-AV1'de taşınıyor.
  x265 dönüştürmesi statik HDR10'a düşüyor.
- Gerekçe: `ReasonCode.HdrDynamicMetadataDropped` → `main.reason.hdr-dynamic-dropped`, 42 dilde. CLI
  gerekçeyi enum adıyla yazdığı için CLI dil dosyası gerekmedi.

## Yapılamayan

- **HDR10+ taşıma.** Kodlayıcıya `dhdr10-info` JSON'u vermek için kaynağın bütün karelerinin çözülüp
  SMPTE2094-40 yan verisinin JSON'a çevrilmesi gerekiyor. Kesit, fps değişimi ve kare atlamada kare
  hizalaması ayrıca kurulmalı. Bu pahalı ve ayrı bir iş, açık kaldı. Bugün not düşüyor.
- **Gerçek DV örneği.** İndirme yasağı yüzünden ölçüm yalnız sentetik RPU ile yapıldı. RPU'nun geçerliliği
  ffmpeg'in ayrıştırıcısıyla sınandı, Dolby'nin doğrulayıcısıyla sınanmadı.
- **Profil 5, 7, 8.2, 8.4.** Taşınmıyor, not düşüyor (profil 5 kararla kapsam dışı).

## Yan Bulgular (kapsam dışı, düzeltilmedi)

- `FfprobeClient` mastering değerlerini `ParseFraction` ile okuyor. O fonksiyon 0,1-1000 fps aralığı dışını
  reddediyor, bu yüzden `blue_y=0.06`, `min_luminance=0.0001` gibi değerler düşüyor ve `MasteringDisplay`
  hep `null` kalıyor.
- CLL ayrıştırıcısı `average_content` anahtarını okuyor, ffprobe'un anahtarı ise `max_average`. Sonuç
  hep `null`.
- İkisi de çıktıyı bozmuyor, çünkü statik veriyi ffmpeg'in kodlayıcı sarmalayıcısı kareden taşıyor
  (yukarıdaki tabloda mastering 3/3).

## Testler ve Mutasyonlar

`tests/VidShrink.Tests/HdrDinamikTests.cs`, 24 ölçü:

- 22 argüman ya da yoklama ölçüsü, olumsuz kontrollerle.
- 2 canlı kol: 2 sn 320x180 kaynak, x265 `pools=2:frame-threads=1`, SVT `lp=2`.

Canlı DV kolu kaynağı kendisi üretiyor (ham HEVC, `DvRpu.Ekle`, `-dolbyvision 1` ile mkv). Sonra
`FfmpegArguments.Build`'in x265 mp4 argümanını koşup dvcC 8.1 ve RPU 3/3 okuyor. Aynı argümandan
`-strict unofficial` çıkarılınca dvcC'nin kaybolduğu olumsuz kontrol. SVT-AV1 mkv dvcC 10.1.

Canlı HDR10+ kolu `ProbeAsync`'in HDR10+'yı gördüğünü (dhdr10'suz eşi görmüyor) ve kodlamada düştüğünü okuyor.

Mutasyon taraması: .calisma/ altında her kesim tek tek uygulandı, test projesi derlendi, `--filter FullyQualifiedName~HdrDinamikTests` koşuldu, dosya geri yazıldı. 13 mutasyonun 13'ü kırmızı, taban 0/24.

```nM0 taban | src/VidShrink.Core/FfmpegArguments.cs | Failed:     0, Passed:    24, Skipped:     0, Total:    24
M1 Build -dolbyvision yazmiyor | src/VidShrink.Core/FfmpegArguments.cs | Failed:     5, Passed:    19, Skipped:     0, Total:    24
    Dv81YazilimKodlayicidaTasiniyorVeNotDusmuyor(kodlayici: "libx265")
    Dv81YazilimKodlayicidaTasiniyorVeNotDusmuyor(kodlayici: "libsvtav1")
    CanliDv81Mp4VeMkvdeDvcCIleTasiniyor
    Mp4AilesindeDvcCIcinStrictUnofficialSonGeciste(cikti: "cikti.mov")
    Mp4AilesindeDvcCIcinStrictUnofficialSonGeciste(cikti: "cikti.mp4")
M2 Build -strict unofficial yazmiyor | src/VidShrink.Core/FfmpegArguments.cs | Failed:     3, Passed:    21, Skipped:     0, Total:    24
    CanliDv81Mp4VeMkvdeDvcCIleTasiniyor
    Mp4AilesindeDvcCIcinStrictUnofficialSonGeciste(cikti: "cikti.mov")
    Mp4AilesindeDvcCIcinStrictUnofficialSonGeciste(cikti: "cikti.mp4")
M3 uyum kimligi 1 sarti kalkti | src/VidShrink.Core/HdrResolver.cs | Failed:     2, Passed:    22, Skipped:     0, Total:    24
    Profil81DisindakiDvTasinmiyorVeNotDusuyor(profil: 8, uyum: 2)
    Profil81DisindakiDvTasinmiyorVeNotDusuyor(profil: 8, uyum: 4)
M4 HDR10+ dusme notundan cikti | src/VidShrink.Core/HdrResolver.cs | Failed:     2, Passed:    22, Skipped:     0, Total:    24
    Hdr10ArtiNotDusurur
    CanliHdr10ArtiYoklanirVeKodlamadaDustuguNotaYaziliyor
M5 dv_profile anahtari bozuk | src/VidShrink.Ffmpeg/FfprobeClient.cs | Failed:     2, Passed:    22, Skipped:     0, Total:    24
    CanliDv81Mp4VeMkvdeDvcCIleTasiniyor
    YoklamaDoviKaydiniOkuyor
M6 SMPTE2094-40 eslesmesi bozuk | src/VidShrink.Ffmpeg/FfprobeClient.cs | Failed:     2, Passed:    22, Skipped:     0, Total:    24
    YoklamaHdr10ArtiKaresiniTaniyor
    CanliHdr10ArtiYoklanirVeKodlamadaDustuguNotaYaziliyor
M7 plan renk matrisi korumasi kalkti | src/VidShrink.Core/PlanCalculator.cs | Failed:     1, Passed:    23, Skipped:     0, Total:    24
    RenkMatrisiDonusumuDvyiKapatirVeNotDusurur
M8 Build renk matrisi korumasi kalkti | src/VidShrink.Core/FfmpegArguments.cs | Failed:     1, Passed:    23, Skipped:     0, Total:    24
    PlandanSonraAcilanRenkMatrisiBayragiDusurur
M9 donusturme -strict yazmiyor | src/VidShrink.Core/ConversionArguments.cs | Failed:     1, Passed:    23, Skipped:     0, Total:    24
    DonusturmeSvtAv1DvTasiyor(kap: "mp4", strict: True)
M10 donusturme VBV korumasi kalkti | src/VidShrink.Core/ConversionArguments.cs | Failed:     1, Passed:    23, Skipped:     0, Total:    24
    DonusturmeX265VbvsizDvTasimiyor
M11 yoklama HDR10+ karesine bakmiyor | src/VidShrink.Ffmpeg/FfprobeClient.cs | Failed:     1, Passed:    23, Skipped:     0, Total:    24
    CanliHdr10ArtiYoklanirVeKodlamadaDustuguNotaYaziliyor
M12 gerekce notu eklenmiyor | src/VidShrink.Core/PlanCalculator.cs | Failed:     8, Passed:    16, Skipped:     0, Total:    24
    DonanimKodlayicisiDvTasimiyorVeNotDusuyor
    Profil81DisindakiDvTasinmiyorVeNotDusuyor(profil: 8, uyum: 2)
    Profil81DisindakiDvTasinmiyorVeNotDusuyor(profil: 7, uyum: 6)
    Profil81DisindakiDvTasinmiyorVeNotDusuyor(profil: 5, uyum: 0)
    Profil81DisindakiDvTasinmiyorVeNotDusuyor(profil: 8, uyum: 4)
    Hdr10ArtiNotDusurur
    CanliHdr10ArtiYoklanirVeKodlamadaDustuguNotaYaziliyor
    RenkMatrisiDonusumuDvyiKapatirVeNotDusurur
M13 donanim kodlayici korumasi kalkti | src/VidShrink.Core/HdrResolver.cs | Failed:     1, Passed:    23, Skipped:     0, Total:    24
    DonanimKodlayicisiDvTasimiyorVeNotDusuyor
```
