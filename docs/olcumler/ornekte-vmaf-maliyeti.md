# Örnek pencerelerde algılanan kalite maliyeti

Tarih: 2026-09-01. Ortam Windows 11, ffmpeg 9.0-full. Bütün süre ve kalite sayıları `tools/VidShrink.Bench` çıktısından alınmıştır. `ComplexityProbe`un mevcut tam ölçek CRF 23 örneği Matroska içinde tutulup silinmeden önce kaynak pencereyle ölçülür; kalite için ikinci bir örnek kodlama yapılmaz.

## Köprü ve duvar saati

`IQualityMeasurement` Core katmanındadır. Ffmpeg katmanındaki `QualityMeasurement`, `QualityMeter`ı uygular; `ProbeResult` hem eski `ComplexityProfile`ı hem pencere sonuçlarını taşır. Yoklamanın bayt birimi üç turda iki kez değişti; bugünkü hali ve ölçülen farkı [Bayt birimi](#bayt-birimi) bölümündedir.

| Kaynak | Süre sn | Çözünürlük | Kalite kapalı ms | Kalite açık ms | Fark ms | Fark % | Ölçülen pencere |
|---|---:|---:|---:|---:|---:|---:|---:|
| Kısa SDR hareket | 8,05 | 1280×720@60 | 3483,5 | 5184,5 | 1701 | 48,83 | 2 |
| Kısa 4K SDR desen | 8 | 3840×2160@30 | 13107 | 20578 | 7471 | 57,00 | 2 |
| Gerçek HDR oyun kaynağı | 1036,17 | 1920×1080@60 | 5848,5 | 11577,5 | 5729 | 97,96 | 3 |

İddia doğrulanmadı: libvmaf ölçümü “neredeyse bedava” değildir. Bu üç koşumda duvar saati farkı 1701–7471 ms'dir. Her durum iki sırada (`kapalı→açık` ve `açık→kapalı`) çalıştırıldı; tablodaki değerler iki koşumun ortalamasıdır, böylece ikinci koşumun dosya önbelleği üstünlüğü tek tarafa yazılmadı.

Aynı HDR kaynak, aynı 517,083–519,083 saniye penceresinde yalnız referans çözme ölçüldü. Normalizasyon kapalı ortalama 301,5 ms, açık 302,5 ms; ölçülen fark 1,0 ms. Tekrarlar kapalı 312/291 ms, açık 305/300 ms idi — kapalı koşumun kendi tekrar yayılımı 21 ms, yani ölçülen farkın yirmi katı. **Fark gürültünün içinde kalmıştır**: bu ölçüm ne normalizasyonun referans çözmeye maliyet eklediğini ne de eklemediğini göstermeye yeter. Daha genel HDR maliyeti **ölçülmedi**.

### Bütçe kararı

Mevcut örnek süreç zaman aşımı pencere başına 90000 ms'dir. En yavaş kalite-açık tam yoklama 20578 ms, en yavaş tek kalite penceresi 7534 ms sürdü; ikisi de mevcut kapının içindedir. Bu nedenle pencere sayısı, pencere süresi veya ölçüm çözünürlüğü sınırı konmadı ve sınır bozunumu **ölçülmedi**. Kullanıcı iptali kalite servisine taşınır; iptal dışındaki ölçüm hatası profili düşürmez ve kalite alanını boş bırakır. `RunAsync` ve `RunDetailedAsync` varsayılanı kaliteyi kapalı tutar; T89 açıkça seçene kadar uygulama bu ek süreyi ödemez.

## Bayt birimi

Yoklamada bayt üreten üç taraf var: tam ölçek örneği, yarım ölçek örneği ve hareket örneği. Tur 3'te üçü de **tek muxer** (`ComplexityProbe.SampleFormat`, Matroska) ve **tek sayma yolu** (`MeasureSampleBytes`, dosya uzunluğu) üzerinden geçiyor. `SampleWindowAsync`ın yedek yolu da aynı yardımcıyı kullandığı için split'in başarısız olduğu pencereler artık karışık birim üretmiyor. Ham akış baytını stderr'den okuyan `ParseVideoBytes` kaldırıldı.

Ölçüm düzeneği: 1280×720@60 lavfi kaynakları, `-ss 2 -t 2`, `-c:v libx264 -crf 23 -preset veryfast`; hareket tarafı `-vf fps=30`. Aynı düzenek iki kez koşuldu, iki koşumun bütün bayt sayıları **bire bir aynı** çıktı (tekrar yayılımı 0 bayt).

### Konteyner ek yükü

| Kaynak | Ham H.264 ES bayt | Matroska bayt | Fark bayt | Fark % |
|---|---:|---:|---:|---:|
| Düşük karmaşıklık (`color=gray`) | 5430 | 6871 | 1441 | +26,54 |
| Orta (`smptebars`) | 6595 | 8040 | 1445 | +21,91 |
| Yüksek hareket (`testsrc2`) | 850339 | 851901 | 1562 | +0,18 |

Ölçülen üç kaynakta ek yük içerikten neredeyse bağımsız, yaklaşık sabit bir toplamdır (1441–1562 bayt). Bu yüzden yüzde olarak yalnız küçük pencerelerde büyür. Üç kaynak dışında sınanmadı.

### Birimin `MotionExponent` girdisine etkisi

`ComplexityProfile.FromProbe` içinde `MotionExponent = Clamp(Log2(halfFpsBppf/fullScaleBppf), 0, 1)`. Aşağıdaki sütunlar aynı ölçülen bayt sayılarından türetilen `Log2` değeridir; kelepçe uygulanmamıştır.

`main`in kendisi bu oranda **iki ayrı birim** kullanıyordu: pay `-f null -` çıktısının stderr'inden KiB'ye yuvarlanarak okunuyordu (`ParseVideoBytes`), payda ise `-f h264` ile yazılmış ham ES **dosyasının** tam uzunluğuydu (`ComplexityProbe.cs:449,472` — `origin/main`). Yani `main` de temiz bir taban değildi; aşağıdaki sütun onun bu iki yolunu birebir taklit eder.

| Kaynak | `main` (pay KiB yuvarlamalı / payda ham ES dosyası) | Tur 2 (pay yuvarlamalı ham / payda mkv) | Tur 3 (üç eksen de Matroska) | Ham ES, iki taraf da yuvarlamasız |
|---|---:|---:|---:|---:|
| Düşük karmaşıklık (`color=gray`) | 0,178 | −0,161 | 0,322 | 0,242 |
| Orta (`smptebars`) | 0,313 | 0,027 | 0,313 | 0,245 |
| Yüksek hareket (`testsrc2`) | 0,622 | 0,619 | 0,623 | 0,623 |

`CodecModel.DefaultMotionExponent = 1 − 0,75 = 0,25`; `PlanCalculator.cs:182` eşiği `<= DefaultMotionExponent`. Ölçülen üç kaynakta eşiğin hangi tarafına düşüldüğü:

- **Tur 2** iki düşük bit hızlı kaynağı da eşiğin altına indiriyordu (−0,161, kelepçeyle 0,0; ve 0,027). `main`de `smptebars` 0,313 ile üstteydi. KRİTİK olarak bildirilen davranış budur.
- **Tur 3** `smptebars` ve `testsrc2`yi `main` ile aynı tarafta bırakıyor, ama `color=gray`i **karşı tarafa geçiriyor**: `main` 0,178 (altta, FPS yarılama dalı), tur 3 0,322 (üstte). Bu, gizlenecek bir şey değil — birim seçiminin bu rejimde kararı belirlediğinin ölçülmüş kanıtıdır.
- Temiz birimlerle bakıldığında ayrım daha da nettir: iki taraf da yuvarlamasız **ham ES** olduğunda iki düşük bit hızlı kaynak da 0,25'in hemen **altında** (0,242 ve 0,245); iki taraf da **Matroska** olduğunda ikisi de **üstünde** (0,322 ve 0,313). Konteynerin yaklaşık sabit ek yükü, kare sayısı yarı olan hareket tarafına kare başına iki kat bindiği için oranı yukarı iter.
- `main`in yuvarlaması tek başına da oynatıyordu: aynı kaynaklarda pay yuvarlanınca `color=gray` 0,242'den 0,178'e (−0,064), `smptebars` 0,245'ten 0,313'e (+0,068) gidiyor. Yuvarlamanın yönü içeriğe göre değişiyor.

Bu üç kaynağın gözlemidir; genel durumda hangi kaynakların eşik tarafını değiştirdiği **ölçülmedi**. Karar T89'a aittir: bu sözleşme `PlanCalculator` eşiklerine ve `CodecModel` sabitlerine dokunmuyor.

Ham ES sütunu birim adayı olarak düşünüldü ve **seçilmedi**. Gerekçe ölçülen tek bir olgudur: ham `.h264` dosyasında zaman damgası yok; 640×360@60 kaynaktan üretilen böyle bir dosya için `ffprobe` `r_frame_rate=120/1` ve `nb_frames=N/A` döndürüyor. Kalite ölçümü aynı tam ölçek dosyasını `QualityMeter`a örnek olarak veriyor ve `QualityMeter` karşılaştırılabilirlik kararında `MediaInfo`ya bakıyor. Ham akışın bu yolu **gerçekten bozup bozmadığı ölçülmedi**; sınanmamış bir risk uğruna çalışan bir yol değiştirilmedi.

Kalan sapma T89'a devreder: Matroska birimi düşük bit hızlı iki kaynakta `Log2` değerini yuvarlamasız ham ölçüye göre +0,068 ve +0,079 yukarı taşıyor; yüksek hareketli kaynakta kayma −0,0001'dir. Bu sözleşme `PlanCalculator` eşiklerine dokunmuyor.

## Yaklaşık 3× taban oranında tepe-kalite

İki 120 saniyelik hareketli SDR kaynak, çözünürlük/FPS düşürme ve kalibrasyon kapalıyken kodlandı. Dar koşum diz eğrisinin 1,02 değeridir; geniş koşum son argüman olarak 1,50'dir. Kalite klibin tamamında ölçüldü.

| Kaynak | Taban oranı | Tepe | Hedef MiB | Teslim MiB | VMAF-NEG mean | Harmonic | p10 |
|---|---:|---:|---:|---:|---:|---:|---:|
| 1280×720@60 | 3,042 | 1,02 | 16 | 15,818 | 90,056 | 90,028 | 88,050 |
| 1280×720@60 | 3,042 | 1,50 | 16 | 15,742 | 90,069 | 90,052 | 88,491 |
| 1920×1080@30 | 3,035 | 1,02 | 22 | 21,670 | 90,371 | 90,331 | 88,324 |
| 1920×1080@30 | 3,035 | 1,50 | 22 | 21,341 | 90,473 | 90,452 | 88,694 |

Tablo okunabilirlik için üç haneye yuvarlandı; aşağıdaki farklar yuvarlanmamış değerlerden hesaplanmıştır.

Geniş tepe iki kaynakta da hedefi aşmadı. 720p60'da geniş eksi dar: mean +0,013, harmonic +0,025, p10 +0,441; 1080p30'da mean +0,103, harmonic +0,121, p10 +0,370. Tek koşum ve gürültü tahmini olmadığı için 1,02'nin kalite bıraktığı sonucuna varılmadı; yalnız gözlenen fark T89'a devredilir. `FfmpegArguments` sabitleri değiştirilmedi.

## Başarısızlık davranışı

`libvmaf` veya `zscale` yoksa kalite servisi çalışmaz. Servis hata atarsa ya da “karşılaştırılamaz” dönerse `ComplexityProfile` ölçülmüş kalır, yalnız `QualityMeasurements` boş olur. Ölçülmeyen bir kalite için sayı üretilmez.

## Test ve mutasyon kanıtı

Köprü çağrısı geçici kaldırıldığında `DetailedProbeExposesWindowQualityThroughCoreContract` başarısız oldu. Ayrı örnek ofseti geçici olarak referans ofsetine bağlandığında `ReferenceAndSampleMayUseDifferentWindowOffsets` VMAF 22,38367795 ile başarısız oldu.

Tur 2'nin “yarım örnek formatı `h264` yapıldığında üç bağımsız test de başarısız oldu” cümlesi geri çekildi: o turda kırmızıya dönen ölçü iki sabiti karşılaştırıyordu, davranışı bağlamıyordu. O test silindi.

Tur 3'ün ölçüleri üretilen ffmpeg argümanına ve sayılan bayta bakar:

- `EveryProbeSampleMuxesThroughTheSameContainer` — `SplitArgs` ve `SampleArgs` çıktısındaki her `-f` değerini okur; dört çıkış, tek muxer, `null` yok.
- `WindowAndMotionSamplesCountTheSameByteUnit` — düşük karmaşıklıklı klipte pencere örneğiyle hareket örneğinin saydığı baytı karşılaştırır. Eşik %8; düzeltilmiş halde ölçülen sapma **%1,5** (pencere 1978 B, hareket 2008 B).
- `ProbeEntryPointUsedByTheAppDoesNotMeasureQuality` — uygulamanın çağırdığı `RunAsync`a sayaçlı bir ölçer verir ve sıfır çağrı bekler.

Mutasyonlar, her biri tek başına uygulanıp geri alındı:

| Mutasyon | Kırmızıya dönen | Sonuç |
|---|---|---|
| `SplitArgs` `[small]` çıkışı yeniden `h264` | `EveryProbeSampleMuxesThroughTheSameContainer` | Başarısız: 1, Başarılı: 17, Toplam: 18 |
| `SplitArgs` `[full]` çıkışı yeniden `h264` | Yukarıdaki + `WindowAndMotionSamplesCountTheSameByteUnit` (“pencere 1143 B, hareket 2008 B, sapma %75,7”) | Başarısız: 2, Başarılı: 16, Toplam: 18 |
| `SampleArgs` (hareket ve yedek yol) yeniden `h264` | Yukarıdaki ikisi (“pencere 1978 B, hareket 1143 B, sapma %42,2”) | Başarısız: 2, Başarılı: 16, Toplam: 18 |
| `RunAsync` içinde `measureQuality: true` | `ProbeEntryPointUsedByTheAppDoesNotMeasureQuality` | Başarısız: 1, Başarılı: 17, Toplam: 18 |

Var olan hiçbir assertion gevşetilmedi, hiçbir test `Skip`e alınmadı.

## Bu turun koşumu

Sözleşme filtresi (`ComplexityProbeTests|QualityMeterTests|PlanCalculatorTests`), `dotnet test` özet satırı olduğu gibi:

```
Başarılı!  - Başarısız:     0, Başarılı:    18, Atlanan:     0, Toplam:    18, Süre: 19 s - VidShrink.Tests.dll (net8.0)
```

Aynı çıktı koşum kapısından geçirildi: `tools/kosum-kapisi/kosum-kapisi.ps1 -MinimumTotal 18` çıkış kodu 0, kapı `başarısız=0 toplam=18 alt-sınır=18` bildirdi.

Tam süit **yerelde koşulmadı**: ölçüm sırasında aynı makinede üç ajan daha çalışıyordu ve paralel koşum ölçüyü kararsız yapıyor. Tur 2 raporundaki 963 ve 974 sayıları doğrulanmadıkları için kaldırıldı.

Tam süidin doğrulanmış sayısı CI'dan gelir. `T88-ornekte-kalite-olcumu` dalının `1c05e7f` koşumu (`gh run 33546346389`, iş `test`) `kosum-kapisi.ps1 -MinimumTotal 950` üzerinden geçti, çıkış kodu 0. Koşumun özet satırı olduğu gibi:

```
Passed!  - Failed:     0, Passed:   921, Skipped:    72, Total:   993, Duration: 8 m 5 s - VidShrink.Tests.dll (net8.0)
```

CI koşucusunda ffmpeg kurulu değil (`.github/workflows/ci.yml` onu kurmuyor); 72 atlanan test buradan gelir ve bu tur hiçbir testi `Skip`e almadı. **Bu turun ffmpeg'e dayanan iki ölçüsü — `WindowAndMotionSamplesCountTheSameByteUnit` ve `ProbeEntryPointUsedByTheAppDoesNotMeasureQuality` — CI'da bağlayıcı değildir**; yukarıdaki mutasyon tablosunun dayanağı ffmpeg 9.0-full'ün kurulu olduğu yerel koşumdur. `EveryProbeSampleMuxesThroughTheSameContainer` argüman üretimine baktığı için ffmpeg'siz de bağlayıcıdır.

## Kalan borç

Tur 3'e “kapanması beklenmiyor” diye yazılan iki borç J1 çözülürken kapandı:

- `SampleWindowAsync`ın yedek yolu artık `SampleAsync` üzerinden aynı muxer'a yazıyor. Split'in başarısız olduğu pencereler de Matroska sayıyor; aynı yoklama içinde karışık birim kalmadı. (`main`de bu ayrım da vardı: split'in tuttuğu pencereler ham ES dosyası, düştüğü pencereler KiB'ye yuvarlanmış stderr baytı sayıyordu.)
- `ParseVideoBytes`ın KiB yuvarlaması `ComplexityProbe`ta yok: yöntem kaldırıldı, bayt tanesi 1 bayt. Ölçülen etkisi yukarıdaki tablodadır — yuvarlama tek başına `color=gray` için `Log2`yi 0,242'den 0,178'e, `smptebars` için 0,245'ten 0,313'e taşıyordu; yönü içeriğe göre değişiyordu.

Kapanmayanlar:

- `CalibrationProbe` kendi `ParseVideoBytes`ını kullanmayı sürdürüyor (`src/VidShrink.Ffmpeg/CalibrationProbe.cs:227,249`) ve aynı KiB yuvarlamasını taşıyor. Bu dosya T88'in `owns` listesinde değil; oradaki iki örnek (düşük ve yüksek CRF) aynı yoldan geçtiği için asimetri yok, ama küçük örneklerde tane kaba. Ölçülmedi.
- `ScanSampleAsync` ve paket okuma yolu baytı `-vstats` `f_size` ve paket boyutundan alır; bunlar konteyner dışı kare/paket yükleridir. Pencere yanlılığı (`bias`) bu sayıların **kendi içindeki oranıdır**, muxlanmış örnek baytlarıyla hiçbir yerde karşılaştırılmaz. Bu yüzden tek muxer kuralının dışında tutuldu.
- `MotionExponent`in Matroska birimindeki kayması (yuvarlamasız ham ölçüye göre düşük bit hızlı iki kaynakta +0,068 ve +0,079) T89'a devreder. Somut sonucu: ölçülen üç kaynaktan biri (`color=gray`) `main`e göre 0,25 eşiğinin karşı tarafına geçiyor (0,178 → 0,322). Eşiğin ya da birimin bu rejimde ne olması gerektiği bu sözleşmenin işi değil; T89 karar vermeden önce bu tablo okunmalı.
