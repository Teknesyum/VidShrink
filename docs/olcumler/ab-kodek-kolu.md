# A/B'nin bizim tarafımız hangi kodeği koşuyor

A/B tablosunun VidShrink sütunu, ürünün varsayılan `auto` modunu değil
**uyumluluk kolunu** koşturuyordu. Bu belge o yapılandırma hatasını, düzeltmesini
ve düzeltmenin tabloya ne yaptığını yazar.

Ölçüm düzeneğinin kendisi ve tablolar `ab-duzenegi.md`de.

## Yapılandırma hatası — üç yerden okunuyor

`tools/VidShrink.Ab/Competitors.cs` `PlanOptions` kuruyor ve `Codec` alanını
hiç atamıyordu. Alanın varsayılanı `src/VidShrink.Core/PlanCalculator.cs:11`de
`CodecPreference.Compatible`. Uygulamanın kendi varsayılanı ise `Auto`:
`src/VidShrink.App/MainWindow.axaml:398` `CmbCodec` `SelectedIndex="0"`, ve
`MainWindow.axaml.cs:1597-1602` `CodecFromIndex(0)` → `CodecPreference.Auto`.

Koşum günlüğü bunu doğruluyor —
`.calisma/ab/gunluk/parca-1_vidshrink_3.497mb.log:1` içinde `-c:v libx264`.

## `Auto` ne zaman gerçekten kodek değiştiriyor

`CodecPreference.Auto` tek başına bir kodek değil, bir **yönlendirme**:
`PlanCalculator.cs:111-113` isteği `CompressionStrategy.AutoPreference(regime)`e
devrediyor. Rejim yalnız **oran**dan, yani `kaynak MB / hedef MB`'den geliyor
(`CompressionStrategy.cs:45-52`, oran dosya boyutundan hesaplanıyor, video
baskısından değil).

| oran (kaynak/hedef) | rejim | `AutoPreference` | seçilen kodek |
|---|---|---|---|
| < 1,5 | `Light` | `Compatible` | libx264 |
| 1,5 – 6,0 | `Balanced` | `Compatible` | libx264 |
| 6,0 – 30,0 | `Aggressive` | `MaxCompression` | libsvtav1 (yoksa libx265) |
| ≥ 30,0 | `Extreme` | `MaxCompression` | libsvtav1 (yoksa libx265) |

Kodek seçimi `PlanCalculator.cs:762-782`: `MaxCompression`in tercihi
`libsvtav1`, ffmpeg'de yoksa `libx265`. Bu makinenin ffmpeg'inde
`libsvtav1` **var** (`ffmpeg -encoders`), yani `MaxCompression` bu ölçümlerde
libsvtav1 demek.

**Yani eşik oran 6,0.** Oran 6,0'ın altındaki her satırda `Auto` ile
`Compatible` aynı şeydir; kolun düzeltilmesi o satırlarda hiçbir şeyi
değiştirmez.

### Bu ölçümün satırları eşiğin neresinde

Kaynak boyutları normalize edilmiş (yalnız video) parçaların baytı.

| girdi | kaynak MB | hedef MB | oran | rejim | `Auto` ne seçer |
|---|---|---|---|---|---|
| parca-1 | 88,289 | 3,4975 | 25,24 | `Aggressive` | libsvtav1 |
| parca-2 | 109,159 | 3,4975 | 31,21 | `Extreme` | libsvtav1 |
| parca-3 | 93,904 | 3,4984 | 26,84 | `Aggressive` | libsvtav1 |
| parca-1 | 88,289 | 34,9745 | 2,52 | `Balanced` | libx264 |
| parca-2 | 109,159 | 34,9861 | 3,12 | `Balanced` | libx264 |
| parca-3 | 93,904 | 34,9936 | 2,68 | `Balanced` | libx264 |

Kodek yalnız **60 MB hedefinin üç satırında** değişiyor. 600 MB hedefinin üç
satırında `Auto` da `Compatible` veriyor; o satırlar düzeltmeden önce ve sonra
aynı yapılandırmayı koşuyor.

## Kodeği değişmeyen satırlarda açık nereden geliyor

600 MB'ın üç satırında biz libx264, HandBrake x265-slow-multipass koşuyor. Bu
açık **ölçüm hatası değil, ürünün kendi kuralı**: oran 6,0'ın altında kaldığı
için `AutoPreference` uyumluluğu seçiyor ve H.264'te kalıyor.

Kuralın gerekçesi kodda yazılı değil; ürünün kullanıcıya söylediği cümlede
duruyor (`src/VidShrink.App/Locales/tr/main.json:293`,
`AdviceCode.CodecUpgradeRecommended`):

> Bu sıkışıklıkta H.265 aynı boyutta gözle görülür şekilde daha iyi sonuç
> verir; sıkıştırma algoritmasını otomatik veya H.265 yapmayı düşün.

Yani gerekçe "yalnız **sıkışıklıkta** kodek yükselt": düşük oranda uyumluluk
(H.264'ün her yerde açılması) modern kodeğin kazancından daha değerli sayılıyor.
Eşik 6,0'ın **nereden geldiği** kodda da belgede de yazılı değil — `ölçülmedi`.

`CompressionStrategy.cs` bu sözleşmenin `owns`unda değil; kural ölçüldü ve
yazıldı, değiştirilmedi.

İki not, ikisi de bu sözleşmenin dışında, ikisi de dokunulmadı:

- **Eşik sert, histerezis yok.** Oran 5,999 → 6,001 geçişinde kodek ve kare
  hızı izni **aynı anda** değişiyor. Aynı bulgu `docs/inceleme/model-strateji.md:38-42`de
  zaten yazılı (T103, "Rejim eşikleri sınırda sert").
- **Tavsiye metni ile kod ayrışıyor.** Metin "H.265" diyor; `MaxCompression`in
  tercihi `libsvtav1`, yani AV1. libx265 yalnız yedek.

## Düzeltme ve mutasyon — kodek gerçekten değişiyor mu

Düzeltme tek satır: `tools/VidShrink.Ab/Competitors.cs`de `PlanOptions`a
`Codec = CodecPreference.Auto` eklendi. **Başka hiçbir alan değişmedi** —
`TargetMb`, `FillPolicy`, `SpeedMode`, `AllowResolutionDrop`, `AllowFpsDrop`,
`HdrPolicy` olduğu gibi duruyor. Değer uydurulmadı, uygulamanın geçirdiği
değerin aynısı (`CodecFromIndex(0)`).

Kolu yanlışa çevirmek düzeneğin çıktısını **dört yerden** değiştiriyor. İki yön
de koşuldu, aynı satırda (`parca-1` @ 3,4975 MB), aynı gün, aynı tabanda:

| düzeneğin çıktısı | `Codec` atanmadan (Compatible) | `Codec = Auto` |
|---|---|---|
| hedef (MB) | 3,4975 | 3,4975 |
| ffmpeg kodek argümanı | `-c:v libx264 -preset slow` | `-c:v libsvtav1 -preset 6` |
| kodek özel parametreler | yok | `-svtav1-params keyint=600:scd=1:tune=0:…` |
| seçilen yerleşim | 768x432 | **882x496** |
| teslim edilen bayt | 3.525.089 | 3.531.823 |
| harmonik | 33,44 | **45,97** |

Yani mutasyon ölçülüyor ve dört ayrı çıktıyı birden kırıyor; "ölçü yok" durumu
değil.

## Hangi satırda kodek gerçekten değişti

Ölçülen tek satır `parca-1` @ 3,4975 MB (oran 25,24, `Aggressive`) ve orada
kodek **değişti**: libx264 → libsvtav1. Yukarıdaki oran tablosuna göre 60 MB
hedefinin öteki iki satırında da değişmesi beklenir, 600 MB hedefinin üç
satırında ise değişmemesi. **Bu beş satır koşulmadı — beklenti koddan okundu,
ölçülmedi.**

Kodeğin değişmediği satırlarda puanın da değişmemesi gerektiği koşulu
(sözleşmenin K4 kapısı) bu turda **sınanamadı**: sınanabilmesi için 600 MB
hedefinin üç satırının hem eski hem yeni kolla koşulması gerekiyordu, koşulmadı.

## K6'yı koşacak olana — adıyla duran karıştırıcı

Yerleşim ayrıştırması (`parca-2` @ 60 MB, `AllowResolutionDrop = false`) bu
turda **koşulmadı**; sözleşmedeki karar eşikleri (69,65 ve 93,70) yeniden
üretilemeyen `381e8ab` tabanından geliyor ve önce bugünkü tabanla yeniden
kurulmaları gerekiyor (`ab-duzenegi.md`, T125 bölümü).

Koşulduğunda şu karıştırıcı sonucun içinde olacak ve ayrıştırılmamış olacak:
düşürme kararı parçanın karmaşıklığından türediği için **düşürülen parçalar
aynı zamanda zor parçalardır**; ve ~500k bit hızında 1080p60 için x264,
x265-slow'a karşı sınıf farkıyla zayıftır. Zorunlu-1080p daha kötü çıkarsa bu
"düşürme doğruydu" demek değildir; **"düşürme zayıf kodek için doğruydu"**
anlamına da gelebilir. İki okumayı ayırmak ayrı bir ölçümün işi.

Bu karıştırıcı düzeltilmiş kolda küçülüyor ama yok olmuyor: Auto artık
libsvtav1 koşuyor, yani "zayıf kodek" kolu 60 MB hedefinde geçerli değil —
ama K6 kodeği bugünkü libx264'te sabit tutmayı istiyor, dolayısıyla K6'nın
kendi koşumunda karıştırıcı **tam güçte** duruyor.

## Koşum künyesi

| ne | değer |
|---|---|
| kaynak | `.calisma/kaynak/parca-1.mkv` — 92.577.316 bayt, 1920x1080, 60 fps, 60,399 sn, bt2020/PQ HDR, yalnız video |
| kaynağın kaynağı | `kaynak-1080p60-hdr-17dk-yalniz-video.mkv`, `00:02:00`'dan 60 sn, `-c copy` (`ChunkCutter.Specs`) |
| hedef | 3,4974511806023365 MB — yayımlanan `parca-1` @ 60 MB satırının hedefiyle **birebir aynı** |
| taban commit | `fcf377f` (`origin/main`) |
| karşılaştırılan taban | `381e8ab` (yayımlanan tablo) |
| HandBrake | 1.11.2 |
| ffmpeg | 9.0-full_build-www.gyan.dev |
| kodek yedeği | `libsvtav1` ffmpeg'de mevcut, yani `MaxCompression` = libsvtav1 (libx265'e düşülmedi) |
| makine | Windows 11 Pro 22631, **yüklü** — koşum sırasında başka ajanlar da kodluyordu |
| koşum sayısı | Compatible kolu 1, Auto kolu 1, HandBrake 2 (iki koşumda da aynı dosya) |
| süre ölçüsü | **alınmadı** — makine yüklüydü, duvar saati anlamlı değil |

Çıktılar ve günlükler `.calisma/ab/t125/` altında (`k1-parca-1-compatible.json`,
`k2-parca-1-auto.json`, `cikti/`, `auto-cikti/`, `gunluk/`, `auto-gunluk/`).

### Bu turda ölçülmeyenler

- `parca-2` ve `parca-3` hiçbir hedefte, hiçbir kolla koşulmadı.
- 600 MB hedefinin altı satırının hiçbiri koşulmadı; dolayısıyla K4'ün
  "kodeği değişmeyen satırda puan da değişmemeli" kapısı **sınanmadı**.
- K6'nın zorunlu-1080p koşumu yapılmadı.
- VidShrink kolundaki değişimin ne kadarının kodlayıcıdan (`-g 120` →
  `-g 600 -keyint_min 60`) ne kadarının ölçerden geldiği ayrıştırılmadı.
  HandBrake kolunda bu ayrıştırma gerekmedi: dosya bit bit aynı olduğu için
  farkın tamamı ölçerden geliyor.
- Ölçerin tekrar sapması bugünkü kodda **sıfır** ölçüldü (iki koşum, onbeş
  basamak aynı); ama bu tek dosya üzerinde. Başka girdilerde sıfır olduğu
  varsayılmadı, ölçülmedi.

## Taban yeniden kuruluyor — karar kuralları sayılardan önce yazıldı

Round-1 önerisi buydu: on iki satırın tamamını yeniden ölçmek, kalan beş
çiftle sınırlamamak — gerekçe K1'in bulgusu (0,86 bir varyans değil sabit
sürüm ofseti, on iki satırın hepsi `381e8ab`de ölçülmüş).

**T0'ın tur-2 kararı bunu daraltı:** yalnız kalan beş çift bugünkü tabanda
koşuldu (bkz. `T125.md`, "## T0 kararı — tur 2"). Zaten ölçülmüş yedi satır
yeniden koşulmadı; sürüm ofseti onlarda da geçerli olduğu için tabloya
kıyaslama uyarısı düşüldü, iki ayrı ölçerin karıştığı iddiası kalkmadı ama
tur-2'nin manşeti bu on iki satırın hepsini değil yalnız yeni altı satırı
sayıyor.

Aşağıdaki kurallar **koşum bitmeden** yazıldı; yeni sayılar geldiğinde
eşiklerin sayıya göre seçilmediği bu sırayla belli olsun.

- **Manşet.** "Altı parça-hedef çiftinin beşinde HandBrake kazandı" cümlesi
  yeni tablonun harmonik sütununda satır satır sayılır. Kaç çift çıkarsa o
  yazılır; beklenen yön yok. Eş boyut kapısından geçmeyen satır **sayıma
  girmez**, sayılmadığı da yazılır.
- **Kodek kolunun payı.** Yeni tablo eski tabloyla **kıyaslanmaz** (sürüm
  sınırı). Kodeğin payı yalnız aynı tabanda ölçülmüş Compatible ↔ Auto
  ikilisinden okunur.
- **K6 eşikleri.** Eski 69,65 / 93,70 / "≥ ~81" üçlüsü **düşürüldü**. Yeni
  kural: zorunlu-1080p koşumu, aynı tabandaki kendi iki sayısıyla kıyaslanır —
  düşük çözünürlüklü VidShrink satırı (alt sınır) ve HandBrake satırı (üst
  sınır). Puan alt sınırda ya da altında kalırsa yerleşim hipotezi **öldü**;
  iki sayının arasını yarıdan fazla kapatırsa **yaşıyor**. Sayılar koşum
  bitince buraya yazılır.

## Asimetri — hangi kolda ne değişti

`381e8ab` ile bugünkü taban arasında iki kol **eşit ölçüde** değişmedi:

| kol | kodlayıcı değişti mi | ölçer değişti mi |
|---|---|---|
| HandBrake | hayır — argüman dizesi aynı, çıktı bit bit aynı (3.531.037 bayt) | evet |
| VidShrink | evet — `8ea80c4` (T98) `-g 120` → `-g 600 -keyint_min 60` | evet |

Bu yüzden eski tablodaki iki kolun farkı, sürüm sınırını geçen bir çıkarmadır ve
iki kolda iki farklı sebep taşır. Yeniden kurum bunu **çözüyor**: on iki satırın
on ikisi tek tabanda, tek ölçerle üretiliyor. Sonraki okuyanın aynı soruyu
sormasına gerek yok — eski tabloyla yeni tablo arasında satır kıyaslaması
yapılamaz, yalnız yeni tablo kendi içinde okunur.

## T120 bu bulgudan etkilenmiyor

T125'in bulgusu T120'ye **dokunmuyor** ve bu iki ayrı yoldan doğrulandı.
T120 bu düzeneği hiç çağırmıyor: kendi koşum programı var
(`tools/auto-mod-olcumu/harness/Program.cs`) ve orada `Codec = CodecPreference.Auto`
doğrudan kuruluyor — yani T125'in düzelttiği yapılandırma hatası o koşumda hiç
yoktu. Ayrıca T120'nin denetimi 18 vmaf JSON'unu kendi ortamında yeniden
hesapladı, yani −0,266'nın iki tarafı tek tabanda ölçülmüş; sürüm sınırı o
satırdan geçmiyor.

İki düzenek tek bir alanda ayrışıyor, karıştırılmasın diye yazılı: T120'nin
koşumu `AllowFpsDrop = true`, buradaki A/B `AllowFpsDrop = false`. Öteki alanlar
(`Intent.Sharing`, `FillPolicy.FillTarget`, `SpeedMode.Quality`,
`AllowResolutionDrop = true`) iki tarafta da aynı.

## K6 düzeneği sınandı — ve sınarken bir kararsızlık ölçüldü

Anahtar iki yönde de çalışıyor. Koşum künyesi:

- girdi: 3 sn'lik 1080p60 HDR kesit
- hedef: 0,35 MB
- ikili ve ortam aynı; tek fark `T125_YERLESIM_KILIT` ortam değişkeni

| koşum | `T125_YERLESIM_KILIT` | plan | sonuç |
|---|---|---|---|
| kilitli | 1 | **1920x1080**@60 | hedefin üstünde kaldı, dosya yazılmadı (çıkış 1) |
| serbest | yok | 882x496@60 | teslim edildi (çıkış 0) |

Günlüğe `yerlesim kilitli: T125_YERLESIM_KILIT=1, AllowResolutionDrop=false`
satırı basılıyor, yani kilit koşum künyesinde görünür.

### Aynı komut iki farklı deney üretiyor — HDR yolu koşumdan koşuma dönüyor

Sınama sırasında **beklenmeyen** bir şey çıktı ve ölçüldü. Aynı komut, aynı
girdi, aynı ikili, arka arkaya iki koşum:

| koşum | pix | HDR | renk kapısı | bayt | harm | p10 | XPSNR |
|---|---|---|---|---|---|---|---|
| serbest (1) | yuv420p | SDR'a tonemap | **ReferenceTransformed** | 362.102 | **25,85** | 16,49 | 28,86 |
| serbest (2) | p010le | korundu | **Direct** | 355.516 | **27,57** | 17,76 | 30,68 |

Yerleşim ikisinde de aynı (882x496); dönen tek şey HDR yolu. Fark **1,72 puan**,
yani K1'de ölçülen sürüm ofsetinden (0,86) büyük.

Sebep koda kadar izlendi, tahmin değil: `HdrResolver.Resolve` HDR korumayı
`SupportsHdr10` → `WorksAsEncoder(codec)` üzerinden soruyor
(`src/VidShrink.Core/HdrResolver.cs:24`), o da `EncoderCapabilities.Probe`nin
**deneme kodlamasına** bakıyor (`src/VidShrink.Ffmpeg/EncoderCapabilities.cs:30`).
Deneme kodlamanın duvar saati bütçesi **4 sn** ve aşılırsa süreç öldürülüp
`false` dönüyor (`EncoderCapabilities.cs:110-114`). `false` dönünce ilke
sessizce `TonemapToSdr`a düşüyor — kodek yine `libsvtav1` seçiliyor, çünkü kodek
seçimi ayrı yoldan (`HasEncoder`) karar veriyor.

Yoklamanın bu makinedeki süresi ayrıca ölçüldü, sekiz tekrar: **121–720 ms**
(p010le yoklaması 126–324 ms). Yani bütçeye kalan pay bugünkü yükte yaklaşık
5 kat. Gözlenen dönüş bir kez görüldü ve tekrarında geri döndü; **4 sn'yi neyin
aştığı ölçülmedi** — süre damgası `EncoderProbeResult`ta duruyor ama A/B
raporuna yazılmıyor.

Ölçüm için sonucu şudur, ve iyi haber tarafı var: **düzenek buna kör değil.**
Tonemap'e düşen satır renk kapısında `ReferenceTransformed` damgası yiyor ve bu
damga JSON'da satır satır duruyor. Yani karışım **tespit edilebilir**; yeni
tablonun on iki satırının on ikisinde `ColorGate` alanı tek tek denetlenir,
`Direct` olmayan satır tabloya girmez, yeniden koşulur.

Kök `src/` altında ve **bu sözleşmenin owns'unda değil**: ölçüldü, yazıldı,
dokunulmadı.

### Bizim kolun koşumdan koşuma yayılımı

K1'de HandBrake'in bit bit aynı dosyayı ürettiği ölçülmüştü (üç koşum, 0 fark).
Bizim kol için aynısı **doğru değil**: `parca-1` @ 3,4975 MB, aynı kod, iki
koşum, ikisi de `Direct` ve Auto:

| koşum | yerleşim | bayt | harm |
|---|---|---|---|
| K2 turu | 882x496 @464k | 3.531.823 | 45,9707 |
| taban turu | 882x496 @464k | 3.531.265 | 45,93 |

Fark **0,04 puan** (bayt farkı 558). Kalibrasyon yoklaması ve eşitleyici koşuma
bağlı olduğu için bizim kolda küçük ama sıfır olmayan bir yayılım var; ölçer
tarafı gürültüsüz (K1), yayılım kodlayıcı tarafından geliyor. Yeni tablodaki
0,04'ten küçük farklar bu yüzden ayırt edici sayılmaz. n = 2, makine yüklü.

## T125 tur 2 — K4 kapısı ve K6 eşiği kapandı

175. satırdaki kurallar koşum bitmeden yazılmıştı. Aşağıdaki sayılar o kurallara
göre okunuyor, sıra bozulmadı.

### K4 — kodek değişmeyen satırlarda skor da değişmemeli mi

600 MB hedefli üç satırın üçü de `CompressionStrategy.RegimeFor` üzerinden
Balanced rejime düşüyor, yani hem Auto hem Compatible kolu **aynı kodeği**
seçiyor (`libx264`, `Settings` alanında doğrulandı). Bu üç satır K4'ün doğal
test kümesi — ayrı bir koşum kurulmadı, mevcut Auto/Compatible çiftinden okundu.

| satır | çözünürlük (ikisi de) | Auto harm | Compat harm | fark | bayt farkı |
|---|---|---|---|---|---|
| parça-1 @ 34,97 MB | 1382x778 | 72,80 | 72,82 | 0,02 | %0,03 |
| parça-2 @ 35,00 MB | 1920x1080 | 96,12 | 96,10 | 0,02 | %2,65 |
| parça-3 @ 34,99 MB | 1190x670 | 66,11 | 65,75 | **0,36** | %2,05 |

İlk iki satır 0,04 puanlık doğal yayılım bandının (K1'deki n=2 ölçüm) içinde —
kodek sabit, skor da fiilen sabit. Üçüncü satır bandın dışına çıkıyor.

**KRİTİK değil.** Sebep izlendi: parça-3 satırında iki kol aynı hedefe
kilitlenmiş olsa da (34,99 MB), gerçekleşen bayt %2,05 ayrıştı — bu, doğal
yayılımın ölçüldüğü referans çiftin (%0,03 bayt farkı) yaklaşık 70 katı. K4'ün
0,04 puanlık eşiği *bayt eşit* iki koşumdan geliyordu; burada bayt eşit değil,
±%2 `SizeEqual` toleransının iki ucuna düşmüş iki bağımsız kalibrasyon. Skor
farkı kodek seçiminden değil, bu bayt farkından geliyor — parça-2 satırı da
%2,65 bayt farkı taşıyor ama 96 puanlık tavana yakın bölgede ek bitin getirisi
düşük olduğundan skor farkı yine 0,02'de kalıyor; parça-3 orta bantta (65-66
puan) olduğundan aynı büyüklükteki bayt farkı skoru daha çok oynatıyor.

Kapıyı sıkılaştıran not: K4'ün 0,04 puanlık eşiği yalnız *bayt de eşit* olan
çiftlerde geçerli. Bayt farkı %2'ye yaklaştığında eşik kendisi geçersiz —
yeniden kurulmadı, gözlem olarak burada kayıtlı.

### K6 — zorunlu-1080p eşiği yeni tabanla yeniden kuruldu

Kural (175. satır): zorunlu-1080p skoru, aynı tabandaki düşük çözünürlüklü
VidShrink satırıyla (alt sınır) ve HandBrake satırıyla (üst sınır) kıyaslanır.
Üçü de `parça-2 @ 3,4999 MB`, üçü de Compatible kodek (K6 koşumu da kodek kolu
Compatible'a sabitliyken koşuldu, yerleşimi izole etmek için):

| satır | yerleşim | harm |
|---|---|---|
| alt sınır — VidShrink serbest yerleşim | 1152x648 | 71,01 |
| K6 — VidShrink zorunlu 1080p | 1920x1080 | **89,67** |
| üst sınır — HandBrake | 1920x1080 | 93,73 |

Açık (üst − alt) = 22,72 puan. K6 alt sınırın üstünde kapattığı pay =
89,67 − 71,01 = 18,66 puan → açığın **%82,1**'i.

Kural yarıdan fazla kapamayı "yaşıyor" sayıyordu. %82,1 > %50, dolayısıyla
**yerleşim hipotezi yaşıyor**: zorunlu 1080p'ye kilitlemek skor açığının
büyük kısmını kapatıyor, tek başına yerleşim düşüşü açığın küçük bir parçası.

### K4 ile K6'nın 1,72 puanlık HDR kararsızlığı arasındaki ilişki

230. satırda ölçülen HDR bayrağı (`ColorGate`, `EncoderCapabilities.Probe`'un
4 sn'lik yoklama bütçesi) K4 ve K6'nın hiçbirini bu turda etkilemedi —
tur 2'nin on beş ölçümünün (5 yeni satır × 3 sütun) tamamında `ColorGate=Direct`
kaydedildi, hiçbiri `ReferenceTransformed`e düşmedi. Yani bu turda gözlenen K4
sapması (parça-3, 0,36 puan) o hatayla **ilgisiz** — kaynağı ayrı ve yukarıda
izlendi (bayt yayılımı). İki bulgu birbirini açıklamıyor, ikisi de ayrı ayrı
gerçek: HDR yolu hâlâ riskli (kod düzeltilmedi, `owns` dışında), K4'ün
0,04 puanlık eşiği hâlâ yalnız bayt-eşit çiftlerde geçerli.
