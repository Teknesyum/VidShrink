# T172 - Ses tabani: AudioDropped kolu kaldirildi

Dal: `T172-ses-tabani`. Motor karari: kaynakta ses varsa ciktida da ses olmali;
`audioK < 16 && totalK < 96` kosulu ses akisini tamamen dusuren yolu artik **kaldirilmis**
durumda (bkz. `git diff` - bu degisiklik onceki ajan tarafindan yapildi, bu turda dogrulandi).

## K1 - Bugun (eski kod) hangi hedeflerde ses dusuyor

Eski `PlanCalculator` (`AudioDropped` kolu iceren, `.calisma/T172/eski-core/`) ile 4 gercek
kaynagin olculmus sure/ses degerleri (1dk=60,01s, 2dk=120s, 5dk=300,01s, 10dk=600s,
kaynakBps~128000, kanal=2) uzerinden 28 hedef MB degeri tarandi. Ham cikti (113 satir):
`.calisma/T172/k1-ham.txt`.

Tetikleme siniri (son `AudioDropped=True` hedef -> ilk `AudioDropped=False` hedef):

| sure | son dusen hedef | ilk tutan hedef | esik (kod: `totalK<96`) |
|---|---|---|---|
| 1 dk  | 0,6 MB | 0,7 MB | totalK 81,9->95,6 kbit/s |
| 2 dk  | 1,2 MB | 1,4 MB | totalK 81,9->95,6 kbit/s |
| 5 dk  | 3,0 MB | 3,5 MB | totalK 81,9->95,6 kbit/s |
| 10 dk | 6,0 MB | 7,0 MB | totalK 81,9->95,6 kbit/s |

Kendi saydim (duzeltme, tur 3): `awk -F'\t' 'NR>1 && $6=="True"' .calisma/T172/k1-ham.txt | wc -l`
= **55** satir `AudioDropped=True` (taranan 4x28=112 satirin 55'inde tetikliyor), dakika
bazinda dagilim `awk -F'\t' 'NR>1 && $6=="True" {c[$1]++} END{for (k in c) print k, c[k]}'`
ile: 1dk=5, 2dk=11, 5dk=17, 10dk=22 (toplam 5+11+17+22=55). Onceki turda buraya yanlislikla
44 yazilmisti (`grep -c True` deseni satirin tamamini eslestirmeye calisiyordu, sutun
bazli degil); esik tablosu (`:16-21`, alttaki) hep dogruydu, yalniz bu ozet cumle yanlisti.

Test amacli secilen 5 kombinasyon (K2/K3'te kullanilan): 1dk/0,3MB, 2dk/0,8MB, 5dk/2MB,
10dk/5MB (dordu de tetikliyor) ve 10dk/8MB (tetiklemiyor, kontrol grubu).

## K2 - Ses her ciktida var mi (gercek kodlama + ffprobe)

Gercek `EncodeRunner` (3 deneme, uygulamanin kullandigi ayni yol) ile kodlandi. Harness:
`.calisma/T172/calistir/Program.cs`. Ham cikti: `.calisma/T172/yeni-k2k3.txt`.

| kombinasyon | yeni kod sonucu | dosya yazildi mi | ffprobe ses akisi |
|---|---|---|---|
| 1dk/0,3MB  | basarisiz (3 deneme, 0,547MB) | Hayir (EncodeRunner "no file was written") | - |
| 2dk/0,8MB  | basarisiz (3 deneme, 1,105MB) | Hayir | - |
| 5dk/2MB    | basarisiz (3 deneme, 2,787MB) | Hayir | - |
| 10dk/5MB   | basarisiz (3 deneme, 5,587MB) | Hayir | - |
| 10dk/8MB   | basarili (7,71MB, 2 deneme)   | Evet  | codec_type=audio codec_name=aac (ham: `.calisma/T172/yeni-ffprobe.txt`) |

**Not - bu K2'yi gecersiz kilmaz, K3'e tasir:** 4/5 kombinasyonda motor hic dosya teslim
etmiyor (hedefi 3 denemede de asiyor), o yuzden "sifir ses akisli dosya" da yok - cunku
hic dosya yok. Tek uretilen dosyada (10dk/8MB) ses akisi var. Dosya uretmeyen 4
kombinasyon K3'te ele aliniyor: bunlar **eskiden basariyla (sessiz) teslim oluyordu**,
yeni kodda "hic teslim yok"a donuyor.

## K3 - Hedef hala tutuyor mu: TUR 1'de KIRMIZI (3/5 kombinasyonda regresyon)

**Bu bolum tur 1'in sonucudur ve tur 2'de duzeldi** - guncel durum icin asagidaki
"Tur 2 - K8/K9/K10" basligina ve uc sutunlu K3 tablosuna bak.

Ayni 5 kombinasyon, ayni harness, **eski kod** (`git stash` ile `AudioDropped` geri
getirilip `dotnet build -c Release --no-incremental` ile derlendi, olculdu, sonra
`git stash pop` ile geri alindi - calisma agaci `git diff --stat` ile dogrulandi, dosya
sayisi ve satir sayisi mutasyon oncesiyle birebir ayni). Ham cikti: `.calisma/T172/eski-k2k3.txt`,
ffprobe: `.calisma/T172/eski-ffprobe.txt`.

| kombinasyon | hedef MB | ESKI kod: gercek MB / basari / ses | YENI kod: gercek MB / basari / ses |
|---|---|---|---|
| 1dk/0,3MB  | 0,3 | 0,342 / **basarisiz** / - (dosya yok) | 0,547 / **basarisiz** / - (dosya yok) |
| 2dk/0,8MB  | 0,8 | 0,762 / **basarili** / ses YOK (video-only) | 1,105 / **basarisiz** / - (dosya yok) |
| 5dk/2MB    | 2   | 1,95  / **basarili** / ses YOK (video-only) | 2,787 / **basarisiz** / - (dosya yok) |
| 10dk/5MB   | 5   | 4,962 / **basarili** / ses YOK (video-only) | 5,587 / **basarisiz** / - (dosya yok) |
| 10dk/8MB   | 8   | 7,71  / **basarili** / ses VAR (aac, mono) | 7,71  / **basarili** / ses VAR (aac, mono) |

Kendi saydim: 5 kombinasyondan **3'unde** (2dk/0,8MB, 5dk/2MB, 10dk/5MB) eski kod hedefi
tutarak (sessiz) teslim ediyordu, yeni kod ayni hedefte **hic dosya teslim etmiyor**. Bu
**K3'un dustugu** anlamina geliyor - bandi genisletmeden, kirmizi olarak bildiriyorum.

**Motor bu 3 aralikta ne yapiyor:** `PlanCalculator.cs:391`'deki
`videoK = Math.Max(MinVideoBitrateK, totalK*ContainerOverhead - audioK - DeliveryReserveK(codec))`
formulunde `MinVideoBitrateK=48`. Eski kodda `audioK=0` oldugu icin cikan ham deger
(53k/53k/67k) 48'in uzerinde kaliyor ve dogrudan kullaniliyor. Yeni kodda ses payi
(24k mono taban) dusulunce ayni ham deger 48'in **altina** dusuyor ve videoK sabit 48'e
"floor"laniyor - bu da ayri bir dala (`FrameRateCutForFloor` notu, yeni kosumlarda var,
eskide yok) giriyor. Floor'a carpan plan iki-pas kodlamada 3 denemede de hedefi asiyor
(0,762->1,105MB gibi ~%45 fazlasi). Yani kusur "ses varligi" degil, **videoK floor'unun ve
iki-pas kodlayicinin bu floor civarinda hedefi tutamamasi**; ses tabani bu var olan
zayifligi daha genis bir araliga yayiyor cunku artik daha fazla kombinasyon floor'a
carpiyor.

Bu, `EncodeRunner.cs`/`FfmpegArguments.cs`'e dokunmadan `owns` icinde (`PlanCalculator.cs`)
cozulebilecek bir butce kalibrasyonu gibi gorunuyor, ama guvenli bir duzeltme (floor'u
dusurmek, container overhead'i yeniden olcmek) bu sozlesmenin olcum butcesinin disinda -
**gizlemiyorum, bildiriyorum:** K3 bu 3 kombinasyonda kirmizi, takip sozlesmesi gerekir.

## K4 - Kaynakta ses yoksa

`sessiz-kaynak.mkv` (15s, `hasAudio=False`), hedef 0,4MB. Ham cikti: `.calisma/T172/k4-ham.txt`.

Ham cikti aynen (`.calisma/T172/k4-ham.txt`, duzeltme: onceki surum bu bloku kirpmisti,
`boyutMB` ve `[deneme 1]` satirini atlamisti):

```
[probe] sure=15s hasAudio=False kanal=0 audioBps=0 boyutMB=24,73
[plan] codec=libx264 mode=2pass videoK=214 audioK=0 audioCodec=null kanal=kaynak regime=Extreme notlar=CodecUpgradeRecommended,ExtremeRatioWarning,ResolutionReduced,TargetEnforcedTwoPass
[encode] basarili=True ciktiMB=0,39 denemeler=1 hata=
[deneme 1] in band hedefMB=0,384 gercekMB=0,39 videoK=214 mod=2pass
```

`notlar` listesinde ses ile ilgili **hicbir** kod yok (kendi saydim: 4 not, hicbiri
Audio* degil). ffprobe (`.calisma/T172/k4-ffprobe.txt`): sadece `codec_type=video` - ses
akisi yok, beklenen. Ayrim olcusu: `info.HasAudio` kaynagin kendi ses akisini tanimlar;
motor bunu **uretmiyor**, korumaya calisiyor. Kaynakta ses yoksa "cikarildi" mesaji hicbir
zaman uretilmiyor cunku uretecek kod yolu (`AudioDropped`) artik yok.

## K5 - Olu uye ve olu metin

`OluUyeTests` + `LanguageTests` filtresi: **66/66 yesil** (ham: `.calisma/T172/final-verify2.txt`).
Olcunun kendi ciktisi (`.calisma/T172/k5-detay.txt`, satir 44): `uye: 162  bu dosyada adi
gecmeyen: 111  pimlenen: 37`. `AudioDropped` kaldirilmadan once bu sayi 163 idi (testin
kendi yorumunda kayitli); kaldirilinca 162'ye dustu - tek uye eksildi, beklenen. Pim
listesinde (`Pinned` dizisi, satir 385-472) `AudioDropped` adi hic gecmiyor, bu yuzden pim
sayisi (37) degismedi.

`main.advice.audio-dropped` anahtari iki dilde de silindi (`en/main.json`, `tr/main.json`);
`LanguageTests` (66'nin icinde) yesil, eksik anahtar sikayeti yok.

## K6 - Mutasyon izgarasi

Iki mutasyon, her biri `dotnet build -c Release --no-incremental` sonrasi olculdu, sonra
kaynak `.calisma/T172/PlanCalculator.cs.oncesi` yedeginden birebir geri yuklendi (`diff`
ile dogrulandi).

| mutasyon | ham cikti | kirilan olcu |
|---|---|---|
| (a) 24 kbps tabani -> 0 | `.calisma/T172/k6-mutasyonA-v2.txt` | `SesTabaniTests.K6_SesTabaniYirmiDortKbpsAltinaDusmez` (13 testten 1'i FAIL: "audioK=8, taban 24 olmali") - bu test bu turda eklendi cunku var olan testler (`PlanCalculatorTests`, orijinal `SesTabaniTests`) sadece `audioK>0` kontrol ediyordu, tam 24 degerini degil |
| (b) dusurme kolu geri | `.calisma/T172/k6-mutasyonB.txt` | `SesTabaniTests` filtresinde 13 testten **10'u FAIL** (K2 teorisinin 4 kombinasyonu + K6 testi) |

Her iki mutasyon da beklenen olculeri kirdi; mutasyon (a) icin mevcut test setinde
kapsama boslugu bulundu ve `tests/VidShrink.Tests/SesTabaniTests.cs` (owns icinde)
`K6_SesTabaniYirmiDortKbpsAltinaDusmez` testiyle kapatildi.

## K7 - Kol sayisi

`--list-tests` ile dogrulandi, sifir bulan kol yok:

| verify filtresi | test sayisi |
|---|---|
| `PlanCalculatorTests\|SesTabaniTests\|ManualOverrideTests` | **tur 1 anindaki sayim** 122 (ham: `.calisma/T172/final-verify1.txt`; K6 testi eklenmeden once 121 idi, `.calisma/T172/k7-liste1.txt`). Guncel sayi tur 4 bolumunde. |
| `OluUyeTests\|LanguageTests` | 66 (ham: `.calisma/T172/k7-liste2.txt`) |

## Tur 1 verify sonucu

- `PlanCalculatorTests|SesTabaniTests|ManualOverrideTests`: **121/122 yesil, 1 kirmizi.**
  Kirmizi: `ManualOverrideTests.K1_VarsayilanPlanGoldenDegerleriyleBirebirAyni` (1920x1080@30,
  600s, 6MB hedef satiri) - bu dosya `owns` disinda (`tests/VidShrink.Tests/ManualOverrideTests.cs`
  bu sozlesmenin sahip oldugu dosyalar arasinda degil). Test, 9b092e9 baz alinarak
  "ses 0k/kaynak" (o zamanki `AudioDropped` davranisi) bekliyor; T172'nin motor karari bu
  satirda artik "ses 24k/mono" uretiyor. **Bu beklenen bir kirilma** - golden degerlerin
  T172'yi yansitacak sekilde guncellenmesi ayri bir sozlesmenin (muhtemelen bu dosyayi
  sahiplenen tur) isi; owns sinirini asip duzeltmedim, bildiriyorum. Ham:
  `.calisma/T172/final-verify1.txt`.
- `OluUyeTests|LanguageTests`: **66/66 yesil.** Ham: `.calisma/T172/final-verify2.txt`.

## Sinir ihlali yok

`EncodeRunner.cs`, `ConversionArguments.cs`, `FfmpegArguments.cs` okunuyor, **yazilmadi**.
Mutasyonlar sadece `owns` icindeki `PlanCalculator.cs`'de yapildi ve geri alindi.
`.calisma/kaynak/` okunuyor, yazilmadi/silinmedi. Tum olcumler `.calisma/T172/` altinda.

## Tur 2 - K8/K9/K10 (floor duzeltmesi olculdu): K3 YESIL

Tur 1'in bakiyesi: PlanCalculator.cs satir 391/545/980/988'de Math.Max(MinVideoBitrateK, ...)
-> Math.Max(0.0, ...) (ve satir 988'de Math.Max(0, ...)) mutasyonu commit'lenmemis halde
duruyordu, K3'un kirmizi bulgusuna cevaben denenmis ama olculmemisti. Bu tur o dort satiri
oldugu gibi kabul etmedi; once MinVideoBitrateK'in tum kullanim yerleri sayildi.

### MinVideoBitrateK sayimi (K8 on-kosul, tur 3'te duzeltildi)

**Duzeltme (tur 3, denetim KRITIK 1 ve KRITIK 3):** bu bolum onceki turda "tanim + 6
kullanim" diyordu ve 4 kullanimin (505, 511, 520, 528) "hic tetiklenmedi" oldugunu
soyleyerek duzeltmiyordu. Gercek diff (`ba705cf`, main'e karsi) **dort** yerde floor
kaldirmisti: 391, 545, **980, 988** - 980/988 (`Correct()` yeniden-deneme kolu) o turde
tabloya hic girmemisti. Ayrica 505/511/520/528'in "tetiklenmiyor" iddiasi kendi supurme
dosyasiyla (`tur2-baseline-sweep-v2.tsv`) yalanlaniyordu: 171 satirin 11'i videoK=48'e
cakiliydi, notlar sutunu `BudgetExceedsCeiling,FillTwoPassBandTooNarrowForCrf` - tam
olarak bu dal. Bu tur (K11) 505/511/520/528'i de 391/545/980/988 ile ayni yaklasimla
(`Math.Max(MinVideoBitrateK, X)` -> `Math.Max(0.0, X)`) duzeltti. Guncel envanter,
`grep -n MinVideoBitrateK src/VidShrink.Core/PlanCalculator.cs` -> tanim + 2 kullanim:

| satir | baglam | floor'suz mu | gerekce |
|---|---|---|---|
| 159 | private const int MinVideoBitrateK = 48 (tanim) | - | - |
| 391 | videoK - SearchLayout'a giden ana butce | evet (tur1) | bu deger cozunurluk/fps aramasinin butcesi; floor burada durursa arama gercek butceyi degil, yapay olarak sisirilmis 48k'yi gorup daha buyuk bir duzen seciyor, sonra kodlayici o duzeni bu kadar dusuk bitrate'te tutamiyor |
| 505 | ceilingVideoK - "butce comert" (CRF tavanina takilan) dalinda son deger | **evet (tur3, K11)** | K11 supurmesinde bu dal gercekten tetikleniyordu (20-120dk/10-50MB araliginda 11 satir; en kisa satir 20dk/10MB); floor kaldirilinca ayni yaklasimla 0.0'a indirildi |
| 511 | desiredVideoK - FillPolicy.FillTarget ince ayari, 505'in dalinin icinde | **evet (tur3, K11)** | ayni gerekce |
| 520 | desiredVideoK fill-CRF kolunda son deger | **evet (tur3, K11)** | ayni gerekce |
| 528 | desiredVideoK fill-iki-pas kolunda son deger | **evet (tur3, K11)** | ayni gerekce - K11 sweep'inde 11 cakili satirin tumu bu kola (`FillTwoPassBandTooNarrowForCrf`) giriyordu |
| 545 | videoK - TargetEnforcedTwoPass dalinin son videoK'si | evet (tur1) | bu tam olarak K1-K3'un dustugu dal; 391'deki gercek butce burada da uygulanmazsa arama dogru duzeni secse bile son atama yapay 48'e zaplanirdi |
| 625 | LockedCrf (elle CRF sabitleme) dalinda tahmini videoK | hayir | sozlesmenin "elle gecersiz kilma kollari degismez" siniri (T165 alani) - kullanici CRF'i sabitledikten sonra hedef boyut zaten zorlanmiyor, plan.VideoBitrateK yalniz bir goruntu tahmini |
| 868 | QualityFloorTargetMb - kalite-hedefli mod (TargetMbForQuality) icin taban MB hesabi | hayir | ayri bir ozellik (kalite hedefi girilen mod), K1-K3'un test ettigi boyut-hedefli mod degil |
| 980/988 | `Correct()` yeniden-deneme kolu: `videoBudgetK` ve `corrected.VideoBitrateK` | evet (tur1'de yapilmis, tur2 denetiminde saklanmis, tur3'te envantere eklendi) | K8'in bes kosumunun **besi de 2. denemede** basarili oldu (asagidaki K8 tablosu) ve o denemelerin videoK'lari **14/26/26/40/78** - dordu 48'in altinda; bu kol 48'e cakili kalsaydi 2. deneme de hedefi asardi |

Kendi saydim: 2 kullanim (159 tanim disinda) floor tasiyor (625, 868 - her ikisi de bu
sozlesmenin kapsami disinda, yukarida gerekceli); geri kalan tum butce-hesabi kollari
(391, 505, 511, 520, 528, 545, 980, 988 - **8 yer**) artik floor'suz.

Asil is cozunurluk/fps secimi sorusuna cevap: floor'u 391'de kaldirmak SearchLayout'un gercek
(0'a kadar inebilen) butceyi gormesini sagliyor; arama bu butceye gore daha kucuk cozunurluk/
kare hizi seciyor (asagidaki K8 tablosunda TargetBelowCodecFloor notu 4/5 kombinasyonda
goruluyor - bu, best.MeetsFloor=false demek, yani secilen duzen kod motorunun "anlamli goruntu"
esiginin altinda, ama teslimat basarili ve hedefi asmiyor). Floor'u 0'a indirmek "48'e cakmamak"
degil, "videoK'yi olceklendirmenin dogru butceyle yapilmasini saglamak" oldu; alt satirda zaten
var olan guvenlik agi (satir 549-568, deliverK kontrolu) liftedMb hedefi asarsa videoK'yi 48'e
zorlamiyor, TargetBelowCodecFloor notuyla oldugu gibi birakiyor.

### K8 - Bes kombinasyonun besi de dosya/ses/hedef sartini karsiliyor mu

.calisma/T172/kaynak-ses/ kaynaklariyla, floor duzeltmesi build'e alinmis halde, ayni 5
kombinasyon gercek EncodeRunner ile kosuldu. Ham cikti: .calisma/T172/tur2-k8-ham.txt.

| kombinasyon | hedef MB | plan (videoK/audioK/notlar) | basarili mi | gercek MB | hedefi asti mi |
|---|---|---|---|---|---|
| 1dk/0,3MB  | 0,3 | videoK=16(->14) audioK=24 mono, notlar iceriyor: TargetBelowCodecFloor | Evet (2 deneme) | 0,29 | Hayir |
| 2dk/0,8MB  | 0,8 | videoK=29(->26) audioK=24 mono, notlar iceriyor: TargetBelowCodecFloor | Evet (2 deneme) | 0,771 | Hayir |
| 5dk/2MB    | 2   | videoK=29(->26) audioK=24 mono, notlar iceriyor: TargetBelowCodecFloor | Evet (2 deneme) | 1,927 | Hayir |
| 10dk/5MB   | 5   | videoK=43(->40) audioK=24 mono, notlar iceriyor: TargetBelowCodecFloor | Evet (2 deneme) | 4,855 | Hayir |
| 10dk/8MB   | 8   | videoK=83(->78) audioK=24 mono, (floor notu yok, kontrol grubu) | Evet (2 deneme) | 7,712 | Hayir |

Kendi saydim: 5 kombinasyonun 5'i de basarili dosya teslim etti, 5'i de hedefi asmadi. ffprobe
ile dogrulanan ses akisi (ham: .calisma/T172/tur2-k8-ffprobe.txt):

tur2-1dk-03.mp4:  codec_type=audio codec_name=aac channels=1
tur2-2dk-08.mp4:  codec_type=audio codec_name=aac channels=1
tur2-5dk-2.mp4:   codec_type=audio codec_name=aac channels=1
tur2-10dk-5.mp4:  codec_type=audio codec_name=aac channels=1
tur2-10dk-8.mp4:  codec_type=audio codec_name=aac channels=1

5 dosyanin 5'inde de ses akisi var. K8 ve K3 (hedef hala tutuyor) bu turda YESIL - tur1'in
bakiyeye biraktigi kirmizi, SearchLayout'un gercek butceyle calismasi (floor kaldirma, satir
391/545) sonucu duzeldi.

**Duzeltme (tur 3, denetim KRITIK 1):** yukaridaki tablonun "(->N)" oklari K8 ham
ciktisindaki (`.calisma/T172/tur2-k8-ham.txt`) `[deneme 2]` satirlarindan geliyor ve
gercekte `PlanCalculator.cs:988` `Correct()` yeniden-deneme kolundan gecer - onceki tur
bu kolu envanterde hic saymamis ve duzeltmeyi tumuyle 391/545'e yazmisti. Kendi saydim:
5 kombinasyonun **5'i de 2. denemede** basarili oldu (`denemeler=2`) ve o denemelerin
videoK'lari sirasiyla **14, 26, 26, 40, 78** - dordu (14/26/26/40) 48'in altinda. Eski
`MinVideoBitrateK` floor'u 988'de dursaydi bu 2. deneme de 48'e cakilir, hedefi asar,
K8'in "5/5 basarili" sonucu tutmazdi.

K4 (sessiz kaynak) bu turda da dogrulandi: .calisma/T172/tur2-k4-ham.txt - sessiz-kaynak.mkv,
hedef 0,4MB, audioK=0 audioCodec=null, notlarda Audio* kod yok (4 not, hicbiri Audio* degil),
basarili=True, ciktiMB=0,39. ffprobe (.calisma/T172/tur2-k4-ffprobe.txt): sadece
codec_type=video - ses akisi yok, beklenen, degismedi.

### K3 - uc sutunlu ozet (ESKI / TUR 1 / TUR 2)

| kombinasyon | hedef MB | ESKI (AudioDropped var): gercek MB / basari / ses | TUR 1 (AudioDropped yok, floor 48 sabit): gercek MB / basari / ses | TUR 2 (floor 391/545'te kaldirildi): gercek MB / basari / ses |
|---|---|---|---|---|
| 1dk/0,3MB  | 0,3 | 0,342 / **basarisiz** / - (dosya yok, duzeltme: `.calisma/T172/eski-k2k3.txt` ilk blok "no file was written" diyor, onceki surum bu hucreye yanlislikla "basarili/ses YOK" yazmisti) | 0,547 / basarisiz / - (dosya yok) | 0,29 / basarili / ses VAR (aac, mono) |
| 2dk/0,8MB  | 0,8 | 0,762 / basarili / ses YOK | 1,105 / basarisiz / - (dosya yok) | 0,771 / basarili / ses VAR (aac, mono) |
| 5dk/2MB    | 2   | 1,95  / basarili / ses YOK | 2,787 / basarisiz / - (dosya yok) | 1,927 / basarili / ses VAR (aac, mono) |
| 10dk/5MB   | 5   | 4,962 / basarili / ses YOK | 5,587 / basarisiz / - (dosya yok) | 4,855 / basarili / ses VAR (aac, mono) |
| 10dk/8MB   | 8   | 7,71  / basarili / ses VAR (aac, mono) | 7,71  / basarili / ses VAR (aac, mono) | 7,712 / basarili / ses VAR (aac, mono) |

Kendi saydim: TUR 1 sutununda 5 kombinasyonun 4'unde basarisizlik (dosya yok); TUR 2 sutununda
5 kombinasyonun 5'inde basari, hedefin altinda, ses akisiyla. K3 tur1'in biraktigi kirmizidan
bu turda yesile gecti.

### K9 - ManualOverrideTests golden guncellemesi

ManualOverrideTests.K1_VarsayilanPlanGoldenDegerleriyleBirebirAyni teorisinin 5 satirindan biri
(1920x1080@30, 600s, 6MB hedef) tur1'in K3 bulgusunda kirmizi kalmisti (dosya owns disinda
kaldigi icin tur1 duzeltmedi, bildirdi). Bu tur bu gorevin metninde acikca kapsama alinan
tests/VidShrink.Tests/ManualOverrideTests.cs dosyasindaki satirin golden degeri guncellendi:

- eski beklenti (9b092e9/T165-oncesi): libsvtav1|2pass|80k|crf=-|690x388@30|ses 0k/kaynak|preset 6
- yeni dogru davranis (T172): libsvtav1|2pass|56k|crf=-|614x346@15|ses 24k/1|preset 6

Degisen davranis tek satirla: ses artik otomatik dusurulmedigi (24k mono) icin video butcesi
kuculdu, arama daha kucuk cozunurluk (690x388->614x346) ve daha dusuk kare hizi (30->15) secti
- bu, T172'nin motor kararinin (ses her zaman var, video butcesinden dusulur) dogrudan sonucu,
kod geriye sarilmadi. InlineData satiri: tests/VidShrink.Tests/ManualOverrideTests.cs:73.

### K10 - Nihai verify (uc kol, sifir bulan yok)

--list-tests ile dogrulandi:

| verify filtresi | test sayisi | ham |
|---|---|---|
| PlanCalculatorTests\|SesTabaniTests\|ManualOverrideTests | **tur 2 anindaki sayim** 122 | .calisma/T172/tur2-k7-liste1.txt |
| OluUyeTests\|LanguageTests | 66 | .calisma/T172/tur2-k7-liste2.txt |

Calistirma sonucu:

- PlanCalculatorTests|SesTabaniTests|ManualOverrideTests: 122/122 yesil (tur1'de 121/122'ydi,
  K9'daki golden guncellemesiyle 122/122'ye cikti). Ham: .calisma/T172/tur2-final-verify1.txt.
- OluUyeTests|LanguageTests: 66/66 yesil, K5 sayimlari degismedi (uye: 162, pimlenen: 37 -
  .calisma/T172/tur2-k5-detay.txt). Ham: .calisma/T172/tur2-final-verify2.txt.

### K6 - mutasyon izgarasi (tur 2'de yeniden kosuldu)

Ayni iki mutasyon, git checkout -- ile geri alinip her defasinda dotnet build -c Release
--no-incremental ile derlendi:

| mutasyon | ham cikti | kirilan olcu |
|---|---|---|
| (a) 24 kbps tabani -> 0 (Math.Max(24, audioK) -> Math.Max(0, audioK)) | .calisma/T172/tur2-k6-mutasyonA.txt | SesTabaniTests filtresinde 13 testten 1'i FAIL: K6_SesTabaniYirmiDortKbpsAltinaDusmez ("audioK=8, taban 24 olmali") |
| (b) dusurme kolu geri (if (audioK < 16 && totalK < 96) return (0, null); eklendi - AdviceCode.AudioDropped K5'te silindigi icin not eklenmeden) | .calisma/T172/tur2-k6-mutasyonB.txt | SesTabaniTests filtresinde 13 testten 10'u FAIL |

Her iki mutasyon da beklenen olculeri kirdi, tur1'deki sonuclarla tutarli.

### Sinir ihlali yok (tur 2)

EncodeRunner.cs, ConversionArguments.cs, FfmpegArguments.cs okunuyor, yazilmadi - mevcut
guvenlik agi (satir 549-568) zaten owns icindeki PlanCalculator.cs'de, disariya cikilmadi.
LockedCrf/LockedAudioKbps/AudioChannels elle-gecersiz-kilma kollari (satir 625 dahil)
degismedi. Mutasyonlar .calisma/T172/ disina yazilmadi, .calisma/kaynak/ okunuyor, yazilmadi.

## Tur 3 - K11/K12/K13/K14 (denetimin 4 kritik bulgusu kapatildi)

Tur 2 denetimi KALDI dedi (9 bulgu, 4 kritik) - motor dogruydu, belge kendi ham
verisiyle 4 yerde celisiyordu ve biri (KRITIK 3) duzeltilmemis bir kusuru "olculdu,
temiz" diye kapatiyordu. Bu tur o kusuru gercekten kapatti (K11), 20-120dk araligini
gercek kodlamayla dogruladi (K12), duzeltmeyi pimleyen bir olcu ekledi (K13) ve
yukaridaki K1/K3/K4/envanter/kok-neden duzeltmeleriyle belgeyi kendi verisiyle uyumlu
hale getirdi (K14).

### K11 - `505/511/520/528` dalindaki floor kapatildi

`src/VidShrink.Core/PlanCalculator.cs` satir 505, 511, 520, 528'deki
`Math.Max(..., MinVideoBitrateK)` -> `Math.Max(..., 0.0)` (391/545/980/988 icin tur1/tur2'de
kullanilan ayni yaklasim). Supurme yeniden kosuldu (`.calisma/T172/analiz/Program.cs`,
9 sure x 19 hedef = 171 satir), once/sonra:

| | ONCE (`.calisma/T172/tur2-baseline-sweep-v2.tsv`) | SONRA (`.calisma/T172/tur3-after-sweep.tsv`) |
|---|---|---|
| toplam satir | 171 | 171 |
| videoK=48'e cakili | **11** | **0** |
| hedefi asan (fark_MB>0) | 62 | 51 |

Kendi saydim (python `csv.DictReader` ile her iki dosyayi ayristirdim, `videoK==48` ve
`fark_MB>0` satirlarini tek tek listeledim): ONCE'deki 11 cakili satirin tumu ayni zamanda
hedefi asan 62'nin icinde (20-120dk, 10-50MB araliginda, en fazla +%55: 120dk/40MB
tahmini=62,109 fark=+22,109). SONRA'da cakili satir **kalmadi** (0/171) - floor'a
carpan hicbir satir yok, gerekce yazacak bir satir da yok.

Kalan 51 asan satirin **tamami** `videoK=0` ve 51'inin de not sutunu ayni iki koddan
olusuyor: `BudgetBelowCeilingTwoPass,PredictedQualityEstimated` (kendi saydim:
`awk -F'	' '$3==0 {print $8}' | sort | uniq -c` -> tek kume, 51 satir) -
bunlar K11'in kapsami disinda, tur1'de kabul edilen ses-tabani odul: hedef, ses icin
ayrilan 24 kbps taban + konteyner payindan bile kucuk (ornegin 60dk/0,5MB -> totalK~1,1,
audioK zaten 24 dahil), videoK 0'a inse bile ses payi tek basina hedefi asiyor. Bu,
"ses her zaman olmali" karari geregi kabul edilen bir odun (ses tabani video butcesinden
once garanti ediliyor); K11'in duzelttigi "video floor'a cakilma" kusuruyla ayni degil ve
30dk/12MB, 30dk/15MB gibi K11'in asagidaki (`tur2-baseline-sweep-v2.tsv`) 11 cakili
satirinin tumu SONRA'da bu 51'in disinda.

Ornek satirlar (once -> sonra, ayni sure/hedef):

```
30dk  10MB  ONCE videoK=48 tahmini=15,527 fark=+5,527   SONRA videoK=21 tahmini=9,704  fark=-0,296
60dk  15MB  ONCE videoK=48 tahmini=31,054 fark=+16,054  SONRA videoK=10 tahmini=14,665 fark=-0,335
120dk 40MB  ONCE videoK=48 tahmini=62,109 fark=+22,109  SONRA videoK=21 tahmini=38,818 fark=-1,182
```

### K12 - 20-120dk araligi gercek `EncodeRunner` ile dogrulandi

`.calisma/T172/kaynak-ses/kaynak-{30,60,120}dk.mp4`'ten oran korunarak (sure/hedef
orani ayni kalacak sekilde) 180 saniyelik kesitler alindi (`.calisma/T172/kosum/tur3-*-kesit.mp4`,
`ffmpeg -ss 0 -t 180 -c copy`) ve gercek `EncodeRunner` (`.calisma/T172/calistir/Program.cs`,
K8'in de kullandigi ayni harness) ile kosuldu:

| kombinasyon (gercek) | kesit/hedef (orani korunmus) | plan | basarili | gercekMB | hedefi asti mi | ffprobe ses |
|---|---|---|---|---|---|---|
| 30dk/10MB | 180s/1,0MB | videoK=21(->18) audioK=24 mono; notlarin **tamami** (8): CodecUpgradeRecommended,ExtremeRatioWarning,AudioReduced,AudioMono,FrameRateCutForFloor,ResolutionReduced,FrameRateReduced,TargetEnforcedTwoPass | Evet (2 deneme) | 0,984 | Hayir | codec_type=audio codec_name=aac channels=1 |
| 60dk/15MB | 180s/0,75MB | videoK=9(->7) audioK=24 mono; notlarin **tamami** (8): CodecUpgradeRecommended,ExtremeRatioWarning,AudioReduced,AudioMono,TargetBelowCodecFloor,ResolutionReduced,FrameRateReduced,TargetEnforcedTwoPass | Evet (2 deneme) | 0,729 | Hayir | codec_type=audio codec_name=aac channels=1 |
| 120dk/40MB | 180s/1,0MB | videoK=21(->18) audioK=24 mono; notlarin **tamami** (8): CodecUpgradeRecommended,ExtremeRatioWarning,AudioReduced,AudioMono,FrameRateCutForFloor,ResolutionReduced,FrameRateReduced,TargetEnforcedTwoPass | Evet (2 deneme) | 0,984 | Hayir | codec_type=audio codec_name=aac channels=1 |

Ham cikti: `.calisma/T172/tur3-k12-30dk.txt`, `tur3-k12-60dk.txt`, `tur3-k12-120dk.txt`,
ffprobe: `.calisma/T172/tur3-k12-ffprobe.txt`. Kendi saydim: 3 kosumun 3'unde de dosya
uretildi, 3'unde de ses akisi var, 3'unde de gercekMB hedefin altinda.

**Durustce bildiriyorum:** bu 3 gercek kosum, K11'in duzelttigi `505/511/520/528`
("butce comert" / CRF tavanina takilan) dalina degil, `TargetEnforcedTwoPass` dalina
(satir 545, tur1'de zaten duzeltilmisti) girdi - gercek test icerigi bu bitrate
araliginda transparanlik tavanina degil once dogrudan iki-pasa dusuyor. K11'in ozel
dalinin kendisi (505/511/520/528), sadece analiz-tabanli 171 satirlik supurmede (yukarida)
sayisal olarak dogrulandi; K12 gercek kodlamada bu spesifik dali tetiklemedi, ama
sozlesmenin K12 CHECK'i (dosya var, ffprobe'da ses var, hedefi asmiyor) 3 kosumun
3'unde de karsilandi.

### K13 - Floor kaldirmayi pimleyen olcu eklendi

`tests/VidShrink.Tests/SesTabaniTests.cs`'e `K11_UzunKaynaktaKucukHedefVideoBitrateFloorunaCakilmiyor`
eklendi: 30dk/10MB icin `result.Estimate.ExpectedMb <= 10.5` bekliyor (floor donerse
tahmini ~15,5 MB'a cikiyor). Mutasyon: 505/511/520/528'deki `Math.Max(..., 0.0)` ->
`Math.Max(..., MinVideoBitrateK)` geri koyuldu (`dotnet build -c Release --no-incremental`
sonrasi), test kosuldu, `git checkout --` ile geri alindi (`git diff HEAD --stat` sadece
`SesTabaniTests.cs`i gosterdi, `PlanCalculator.cs` temiz).

Ham cikti (`.calisma/T172/tur3-k13-mutasyon.txt`):

```
[xUnit.net]     VidShrink.Tests.SesTabaniTests.K11_BuceTavanaCakiliKalanDalHedefiAsmaz [FAIL]
Hata Iletisi: tahminiMB=15,527 hedefi asti (30dk/10MB) - MinVideoBitrateK florou
505/511/520/528'e geri donduyse videoK 48'e cakilir ve tahmini ~15,5 MB'a cikar
Basarisiz! - Basarisiz: 1, Basarili: 122, Toplam: 123
```

Mutasyondan once 123/123 yesildi; mutasyonla tam olcunun kendisi kirildi, baska hicbir
olcu etkilenmedi. (Yukaridaki blok tur 3'un ham ciktisidir ve olcunun **o gunku** adini
tasiyor; ad tur 4'te `K11_UzunKaynaktaKucukHedefVideoBitrateFloorunaCakilmiyor` oldu.)

### K14 - Belge duzeltmeleri (bu tur)

| duzeltilen | eski deger | yeni deger | ham dosya |
|---|---|---|---|
| K1 sayimi | 44 | **55** (1dk=5, 2dk=11, 5dk=17, 10dk=22) | `.calisma/T172/k1-ham.txt` |
| Envanter (MinVideoBitrateK kullanimlari) | "6 kullanim, 2'si floor'suz" | tanim + 2 kalan floor (625, 868), **8 yer floor'suz** (391,505,511,520,528,545,980,988) | `git show ba705cf`, K11 diff'i, kod satirlari |
| Kok neden anlatisi | yalniz 391/545 | + `Correct()` yeniden-deneme kolu (988): 5/5 kosum 2. denemede basarili, videoK 14/26/26/40/78 | `.calisma/T172/tur2-k8-ham.txt` |
| K3 tablosu ESKI hucresi (1dk/0,3MB) | "basarili / ses YOK" | **basarisiz / dosya yok** | `.calisma/T172/eski-k2k3.txt` (ilk blok) |
| K4 ham blogu | kirpilmis (3 satir) | aynen (4 satir, `boyutMB` ve `[deneme 1]` dahil) | `.calisma/T172/k4-ham.txt` |
| "505/511/520/528 tetiklenmiyor" iddiasi | dogru degildi | K11'de duzeltildi, envanterde isaretli | `.calisma/T172/tur2-baseline-sweep-v2.tsv` |
| 5 bos TSV (`tur2-mut-505*.tsv`, `tur2-fix-511-520-528.tsv`, `tur2-k8ek-fixed-sweep.tsv`) | kanit gibi duruyordu, baseline ile bayt bayt ayniydi | **silindi** (`.calisma/T172/` icinden) | - |

### Borclar (gizlenmedi, tasindi)

1. **`ManualOverrideTests.cs:62-67` yorumu ve `K1_VarsayilanPlanGoldenDegerleriyleBirebirAyni`
   test adi hala yanıltici** (tur2 denetim borc bulgusu 4): yorum "beklenen degerler
   uydurulmadi, 9b092e9 agacinda ayni bes bilesim kosuldu" diyor ama dorduncu InlineData
   satiri artik T172 sonrasi motordan geliyor (K9'da guncellendi); test adi da "T165
   Oncesi Motorla Birebir Ayni" diyordu. **Tur 4'te kapatildi** (K18 borc 7): yorum ve
   test adi duzeltildi, yeni ad `K1_VarsayilanPlanGoldenDegerleriyleBirebirAyni`.
   Bu paragraf tur 3 anindaki hali anlatiyor.
2. K12'nin 3 gercek kosumu K11'in ozel dalini (505/511/520/528) degil, tur1'de zaten
   duzeltilmis olan `TargetEnforcedTwoPass` (545) dalini tetikledi - K11'in kendisi
   yalniz analiz-tabanli 171 satirlik supurmede sayisal olarak dogrulandi, gercek
   kodlamada degil. Yukarida (K12) acikca yazildi, saklanmadi.

---

# Tur 4 - denetim bulgularinin kapatilmasi (K15-K18)

Tur 3 denetcisi tek bir agir bulgu birakti: `videoK=0` uretilen planlar ffmpeg'e
kosturulamayan bir komut veriyordu. Bu bolum K15-K18'in olcumleridir.

## K15 - `videoK=0` plani kaldirildi

### Secilen yol ve gerekcesi

Sozlesme iki yol birakmisti: (a) kodlayicinin kosabildigi bir tabana yukseltmek ve plana
not dusmek, (b) plani tumden reddetmek. **(a) secildi.** Gerekce:

- (b) reddi uygulamak icin denetim akisinin `EncodeRunner` ya da `MainWindow` tarafinda
  kesilmesi gerekirdi; ikisi de bu sozlesmenin `owns` listesi disinda, salt okunur.
- (a) duzeltmeyi tumuyle `PlanCalculator.cs` icinde tutuyor. Taban yukseltildikten sonra
  `EncodeRunner`'in **zaten var olan** "hedefin uzerinde kaldi" dali devreye giriyor ve
  kullaniciya durust bir cumle ile donuyor (K16'da ham cikti ile gosterildi). Yani (b)'nin
  istedigi sonuc - kullaniciya durust cumle - (a) ile de aliniyor, kod sinirini asmadan.

### Taban neden sabit degil, olculdu

Ilk deneme sabit bir alt sinirdi (mutlak 8k, sonra 2k). **2k gercek kodlamada patladi:**
640x360@24 icin ffmpeg `requested bitrate is too low. estimated minimum is 3 kbps` dedi
(`-542398533`). libx264'un alt siniri cozunurluk ve fps'e bagli. 12 noktali izgara
olculdu - her nokta icin ffmpeg'in gercekten actigi en kucuk `-b:v`:

```
$ cat .calisma/T172/tur4/x264-taban.tsv
genislik	yukseklik	fps	mbps	enKucukKosanK
230	130	6	810	1
320	180	12	2880	2
384	216	10	3360	2
480	270	15	7650	2
640	360	24	22080	4
640	360	30	27600	5
854	480	30	48600	5
1280	720	30	108000	8
1920	1080	30	244800	12
1920	1080	60	489600	25
2560	1440	30	432000	20
3840	2160	60	1944000	80
```

`mbps` = saniyedeki makroblok = `ceil(w/16) * ceil(h/16) * fps`. Egri ne sabit ne saf
dogrusal: kucuk yerlesimlerde sabit terim, buyuklerde dogrusal terim baskin. Modelin
`PlanCalculator.RunnableVideoBitrateK`'de aldigi hal:

```
ceil(mbps * 0.05 / 1000) + 4
```

12 noktanin **hepsinde** olculen degerin uzerinde kaliyor. En dar pay 640x360@30'da:
model 6k, olculen 5k. Dogru sabit yok, bu bir emniyet payli ust zarf.

Ikinci sifir sabit noktasi `Correct()` yeniden-deneme kolundaydi (`Math.Max(0, ...)`);
o da ayni tabana baglandi.

### Once/sonra supurme

Ayni 171 satirlik izgara, tur 3 motoru ve tur 4 motoru:

- ham tur 3: `.calisma/T172/tur3-after-sweep.tsv`
- ham tur 4: `.calisma/T172/tur4/tur4-after-sweep.tsv`
- ham tur 4 (genislik/yukseklik/fps/mod/runnableK/notlar sutunlu): `.calisma/T172/tur4/tur4-detay-sweep.tsv`

Kendi saydigim ham cikti:

```
tur3 satir: 171 tur4 satir: 171
tur3 videoK==0: 51  tur4 videoK==0: 0
tur3 fark>0: 51  tur4 fark>0: 57
yeni asan: 6
  5 dk / 1 MB : tur3 videoK=3 fark=-0,03 -> tur4 videoK=5 fark=0,042
  10 dk / 2 MB : tur3 videoK=3 fark=-0,059 -> tur4 videoK=5 fark=0,085
  20 dk / 4 MB : tur3 videoK=3 fark=-0,118 -> tur4 videoK=5 fark=0,169
  30 dk / 6 MB : tur3 videoK=3 fark=-0,177 -> tur4 videoK=5 fark=0,254
  60 dk / 12 MB : tur3 videoK=3 fark=-0,355 -> tur4 videoK=5 fark=0,508
  120 dk / 25 MB : tur3 videoK=4 fark=-0,847 -> tur4 videoK=5 fark=0,016
asandan altina donen: 0
tur4 videoK==runnableK: 57
bunlardan TargetBelowCodecFloor tasimayan: 0
tur3'te sifir olan 51 satirin tur4'te TBCF tasimayani: 0
```

**51 -> 0.** Sifir plan kalmadi. Tabana cakilan 57 satirin **hepsi**
`AdviceCode.TargetBelowCodecFloor` tasiyor (yukarida sayildi: TBCF tasimayan 0).

**6 satir "hedefin altinda" tahminden "hedefin ustunde" tahmine gecti** - saklamiyorum,
olctum. Bunlar teslimat gerilemesi degil, tahmin duzeltmesi: 5dk/1MB satiri gercek
kodlamada zaten hedefi tutmuyordu. **Gercek kodlama yalniz o bir satir icin var**;
kalan bes satir icin genelleme, tur 4 denetiminin `main` karsilastirmasiyla ayrica
dogrulandi (main'e gore kotulesen satir 0, iyilesen 14).

```
$ cat .calisma/T172/tur4/k15-gerileme-5dk-1mb.txt
[probe] sure=300,01s hasAudio=True kanal=2 audioBps=127972 boyutMB=50,64
[plan] codec=libx264 mode=2pass yerlesim=218x122@6 runnableK=5 videoK=5 audioK=24 audioCodec=aac kanal=1 regime=Extreme notlar=CodecUpgradeRecommended,ExtremeRatioWarning,AudioReduced,AudioMono,TargetBelowCodecFloor,ResolutionReduced,FrameRateReduced,TargetEnforcedTwoPass
[encode] basarili=False ciktiMB=1,14 denemeler=3 hata=Stayed over the 1 MB target after 3 attempts (last result: 1,1 MB); no file was written.
[deneme 1] over ceiling hedefMB=0,96 gercekMB=1,14 videoK=5 mod=2pass
[deneme 2] over ceiling hedefMB=0,96 gercekMB=1,14 videoK=5 mod=2pass
[deneme 3] over ceiling hedefMB=0,96 gercekMB=1,14 videoK=5 mod=2pass
```

Ayni satirin tur3 plani (218x122@6, `-b:v 3k`, 24k mono AAC, 300 s) elle ffmpeg'e
kosuldugunda **1,067 MB** verdi - o da hedefin ustunde. Yani tur3'un `-0,03` tahmini
yanlisti; tur4'un `+0,042`'si dogru yone dondu. Ters yonde hareket eden satir yok
(`asandan altina donen: 0`).

## K16 - gercek kodlama kosumlari

Iki kosum, ikisi de eski `videoK=0` bandindan, sure/hedef orani korunarak kisaltildi.
**120dk/20MB orani gercek kodlamayla kosulmadi** - K16'nin "en az biri 60dk/10MB ya da
120dk/20MB" sarti kosum 1 ile karsilaniyor:

| kosum | oran | kaynak | hedef |
| --- | --- | --- | --- |
| 1 | 60dk / 10MB (Discord) | `kesit-60dk-orani.mp4` (120 s) | 0,33 MB |
| 2 | 20dk / 3MB | `kesit-20dk-orani.mp4` (120 s) | 0,30 MB |

### Onceki hal (tur 3 motoru, ayni kesit)

```
$ head -3 .calisma/T172/tur4/k16-tur3-oncesi-60dk.txt
[probe] sure=120s hasAudio=True kanal=2 audioBps=127982 boyutMB=20,25
[plan] codec=libx264 mode=2pass yerlesim=640x360@24 videoK=0 audioK=24 ... notlar=...,TargetBelowCodecFloor,TargetEnforcedTwoPass
Unhandled exception. System.InvalidOperationException: ffmpeg failed (-542398533):
...
[libx264 @ ...] CRF/CQP is incompatible with 2pass.
[vost#0:0/libx264 @ ...] Task finished with error code: -22 (Invalid argument)
[out#0/mp4 @ ...] Nothing was written into output file, because at least one of its streams received no packets.
frame=    0 fps=0.0 q=0.0 Lsize=       0KiB
Conversion failed!
```

Ayni sey 20dk kesitinde de: `.calisma/T172/tur4/k16-tur3-oncesi-20dk.txt`. Yani denetcinin
tarifi birebir dogrulandi - **islenmemis istisna, 0 baytlik dosya**.

### Sonraki hal (tur 4 motoru)

```
$ cat .calisma/T172/tur4/k16-kosum1.txt
[probe] sure=120s hasAudio=True kanal=2 audioBps=127982 boyutMB=20,25
[plan] codec=libx264 mode=2pass yerlesim=640x360@24 runnableK=6 videoK=6 audioK=24 audioCodec=aac kanal=1 regime=Extreme notlar=CodecUpgradeRecommended,ExtremeRatioWarning,AudioReduced,AudioMono,TargetBelowCodecFloor,TargetEnforcedTwoPass
[encode] basarili=False ciktiMB=0,517 denemeler=3 hata=Stayed over the 0,33 MB target after 3 attempts (last result: 0,5 MB); no file was written.
[deneme 1] over ceiling hedefMB=0,32 gercekMB=0,516 videoK=6 mod=2pass
[deneme 2] over ceiling hedefMB=0,32 gercekMB=0,515 videoK=6 mod=2pass
[deneme 3] over ceiling hedefMB=0,32 gercekMB=0,517 videoK=6 mod=2pass

$ cat .calisma/T172/tur4/k16-kosum2.txt
[probe] sure=120s hasAudio=True kanal=2 audioBps=127982 boyutMB=20,25
[plan] codec=libx264 mode=2pass yerlesim=640x360@24 runnableK=6 videoK=6 audioK=24 audioCodec=aac kanal=1 regime=Extreme notlar=CodecUpgradeRecommended,ExtremeRatioWarning,AudioReduced,AudioMono,TargetBelowCodecFloor,TargetEnforcedTwoPass
[encode] basarili=False ciktiMB=0,515 denemeler=3 hata=Stayed over the 0,3 MB target after 3 attempts (last result: 0,5 MB); no file was written.
[deneme 1] over ceiling hedefMB=0,288 gercekMB=0,516 videoK=6 mod=2pass
[deneme 2] over ceiling hedefMB=0,288 gercekMB=0,515 videoK=6 mod=2pass
[deneme 3] over ceiling hedefMB=0,288 gercekMB=0,515 videoK=6 mod=2pass
```

Sozlesmenin kabul ettigi iki sonuctan **ikincisi** gerceklesti: dosya yok, ama cikan sey
ham ffmpeg hatasi degil. **ffprobe yok cunku dosya yok** - olculmedi, olculemezdi.

### Kullaniciya giden tam metin

ffmpeg cikti uretmedigi icin sozlesme "kullaniciya giden tam metni" istiyor. Uc parca:

**1. Hata cumlesi** (`EncodeResult.Error`, kosum 1):

> Stayed over the 0,33 MB target after 3 attempts (last result: 0,5 MB); no file was written.

**2. Plan gerekcesi** (`plan.Reason`, ham cikti `.calisma/T172/tur4/k16-reason-60dk.txt`):

> no layout reaches the 0,0350 bits per pixel per frame that libx264 needs for a meaningful
> picture; the densest layout (0,0000) was taken, but this target is genuinely too small
> for this source; the budget lands near CRF 128,3, short of the CRF 20 ceiling, so
> two-pass VBR spends 0,32 MB, the band center of the 0,33 MB target; predicted quality
> -72,1/100 estimated from the source bitrate; **the budget left 0k for video, under the 6k
> the encoder still opens a two-pass run at 640x360@24; the bitrate was raised to 6k, so
> the smallest file this source can deliver with its 24k audio is about 0,43 MB against the
> 0,33 MB target**

Kalin kisim K15'te eklenen cumledir.

**3. `TargetBelowCodecFloor` ogudu** (`Locales/*/main.json:342`):

> **tr:** Bu hedef bu kaynak icin gercekten cok kucuk: hicbir duzen kodlayicinin anlamli bir
> goruntu icin gereken piksel basina bit yogunluguna ulasmiyor. En yogun duzen secildi ama
> sonuc belirgin sekilde bozuk cikacak; hedefi buyutmek tek gercek care.

> **en:** This target is genuinely too small for this source: no layout reaches the bits per
> pixel the encoder needs for a meaningful picture. The densest layout was taken, but the
> result will look visibly broken; raising the target is the only real fix.

## K17 - `videoK != 0` pimlendi

`SesTabaniTests.cs`'e iki olcu eklendi:

- `K17_SesTabaniButceyiYediginde_VideoBitrateKodlayiciTabaninaYukselir` - 7 satirlik
  Theory (300s/0,5MB, 600s/1,5MB, 1200s/3MB, 1800s/5MB, 3600s/10MB, 5400s/15MB,
  7200s/20MB). Her satirda `ModeEnum == TwoPass`, `VideoBitrateK >= RunnableVideoBitrateK(...)`
  ve notlarda `TargetBelowCodecFloor` bekleniyor.
- `K17_YenidenDenemeSifirdaCakilmiyor` - `Correct()` yeniden-deneme kolu, 3600s/10MB'de
  iki kez ust uste; ikisi de tabanin altina dusmemeli.

### Mutasyon izgarasi

Her mutasyon oncesi `dotnet build -c Release --no-incremental`.

| mutasyon | ne bozuldu | sonuc | dusen olcu |
| --- | --- | --- | --- |
| A | plandaki taban kelepcesi silindi (`videoK` yine 0 olabiliyor) | 7 basarisiz / 131 | `K17_SesTabaniButceyiYediginde_...` (7 satirin hepsi) |
| B | `Correct()` yine `Math.Max(0, ...)` | 1 basarisiz / 131 | `K17_YenidenDenemeSifirdaCakilmiyor` |

Ham cikti: `.calisma/T172/tur4/k17-mutasyonA.txt`, `k17-mutasyonB.txt`.

```
$ cat .calisma/T172/tur4/k17-mutasyonA-adlar.txt
  Basarisiz VidShrink.Tests.SesTabaniTests.K17_SesTabaniButceyiYediginde_VideoBitrateKodlayiciTabaninaYukselir(durationSeconds: 5400, targetMb: 15) [1 ms]
  Basarisiz VidShrink.Tests.SesTabaniTests.K17_SesTabaniButceyiYediginde_VideoBitrateKodlayiciTabaninaYukselir(durationSeconds: 3600, targetMb: 10) [< 1 ms]
  Basarisiz VidShrink.Tests.SesTabaniTests.K17_SesTabaniButceyiYediginde_VideoBitrateKodlayiciTabaninaYukselir(durationSeconds: 300, targetMb: 0,5) [< 1 ms]
  Basarisiz VidShrink.Tests.SesTabaniTests.K17_SesTabaniButceyiYediginde_VideoBitrateKodlayiciTabaninaYukselir(durationSeconds: 7200, targetMb: 20) [< 1 ms]
  Basarisiz VidShrink.Tests.SesTabaniTests.K17_SesTabaniButceyiYediginde_VideoBitrateKodlayiciTabaninaYukselir(durationSeconds: 600, targetMb: 1,5) [< 1 ms]
  Basarisiz VidShrink.Tests.SesTabaniTests.K17_SesTabaniButceyiYediginde_VideoBitrateKodlayiciTabaninaYukselir(durationSeconds: 1800, targetMb: 5) [< 1 ms]
  Basarisiz VidShrink.Tests.SesTabaniTests.K17_SesTabaniButceyiYediginde_VideoBitrateKodlayiciTabaninaYukselir(durationSeconds: 1200, targetMb: 3) [< 1 ms]
```

(Ham dosyada "Başarısız" Turkce yaziliyor; buraya ASCII'ye cevrilerek alindi, satir sayisi
ve adlar aynen.) Yedi ad, hepsi K17 - kendim saydim, listesi yukarida.

```
$ cat .calisma/T172/tur4/k17-mutasyonB.txt
  Basarisiz VidShrink.Tests.SesTabaniTests.K17_YenidenDenemeSifirdaCakilmiyor [< 1 ms]
  Hata Iletisi:
   1. duzeltme videoK=0, taban 17k
```

**Sozlesmenin sordugu soru:** mevcut `K11_...` olcusu bunu zaten kapsiyor mu?
**Hayir.** Iki mutasyonda da K11 yesil kaldi - A'da dusen 7 adin, B'de dusen 1 adin
hicbiri K11 degil (yukaridaki ad listeleri). K11 tavana cakili dalin hedefi asmadigina
bakiyor, video bitrate'inin sifir olup olmadigina degil.

## K18 - tur 3'un sekiz borcu

| # | borc | ne yapildi |
| --- | --- | --- |
| 1 | K11 dal atfi fazla kesin | **metin degistirilmedi** - borcun ozu tur 3'te zaten `:381-388` ve `:433-436`te aciktan yaziliydi (K11 yalniz supurmede dogrulandi, gercek kodlama baska dali tetikledi). Tur 4'te yalniz borc 2'nin `30-120` -> `20-120` duzeltmesi girdi |
| 2 | "30-120dk" yanlis | **yerinde duzeltildi** -> "20-120dk". Sayildi: 11 `videoK==48` satiri 20dk/10MB ile 120dk/50MB arasinda |
| 3 | "tek notu `BudgetBelowCeilingTwoPass`" eksik | **yerinde duzeltildi** - not sutunu iki koda cikti (`BudgetBelowCeilingTwoPass,PredictedQualityEstimated`), altina `uniq -c` oz-sayimi kondu: 51 satirda tek ayrik kume |
| 4 | K12 tablosunun `notlar:` sutunu eksik | **yerinde duzeltildi** - her satir icin ham dosyadaki **8** ogut kodunun tamami yazildi |
| 5 | `122` sayisi guncelligini yitirmis | **yerinde etiketlendi** - iki gecis "tur 1 anindaki sayim" / "tur 2 anindaki sayim" olarak isaretlendi, guncel sayim icin bu bolume yonlendirildi |
| 6 | K11 test adi yaniltici | ad degistirildi: `K11_BuceTavanaCakiliKalanDalHedefiAsmaz` -> `K11_UzunKaynaktaKucukHedefVideoBitrateFloorunaCakilmiyor`. Belgedeki **duz metin** referanslar guncellendi; tur 3'un kod blogu icindeki **ham cikti eski adi koruyor** (kanit degistirilmez), altina adin tur 4'te degistigi notu dusuldu |
| 7 | `ManualOverrideTests.cs` yorumu/adi yaniltici | duzeltildi - yorum artik "bes InlineData satirinin dordu 9b092e9'dan, dorduncusu (1920x1080@30, 600s, 6MB) T172 motorundan, T172 tur2 K9'da guncellendi" diyor; test adi `K1_VarsayilanT165OncesiMotorlaBirebirAyni` -> `K1_VarsayilanPlanGoldenDegerleriyleBirebirAyni` |
| 8 | `PlanCalculator.cs:868` tutarsizligi | **kapatilmadi - acik borc.** Olculdu, asagida |

### Borc 8 - olculdu, kapatilmadi

`QualityFloorTargetMb = SizeMb(MinVideoBitrateK, 0, dur)` hala **0 kbps ses** varsayiyor;
ses tabani geldikten sonra 0 kbps ses imkansiz. Ayrica `MinVideoBitrateK = 48` artik
motorun gercek video tabani degil (o `RunnableVideoBitrateK`, cozunurluge bagli).
Olculen fark (1920x1080@30 kaynak, gercek taban `SizeMb(17, 24, dur)`):

```
300 s: QualityFloorTargetMb(=SizeMb(48,0)) = 1,725 MB, gercek taban SizeMb(17,24) = 1,474 MB, fark -0,252
600 s: QualityFloorTargetMb(=SizeMb(48,0)) = 3,450 MB, gercek taban SizeMb(17,24) = 2,947 MB, fark -0,503
3600 s: QualityFloorTargetMb(=SizeMb(48,0)) = 20,703 MB, gercek taban SizeMb(17,24) = 17,684 MB, fark -3,019
```

**Denetcinin bekledigi yon ters cikti:** beyan edilen taban gercek tabanin **altinda**
degil, **ustunde** (0,25-3,02 MB). Yani kalite-hedefli tarama gereginden ihtiyatli -
gercekten teslim edilebilir 1,47-20,70 MB araligini hic incelemiyor. Bu bir dogruluk
deligi degil, bir korluk. Sozlesmenin K maddeleri bu satiri kapsamadigi icin
**dokunulmadi**; takip gerekir.

### Borc 7'nin yan etkisi - `owns` disinda kalan referanslar

`K1_VarsayilanT165OncesiMotorlaBirebirAyni` adi bu sozlesmenin `owns` listesi disindaki
iki dosyada gecmeye devam ediyor, elleyemedim:

```
docs/olcumler/elle-gecersiz-kilma.md:87,660,661,731,737,768   (6 gecis)
tools/VidShrink.PlanBaseline/AGENTS.md:3                       (1 gecis)
```

Kendim saydim: 2 dosya, 7 satir gecisi. **Acik borc, takip gerekir.**

## Dogrulama

Iki `verify` kolu, once `--list-tests` ile sayildi, sonra kosuldu:

```
$ dotnet test -c Release --no-build --filter "PlanCalculatorTests|SesTabaniTests|ManualOverrideTests" --list-tests | grep -c "^    "
131
$ dotnet test -c Release --no-build --filter "OluUyeTests|LanguageTests" --list-tests | grep -c "^    "
66
$ dotnet test -c Release --no-build --filter "PlanCalculatorTests|SesTabaniTests|ManualOverrideTests"
Basarili!  - Basarisiz: 0, Basarili: 131, Atlanan: 0, Toplam: 131, Sure: 594 ms
$ dotnet test -c Release --no-build --filter "OluUyeTests|LanguageTests"
Basarili!  - Basarisiz: 0, Basarili: 66, Atlanan: 0, Toplam: 66, Sure: 6 s
```

Sifir eslesen filtre yok: her iki kol da `--list-tests`'te sifirdan buyuk sayildi
(131 ve 66) ve kosum sayilari birebir tuttu.

## Olculmeyenler

Durustluk icin: **olculmedi** diye yazilmasi gerekenler.

- K16'da **ffprobe kosulmadi** - iki kosumda da dosya uretilmedi, ffprobe'lanacak sey yoktu.
- Tabana cakilan bandin **gorsel kalitesi** olculmedi; taban "kodlayici komutu aciyor mu"
  olcusudur, "goruntu izlenebilir mi" olcusu degil. `TargetBelowCodecFloor` ogudu zaten
  "sonuc belirgin sekilde bozuk cikacak" diyor.
- Yeni asan 6 satirin **5dk/1MB disindakileri** gercek kodlamayla dogrulanmadi; yalniz
  o satir kosuldu ve tur3 planinin elle emulasyonu (1,067 MB) ile karsilastirildi.
- `RunnableVideoBitrateK` modeli **libx264 icin** olculdu. Diger kodekler (hevc, av1,
  donanim kodlayicilari) olculmedi.
