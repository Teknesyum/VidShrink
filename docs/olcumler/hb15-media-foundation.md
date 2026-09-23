# HB #15 Media Foundation Ölçümü

Tarih 2026-09-23. Makine: Windows 11 Pro, RTX 5070 Ti (NVIDIA MFT), AMD MFT'leri de kayıtlı.
ffmpeg `9.0-full_build-www.gyan.dev`. Bütün kaynaklar lavfi: 2 sn `testsrc2=size=320x240:rate=30,noise=alls=10:allf=t`
(ffv1 ara dosya), kodlama `-threads 2`.

## 1. Kodlayıcının Bildirdikleri

`ffmpeg -h encoder=h264_mf` (hevc_mf ve av1_mf aynı seçenekleri bildiriyor):

| Seçenek | Değerler |
|---|---|
| `-rate_control` | default -1, cbr 0, pc_vbr 1, u_vbr 2, quality 3, ld_vbr 4, g_vbr 5, gld_vbr 6 |
| `-scenario` | default, display_remoting, video_conference, archive, live_streaming, camera_record, display_remoting_with_feature_map |
| `-quality` | -1..100, varsayılan -1 |
| `-hw_encoding` | boolean, varsayılan false |
| Piksel biçimleri | nv12, yuv420p, d3d11 |

`-preset` yok. Ürün yalnız `-hw_encoding`, `-rate_control`, `-b:v`/`-maxrate`/`-bufsize`,
`-g` ve `-pix_fmt` yazar.

## 2. Yoklama

Ürünün yoklama argümanı (`EncoderCapabilities.ProbeArguments`): 256x256 testsrc2, 1 kare,
`-f null`. Tabloda üç bileşim:

| Kodlayıcı | hw + nv12 | hw + yuv420p | yazılım + nv12 |
|---|---|---|---|
| h264_mf | çıkış 0, 272 ms | `format negotiation failed` | çıkış 0, 42 ms (yazılım MFT) |
| hevc_mf | çıkış 0, 302 ms | `format negotiation failed` | `failed processing input: 80004005` |
| av1_mf | çıkış 0, 308 ms | `format negotiation failed` | `could not find any MFT for the given media type` |

Sonuç: ürün her MF çağrısında `-hw_encoding 1 -pix_fmt nv12` yazar (`FfmpegArguments.MediaFoundationDeviceArgs`,
`CodecModel.OutputPixelFormat`). `-hw_encoding` olmadan h264 sessizce yazılım MFT'sine düşüyor,
hevc/av1 hiç açılmıyor. Yoklama ürünün kendi anahtarlarıyla koşar; yoksa yazılım MFT'si
"çalışıyor" derdi.

## 3. Hız Denetimi

Teslim / istek oranı, video akışı bit hızı (ffprobe). `pc_vbr` kolunda tepe 1,02x, tampon 1,04x.

| Kol | 200k | 300k | 600k |
|---|---:|---:|---:|
| libx264 tek geçiş (kıyas) | 0,894 | 0,999 | 0,992 |
| h264_nvenc vbr (kıyas) | 1,120 | 1,130 | 1,179 |
| h264_mf default | 1,080 | 1,045 | 1,044 |
| h264_mf cbr | 1,080 | 1,045 | 1,044 |
| h264_mf u_vbr | 1,161 | 1,142 | 1,044 |
| **h264_mf pc_vbr** | **1,080** | **1,045** | **1,017** |
| hevc_mf cbr | 1,091 | 1,063 | 1,086 |
| hevc_mf u_vbr | 1,134 | 1,160 | 1,086 |
| **hevc_mf pc_vbr** | **1,071** | **1,063** | **1,067** |
| av1_mf u_vbr | 1,215 | 1,244 | 1,097 |
| **av1_mf pc_vbr** | **1,118** | **1,099** | **1,115** |

`pc_vbr` her kodlayıcıda en iyi ya da eşit. `u_vbr` en çok aşan kol. Ürün `pc_vbr` yazar
(`CodecModel.BitrateRateControlArgs`). MF donanım ailesine girdi (`IsHardware`): tek geçiş,
dar tepe (`PeakRateFactor`, 1,02-1,10), taban/pay kuralları NVENC dışı donanımla aynı.

### Kalite Ölçeği

`-rate_control quality`: `-quality 30` 204,8k, `-quality 70` 3467,8k. Yani ölçek gerçek.
`-global_quality 20` ve `35` bayt bayt aynı dosyayı verdi (570 928 bayt), yani etkisiz.
CRF → `-quality` eşlemesi ölçülmedi. Bu yüzden MF'nin kalite ölçeği yok sayılır
(`CodecModel.HasQualityScale` false):

- CRF isteyen plan (motor, `LockedCrf` ya da `LockedMode = Crf`) bit hızına döner, gerekçe `ReasonCode.NoQualityScaleBitrate` (VP9 emsali).
- Plan ayrıştırıcısı crf kipli MF planını reddeder.
- `QualityArgs` MF'de `NotSupportedException` atar.
- Kalibrasyon örneklemesi koşmaz.
- Önizleme parçası modellenmez, "temsili" rozeti çıkar.

## 4. Otomatik Kip Kararı

MF, `FastHardwareOrder`'a **eklenmedi**. Yalnız elle seçilir: Gelişmiş'teki kodek kilidi, CLI
`--kodek`, plan JSON'u, ön ayar `lockedCodec`.

Neden: bu makinede MF, NVIDIA MFT'sinin kendisi. NVENC aynı donanımı doğrudan ve ölçülmüş
kurallarla (D1 dolumu, GOP kapıları) kullanıyor, dolayısıyla Otomatik'te MF'nin NVENC'in önüne
geçmesi için bir sebep yok. MF'nin tek başına işe yarayacağı makine (NVENC/QSV/AMF'si olmayan,
örn. Qualcomm MFT'li Windows on ARM) burada ölçülemiyor. Ölçülmeden yeğleme sırasına
"son çare" olarak da girmez: `OtomatikHizliKipMfyiSecmez` bunu pimler.

## 5. Hedef Boyut İsabeti (Ürün Yolu)

`PlanCalculator` (kilitli kodek, çözünürlük/fps düşürme kapalı) → `EncodeRunner.RunAsync`,
`FillPolicy.FillTarget`. `BudgetFill.AimFor` MF'de plan kurmaz (QSV/AMF gibi). Tavanı
`EncodeRunner`'ın tavan koruması tutar.

| Kodlayıcı | Hedef MB | Plan | 1. deneme | Teslim MB | Oran | Deneme |
|---|---:|---:|---|---:|---:|---:|
| h264_mf | 0,08 | 295k | 0,0784 (bantta) | 0,0784 | 0,980 | 1 |
| h264_mf | 0,2 | 753k | 0,2116 (tavan üstü) | 0,1953 | 0,976 | 2 |
| h264_mf | 0,5 | 1899k | 0,5621 (tavan üstü) | 0,4809 | 0,962 | 2 |
| hevc_mf | 0,08 | 295k | 0,0790 (bantta) | 0,0790 | 0,987 | 1 |
| hevc_mf | 0,2 | 753k | 0,2165 (tavan üstü) | 0,1918 | 0,959 | 2 |
| hevc_mf | 0,5 | 1899k | 0,5608 (tavan üstü) | 0,4904 | 0,981 | 2 |
| av1_mf | 0,08 | 295k | 0,0867 (tavan üstü) | 0,0776 | 0,970 | 2 |
| av1_mf | 0,2 | 753k | 0,2313 (tavan üstü) | 0,1981 | 0,990 | 2 |
| av1_mf | 0,5 | 1899k | 0,5467 → 0,5056 (tavan üstü) | 0,4610 | 0,922 | 3 |

9/9 hücre hedefin altında teslim etti, hiçbiri aşmadı. Ancak ilk deneme 9 hücreden 6'sında
tavanı %6-16 aşıyor ve bedeli bir ya da iki ek kodlama. Bunun sebebi, MF için
`RelativeBitrateNeed` ya da donanım sapma katsayısının ölçülmemiş olması; varsayılan 1,0'da
kaldı. Kalıcı canlı kol `KilitliMfHedefBoyutuAsmadanTeslimEder`, h264_mf 0,08 MB hücresidir.

## 6. CI Koşucusu

Koşum 35905549045 (commit 5d4f24c8), `ana` işi: 4690 test, 4660 geçti, 30 atlandı, 0 kırmızı.
Koşucunun ffmpeg'i (GyanD) `--enable-mediafoundation` ile derlenmiş; kodlayıcı adları listede.

- `YoklamaKararVerirVeKilitliPlanOnuIzler` (`[FfmpegFact]`) koştu ve geçti: yoklama bir karar
  veriyor, kilitli plan ona uyuyor — MF açılmasa da yol yazılım ailesine düşüyor.
- `KilitliMfHedefBoyutuAsmadanTeslimEder` (`[MediaFoundationFact]`) atlandı: GPU'suz Windows
  koşucusunda `-hw_encoding 1` ile donanım MFT'si açılmıyor, deneme kodlaması düşüyor.
- `suzgecli-test` (koşum 35905548783): 19 testin 17'si geçti, 2 canlı kol atlandı (o işte ffmpeg yok).

Öznitelik seçimi bu ölçüye göre: koşucuda MF yok diye kol sessizce geçmiyor. `MediaFoundationFact`
Windows dışı, ffmpeg yokluğu, kodlayıcı yokluğu ve başarısız deneme kodlaması için ayrı gerekçeyle
atlıyor. Kolun gerçek koşumu yalnız MFT'si olan makinede; bu makinede (RTX 5070 Ti) yeşil.

## 7. Mutasyonlar

Taban `MediaFoundationTests` 19/19.

| Mutasyon | Kırmızı |
|---|---|
| M1 `SpeedArgs` MF kolunda `-hw_encoding` kalkar | 4/19 (argüman teorisi 3 + canlı hedef kolu) |
| M2 `IsOfferedOn` hep true | 2/19 (Windows dışı olumsuz kontrol, plan ayrıştırıcısı) |
| M3 CRF → bit hızı dönüşümü kalkar | 4/19 |
| M4 `pc_vbr` → `u_vbr` | 3/19 |
| M5 yoklamada `-hw_encoding` kalkar | 1/19 |

## 8. Ölçülmeyenler

- `RelativeBitrateNeed` ve donanım sapma katsayısı: MF 1,0 varsayılanında. İlk denemedeki aşım bundan geliyor (§5).
- `-scenario`: ürün yazmıyor, etkisi ölçülmedi.
- 10 bit: MF her zaman `nv12`'ye iner. 10 bit kaynak MF'de 8 bit `nv12`'ye iner, p010 yolu ölçülmedi.
- CRF → `-quality` eşlemesi (§3).
- Yalnız MF'si çalışan bir makinede hız ve isabet (§4).
