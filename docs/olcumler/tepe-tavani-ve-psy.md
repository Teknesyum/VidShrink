# Tepe hızı tavanı ve psiko-görsel bayraklar

Tarih: 2026-09-01. Ortam: Windows 11, ffmpeg 9.0-full, `av1_nvenc` p5. Boyut ve gerçek argüman sayıları `tools/VidShrink.Bench shrink`, VMAF-NEG sayıları `tools/VidShrink.Bench measure` çıktısıdır. Kaynaklar aynı hareketli içeriğin kayıpsız hazırlanmış 20 saniyelik iki klibidir: 1280×720@60 ve 1920×1080@30. Ses kapalı, çözünürlük ve FPS düşürme kapalı, kalibrasyon kapalıdır.

## Tepe çarpanı ve teslim boyutu

`Eğri` sütununda mevcut diz eğrisinin ürettiği gerçek tepe çarpanı, `1,50` sütununda bağımsız geniş-tepe koşumu vardır. Taban oranı, planın istediği video bit hızının `CodecModel.MinBitrateK` değerine bölümüdür. Teslim oranı gerçek MiB / hedef MiB'dir.

| Kaynak | Taban | İstenen kbit/sn | Hedef MiB | Eğri tepe | Eğri teslim MiB | Eğri oran | 1,50 teslim MiB | 1,50 oran |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 720p60 | 2,920680 | 1031 | 2,6 | 1,02 | 2,5313377380371094 | 0,9735914377065805 | 2,547816276550293 | 0,9799293371347281 |
| 720p60 | 7,798867 | 2753 | 6,9 | 1,046498 | 6,794994354248047 | 0,9847817904707313 | 6,785617828369141 | 0,9834228736766870 |
| 720p60 | 11,898017 | 4200 | 10,35 | 1,10 | 10,032888412475586 | 0,9693611992730034 | 10,018817901611328 | 0,9680017296242830 |
| 1080p30 | 3,055328 | 1491 | 3,75 | 1,02 | 3,6605911254882812 | 0,9761576334635417 | 3,516530990600586 | 0,9377415974934897 |
| 1080p30 | 7,776639 | 3795 | 9,5 | 1,046115 | 9,398728370666504 | 0,9893398284912110 | 9,368837356567383 | 0,9861934059544613 |
| 1080p30 | 11,901639 | 5808 | 14,3 | 1,10 | 14,216007232666016 | 0,9941263799067144 | 14,200657844543457 | 0,9930529961219200 |

Bu iki klipte 1,50 hiçbir hedefi aşmadı; buna karşın eski 882×496@60, 11,4× ölçümü 1,50 ile 1,056 teslim oranını göstermişti. Dolayısıyla çelişki kaynak/yerleşim bağımlılığıdır: gerçek HDR koşumundaki kalite kazancı doğrudur, fakat 1,50 genel boyut garantisi değildir. `HardwarePeakCeiling=1.10` ve 6,0–11,4 diz eğrisi korunacaktır; yeni ölçüm eski aşma kanıtını geçersiz kılmıyor.

## Psy/AQ ablasyonu

Kaynak `clip-720p60.mkv`, hedef 6,9 MiB, plan 1280×720@60 `av1_nvenc` p5 ve 2753 kbit/sn'dir. Tek fark, açık koşumda ölçülerek desteklendiği doğrulanan `-spatial-aq 1 -temporal-aq 1`; kapalı koşumda aynı seçeneklerin son argüman olarak `0` verilmesidir. Kalite, `tools/VidShrink.Bench measure` ile klibin tamamında ölçüldü.

| AQ | Teslim MiB | Hedef oranı | VMAF-NEG mean | Harmonic | p10 |
|---|---:|---:|---:|---:|---:|
| Kapalı | 6,787405014038086 | 0,9836818860924762 | 75,19705973749988 | 63,44009931105794 | 36,7723875 |
| Açık | 6,794994354248047 | 0,9847817904707313 | 75,29163206000004 | 63,59703711920521 | 36,9102289 |
| Açık − kapalı | 0,007589340209961 | 0,0010999043782551 | 0,09457232250016 | 0,15693780814727 | 0,1378414 |

Tek koşumda AQ açıkken mean +0,09457232250016, harmonic +0,15693780814727 ve p10 +0,1378414 oldu; teslim boyutu da +0,007589340209961 MiB (+%0,11) büyüdü. Tekrar ve eş-boyut deneyi yapılmadığı için bit başına kalite kazancı sonucu çıkarılamaz; yalnız bu tek koşumun üç özetinde artış görüldüğü söylenebilir. `libx265` ve `libsvtav1` eşdeğerlerinin kalite etkisi ölçülmedi; bunlar yalnız gerçek seçenek yoklaması başarılıysa üretiliyor. Özellikle SVT-AV1 yoklaması yalnız çıkış koduna güvenmiyor: stderr'deki `Error parsing option`, `Option not found` ve `Unrecognized option` tanılarını da başarısız sayıyor.

## Düzeltme turu 2

- HDR `libx265` yolundaki psy ve HDR değerleri artık tek `-x265-params` dizgisinde birleşiyor; böylece ffmpeg'in son değeri kullanması iki taraftan birini silemiyor.
- Arayüzde gösterilen komut, `EncodeRunner` ile aynı `EncoderCapabilities.Instance` sonucunu kullanıyor. `CalibrationProbe` psy/AQ bayraklarını aynı ölçülmüş yetenek üzerinden alıyor. **Düzeltme (tur 3):** bu satır önizleme parçası için yanlıştı — `PreviewSegment.For` isteğe bağlı parametreyi kazanmıştı ama üretimdeki iki çağıran da geçirmiyordu, yani önizleme psy/AQ'suz kodlanıyordu. Tur 3'te düzeltildi.
- **Geri çekildi (tur 3):** bu satır `HardwareRateControlTests.LiveFastTargetsLandInsideTheBandOnTheFirstAttempt`i boyut garantisinin dayanağı olarak gösteriyordu. O test `[LiveSourceTheory]` ile işaretli; `VIDSHRINK_LIVE_SOURCE` verilmeden **atlanıyor**, dolayısıyla tur 2 koşumlarında hiç çalışmadı ve bir şey kanıtlamadı. Tur 2'de yazılmış da değil (dosyanın son dokunuşu `cd17e28`). 882×496@60 / 11,4× koşumundaki 1,056 oranı gerçek bir ölçüm, ama tur 2'de o mutasyonu kırmızıya döndüren koşan bir ölçü yoktu. Boyut garantisinin koşan ölçüsü tur 3'te yazıldı.
- G1 mutasyonunda birleştirme kaldırılınca `Hdr_x265_psy_ve_renk_parametreleri_tek_dizgide_birlesir`; G3 mutasyonunda arayüz eski `Build` çağrısına döndürülünce `Arayuzde_gosterilen_komut_kosucunun_argumanlariyla_aynidir`; G4 mutasyonunda availability aktarımı kaldırılınca `Parca_tam_kodlamayla_ayni_psy_kabiliyetini_kullanir` kırmızı oldu.
- Sözleşmedeki `CodecModelTests` sınıfı depoda yoktur; yerel filtre bunun yerine mevcut `HardwareRateControlTests` ile koşturuldu.
- Önceki turda sahiplik listesi dışındaki `IEncoderAvailability.cs`, `EncoderCapabilities.cs` ve `EncodeRunner.cs` değişiklikleri seçenek desteğini gerçek ffmpeg ile ölçmek ve aynı sonucu koşucuya vermek için zorunluydu. Bench `Program.cs` de rapor sayılarını gerçek argümandan üretmek için değişti.
- Teknik borç: `RunOptionProbe`, saf görünen `Build()` yolundan senkron ffmpeg G/Ç'si başlatabiliyor ve kodlayıcı başına dört saniyeye kadar bloklayabiliyor. Bu turda ayrı önbellekli/asenkron yoklama katmanına taşınmadı. **Kapandı (T92):** `Build` artık yalnız ısıtılmış sonucu okuyor, yoklamayı çağıran ısıtıyor.

## Doğrulama

Psy/AQ argüman ekleme çağrısı geçici olarak kaldırıldığında üç yeni davranış testi de başarısız oldu: libx265, libsvtav1 ve NVENC. Çağrı geri getirildikten sonra sözleşme filtresi 14 başarılı / 0 başarısız; tam Release paketi 957 başarılı / 0 başarısız sonuçlandı. Var olan assertion gevşetilmedi ve test atlaması eklenmedi.

Düzeltme turu 2 son doğrulaması: `FfmpegArgumentsTests|PlanCalculatorTests|HardwareRateControlTests|HdrArgumentsTests` filtresi 63/63; kesintisiz tam Release süiti 969 başarılı / 0 başarısız / 17 atlandı, 529,2 saniye. Çıktıda kesinti satırı yoktu.

**Düzeltme (tur 3):** yukarıdaki "63/63" yanlış yazılmış. Aynı filtrenin gerçek özeti 63 başarılı / 2 atlanan / 65 toplam ve atlanan iki test tam da boyut garantisi iddiasını taşıyanlardı: `LiveFastTargetsLandInsideTheBandOnTheFirstAttempt` ve `LiveProcessorTargetsStillLandInsideTheBandOnTheFirstAttempt`.

## Düzeltme turu 3

### I1 — boyut garantisinin koşan ölçüsü

`FfmpegArgumentsTests` içine, canlı kaynak gerektirmeyen ve `Math.Clamp` sınırlarını tekrar etmeyen üç ölçü girdi. Üçü de `PeakRateFactor`'ın **şekline** bakıyor; ikisi yalnızca üretimin iki çıktısını birbiriyle karşılaştırıyor, üçüncüsü yukarıdaki bench tablosunun ve `FfmpegArguments` yorum bloğundaki ölçümlerin sayılarına yaslanıyor:

| Ölçü | İddia | Dayanak |
|---|---|---|
| `Tepe_carpani_taban_orani_boyunca_geri_gitmez` | 0,5×–30× arasında çarpan hiçbir adımda geri gitmez | Eksik teslim taban oranıyla birlikte büyüyor (11,4×'te 1,02 → 0,973) |
| `Tepe_egrisi_dizden_once_duz_dizden_sonra_artan_olcum_disinda_doymus` | 1,0× = 2,92× = 5,3×; 5,3× < 7,80× < 11,90×; 11,90× = 12,5× = 30× | Bench tablosundaki üç taban oranı; en yüksek ölçülen oranın üstünde ölçüm yok |
| `Tepe_carpani_olculen_guvenli_degerlerin_disina_cikmaz` | 5,3×'te çarpan ≤ 1,02; 11,4×'te ≤ 1,10; 200×'e kadar hiçbir yerde < 1,50 değil | 882×496@60: 5,3× / 1,02 → 1,007; 11,4× / 1,10 → 1,008; 11,4× / 1,50 → 1,056 |

Ölçüler `CodecModel.MinBitrateK` üzerinden taban oranını bit hızına çevirip `PeakRateFactor`'ı çağırıyor; beklenen değerlerin hiçbiri `TightPeakFactor`/`HardwarePeakCeiling` sabitlerinden okunmuyor.

Dört mutasyon, dördü de kırmızı (`dotnet test -c Release --filter "FfmpegArgumentsTests"`, taban yeşil koşum 19/19):

| Mutasyon | Sonuç | Kırılan |
|---|---|---|
| `PeakWidestAtFloorRatio` 11.4 → 7.0 | Başarısız: 2, Başarılı: 17, Toplam: 19 | `Tepe_egrisi_...doymus` (iki yerleşim) |
| `PeakOpensAtFloorRatio` 6.0 → 2.0 | Başarısız: 4, Başarılı: 15, Toplam: 19 | `..._olculen_guvenli_degerlerin_disina_cikmaz`, `Tepe_egrisi_...doymus` |
| `opening` ters çevrildi | Başarısız: 6, Başarılı: 13, Toplam: 19 | üç ölçü de |
| `Math.Clamp` üst sınırı `double.MaxValue` | Başarısız: 5, Başarılı: 14, Toplam: 19 | `..._olculen_guvenli_degerlerin_disina_cikmaz`, `Tepe_egrisi_...doymus`, eski `Donanim_tepe_carpani_...` |

Sabit hiç değişmedi: `HardwarePeakCeiling = 1.10` ve 6,0–11,4 diz eğrisi tur 2'deki gibi duruyor, bu turda yeni boyut ölçümü alınmadı.

Eski `Donanim_tepe_carpani_boyut_guvencesi_tavanini_asmaz` tur 3'te olduğu gibi bırakılmıştı: `Clamp` çıkışını `Clamp` sınırlarıyla karşılaştırdığı için yanlışlanamazdı, ama assertion gevşetmemek için silinmedi. **Düzeltme (T92):** bu cümle artık geçersiz — o ölçü T92'de yedi vakalık `Donanim_tepe_carpani_taban_oraninda_beklenen_degeri_uretir` teorisine dönüştürüldü; beklenen değerler `Clamp` sınırlarından değil elle yürütülen eğriden geliyor.

### I2 — önizleme psy/AQ'yu görüyor

Yetenek tek yerde duruyor: `SegmentEncoder.Availability`. Hem kodlayan yol (`EncodeAsync`) hem imza hesabı (`PanelHost.ClipSignature`) artık `SegmentEncoder.Describe` üzerinden geçiyor, yani ikisi aynı değeri görüyor. `MainWindow.ApplyHardwareVerdict` arka planda ölçülen yeteneği `PanelHost.Availability`'ye veriyor. `PreviewSegment.For` ve `FfmpegArguments.BuildSegment` docstring'leri `availability` verilmediğinde parçanın psy/AQ taşımadığını söylüyor.

Mutasyonlar (`--filter "FfmpegArgumentsTests"`, taban yeşil 23/23):

| Mutasyon | Sonuç | Kırılan |
|---|---|---|
| `Describe`'dan `availability: Availability` çıkarıldı | Başarısız: 1, Başarılı: 22, Toplam: 23 | `Onizleme_kodlayicisi_kendi_kabiliyetini_parcaya_gecirir` |
| `PanelHost.ClipSignature` doğrudan `PreviewSegment.For`a döndürüldü | Başarısız: 1, Başarılı: 22, Toplam: 23 | aynı ölçü |

`PreviewSegmentTests|SegmentEncoderTests|PanelHostTests` filtresi değişiklikten sonra 37 başarılı / 0 başarısız / 0 atlanan / 37 toplam, 1 m 13 s.

### I3 — yoklama arayüz iş parçacığından çıktı

`MainWindow.WarmPsychovisualProbe`, `FfmpegArguments.KnownCodecs` üzerinden psy/AQ seçenek yoklamasını `ProbeHardwareEncodersAsync`'in var olan `Task.Run`'ı içinde bir kez tüketiyor. `RefreshPlanView` artık `EncoderCapabilities.Instance` okumuyor, arka plan yoklamasının sonucunu tutan `_encoders` alanını kullanıyor. Sonuç önbellekli olduğu için ilk çağrının nereye düştüğü tek karardır.

Bu makinede yoklamanın maliyeti — `RunOptionProbe`'un koşturduğu ffmpeg komutları `Measure-Command` ile tek tek ölçüldü (ffmpeg 9.0-full, 1 Eylül 2026):

    libx265 -x265-params=psy-rd=2:psy-rdoq=1:aq-mode=2 : 93 ms
    libsvtav1 -svtav1-params=tune=0:enable-variance-boost=1:variance-boost-strength=2 : 71 ms
    av1_nvenc -spatial-aq=1 : 197 ms
    av1_nvenc -temporal-aq=1 : 196 ms
    hevc_nvenc -spatial-aq=1 : 174 ms
    hevc_nvenc -temporal-aq=1 : 180 ms
    h264_nvenc -spatial-aq=1 : 180 ms
    h264_nvenc -temporal-aq=1 : 183 ms
    TOPLAM: 1274 ms

`KnownCodecs`'teki on üç kodlayıcıdan yalnız bu sekiz yoklama doğuyor; `libx264`, `libvpx-vp9`, QSV ve AMF kolları `PsychovisualArgs` içinde hiç seçenek sormuyor. Eskiden arayüz iş parçacığında ödenen kısım plan `av1_nvenc` iken 197 + 196 = 393 ms idi; şimdi 1274 ms'in tamamı açılıştaki arka plan görevinde ödeniyor. Isınma ile plan görünümünün ilk çizimi arasındaki yarış ölçülmedi: yoklama bitmeden plan çizilirse gösterilen komut o çizimde psy/AQ'suz kalır, `ApplyHardwareVerdict` sonrası `Recalculate()` ile düzelir.

Mutasyonlar (taban yeşil 23/23):

| Mutasyon | Sonuç | Kırılan |
|---|---|---|
| `RefreshPlanView` yeniden `EncoderCapabilities.Instance` okuyor | Başarısız: 1, Başarılı: 22, Toplam: 23 | `Arayuz_yolunda_kodlayici_yoklamasi_dogurulmaz` |
| `WarmPsychovisualProbe(capabilities)` çağrısı silindi | Başarısız: 1, Başarılı: 22, Toplam: 23 | aynı ölçü |

### I5 — muhasebe

- Tur 2'de `owns:` dışına yazılan `PreviewSegment.cs` ve `CalibrationProbe.cs` bu turda listeye alınmıştı; bu turda `owns:` dışına **hiçbir dosya yazılmadı**. `tests/VidShrink.Tests/TipSources.cs`'e yeni bir yol sabiti eklemek yerine yol test içinde `TipSources.Root` üzerinden kuruldu, çünkü o dosya `owns:` dışında.
- Bu turda sabit değişmedi, dolayısıyla `FfmpegArguments` yorum bloğu da değişmedi; yalnız `KnownCodecs`, `BuildSegment` ve `PreviewSegment.For` docstring'leri eklendi/hizalandı.

### I6 — kapatılmayan borç

Sözleşmenin bu turda kapanmasını beklemediği üç madde duruyor:

- `tools/VidShrink.Bench/Program.cs` `--no-psy` ablasyonu `-spatial-aq 0 -temporal-aq 0`'ı `ExtraArgs` ile psy'den sonra ekliyor; son-yazan-kazanır kalıbı, `ExtraArgs` sırası değişirse ablasyon sessizce yanlış ölçer.
- `ExtraArgs` `PsychovisualAndColorArgs` birleştiricisinden geçmiyor: `-x265-params` içeren bir `ExtraArgs` birleşik dizgeyi yine sessizce ezer. **Kapandı (T92):** `Build` her iki çıkış yolunda `MergeEncoderParams`tan geçiyor, `ExtraArgs` dahil.
- `RunOptionProbe` hâlâ `Build()` yolundan senkron ffmpeg süreci doğurabiliyor. Bu turda yalnız **ilk çağrının yeri** düzeltildi; ayrı önbellekli/asenkron yoklama katmanına taşınmadı. **Kapandı (T92):** yoklama `Build`den çıktı.

### Doğrulama

Sözleşme filtresi `dotnet test -c Release --filter "FfmpegArgumentsTests|PlanCalculatorTests|HardwareRateControlTests"`:

    Başarılı!  - Başarısız:     0, Başarılı:    65, Atlanan:     2, Toplam:    67, Süre: 66 ms - VidShrink.Tests.dll (net8.0)

Atlanan iki test `HardwareRateControlTests.LiveFastTargetsLandInsideTheBandOnTheFirstAttempt` ve `LiveProcessorTargetsStillLandInsideTheBandOnTheFirstAttempt`; ikisi de `[LiveSourceTheory]` ve `VIDSHRINK_LIVE_SOURCE` verilmediği için koşmadı. **Bu turdaki hiçbir iddia bu iki teste dayanmıyor**; boyut garantisinin ölçüleri koşan 65 testin içinde. Okuma koşum kapısından geçirildi:

    KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=67 alt-sınır=67

Tam süit bu turda yerel olarak koşulmadı — makinede paralel çalışan başka ajanlar ölçüyü kararsız yapıyor; tam süit dalın CI koşumuna bırakıldı. Var olan hiçbir assertion gevşetilmedi, hiçbir test `Skip`e alınmadı.

Dalın CI koşumu (`gh run view 33545797054`, `T87-tepe-tavani-ve-psy`, 14 m 38 s):

    Passed!  - Failed:     0, Passed:   924, Skipped:    72, Total:   996, Duration: 13 m 29 s - VidShrink.Tests.dll (net8.0)
    KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=996 alt-sınır=950

Bir önceki itmenin CI koşumu (33545605270) `dotnet build -c Release -warnaserror` adımında düşmüştü: `MainWindow.axaml.cs:1249` `_preview.Availability = encoders;` satırı CS8602 (olası `null` başvurusu) veriyordu. Yerel `dotnet test` bu bayrağı kullanmadığı için yeşil okunmuştu. `if (_preview is not null)` denetimiyle düzeltildi.

## T92 — yanlışlanamayan ölçü, tek birleştirici ve ısıtma sorumluluğu

T87 denetiminin bıraktığı beş borcu kapatan sözleşme. Bu bölümdeki bütün sayılar
`dotnet test -c Release --filter "FfmpegArgumentsTests"` çıktısından gelir; bu turda
`tools/VidShrink.Bench` koşturulmadı, yani **yeni boyut ya da VMAF ölçümü alınmadı**.

### Ne değişti

- Yanlışlanamayan tepe ölçüsü yedi vakalık bir teoriye dönüştü
  (`Donanim_tepe_carpani_taban_oraninda_beklenen_degeri_uretir`); beklenen değerlerin hiçbiri
  `TightPeakFactor`/`HardwarePeakCeiling` sabitlerinden okunmuyor, elle yürütülen eğriden
  geliyor.
- Psy/AQ, HDR renk ve kullanıcının `ExtraArgs`ı tek `MergeEncoderParams` birleştiricisinden
  geçiyor; `Build`in her iki çıkış yolu (birinci geçiş ve son geçiş) birleştiriciden dönüyor.
- Argüman üretimi saf: `Build` yalnız ısıtılmış sonucu okuyor, süreç doğurmuyor.

### J1 — ısıtılmayan çağıranda psy/AQ'nun sessizce düşmesi

Tur 1'de `Build` saf oldu, ama ısıtan tek yer Avalonia penceresiydi. `tools/VidShrink.Bench`
`EncodeRunner` üzerinden geçtiği ve hiç ısıtmadığı için ölçüm aracı psy/AQ argümanları
**olmadan** kodluyordu: hata yok, çıkış kodu 0, rapora giren sayı yanlış.

Sözleşmenin iki adayından **ısıtmayı `EncodeRunner`a vermek** seçildi. Gerekçe: tembel
ısıtma `SupportsEncoderOption`u yeniden süreç doğuran bir yola çevirir ve `Build`in saflığını
(kriter 6) feda ederdi; kodlayan bütün yollar zaten `EncodeRunner`dan geçtiği için tek nokta
yetiyor. `EncodeRunner.EncodeArguments` önce `FfmpegArguments.WarmPsychovisual`ı, sonra
`Build`i çağırıyor; `RunOneAsync` bu yoldan geçiyor. `tools/VidShrink.Bench/Program.cs`
değiştirilmedi — T88'in elinde.

Ölçüsü `Kosucunun_arguman_uretimi_isitilmamis_kabiliyette_psy_bayragini_dusurmez`: hiç
ısıtılmamış sahte bir kabiliyetle `EncodeArguments` koşuyor ve üç vakada da (av1_nvenc
`-spatial-aq`, `-temporal-aq`; libx265 `-x265-params`) bayrağın üretilen komutta bulunduğunu
ve kabiliyetin ısıtıldığını gösteriyor.

### J2 — ısıtma varsayılan parametreden ada geçti

`PsychovisualArgs(..., bool warm = true)` kalktı. Üç ad var: `PsychovisualArgs` ölçer,
`CachedPsychovisualArgs` yalnız ısıtılmış sonucu okur, `WarmPsychovisual` yalnız ısıtır.
`Build` saf olanı çağırıyor; `CalibrationProbe` açıkça ısıtıp saf yoldan okuyor.

**Sapma:** sözleşme "saf yol varsayılan olur" diyor; burada saf yolun adı
`CachedPsychovisualArgs`, süssüz ad (`PsychovisualArgs`) ölçen yolda kaldı. Nedeni:
`MainWindow.WarmPsychovisualProbe` ısıtma döngüsünde tam olarak `PsychovisualArgs(codec,
capabilities)` çağırıyor ve `MainWindow.axaml.cs` bu turda T88'in `owns` kümesinde. Süssüz
adı saf yola vermek, o dosyaya dokunmadan, arayüzün ısıtmasını sessizce kapatırdı —
J1'in kapattığı kusurun aynen arayüz tarafında açılması. Ada göre ayrım ve varsayılan
parametrenin kalkması sağlandı; hangi adın süssüz olduğu `MainWindow.axaml.cs` serbest
kaldığında çevrilebilir.

Arayüzün ısıtma ölçüsü de bu turda gerçekten davranış ölçer hale geldi:
`Yoklama_isinmasi_...` artık okuma sayacına değil **ısıtma sayacına** bakıyor, yani
`WarmPsychovisualProbe` ısıtmayı bırakırsa kırmızıya döner (M8, M10).

### J3 — mutasyon tablosu

Tur 1'de sekiz mutasyon koşturulduğu bildirilmiş ama tablo ağaçta yoktu; denetçi K7'yi
doğrulayamadı. Tablo bu turda **sıfırdan** koşturuldu ve tur 1'in düzeltmelerini de kapsıyor.
Düzenek her mutasyonu üretim kaynağına uyguluyor, filtreyi koşuyor, kaynağı geri alıyor.
Taban yeşil koşum: **Başarısız 0, Başarılı 37, Atlanan 0, Toplam 37**. Mutasyonların hepsi
üretim davranışını bozar; hiçbiri testin kendi sabitini değiştirmez.

| # | Mutasyon | Sonuç | Kırılan ölçü |
|---|---|---|---|
| M1 | `PeakOpensAtFloorRatio` 6.0 → 2.0 | Başarısız 8 / 37 | `Donanim_tepe_carpani_taban_oraninda_beklenen_degeri_uretir`, `Tepe_carpani_olculen_guvenli_degerlerin_disina_cikmaz`, `Tepe_egrisi_..._doymus` |
| M2 | `PeakWidestAtFloorRatio` 11.4 → 7.0 | Başarısız 4 / 37 | `Donanim_tepe_carpani_taban_oraninda_beklenen_degeri_uretir`, `Tepe_egrisi_..._doymus` |
| M3 | `opening` payı ters çevrildi | Başarısız 12 / 37 | yukarıdaki üçü + `Tepe_carpani_taban_orani_boyunca_geri_gitmez` |
| M4 | `MergeEncoderParams` girdiyi olduğu gibi döndürüyor | Başarısız 4 / 37 | `Hdr_x265_psy_ve_renk_parametreleri_tek_dizgide_birlesir`, `Ilk_gecis_de_tek_x265_dizgesi_uretir`, `Psy_renk_ve_kullanici_x265_parametreleri_tek_dizgide_birlesir`, `Svtav1_parametreleri_de_tek_dizgide_birlesir` |
| M5 | birinci geçiş çıkışı birleştiriciyi atlıyor | Başarısız 1 / 37 | `Ilk_gecis_de_tek_x265_dizgesi_uretir` |
| M6 | `ExtraArgs` birleştirmeden **sonra** ekleniyor | Başarısız 2 / 37 | `Psy_renk_ve_kullanici_x265_parametreleri_tek_dizgide_birlesir`, `Svtav1_parametreleri_de_tek_dizgide_birlesir` |
| M7 | `Build` yeniden ölçen yolu çağırıyor (saf değil) | Başarısız 3 / 37 | `Arguman_uretimi_kodlayici_yoklamasi_dogurmaz`, `Arayuz_yolunda_kodlayici_yoklamasi_dogurulmaz`, `Isitilan_secenek_sonraki_arguman_uretiminde_onbellekten_okunur` |
| M8 | ölçen yol ısıtma arabirimini yok sayıyor | Başarısız 5 / 37 | `Yoklama_isinmasi_...`, `Kosucunun_arguman_uretimi_isitilmamis_kabiliyette_psy_bayragini_dusurmez`, `Isitilan_secenek_sonraki_arguman_uretiminde_onbellekten_okunur` |
| M9 | `EncodeArguments`tan `WarmPsychovisual` çağrısı silindi (J1) | Başarısız 3 / 37 | `Kosucunun_arguman_uretimi_isitilmamis_kabiliyette_psy_bayragini_dusurmez` (üç vaka) |
| M10 | `CachedPsychovisualArgs` ölçmeye çevrildi (J2) | Başarısız 4 / 37 | `Saf_psy_yolu_kabiliyeti_isitmaz`, `Arguman_uretimi_kodlayici_yoklamasi_dogurmaz`, `Arayuz_yolunda_kodlayici_yoklamasi_dogurulmaz`, `Isitilan_secenek_sonraki_arguman_uretiminde_onbellekten_okunur` |

Onunun onu da kırmızıya döndü. M9 ve M10 J1/J2'nin ölçüleridir: ısıtma çağrısı silinince ya
da saf yol ölçmeye çevrilince ilgili ölçü düşüyor.

### Ölçülmeyenler

- Bu turda `tools/VidShrink.Bench` koşturulmadı: teslim boyutu, VMAF ve ısıtmanın ölçüm
  aracındaki süre maliyeti **ölçülmedi**.
- Tam süit bu turda yerel olarak koşturulmadı (paralel çalışan üç ajan ölçüyü kararsız
  yapıyor); yalnız sözleşme filtresi koştu. Tam süit dalın CI koşumuna bırakıldı.
- `EncodeRunner.ConvertAsync` (GIF/dönüştürme) yolu psy/AQ argümanı üretmiyor, bu yüzden
  ısıtma oraya eklenmedi ve ölçülmedi.

### Doğrulama

`dotnet build VidShrink.sln -c Release`: 0 Uyarı, 0 Hata.
`dotnet test -c Release --filter "FfmpegArgumentsTests"`: Başarısız 0, Başarılı 37,
Atlanan 0, Toplam 37. Hiçbir assertion gevşetilmedi, hiçbir test `Skip`e alınmadı.

## T98 — Anahtar kare aralığı

Tarih: 2026-09-02. Ortam: Windows 11, ffmpeg 9.0-full. Ölçüm düzeneği `tools/VidShrink.Ab`
**değil** — T95'in aleti bu turda `main`de değildi; sayılar sözleşmeye özel bir düzenekten
(`.calisma/t98/olcum`, `VidShrink.Core` + `VidShrink.Ffmpeg`e bağlı) çıktı ve üç kapı elle
uygulandı: karşılaştırılan iki tarafın renk uzayı, akış sayısı ve teslim boyutu her satırda
raporlanıyor. Makine paylaşımlı: ölçüm boyunca beş ajan daha koşuyordu, bu yüzden **süreye
dayalı hiçbir sayı** (kodlama süresi, atlama gecikmesi) tek başına karar dayanağı sayılmadı.

Kaynaklar `.calisma/kaynak/parca-{1,2,3}.mkv`'nin ilk 20 saniyesinden `-c copy` ile kesilmiş,
yalnız video akışı taşıyan klipler (1920×1080@60, HDR PQ, `yuv420p10le`, `bt2020nc/smpte2084/bt2020`).
Ses akışı kesilerek `parca-1` ile `parca-2`/`parca-3` arasındaki akış sayısı farkı kaldırıldı.

### Sabit `-g` yerine aralık

`FfmpegArguments` artık her kodlamaya tek bir `-g` yazmıyor; bir **alt sınır**, bir **üst
sınır** yazıyor ve I-kareyi nereye koyacağına kodlayıcının sahne kesimi karar veriyor.

| Kodlayıcı | Üretilen |
|---|---|
| libx264, libvpx-vp9 | `-g <üst> -keyint_min <alt>` (scenecut varsayılan: açık) |
| libx265 | `-g <üst> -x265-params keyint=<üst>:min-keyint=<alt>:scenecut=40` |
| libsvtav1 | `-g <üst> -svtav1-params keyint=<üst>:scd=1` |
| donanım (nvenc/qsv/amf) | `-g <üst>` |

Alt sınır 1 saniye; HandBrake'in `min-keyint = fps` değeri (`encx264.c:386-391`,
`encx265.c:188-190`). Üst sınır harita yoksa 10 saniye, yine HandBrake (`keyint = 10*fps`).

### Üst sınır neye bağlı

Üst sınır **sahne haritasından** türüyor: haritanın bildirdiği ortalama sahne uzunluğu
`SceneMapMergeFactor = 2.8`'e bölünür, sonuç 5–10 saniye arasına kısılır.

**2,8 nereden geliyor.** T101 haritayı yer gerçeğine karşı ölçtü: 144,2–333,3 saniyelik
pencerede 28 gerçek kesim var, harita 10 sahne bildiriyor, yanlış pozitif yok. Kaçan 18
kesimin hepsi `SceneMap.DefaultThreshold = 0.2` eleğinin altında (0,112–0,199) kaldı. Yani
haritanın ortalama sahne uzunluğu sistematik olarak yaklaşık 2,8 kat uzun; üst sınırı ham
ortalamaya bağlamak tam da kaçırılan kesimlerin üstünden geçmek olurdu. Haritaya güvenilen
şey **sınırlar değil aralık**: yerleşim kararı kodlayıcının lookahead'inde ve o, haritanın
kaçırdığı kesimleri buluyor.

**5–10 saniyelik kıskaç nereden geliyor.** `parca-1-20sn` üzerinde üst sınır süpürmesi
(libx264, 2 geçiş, 20 MiB hedef, VMAF-NEG tüm klipte). Teslim boyutu süpürmenin tamamında
%0,3 içinde kaldığı için süpürme **eş boyutta kalite** olarak okunuyor:

| Üst sınır | Teslim MiB | Hedef oranı | mean | harmonic | p10 | I-kare | Gerçekleşen aralık |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 sn | 19,1083 | 0,95541 | 88,6373 | 86,4658 | 85,9327 | 13 | 1,539 sn |
| 5 sn | 19,0766 | 0,95383 | 88,8782 | 86,6907 | 86,6405 | 6 | 3,334 sn |
| 10 sn | 19,1347 | 0,95674 | 88,9513 | 86,7729 | 86,6743 | 3 | 6,667 sn |
| 20 sn | 19,1220 | 0,95610 | 88,9541 | 86,7749 | 86,7507 | 3 | 6,667 sn |

Kapılar: renk `bt2020nc/smpte2084/bt2020` → aynı, akış 1→1, `pix_fmt yuv420p10le` → aynı,
ölçü 1920×1080, `Comparable=True` (dört satırın dördünde).

Okunan iki şey:

- 2 sn ile 20 sn arasındaki p10 farkının (+0,818) **%87'si 5 saniyede** zaten alınmış
  (+0,708). Kıskacın alt ucu bu yüzden 5 saniye; daha kısası ölçülen kaybı geri getiriyor.
- 10 sn ile 20 sn **aynı üç I-kareyi aynı yerlere** koyuyor. Yani 10 saniyenin üstünde üst
  sınır artık bağlamıyor, karar tümüyle scenecut'a geçiyor. Kıskacın üst ucu bu yüzden 10
  saniye; büyütmenin ölçülebilir bir karşılığı yok.

Süpürme yalnız `parca-1-20sn` üzerinde tamamlandı. `parca-2` ve `parca-3` için üst sınır
süpürmesi **ölçülmedi** — makine paylaşımlı olduğu için süpürme kesilip zorunlu üç rejim
karşılaştırmasına (K3) geçildi.

**Bölen neden sabit yazılmadı.** 2,8 bir ayar sabiti değil, haritanın ölçülen duyarlılığı;
koda ölçüldüğü iki sayı olarak (`SceneMapGroundTruthCuts = 28`, `SceneMapReportedScenes = 10`)
girdi. Bunun nedeni somut: T105 şu anda `SceneMap.DefaultThreshold`'u ölçüye göre yeniden
koyuyor ve kaçan 18 kesimin 18'i tam o eleğe takılıyordu. Eşik düşerse harita daha çok sahne
bulur, ortalama sahne uzunluğu kendiliğinden kısalır — ve sabit bir bölen o düzeltmeyi **ikinci
kez** uygular, üst sınır olması gerekenin yarısına iner. Bölen tümden atılsaydı da işlemezdi:
T101'in penceresinde haritanın ham ortalaması 18,91 saniye, yani kıskacın 10 saniyelik üst
ucunun üstünde; bölensiz harita üst sınırı hiç oynatmazdı ve K2 lafta kalırdı. Seçilen yol
üçüncüsü: bölen kalıyor ama **ölçüldüğü eşiğe bağlanıyor**. `SceneMapThresholdOfRecord`
ile `SceneMap.DefaultThreshold` ayrışırsa `Az_bolme_duzeltmesi_olculdugu_esikte_kalir`
kırmızıya döner ve duyarlılık yeniden ölçülmeden geçilemez.

### K3 — üç rejim yan yana

`parca-1-20sn`, libx264, 2 geçiş, 20 MiB hedef, `preset=slow`, VMAF-NEG tüm klipte. Sahne
haritası bu klipte 2 sahne bildiriyor (ortalama 10 sn); duyarlılık düzeltmesiyle üst sınır
3,57 sn çıkıyor ve kıskacın alt ucuna, 5 saniyeye oturuyor. Üç rejim gerçekten üç ayrı
`-g` üretiyor: 120 / 600 / 300.

| Rejim | `-g` | `keyint_min` | Teslim MiB | Hedef oranı | mean | p10 | harmonic* | I-kare | Gerçekleşen aralık |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| sabit `-g fps*2` (bugünkü) | 120 | — | 19,1169 | 0,95584 | 88,6303 | 86,0416 | 86,4510 | 13 | 1,54 sn |
| HandBrake 1–10 sn | 600 | 60 | 19,1387 | 0,95693 | 88,9187 | 86,7402 | 86,7383 | 3 | 6,67 sn |
| **dinamik (harita, 1–5 sn)** | **300** | **60** | **19,1247** | **0,95623** | **88,9580** | **86,6304** | **86,7669** | **6** | **3,33 sn** |

\* Harmonik sütunu bilgi içindir, karara girmedi: **T106** bench'in harmonik ortalamasındaki
`max(x,1)` katlamasını soruşturuyor ve SVT-AV1 çıktısında 1'in altına düşen kare kümesi
bulundu. Karar `mean` ve `p10` üzerinden verildi.

Kapılar üç satırda da geçti: renk `bt2020nc/smpte2084/bt2020` → aynı, akış 1→1,
`pix_fmt yuv420p10le` → aynı, ölçü 1920×1080, `Comparable=True`.

Teslim boyutu üç rejimde %0,11 içinde. Yani 2 geçişte kazanç boyuta değil kaliteye gidiyor;
T102'nin CRF yolunda gördüğü %24,5'lik küçülme burada görünmüyor, görünmesi de beklenmiyor.

### K4 — atlama gecikmesi

K3'ün üç çıktısı üzerinde, dosya başına **120** rastgele nokta, her noktada
`ffmpeg -ss T -i dosya -frames:v 1 -f null -`. Süreç açılış tabanı aynı koşumda ölçüldü
(`nullsrc`, çözme yok): **30,5 ms**. Tablodaki net değer bu taban düşülmüş hâlidir.

| Rejim | I-kare | Gerçekleşen aralık | p50 | p90 | ortalama | p50 − taban |
|---|---:|---:|---:|---:|---:|---:|
| sabit 2 sn | 13 | 1,54 sn | 158,4 ms | 218,7 ms | 162,7 ms | **128,0 ms** |
| dinamik 1–5 sn | 6 | 3,33 sn | 219,8 ms | 356,5 ms | 236,5 ms | **189,4 ms** |
| HandBrake 1–10 sn | 3 | 6,67 sn | 358,7 ms | 558,5 ms | 379,9 ms | **328,3 ms** |

**Makine paylaşımlıydı** (koşum boyunca altı ajan); bu damga tablodaki bütün süre sayıları
için geçerlidir. Kalite ve boyut sayıları süreye bağlı değil, o damga onlara işlemez. Gecikme
sıralaması I-kare yoğunluğuyla birebir uyumlu ve üç ölçütte de (p50, p90, ortalama) aynı
sırada; yön bu yüzden kararlı sayıldı, mutlak milisaniye değeri sayılmadı.

Karar: HandBrake rejimi p10'da sabit rejime göre +0,699 kazandırıyor ama atlama bedelini
**2,6 katına** çıkarıyor. Dinamik rejim aynı kazancın %84'ünü (+0,589) alıyor ve bedeli
1,5 katta tutuyor; üstelik ortalamada üçünün en iyisi. Aralık bu yüzden kısaltıldı: üst
sınırın kıskacı 10 saniyeye kadar açık ama harita kısa sahne bildirdiğinde 5 saniyeye
iniyor ve gecikme oraya değil buraya yaslanıyor.

### K5 — CRF yolunda maxrate

`parca-1-20sn`, libx264, CRF 23, tek geçiş, hedef 20 MiB. Tek fark `-maxrate`/`-bufsize`
çiftinin varlığı; başka hiçbir argüman değişmedi.

| VBV | Teslim MiB | Hedef oranı | mean | p10 |
|---|---:|---:|---:|---:|
| var (2× / 4×) | 14,7310 | 0,73655 | 86,9770 | 84,9990 |
| yok | 15,3120 | 0,76560 | 87,2870 | 85,5980 |
| yok − var | +0,5810 (+%3,9) | +0,02905 | +0,3100 | +0,5990 |

**Yargı: ölçüldü, gerekli — kalıyor.** Sözleşmenin öngörüsü doğrulandı, kuyruk açığının bir
parçası gerçekten orada: kaldırınca p10 +0,599 geliyor ve bu ortalamadaki kazancın iki katı.
Ama aynı CRF'te dosya %3,9 büyüyor. HandBrake bunu göze alabiliyor çünkü onun CRF'i ucu açık
bir kalite kipi; bizde CRF **hedefe inen** bir kip — `PlanCalculator`'ın doldurma politikası
bandın ortasına nişan alan bir CRF seçiyor (`PlanCalculator.cs:280-284`). Sistematik %3,9
o bandı yer. Donanım yoluna dokunulmadı.

Ara bir değer (2× ile tümden kaldırma arasında bir gevşetme) **ölçülmedi**; kuyruk açığı için
en güçlü açık aday bu ve ayrı bir tur hak ediyor.

### K6 — tepe eğrisi

Sözleşmenin sorduğu şikâyet durumu taban oranı ≈4,7. Klibin 20 MiB hedefindeki oranı 10,236
çıktığı için hedef 9,2 MiB'a indirilerek **4,636** oranına inildi; her iki oran da ölçüldü.
`av1_nvenc`, 2 geçiş, tek fark `-maxrate`/`-bufsize` çarpanı.

| Taban oranı | Tepe | Teslim MiB | Hedef oranı | mean | p10 |
|---:|---:|---:|---:|---:|---:|
| 4,636 | 1,02 | 8,895 | 0,9669 | 82,372 | 69,812 |
| 4,636 | 1,10 | 8,594 | 0,9341 | 82,290 | 71,241 |
| 4,636 | 1,50 | 8,723 | 0,9482 | 82,609 | **73,477** |
| 10,236 | 1,02 | 19,637 | 0,9818 | 89,334 | 84,211 |
| 10,236 | 1,10 | 18,902 | 0,9451 | 89,144 | 83,705 |
| 10,236 | 1,50 | 19,128 | 0,9564 | 89,318 | 84,108 |

Sözleşmenin sorusunun cevabı: **4,6 oranında tepeyi açmak boyutu aşırmıyor** — üç değerin
üçü de hedefin altında, üstelik 1,50 (0,9482) 1,02'den (0,9669) küçük çıkıyor. Ve kazanç
var: p10 69,812 → 71,241 → 73,477, üç noktada tek yönlü, toplam **+3,665**. 10,2 oranında
aynı sıralama yok ve fark 0,1 mertebesinde — yani VBV yalnız bütçe sıkışıkken bağlıyor,
bu da mekanizmayla tutarlı.

**Karar: eğri değişmedi — ölçüldü, değişmedi.** Gerekçe: kazancı almanın yolu açılma
noktasını indirmek değil `TightPeakFactor`'ı yükseltmek. Açılma noktasını ölçülen 4,6'ya
çekmek 4,636'da çarpanı 1,0204 yapardı; ölçülen kazancın hemen hiçbiri gelmezdi, yani
göstermelik bir değişiklik olurdu. `TightPeakFactor` ise bütün düşük oranlı planları
değiştirir ve güvenli aralığı bu belgede birden çok kaynakla kurulmuştu; tek klipte,
tek kodlayıcıda ölçülen bir sonuçla oynatılmaz. Bulgu bu turun en güçlü açık ipucudur
ve kendi turunu hak ediyor: **eğrinin şekli ölçümle ters görünüyor** — aşma kanıtının
geldiği yüksek oranda (11,4×) geniş açılıyor, açmanın güvenli ve kazançlı ölçüldüğü
düşük oranda (4,6×) 1,02'ye kilitli.

### K8 — boyut garantisi

Anahtar kare değişikliği boyutu **sistematik olarak aşırmıyor**. İki geçişte üç rejimin
teslim oranı 0,95584 / 0,95623 / 0,95693 — aralarındaki fark %0,11 ve üçü de hedefin
altında. CRF yolunda uzun aralık dosyayı **küçültüyor** (T102: %24,5), yani o yönde de
aşma riski yok. Aşma riski taşıyan tek aday K5'teki VBV kaldırma idi (+%3,9); ölçüldü ve
tam bu nedenle uygulanmadı. Donanım yolunda üst sınır 5 saniyede tutuldu ve tepe eğrisine
dokunulmadı, dolayısıyla bu belgenin önceki aşma kanıtı geçerliliğini koruyor.

### K9 — mutasyon tablosu

Düzenek her mutasyonu üretim kaynağına uyguluyor, `FfmpegArgumentsTests` filtresini
koşuyor, kaynağı geri alıyor. Taban yeşil koşum: **Başarısız 0, Başarılı 58, Atlanan 0**.
Hiçbir mutasyon testin kendi sabitini değiştirmiyor.

| # | Mutasyon | Sonuç | Kırılan ölçü (ilk üç) |
|---|---|---|---|
| N1 | aralık tek sayıya geri çevrildi, alt sınır silindi | Başarısız 4 / 58 | `Anahtar_kare_araligi_alt_ve_ust_siniri_ayri_yazar`, `Parca_argumanlari_tam_kodlamadan_yalniz_uc_baslikta_ayrilir` |
| N2 | üst sınır haritayı yok sayıyor | Başarısız 5 / 58 | `Ust_sinir_ortalama_sahne_uzunluguyla_birlikte_uzar`, `Haritanin_az_bolmesi_ust_sinirdan_dusulur`, `Uzun_ust_sinir_kesimsiz_kaynakta_daha_az_anahtar_kare_uretir` |
| N3 | az bölme düzeltmesi kaldırıldı (28/10 → 10/10) | Başarısız 2 / 58 | `Az_bolme_duzeltmesi_T101_penceresinde_gercek_ortalamayi_uretir`, `Haritanin_az_bolmesi_ust_sinirdan_dusulur` |
| N4 | düzeltmenin eşik bağı koparıldı (0,2 → 0,15) | Başarısız 1 / 58 | `Az_bolme_duzeltmesi_olculdugu_esikte_kalir` |
| N5 | x265 sahne kesimi kapatıldı (`scenecut=0`) | Başarısız 2 / 58 | `Sahne_kesimi_kodlayicinin_kendi_diliyle_acik_yazilir` |
| N6 | SVT-AV1 sahne kesimi kapatıldı (`scd=0`) | Başarısız 2 / 58 | `Sahne_kesimi_kodlayicinin_kendi_diliyle_acik_yazilir` |
| N7 | donanım üst sınırı yazılım varsayılanına eşitlendi | Başarısız 3 / 58 | `Donanimda_ust_sinir_haritadan_etkilenmez` |
| N8 | CRF yolundaki VBV tavanı kaldırıldı | Başarısız 2 / 58 | `Crf_yolunda_VBV_tavani_bit_hiziyla_olcekleniyor` |
| N9 | CRF VBV arabelleği tavana eşitlendi (4× → 2×) | Başarısız 2 / 58 | `Crf_yolunda_VBV_tavani_bit_hiziyla_olcekleniyor` |
| N10 | tepe eğrisi açılma noktası 6,0 → 2,0 | Başarısız 8 / 58 | `Donanim_tepe_carpani_taban_oraninda_beklenen_degeri_uretir`, `Tepe_carpani_olculen_guvenli_degerlerin_disina_cikmaz` |

Onunun onu da kırmızıya döndü. İki ölçü ffmpeg gerektiriyor ve `[FfmpegFact]` ile
işaretli; ikisi de koşuyor, atlanmıyor. Süitte `Skip` yok, atlanan test yok.

### Ölçülmeyenler

- **`tools/VidShrink.Ab` kullanılamadı** — T95 bu tur boyunca `main`e inmedi. Renk kapısı,
  akış sayısı kapısı ve boyut karşılaştırılabilirliği elle uygulandı ve her satırda
  raporlandı; aletin sağladığı **duyarlılık kanıtı** (ölçünün gerçek bir farkı ayırt
  edecek çözünürlükte olduğunun gösterimi) bu turda **yok**.
- K3, K5 ve K6 **tek klipte** (`parca-1-20sn`) ölçüldü, genellenmedi. `parca-2` ve
  `parca-3` üzerinde hiçbir rejim, maxrate ya da tepe koşumu **ölçülmedi**.
- Üst sınır süpürmesi yalnız `parca-1-20sn`'de tamamlandı; `parca-2`/`parca-3` **ölçülmedi**.
- CRF yolunda VBV'nin **ara değerleri ölçülmedi**; yalnız 2×/4× ile tümden kaldırma
  karşılaştırıldı.
- `TightPeakFactor` yükseltmenin boyut etkisi **ölçülmedi**; K6 kararı bu yüzden
  değişiklik değil.
- Donanım yolunda gerçekleşen aralık ve atlama gecikmesi **ölçülmedi**; oradaki 5 saniye
  ölçüm değil, atlama bütçesi kararıdır ve mekanizma gerekçesi `-h encoder=hevc_nvenc`
  çıktısına dayanır.
- İş parçacığı sayısı **sabitlenmedi** (`-threads` verilmedi). Üst sınır süpürmesi
  sabitlemeden koşmuştu; sonraki koşumları sabitlemek iki tabloyu kıyaslanamaz yapardı.
  Sonuç: bütün süre sayıları paylaşımlı makine damgalıdır, kalite ve boyut sayıları değil.
- Tam süit koşturulmadı; sözleşme yalnız `FfmpegArgumentsTests` filtresini istiyor.


### Donanım ayrı bir mekanizma

NVENC sahne kesimini yalnız lookahead açıkken uyguluyor (`ffmpeg -h encoder=hevc_nvenc`:
`-no-scenecut ... When lookahead is enabled`) ve bu proje lookahead açmıyor. Donanımda üst
sınır bir üst sınır değil, gerçekleşen aralığın kendisi: dosyadaki I-kare sıklığı ve atlama
bedeli doğrudan o sayı. Bu yüzden donanım üst sınırı içerikten değil atlama bütçesinden
geliyor ve `HardwareKeyframeCeilingSeconds = 5.0` olarak yazılım varsayılanının altında
tutuldu. Donanım yolunda sahne haritası üst sınırı **oynatmıyor**.
