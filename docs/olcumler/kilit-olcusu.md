# T119 — kare kilidinin iki yarısı, tek sabit, sessiz `null`

Kilit: `settb=AVTB,setpts=N`, `src/VidShrink.Ffmpeg/QualityMeter.cs`
(`MeasureFilterGraph.FrameLock`). İki yarısı var ve **ayrı şeyler yapıyor**;
T111 yalnız Bench kopyasını mutasyona soktu, üretim kopyası ölçülmemişti.

Koşum: `dotnet test -c Release --no-build --filter "VmafPoolingTests|QualityMeterTests"`
Her mutasyondan önce `dotnet build VidShrink.sln -c Release --no-incremental`.
Dal `T119-kilit-olcusu`, worktree `.claude/worktrees/T119`.
Makine paylaşımlı, on ajan koşuyor — süreler damgalı ve karşılaştırılabilir değil.

---

## 1. İki yarım, iki mutasyon — üretim kopyasında

Mutasyon `QualityMeter.cs`'teki üretim sabitine uygulandı. Süit toplamı 43 ölçü.

| # | Mutasyon | Damga (UTC) | Kırmızı | Yeşil |
| --- | --- | --- | ---: | ---: |
| — | (temiz taban) | 04:54 | 0 | 43 |
| M1 | `FrameLock` → `"setpts=N"` (**`settb` yarısı silindi**) | 04:54–04:55 | **4** | 39 |
| M2 | `FrameLock` → `"settb=AVTB"` (**`setpts` yarısı silindi**) | 04:55–04:56 | **4** | 39 |
| — | geri alındı, yeniden `--no-incremental` | 04:56–04:57 | 0 | 43 |

Tur 2'de aynı iki mutasyon 45 ölçülük süitle yeniden koşuldu (06:45–06:46 UTC)
ve **aynı dört ölçü** düştü: M1 4 kırmızı / 41 yeşil, M2 4 kırmızı / 41 yeşil.
Tur 2'nin tip birleştirmesi (aşağıda) sonucu değiştirmedi.

M1'de kırmızıya düşenler:

- `VmafPoolingTests.UretimKilidi_ZamanTabanlariFarkliGirdileri_BireBir_Esler` (**yeni**)
- `QualityMeterTests.TonemappedReferenceSeparatesTwoSdrQualities`
- `QualityMeterTests.SubFrameTimestampSlipDoesNotCostTheScoreAWholeFrame`
- `QualityMeterTests.ReferenceAndSampleMayUseDifferentWindowOffsets`

M2'de kırmızıya düşenler:

- `VmafPoolingTests.UretimKilidi_AltKareKaymasinda_KareleriDamgayaDegil_IndekseEsler` (**yeni**)
- `VmafPoolingTests.KareKilidi_AltKareKaymasinaRagmen_KareleriDogruEsler`
- `VmafPoolingTests.OlcumFiltresi_KareKilidi_OlceklemedenSonraGelir`
- `QualityMeterTests.SubFrameTimestampSlipDoesNotCostTheScoreAWholeFrame`

**İki yeni ölçü birbirinden yalıtık:** M1'de `AltKareKaymasinda` yeşil kaldı,
M2'de `ZamanTabanlariFarkli` yeşil kaldı. Yani her yarım kendi ölçüsüyle
pimlenmiş durumda, ortak bir ölçünün gölgesinde değil.

### T111'in "15 ölçü yeşil kaldı" bulgusu ne kadar doğruydu

T111 `settb=AVTB`'yi **Bench kopyasından** sildi ve `VmafPoolingTests`'in 15
ölçüsü yeşil kaldı. Bu bulgu doğru ve M1 onu yeniden üretiyor: M1'de mevcut
`VmafPoolingTests` ölçülerinden **hiçbiri** düşmedi. Sebebi de belli — o ölçüler
tek kaynağı kendisiyle karşılaştırıyor, iki girdi **aynı zaman tabanını**
taşıyor, `settb` orada gerçekten eşdeğer.

Ama T111'in ölçmediği bir şey vardı: **üretim kopyası tümüyle korumasız
değilmiş.** `QualityMeterTests`'in üç ölçüsü M1'i zaten yakalıyor. Sebebi
tesadüf: üçü de **`.mkv` ile `.mp4`'ü** karşılaştırıyor (`hdr.mkv`/`high.mp4`,
`shifted.mkv`/`test.mp4`, `reference.mp4`/`sample.mkv`), yani girdilerin zaman
tabanları zaten farklı. Bunu ilk kez bu sözleşme ölçtü.

**Sözleşmenin öncülü çürütüldü, adını koyalım.** `T119.md:23` "kilidin
`settb=AVTB` yarısını **hiçbir** ölçü korumuyor" diyordu; üretim kopyasında bu
yanlış — üç ölçü koruyordu. Doğru olan daha dar bir ifade: T111'in ölçtüğü Bench
kopyasında hiçbir ölçü korumuyordu, ve üretimde kilidi **adlandıran, kasıtlı,
yalıtık** bir ölçü yoktu. Yakalayan üç ölçü kilidi ölçmek için yazılmamıştı;
M1'i tesadüfen, girdilerinin kabından yakalıyorlardı. Sözleşme yine de
gerekliydi: tesadüfe dayanan koruma, ölçü değildir.

### `settb=AVTB` neden ve ne zaman davranış değiştiriyor

Kusurun tetiklendiği koşul: **iki girdinin zaman tabanı farklı.** `setpts=N`
kare indeksini girdinin *kendi* tabanına yazar; tabanlar farklıysa aynı `N` iki
akışta farklı saniyeye düşer ve framesync eşleşmesi çöker.

Yalın ffmpeg koşumu (160x120, 30 fps, 60 kare, kayıpsız ffv1; aynı görüntü iki
kapta: `.mkv` → `1/1000`, `.nut` → `1/64000`):

| filtre | eşlenen kare | en düşük `psnr_y` |
| --- | ---: | ---: |
| `settb=AVTB,setpts=N` | 60 | `inf` |
| `setpts=N` (M1) | **119** | 17,15 dB |
| `settb=AVTB` (M2) | 60 | `inf` |

Aynı düzenek, bu kez tek kaynak kendisiyle ve referansa `-itsoffset 0.004`
(33 ms'lik karenin altında):

| filtre | eşlenen kare | en düşük `psnr_y` |
| --- | ---: | ---: |
| `settb=AVTB,setpts=N` | 60 | `inf` |
| `setpts=N` (M1) | 60 | `inf` |
| `settb=AVTB` (M2) | 60 | **23,53 dB** |

İki tablo birlikte okunduğunda: `settb` yarısı **farklı zaman tabanına**,
`setpts` yarısı **alt kare kaymasına** karşı koruyor. Yeni iki ölçü tam olarak
bu iki düzeneği kuruyor.

---

## 2. Kopya sabitin akıbeti — tek sabit kaldı

`tools/VidShrink.Bench/Program.cs` kendi `public const string FrameLock`
kopyasını taşıyordu. Kopya kaldırıldı; Bench artık üretim sabitini okuyor:

    public static string FrameLock => VidShrink.Ffmpeg.MeasureFilterGraph.FrameLock;

Üretim tarafında `const` yerine `static readonly` seçildi. Gerekçe: `const`
derleme anında çağıran derlemeye **kopyalanır**; bu projede artımlı derlemenin
bayat ikili koşturduğu bir vaka kayıtlı ve o durumda Bench eski değeri taşımaya
devam ederdi. `static readonly` ile Bench değeri koşum anında `VidShrink.Ffmpeg`
derlemesinden alır, kopya oluşmaz.

**Birleşmenin işe yaradığının kanıtı M2'de:** üretim sabiti mutasyona uğrayınca
Bench grafiğini kullanan `KareKilidi_AltKareKaymasinaRagmen_KareleriDogruEsler`
kırmızıya döndü. Birleştirme öncesi üretim sabitinin mutasyonu o ölçüye hiç
ulaşmıyordu. İki sabiti karşılaştıran bir ölçü yazılmadı; gereği kalmadı, çünkü
ortada tek sabit var.

---

## 3. Sessiz `null` — "ölçülmedi" ile "ölçüm başarısız" ayrıldı

Eskiden `QualityMeter.cs`'te iki durum da `null` dönüyordu:

- libvmaf yok → ölçüm **yapılmadı** (meşru),
- libvmaf koştu ama günlük yazılmadı / kare puanı içermiyor → ölçüm
  **başarısız** (kusur).

Şimdi ayrık:

| durum | üretimde nasıl okunur |
| --- | --- |
| libvmaf filtresi yok | `QualityScore.VmafNegMean == null`; `MeasureVmafAsync` hiç çağrılmıyor |
| zincir koştu, günlük yok / boş / bozuk | `QualityMeasurementFailedException` fırlar, iletisi günlük yolunu içerir |
| pencere ölçümü başarısız | `QualityMeasurement.MeasureWindowAsync` **`null` değil**, `Comparable = false` + `Message` dolu bir `WindowQualityMeasurement` döner |

`MeasureVmafAsync`'in dönüş türü `VmafAggregate?`'ten `VmafAggregate`'e çekildi:
artık o yoldan `null` çıkamıyor, "ölçülmedi" kararı yalnız dışarıdaki
`HasFilter("libvmaf")` kapısında veriliyor.

### Bozuk zincirle koşum — ne dönüyor

`GunlukYazilamayanZincir_FfmpegSifirDonuyor_OkuyucuBasarisizDiyor` üretim
`MeasureFilterGraph.Build`'iyle zincir kurup gerçek ffmpeg'le koşturuyor.
Karşılaştırma filtresi `libvmaf=...:log_fmt=json:log_path='olmayan/vmaf.json'`
— yol **veriliyor**, ama hedef klasör yok, yani libvmaf günlüğü yazamıyor.
Ölçülen:

- ffmpeg stderr'e `could not open file` yazıp **çıkış kodu 0** veriyor,
- beklenen günlük dosyası yok,
- `QualityMeter.ReadVmafScoresAsync(gunluk)` `QualityMeasurementFailedException`
  fırlatıyor; eskiden aynı yer `null` dönüp "ölçüm yok" diye okunuyordu.

Bu, sessiz başarısızlığın **üretimdeki gerçek biçimi**: `log_path` doğru
kaçırılıp veriliyor, ffmpeg başarı raporluyor, ölçüm yok.

Tur 1'de bu ölçü `log_path`'i ffmpeg'e hiç vermiyordu; `File.Exists` iddiası o
hâliyle boştu ve ölçünün tek gerçek katkısı "üretim grafiği + libvmaf çıkış
kodu 0"dı. Tur 2'de düzeltildi, adı da ölçtüğü şeye göre değişti.

Not: filtre adı yanlış yazılmak gibi kaba bozulmalarda ffmpeg sıfır dışı kod
döner ve `RunFilterAsync` zaten gürültülü patlıyordu. Sessiz kalan durum dar
olanıydı: **zincir koşuyor ama ölçüm üretmiyor.** Kapatılan bu.

### Kapanmayan taraf — çağıran hatayı hâlâ yutuyor

`src/VidShrink.Ffmpeg/ComplexityProbe.cs:472-477` çağrıyı `catch { quality =
null; }` ile sarıyor, `:74` ise yalnız `Comparable: true` olanları topluyor;
`src/VidShrink.App/MainWindow.axaml.cs:1505` de aynı süzgeci uyguluyor. Yani
başarısız ölçüm artık **nesne olarak** ayırt edilebiliyor ve iletisini taşıyor,
ama hiçbir çağıran o iletiyi **göstermiyor**. İki dosya da bu sözleşmenin
`owns`'unda değil; düzeltilmedi, yazıldı.

---

## 4. Tur 2 — düşme yolu, veri kaybı, tip adı

### 4.1 Bench artık sessizce sıfır dönmüyor

Tur 1'in yan etkisi: `QualityMeter.MeasureAsync`/`MeasureWindowAsync` istisna
sızdırmaya başlayınca `tools/VidShrink.Bench/Program.cs`'in `measure` ve
`measure-window` komutları **yakalanmamış istisnayla** düşüyordu. Eski davranış
`null` alanlı JSON basıp `return 0` idi — yani ölçüm başarısızken çıkış kodu
başarıyı söylüyordu.

**Seçim: sıfırdan farklı.** Gerekçe: bu sözleşmenin kapattığı kusur "başarısız
ölçümün başarılıdan ayırt edilememesi"ydi; çıkış kodu 0'a dönmek onu geri
getirir. Bench çıktısı ölçüm tablolarını besliyor, tabloyu üreten betik tek
baktığı şey çıkış kodu olabilir.

Seçilen davranış — `BenchOlcum.YazAsync`:

| durum | çıkış kodu | stdout | stderr |
| --- | ---: | --- | --- |
| ölçüm başarılı | 0 | tam skor JSON'u | — |
| ölçüm başarısız | **2** | **kısmi** skor JSON'u (`Comparable=false`) | başarısızlık iletisi |
| kullanım hatası | 1 | — | kullanım iletisi |

2 seçildi çünkü 1 bu programda zaten "kullanım hatası"; üç durum üç kodla
ayrışıyor. Kısmi JSON yine basılıyor: gürültülü düşmek veri atmayı gerektirmiyor.

### 4.2 `xpsnr` ve `ssim` artık vmaf başarısızlığında kaybolmuyor

Tur 1'de vmaf hatası `MeasureAsync`'in ortasından fırladığı için `xpsnr` ve
`ssim` **hiç hesaplanmıyordu**; oysa eskiden vmaf `null` dönerken ikisi de
hesaplanıyordu. Şimdi hata yakalanıp saklanıyor, iki ölçü hesaplanıyor, sonra
istisna **kısmi skoru taşıyarak** fırlatılıyor
(`QualityMeasurementFailedException.PartialScore`). Bench o kısmi skoru basıyor.

**Bu düzeltme pimli değil — ölçüldü, tahmin değil.** M4 mutasyonu (`catch`
gövdesi `throw;` ile değiştirilip eski sıra geri getirildi, `--no-incremental`,
06:48–06:49 UTC) süiti **0 kırmızı / 45 yeşil** bıraktı. Sebebi: gerçek bir vmaf
başarısızlığını `MeasureAsync` üzerinden zorlamak için günlük yolunu dışarıdan
vermek gerekiyor, o yol `private`. Pimlenen tek şey istisnanın kısmi skoru
taşıması ve Bench'in onu basması (M5, aşağıda). Sıralamanın kendisi
**ölçülmemiş durumda.**

### 4.3 Tip adı da teklendi

`MeasureFilterGraph` adı iki tipte birden duruyordu: `VidShrink.Ffmpeg`
içindeki üretim tipi ve Bench'in genel ad alanındaki kopyası. Nitelenmemiş ad
Bench'e çözülüyordu — T111'in yanlış kopyayı ölçmesine yol açan tuzak buydu ve
tur 1'de sabit teklenmiş ama **ad teklenmemişti**.

Bench'in tipi `BenchMeasureFilterGraph` oldu ve gövdesi üretim `Build`'ine
devrediyor:

    return MeasureFilterGraph.Build($"scale=w={width}:h={height}:flags=lanczos", "null", filterChain);

Artık nitelenmemiş `MeasureFilterGraph` her yerde **üretim** tipi demek. Yan
etki: Bench grafiğinin referans dalı `[1:v]settb=...` yerine
`[1:v]null,settb=...` oluyor. `null` bir geçirgen filtre; ölçüm sonuçları
değişmiyor (M1/M2 tur 2 koşumu aynı dört ölçüyü düşürdü, taban 45/45 yeşil),
ama Bench'in bastığı komut dizesi eski arşiv günlüklerinden bu ek kadar farklı.

### 4.4 Tur 2 mutasyonları

| # | mutasyon | damga (UTC) | kırmızı |
| --- | --- | --- | ---: |
| M1 | `FrameLock` → `"setpts=N"` | 06:45 | 4 |
| M2 | `FrameLock` → `"settb=AVTB"` | 06:45–06:46 | 4 |
| M3 | `BenchOlcum.Basarisiz` `2` → `0` | 06:46–06:47 | 1 |
| M4 | vmaf hatası anında yeniden fırlatılır (eski sıra) | 06:48–06:49 | **0** |
| M5 | kısmi skor basılmaz | 06:47 | 1 |

M3 ve M5'i düşüren ölçü aynı:
`BasarisizOlcumde_Bench_SifirDonmez_KismiSonucu_YineDeYazar`. M4 hiçbir ölçüyü
düşürmüyor — 4.2'de yazıldığı gibi.

---

## Ölçülmeyen / bilerek bırakılan

- **CI hakkında hiçbir şey söylenmiyor.** `main`in CI'ında ffmpeg yok; buradaki
  dört yeni ölçünün üçü `[FfmpegFact]` ve orada **atlanır**. Kilit CI'da
  korunmuyor; bu sözleşme onu değiştirmedi.
- **Tek makine, tek ffmpeg sürümü.** Sayılar `ffmpeg 9.0-full_build` ile,
  Windows 11 üzerinde alındı. `1/1000` ve `1/64000` taban değerleri kapların
  varsayılanı; başka bir sürümde değişirse `ZamanTabaniAsync` karşılaştırması
  ölçüyü boşa düşmeden **kırar** (öncül `Assert.NotEqual` ile pimli).
- **`xpsnr`/`ssim` korunmasının sırası ölçülmedi** (M4 = 0 kırmızı). Gerçek bir
  vmaf başarısızlığını üretim `MeasureAsync`'i üzerinden zorlamak için günlük
  yolunun dışarıdan verilebilmesi gerekiyor; o `private` ve bu sözleşme onu
  açmadı.
- **Bench'in çıkış kodu gerçek süreçle ölçülmedi**, `BenchOlcum.YazAsync`
  seviyesinde ölçüldü. Yani "exe 2 döndürüyor" değil, "karar veren işlev 2
  döndürüyor ve `Program.cs` onu döndürüyor" ölçüldü.
- **Kilidin `PTS-STARTPTS` almaşığı yeniden ölçülmedi.** T110'un gerekçesi
  (`docs/olcumler/olcu-gecerliligi.md`) olduğu gibi duruyor.
- **`FrameLock`'un değeri değişmedi**, yalnız bildirimi (`const` →
  `static readonly`) ve Bench kopyasının kaldırılması değişti. Sabiti anlatan
  düzyazı `docs/olcumler/olcu-gecerliligi.md:398-412` ve
  `docs/olcumler/algi-olcusu.md:747-748`'de; ikisi de bu değişiklikten sonra
  hâlâ doğru — algi-olcusu'nun "T106'nın kullandığı kilidin birebir aynısı"
  cümlesi artık derleyici tarafından zorlanıyor. Her iki dosya da bu
  sözleşmenin `owns`'unda değil, elle doğrulandı, düzeltme gerekmedi.
