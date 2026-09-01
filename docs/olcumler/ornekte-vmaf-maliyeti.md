# Örnek pencerelerde algılanan kalite maliyeti

Tarih: 2026-09-01. Ortam Windows 11, ffmpeg 9.0-full. Bütün süre ve kalite sayıları `tools/VidShrink.Bench` çıktısından alınmıştır. `ComplexityProbe`un mevcut tam ölçek CRF 23 örneği Matroska içinde tutulup silinmeden önce kaynak pencereyle ölçülür; kalite için ikinci bir örnek kodlama yapılmaz.

## Köprü ve duvar saati

`IQualityMeasurement` Core katmanındadır. Ffmpeg katmanındaki `QualityMeasurement`, `QualityMeter`ı uygular; `ProbeResult` hem eski `ComplexityProfile`ı hem pencere sonuçlarını taşır. Plan kararları bu sözleşmede değişmedi.

| Kaynak | Süre sn | Çözünürlük | Kalite kapalı ms | Kalite açık ms | Fark ms | Fark % | Ölçülen pencere |
|---|---:|---:|---:|---:|---:|---:|---:|
| Kısa SDR hareket | 8,05 | 1280×720@60 | 3708 | 5602 | 1894 | 51,04832757935358 | 2 |
| Kısa 4K SDR desen | 8 | 3840×2160@30 | 13611 | 21363 | 7752 | 56,95152413192195 | 2 |
| Gerçek HDR oyun kaynağı | 1036,165911 | 1920×1080@60 | 6456 | 12381 | 5925 | 91,78118021225761 | 3 |

İddia doğrulanmadı: libvmaf ölçümü “neredeyse bedava” değildir. Bu üç koşumda duvar saati farkı 1894–7752 ms'dir. HDR satırında referansın PQ/BT.2020 olarak çözülüp örnekle aynı açık renk uzayına normalize edilmesi dahil ek maliyet 5925 ms'dir; kısa SDR satırındaki 1894 ms'den 4031 ms fazladır. Kaynakların çözünürlüğü, FPS'i ve pencere sayısı farklı olduğundan bu farkın tamamı HDR renk dönüşümüne atfedilemez; yalnız ölçülen uçtan uca farktır.

### Bütçe kararı

Mevcut örnek süreç zaman aşımı pencere başına 90000 ms'dir. En yavaş kalite-açık tam yoklama 21363 ms, en yavaş tek kalite penceresi 7915 ms sürdü; ikisi de mevcut kapının içindedir. Bu nedenle pencere sayısı, pencere süresi veya ölçüm çözünürlüğü sınırı konmadı ve sınır bozunumu **ölçülmedi**. Kullanıcı iptali kalite servisine taşınır; iptal dışındaki ölçüm hatası profili düşürmez ve kalite alanını boş bırakır.

## Yaklaşık 3× taban oranında tepe-kalite

İki 120 saniyelik hareketli SDR kaynak, çözünürlük/FPS düşürme ve kalibrasyon kapalıyken kodlandı. Dar koşum diz eğrisinin 1,02 değeridir; geniş koşum son argüman olarak 1,50'dir. Kalite klibin tamamında ölçüldü.

| Kaynak | Taban oranı | Tepe | Hedef MiB | Teslim MiB | VMAF-NEG mean | Harmonic | p10 |
|---|---:|---:|---:|---:|---:|---:|---:|
| 1280×720@60 | 3,0424929178470255 | 1,02 | 16 | 15,817834854125977 | 90,05607107861103 | 90,02760819868892 | 88,0500133 |
| 1280×720@60 | 3,0424929178470255 | 1,50 | 16 | 15,741707801818848 | 90,06902001666657 | 90,05225934724531 | 88,4909184 |
| 1920×1080@30 | 3,0348360655737705 | 1,02 | 22 | 21,669864654541016 | 90,37059560666665 | 90,3311891343612 | 88,32392300000001 |
| 1920×1080@30 | 3,0348360655737705 | 1,50 | 22 | 21,34074306488037 | 90,47315896361098 | 90,4520939149023 | 88,69381940000001 |

Geniş tepe iki kaynakta da hedefi aşmadı. 720p60'da geniş eksi dar: mean +0,01294893805554, harmonic +0,02465114855639, p10 +0,4409051; 1080p30'da mean +0,10256335694433, harmonic +0,1209047805411, p10 +0,3698964. Bu örneklerde 1,02 özellikle p10 kalitesi bırakmıştır; bulgu T89'a devredilir. `FfmpegArguments` sabitleri değiştirilmedi.

## Başarısızlık davranışı

`libvmaf` veya `zscale` yoksa kalite servisi çalışmaz. Servis hata atarsa ya da “karşılaştırılamaz” dönerse `ComplexityProfile` ölçülmüş kalır, yalnız `QualityMeasurements` boş olur. Ölçülmeyen bir kalite için sayı üretilmez.
