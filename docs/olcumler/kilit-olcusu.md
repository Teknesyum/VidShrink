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
tabanları zaten farklı. Bunu ilk kez bu sözleşme ölçtü. Açık, sözleşmede
yazıldığı kadar büyük değildi; **T111 yanlış bir şey söylemedi, ölçtüğü kopya
farklıydı.** Yakalayan üç ölçü de kilidi ölçmek için yazılmamış — kilidi
adlandıran, kasıtlı ve yalıtık ölçü M1 için yalnızca yeni eklenendir.

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

`GunlukUretmeyenZincir_Olculmedi_Degil_Basarisiz_DiyeOkunur` üretim
`MeasureFilterGraph.Build`'iyle bir zincir kurup gerçek ffmpeg'le koşturuyor;
karşılaştırma filtresi `libvmaf=...:log_fmt=json`, **`log_path` yok**. Sonuç:

- ffmpeg **çıkış kodu 0** verir — yani "hata" gibi görünmez,
- beklenen günlük dosyası **yoktur**,
- `QualityMeter.ReadVmafScoresAsync(gunluk)` artık
  `QualityMeasurementFailedException` fırlatır; eskiden aynı yer `null` dönüp
  "ölçüm yok" diye okunuyordu.

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

## Ölçülmeyen / bilerek bırakılan

- **CI hakkında hiçbir şey söylenmiyor.** `main`in CI'ında ffmpeg yok; buradaki
  dört yeni ölçünün üçü `[FfmpegFact]` ve orada **atlanır**. Kilit CI'da
  korunmuyor; bu sözleşme onu değiştirmedi.
- **Tek makine, tek ffmpeg sürümü.** Sayılar `ffmpeg 9.0-full_build` ile,
  Windows 11 üzerinde alındı. `1/1000` ve `1/64000` taban değerleri kapların
  varsayılanı; başka bir sürümde değişirse `ZamanTabaniAsync` karşılaştırması
  ölçüyü boşa düşmeden **kırar** (öncül `Assert.NotEqual` ile pimli).
- **Kilidin `PTS-STARTPTS` almaşığı yeniden ölçülmedi.** T110'un gerekçesi
  (`docs/olcumler/olcu-gecerliligi.md`) olduğu gibi duruyor.
- **`FrameLock`'un değeri değişmedi**, yalnız bildirimi (`const` →
  `static readonly`) ve Bench kopyasının kaldırılması değişti. Sabiti anlatan
  düzyazı `docs/olcumler/olcu-gecerliligi.md:398-412` ve
  `docs/olcumler/algi-olcusu.md:747-748`'de; ikisi de bu değişiklikten sonra
  hâlâ doğru — algi-olcusu'nun "T106'nın kullandığı kilidin birebir aynısı"
  cümlesi artık derleyici tarafından zorlanıyor. Her iki dosya da bu
  sözleşmenin `owns`'unda değil, elle doğrulandı, düzeltme gerekmedi.
