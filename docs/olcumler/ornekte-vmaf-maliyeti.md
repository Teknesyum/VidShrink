# Örnek pencerelerde algılanan kalite maliyeti

Tarih: 2026-09-01. Ortam Windows 11, ffmpeg 9.0-full. Bütün süre ve kalite sayıları `tools/VidShrink.Bench` çıktısından alınmıştır. `ComplexityProbe`un mevcut tam ölçek CRF 23 örneği Matroska içinde tutulup silinmeden önce kaynak pencereyle ölçülür; kalite için ikinci bir örnek kodlama yapılmaz.

## Köprü ve duvar saati

`IQualityMeasurement` Core katmanındadır. Ffmpeg katmanındaki `QualityMeasurement`, `QualityMeter`ı uygular; `ProbeResult` hem eski `ComplexityProfile`ı hem pencere sonuçlarını taşır. Tur 1'de tam örneğin Matroska, yarım örneğin ham H.264 sayılması plan girdisini değiştirmişti. Tur 2'de iki taraf da Matroska dosya baytı ölçüyor. Aynı 2 saniyelik kodlamada Matroska ek yükü düşük karmaşıklıkta 5434 → 6875 bayt (+%26,52), hareketli içerikte 734368 → 735930 bayt (+%0,21) ölçüldü; aynı birim kullanıldığı için oran hesabında tek tarafa binmiyor.

| Kaynak | Süre sn | Çözünürlük | Kalite kapalı ms | Kalite açık ms | Fark ms | Fark % | Ölçülen pencere |
|---|---:|---:|---:|---:|---:|---:|---:|
| Kısa SDR hareket | 8,05 | 1280×720@60 | 3483,5 | 5184,5 | 1701 | 48,83 | 2 |
| Kısa 4K SDR desen | 8 | 3840×2160@30 | 13107 | 20578 | 7471 | 57,00 | 2 |
| Gerçek HDR oyun kaynağı | 1036,17 | 1920×1080@60 | 5848,5 | 11577,5 | 5729 | 97,96 | 3 |

İddia doğrulanmadı: libvmaf ölçümü “neredeyse bedava” değildir. Bu üç koşumda duvar saati farkı 1701–7471 ms'dir. Her durum iki sırada (`kapalı→açık` ve `açık→kapalı`) çalıştırıldı; tablodaki değerler iki koşumun ortalamasıdır, böylece ikinci koşumun dosya önbelleği üstünlüğü tek tarafa yazılmadı.

Aynı HDR kaynak, aynı 517,083–519,083 saniye penceresinde yalnız referans çözme ölçüldü. Normalizasyon kapalı ortalama 301,5 ms, açık 302,5 ms; ölçülen fark **1,0 ms / %0,33**. Tekrarlar kapalı 312/291 ms, açık 305/300 ms idi. Bu tek pencere ve iki tekrarın ölçümüdür; daha genel HDR maliyeti **ölçülmedi**.

### Bütçe kararı

Mevcut örnek süreç zaman aşımı pencere başına 90000 ms'dir. En yavaş kalite-açık tam yoklama 20578 ms, en yavaş tek kalite penceresi 7534 ms sürdü; ikisi de mevcut kapının içindedir. Bu nedenle pencere sayısı, pencere süresi veya ölçüm çözünürlüğü sınırı konmadı ve sınır bozunumu **ölçülmedi**. Kullanıcı iptali kalite servisine taşınır; iptal dışındaki ölçüm hatası profili düşürmez ve kalite alanını boş bırakır. `RunAsync` ve `RunDetailedAsync` varsayılanı kaliteyi kapalı tutar; T89 açıkça seçene kadar uygulama bu ek süreyi ödemez.

## Yaklaşık 3× taban oranında tepe-kalite

İki 120 saniyelik hareketli SDR kaynak, çözünürlük/FPS düşürme ve kalibrasyon kapalıyken kodlandı. Dar koşum diz eğrisinin 1,02 değeridir; geniş koşum son argüman olarak 1,50'dir. Kalite klibin tamamında ölçüldü.

| Kaynak | Taban oranı | Tepe | Hedef MiB | Teslim MiB | VMAF-NEG mean | Harmonic | p10 |
|---|---:|---:|---:|---:|---:|---:|---:|
| 1280×720@60 | 3,0424929178470255 | 1,02 | 16 | 15,817834854125977 | 90,05607107861103 | 90,02760819868892 | 88,0500133 |
| 1280×720@60 | 3,0424929178470255 | 1,50 | 16 | 15,741707801818848 | 90,06902001666657 | 90,05225934724531 | 88,4909184 |
| 1920×1080@30 | 3,0348360655737705 | 1,02 | 22 | 21,669864654541016 | 90,37059560666665 | 90,3311891343612 | 88,32392300000001 |
| 1920×1080@30 | 3,0348360655737705 | 1,50 | 22 | 21,34074306488037 | 90,47315896361098 | 90,4520939149023 | 88,69381940000001 |

Geniş tepe iki kaynakta da hedefi aşmadı. 720p60'da geniş eksi dar: mean +0,013, harmonic +0,025, p10 +0,441; 1080p30'da mean +0,103, harmonic +0,121, p10 +0,370. Tek koşum ve gürültü tahmini olmadığı için 1,02'nin kalite bıraktığı sonucuna varılmadı; yalnız gözlenen fark T89'a devredilir. `FfmpegArguments` sabitleri değiştirilmedi.

## Başarısızlık davranışı

`libvmaf` veya `zscale` yoksa kalite servisi çalışmaz. Servis hata atarsa ya da “karşılaştırılamaz” dönerse `ComplexityProfile` ölçülmüş kalır, yalnız `QualityMeasurements` boş olur. Ölçülmeyen bir kalite için sayı üretilmez.

## Test ve mutasyon kanıtı

Köprü çağrısı geçici kaldırıldığında `DetailedProbeExposesWindowQualityThroughCoreContract` başarısız oldu. Ayrı örnek ofseti geçici olarak referans ofsetine bağlandığında `ReferenceAndSampleMayUseDifferentWindowOffsets` VMAF 22,38367795 ile başarısız oldu. Her iki düzeltme geri getirildikten sonra sözleşme filtresi yeniden çalıştırıldı. Var olan assertion gevşetilmedi, test Skip'e alınmadı.

Son doğrulama: sözleşme filtresi 14 başarılı / 0 başarısız; tam `dotnet test -c Release` 963 başarılı / 0 başarısız.

Tur 2 mutasyonu: yarım örnek formatı yeniden `h264`, kalite varsayılanı yeniden açık ve pencere dizisi `Take(1)` yapıldığında üç bağımsız test de başarısız oldu. Düzeltmeler geri getirildi; tur 2 sözleşme filtresi 16 başarılı / 0 başarısız, kesintisiz tam süit 974 başarılı / 0 başarısız sonuçlandı.
