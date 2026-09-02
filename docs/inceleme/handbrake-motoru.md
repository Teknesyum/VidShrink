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
`docs/olcumler/auto-mod.md:202-204` — "boyut eşlemesi elle yapıldı … HandBrake
için iki koşum". Yani T102'de HandBrake'e bizim yaptığımız işi yaptırmadık;
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
(`docs/olcumler/auto-mod.md:289`). 10 saniyelik tavan `libhb/encx265.c:188-190`'in
sonucu (60 fps × 10). Aradaki `28,35` ve `56,87` ise **tam olarak kaynağın iki
sahne kesmesi** (`docs/olcumler/auto-mod.md:283-287`, ölçülen skorlar 0,314 ve
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
| `VideoPreset` | `"veryfast"` (`:332`) | `"fast"` (`:1012`) | var — `EncodePlan.Preset`, auto'da libsvtav1 preset 6 (`docs/olcumler/auto-mod.md:209`) |
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

**Hiçbiri.** Komut (`docs/olcumler/auto-mod.md:209`):

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
| Anahtar kare aralığı | `FfmpegArguments.cs:162` — `-g = max(2, round(fps × 2))` | üretimde, **sabit formül** |
| Sahne haritası | `SceneMap.cs:20-46`, `SceneDetector.cs:123-131` | **plana bağlı değil**; yalnız `QualityMeter.cs:177,219` |

---

## 6. Açık uçlar

Her madde: ne, hangi dosyaya, hangi ölçüyle sınanır. Sözleşme açmıyorum; liste
sözleşme yazılabilecek somutlukta.

**Öncelik sırası hakkında bir uyarı.** Elimizdeki tek ölçülmüş açık T102'nin
auto 94,462 ↔ uzman-hb2 95,731 farkı (`docs/olcumler/auto-mod.md:214,216`).
**O sayı kirli:** T111 sebebini yazıyor — bizim AV1 koşumlarımızda kaymış kare
eşlemesi var, HandBrake koşumunda yok (`.claude/relay/contracts/T111.md:25-39`).
Düzeltmenin büyüklüğü henüz ölçülmedi. Aşağıdaki her "beklenir" cümlesi
**beklentidir, ölçü değildir** ve T111 kapandığında sıra yeniden çizilmelidir.

### 1. Anahtar kare aralığı içerikten türesin

- **Ne:** `-g` bugün `fps × 2` sabiti. HandBrake `fps × 10` kullanıyor
  (`libhb/encx264.c:391`, `libhb/encx265.c:190`).
- **Nereye:** `src/VidShrink.Core/FfmpegArguments.cs:162`, kararı
  `PlanCalculator`'a taşıyarak.
- **Ölçü:** `tools/auto-mod-olcumu`, aynı kaynak, `-g` ızgarası, boyut eşitlenmiş.
- **Açığın hangi kısmını kapatması beklenir:** en büyüğü. Bu zaten ölçüldü —
  `-g 300` dosyayı **%24,5 küçültürken** ortalamayı **+0,155**, p10'u **+0,333**
  artırdı (`docs/olcumler/auto-mod.md:250`). Tek yönlü kazanç. HandBrake'in bizim
  önümüzde olduğu 1,269 puanın kayda değer bir kısmının burada olması beklenir.
- **Not:** aynı belge (`:316`) anahtar kareyi sahne kesmesine hizalamanın payının
  **negatif** olduğunu ölçmüş. Yani kazanç aralığın uzunluğundan; hizalamaya
  yatırım yapılmamalı.

### 2. Örnekleme penceresi içeriğe göre büyüsün

- **Ne:** `MaxWindows = 3`, `WindowSeconds = 2.0` sabitleri. Bir saatlik video
  6 saniyeden planlanıyor.
- **Nereye:** `src/VidShrink.Ffmpeg/ComplexityProbe.cs:14,17,131-143`.
- **Ölçü:** pencere sayısını 3 / 6 / 12 yapıp aynı kaynakta tahmin edilen
  bppf ile teslim edilen bppf arasındaki hatayı karşılaştır; ek ölçüm süresi
  de raporlansın.
- **Beklenen:** tek başına kaliteyi artırmaz; **ilk deneme isabetini** artırır,
  yani `EncodeRunner`'ın yeniden kodlama sayısını düşürür. Puan açığına katkısı
  dolaylı ve muhtemelen küçük. Ama "dinamik analiz" iddiasının ayağa kalkması
  bu maddeden geçiyor.

### 3. `SceneMap` plana bağlansın

- **Ne:** sahne haritası bugün yalnız VMAF örneklemesinde kullanılıyor.
- **Nereye:** `src/VidShrink.Core/PlanCalculator.cs`, `SearchLayout` (`:622`)
  girdisi olarak; `SceneMap.cs:20-46` zaten sahne başına `BitsPerSecond` üretiyor.
- **Ölçü:** sahne başına karmaşıklık dağılımının varyansı yüksek olan bir kaynakta
  tek-CRF ile sahne-farkındalıklı planın aynı boyutta p10 farkı.
- **Beklenen:** p10 ve harmonik ortalamada kazanç, ortalamada az. T102'de
  auto'nun harmonik ortalaması **56,313**, HandBrake'in **95,727** —
  ama bu fark T111'in kaymış eşlemesinin en çok bozduğu metrik
  (26 kare 1 puan altında, en düşük kare 0,00; `docs/olcumler/auto-mod.md:214`).
  **T111 kapanmadan bu maddeye yatırım yapılmamalı.**

### 4. Kare hızı modu `pfr` olsun

- **Ne:** HandBrake preset'leri `pfr` kullanıyor
  (`preset/preset_builtin.json:1009`); biz fps düşürdüğümüzde CFR yazıyoruz.
  `pfr`, tavanı aşmayan yerlerde kaynağın kendi zamanlamasını koruyor
  (`libhb/vfr.c:198-205`).
- **Nereye:** `src/VidShrink.Core/FfmpegArguments.cs`, fps düşürme dalı.
- **Ölçü:** fps düşürmenin devreye girdiği bir hedefte CFR ↔ PFR, boyut eşitlenmiş.
- **Beklenen:** küçük. Fark yalnız fps düşürülen koşumlarda görünür.

### 5. Otomatik kırpma

- **Ne:** siyah kenar kırpma bizde hiç yok; HandBrake varsayılan olarak yapıyor
  (`preset/preset_builtin.json` `PictureCropMode: 0`, `libhb/scan.c:1279-1313`).
- **Nereye:** yeni bir yoklama; `ComplexityProbe` ile aynı geçişte ölçülebilir.
- **Ölçü:** letterbox'lı bir kaynakta aynı hedef boyutta kırpmalı/kırpmasız p10.
- **Beklenen:** T102'nin kaynağında **sıfır** — o koşum `--crop-mode none` ile
  yapıldı, yani kırpma o karşılaştırmada iki tarafta da yoktu. Letterbox'lı
  kaynaklarda büyük olabilir; bu kaynak sınıfını hiç ölçmedik.

### 6. Taramalı içerik

- **Ne:** deinterlace hiç yapmıyoruz. HandBrake'in yaptığı
  (`comb-detect` → seçici `decomb`) kare başına uyarlamadır ve bizde karşılığı yok.
- **Nereye:** `src/VidShrink.Core/FfmpegArguments.cs` filtre zinciri; tespiti
  probe'a.
- **Ölçü:** taramalı bir kaynakta VMAF; ama asıl ölçü **kabul edilebilirlik** —
  taraklı çıktı puandan bağımsız olarak kusurlu.
- **Beklenen:** T102 açığına katkısı **sıfır** (kaynak taramalı değil). Ürün
  kapsamı sorusu, açık sorusu değil. Öncelik sırasında en sonda durmasının
  sebebi bu.

### 7. Turbo ilk geçiş

- **Ne:** HandBrake iki geçişli ABR'de ilk geçişi hızlandırıyor
  (`libhb/encx264.c:604-608`, preset alanı `VideoTurboMultiPass`).
- **Nereye:** `src/VidShrink.Core/FfmpegArguments.cs`, geçiş 1 argümanları.
- **Ölçü:** aynı hedefte toplam kodlama süresi ve teslim edilen puan.
- **Beklenen:** puan açığına katkısı yok ya da hafif negatif; **süre** kazancı.
  Bu bir hız maddesi, kalite maddesi değil.

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
