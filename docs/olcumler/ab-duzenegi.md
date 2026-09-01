# A/B ölçüm düzeneği — eş boyutta HandBrake karşılaştırması

Tarih: 2026-09-02. Araç: `tools/VidShrink.Ab`. Sözleşme: T95.

Bu belgenin konusu önce **alet**, sonra sayı. `docs/olcumler/handbrake-acigi.md`
içindeki GEÇERSİZ tablo, etiketsiz kaynağı bt709 etiketli çıktıyla karşılaştırdığı
için hedef boyut on kat değişirken XPSNR'ı 14,86 / 14,78 / 14,67'de sabit
bırakmıştı. Duyarsız ölçü sayı basmaya devam eder ve yanlışlığını kendi söylemez.
Buradaki düzenek o hatayı tekrar edemesin diye kapılıdır.

## Düzenek

Kaynak: `.calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4` — 1920x1080, hevc,
yuv420p10le, bt2020 / smpte2084 / bt2020nc, full range, 60 fps, 1036,17 sn,
1.729.085.563 bayt.

Araç `ab kos` ile koşar; çıktısı hem makine okunur JSON hem insan okunur tablodur.
Ham ffmpeg ve HandBrake günlükleri `.calisma/ab/gunluk/` altında kalır.

### Renk doğruluğu kapısı

Her çıktının `color_space` / `color_transfer` / `color_primaries` / `pix_fmt`
değeri ffprobe ile okunur ve referansınkiyle karşılaştırılır. Karar üç yoldan biridir:

| durum | karar | etiket |
|---|---|---|
| iki taraf da aynı HDR uzayı | doğrudan ölç | `aynı renk uzayında doğrudan karşılaştırma` |
| iki taraf da aynı SDR uzayı | doğrudan ölç | `aynı renk uzayında doğrudan karşılaştırma` |
| referans HDR, çıktı bt709 SDR | referansı aynı dönüşümden geçir, öyle ölç | `SDR uzayında karşılaştırma — HDR kaybı hariç` |
| taraflardan biri etiketsiz | **reddet** | sayı basılmaz |
| iki HDR uzayı ayrı (ör. PQ ↔ HLG) | **reddet** | sayı basılmaz |
| referans SDR, çıktı HDR | **reddet** | sayı basılmaz |

Etiketsizi reddetmek bu kapının asıl işidir: GEÇERSİZ tablo tam olarak etiketsiz
tarafa varsayım uydurulduğu için sabit sayı basmıştı.

Renk kapısının yanında bir **kare hızı kapısı** vardır. Kare hızları ayrıysa
libvmaf kareleri yanlış eşler ve sessizce bir sayı üretir; araç bu durumda da
sayı basmaz. Bu yüzden her iki yarışmacı da kaynağın kare hızına sabitlenir
(HandBrake `-r 60 --cfr`, VidShrink `AllowFpsDrop = false`).

### Eş boyut

Yarışmacılar aynı hedef boyutu alır. Karşılaştırma tabanı listedeki ilk
yarışmacının gerçek bayt sayısıdır; diğerleri ona göre ±%2 içinde sayılır.
Dışarı taşan satıra `eş boyut değil` damgası basılır ve gerçek bayt sayıları
tabloda durur. Sessiz karşılaştırma yok.

### Ses

HandBrake tarafı `-a none` ile koşuyor. Bütçenin iki tarafta da tümüyle videoya
gitmesi için düzenek **video-only** girdiyle çalışır: sesli her girdiden
`-map 0:v:0 -c copy` ile sessiz bir kopya türetilir, kodlama onun üzerinde yapılır.
Girdi bu adımdan sonra hâlâ ses taşıyorsa araç kodlamaya başlamadan durur.

### Parçalar

17 dakikayı her yapılandırma için baştan sona kodlamak saatler sürdüğünden araç
bir `--parca` kipi taşır. Parçalar `-c copy` ile kesilir, dolayısıyla kesim
istenen saniyeye değil ondan önceki en yakın anahtar kareye oturur. Üç parçanın
kaynaktaki gerçek başlangıcı kare karma (`framehash`) eşlemesiyle doğrulandı:

| parça | istenen | doğrulanan başlangıç |
|---|---|---|
| `parca-1.mkv` | 00:02:00 | 00:01:59,6 |
| `parca-2.mkv` | 00:07:30 | 00:07:29,6 |
| `parca-3.mkv` | 00:13:00 | 00:12:59,6 |

Üçü de hedeflenen saniyeden 0,4 sn önceki anahtar kareye oturuyor; `-c copy`
kesiminin beklenen davranışı budur. `parca-2` ve `parca-3` bu sözleşme
başlamadan önce paralel çalışan başka bir ajan tarafından kesilmişti; sözleşme
"varsa yeniden kesme" dediği için yeniden kesilmediler, yalnız başlangıçları
doğrulandı. İkisi ses taşıdığı için ölçüme video-only türevleriyle girdiler.

Parça kipinde hedef boyut parçanın süresiyle oranlanır: tam kaynak için istenen
N MB, 60,4 saniyelik bir parçada N × 60,4 / 1036,17 MB olur.

### Ölçü

VMAF-NEG dörtlüsü (ortalama, harmonik ortalama, p10, kare minimumu) ile XPSNR ve
SSIM `src/VidShrink.Ffmpeg/QualityMeter.cs`ten gelir; A/B aracı bu hesabı
kopyalamaz, kütüphane olarak çağırır.

Parçalar birleştirilirken:

- **harmonik ortalama** kare sayısıyla ağırlıklı harmonik ortalamadır. Parça
  başına kare sayıları eşit olduğunda bu, bütün karelerin harmonik ortalamasına
  matematiksel olarak eşittir.
- **kare minimumu** parça minimumlarının en küçüğüdür — bu da kesin.
- **p10** parçalar arasında kesin olarak birleştirilemez, çünkü `QualityMeter`
  kare başına puanları dışarı vermiyor. Bu yüzden özet satırındaki sütun
  **en kötü parçanın p10'u**dur ve tabloda öyle adlandırılmıştır. Parça başına
  p10'lar ayrı satırlarda durur.

Rapora tek bir ortalama sayı başlık yapılmaz: harmonik ortalama, p10 ve kare
minimumu birlikte durur. Kullanıcı en kötü sahnede rahatsız olur, ortalamada değil.

### Duyarlılık

Aynı kodlayıcı ve aynı kaynak iki farklı hedef boyutta koşturulur. Büyük hedefin
puanı küçük hedefinkinden en az 1,00 VMAF-NEG puanı yüksek çıkmazsa araç o satırı
`AYRIŞMIYOR` diye işaretler; düzenek duyarsız sayılır.

## Düzeneğin reddettikleri

Bir ölçüm düzeneğinin neyi **reddettiği**, ne ölçtüğü kadar önemlidir. Bu araç
şu durumlarda sayı basmaz, hata verir:

| reddediş | gerekçe |
|---|---|
| taraflardan biri etiketsiz | etiketsize varsayım uydurmak GEÇERSİZ tabloyu üreten hatanın ta kendisi |
| iki HDR uzayı ayrı | PQ ile HLG doğrudan puanlanamaz |
| referans SDR, çıktı HDR | referans yükseltilemez |
| kare hızları ayrı | libvmaf kareleri yanlış eşler, sessizce sayı üretir |
| girdi ses taşıyor | HandBrake `-a none` koşarken bütçe iki tarafta eşit bölünmez |

Son satır bu sözleşme sırasında gerçekten yakalandı ve bir koşumu iptal ettirdi.
`parca-2` ve `parca-3` — bu sözleşmeden önce başka bir ajan tarafından kesilmiş
oldukları için — ses akışı taşıyordu; `parca-1` taşımıyordu. VidShrink tarafı iki
parçada bütçenin bir kısmını sese ayırıp üçüncüsünde ayırmayacak, HandBrake tarafı
ise hiçbirinde ayırmayacaktı. Ortaya çıkacak sayı hem HandBrake'e karşı haksız hem
de parçalar arasında kendi içinde tutarsız olurdu. Sayılar üretildikten sonra bunu
fark etmek zordur: tablo gayet makul görünür.

Düzenek artık sesli her girdiden `-map 0:v:0 -c copy` ile video-only bir kopya
türetiyor; türetmeden sonra girdi hâlâ ses taşıyorsa kodlamaya başlamadan duruyor.

### Bu turda ölçülmeyen

Süre ve hız. Makine paylaşımlı — ölçüm boyunca aynı makinede başka ajanlar da
kodlama koşuyordu. Bu belgede hiçbir süre ya da hız iddiası yok.

## Ölçüler

`AbTests` adıyla, `dotnet test -c Release --filter "AbTests"` — 49 test, 49 geçti,
0 kaldı, 0 atlandı. 36'sı bu sözleşmenin (`ColorGateAbTests` 9,
`ChunkAggregateAbTests` 8, `SensitivityAbTests` 6, `SizeParityAbTests` 5,
`AbSettingsAbTests` 4, `HandBrakeArgumentsAbTests` 3, `DeviationAbTests` 1);
kalan 13 `SettingsTabTests` süzgece adından ötürü takılıyor. Renk kapısı, eş
boyut toleransı, parça birleştirme (p10 dahil), duyarlılık eşiği ve HandBrake
bit hızı hesabı üretim davranışı üzerinden ölçülür: sabit karşılaştırma, `Skip`
ve sessiz erken dönüş yok.
