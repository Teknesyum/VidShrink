# Sürücü yokluğu: derleme listesi ile gerçek yoklama

T123. Ölçüm düzeneği `tools/surucu-yoklugu`, koşum:

```
dotnet run --project tools/surucu-yoklugu/surucu-yoklugu.csproj -c Release -- hepsi 20
```

Ölçüm makinesi: Windows 11, ffmpeg 9.0-full_build-www.gyan.dev, NVIDIA sürücüsü kurulu
ve çalışıyor. Intel QSV ve AMD AMF donanımı yok.

**İki koşum var ve ikisi farklı yük altında alındı.** Bu belgedeki bütün süre sayılarına
"makine paylaşımlıydı" damgası basılıdır:

- **Koşum 1** — on dört ajan koşarken, 2026-09-02.
- **Koşum 2** — makine büyük ölçüde boşken, 2026-09-02.

Ham çıktıların ilgili satırları aşağıda alıntılandı. Günlüklerin kendisi worktree'ye
yerel `.calisma/` altında ve `.gitignore`'da — dala girmiyor, birleşmiyor, worktree
kaldırılınca gidiyor. Kalıcı kayıt bu belgedeki sayılar.

İkisi arasındaki fark bu belgenin en önemli bulgusu (§4), o yüzden tablolarda ikisi de
duruyor. Kodlayıcı seçimi ve geçti/kaldı sonuçları da yüke bağlı çıktı — onlar da
işaretlendi.

## 1. Kusur: sürücüsüz makinede "Hızlı" kipi h264_nvenc seçiyor

Sahte `IEncoderAvailability`: derleme listesinde `h264_nvenc`, `hevc_nvenc`, `av1_nvenc`
var, yoklamayı yalnız yazılım kodlayıcıları geçiyor. Yani nvenc'li ffmpeg, sürücüsüz
makine.

`tools/surucu-yoklugu geridusme` çıktısı, bugünkü kod (iki koşumda da aynı, yüke bağlı
değil):

```
bugunku (HasEncoder) | Codec=Fast, SpeedMode=Quality
  kodlayici : h264_nvenc
  notlar    : HardwareCodecCostsQuality, ResolutionReduced, TargetEnforcedTwoPass
  gerekce   : ... h264_nvenc was not calibrated on this source ...
```

Seçilen kodlayıcı `h264_nvenc`. `EncoderFallback` notu **yok**: kullanıcıya hiçbir şey
söylenmiyor, plan çalışmayacak bir kodlayıcının üstüne kuruluyor ve kodlama çalışma
anında düşüyor.

Kardeş yol aynı makinede doğru cevabı veriyor:

```
bugunku (HasEncoder) | Codec=Auto, SpeedMode=Fast
  kodlayici : libx264
  notlar    : EncoderFallback, CodecUpgradeRecommended, ...
```

Fark tek satırda: `PickFastCodec` `WorksAsEncoder` soruyor, `PickCodec` `HasEncoder`.

Kusur ölçüde de sabitlendi: `EncoderAvailabilityTests.
KusurKaydiPickCodecYoklamayaDegilDerlemeListesineBakiyor` bugünkü yanlış çıktıyı
doğruluyor ve `h264_nvenc`'in **hiç yoklanmadığını** sayıyor. `PickCodec` düzeltilince
bu ölçü kırmızıya döner; düzeltmeyi yazan tur onu çevirecek.

## 2. Yoklamanın maliyeti

`WorksAsEncoder` süreç doğuruyor, `HasEncoder` doğurmuyor.

Plan hesabı, tek bir tercih edilen kodlayıcı için (`Codec=Fast, SpeedMode=Quality`):

| yol | soğuk ms (k1, yüklü) | soğuk ms (k2, boş) | sıcak ort. ms, 20 tekrar (k1 / k2) |
|---|---:|---:|---:|
| `HasEncoder` (bugünkü) | 16,01 | 17,17 | 0,1627 / 0,1637 |
| `WorksAsEncoder` (önerilen) | 2951,44 | 237,38 | 0,1804 / 0,1953 |
| `PickFastCodec`, 7 aday (bugün zaten yoklamalı) | 2938,09 | 253,22 | 1,8057 / 0,3295 |

`PerformanceProbe` aday seçimi, yedi adaylık liste:

| yol | soğuk ms (k1, yüklü) | soğuk ms (k2, boş) | sıcak ort. ms (k2) |
|---|---:|---:|---:|
| listedeki ilk (bugünkü) | 0,23 | 0,29 | — |
| çalışan ilk (önerilen) | 90 517,18 | 250,75 | 0,0009 |

Koşum 1'de yedi adayın hepsi düştü, o yüzden hepsi yoklandı ve her biri 4 saniyelik süre
aşımına takıldı. Koşum 2'de ilk aday geçtiği için yürüyüş orada durdu.

**90 517 sayısı tek gözlem ve §4'ün tablosuyla çaprazlanmıyor.** §4'te aynı yedi adayın
tek tek ölçülen süreleri toplandığında 28 163 ms çıkıyor; 4000 ms'lik öldürme sınırıyla
beklenen üst sınır da bu mertebede (7 × 4000). 90 517 / 7 = 12,9 s, yani sınırın üç katı.
Fark bekleme penceresinin dışında geçen zamandan geliyor olabilir — §6'da 30 000 ms
sınırıyla koşan tek bir yoklama 37 449 ms sürdü, yani 7 449 ms `WaitForExit` dışında
(süreç başlatma + `Kill(true)` ağaç öldürmesi) harcandı. Bu, farkın yönünü açıklıyor ama
büyüklüğünü ölçmedim. **Aşağıdaki hüküm 90 517'ye değil, iki tablonun ortak söylediğine
dayanıyor.**

**Karar: senkron plan hesabında kabul edilemez.**

- Sıcak önbellekte maliyet yok: 0,16 → 0,18 ms, ölçüm gürültüsü içinde.
- Soğuk önbellekte, **tek kodlayıcılık plan hesabında** (üstteki ilk tablo) boş makinede
  237 ms, yüklü makinede 2951 ms — aynı iş için 12 kat fark.
- **Yedi adaylık seçim yürüyüşünde** (üstteki ikinci tablo) boş makinede 251 ms; yüklü
  makinede §4'ün ölçülen toplamı 28 163 ms — 112 kat. Kabul edilemez olan ortalama değil
  **yayılım**: kullanıcının hedef boyutu değiştirdiği anda arayüzün ne kadar donacağı
  öngörülemez.
- 28 163 ms, `PerformanceProbe.BudgetMs`'i (20 000 ms) tek başına aşıyor. Bu hüküm için
  90 517'ye gerek yok; öldürme sınırının kendi üst sınırı olan 7 × 4000 = 28 000 ms bile
  bütçenin üstünde.

Üçüncü yol — bu turda uygulanmadı, T0'a öneri: `WarmEncoderOption` deseni.
`EncoderCapabilities` seçenek yoklamasında zaten bunu kullanıyor: saf okuma
(`SupportsEncoderOption`) süreç doğurmuyor, ısıtma (`WarmEncoderOption`) arka planda
koşuyor. Aynısı kodlayıcı yoklaması için kurulursa `PickCodec` saf kalır ve soğuk maliyet
açılışta, kullanıcı beklemeden ödenir. `EncoderCapabilities.cs` T94'ün; bu tur oraya
yazmadı.

## 3. Önbellek: başarısız yoklama da saklanıyor

`EncoderCapabilities.Probe` sonucu `_probed` sözlüğüne koşulsuz yazıyor — geçen de düşen
de. Aynı örnek üzerinde arka arkaya çağrılar:

koşum 2 (boş makine):

```
dusen kodlayici: h264_qsv
ayni ornek uzerinde  1. cagri 97,33 ms   2. cagri 0,13 ms   3. cagri 0,05 ms
yeni ornek           1. cagri 85,65 ms
gecen kodlayici: h264_nvenc   1. cagri 236,10 ms   2. cagri 0,13 ms
```

koşum 1 (yüklü makine), aynı desen:

```
dusen kodlayici: h264_nvenc
ayni ornek uzerinde  1. cagri 4113,06 ms   2. cagri 0,11 ms   3. cagri 0,02 ms
yeni ornek           1. cagri 11305,80 ms
```

İlk çağrı süreç doğuruyor, sonrakiler sözlükten okuyor — düşen de geçen de. Yeni bir
örnek yeniden yokluyor.

**Sürücü sonradan yüklenirse ne oluyor:** hiçbir şey. `EncoderCapabilities.Instance` bir
`Lazy<>` tekil ve `_probed` sözlüğünü boşaltan ya da geçersiz kılan hiçbir yol yok — ne
`Clear`, ne `Remove`, ne süre aşımı. Kullanıcı sürücüyü kurup VidShrink'i açık bırakırsa
uygulama süreç ömrü boyunca "donanım yok" demeye devam eder. Çözüm uygulamayı kapatıp
açmak; kullanıcıya bu söylenmiyor.

Davranış bu turda değiştirilmedi: `EncoderCapabilities.cs` T94'ün.

## 4. Yoklamanın kendisi makine yüküne duyarlı — düzeltmenin ön koşulu

Bu, ölçümün en önemli bulgusu ve §2'deki "üçüncü yol ara" kararının asıl sebebi.

`EncoderCapabilities.RunProbe` yokladığı süreci **4000 ms** sonra öldürüp `false`
dönüyor. Aynı makinede, aynı kodlayıcılar, iki koşum:

| kodlayıcı | koşum 1 (14 ajan) | koşum 2 (boş) |
|---|---|---|
| `h264_nvenc` | **False**, 4539 ms | True, 308 ms |
| `hevc_nvenc` | **False**, 7860 ms | True, 235 ms |
| `av1_nvenc`  | True, 2669 ms | True, 256 ms |
| `h264_qsv`   | False, 5166 ms | False, 86 ms |
| `h264_amf`   | False, 4049 ms | False, 57 ms |
| `hevc_qsv`   | False, 1919 ms | False, 96 ms |
| `hevc_amf`   | False, 1961 ms | False, 48 ms |

Bu makinede NVIDIA sürücüsü kurulu ve çalışıyor. Kabuktan doğrudan:

```
$ ffmpeg -hide_banner -loglevel error -f lavfi \
    -i "testsrc2=size=256x256:rate=30:duration=0.1" \
    -c:v h264_nvenc -frames:v 1 -f null NUL
$ echo $?
0
```

Koşum 1'deki `h264_nvenc False` satırı "sürücü yok" demiyor, "4 saniyelik sınıra takıldı"
diyor. Koşum 2 aynı kodlayıcıyı 308 ms'de geçirdi.

QSV ve AMF satırları farklı: iki koşumda da düştüler ve boş makinede 48–96 ms'de, yani
süre aşımına takılmadan düştüler. Bunlar gerçek yoklama başarısızlığı — bu makinede o
donanım yok.

Sonuç: `WorksAsEncoder` bugün her zaman bir **yetenek** sınıflandırıcısı değil; makine
meşgulken **yük** sınıflandırıcısına dönüşüyor. `PickCodec` bugünkü haliyle ona
bağlanırsa çalışan bir NVENC makinesi meşgulken yazılıma düşer ve bu yanlış cevap süreç
ömrü boyunca önbellekte kalır (§3). Deterministik yanlış cevap, yüke bağlı yanlış cevapla
değiştirilmiş olur.

Bu yüzden `PickCodec` düzeltmesi tek başına yeterli değil. Ön koşullar:

1. Yoklama süre aşımı 4000 ms'den yukarı, ya da süre aşımı "çalışmıyor" değil
   "ölçülemedi" olarak okunmalı.
2. Süre aşımı sebebiyle düşen yoklama süreç ömrü boyunca saklanmamalı.

İkisi de `EncoderCapabilities.cs` içinde, T94'ün. Bu tur oraya yazmadı.

Düzeneğin `yuk` bölümü (taze önbellekle beş kez aynı yoklama) boş makinede koşabildi ve
`h264_nvenc` 5/5, `av1_nvenc` 5/5 geçti, 222–255 ms. Yani **kararsızlık boş makinede
gözlenmedi**; yukarıdaki iki koşumun farkı yükten geliyor.

## 5. `PerformanceProbe` aday seçimi — bu turda düzeltildi

Eski hali (`PerformanceProbe.cs:81`):

```csharp
var hardwareCodec = HardwareCandidates.FirstOrDefault(availability.HasEncoder);
```

İki yerde yanlış cevap veriyor:

- Sürücüsüz makinede listedeki ilk adayı ölçüp düşmesini bekliyor.
- Bir adayı düşen ama başkası çalışan makinede yanlış adayı ölçüyor. Bu makine koşum 1'de
  tam olarak bu durumdaydı: `h264_nvenc` yoklamayı geçmedi, `av1_nvenc` geçti, eski kod
  yine de `h264_nvenc`'i seçti. Koşum 2'de aynı makinede `h264_nvenc` geçtiği için bu
  ayrım görünmüyor — yani belirti yüke bağlı, kusur değil.

Yeni hali `SelectHardwareCodec`: derleme listesindeki adaylar sırayla yoklanır, **çalışan
ilk aday** seçilir. Hiçbiri çalışmıyorsa listedeki ilk adaya dönülür — `null` değil.
Sebebi `PerformanceCheck.Evaluate`'in ayrımı: kodlayıcı listede varken kodlamanın düşmesi
(`HardwareEncoderFailed`) ile hiç olmaması (`NoHardwareEncoder`) farklı iki cevap. `null`
dönmek sürücüsüz makinede ikinciyi söyler ve birinciyi ulaşılmaz yapardı.

§2'deki 90 saniye bu yolu da vurduğu için seçim `RunAsync`'in bütçesine bağlandı: adaylar
arasında kalan bütçeye bakılır, bütçe bittiyse kalan adaylar yoklanmaz ve listedeki ilk
adaya dönülür. `Stopwatch` artık seçimden **önce** başlatılıyor, yani yoklamanın süresi
`BudgetMs`'e yazılıyor; eskiden bütçenin dışındaydı. `BudgetMs`'in belge cümlesi de
buna göre güncellendi.

## 6. Geri düşme zinciri: kullanıcı ne görüyor

`FallbackCodecFor(Fast)` = `libx264`. Sürücüsüz makinede geri düşme gerçekleştiğinde
(sahte `IEncoderAvailability`, iki koşumda da aynı):

```
onerilen (WorksAsEncoder) | Codec=Fast, SpeedMode=Quality
  kodlayici : libx264
  notlar    : EncoderFallback, ResolutionReduced, TargetEnforcedTwoPass
  gerekce   : the h264_nvenc encoder is not available on this ffmpeg build,
              so encoding falls back to libx264; ...
```

Sessizce yavaşlamıyor: `AdviceCode.EncoderFallback` notu ve gerekçe cümlesi var. Yani
kullanıcı geri düşmeden haberdar ediliyor. Geri düşmenin **kaliteye** etkisi bu turda
ölçülmedi; `geridusme` komutu kodlayıcı, not ve gerekçe basıyor, kalite puanı basmıyor.

**Ama gerekçe cümlesi yanlış olacak.** "not available on this ffmpeg build" — sürücüsüz
makinede kodlayıcı ffmpeg derlemesinde **var**, eksik olan sürücü. Bugün cümle doğru,
çünkü geri düşme yalnız derleme listesi eksikken oluyor. `PickCodec` gerçek yoklamaya
bağlandığı an cümle yalan söylemeye başlar. Düzeltmeyi yazan tur bu metni de
değiştirmeli.

Metin `PlanCalculator.cs` içinde, T107'nin; bu tur oraya yazmadı.

## Ölçü ve mutasyon

`tests/VidShrink.Tests/EncoderAvailabilityTests.cs`, 9 ölçü, hepsi yeşil. Mutasyon iki
yönde de kırmızı:

| mutasyon | düşen ölçü |
|---|---|
| `PerformanceProbe.SelectHardwareCodec`: `WorksAsEncoder` → `HasEncoder` | 3/9: `OlcumCalisanAdayiSeciyorListedekiIlkiniDegil`, `OlcumSirayiCalisanIlkAdayaKadarYuruyor`, `CalisanAdayBulununcaSonrakiAdaylarYoklanmiyor` |
| `PlanCalculator.PickFastCodec`: `WorksAsEncoder` → `HasEncoder` | 1/9: `SurucusuzMakinedeHizliKipYazilimaDusuyor` |

İkinci mutasyon `PlanCalculator.cs` üzerinde ölçüldü ve **geri alındı**;
`git diff HEAD -- src/VidShrink.Core/PlanCalculator.cs` boş.

## Ölçülmeyenler

- Gerçekten sürücüsüz bir makinede uçtan uca davranış. Bu makinede NVIDIA sürücüsü
  kurulu; sürücüsüzlük sahte `IEncoderAvailability` ile üretildi. CI'da (nvenc'li ffmpeg,
  sürücü yok) doğrudan koşum yapılmadı.
- `PickCodec` düzeltmesinin `PlanCalculator` içindeki uçtan uca etkisi. Düzeltme
  uygulanmadı, `ProbingAdapter` ile dışarıdan taklit edildi.
- Süre aşımını 4000 ms'den yukarı çekmenin ne kadar yeteceği. Koşum 1'de 7860 ms'lik bir
  yoklama görüldü ama üst sınır aranmadı.
- Arka planda ısıtmanın (`WarmEncoderOption` deseni) açılış süresine maliyeti.
- QSV ve AMF'in bu makinede *neden* düştüğü ayrı ayrı doğrulanmadı; kabuktan
  `Error creating a MFX session` ve `AMFQueryVersion failed` görüldü, o kadar.

## 6. Süre aşımının yüke göre dağılımı

Komut: `dotnet run --project tools/surucu-yoklugu -c Release -- dagilim 12`.
Öldürme sınırı bu ölçümde **30 000 ms**'ye açıldı, yani sayılar `RunProbe`'un 4000 ms'de
kırptığı değil **gerçek** süreler. `ek_yuk`, makinede zaten koşan on dört ajanın
*üstüne* başlatılan `libx264 -preset veryslow` süreç sayısı. 16 çekirdek, 12 tekrar.

`islemci%` bir yaklaşıklıktır (pencere içinde başlayıp biten süreçler yüzünden %100'ü
aşabiliyor); karar için `ek_yuk` sütununu kullanın.

`h264_nvenc` — bu makinede gerçekten çalışıyor:

| ek_yuk | islemci% | min | ortanca | p90 | max | >4000 | >8000 | >15000 | çıkış 0 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 56,7 | 248 | 279 | 327 | 334 | 0/12 | 0/12 | 0/12 | 12/12 |
| 4 | 100,0 | 4706 | 6493 | 9816 | 9994 | **12/12** | 4/12 | 0/12 | 12/12 |
| 8 | 119,5 | 11265 | 15854 | 21548 | 29857 | 12/12 | 12/12 | 9/12 | 12/12 |
| 16 | 100,3 | 21918 | 26058 | 34401 | 37449 | 12/12 | 12/12 | 12/12 | 8/12 |

`h264_amf` — bu makinede gerçekten yok (`AMFQueryVersion failed`):

| ek_yuk | islemci% | min | ortanca | p90 | max | >4000 | >8000 | >15000 | çıkış 0 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 11,6 | 39 | 46 | 53 | 58 | 0/12 | 0/12 | 0/12 | 0/12 |
| 4 | 109,5 | 3774 | 5396 | 6884 | 7025 | 11/12 | 0/12 | 0/12 | 0/12 |
| 8 | 139,2 | 10127 | 11056 | 12090 | 12115 | 12/12 | 12/12 | 0/12 | 0/12 |
| 16 | 140,9 | 15188 | 17336 | 20073 | 20458 | 12/12 | 12/12 | 12/12 | 0/12 |

### 4000 ms sınırı hangi yükte aşılıyor

**İlk yük basamağında.** Boş makinede `h264_nvenc` en kötü 334 ms; dört ek süreç
eklendiğinde **en iyi** örnek 4706 ms. Yani 12/12 örnek sınırın üstüne çıkıyor. Aradaki
bir yük seviyesini ölçmedim; sınırın kırıldığı eşik 0 ile 4 ek süreç arasında bir yerde,
daha keskin yerini bilmiyorum.

Çıkış kodu sütunu bu tablonun bel kemiği: `ek_yuk` 4 ve 8'de 12/12 yoklama **çıkış 0**
veriyor, yani kodlayıcı her seferinde gerçekten çalıştı. 4000 ms sınırı bu 24 başarılı
yoklamanın hepsini "çalışmıyor" diye raporlardı.

### Sınırı yükseltmek çözüyor mu, öteliyor mu

**Öteliyor.** Her sınır tam bir yük basamağı satın alıyor:

| sınır | ek_yuk 4 | ek_yuk 8 | ek_yuk 16 |
|---|---:|---:|---:|
| 4 000 ms | 12/12 yanlış | 12/12 yanlış | 12/12 yanlış |
| 8 000 ms | 4/12 yanlış | 12/12 yanlış | 12/12 yanlış |
| 15 000 ms | 0/12 yanlış | 9/12 yanlış | 12/12 yanlış |
| 30 000 ms | 0/12 yanlış | 0/12 yanlış | 4/12 yanlış |

`ek_yuk` 16'da dört yoklama 30 000 ms'yi de aştı. Yeterli yükte her sabit kırılıyor;
ölçüm hiçbir sonlu sabitin doğru cevabı garantilediğini göstermiyor.

### Yavaşlık ile yokluk ayırt edilebilir mi

Tek yönlü. Boş makinede gerçek yokluk **hızlı**: `h264_amf` 39–58 ms, çalışan
`h264_nvenc`'in en hızlı örneğinden (248 ms) 4–6 kat hızlı — bu makinede 58 ile 248 ms
arasında ayırıcı bir pencere var. Ama yük altında `h264_amf` de yavaşlıyor (ek_yuk 16'da
15 188–20 458 ms). Yani:

- **hızlı düşen yoklama** gerçek yokluğun kanıtı sayılabilir,
- **yavaş yoklama** yetenek hakkında hiçbir şey söylemiyor — ne var ne yok.

58/248 ms penceresi tek makinede, iki kodlayıcıda ölçüldü. Taşınabilir bir sabit
önermeye yetmez; başka makine ve kodlayıcılarda ölçülmedi.

## 7. Üç yol — kazandığı ve kırdığı

Karar vermedim; üçünü ayrı ayrı ölçüp yazdım.

### (a) Sınırı yükselt

Kazandığı: tek satırlık değişiklik, mevcut yapıyı bozmuyor.

Kırdığı: §6'nın ikinci tablosu — çözmüyor, bir yük basamağı öteliyor. Üstelik bedeli
simetrik değil: sınır ne kadar yüksekse, **gerçekten yok** olan bir kodlayıcı için o
kadar uzun bekleniyor. Yedi adaylık yürüyüşte 15 000 ms sınırı, hepsi yoksa 105 s eder.
Tek başına yeterli değil.

### (b) Süre aşımını "ölçülemedi" say, önbelleğe yazma

Kazandığı: §6'nın gösterdiği asıl kusuru hedefliyor — yanlış cevabın *kalıcı* olmasını
engelliyor. Yük geçince bir sonraki çağrı yeniden yokluyor. Hızlı düşüşler yine
önbelleğe girdiği için gerçek yokluk hâlâ bir kez ödeniyor.

Kırdığı: `WorksAsEncoder`'ın `bool` imzası üç durumu taşıyamıyor; `IEncoderAvailability`
sözleşmesi genişlemek zorunda. Ayrıca önbelleğe yazılmayan durum, yük sürerken her
çağrıda yeniden süreç doğuruyor — sıcak okumanın bedavalığı o pencerede kayboluyor.

**"Ölçülemedi" durumunda `PickCodec` ne yapmalı: yazılıma düşmeli.** Gerekçe iki ölçü:

Yanlışlıkla donanıma güvenmenin bedeli **sınırsız**. `EncodeRunner.RunCommandAsync`
(`src/VidShrink.Ffmpeg/EncodeRunner.cs:307-308`) ffmpeg sıfırdan farklı dönerse
`InvalidOperationException` atıyor; kodlayıcı düzeyinde koşum içi geri düşme **yok**.
Yani seçim yanlışsa dönüşüm, kullanıcı bekledikten sonra tümden başarısız oluyor.

Yanlışlıkla yazılıma düşmenin bedeli **ölçüldü ve sınırlı**:

| örnek | h264_nvenc | libx264 veryfast | oran |
|---|---:|---:|---|
| 1080p60, 6 s | 960 ms | 806 ms | yazılım daha hızlı |
| 1080p60, 90 s | 10 347 ms | 12 732 ms | 1,23 kat |

Komut: `dotnet run --project tools/surucu-yoklugu -c Release -- kodlama 90`; üç koşumun
en iyisi, `-f null` çıkış, **makine paylaşımlıydı** (yük denetlenmedi). Kısa klipte fark
başlatma gürültüsünde kayboluyor. 90 s'de donanım %23 önde.

Kaynak `testsrc2` sentetiği; x264 için alışılmadık derecede kolay bir girdi, gerçek
içerikte oran büyük olasılıkla daha yüksek. **Gerçek içerikte ölçülmedi.** Kalite farkı
da ölçülmedi. Yine de karşılaştırmanın yönü net: sınırlı bir yavaşlama ile tümden
başarısız dönüşüm arasında seçim var.

### (c) `WarmEncoderOption` deseniyle arka planda ısıt

Kazandığı: düşünülenden ucuz — **ısıtma yolu zaten var.**
`MainWindow.axaml.cs:404` açılışta `ProbeHardwareEncodersAsync`'i bekliyor, o da
`:1214`'te `Task.Run` içinde `EncoderCapabilities.Instance` + `WarmPsychovisualProbe` +
`PlanCalculator.Build` + `capabilities.Probe(plan.Codec)` koşturuyor. Yani arka plan
ısıtması kurulu; eksik olan, aday listesinin tamamının değil yalnız `plan.Codec`'in
ısıtılması.

Kırdığı — ilk ikisi ölçümle değil kodla görülüyor:

1. **Saf okuma yolu yok.** `EncoderCapabilities.Probe` (`:79`) `lock (_probed)` kilidini
   ffmpeg süreci boyunca **tutuyor**. `SupportsEncoderOption` (`:37-41`) kilidi yalnız
   sözlük okuması kadar tutuyor; `Probe` öyle değil. Bu yüzden ısıtma tek başına
   `PickCodec`'i saf yapmıyor: ısıtma sürerken gelen okuma kilidin arkasında,
   yoklamanın tamamı kadar bekliyor. (c) uygulanacaksa `SupportsEncoderOption`'ın
   karşılığı olan, süreç doğurmayan bir okuma yöntemi de gerekiyor.
2. **Isıtma en kötü anda koşuyor.** Açılış, makinenin en yüklü olduğu an. §6'ya göre
   `ek_yuk` 4'te tek yoklamanın ortancası 6493 ms; yedi aday sırayla ısıtılırsa kabaca
   45 s. Arka planda olduğu için arayüzü kilitlemiyor ama ısıtma bitene kadar gelen her
   okuma (1) yüzünden bekliyor.
3. `HdrResolver.cs:57` ve `EncoderCapabilities.Hdr10PixelFormat` ayrı bir önbellek
   kullanıyor; ısıtma kurulursa o yolun da ısıtılması gerekiyor, yoksa aynı kusur orada
   kalır.

## 8. `_probed` temizleme yolu — sürücü sonradan kurulursa

`_probed` sözlüğünü temizleyen hiçbir yol yok; `Instance` da `Lazy<>` (`:8`), yani
süreç ömrü boyunca tek örnek. Kullanıcı sürücüyü kurup uygulamaya dönerse cevap
değişmiyor.

Bedelini §6 fiyatlıyor. Yeniden yoklamanın maliyeti boş makinede kodlayıcı başına
248–334 ms, yedi aday için 1,7–2,3 s. Yüklü makinede aynı iş basamak basamak 4706 →
11 265 → 21 918 ms'ye çıkıyor.

Bu sayılara göre:

- **"Kapat ve yeniden aç" tek başına kabul edilebilir değil,** çünkü sorun yalnız
  sürücünün sonradan kurulması değil. §6, geçici yükün de aynı kalıcı yanlışı
  ürettiğini gösteriyor: kullanıcı hiçbir şey kurmasa bile, uygulama yoğun bir anda
  açıldıysa süreç ömrü boyunca "donanım yok" diyor. Kapatıp açmak bunu ancak şansa
  bağlı olarak düzeltir.
- **Zamanlı yeniden yoklama da gerekmiyor.** Süre aşımı önbelleğe yazılmazsa (yol b),
  yük geçtiğinde bir sonraki çağrı zaten yeniden yokluyor; ayrı bir zamanlayıcıya gerek
  kalmıyor. Sürücünün sonradan kurulması hâlâ açıkta kalıyor, ama o **hızlı** düşen bir
  yoklama (§6: 39–58 ms) ve o an gerçekten yok olduğu için o anda verilen cevap doğru.
- Geriye tek gerçek açık kalıyor: sürücü kurulduktan sonra, aynı süreç ömründe hızlı
  düşüşün önbellekten silinmesi. Bunun için elle bir "yeniden tara" düğmesi yeter;
  maliyeti yukarıdaki 1,7–2,3 s. **Ölçmedim:** kullanıcıların uygulamayı ne kadar açık
  tuttuğunu, yani bu senaryonun ne sıklıkta yaşandığını bilmiyorum.

## 9. K6 — gerekçe cümlesinin doğrusu

Cümlenin **iki** kopyası var, ikisi de değişmeli:

- `src/VidShrink.Core/PlanCalculator.cs:144` — ham İngilizce metin (T107'nin).
- `src/VidShrink.App/Locales/en/main.json:283` ve `tr/main.json:283`
  (`main.reason.encoder-fallback`), ayrıca her ikisinde `:312`
  (`main.advice.encoder-fallback`). Bu dosyalar owns'umda değil.

Bugünkü metin geri düşmenin sebebini **derleme listesi eksikliği** olarak söylüyor.
`PickCodec` gerçek yoklamaya bağlandığı an bu yanlış olacak: sürücüsüz makinede kodlayıcı
derlemede vardır, eksik olan sürücüdür. Önerilen metin sebebi adlandırmadan doğru kalıyor:

- en, `main.reason.encoder-fallback`:
  `"the {0} encoder could not be used on this machine, so encoding falls back to {1}"`
- tr, `main.reason.encoder-fallback`:
  `"{0} kodlayıcısı bu makinede kullanılamadı, bu yüzden {1}'e düşüldü"`
- en, `main.advice.encoder-fallback`:
  `"The preferred encoder could not be used on this machine; falling back to a software encoder."`
- tr, `main.advice.encoder-fallback`:
  `"Tercih edilen kodlayıcı bu makinede kullanılamadı; yazılım karşılığına düşüldü."`

Yol (b) uygulanırsa "ölçülemedi" için ayrı bir gerekçe gerekiyor; onu ölçmedim ve metnini
önermiyorum, çünkü hangi durumda gösterileceği `PickCodec` düzeltmesinin şekline bağlı.
