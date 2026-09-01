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
- Teknik borç: `RunOptionProbe`, saf görünen `Build()` yolundan senkron ffmpeg G/Ç'si başlatabiliyor ve kodlayıcı başına dört saniyeye kadar bloklayabiliyor. Bu turda ayrı önbellekli/asenkron yoklama katmanına taşınmadı.

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

Eski `Donanim_tepe_carpani_boyut_guvencesi_tavanini_asmaz` **kaldırıldı değil, olduğu gibi duruyor**; içindeki `Assert.InRange(factor, TightPeakFactor, HardwarePeakCeiling)` hâlâ `Clamp` çıkışını `Clamp` sınırlarıyla karşılaştırdığı için yanlışlanamaz. Assertion gevşetmemek için silinmedi; iddiayı taşıyan ölçüler yukarıdaki üçü.

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
- `ExtraArgs` `PsychovisualAndColorArgs` birleştiricisinden geçmiyor: `-x265-params` içeren bir `ExtraArgs` birleşik dizgeyi yine sessizce ezer.
- `RunOptionProbe` hâlâ `Build()` yolundan senkron ffmpeg süreci doğurabiliyor. Bu turda yalnız **ilk çağrının yeri** düzeltildi; ayrı önbellekli/asenkron yoklama katmanına taşınmadı.

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
