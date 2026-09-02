# Sahne haritası üretimde bağlandı

T98 buldu: sahne haritası hesaplanıyor ama üretimde kimse geçirmiyor, uçtan uca
anahtar kare tavanı her zaman 10 sn varsayılanı kalıyordu. T101/T105/T109/T98'in
ölçtüğü hiçbir şey kullanıcının dosyasına ulaşmıyordu. Bu belge bağlantıyı ve
bağlanmanın kullanıcı çıktısına etkisini ölçüyor.

Ham ölçüm dosyaları `.calisma/t113/` altında ve **worktree-yereldir** — denetçi
göremez. O yüzden buradaki her sayının üreten komutu bu belgede yazılı.

## Ölçüm düzeneği

`tools/harita-baglantisi/` (`AssemblyName=t113baglanti`). Üretim yolunun kendisini
çağırır: `EncodeRunner.TryBuildSceneMapAsync` → `PlanCalculator.BuildDetailed` →
`EncodeRunner.RunAsync`, biri harita **verilerek** biri **verilmeden**.

```
dotnet build tools/harita-baglantisi/Baglanti.csproj -c Release
t113baglanti <kaynak> <hedefMB> <cikti-klasoru> [threads] [atlama-turu] [fps-kilit]
t113baglanti atlama <bagli.mp4> <bagsiz.mp4> <sure_sn> <cikti-klasoru> [tur]
t113baglanti kalite <referans> <test...>
```

Kaynaklar ortak 17 dakikalık kaynağın yalnız-video kopyasından **akış kopyasıyla**
kesildi (yeniden kodlama yok, ses yok — "ortak ölçüm parçaları sessizce farklı"
tuzağı bu yüzden):

```
ffmpeg -hide_banner -v error -y -ss 200 -t 60 \
  -i .calisma/kaynak/kaynak-1080p60-hdr-17dk-yalniz-video.mkv \
  -c:v copy -an .calisma/t113/kaynak/cok-kesimli.mkv
```

`-ss 360` durgun, `-ss 700` tek-sahne klibini veriyor.

| Klip | Kesildiği an | Süre | Boyut | Neyi temsil ediyor |
| --- | --- | --- | --- | --- |
| `cok-kesimli.mkv` | 200 sn | 60,400 sn | 105,98 MB | çok kesimli |
| `durgun.mkv` | 360 sn | 60,399 sn | 71,07 MB | durgun |
| `tek-sahne.mkv` | 700 sn | 60,415 sn | 92,99 MB | tek sahnelik |

Üçü de 1920x1080, 60/1, HDR (bt2020nc/smpte2084). Hedef boyut üçünde de 12 MB,
iş parçacığı `-threads 2` ile sabit (`plan.ExtraArgs`, iki geçişe de giriyor).

## 1. Üç çağıran

| Çağıran | Künye | Geçirilen argüman |
| --- | --- | --- |
| Arayüz — gösterilen komut | `src/VidShrink.App/MainWindow.axaml.cs:1808` | `_sceneMap?.Map` |
| Arayüz — gerçek kodlama | `src/VidShrink.App/MainWindow.axaml.cs:2412` | `_sceneMap?.Map` |
| Ön izleme | `src/VidShrink.Core/PreviewSegment.cs:170` | `scenes` (`PreviewSegment.For`'un 7. parametresi) |
| Koşucu — argüman üretimi | `src/VidShrink.Ffmpeg/EncodeRunner.cs:333` | `scenes` |
| Koşucu — geçiş başına koşum | `src/VidShrink.Ffmpeg/EncodeRunner.cs:341` | `scenes` (`RunOneAsync`'ten) |

Sözleşme üç çağıran sayıyor; arayüz ikiye, koşucu ikiye ayrılıyor çünkü ikisi de
tek başına bağsız kalabilir ve mutasyonda ayrı ayrı ölçülüyor (§ 7).

**Haritayı kim üretiyor.** `EncodeRunner.TryBuildSceneMapAsync`
(`src/VidShrink.Ffmpeg/EncodeRunner.cs:277`). Arayüz onu
`MainWindow.MeasureComplexityAsync` içinde, kaynak başına **bir kez** çağırıyor
(`MainWindow.axaml.cs:1529`): karmaşıklık profili düştükten ve ilk
`Recalculate()` koştuktan hemen sonra, kalibrasyon döngüsünden önce. Kaynak
değişince alan sıfırlanıyor (`MainWindow.axaml.cs:1478`).

Yeri seçilmiş bir karardır: kestirim kullanıcıya gecikmeden çıksın diye ilk
`Recalculate()` yoklamadan önce koşar. Kullanıcı yoklama bitmeden Kodla'ya
basarsa `_sceneMap` `null`'dur ve çıktı belgelenmiş 10 sn varsayılanına düşer.

**Yoklama zaten koşuyor muydu — hayır.** `ComplexityProbe` sahne taraması
yapmıyor (`src/VidShrink.Ffmpeg/ComplexityProbe.cs` içinde ne `SceneDetector`
ne `gte(scene,…)` geçiyor). Bu yeni bir maliyettir; § 4'te ölçüldü.

## 2. Bağlanmanın kullanıcı çıktısına etkisi

```
t113baglanti .calisma/t113/kaynak/<klip>.mkv 12 .calisma/t113/olcum/<klip> 2 6
t113baglanti atlama <klip>-bagli.mp4 <klip>-bagsiz.mp4 60.4 <klip>/atlama2 4
```

Plan üretim varsayılanlarıyla kuruldu (`Intent.Sharing`, `SpeedMode.Quality`,
çözünürlük ve fps düşüşü serbest, `HdrPolicy.Preserve`). Bu yüzden üç klip üç
ayrı plana düştü; tablodaki `-g` mutlak değerleri o planların fps'ine aittir.

| Klip | Sahne | Medyan sahne | Tavan (bağlı) | `-g` bağlı | `-g` bağsız |
| --- | --- | --- | --- | --- | --- |
| cok-kesimli | 8 | 2,584 sn | 5,0 sn (alt kıskaç) | 150 (30 fps) | 300 |
| durgun | 2 | 30,200 sn | 10,0 sn (üst kıskaç) | 600 (60 fps) | 600 |
| tek-sahne | 1 | 60,415 sn | 10,0 sn (üst kıskaç) | 250 (25 fps) | 250 |

**Üç kaynağın ikisinde bağlamak hiçbir şeyi değiştirmiyor.** Kıskaç
[5 sn, 10 sn] üst ucundan bağlıyor ve üst uç zaten varsayılanla aynı sayıdır;
durgun ve tek sahnelik içerikte bağlı koşum bağsız koşumla **aynı argümanı**
üretiyor. Kazanç yalnız medyan sahnesi 10 sn'nin altına inen içerikte var.

| Klip | Koşum | Boyut (MB) | VMAF-NEG ort. | VMAF-NEG p10 | I-kare | Atlama net p50 (ms) |
| --- | --- | --- | --- | --- | --- | --- |
| cok-kesimli | bağlı | 11,614 | ölçülemedi | ölçülemedi | 13 | 97,0 |
| cok-kesimli | bağsız | 11,609 | ölçülemedi | ölçülemedi | 7 | 124,8 |
| durgun | bağlı | 11,743 | 58,396 | 57,894 | 7 | 84,6 |
| durgun | bağsız | 11,742 | 58,395 | 57,893 | 7 | 85,1 |
| tek-sahne | bağlı | 11,638 | ölçülemedi | ölçülemedi | 7 | 114,3 |
| tek-sahne | bağsız | 11,635 | ölçülemedi | ölçülemedi | 7 | 118,3 |

Atlama sayıları **paylaşımlı makine** damgalıdır; boyut, VMAF ve I-kare değil.
Boyut ve I-kare bu tabloda `ffprobe` ile dosyadan yeniden okundu:

```
ffprobe -v error -select_streams v:0 -show_entries frame=pict_type -of csv=p=0 <dosya>
```

**VMAF neden iki klipte ölçülemedi.** İkisi de ölçünün reddi, benim atlamam
değil:

- `cok-kesimli`: plan HDR kaynağı SDR'a indiriyor (svtav1 çıktısı bt709),
  `QualityMeter` "renk uzayı uyuşmuyor" deyip `Comparable = false` döndürüyor.
- `tek-sahne`: plan 60 fps'i 25 fps'e düşürüyor; ölçü sayı **veriyor** ama sayı
  geçersiz (ort. 4,972, p10 0,000 — kare eşleşmesi çöküyor). Geçersiz sayı
  tabloya yazılmadı.

`durgun` ikisinden de kaçtı (libx264, fps düşmedi) ve bu yüzden **gürültü
tabanı** olarak işe yarıyor: bağlı ve bağsız koşumun argümanı **birebir aynı**
olduğu için aradaki fark yalnız kodlayıcı belirsizliğidir — boyutta 0,001 MB,
VMAF ortalamasında 0,0005, p10'da 0,0011, atlamada 0,5 ms. `tek-sahne` de aynı
şekilde argüman-özdeş bir çift ve atlamada 4,0 ms fark verdi; **atlama ölçüsünün
gürültü tabanı bu iki çiftte 0,5–4,0 ms.**

### Kaliteyi ölçülebilir kılan ek koşum (fps kilitli)

Kalitenin ölçülemediği yerlerden biri, bağlamanın **fark yarattığı** tek klipti.
O yüzden `cok-kesimli` bir kez daha, fps düşüşü kapalı (`AllowFpsDrop = false`)
koşuldu; böylece kare eşleşmesi düzeliyor ve renk uyuşmazlığı da kalmıyor:

```
t113baglanti .calisma/t113/kaynak/cok-kesimli.mkv 12 \
  .calisma/t113/olcum/cok-kesimli-fpskilit 2 4 fps-kilit
```

Plan: libsvtav1 preset 6, 2 geçiş, 806x454@60, hedef 12 MB.

| Koşum | `-g` | Boyut (MB) | VMAF-NEG ort. | VMAF-NEG p10 | I-kare | Atlama net p50 (ms) |
| --- | --- | --- | --- | --- | --- | --- |
| bağlı | 300 (5,0 sn) | 11,644 | 52,872 | 45,076 | 13 | 98,4 |
| bağsız | 600 (10,0 sn) | 11,637 | 52,957 | 45,525 | 7 | 186,0 |

### Ne çıktı

Aynı hedef boyutta, çok kesimli içerikte:

- **I-kare 7 → 13.** İki koşumda da (üretim planı ve fps kilitli) aynı sayılar.
- **Atlama hızlanıyor.** Üretim planında net p50 124,8 → 97,0 ms, yani
  **%22,3**; fps kilitli koşumda 186,0 → 98,4 ms, yani **%47,1**. İkisi de
  gürültü tabanının (0,5–4,0 ms) çok üstünde. Paylaşımlı makine damgalı.
- **Boyut pratikte değişmiyor, ama gürültüye gömülmüyor.** 11,637 → 11,644 MB
  (fps kilitli), 11,609 → 11,614 MB (üretim planı); bağlı koşum iki kez de
  **biraz büyük** (0,007 ve 0,005 MB). Gürültü tabanının üst ucu 0,003 MB, yani
  fark tabanın iki katı. Hedefin (12 MB) binde biri mertebesinde — ölçülebilir
  ama kullanıcı için görünmez.
- **Kalite bir miktar düşüyor.** Ortalama 52,957 → 52,872 (**0,085 düşük**),
  p10 45,525 → 45,076 (**0,449 düşük**). İkisi de gürültü tabanının
  (0,0005 / 0,0011) üstünde, yani **gerçek ama küçük bir bedel.**

**Takas gizlenmiyor: bağlamak aynı boyutta biraz kalite verip belirgin atlama
hızı alıyor.** Bu, T98'in `FfmpegArguments` seviyesinde ölçtüğü sonuçla aynı
yönde değil. T98'in karşılaştırdığı çift bu çift değildi:
`tepe-tavani-ve-psy.md:539,540` **ortalama tavan ile medyan tavanı** karşılaştırıp
medyanın iki kalite ölçütünde de kötü olmadığını ölçtü (88,5253 → 88,6025 ort.,
86,4959 → 86,5155 p10). Buradaki çift **haritalı ile haritasız**, ve iki ölçütte
de küçük bir düşüş var. Rejimler de ayrı — T98 libx264 2 geçiş 8000k'da, bu ölçüm
libsvtav1 preset 6'da 12 MB hedefiyle. Bu bir çelişki iddiası değil: T98'in
bulgusu uçtan uca **sınanmadı**, farklı bir soru sınandı.

Diğer iki içerik türünde **kazanç da bedel de yok**: argüman değişmiyor.

## 3. Ön izleme yolu

`FfmpegArguments.BuildSegment` (`FfmpegArguments.cs:468`) aynı haritayı `Build`'e
devrediyor, dolayısıyla aynı plan + aynı harita aynı `-g`'yi vermek zorunda.
Ölçüldü — üç klipte de, iki rejimde de ayrışma yok:

| Klip | Ön izleme `-g` bağlı | Nihai `-g` bağlı | Ön izleme `-g` bağsız | Nihai `-g` bağsız |
| --- | --- | --- | --- | --- |
| cok-kesimli | 150 | 150 | 300 | 300 |
| durgun | 600 | 600 | 600 | 600 |
| tek-sahne | 250 | 250 | 250 | 250 |

Davranışla da pimli:
`PreviewSegmentTests.Onizleme_ve_nihai_kodlama_ayni_anahtar_kare_araligini_verir`
haritasız ve 3,0 / 7,5 / 40,0 sn'lik haritalarda `PreviewSegment.For` ile
`FfmpegArguments.Build`'in ürettiği `-g`'yi karşılaştırıyor.

**Ama arayüzdeki ön izleme hâlâ ayrışıyor — ve düzeltmesi `owns` dışında.**
Kullanıcının ekranda gördüğü parçayı `PreviewSegment.For`'a veren zincir
`MainWindow` → `PanelHost` → `SegmentEncoder.Describe`
(`src/VidShrink.App/Playback/SegmentEncoder.cs:118`) ve o çağrı `scenes`
**geçirmiyor**. Yani bugün arayüzde nihai kodlama 5 sn tavanla, ön izleme 10 sn
tavanla üretiliyor. Sözleşme bu ayrışmayı KRİTİK sayıyor; kapatmak iki dosya
ister ve ikisi de bu sözleşmenin `owns`'unda değil:

- `src/VidShrink.App/Playback/SegmentEncoder.cs` — `Availability`'nin (:109)
  birebir eşi bir `SceneMap? Scenes` özelliği ve `Describe`'da geçirilmesi.
- `src/VidShrink.App/Playback/PanelHost.cs:244` — `Availability`'yi ileten
  ayarlayıcının eşi.

Ondan sonra `MainWindow.axaml.cs:1250`'nin (`_preview.Availability = encoders;`)
yanına tek satır düşer. **T0 kararı gerekli.**

## 4. Yoklama maliyeti

Yoklama `SceneDetector.ScanAsync`: kaynağı bir kez çözer, 640 genişliğe
ölçekleyip ultrafast/crf 23 ile kodlar ve `gte(scene, 0.012)` günlüğü çıkarır.

| Klip | Kaynak süresi | Yoklama süresi | Kaynak süresine oranı | Bağlı kodlama süresi | Kodlamaya oranı |
| --- | --- | --- | --- | --- | --- |
| cok-kesimli | 60,400 sn | 14,552 sn | %24,1 | 138,2 sn | %10,5 |
| durgun | 60,399 sn | 18,498 sn | %30,6 | 147,0 sn | %12,6 |
| tek-sahne | 60,415 sn | 16,203 sn | %26,8 | 54,4 sn | %29,8 |
| cok-kesimli (fps kilitli, makine boşken) | 60,400 sn | 7,009 sn | %11,6 | 33,6 sn | %20,9 |

**Bütün bu süreler paylaşımlı makine damgalıdır ve damga burada gerçekten
bağlıyor:** aynı klibin aynı yoklaması yüklü makinede 14,552 sn, makine
boşaldığında 7,009 sn sürdü — **iki katından fazla fark.** Yukarıdaki oranlar
mertebe verir, kesin sayı vermez.

Mertebe şu: yoklama kaynak süresinin **onda biri ile üçte biri** kadar sürüyor
ve toplam kodlamaya **onda biri ile üçte biri** kadar ekliyor. 17 dakikalık bir
kaynakta bu, yoklamanın tek başına **birkaç dakika** demektir.

**Maliyet kazancı yiyor mu — içeriğe bağlı, ve iki içerik türünde yiyor.**
Durgun ve tek sahnelik kaynakta yoklama hiçbir şey değiştirmiyor (§ 2), yani
oradaki 16–18 sn **tamamen boşa gidiyor**. Çok kesimli kaynakta karşılığında
atlamada %22–47 kazanç var. Bugünkü yerleşimde yoklama kullanıcının Kodla'ya
basmasını beklemiyor, arka planda kestirimden sonra koşuyor; yine de kullanıcı
Kodla'ya erken basarsa haritasız kodlama alır.

**Ölçülmedi:** yoklamayı ucuzlatan hiçbir şey (kare atlatarak tarama, yalnız ilk
N saniye, ölçeği düşürme) bu sözleşmede ölçülmedi. Bunlar `SceneDetector`'ün işi
ve o dosya T109'un.

## 5. Harita üretilemezse

`TryBuildSceneMapAsync` hiçbir yolda fırlatmaz; dördü de `SceneMapAttempt`
döndürür ve düşüşün **sebebini taşır** — `Fallback` sayılabilir bir değer,
`Detail` ffmpeg'in kendi metni. Sessiz sayı üretimi yok: `Map` `null` olduğunda
çağıran zorunlu olarak `KeyframeCeilingDefaultSeconds` = 10 sn'ye düşer.

| Yol | `Fallback` | `Detail` | Üretilen `-g` |
| --- | --- | --- | --- |
| ffmpeg yok | `ScanFailed` | `SceneDetector.ScanAsync`'in yakaladığı süreç hatası | `fps × 10` |
| Yoklama başarısız (bozuk kaynak) | `ScanFailed` | ffmpeg'in `Invalid data found…` metni | `fps × 10` |
| Sonda karesi yok | `NoProbeFrames` | `sonda karesi yok` | `fps × 10` |
| Süre bilinmiyor | `NoDuration` | ölçülen süre değeri | `fps × 10` |

Dördüncü satır sözleşmenin istediği üçün dışında; `SceneMap.BuildDerived` süreye
bölerek çalıştığı için süresiz kaynakta tarama **hiç koşturulmuyor**, o kısayol
da pimli.

`SceneDetector.ScanAsync` süreç başlatma hatasını kendisi yakalayıp `Ok=false`
döndürüyor, yani "ffmpeg yok" bir istisna değil başarısız tarama olarak geliyor.
`SceneDetector.BuildMapAsync` ise başarısız taramada `InvalidOperationException`
fırlatıyor — üretim yolu onu değil `TryBuildSceneMapAsync`'i kullanıyor.

**Pimleyen ölçüler.** `tests/VidShrink.Tests/EncodeRunnerTests.cs`:

- `AFailedScanFallsBackToTheDefaultCeilingAndSaysSo` — dört düşüş biçimini de
  enjekte edilen tarayıcıyla kurar, `Fallback` ve `Detail`'i doğrular, sonra
  **dördü için de üretilen `-g`'yi** okur. ffmpeg gerektirmez.
- `ABrokenSourceFallsBackInsteadOfThrowing` (`[FfmpegFact]`) — 4096 sıfır baytlık
  bir dosyayı gerçek ffmpeg'e verir; fırlatmadığını ve `ScanFailed` döndüğünü
  ölçer.

`Skip` yok, ffmpeg yokluğunda sessiz erken dönüş yok: ffmpeg gerektiren ölçüler
`[FfmpegFact]` ile işaretli, geri kalanı ffmpeg'siz de koşar.

**Ölçülmedi:** düşüşün kullanıcıya **arayüzde** söylenmesi. `SceneMapAttempt`
sebebi taşıyor ama ekrana çıkaracak metin yok; yerelleştirme dosyaları
(`src/VidShrink.App/Locales/`, `Localization/Strings.cs`) bu sözleşmenin
`owns`'unda değil.

## 6. Başkasının belgesindeki eski künyeler

`docs/olcumler/tepe-tavani-ve-psy.md` T108'in `owns`'unda; **dokunulmadı.**

| Belgede yazan | Sözleşmenin bildirdiği | Bugünkü gerçek |
| --- | --- | --- |
| `MainWindow.axaml.cs:1796` | 1807 | **1808** (gösterilen komut) ve **2412** (gerçek kodlama) |
| `EncodeRunner.cs:244` | 246 | **333** (`EncodeArguments` → `FfmpegArguments.Build`) |

Aynı belge "harita yolu üretimde bağlı değil" diyor; bu sözleşmeden sonra o cümle
de yanlış. Düzeltmesi T108'in.

## 7. Mutasyon kanıtı

Her mutasyondan **önce** `dotnet build VidShrink.sln -c Release --no-incremental`,
sonra `dotnet test … --no-build --filter "EncodeRunnerTests|PreviewSegmentTests|FfmpegArgumentsTests"`.
Düzenek `.calisma/t113/mutasyon.sh`: yamayı uygular, derler, koşar, kaynağı geri
alır. Mutasyonsuz taban **93 geçti / 1 kaldı / 0 atlandı / 94 toplam**; kalan tek
ölçü T108'in `FfmpegArgumentsTests.cs:408` kaynak-metin pimi (§ 8).

| # | Bozulan bağlantı | Yama | Kırmızıya dönen ölçü |
| --- | --- | --- | --- |
| M1 | `MainWindow.axaml.cs:1808` gösterilen komut | `_sceneMap?.Map` → `null` | `TheWindowFeedsTheMapToTheDisplayedCommand` |
| M2 | `MainWindow.axaml.cs:2412` gerçek kodlama | `_sceneMap?.Map` → `null` | `TheWindowFeedsTheMapToTheEncode` |
| M3 | `MainWindow.axaml.cs:1529` harita üretimi | çağrı → `_sceneMap = null` | `TheWindowBuildsTheMapWhenTheSourceLoads` |
| M4 | `PreviewSegment.cs:170` ön izleme | `scenes` → `null` | `Onizleme_haritayi_arguman_uretimine_gecirir`, `Onizleme_ve_nihai_kodlama_ayni_anahtar_kare_araligini_verir` |
| M5 | `EncodeRunner.cs:333` argüman üretimi | `scenes` → `null` | `EncodeArgumentsCarryTheSceneMapCeiling`, `DisplayedCommandCarriesTheSameCeilingAsTheEncode`, `TheMapChangesTheIFrameCountOfTheDeliveredFile` |
| M6 | `EncodeRunner.cs:341` geçiş başına koşum | `scenes` → `null` | `TheMapChangesTheIFrameCountOfTheDeliveredFile` |

**Altı mutasyonun altısı da öldü, ve her biri kendi ölçüsünü kırıyor** — biri
bağlıyken öteki bağsız kalırsa yakalanır.

M6 bu tablonun sebebidir: `EncodeArguments` haritayı geçirmeye devam ederken
`RunOneAsync` onu düşürürse **argüman üreten ölçülerin hepsi yeşil kalır.** O
mutasyonu yalnız `TheMapChangesTheIFrameCountOfTheDeliveredFile` yakalıyor; o
ölçü argüman değil **teslim edilen dosyanın I-kare sayısını** okuyor (30 sn
`testsrc2`, hem crf hem 2 geçiş kipinde, haritalı 6 haritasız 3 I-kare).

**Dürüst kayıt: M1–M3 davranışla değil kaynak metniyle pimli.** `MainWindow`
başsız koşumda kurulamıyor, o yüzden üç arayüz bağlantısı
`TipSources.WindowCodePath` üzerinden dosya metni okunarak pimlendi. Bu projedeki
mevcut kalıptır (`FfmpegArgumentsTests.cs:405` aynısını yapıyor) ama davranış
ölçüsü değildir: `MainWindow`'un o satırları başka bir yoldan etkisizleştiren bir
değişikliği yakalayamaz.

## 8. `null` varsayılanı

`FfmpegArguments.Build`/`BuildSegment` imzasındaki `SceneMap? scenes = null` bu
kusurun sebebidir: çağıran parametreyi unutunca derleyici susuyor.
`FfmpegArguments.cs` T108'in; **öneri olarak yazılıyor — varsayılan kaldırılsın**,
üç çağıran da açıkça karar versin. Kaldırılırsa dokunulacak çağıranlar:

- `src/VidShrink.Core/PreviewSegment.cs:170` — bu sözleşmede açıkça geçiyor
- `src/VidShrink.Ffmpeg/EncodeRunner.cs:333` — bu sözleşmede açıkça geçiyor
- `tests/VidShrink.Tests/FfmpegArgumentsTests.cs:94,125,312,313` (T108'in)

`PreviewSegment.For`'un kendi `scenes = null` varsayılanı da **kaldırılamadı**:
`src/VidShrink.App/Playback/SegmentEncoder.cs:118` onu 6 argümanla çağırıyor ve o
dosya `owns` dışında.

**T108'in bir ölçüsü bu sözleşme yüzünden kırmızı.**
`FfmpegArgumentsTests.cs:408`, `MainWindow` kaynak metninde
`BuildUniqueOutputPath(_info.FilePath, "shrunk", "mp4"), _encoders));` dizesini
arıyor; çağrıya `, _sceneMap?.Map` eklenince pim tutmuyor. Dosya T108'in
`owns`'unda, **dokunulmadı**. Tek satırlık düzeltme:

```csharp
Assert.Contains("BuildUniqueOutputPath(_info.FilePath, \"shrunk\", \"mp4\"), _encoders, _sceneMap?.Map));", windowSource);
```

## 9. Ölçülmedi

- **VMAF, üç klibin ikisinde.** Sebepleri § 2'de; geçersiz sayı tabloya
  yazılmadı. Çok kesimli klipte kalite ancak fps kilitli ek koşumda ölçülebildi.
- **Arayüzdeki ön izleme ayrışması** (§ 3) yalnız kod okumasıyla saptandı;
  ayrışmanın kullanıcı çıktısındaki büyüklüğü **ölçülmedi.**
- **`SceneMap.Threshold = NaN` aritmetiğe giriyor mu.** `grep` ile bakıldı,
  **ölçülmedi**: `SceneMap.Threshold` üretim kodunda hiçbir yerde okunmuyor; tek
  okuyan `tests/VidShrink.Tests/SceneMapTests.cs:340,571` ve ikisi de
  `double.IsNaN(...)` iddiası. `tools/harita-baglantisi/Program.cs` de yalnız
  `double.IsNaN` ile basıyor. Yani bugün sessizce aritmetiğe giren bir `NaN` yok.
- **Yoklamayı ucuzlatan seçenekler** (§ 4).
- **Düşüşün arayüzde söylenmesi** (§ 5).
- **Donanım kodlayıcı yolu.** `HardwareKeyframeCeilingSeconds` = 5,0 sn bu
  sözleşmede hiç ölçülmedi; üç klip de yazılım kodlayıcıya düştü.
- **Uzun kaynak.** Bütün ölçümler 60 sn'lik kliplerde. 17 dakikalık kaynakta
  yoklama maliyeti oranla değil **doğrusal** büyür ve o hiç ölçülmedi.
