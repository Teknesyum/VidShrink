# Kodek seçimi kapanışı: `PickCodec` gerçek yoklamaya bağlandı

T128. T123 kusuru saptadı ve ölçtü ama düzeltmeyi **bilerek** yazmadı: o gün
`WorksAsEncoder`a bağlamak deterministik bir yanlış cevabı yüke bağlı bir yanlış cevapla
değiştiriyordu. T94 ön koşulu kapattı. Bu tur bağladı.

Ölçüm makinesi: Windows 11, ffmpeg 9.0-full_build-www.gyan.dev, NVIDIA sürücüsü kurulu ve
çalışıyor. Intel QSV ve AMD AMF donanımı yok. **Makine paylaşımlıydı** — süre ölçümlerinin
başında ve sonunda koşan süreç sayısı tabloların altında yazılı.

## Hüküm

**Bağlandı.** Ama bağlanan şey `WorksAsEncoder` değil, önündeki iki kapı:

1. Derleme listesinde olmayan kodlayıcı **yoklanmadan** elenir.
2. Henüz ölçülmemiş kodlayıcı **yoklanmaz**: geçici cevap olarak döner ve
   `HardwareNotMeasured` ile işaretlenir.
3. Geriye kalan — listede var, ölçümü de var — `WorksAsEncoder` ile okunur.

Bu sıra `PickFastCodec`in zaten yaptığı sıranın aynısı. Sonuç: **arayüzün plan hesabı
hiç süreç doğurmuyor** (§3), çünkü arayüz gerçek yetenek nesnesini değil ölçümü arka
plana atan geçidi veriyor. Süreç doğuran tek yol, plan hesabına ham
`EncoderCapabilities` veren çağıranlar — arayüzde bir tane var ve o zaten `Task.Run`
içinde, açılışta.

## K1 — Ön koşul: doğrulandı, tutuyor

T94'ün `src/VidShrink.Ffmpeg/EncoderCapabilities.cs`i okundu. Soru: zaman aşımı
`_probed`e ve `_hdr10PixelFormats`e yazılıyor mu, kaçak dal var mı.

`_probed`e yazan **iki** yer var, ikisi de sayıldı:

| satır | ne yazıyor | koşul |
|---|---|---|
| `:180` | `missing` (deterministik yokluk) | yalnız `!HasEncoder(codec)` iken |
| `:191` | ölçülmüş sonuç | `if (!result.Measured) return result;` satırının **altında** |

Yani `Probe` ölçülemeyen sonuçta erken dönüyor ve önbelleğe hiç uğramıyor. `Hdr10Probe`
aynı deseni kullanıyor: `if (timedOut) return (result, false);` — o da erken dönüş.
Üçüncü bir yazıcı yok.

**Ön koşul tutuyor.** T123'ün "geçici düşüş kalıcı karara dönüşüyor" itirazı kapanmış.

**Ama bir nüans var ve tasarımı o belirledi.** `WorksAsEncoder`
(`EncoderCapabilities.cs:70`) iki durumlu:

```csharp
public bool WorksAsEncoder(string codec) => Probe(codec).Succeeded;
```

`Unmeasured`ı `false` kovasına katlıyor. Ve zaman aşımı önbelleğe yazılmadığı için
ölçülemeyen bir kodlayıcı **her çağrıda yeniden yoklanıyor** — gerçek bir ffmpeg süreci,
`ProbeKillMs = 15000` (`EncoderCapabilities.cs:206`) öldürme sınırıyla. T94 yanlış
cevabın *kalıcılığını* kaldırdı, *olasılığını* değil.

Bu yüzden düzeltme `WorksAsEncoder`a çıplak bağlanmadı; önüne `IEncoderMeasurementState`
kapısı kondu. Süreç doğurmayan okuma yolu (`EncoderState`, `SupportsEncoderOption`) zaten
tam bu iş için var.

## K2 — Kusur önce üretildi

Üreten komut:

```
dotnet build -c Release --no-incremental tests/VidShrink.Tests/VidShrink.Tests.csproj
dotnet test  -c Release --filter "PlanCalculatorTests|EncoderAvailabilityTests" \
             tests/VidShrink.Tests/VidShrink.Tests.csproj
```

Sahte `IEncoderAvailability` (`PlanCalculatorTests.SurucusuzMakine`): derleme listesinde
`libx264, libx265, libsvtav1, h264_nvenc, hevc_nvenc, av1_nvenc`; yoklamayı yalnız
`libx264, libx265` geçiyor. Yani nvenc'li ffmpeg, sürücüsüz makine.

Kırmızı, düzeltmeden önce (commit `80686a2`):

| ölçü | beklenen | bugünkü kod |
|---|---|---|
| `MaxCompressionListedeOlupCalismayanKodlayiciyiSecmiyor` | `libx265` | `libsvtav1` |
| `FastTercihiListedeOlupCalismayanDonanimiSecmiyor` | `libx264` | `h264_nvenc` |
| `OlculmemisKodlayiciYoklanmiyorGeciciCevapVeriliyor` | `HardwareNotMeasured` True | False |
| `OlculmusKodlayiciIcinGecicilikIsaretiKonmuyor` | `libx264` | `h264_nvenc` |

```
Başarısız! - Başarısız: 4, Başarılı: 37, Atlanan: 0, Toplam: 41
```

Listedeki dört satırı saydım: **4**, koşumun bildirdiği başarısız sayısıyla aynı.

Düzeltmeden sonra (commit `ccd2abd`), aynı komut:

```
Başarılı! - Başarısız: 0, Başarılı: 41, Atlanan: 0, Toplam: 41
```

## K3 — Maliyet: önce ölçüldü, sonra bağlandı

### Ölçülen: ham `EncoderCapabilities` veren çağıran

Düzenek T123'ün bıraktığı `tools/surucu-yoklugu`; bu tur ona **yazmadı**, yalnız koşturdu.
Komut, iki koşumda da birebir aynı:

```
dotnet run --project tools/surucu-yoklugu/surucu-yoklugu.csproj -c Release -- maliyet 20
```

Girdi `Codec=Fast, SpeedMode=Quality`, yani `PickCodec` yolu, tercih edilen kodlayıcı
`h264_nvenc`. Her koşumda taze `EncoderCapabilities`, 20 sıcak tekrar.

**Düzeneğin satır etiketi düzeltmeden sonra yanıltıcı.** `HasEncoder (bugunku)` diye
basılan satır, aslında "plan hesabına ham yetenek nesnesi verilirse ne oluyor" satırı;
düzeltmeden sonra o satır artık `WorksAsEncoder` çağırıyor. Aşağıda satırlar etiketleriyle
değil **ne koştuklarıyla** adlandırıldı.

| ne koşuyor | soğuk ms | sıcak ort. ms (20 tekrar) |
|---|---:|---:|
| `PickCodec`, düzeltmeden **önce** (`HasEncoder`) | 9,56 | 0,1693 |
| `PickCodec`, düzeltmeden **sonra** (`WorksAsEncoder`) | 279,45 | 0,1658 |
| aynı koşumda `ProbingAdapter` ile taklit (önce / sonra) | 216,44 / 259,03 | 0,1249 / 0,1294 |

Yük damgası: birinci koşum başında 1 ffmpeg + 6 dotnet, sonunda 1 + 6. İkinci koşum
başında 1 ffmpeg + 8 dotnet, sonunda 1 + 7. Sayan komut:

```
powershell -NoProfile -Command "@(Get-Process ffmpeg -EA SilentlyContinue).Count"
```

Okuma:

- **Sıcak önbellekte maliyet yok.** 0,1693 → 0,1658 ms. Fark negatif, yani ölçüm
  gürültüsünün içinde; bu iki sayı "değişmedi" demekten fazlasını söylemiyor.
- **Soğuk önbellekte tek yoklamanın bedeli ~210–270 ms.** İki bağımsız kestirim var ve
  ikisi de aynı mertebeyi veriyor: düzeltmeden önce taklitle 216,44 − 9,56 = 206,88 ms;
  düzeltmeden sonra gerçek kodla 279,45 − 9,56 = 269,89 ms. İkisi aynı makinede farklı
  dakikalarda alındı; T123 aynı işte 12 kat yayılım ölçmüştü, bu iki sayının 1,3 katlık
  farkı o yayılımın içinde.

### Ölçülen: plan hesabı başına kaç yoklama

`BuildDetailed` iki kez kodlayıcı seçiyor: plan kodlayıcısı (`:164`) ve tavsiye
kodlayıcısı (`:393` ya da geçiş yolunda `:443` — ikisi birbirini dışlıyor). Yani plan
hesabı başına en çok iki yoklama.

İkisi hep aynı işlev değil: `:164` `SpeedMode.Fast` iken `PickFastCodec`e gidiyor,
değilse `PickCodec`e. Tavsiye satırları `fast`e bakmadan hep `PickCodec` çağırıyor. Bu
ayrım maliyeti bölüyor: hızlı kipte iki yoklamanın biri T128 öncesinde de vardı
(`PickFastCodec` zaten `WorksAsEncoder` soruyordu), **yeni** olan yalnız tavsiye
kodlayıcısınınki.

Sayı tahmin değil, ölçüde pimli:

| ölçü | ne sayıyor | değer |
|---|---|---:|
| `MaxCompressionListedeOlupCalismayanKodlayiciyiSecmiyor` | `libsvtav1` yoklaması (plan **ve** tavsiye aynı kodlayıcı) | 2 |
| `FastTercihiListedeOlupCalismayanDonanimiSecmiyor` | `h264_nvenc` yoklaması (yalnız plan) | 1 |
| `CompatibleYolununPlanKodlayicisiYoklanmiyorTavsiyeninkiYoklaniyor` | `h264_nvenc` / `libsvtav1` | 0 / 1 |

Üçüncü satır kendi başına bir bulgu: **"Uyumlu" seçmek yoklamadan kurtarmıyor.** Plan
kodlayıcısı (`libx264`) hiç yoklanmıyor ama tavsiye kodlayıcısı yoklanıyor, çünkü
`suggestedPreference` bu girdide `MaxCompression`.

### En kötü durum

Yoklama zaman aşımına takılırsa `ProbeKillMs = 15000` ms
(`src/VidShrink.Ffmpeg/EncoderCapabilities.cs:206`) ve **zaman aşımı önbelleğe
yazılmıyor** (K1), yani ikinci çağrı da baştan yokluyor. Plan hesabı başına en çok iki
yoklama olduğuna göre en kötü durum **2 × 15 000 = 30 000 ms**.

Bu sayı ölçülmedi, iki ölçülmüş parçadan çarpıldı: yoklama sayısı (yukarıdaki tablo,
ölçüyle pimli) ve öldürme sınırı (kaynak sabiti). T123 §6 aynı makinede yük altında
gerçekten 15 000 ms'yi aşan yoklamalar ölçmüştü (`ek_yuk` 8'de 9/12 örnek), yani sınıra
takılmak varsayımsal bir senaryo değil.

### Neden yine de kabul edilebilir

Çünkü **arayüzün yeniden hesabı bu yolu kullanmıyor.** `src/VidShrink.App` içindeki plan
hesabı çağrılarını saydım — komut:

```
grep -rn "PlanCalculator.Build" src/VidShrink.App/ --include=*.cs
```

Yedi satır dönüyor, ikisi `<see cref=...>` belge satırı (`:1839`, `:2970`). Geriye **beş**
gerçek çağrı kalıyor:

| satır | verdiği yetenek nesnesi | ne zaman koşuyor |
|---|---|---|
| `:1377` | ham `EncoderCapabilities` | açılış, `Task.Run` içinde (`:1370`), `SpeedMode.Fast` |
| `:1694` | `_planEncoders` | yeniden hesap |
| `:1708` | `_planEncoders` | yeniden hesap |
| `:1773` | `_planEncoders` | yeniden hesap |
| `:2997` | çağıranın verdiği | `QualityHint.For`, iki çağıranı da (`:1864`, `:2297`) `_planEncoders` veriyor |

Beşi saydım: dördü geçidi görüyor, biri ham nesneyi ve o da arka planda, açılışta bir kez.

`_planEncoders` bir `DeferredEncoderAvailability` (`MainWindow.axaml.cs:1221`) ve
`IEncoderMeasurementState` gerçekleştiriyor. Yeni `PickCodec` ölçülmemiş kodlayıcıda
`IsMeasured`de duruyor, `WorksAsEncoder`a hiç ulaşmıyor; `IsMeasured` de ölçümü
`Task.Run`a atıp `false` dönüyor. Ölçüm bitince `_onMeasured` → `ScheduleRecalculate`.

Bu, ölçüde pimli: `OlculmemisKodlayiciYoklanmiyorGeciciCevapVeriliyor`
`YoklamaSayisi("h264_nvenc") == 0` sayıyor.

### Neden `IEncoderMeasurementState`, neden `EncoderState` değil

`EncoderCapabilities.EncoderState` (`:78`) de üç durumlu ve süreç doğurmuyor; ilk bakışta
daha temiz kapı o. Kullanılmadı, çünkü ham nesne veren çağıranda **hiç ölçüm
yaptırmıyor**: hiç yoklanmamış kodlayıcı orada `Unmeasured` döner ve `PickCodec` tercih
edileni geçici cevap olarak verir, ama o çağıranda ölçümü kuyruğa alacak kimse yoktur.
Arayüzde karşılığı var — geçit ölçümü arka planda başlatıp hesabı yeniliyor — `tools/`
altındaki başsız çağıranda yok: kodlama hiç doğrulanmamış bir kodlayıcının üstünde başlar.

Arayüzdeki tek ham çağıran `:1377` de bu yüzden görünenden ucuz: `SpeedMode.Fast`
veriyor, yani plan kodlayıcısı `PickFastCodec`ten geçiyor ve o yoklama T128 öncesinde de
vardı. T128'in oraya **eklediği** en çok bir yoklama, tavsiye kodlayıcısınınki — hem de
adı `ProbeHardwareEncodersAsync` olan, işi zaten yoklamak olan bir `Task.Run` içinde.

`IEncoderMeasurementState` tam bu ayrımı yapıyor: geçidi olan çağıran hiç süreç
doğurmaz ve ölçüm gelince yeniden hesaplar; geçidi olmayan çağıran yoklamayı senkron öder
ve gerçek cevabı alır. İkisi de doğru cevabı alıyor, bedelini farklı yerde ödüyor.

Yani K3'ün asıl sorusu — "kullanıcı ayarı değiştirdiğinde arayüz ne kadar donuyor" —
cevabı **sıfır süreç, 0,1658 ms**. 30 000 ms'lik en kötü durum yalnız ham nesne veren
çağıranlara ait: arayüzde `:1377` (arka planda, açılışta) ve `tools/` altındaki ölçüm
programları.

## K4 — `null` yolu pimlendi

`AvailabilityNullIkenTercihEdilenKodlayiciDonuyor` üç tercihi birden pimliyor:
`MaxCompression → libsvtav1`, `Fast → h264_nvenc`, `Compatible → libx264`; ayrıca
`EncoderFallback` notu yok ve `HardwareNotMeasured` False. ffmpeg'siz makinede plan
hesabı bugünkü cevabı vermeye devam ediyor.

## K5 — Mutasyon iki yönde de kırmızı

Her mutasyon tek satır, uygulandı, koşuldu, `git checkout --` ile geri alındı.
`git diff HEAD --stat` iki mutasyondan sonra da boş.

### (a) Düzeltmeyi geri al: `PickCodec` `WorksAsEncoder` → `HasEncoder`

```
Başarısız VidShrink.Tests.PlanCalculatorTests.CompatibleYolununPlanKodlayicisiYoklanmiyorTavsiyeninkiYoklaniyor
Başarısız VidShrink.Tests.PlanCalculatorTests.FastTercihiListedeOlupCalismayanDonanimiSecmiyor
Başarısız VidShrink.Tests.PlanCalculatorTests.MaxCompressionListedeOlupCalismayanKodlayiciyiSecmiyor
Başarısız VidShrink.Tests.PlanCalculatorTests.OlculmusKodlayiciIcinGecicilikIsaretiKonmuyor
Başarısız VidShrink.Tests.EncoderAvailabilityTests.PickCodecArtikDerlemeListesineDegilYoklamayaBakiyor
Başarısız! - Başarısız: 5, Başarılı: 36, Atlanan: 0, Toplam: 41
```

Beş satır saydım: **5**, koşumun bildirdiğiyle aynı. K2'nin dört kırmızısından biri —
`OlculmemisKodlayiciYoklanmiyorGeciciCevapVeriliyor` — bu mutasyonda yeşil kalıyor,
çünkü mutasyon `WorksAsEncoder` satırını değiştiriyor, ölçülmemişlik kapısını değil; o
ölçü zaten kapıda duruyor ve `WorksAsEncoder`a hiç ulaşmıyor. Beşinci kırmızı T123'ün
çevrilmiş kusur kaydı.

### (b) `PickFastCodec` `WorksAsEncoder` → `HasEncoder`

```
Başarısız VidShrink.Tests.EncoderAvailabilityTests.SurucusuzMakinedeHizliKipYazilimaDusuyor
Başarısız VidShrink.Tests.PlanCalculatorTests.HizliKipDonanimYoklamasinaBagliKaliyor
Başarısız! - Başarısız: 2, Başarılı: 39, Atlanan: 0, Toplam: 41
```

İki satır saydım: **2**, koşumun bildirdiğiyle aynı.

İki yön de kırmızı verdiği için ölçüler sabit karşılaştırmıyor, davranış ölçüyor.

## K6 — Kullanıcıya görünen cümle

Bugünkü metin geri düşmenin sebebini **derleme listesi eksikliği** diye söylüyordu.
`PickCodec` gerçek yoklamaya bağlandığı an bu yalan oluyor: sürücüsüz makinede kodlayıcı
derlemede vardır, eksik olan sürücüdür.

Core'daki kopya bu turda düzeltildi (`PlanCalculator.cs:194`, `owns` içinde):

```
- the {preferredCodec} encoder is not available on this ffmpeg build, so encoding falls back to {codec}
+ the {preferredCodec} encoder could not be used on this machine, so encoding falls back to {codec}
```

**Arayüzdeki kopyalar değiştirilmedi — `owns`umda değiller.** Kullanıcının gerçekten
gördüğü metin bunlar; `MainWindow.axaml.cs:2114` `ReasonCode.EncoderFallback`i
`Say("main.reason.encoder-fallback", ...)` ile basıyor. Değişmesi gereken **dört** satır,
saydım:

| dosya | anahtar | önerilen metin |
|---|---|---|
| `src/VidShrink.App/Locales/en/main.json:283` | `main.reason.encoder-fallback` | `the {0} encoder could not be used on this machine, so encoding falls back to {1}` |
| `src/VidShrink.App/Locales/tr/main.json:283` | `main.reason.encoder-fallback` | `{0} kodlayıcısı bu makinede kullanılamadı, bu yüzden {1}'e düşüldü` |
| `src/VidShrink.App/Locales/en/main.json:312` | `main.advice.encoder-fallback` | `The preferred encoder could not be used on this machine; falling back to a software encoder.` |
| `src/VidShrink.App/Locales/tr/main.json:312` | `main.advice.encoder-fallback` | `Tercih edilen kodlayıcı bu makinede kullanılamadı; yazılım karşılığına düşüldü.` |

Bugün Core düzeltildiği, arayüz düzeltilmediği için **cümlenin iki kopyası ayrıştı.**
Kullanıcı hâlâ yanlış sebebi görüyor; borç açık.

## Öneri — T0 kararı gerekli

1. **Yukarıdaki dört yerel metin satırı.** `owns` dışında, dokunulmadı.
2. **`owns` boşluğu: `tests/VidShrink.Tests/EncoderAvailabilityTests.cs`.**
   T123'ün bıraktığı kusur kaydı `KusurKaydiPickCodecYoklamayaDegilDerlemeListesineBakiyor`
   bugünkü **yanlış** çıktıyı pimliyordu; düzeltme onu kırmızıya çevirdi. T123 raporu
   ("düzeltmeyi yazan tur onu çevirmeli") ve sözleşmenin `verify` filtresi bu dosyayı
   bu tura veriyor, ama `owns` listesi vermiyor. Ölçü çevrildi ve adı
   `PickCodecArtikDerlemeListesineDegilYoklamayaBakiyor` oldu. **Bilerek yapıldı ve
   burada bildiriliyor**; `owns` genişletilmezse ihlal olarak okunur.
3. **`WorksAsEncoder`ın iki durumlu imzası hâlâ tuzak.** Bu tur onu geçidin arkasına
   aldı ama kaldırmadı: ham `EncoderCapabilities` veren her çağıran (`:1377`, `tools/`)
   ölçülemeyen yoklamayı "çalışmıyor" diye okumaya devam ediyor. Kalıcı çözüm
   `IEncoderAvailability.EncoderState`in üç durumunu `IEncoderMeasurementState`in yerine
   geçirmek; T129 aynı ayrımı `EncoderProbeResult` üzerinde açıyor, birleşince tek
   temsile inmeli.

## Ölçülmeyenler

- **Gerçekten sürücüsüz bir makinede kullanıcıya görünen sonuç.** Bu makinede NVIDIA
  sürücüsü kurulu ve çalışıyor; sürücüsüzlük yalnız sahte `IEncoderAvailability` ile
  üretildi. Uçtan uca bir kodlama koşulmadı.
- **`Compatible` yolunun hiç değişmediği doğrulanmadı — çünkü değişti.** Plan
  kodlayıcısı değişmiyor (`libx264`, ölçüyle pimli) ama tavsiye kodlayıcısı artık
  yoklanıyor: `libsvtav1` için 1 yoklama. Ham yetenek nesnesi veren bir çağıranda bu,
  `Compatible` seçmiş kullanıcıya da bir ffmpeg süreci ödetiyor. Arayüz yolunda ödemiyor
  (geçit). Bu yolun kaldırılıp kaldırılmaması gerektiği ölçülmedi.
- **30 000 ms'lik en kötü durum doğrudan ölçülmedi**, iki ölçülmüş parçadan çarpıldı
  (yoklama sayısı × `ProbeKillMs`).
- **Arayüz yolunun milisaniye ölçümü yapılmadı.** O yolda süreç doğmadığı sayıldı
  (`YoklamaSayisi == 0`), süresi ayrıca ölçülmedi; geçidi koşturan bir zamanlama düzeneği
  `tools/` altında yok ve `tools/` bu turun `owns`unda değil.
- **Arka plan ölçümünün açılış süresine maliyeti** ölçülmedi.
- `HardwareNotMeasured` işaretinin `BtnStart.IsEnabled`i (`MainWindow.axaml.cs:1793`)
  ne kadar süre kapalı tuttuğu ölçülmedi; ölçüm bitene kadar başlat düğmesi kapalı
  kalıyor ve bu sürenin kullanıcıya nasıl göründüğü denenmedi.
