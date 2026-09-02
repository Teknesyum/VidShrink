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
| çalışan ilk (önerilen) | **90 517,18** | 250,75 | 0,0009 |

Koşum 1'de yedi adayın hepsi düştü, o yüzden hepsi yoklandı ve her biri 4 saniyelik süre
aşımına takıldı. Koşum 2'de ilk aday geçtiği için yürüyüş orada durdu.

**Karar: senkron plan hesabında kabul edilemez.**

- Sıcak önbellekte maliyet yok: 0,16 → 0,18 ms, ölçüm gürültüsü içinde.
- Soğuk önbellekte boş makinede 237 ms — kabul edilebilir sayılabilirdi.
- Ama yüklü makinede aynı iş 2951 ms, kötü durumda 90 517 ms. Kabul edilemez olan ortalama
  değil **yayılım**: aynı iş, aynı makinede 360 kat fark. Kullanıcının hedef boyutu
  değiştirdiği anda arayüzün ne kadar donacağı öngörülemez.
- 90 s aynı zamanda `PerformanceProbe.BudgetMs`'i (20 000 ms) tek başına aşıyor.

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

Sessizce yavaşlamıyor: `AdviceCode.EncoderFallback` notu ve gerekçe cümlesi var. Tahmin
edilen kalite de düşmüyor — aynı kaynakta donanım yolu 74,3/100, yazılım yolu 76,5/100.
Yani kullanıcı için doğru olan yapılıyor ve söyleniyor.

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
