# HandBrake motoru — kaynaktan okuma

T112. Ürünün en üst hedefi "piyasadaki en iyi auto mod" ve ölçtüğümüz tek rakip
HandBrake. Bugüne kadar onu yalnız çıktısından tanıdık. Bu belge kaynağından
tanıyor.

## 0. Ne okundu, ne okunmadı

Klon: `github.com/HandBrake/HandBrake`, `.calisma/handbrake/`,
commit `1d2135bc84d3587f282337aa273dd5d363e1f4cd` (2026-08-31).
Sığ (`--depth 1`), blobsuz, seyrek çekim — çekilen yollar: `libhb/`,
`win/CS/HandBrakeWPF/`, `win/CS/HandBrake.Interop/`, `preset/`.

**Okunmadı:** `macosx/`, `gtk/`, `test/` (HandBrakeCLI'nin kendi argüman
ayrıştırması), `contrib/`. Bu belgede macOS ve GTK arayüzlerine dair hiçbir
iddia yok. HandBrakeCLI'nin bayrakları hakkında yazdığım tek şey, T102'nin
kendi komut satırından okunandır — CLI kaynağından değil.

Bu belgedeki her iddianın `dosya:satır` künyesi var. Künyesiz cümle yok; olması
gereken yerde künye üretemediysem **"bulamadım"** yazılı ve neyi arayıp
bulamadığım da yazılı.

Satır numaraları yukarıdaki commit'e aittir.

Kısa yazım: `libhb/...` ve `win/CS/...` HandBrake klonuna, `preset/...` klonun
`preset/preset_builtin.json` dosyasına aittir. Yalnız dosya adıyla geçen künyeler
bizim depomuzdadır: `PlanCalculator.cs`, `PlanParser.cs`, `SceneMap.cs`,
`ComplexityProfile.cs`, `FfmpegArguments.cs` →
`src/VidShrink.Core/`; `EncodeRunner.cs`, `ComplexityProbe.cs`, `SceneDetector.cs`,
`QualityMeter.cs` → `src/VidShrink.Ffmpeg/`.

---

## 1. Hedef boyut yolu

### Bulgu: HandBrake'te hedef boyut yolu yok

Aradığım şey: kullanıcının verdiği dosya boyutunu bit hızına çeviren kod.
`target_size`, `TargetSize`, `targetSize`, `calc_bitrate`, `CalculateBitrate`,
`filesize` desenlerini `libhb/` ve `win/CS/` altında `.c/.h/.cs/.json`
uzantılarında taradım.

Tek anlamlı isabet:

> `win/CS/HandBrakeWPF/Model/Video/VideoEncodeRateType.cs:17` — `TargetSize = 0,`

Bu enum değeri **hiçbir yerde okunmuyor.** `VideoEncodeRateType` geçen 26 satırın
hepsini taradım; hiçbiri `TargetSize` ile karşılaştırma yapmıyor. Karar veren iki
yer var ve ikisi de yalnız diğer iki değeri tanıyor:

- `win/CS/HandBrakeWPF/ViewModels/VideoViewModel.cs:110-130` — `IsConstantQuantity`
  yalnız `ConstantQuality` ile `AverageBitrate` arasında gidip geliyor. Üçüncü
  seçenek yok.
- `win/CS/HandBrakeWPF/Services/Encode/Factories/EncodeTaskFactory.cs:280-288` —
  işe yalnız `Quality` (CQ ise) ya da `Bitrate` (ABR ise) yazılıyor. `TargetSize`
  dalı yok.

`libhb` tarafında da boyut→bit hızı çevirimi yok: `hb_job_t`'de hedef boyut alanı
bulamadım (`libhb/handbrake/common.h` içinde `vbitrate` ve `vquality` var, boyut
yok).

**Sonuç:** HandBrake'in kullanıcıya sunduğu iki mod var — sabit kalite (CRF/CQ)
ve ortalama bit hızı. **"Şu boyuta sığdır" diye bir mod yok.** `TargetSize = 0`
enum değeri ölü bir kalıntı. Ürünümüzün merkezinde duran soruya HandBrake'in
cevabı, cevap vermemek.

Bu, T102'de HandBrake koşumunun boyutunun **elle** eşitlenmiş olmasının sebebi:
`docs/olcumler/auto-mod.md` § "K3 — Uzman açığı" — "boyut eşlemesi elle yapıldı … HandBrake
için iki koşum" (şu an `:223-225`). Yani T102'de HandBrake'e bizim yaptığımız işi yaptırmadık;
insan yaptı.

### İki geçiş nasıl kuruluyor

Geçişleri kuran tek yer `hb_job_setup_passes`:

- `libhb/hb.c:1958-1974` — `job->multipass` açıksa kodlayıcının istediği kadar
  `HB_PASS_ENCODE_ANALYSIS` geçişi, ardından bir `HB_PASS_ENCODE_FINAL` eklenir;
  kapalıysa tek `HB_PASS_ENCODE`.
- `libhb/handbrake/common.h:1487-1489` — geçiş kimlikleri.

Kapı, geçişlerden önce:

- `libhb/hb.c:1946-1949` — `vquality` geçerliyse (yani CRF modundaysak) ve
  `hb_video_multipass_is_supported(codec, 1)` sıfır dönüyorsa `multipass = 0`.
- `libhb/common.c:1946-1947` — o fonksiyonun `default` dalı
  `return !constant_quality;`. x264/x265 bu dala düşüyor
  (`libhb/common.c:1909-1944` arasındaki `case` listesinde yoklar).

**Sonuç:** CRF modunda x264/x265 için çok geçiş **kapatılıyor.** Preset'lerdeki
`"VideoMultiPass": true` alanı CRF preset'lerinde etkisiz — aşağıda §4'te bunun
ne demek olduğu var.

İki geçişin gerçek mekanizması yalnız ABR dalında:

- `libhb/encx264.c:541-564` — `X264_RC_ABR`, geçici `x264.log` dosyası;
  `ANALYSIS` geçişi `b_stat_write=1`, `FINAL` geçişi `b_stat_read=1`.
- `libhb/encx264.c:534-539` — CRF dalı. Burada `pass_id` hiç okunmuyor, istatistik
  dosyası açılmıyor.
- `libhb/encx264.c:604-608` — `fastanalysispass` ise ilk geçişe
  `param_apply_fastfirstpass` (HandBrake preset alanı `VideoTurboMultiPass`,
  eşlemesi `libhb/preset.c:2209-2217`).

Yani HandBrake'in iki geçişi **x264'ün kendi iki geçişidir**; HandBrake yalnız
geçişleri sıraya koyup istatistik dosyasının yolunu veriyor. Bit hızı hedefi
x264'ün ABR denetleyicisine bırakılıyor.

### Hedef aşımında ne oluyor

**Hiçbir şey.** Aradığım şey: teslim edilen boyutu hedefle karşılaştırıp yeniden
kodlayan bir döngü. Bulduğum tek karşılaştırma bir günlük satırı:

- `libhb/muxcommon.c:564-568` — muxer kapanırken, CQ modunda değilse,
  `track->bytes - mux->pts * job->vbitrate * 125 / 90000` hesaplanıp
  `hb_deep_log(2, "mux: video bitrate error, %+... bytes")` ile yazılıyor.

`hb_deep_log` seviye 2 — normal günlükte bile görünmüyor. Sonuç hiçbir yere
beslenmiyor, yeniden kodlama yok, düzeltme yok. Aynı dosyada `:576` satırında
kare başına kapsayıcı ek yükü de aynı şekilde yalnız yazılıyor.

`libhb/work.c` tarafında da geri besleme bulamadım: `vbitrate` geçen tek satır
`libhb/work.c:649`, o da günlüğe iş ayarlarını basan satır.

**Karşılaştırma:** bizde bu döngü var ve ürünün kalbi —
`src/VidShrink.Ffmpeg/EncodeRunner.cs:88-165`: teslim edilen boyut ölçülür
(`:88`), kodlayıcı verimi hesaplanır (`:89`), bant dışındaysa plan düzeltilir
(`:115` ve `:163`, `PlanCalculator.Correct`), yeniden kodlanır. Düzeltmenin
matematiği `src/VidShrink.Core/PlanCalculator.cs:525-551`.

---

## 2. Uyarlanabilirlik ızgarası

Soru: HandBrake içerikten türeyen ne yapıyor, ve **kararı kendi mi veriyor yoksa
kodlayıcıya mı devrediyor?**

| Mekanizma | İçerikten mi türüyor | Kararı kim veriyor | Künye |
|---|---|---|---|
| Otomatik kırpma (crop) | evet — 10 önizleme karesinin kenar taraması | **HandBrake** | `libhb/scan.c:1279-1313`, `libhb/preset.c:2361-2367` |
| Taramalı (interlace) yoklaması | evet — 10 önizleme karesinde tarak tespiti | **HandBrake tespit ediyor, kimse kullanmıyor** | `libhb/scan.c:972-977`, `:1421-1430` |
| `comb-detect` → seçici `decomb` | evet — kare başına tarak sınıflaması | **HandBrake** | `libhb/comb_detect.c:1029-1048`, `libhb/work.c:1416-1433`, `libhb/decomb.c:502-506` |
| `detelecine` (pullup) | evet — alan eşleme | **HandBrake** (filtre açıksa) | `libhb/detelecine.c:448`, `:851-855` |
| `pfr` kare düşürme | kısmen — **hangi** karenin düşeceği hareket ölçüsüyle | **HandBrake** | `libhb/vfr.c:133-182`, mod açıklaması `:186-205` |
| `pfr/cfr/vfr` **modunun seçimi** | hayır — preset alanı | preset (sabit) | `libhb/preset.c:2019-2025` |
| `--auto-anamorphic` ölçek | hayır — kaynak PAR/DAR + preset üst sınırları | preset + kaynak üstverisi | `libhb/hb.c:1540-1600` |
| Anahtar kare aralığı (keyint) | hayır — yalnız kare hızından | **HandBrake, sabit formül** | `libhb/encx264.c:388-391`, `libhb/encx265.c:188-190` |
| `scenecut` | evet | **kodlayıcıya devir** | `libhb/encx264.c:1529-1537` (yalnız varsayılandan farklıysa yazdırılır); `encx265.c`'de hiç geçmiyor |
| `aq-mode` / `aq-strength` | evet | **kodlayıcıya devir** | `libhb/encx264.c:1949-1967` |
| `rc-lookahead` | evet | **kodlayıcıya devir** | `libhb/encx264.c:1978-1987` |
| `mbtree` | evet | **kodlayıcıya devir** | `libhb/encx264.c:1969-1977` |
| Bit hızı / CRF'nin içeriğe göre ayarı | — | **yok** | (aranıp bulunamadı, §3) |

### "Kodlayıcıya devir" satırlarının okunuşu

`libhb/encx264.c:1949-1986` satırları bir karar mekanizması **değil.** Bulundukları
fonksiyon `hb_x264_param_unparse` (`libhb/encx264.c:1363`) ve işi, x264 preset+tune
uygulandıktan sonra ortaya çıkan parametreleri **x264'ün kendi varsayılanlarıyla
karşılaştırıp farkları bir sözlüğe yazmak.** `aq-mode` varsayılandan farklı değilse
sözlükten siliniyor (`:1954-1957`). Yani HandBrake `aq-mode`'u seçmiyor; x264'ün
seçtiğini görünür kılıyor. Yorum satırları bunu kendisi söylüyor:
`:1951` — "can be modified by: preset ultrafast, tune psnr".

**Bu ayrımın kullanıcıya ulaşan sonucu var mı?** Var, ama iddiamızın lehine değil.
T102'de HandBrake çıktısının anahtar kareleri
`0,02 / 10,02 / 20,02 / 28,35 / 38,35 / 48,35 / 56,87` idi
(`docs/olcumler/auto-mod.md` § "K4 — Açığın ayar başına ayrıştırması" > "En büyük kalem: anahtar kare aralığı" — "HandBrake'in anahtar kare zamanları 0,02 / 10,02…", şu an `:320`). 10 saniyelik tavan `libhb/encx265.c:188-190`'in
sonucu (60 fps × 10). Aradaki `28,35` ve `56,87` ise **tam olarak kaynağın iki
sahne kesmesi** (`docs/olcumler/auto-mod.md` § "K4 — Açığın ayar başına ayrıştırması" > "En büyük kalem: anahtar kare aralığı" — "Çıktı: pts_time 28.353 (skor 0,314)…", şu an `:314-318`, ölçülen skorlar 0,314 ve
0,261). Bu iki anahtar kareyi HandBrake koymadı — x265'in `scenecut`'ı koydu.

Sonuç kullanıcının dosyasında duruyor ve kimin koyduğunu sormuyor. **"HandBrake
sahne analizi yapmıyor" doğru; "HandBrake'in çıktısında sahne uyarlaması yok"
yanlış.** İkincisini söylersek yanılırız.

### Önizleme taramasının ölçüsü

HandBrake'in kodlamadan önceki tek içerik analizi önizleme taraması:

- `libhb/hb.c:422-424` — çağıran `preview_count` vermezse **10.**
- `libhb/scan.c:972-977` — her önizleme karesinde `hb_detect_comb`.
- `libhb/scan.c:1421-1430` — önizlemelerin yarısı ya da fazlası taraklıysa
  `title->detected_interlacing = 1` ve günlüğe "You should do something about that."
- `libhb/scan.c:1279-1313` — kırpma değerleri önizlemeler üzerinden medyan +
  eşik ile seçiliyor.

10 kare. Süreye bakılmaksızın 10. Bu, HandBrake'in içerik yoklamasının tamamı.

---

## 3. "Statik" iddiasının sınavı

Ön kabulümüz şuydu: *HandBrake sabit ayar takımı uyguluyor, içeriğe göre plan
kurmuyor.* Kaynağa göre bu **kısmen doğru, kısmen yanlış** — ve yanlış olan kısım
bizi ilgilendiriyor.

### Doğru olan kısım: bit bütçesi içerikten hiç etkilenmiyor

`libhb/preset.c` içinde `title->` geçen 26 satırın hepsini okudum
(`:554-1454` ses ve altyazı iz seçimi, `:2350-2367` geometri ve kırpma,
`:2430` geometri, `:2580` bölüm sayısı). **Hiçbiri video kalitesine ya da bit
hızına dokunmuyor.** `VideoQualitySlider` ve `VideoAvgBitrate` preset'ten
olduğu gibi alınıp işe yazılıyor.

Ve `detected_interlacing` — HandBrake'in kaynak hakkında öğrendiği en güçlü şey —
**hiçbir kararı beslemiyor.** Tüm tüketicileri:

- `libhb/scan.c:1425` ve `:1429` — yazılıyor
- `libhb/hb_json.c:297` — JSON'a `"InterlaceDetected"` diye aktarılıyor
- `win/CS/HandBrake.Interop/Interop/Json/Scan/SourceTitle.cs:82` — C# tarafında
  bir özelliğe okunuyor

Ve orada bitiyor. `InterlaceDetected` deseni tüm depoda bu üç yerden başka hiçbir
yerde geçmiyor. **HandBrake taramalı içeriği tespit ediyor, kullanıcıya
"bir şeyler yapmalısın" diyor, kendisi hiçbir şey yapmıyor.**

Yani bu eksende iddiamız ayakta: HandBrake'in planı içerikten türemiyor.

### Yanlış olan kısım: kodlama sırasında kare başına uyarlama var

Ama "statik" kelimesi geniş ve o genişlikte iddia tutmuyor. HandBrake'in
varsayılan preset'i çalışırken **kare başına karar veren** iki zinciri var:

**Tarak zinciri.** `Fast 1080p30` preset'inde
`"PictureDeinterlaceFilter": "decomb"` ve `"PictureCombDetectPreset": "default"`
(`preset/preset_builtin.json:1072`). Bu ikisi birlikte şu zinciri kuruyor:

1. `libhb/comb_detect.c:1029-1048` — her kare `HB_COMB_NONE` / `LIGHT` / `HEAVY`
   diye sınıflanıyor.
2. `libhb/work.c:1416-1433` — comb-detect filtresi listedeyse decomb'a
   `MODE_DECOMB_SELECTIVE` biti ekleniyor.
3. `libhb/decomb.c:502-506` — o bit varken, taraklı olmayan kare
   **deinterlace edilmeden** kopyalanıp geçiyor.

Bu, kare çözünürlüğünde içerik uyarlamasıdır ve HandBrake'in kendi kodundadır.

**Kare düşürme zinciri.** `pfr` modunda hangi karenin düşeceğine
`libhb/vfr.c:133-182` karar veriyor ve kararı hareket ölçüsüne dayanıyor:
`:158-171` — pencere içindeki en düşük `frame_metric` değerli kare seçiliyor.
Sabit bir "her n'inci kareyi at" kuralı değil.

### İddiamızın hangi kısmı ayakta

Kullanıcının cümlesi şuydu: *"atıyorum 2 sn'lik pencereleri analiz etmek gibi
statik bir yöntem yerine … dinamik bir işleyişimiz olacak."*

Kaynağı okuduktan sonra bu cümlenin üç parçasını ayırmak gerekiyor.

**Ayakta kalan (en güçlü):** *hedef boyuta kapalı çevrim.* HandBrake'te yok — §1.
Teslim edilen dosyayı ölçüp planı düzelten ve yeniden kodlayan bir mekanizma
HandBrake'in hiçbir yerinde bulunmuyor; en yakını `libhb/muxcommon.c:567`
günlük satırı. Bizde `src/VidShrink.Ffmpeg/EncodeRunner.cs:88-165`. Bu, iddianın
ölçülebilir ve savunulabilir kısmı.

**Ayakta kalan (ikincil):** *yerleşimin içerikten türetilmesi.* HandBrake'te
çözünürlük ve kare hızı preset alanıdır (`preset/preset_builtin.json:1008` ve
`:1098-1099`, kaynak üstverisiyle sınırlanır — `libhb/hb.c:1540-1600`). Bizde
`src/VidShrink.Core/PlanCalculator.cs:622` (`SearchLayout`) ölçek×fps ızgarasını
**ölçülen karmaşıklık profiliyle** puanlayıp seçiyor.

**Ayakta kalmayan:** *"biz dinamiğiz, o statik."* Bu cümle iki yerden kırılıyor.

Birincisi yukarıda: HandBrake'in kendi kodunda kare başına karar var
(comb-detect → seçici decomb, pfr hareket ölçüsü), ve kodlayıcıya devrettiği
`scenecut`/`aq-mode`/`mbtree` kullanıcının dosyasında içerik uyarlaması olarak
görünüyor — T102'nin anahtar kare zamanları bunun ölçülmüş kanıtı.

İkincisi bizde. Kullanıcının "statik yöntem" örneği olarak verdiği şey —
**2 saniyelik pencere analizi** — bugün bizim yaptığımız şeydir:

- `src/VidShrink.Ffmpeg/ComplexityProbe.cs:14` — `WindowSeconds = 2.0`
- `src/VidShrink.Ffmpeg/ComplexityProbe.cs:17` — `MaxWindows = 3`
- `src/VidShrink.Ffmpeg/ComplexityProbe.cs:131-143` — süre ne olursa olsun
  en fazla **3 pencere**, kaynağa eşit aralıkla serpilmiş
- `src/VidShrink.Core/ComplexityProfile.cs:87` — `SampleWindowSeconds = 2.0`

Yani bir saatlik videoyu 6 saniyeye bakarak planlıyoruz. HandBrake 10 önizleme
karesine bakıyor. İkisi de sabit ölçekli örnekleme; bizimki daha büyük bir örnek,
**ama cinsi aynı.**

Ve sahne haritamız — dinamikliğin taşıyıcısı olarak yazılmış parça — plana hiç
girmiyor:

- `src/VidShrink.Core/SceneMap.cs:20-46` — sahne sınırları ve sahne başına
  karmaşıklık üretiliyor
- `src/VidShrink.Ffmpeg/SceneDetector.cs:123-131` — haritayı kuran tek yer
- Tüketicileri: `src/VidShrink.Ffmpeg/QualityMeter.cs:177` ve `:219` — yalnız
  **VMAF örneklerinin nereden alınacağını** seçmek için.
- `src/VidShrink.Core/PlanCalculator.cs` içinde `SceneMap` geçmiyor.

**`SceneMap` bir ürün yeteneği değil, bir ölçüm aracı.** Yol haritası adım 6
onu plana bağlamayı öngörüyor; bugün bağlı değil.

**Bu bölümün özeti:** iddiamızın kapalı çevrim kısmı kaynakla doğrulandı ve
güçlü. "Dinamik analiz" kısmı bugün doğru değil — ne yaptığımız açısından da,
HandBrake'in ne yaptığı açısından da. Dışarıya bu cümleyi bugünkü kodla
kuramayız.

---

## 4. Preset'ler veri olarak

HandBrake'in yerleşik preset'leri `preset/preset_builtin.json` (12 440 satır).
Derleme sırasında `libhb/handbrake/preset_builtin.h`'ye gömülüyor ve
`libhb/preset.c:4435-4448`'de JSON olarak geri ayrıştırılıyor.

### "Fast 1080p30" ↔ "Very Fast 1080p30"

İki preset **92 ortak alan** taşıyor ve yalnız 5'i farklı (biri sırf açıklama
metni, biri `Default` bayrağı):

| Alan | Very Fast 1080p30 | Fast 1080p30 | Bizdeki karşılığı |
|---|---|---|---|
| `VideoPreset` | `"veryfast"` (`:332`) | `"fast"` (`:1012`) | var — `EncodePlan.Preset`, auto'da libsvtav1 preset 6 (`docs/olcumler/auto-mod.md` § "K3 — Uzman açığı" — "Motor libsvtav1, preset 6, -g 120…", şu an `:229`) |
| `VideoQualitySlider` | `24.0` (`:338`) | `22.0` (`:1018`) | var — `PlanCalculator.cs:226` (`budgetCrf`), **ama bütçeden türetiliyor, sabit değil** |
| `VideoAvgBitrate` | `4000` (`:324`) | `6000` (`:1004`) | var — `PlanCalculator.cs:156` (`videoK`), hedef boyuttan türetiliyor |
| `PictureCombDetectPreset` | `"fast"` (`:392`) | `"default"` (`:1072`) | **yok** |
| `PresetDescription` / `Default` | metin / `false` | metin / `true` | — |

Değişmeyen ve bizde karşılığı olmayan alanlar (ikisinde de aynı):

| Alan | Değer | Bizdeki karşılığı |
|---|---|---|
| `PictureDeinterlaceFilter` (`:1074`) | `"decomb"` | **yok** — deinterlace hiç yapmıyoruz |
| `PictureCropMode` (`:1062`) | `0` (otomatik) | **yok** — otomatik kırpma yok |
| `PictureDetelecine` (`:1089`) | `"off"` | yok (HandBrake'te de kapalı) |
| `PictureDenoiseFilter` (`:1077`) | `"off"` | yok (HandBrake'te de kapalı) |
| `PictureSharpenFilter` (`:1086`) | `"off"` | yok (HandBrake'te de kapalı) |
| `VideoFramerateMode` (`:1009`) | `"pfr"` | kısmen — fps düşürüyoruz (`PlanCalculator.cs:672`) ama `fps=` filtresiyle sabit hedef kare hızı yazıyoruz (`FfmpegArguments.cs:130-131`); PFR karşılığı yok |
| `VideoProfile` / `VideoLevel` (`:1014-1015`) | `"main"` / `"4.0"` | **yok** — plan kendiliğinden üretmiyor; `PlanParser.cs:149-150` yalnız elle yazılmışı doğruluyor |
| `VideoMultiPass` (`:1019`) | `true` | var — `EncodeRunner.cs:77-81` |
| `VideoTurboMultiPass` (`:1020`) | `true` | **yok** — hızlı ilk geçiş yok |

`VideoQualityType: 2` (`:337` ve `:1017`) = `ConstantQuality`
(`VideoEncodeRateType.cs:19`). **Yerleşik preset'ler CRF ile geliyor.** Bu iki
sonucu doğuruyor:

1. `VideoAvgBitrate` alanı bu preset'lerde ölü — kullanıcı modu elle
   değiştirmedikçe okunmuyor (`EncodeTaskFactory.cs:285-288`).
2. `VideoMultiPass: true` de bu preset'lerde ölü — §1'deki
   `libhb/common.c:1946-1947` + `libhb/hb.c:1946-1949` kapısı x264'ün CRF
   modunda çok geçişi kapatıyor.

### T102'nin `uzman-hb2` koşumu hangi preset'ti

**Hiçbiri.** Komut (`docs/olcumler/auto-mod.md` § "K3 — Uzman açığı" — "Koşum adı uzman-hb2: HandBrakeCLI -e x265_10bit…", şu an `:231`):

```
HandBrakeCLI -e x265_10bit --encoder-preset slow --multi-pass --turbo \
  -E ca_aac -B 128 -w 1920 -l 1080 --crop-mode none -r 60 --cfr -b 1900
```

Preset adı verilmemiş; alanlar tek tek bayrakla kurulmuş. Bayrakların preset
alanı karşılıkları ve bizdeki durum:

| Bayrak | Preset alanı karşılığı | Bizde | Not |
|---|---|---|---|
| `-e x265_10bit` | `VideoEncoder` | var | auto libsvtav1 seçti |
| `--encoder-preset slow` | `VideoPreset` | var | auto preset 6 seçti |
| `-b 1900` + `--multi-pass` | `VideoAvgBitrate` + `VideoMultiPass`, `VideoQualityType: 1` | var | ABR olduğu için HandBrake'in iki geçişi burada **gerçekten** çalışıyor (`encx264.c:541-564`'ün x265 karşılığı `encx265.c:434-456`) |
| `--turbo` | `VideoTurboMultiPass` | **yok** | |
| `--crop-mode none` | `PictureCropMode: 2` | — | otomatik kırpma bilerek kapatılmış |
| `-r 60 --cfr` | `VideoFramerate` + `VideoFramerateMode: cfr` | var | |
| `-E ca_aac -B 128` | ses alanları | var | |
| (verilmedi) | `PictureDeinterlaceFilter`, `PictureCombDetectPreset` | **yok** | CLI varsayılanını okumadım — `test/` çekilmedi, **bulamadım** |

Yani T102'nin karşılaştırdığı HandBrake, yerleşik preset'lerden farklı olarak
**ABR + iki geçiş** modundaydı. Bizim auto modumuz da aynı koşumda iki geçişli
VBR'daydı. Bu ikisi karşılaştırılabilir; ama HandBrake'in **kutudan çıkan hâli**
bu değil — kutudan CRF çıkıyor ve boyut hedefi diye bir şey sunmuyor.

---

## 5. Bizim tarafımız

Karşılaştırmanın bizim yakamızdaki künyeleri (yukarıda geçenler tekrar edilmedi):

| Ne | Nerede | Durum |
|---|---|---|
| Hedef boyut → bit bütçesi | `PlanCalculator.cs:154-156` | üretimde |
| Hedef boyut → CRF | `PlanCalculator.cs:225-226` | üretimde |
| Kapalı çevrim düzeltme | `EncodeRunner.cs:88-165`, `PlanCalculator.cs:525-551` | üretimde |
| Ölçülen kodlayıcı verimi | `PlanCalculator.cs:399-407` | üretimde |
| Doldurma bandı | `PlanCalculator.cs:25-33` (`FillBand.For`) | üretimde |
| Yerleşim araması (ölçek×fps) | `PlanCalculator.cs:622` | üretimde |
| Karmaşıklık profili | `ComplexityProbe.cs:62-95`, `ComplexityProfile.cs:251-276` | üretimde, **3 × 2 s pencere** |
| Pencere yanlılığı taraması | `ComplexityProbe.cs:20-27` (40 nokta × 1 s), `:293-301` | üretimde |
| Psikogörsel sabitleri | `FfmpegArguments.cs:264-269` | üretimde, **sabit** |
| Anahtar kare aralığı | `FfmpegArguments.KeyframeArgs` | üretimde, **aralık**: taban 1 s, tavan sahne haritasından 5-10 s'ye kelepçeli. T112'de burada `-g = max(2, round(fps × 2))` sabiti yazıyordu; T98 (`8ea80c4`) dinamiğe çevirdi |
| Sahne haritası | `SceneMap.cs:20-46`, `SceneDetector.cs:123-131` | **plana bağlı değil**; yalnız `QualityMeter.cs:177,219` |

---

## 6. Açık uçlar

Her madde: ne, hangi dosyaya, hangi ölçüyle sınanır. Sözleşme açmıyorum; liste
sözleşme yazılabilecek somutlukta.

### Sıra neye göre çizildi

Bu bölümün önceki sürümü sırayı **tek bir sayıdan** türetmişti — T102'nin
auto ↔ uzman-hb2 farkından — ve kendi uyarısını da yazmıştı: o sayı kirliydi,
"T111 kapandığında sıra yeniden çizilmelidir". **T111 kapandı**
(`.claude/relay/contracts/done/T111.md`, `status: done`) ve sıra burada, T135'te
yeniden çizildi.

Aşağıdaki toplu hüküm cümleleri **elle yazılmadı.**
`python tools/harita-tazeleme/harita-tazeleme.py hukum` çıktısından birebir
alındı; aynı betiğin `dogrula` alt komutu her cümlenin bu belgede birebir durduğunu
ve her düzeltilmiş sayının kaynağında bulunduğunu denetliyor.

Ölçülmüş HandBrake açığı bugün ortalamada +0,097, p10'da +0,477, harmonikte
+0,099; bu belgenin sırayı çizdiği eski ortalama açığı +1,269 idi, yani 13,1 kat
büyük okunmuş. (`docs/olcumler/auto-mod.md` § "K3 — AV1 ↔ HandBrake yeniden
ölçüldü" — tablo satırı "HandBrake açığı | T111 yeni (**kilitli**)", şu an `:672`)

Eski cümlenin taşıdığı iki ham ortalamanın kilitli karşılığı: auto **95,647**,
uzman-hb2 **95,743** (aynı belge § "Boyut eşlemesi — band yoklandı, yöntem ve
deneme sayısı" — tablo başlığı "açık (auto = 95,647)" ve tablo satırı
"`uzman-hb2` | 1900 kbps", şu an `:703` ve `:711`). Bu çift T111'in yeniden
ürettiği auto'ya eşlendi; o auto **15 496 155** bayt teslim etti, T102'nin
belgelediğinden %1,72 küçük. Yani eski `94,462 ↔ 95,731` çifti yalnız bayat
değil, başka bir auto'ya ait.

Açığın ayakta kaldığı eksen p10: +0,477, yeniden üretim sınırının (0,023) 20,7
katı. Ortalamadaki +0,097 kendi sınırının (0,013) 7,5 katı — sıfır değil, ama
p10'un 4,9 katı küçüğü. (Yeniden üretim sınırı: aynı belge § "Kilidin tek başına
etkisi — aynı dosya, iki ölçüm" — tablo satırı "on bir koşumun hepsi", şu an
`:604`.)

Harmonik artık ayrı bir eksen değil: kilitli ölçümde harmonik açığı (+0,099)
ortalama açığından (+0,097) 0,002 uzakta.

Auto'nun bugünkü en büyük ölçülmüş açığı HandBrake'e karşı değil, kendi uzman
ayarlarımıza karşı: uzman açığı ortalamada +0,437, HandBrake açığının 4,5 katı;
p10'da +0,673, 1,4 katı. (Aynı belge § "K2 — yanlılığın büyüklüğü: eski fark,
yeni fark" — tablo satırı "uzman açığı | T111 yeni (**kilitli**)", şu an `:656`.)

**Ölçüt — sıra artık neye bakıyor.** Sırayla:

1. **Ölçülmüş p10 etkisi.** Açığın ayakta kaldığı tek eksen orası.
2. **Ölçülmüş ortalama etkisi.** Ayakta ama küçük; birinciyi bozmadan ayırıyor.
3. Ölçülmemiş maddelerde: **ölçümü yürüyen bir sözleşme var mı.** Ölçülecek olan,
   ölçülmesi planlanmayanın üstünde.
4. Kalanda: kuyruğa herhangi bir kaynak sınıfında dokunabiliyor mu.
5. Ürün kapsamı ve süre maddeleri sonda.

Harmonik ölçüt **değil** — yukarıdaki üçüncü cümle sebebini söylüyor. Ortalama
tek başına da ölçüt değil; eski sıra ortalamaya bakarak çizilmişti ve bu bölümün
düzeltmesi tam olarak budur.

Yedi maddenin 1'inde ölçülmüş bir etki var, 6'sında yok.

En büyük ölçülmüş kalem eski 1. madde: p10'da +0,235, HandBrake p10 açığının
(+0,477) %49'u; ortalamada +0,181, ortalama açığının (+0,097) 1,9 katı.

Yeni sıra, eski numaralarıyla: 1, 3, 5, 2, 4, 7, 6. 3 madde yürüyen bir
sözleşmeye değiyor — eski 1. madde T133, eski 3. madde T114, eski 5. madde T134.

2 madde iki basamak oynadı: eski 5. madde 3. sıraya, 2 basamak yukarı; eski 2.
madde 4. sıraya, 2 basamak aşağı.

Ölçülmemiş altı maddenin beklentilerinden 4'i düzeltilmemiş — karşılığı ne
kilitten önce ne sonra ölçüldü: eski 2, 3, 4, 7. maddeler.

Madde madde gerekçe `harita-tazeleme.py sira` çıktısında; bayat sayının tam
listesi, düzeltilmiş karşılığı ve her karşılığın bölüm çapası
`harita-tazeleme.py bayat` çıktısında.

### Maddeler hangi sözleşmeyi bekliyor

Aşağıdaki `status` alanları `.claude/relay/contracts/` altından okundu.

| madde (yeni sıra) | sözleşme | bugünkü `status` | ne demek |
|---|---|---|---|
| bölüm girişi | T111 | `done` | **Kapı kaldırıldı** — "T111 kapandığında sıra yeniden çizilmelidir". Sıra yukarıda çizildi. |
| 1 | T98 | `done` | İş uygulandı (`8ea80c4`); madde artık öneri değil. |
| 1 | T133 | `open` | `-g` ızgarasını **şu anda ölçüyor**. Hangi `-g` değerinin en iyi olduğu hâlâ ölçülmedi. |
| 1 | T108 | `submitted` | Aynı dosyaya (`FfmpegArguments.cs`) değiyor ama konusu tepe tavanı eğrisi, `-g` değil. |
| 2 | T111 | `done` | **Kapı kaldırıldı** — "T111 kapanmadan bu maddeye yatırım yapılmamalı". Sonucu maddenin içinde. |
| 2 | T114 | `active` | Sahne başına bit dağıtımı (`SceneBudget.cs`) — bu maddenin ta kendisi, yürüyor. |
| 3 | T134 | `active` | Siyah kenarlı kaynak sınıfını ölçüyor; bu madde ilk sayısını oradan alacak. |
| 4 | T131 | `submitted` | Aynı dosyaya (`ComplexityProbe.cs`) değiyor ama konusu iptal yolu, pencere sayısı değil. |
| 5, 6, 7 | — | — | Yürüyen sözleşme yok. |

Bu belgenin kendisi de T126'nın (`submitted`) `owns` listesinde; bu bölüm
değişirse orayla çakışma ihtimali var.

### 1. Anahtar kare aralığı içerikten türesin

**Sıra: yeni 1, eski 1 — değişmedi.** Yedi maddenin ölçülmüş etkisi olan tek
maddesi: kilitli ölçümde p10 +0,235, ortalama +0,181.

- **Ne:** bu madde yazıldığında `-g` sabitti (`fps × 2`). **T98'de uygulandı**
  (`8ea80c4`): `FfmpegArguments.KeyframeArgs` bugün bir **aralık** üretiyor —
  taban 1 s, tavan sahne haritasından çıkıp 5-10 s'ye kelepçeleniyor, harita
  yoksa HandBrake'in 10 s'si. HandBrake `fps × 10` kullanıyor
  (`libhb/encx264.c:391`, `libhb/encx265.c:190`).
  (`docs/olcumler/auto-mod.md` § "K6'nın önerileri bugünkü `main`e karşı
  denetlendi" — "Madde 1 — anahtar kare aralığını uzat: T98'de uygulandı.",
  şu an `:755`)
- **Nereye:** yapıldı — `src/VidShrink.Core/FfmpegArguments.cs`, `KeyframeArgs`.
- **Ölçü:** `-g` ızgarası, aynı kaynak, boyut eşitlenmiş. **T133 şu anda
  ölçüyor** (`.claude/relay/contracts/T133.md`, `status: open`). Ölçülen tek
  nokta bugüne kadar 120 → 300; hangi `-g` değerinin en iyi olduğu taranmadı.
- **Açığın hangi kısmını kapattığı ölçüldü:** `-g 300` dosyayı **%24,5
  küçültürken** kilitli ortalamayı **+0,181**, p10'u **+0,235** artırdı
  (`docs/olcumler/auto-mod.md` § "K4 — Açığın ayar başına ayrıştırması" — tablo
  satırı "anahtar kare aralığı (-g)", şu an `:276`). İki eksende birden kazanan
  tek satır. Yukarıdaki hüküm cümlesi bu kalemi p10 açığının %49'u diye
  ölçüyor — "en büyüğü" artık bir beklenti değil, oranı yazılı bir ölçü.

- **Not — yerleşimin payı ve 6.1'in eski çıkarımı.** Bu maddenin eski notu
  ortalamaya bakarak sonuç çıkarıyordu; ortalama artık geriliğimizin olduğu yer
  değil. Not düzeltildi, **veri değişmedi.**

  Eski hâli:

  > Yani kazanç aralığın uzunluğundan geliyor iddiası **ortalama** için hâlâ
  > geçerli; "hizalamaya yatırım yapılmamalı" sonucu da ortalama içindir —
  > p10'da tam tersini gösteren güncel bir veri var, bu maddeye yatırım kararı
  > verilecekse bu nüans hesaba katılmalı.

  Yeni hâli: anahtar kareyi sahne kesmesine hizalamanın payı kilitli ölçümde
  **ortalamada -1,231, p10'da +0,135** (`docs/olcumler/auto-mod.md` § "K4 —
  Açığın ayar başına ayrıştırması" > "Yerleşimin payı ölçüldü: sıfır değil,
  negatif" — "ama p10'da **+0,135**", şu an `:344`). Eski çıkarım — "hizalamaya
  yatırım yapılmamalı" — **ortalamaya bakıyordu.** Ölçüt p10'a geçince aynı veri
  ters yöne işaret ediyor: p10'daki +0,135'in işareti **lehimize**, ve p10 açığın
  ayakta kaldığı tek eksen. Yani hizalamayı dışlayan gerekçe düştü; onun yerine
  **ölçülmemiş bir soru** kaldı.

  **Bu sözleşme maddenin kaderine karar vermiyor.** Üç sebeple: (a) +0,135 tek
  kaynakta, tek `-g` değerinde, tek koşumda ölçüldü ve başka içerikte tekrarı
  ölçülmedi; (b) aynı iki koşumun kilitli en düşük kareleri tablonun en
  düşükleri (72,574 / 71,940) ve p10'un neden ters yöne gittiği **ölçülmedi**;
  (c) `-g` ızgarasını **T133 şu anda ölçüyor**. Karar T133'ün.

  Bir uyarı daha, ölçüm sınırı: bu belgenin "hizalama" ölçtüğü şey
  `-force_key_frames`'tir. T98'in yazdığı `scd=1` kodlayıcının kendi sahne kesme
  algılamasıdır ve **ölçülmedi** (aynı belge § "K6'nın önerileri bugünkü `main`e
  karşı denetlendi" — "T98'in `scd=1`'i, T111'in ölçtüğü şey değil.", şu an
  `:770`).

### 2. `SceneMap` plana bağlansın

**Sıra: yeni 2, eski 3 — 1 basamak yukarı.** Ölçülmüş değeri yok; ama
müdahalenin şekli açığın kaldığı eksene (p10) oturuyor ve ölçümü yürüyen bir
sözleşmede.

- **Ne:** sahne haritası bugün yalnız VMAF örneklemesinde kullanılıyor.
- **Nereye:** `src/VidShrink.Core/PlanCalculator.cs`, `SearchLayout` girdisi
  olarak; `SceneMap.cs:20-46` zaten sahne başına `BitsPerSecond` üretiyor.
- **Ölçü:** sahne başına karmaşıklık dağılımının varyansı yüksek olan bir
  kaynakta tek-CRF ile sahne-farkındalıklı planın aynı boyutta p10 farkı.
- **Beklenen:** p10'da kazanç — ve **bu bir beklenti, ölçü değil.**
- **Kapı kaldırıldı.** Bu maddede "T111 kapanmadan bu maddeye yatırım
  yapılmamalı" yazıyordu. T111 kapandı; sonucu şu: maddenin eski gerekçesi
  T102'de auto'nun harmonik ortalamasının **56,313**, HandBrake'inkinin
  **95,727** olmasıydı — aradaki ~39 puan. **O 39 puanın tamamı ölçüm
  kusuruymuş:** kilitli harmonik açığı **+0,099**
  (`docs/olcumler/auto-mod.md` § "K3 — AV1 ↔ HandBrake yeniden ölçüldü" — tablo
  satırı "HandBrake açığı | T111 yeni (**kilitli**)", şu an `:672`). Aynı
  kilitle auto'nun 1 puan altındaki 26 karesi de sıfırlandı, en düşük kare
  0,000 → **92,376** (aynı belge § "Kilidin tek başına etkisi — aynı dosya, iki
  ölçüm" — tablo satırı "`auto`", şu an `:611`).
- **Yani bu madde başlığındaki sayıyı kaybetti, sırasını kaybetmedi.** Harmonik
  ayağı düştü; p10 ayağı ölçülmedi ama açığın kaldığı eksen orası.
- **Yürüyen sözleşme:** T114 (`active`) sahne başına bit dağıtımını yazıyor.

### 3. Otomatik kırpma

**Sıra: yeni 3, eski 5 — 2 basamak yukarı.** Bu kaynakta yapısal olarak sıfır;
ama ölçülen kaynakta kalan açık zaten küçük ve hiç ölçmediğimiz kaynak sınıfını
yürüyen bir sözleşme ölçüyor.

- **Ne:** siyah kenar kırpma bizde hiç yok; HandBrake varsayılan olarak yapıyor
  (`preset/preset_builtin.json` `PictureCropMode: 0`, `libhb/scan.c:1279-1313`).
- **Nereye:** yeni bir yoklama; `ComplexityProbe` ile aynı geçişte ölçülebilir.
- **Ölçü:** letterbox'lı bir kaynakta aynı hedef boyutta kırpmalı/kırpmasız p10.
- **Beklenen:** T102'nin kaynağında **sıfır** — o koşum `--crop-mode none` ile
  yapıldı, yani kırpma o karşılaştırmada iki tarafta da yoktu. Bu yapısal bir
  sıfır, kilitten etkilenmez.
- **Neden yukarı taşındı:** ölçülen kaynakta kalan açık artık ortalamada +0,097.
  Orada kazanılacak yer küçük; hiç ölçmediğimiz kaynak sınıfları ise ölçülmemiş
  olmaya devam ediyor. **T134 (`active`) tam bu sınıfı ölçüyor** — bu maddenin
  ilk gerçek sayısı oradan gelecek.

### 4. Örnekleme penceresi içeriğe göre büyüsün

**Sıra: yeni 4, eski 2 — 2 basamak aşağı.** Ölçülmedi, yürüyen sözleşmesi yok;
kendi metni puan açığına katkısını dolaylı ve küçük diyor.

- **Ne:** `MaxWindows = 3`, `WindowSeconds = 2.0` sabitleri. Bir saatlik video
  6 saniyeden planlanıyor.
- **Nereye:** `src/VidShrink.Ffmpeg/ComplexityProbe.cs`.
- **Ölçü:** pencere sayısını 3 / 6 / 12 yapıp aynı kaynakta tahmin edilen bppf
  ile teslim edilen bppf arasındaki hatayı karşılaştır; ek ölçüm süresi de
  raporlansın.
- **Beklenen:** tek başına kaliteyi artırmaz; **ilk deneme isabetini** artırır,
  yani `EncodeRunner`'ın yeniden kodlama sayısını düşürür. **Düzeltilmemiş:** bu
  beklenti ne kilitten önce ne sonra ölçüldü.
- **Neden aşağı indi — ve karşı argüman.** Eski sırada ikinciydi; oraya "dinamik
  analiz iddiasının ayağa kalkması bu maddeden geçiyor" gerekçesiyle konmuştu, o
  gerekçe bir ölçü değil. Ölçüt ölçülmüş p10 etkisine geçince, kendi metni
  katkısını "dolaylı ve muhtemelen küçük" diyen bir madde ölçümü yürüyen
  maddelerin üstünde duramaz. **Karşı argüman kayıtta:** açık ortalamada
  neredeyse kapandığı için geriye kalan asıl kayıp puan değil **yer** olabilir —
  auto teslim ettiği dosyada kendi doldurma bandının altında kalıyor (15,04 MiB
  teslim, band alt kenarı 15,20 MiB) ve bu sapmanın modellenmesi hâlâ açık.
  O açık bu yedi maddenin içinde değil; `docs/olcumler/auto-mod.md` § "K6 —
  Sıradaki adım" madde 2'de duruyor.

### 5. Kare hızı modu `pfr` olsun

**Sıra: yeni 5, eski 4 — 1 basamak aşağı.** Ölçülmedi, yürüyen sözleşmesi yok;
yalnız fps düşürülen koşumlarda görünür.

- **Ne:** HandBrake preset'leri `pfr` kullanıyor
  (`preset/preset_builtin.json:1009`); biz fps düşürdüğümüzde CFR yazıyoruz.
  `pfr`, tavanı aşmayan yerlerde kaynağın kendi zamanlamasını koruyor
  (`libhb/vfr.c:198-205`).
- **Nereye:** `src/VidShrink.Core/FfmpegArguments.cs`, fps düşürme dalı.
- **Ölçü:** fps düşürmenin devreye girdiği bir hedefte CFR ↔ PFR, boyut
  eşitlenmiş.
- **Beklenen:** küçük. Fark yalnız fps düşürülen koşumlarda görünür.
  **Düzeltilmemiş:** ölçülmedi.

### 6. Turbo ilk geçiş

**Sıra: yeni 6, eski 7 — 1 basamak yukarı.** Kalite maddesi değil, süre
maddesi; puan açığına katkısı yok ya da hafif negatif.

- **Ne:** HandBrake iki geçişli ABR'de ilk geçişi hızlandırıyor
  (`libhb/encx264.c:604-608`, preset alanı `VideoTurboMultiPass`).
- **Nereye:** `src/VidShrink.Core/FfmpegArguments.cs`, geçiş 1 argümanları.
- **Ölçü:** aynı hedefte toplam kodlama süresi ve teslim edilen puan.
- **Beklenen:** puan açığına katkısı yok ya da hafif negatif; **süre** kazancı.
  **Düzeltilmemiş:** ne puan ne süre ölçüldü — ve süre bu projede hiç
  ölçülmedi, ölçümler paylaşımlı makinede koştu.
- **Neden taramalı içeriğin üstüne çıktı:** ikisinin de puan açığına katkısı
  sıfır sayılıyor, ama bu bir mühendislik maddesi; taramalı içerik bir ürün
  kapsamı sorusu ve ölçütün dışında.

### 7. Taramalı içerik

**Sıra: yeni 7, eski 6 — 1 basamak aşağı.** Ürün kapsamı sorusu, açık sorusu
değil — ölçütün dışında kaldığı için sonda.

- **Ne:** deinterlace hiç yapmıyoruz. HandBrake'in yaptığı
  (`comb-detect` → seçici `decomb`) kare başına uyarlamadır ve bizde karşılığı
  yok.
- **Nereye:** `src/VidShrink.Core/FfmpegArguments.cs` filtre zinciri; tespiti
  probe'a.
- **Ölçü:** taramalı bir kaynakta VMAF; ama asıl ölçü **kabul edilebilirlik** —
  taraklı çıktı puandan bağımsız olarak kusurlu.
- **Beklenen:** T102 açığına katkısı **sıfır** (kaynak taramalı değil). Yapısal
  bir sıfır, kilitten etkilenmez.

---

## Kaynaktan doğrulanmadı

Bu belgede aşağıdaki sorular **cevaplanmadı**, çünkü kaynağı okumadım:

- HandBrakeCLI'nin bayraksız varsayılanları (`test/` çekilmedi). `uzman-hb2`
  koşumunda deinterlace/comb-detect'in açık mı kapalı mı olduğunu bilmiyoruz.
- macOS ve GTK arayüzlerinin hedef boyut davranışı. Yalnız `HandBrakeWPF`
  okundu; `TargetSize` enum'unun ölü olduğu tespiti **Windows arayüzü içindir.**
  `libhb`'de hedef boyut alanı olmadığı için diğer arayüzlerin de yapamayacağını
  düşünüyorum, ama **bunu doğrulamadım.**
- x265'in `scenecut` varsayılanının anahtar kare zamanlarını nasıl seçtiği.
  `libhb/encx265.c` `scenecut`'a hiç dokunmuyor (grep boş); kararın x265'te
  olduğunu buradan çıkardım, x265 kaynağını okumadım.
- CRF modunda `VideoMultiPass: true` alanının HandBrake günlüğünde ne
  gösterdiği. Kod yolunu okudum (`hb.c:1946-1949` kapıyı kapatıyor);
  **koşturarak doğrulamadım** — bu sözleşme ölçüm yapmıyor.
