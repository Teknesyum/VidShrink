# Elle geçersiz kılma — ölçümler

T165, tur 3. Tur 1 bağımsız denetimde `verdict: failed` döndü ve belge baştan yazıldı;
tur 2 denetimi dört bulgu (F1-F4) döndürdü ve bu sürüm onları kapatıyor.

Ölçü dosyası: `tests/VidShrink.Tests/ManualOverrideTests.cs`.
Ham çıktı: `dotnet test -c Release --filter "FullyQualifiedName~ManualOverrideTests" --logger "console;verbosity=detailed"`.

Bu belgedeki ham çıktının tamamı belgenin kendi içinde; `.calisma/` altına atıf yok
(F4: `.calisma/` `.gitignore`da, dal birleşince kanıt kaybolur).

---

## K1 — Varsayılan hiçbir şeyi değiştirmiyor

Beklenen değerler bu ağaçtan değil, **T165 öncesi motordan** ölçüldü ve tur 3'te bu
ölçüm gerçekten koşuldu (F3: tur 2'de iddia doğruydu ama gösterilen kanıt aynı süreçten
çıkıyordu, iki motoru ayırt edemiyordu).

Taban koşumu şöyle üretildi — depoya girmeyen, tek kullanımlık bir düzenek:

```
git archive 9b092e9 src/VidShrink.Core > taban.tar     # T165'in ebeveyni, sözleşme öncesi motor
tar -xf taban.tar -C <ayri-dizin>                      # yalnız Core; bağımlılığı yok
# <ayri-dizin>/harness: net8.0 konsol projesi, VidShrink.Core'a ProjectReference,
# aynı beş bileşimi FakeAvailability(hepsi Working) ile koşup tek satır basıyor
dotnet run --project harness -c Release
```

Ayrı dizindeki ağacın gerçekten T165 öncesi olduğu doğrulandı: o `PlanOptions` içinde
`LockedCrf`, `LockedMode`, `LockedPreset`, `LockedAudioKbps`, `AudioChannels`,
`MinResolutionHeight`, `MinFps`, `EncoderPath` alanlarının **hiçbiri yok**.

Taban koşumunun ham çıktısı (T165 öncesi motor, ayrı dizin):

```
1920x1080@30 -> 25MB
  taban (9b092e9): libsvtav1|2pass|1567k|crf=-|1920x1080@30|ses 128k/kaynak|preset 6
1280x720@24 -> 8MB
  taban (9b092e9): libsvtav1|2pass|188k|crf=-|1202x676@24|ses 26k/1|preset 6
3840x2160@60 -> 50MB
  taban (9b092e9): libsvtav1|2pass|9016k|crf=-|3840x2160@60|ses 128k/kaynak|preset 6
1920x1080@30 -> 6MB
  taban (9b092e9): libsvtav1|2pass|80k|crf=-|690x388@30|ses 0k/kaynak|preset 6
1280x720@30 -> 100MB
  taban (9b092e9): libx264|2pass|27305k|crf=-|1280x720@30|ses 128k/kaynak|preset slow
```

Bu on satır `K1_VarsayilanT165OncesiMotorlaBirebirAyni`'ye `InlineData` olarak
sabitlendi. Karşılaştırma iki farklı motor arasında.

| kaynak | hedef | kodek | kip | videoK | çözünürlük | fps | ses | preset |
|---|---|---|---|---|---|---|---|---|
| 1920x1080@30, 120s | 25 MB | libsvtav1 | 2pass | 1567k | 1920x1080 | 30 | 128k / kaynak | 6 |
| 1280x720@24, 300s | 8 MB | libsvtav1 | 2pass | 188k | 1202x676 | 24 | 26k / 1 | 6 |
| 3840x2160@60, 45s | 50 MB | libsvtav1 | 2pass | 9016k | 3840x2160 | 60 | 128k / kaynak | 6 |
| 1920x1080@30, 600s | 6 MB | libsvtav1 | 2pass | 80k | 690x388 | 30 | 0k / kaynak | 6 |
| 1280x720@30, 30s | 100 MB | libx264 | 2pass | 27305k | 1280x720 | 30 | 128k / kaynak | slow |

Beş satırın hepsinde taban ve şimdiki çıktı birebir aynı — aşağısı bu ağacın koşumundan,
`taban` satırı yukarıdaki ayrı dizinden gelen değer:

```
taban (9b092e9): libsvtav1|2pass|1567k|crf=-|1920x1080@30|ses 128k/kaynak|preset 6
simdi   (T165): libsvtav1|2pass|1567k|crf=-|1920x1080@30|ses 128k/kaynak|preset 6
taban (9b092e9): libsvtav1|2pass|188k|crf=-|1202x676@24|ses 26k/1|preset 6
simdi   (T165): libsvtav1|2pass|188k|crf=-|1202x676@24|ses 26k/1|preset 6
taban (9b092e9): libsvtav1|2pass|9016k|crf=-|3840x2160@60|ses 128k/kaynak|preset 6
simdi   (T165): libsvtav1|2pass|9016k|crf=-|3840x2160@60|ses 128k/kaynak|preset 6
taban (9b092e9): libsvtav1|2pass|80k|crf=-|690x388@30|ses 0k/kaynak|preset 6
simdi   (T165): libsvtav1|2pass|80k|crf=-|690x388@30|ses 0k/kaynak|preset 6
taban (9b092e9): libx264|2pass|27305k|crf=-|1280x720@30|ses 128k/kaynak|preset slow
simdi   (T165): libx264|2pass|27305k|crf=-|1280x720@30|ses 128k/kaynak|preset slow
```

Kol ayrıca varsayılan planda hiçbir `Manual*` sebep kodunun bulunmadığını denetler.

---

## K2 — Sekiz kalem de ffmpeg komut satırına ulaşıyor

Her satır `K2_SabitlenenDegerFfmpegKomutSatirindaGorunuyor`'un bir kolu. Kol, senaryoyu
kurarken **motorun kendiliğinden ne seçtiğini** de doğruluyor: motor zaten aynı değeri
seçiyorsa senaryo bir şey kanıtlamaz, o durumda kol kurulum aşamasında düşer.

| kalem | sabitlenen | ffmpeg argümanında görünen |
|---|---|---|
| EncodeMode | TwoPass | `-b:v 353k` (ve `-crf` yok) |
| EncodeMode | Crf | `-crf 41` |
| CRF değeri | 19 | `-crf 19` |
| preset / hız | veryslow | `-preset veryslow` |
| ses hedefi | 96 kbps | `-b:a 96k` |
| ses kanalı | Stereo | `-ac 2` |
| ses kanalı | Mono | `-ac 1` |
| ses kanalı | None | `-an` |
| çözünürlük tabanı | en az 720p | `scale=1306:734` |
| kare hızı tabanı | en az 24 | `fps=24` |
| kodlayıcı yolu | Software | `-c:v libsvtav1` |
| kodlayıcı yolu | Hardware | `-c:v av1_nvenc` |

Ham komut satırları:

```
| EncodeMode | TwoPass | -b:v 353k |
ffmpeg -hide_banner -y -hwaccel auto -i kucuk.mp4 -c:v libx264 -preset slow -b:v 353k -maxrate 529k -bufsize 706k -pass 2 -g 240 -keyint_min 24 -pix_fmt yuv420p -c:a aac -b:a 96k -movflags +faststart out.mp4

| EncodeMode | Crf | -crf 41 |
ffmpeg -hide_banner -y -hwaccel auto -i sample.mp4 -vf scale=614:346:flags=lanczos -c:v libx264 -preset slow -crf 41 -maxrate 470k -bufsize 940k -g 300 -keyint_min 30 -pix_fmt yuv420p -c:a aac -b:a 32k -ac 1 -movflags +faststart out.mp4

| CRF degeri | 19 | -crf 19 |
ffmpeg -hide_banner -y -hwaccel auto -i sample.mp4 -vf scale=1612:906:flags=lanczos -c:v libx264 -preset slow -crf 19 -maxrate 58052k -bufsize 116104k -g 300 -keyint_min 30 -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart out.mp4

| preset / hiz | veryslow | -preset veryslow |
ffmpeg -hide_banner -y -hwaccel auto -i sample.mp4 -vf scale=1612:906:flags=lanczos -c:v libx264 -preset veryslow -b:v 1567k -maxrate 2350k -bufsize 3134k -pass 2 -g 300 -keyint_min 30 -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart out.mp4

| ses hedefi | 96 kbps | -b:a 96k |
ffmpeg -hide_banner -y -hwaccel auto -i sample.mp4 -vf scale=1612:906:flags=lanczos -c:v libx264 -preset slow -b:v 1599k -maxrate 2398k -bufsize 3198k -g 300 -keyint_min 30 -pix_fmt yuv420p -c:a aac -b:a 96k -movflags +faststart out.mp4

| ses kanali | Stereo | -ac 2 |
ffmpeg -hide_banner -y -hwaccel auto -i sample.mp4 -vf scale=1612:906:flags=lanczos -c:v libx264 -preset slow -b:v 1567k -maxrate 2350k -bufsize 3134k -g 300 -keyint_min 30 -pix_fmt yuv420p -c:a aac -b:a 128k -ac 2 -movflags +faststart out.mp4

| ses kanali | Mono | -ac 1 |
ffmpeg -hide_banner -y -hwaccel auto -i sample.mp4 -vf scale=1612:906:flags=lanczos -c:v libx264 -preset slow -b:v 1567k -maxrate 2350k -bufsize 3134k -g 300 -keyint_min 30 -pix_fmt yuv420p -c:a aac -b:a 128k -ac 1 -movflags +faststart out.mp4

| ses kanali | None | -an |
ffmpeg -hide_banner -y -hwaccel auto -i sample.mp4 -vf scale=1690:950:flags=lanczos -c:v libx264 -preset slow -b:v 1695k -maxrate 2542k -bufsize 3390k -g 300 -keyint_min 30 -pix_fmt yuv420p -an -movflags +faststart out.mp4

| cozunurluk tabani | en az 720p | scale=1306:734 |
ffmpeg -hide_banner -y -hwaccel auto -i sample.mp4 -vf scale=1306:734:flags=lanczos,fps=6 -c:v libsvtav1 -preset 6 -b:v 176k -pass 2 -g 60 -svtav1-params keyint=60:scd=1 -pix_fmt yuv420p -c:a aac -b:a 24k -ac 1 -movflags +faststart out.mp4

| kare hizi tabani | en az 24 | fps=24 |
ffmpeg -hide_banner -y -hwaccel auto -i sample.mp4 -vf fps=24 -c:v libsvtav1 -preset 6 -b:v 83k -pass 2 -g 240 -svtav1-params keyint=240:scd=1 -pix_fmt yuv420p -c:a aac -b:a 24k -ac 1 -movflags +faststart out.mp4

| kodlayici yolu | Software | -c:v libsvtav1 |
ffmpeg -hide_banner -y -hwaccel auto -i sample.mp4 -c:v libsvtav1 -preset 6 -b:v 1567k -g 300 -svtav1-params keyint=300:scd=1 -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart out.mp4

| kodlayici yolu | Hardware | -c:v av1_nvenc |
ffmpeg -hide_banner -y -hwaccel auto -i sample.mp4 -vf scale=806:454:flags=lanczos -c:v av1_nvenc -preset p6 -b:v 1556k -maxrate 1711k -bufsize 1867k -rc vbr -multipass fullres -g 150 -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart out.mp4
```

### D1'in iki kalemi

Tur 1'de bu iki kalem komut satırına hiç ulaşmıyordu. Bugün ikisinin de ayrı bir ölçüsü var.

**Kare hızı tabanı.** Tur 1'in senaryosunda taban 24 konunca plan kaynak fps'e (60)
sıçrıyordu; kaynak fps'te `fps=` filtresi hiç yazılmadığı için sabitlenen değer komut
satırında görünmüyordu. Senaryo, kullanıcının çözünürlük düşüşünü kapattığı hâle
çevrildi — arama fps'ten başka bir yerden bit bulamıyor, taban gerçekten bağlıyor.
`D1_KareHiziTabaniFfmpegKomutSatirindakiFpsiDegistiriyor` iki komut satırını
karşılaştırıyor:

```
tabansiz (6 fps):  ... -vf fps=6  ... -g 60  -svtav1-params keyint=60:scd=1 ...
tabanli  (24 fps): ... -vf fps=24 ... -g 240 -svtav1-params keyint=240:scd=1 ...
```

**Kodlayıcı yolu.** `D1_KodlayiciYoluFfmpegKomutSatirindakiCVyiDegistiriyor` aynı girdide
üç yolu koşuyor:

```
otomatik: -c:v libx264
yazilim : ... -c:v libx264 -preset slow -b:v 1567k ...
donanim : ... -c:v av1_nvenc -preset p6 -b:v 1556k -rc vbr -multipass fullres ...
```

`FfmpegArguments.cs` `owns` içinde ama **değiştirilmedi**: sekiz kalemin sekizi de bugünkü
argüman üreticisinden geçiyor, eksik olan şey argüman üreticisi değil ölçüydü. Bir dosyayı
sırf sahibi olduğum için değiştirmedim.

---

## K3 — CRF sabitlenince hedef boyut tahmine dönüyor

İki bağımsız ölçü. Tur 1'in `Assert.False(estimate.Enforced)` ve kalıcı doğru VEYA'sı
kaldırıldı; ikisi de düşemiyordu.

**`K3_CrfSabitlenincHedefBoyutZorlanmiyor`** — aynı kaynak, aynı hedef, tek değişken CRF:

```
hedef 25MB
  serbest: mode=2pass crf=-  960x540@30 videoK=555   tahmin=24,37MB
  crf=16 : mode=crf   crf=16 960x540@30 videoK=10961 tahmin=398,40MB
```

Geçersiz kılma yokken motor hedefi zorluyor (24,37 ≤ 25). CRF sabitken üretilen boyut
hedefin 16 katına çıkıyor — hedef artık zorlanmıyor. Plan bunu açıkça söylüyor:

```
kullanici CRF'i 16 olarak sabitledi; hedef boyut artik zorlanmiyor, 10961k yalniz bir
tahmin — motor 2pass kipinde 2pass@555k secmisti
```

**`K3_AyniCrfFarkliHedeflerdeAyniCrfiVeriyor`** — aynı CRF, iki farklı hedef:

```
serbest 25MB videoK=555 / 120MB videoK=3164
crf=22  25MB crf=22 videoK=5481 / 120MB crf=22 videoK=32097
```

Hedef CRF'i artık çekmiyor (iki hedefte de 22), ve CRF sabitken plandaki bitrate bütçeden
değil **CRF'ten** türüyor: aynı hedefte 555k yerine 5481k, 3164k yerine 32097k. Bu ikinci
karşılaştırma tur 3'te eklendi — o olmadan `plan.VideoBitrateK`'yı bütçe değerinde bırakan
bir mutasyon 55 kolun hiçbirini düşürmüyordu (F1 ile aynı sınıf; M2 satırına bak).

---

## K4 — Geçersiz kılma plan panelinde gerekçeleniyor

Tur 1'in belgesi bu iddiayı `Assert.Contains(..., n => n.Code == ...)` satırlarına
dayandırıyordu; o satırlar yalnız kodun varlığını ölçüyor, alanların dolduğunu değil.
`K4_HerKalemNotuIkiAlaniDolduruyor` on iki kolun her birinde iki alanın da dolu olduğunu
ve `EngineWouldHaveChosen`'ın gerekçe metninde geçtiğini denetliyor.

| kalem | sebep kodu | ManualOverrideValue | EngineWouldHaveChosen |
|---|---|---|---|
| EncodeMode | ManualModeOverride | TwoPass | crf |
| EncodeMode | ManualModeOverride | Crf | 2pass |
| CRF değeri | ManualCrfOverride | 19 | 2pass@1567k |
| preset / hız | ManualPresetOverride | veryslow | slow |
| ses hedefi | ManualAudioBitrateOverride | 96 | 128 |
| ses kanalı | ManualAudioChannelsOverride | Stereo | source |
| ses kanalı | ManualAudioChannelsOverride | Mono | source |
| ses kanalı | ManualAudioChannelsOverride | None | source |
| çözünürlük tabanı | ManualMinResolutionOverride | 720 | 582 |
| kare hızı tabanı | ManualMinFpsOverride | 24 | 6 |
| kodlayıcı yolu | ManualEncoderPathOverride | Software | av1_nvenc |
| kodlayıcı yolu | ManualEncoderPathOverride | Hardware | libx264 |

`K4_CrfNotuMotorunKendiSeciminiTasiyor` ayrıca notun taşıdığı motor seçimini, aynı girdiyle
geçersiz kılmasız koşulan planın gerçek çıktısıyla karşılaştırıyor — metin eşitliği değil,
iki koşunun eşitliği.

---

## D2 — Karşılanmayan donanım isteği karşılanmış gibi anlatılmıyor

Donanım kodlayıcı çalışmadığında `PickFastCodec` yazılım kodlayıcıya düşüyordu; kod yine de
"kullanıcı Hardware sabitledi" gerekçesini yazıyordu. Artık istenen yol ile seçilen
kodlayıcının ailesi karşılaştırılıyor ve tutmuyorsa ayrı bir sebep kodu
(`ManualEncoderPathUnmet`) yazılıyor.

`D2_DonanimYokkenIstekKarsilanmadiDeniyor` — üç nvenc kodlayıcısı `NotWorking`:

```
codec=libx264
gerekce: kullanici kodlayici yolunu Hardware olarak sabitledi ama bu makinede o yolda
kullanilabilir kodlayici yok; istek karsilanmadi ve libx264 ile devam ediliyor; ...
```

Kol ayrıca `ManualEncoderPathOverride`'ın (yani "istek karşılandı" kodunun) **bulunmadığını**
denetliyor. `D2_DonanimVarkenIstekKarsilandiDeniyor` tersini tutuyor: donanım varken
`ManualEncoderPathUnmet` yazılmıyor.

---

## D3 — Kopyalama yolunda geçersiz kılma sessizce düşmüyor

Kaynak zaten hedefin altındayken erken dönüş sekiz kalemden yedisinin önünde duruyordu.
İki ayrı çözüm uygulandı.

**Yeniden kodlama gerektiren beş kalem artık kopyalama yolunu kapatıyor.**
`HasReencodeOverride` doluysa `CanPassThrough` false dönüyor; kullanıcı ne istediyse onu
alıyor. `D3_KopyaYolundaYenidenKodlamaIsteyenGecersizKilmaUygulaniyor` (10 MB kaynak,
25 MB hedef — geçersiz kılmasız plan `passthrough`):

| sabitlenen | kopyasız sonuç |
|---|---|
| LockedCrf = 20 | mode=crf, crf=20 |
| LockedMode = TwoPass | mode=2pass |
| LockedPreset = veryslow | preset=veryslow |
| LockedAudioKbps = 64 | ses 64k |
| AudioChannels = Mono | kanal 1 |

**Kopya yolunda uygulanamayan üç kalem söyleniyor.**
`D3_KopyaYolundaUygulanamayanIstekSessizceDusmuyor`:

```
kodlayici-yolu:     not=kodlayici yolu=Hardware -> kopya, kodlayici hic calismiyor
cozunurluk-tabani:  not=cozunurluk tabani=2160p -> kaynagin kendi 720p'si
kare-hizi-tabani:   not=kare hizi tabani=60     -> kaynagin kendi 30 fps'i

gerekce: the source is already 10,0 MB, under the 25 MB target, so it is copied as it is
instead of being re-encoded; kullanicinin sabitledigi kodlayici yolu (Hardware) kopyalama
yolunda uygulanamadi; gecerli olan kopya, kodlayici hic calismiyor
```

---

## D4 — Etkisiz istek "sabitlendi" diye kaydedilmiyor

Taban notları koşulsuz yazılıyordu ve notun `Height`/`Fps` alanı planın gerçek değerini
değil **istenen** değeri taşıyordu. İkisi de düzeltildi: not ancak istek çıktıyı gerçekten
değiştirdiyse yazılıyor, ve `EngineWouldHaveChosen` motorun ne seçeceğinin bir tahmini
değil — aynı seçenekler tabansız koşulup çıkan planın gerçek değeri.

`D4_EtkisizTabanIstegiNotUretmiyor` — motor zaten 690x388@30 seçiyor, kullanıcı 100p+5fps
istiyor, hiçbir şey değişmiyor:

```
tabansiz 690x388@30 / istek 100p+5fps -> 690x388@30
```
Not yok: `ManualMinResolutionOverride` de `ManualMinFpsOverride` de yazılmıyor.

`D4_EtkiliTabanNotuPlaninGercekDegeriniTasiyor`:
```
motor 1036x582 -> plan 1306x734; not Height=734 deger=720 motor=582
```
Notun `Height`'ı planın gerçek yüksekliği (734), istenen taban (720) değil.

`D4_EtkiliFpsTabanNotuPlaninGercekFpsiniTasiyor`:
```
motor 6 -> plan 24; not Fps=24 deger=24 motor=6
```

---

## F2 — Kaynağın üstündeki taban isteği artık sessizce düşmüyor (ürün kusuru)

Motor hiçbir yolda yukarı ölçekleme yapmıyor: `ScaleCandidates` 1,0'dan başlayıp aşağı
iniyor, `FpsCandidates` kaynak fps'in üstüne çıkmıyor. Kullanıcı kaynağın üstünde bir
taban isterse istek **karşılanamaz**. Kopyalama yolunda bu söyleniyordu; yeniden kodlama
yolunda söylenmiyordu — `plan.Fps > enginePlan.Fps + 0.01` koşulu tutmadığı için hiçbir
not yazılmıyordu.

Üç hâl ayrıldı: plan isteğin **altında** kaldıysa `...Unmet`, planı motorun seçimini
**aşacak** şekilde çektiyse `...Override`, ikisi de değilse istek etkisiz ve hiçbir not
yazılmıyor (D4). İki yeni sebep kodu: `ManualMinResolutionUnmet`, `ManualMinFpsUnmet`.

`F2_KaynagiAsanFpsTabaniYenidenKodlamaYolundaKarsilanmadiDeniyor` — 1920x1080@30 kaynak,
`MinFps = 60`:

```
kaynak 1920x1080@30, istek MinFps=60 -> plan 1920x1080@30 kip=2pass
gerekce: ... kullanici kare hizi tabanini en az 60 olarak sabitledi ama kaynak 30 fps ve
motor kaynagin ustune cikmiyor; istek karsilanmadi, plan 30 fps ile cikiyor
```

**Aynı sorun çözünürlük tabanında da vardı** ve kendi ölçüsüyle kapatıldı.
`F2_KaynagiAsanCozunurlukTabaniYenidenKodlamaYolundaKarsilanmadiDeniyor` —
`MinResolutionHeight = 2160`:

```
kaynak 1920x1080@30, istek MinResolutionHeight=2160 -> plan 1920x1080@30 kip=2pass
gerekce: ... kullanici cozunurluk tabanini en az 2160p olarak sabitledi ama kaynak 1080p
ve motor yukari olcekleme yapmiyor; istek karsilanmadi, plan 1920x1080 ile cikiyor
```

Her iki kol da `...Override` kodunun **bulunmadığını** ayrıca denetliyor, ve her ikisi de
"motor zaten 60 fps / 2160p verebiliyorsa bu senaryo bir şey ölçmez" ön koşuluyla
başlıyor — senaryo kendiliğinden yeşile dönerse kol düşüyor.

Negatif kontrol: `F2_KarsilanabilenTabanIstegiKarsilanmadiDemiyor` — karşılanabilen iki
istek için `...Unmet` yazılmadığını tutuyor. O kol olmadan koşulsuz not yazan bir düzeltme
de yeşil görünürdü.

```
fps istegi 24 -> plan 24; cozunurluk istegi 720p -> plan 1306x734
```

Düşüren mutasyonlar: M11, M12 (aşağıdaki ızgara).

---

## F1 — Hiçbir kolun ölçmediği yer bulundu

Tur 2'de bir mutasyon hiçbir kolu düşürmeden geçti ve formülü kaydedilmedi. Tur 3'te
aday üretim noktaları tek tek koşuldu; **iki** yer bulundu ve ikisi de kapatıldı.

| bulunan yer | formül | kapatılmadan önce | kapatan kol |
|---|---|---|---|
| `PlanCalculator.EffectiveTargetMb` — `Math.Min(targetMb, sourceMb * SourceSizeCap)` | `SourceSizeCap = 0.95` → `0.80` | o andaki 54 kolun tamamı geçti **ve** plan hesabına dokunan 14 sınıfın 280 kolu 280/280 geçti | `F1_KaynakUstuHedefKaynaginYuzde95ineKirpiliyor` |
| CRF sabitken plandaki bitrate | `plan.VideoBitrateK = Math.Round(Math.Max(VideoBitrateK(bppfAtCrf, ...), MinVideoBitrateK))` → atama düşer, bütçe değeri kalır | 55 kolun tamamı geçti | `K3_AyniCrfFarkliHedeflerdeAyniCrfiVeriyor`'a eklenen üç ölçü |

Birinci kolun ham çıktısı:

```
kaynak 500 MB, hedef 490 MB -> kirpilan 475 MB (not TargetMb=490)
EffectiveTargetMb(490, 500) = 475
```

500 MB kaynak, 490 MB hedef: kopyalama yolu kapalı (kaynak hedefin üstünde) ama hedef
kaynağın %95'inin üstünde, yani kırpma kapısı tam bu aralıkta çalışıyor. Kol hem
`TargetCappedToSource` notunun taşıdığı değeri hem `EffectiveTargetMb`'nin kendisini
475'e pinliyor; oran değişirse kol düşer (M10).

**Kapatılamayan, bildirilen bir üçüncü yer var.** `LowFpsSurcharge = 12.0` → `1.0`
mutasyonu ManualOverrideTests'in 55 kolunu **ve** plan hesabına dokunan 14 sınıfın 281
kolunu düşürmüyor:

```
LowFpsSurcharge = 12.0 -> 1.0
Basarili! - Basarisiz: 0, Basarili: 281, Atlanan: 6, Toplam: 287
```

Bu sabit `FpsPenalty` içinde, 20 fps altındaki adaylara kalite cezası ekliyor
(`penalty += LowFpsSurcharge * (LowFpsThreshold - fps) / 8.0`) ve `Balanced`/`Aggressive`
rejimlerinde açık. Kalibre edilmiş bir iç sabit; T165'in kapsamı geçersiz kılma
davranışı, kalite puanlamasının kalibrasyonu değil — bu yüzden buraya kol yazmadım.
**Ölçüsüz bir üretim sabiti olarak T0'a bildiriyorum**; ölçüsü, fps seçimini ölçen
sözleşmenin işi.

---

## D5 / D6 — Üç boş ölçü ve dayanaksız kanıt

| tur 1'deki boş ölçü | yerine ne var | hangi mutasyon düşürüyor |
|---|---|---|
| K1: `before`/`after` aynı `PlanOptions` ile aynı motor | `9b092e9`den ölçülmüş sabit ızgara + varsayılanda `Manual*` kodu yok denetimi | M8, M9 |
| `K2_SekizKalemHamCiktiTablosu` gövdesi `Assert.True(true)` | tablo bir `[Theory]` oldu; her satır kendi ffmpeg argümanını denetliyor | M1, M3, M7 |
| K3: `Enforced` tanım gereği + kalıcı doğru VEYA | iki bağımsız ölçü (tahmin/hedef aşımı, aynı CRF iki hedefte) | M2 |
| K4 kanıtı olarak `Assert.Contains(n => n.Code == ...)` satırları | `K4_HerKalemNotuIkiAlaniDolduruyor` on iki kolda iki alanı da denetliyor | M3 |

Bu belgedeki her sayı ve her komut satırı yukarıdaki koşunun ham çıktısından alındı;
özetlenmedi.

---

## K5 — Kapalı kalanlar kapalı kaldı

`PlanOptions`ın açık alan kümesi pinlenmiş durumda; listeye izinsiz bir alan eklemek
`K5_PlanOptionsKapaliSabitleriDisaAcmiyor`'u düşürür.

```
PlanOptions alanlari: TargetMb, Intent, Codec, AllowResolutionDrop, AllowFpsDrop,
HdrPolicy, FillPolicy, SpeedMode, LockedCodec, LockedMode, LockedCrf, LockedPreset,
LockedAudioKbps, AudioChannels, MinResolutionHeight, MinFps, EncoderPath
FillBand: LowerMb, HardFloorMb, UpperMb, CenterMb, RelativeWidth
RegimeFloors: MinScale, MinHeight, MinFps
```

| kapalı kalacak | açılmadı kanıtı |
|---|---|
| FillBand (%92-100 / %95-100 / %97,2-100) | `FillBand` alan kümesi pinli; `PlanOptions`ta karşılığı yok |
| RegimeFloors (Aggressive, Extreme) | `RegimeFloors` alan kümesi pinli; kullanıcı tabanı yalnız `Math.Max` ile **yükseltebiliyor** |
| ses bütçe payı (%30/%25/%18/%12) | `PlanOptions`ta pay alanı yok; kullanıcı yalnız nihai kbps veriyor |
| EncoderFallback mantığı ve üç sebebi | `PlanOptions`ta alan yok; `EncoderFallbackCause` dışarıdan verilemiyor |
| retry döngüsü | `PlanOptions`ta alan yok |
| CodecModel sabitleri | `CodecModel.cs` bu sözleşmede hiç değişmedi |

---

## K6 — Mutasyon ızgarası

On iki mutasyon, her birinden önce `dotnet build -c Release --no-incremental`;
`--no-build` kullanılmadı. Ham çıktı bu bölümün sonunda, tam metniyle.

| # | mutasyon | düşen kollar (sayı) |
|---|---|---|
| M1 (K6a) sabitlenen on ayar yok sayilir | `plan.Preset = manualPreset;` -> `plan.Preset = enginePreset;` | D3_KopyaYolundaYenidenKodlamaIsteyenGecersizKilmaUygulaniyor, K2_SabitlenenDegerFfmpegKomutSatirindaGorunuyor (2) |
| M2 (K6b) CRF sabitken bitrate yine butceden gelir | `plan.VideoBitrateK = (int)Math.Round(Math.Max(VideoBitrateK(bppfAtCrf, best.Width, best.Height, best.Fps), MinVideoBitrateK));` -> `plan.VideoBitrateK = plan.VideoBitrateK;` | K3_AyniCrfFarkliHedeflerdeAyniCrfiVeriyor (1) |
| M3 (K6c) on ayar gerekcesi uretilmez | `ReasonCode.ManualPresetOverride` -> `ReasonCode.PredictedQualityEstimated` | K2_SabitlenenDegerFfmpegKomutSatirindaGorunuyor, K4_HerKalemNotuIkiAlaniDolduruyor (2) |
| M4 (D2) karsilanmayan donanim istegi susar | `ReasonCode.ManualEncoderPathUnmet` -> `ReasonCode.ManualEncoderPathOverride` | D2_DonanimYokkenIstekKarsilanmadiDeniyor (1) |
| M5 (D3) kopya yolu yeniden kodlama istegini yine yutar | `if (HasReencodeOverride(options)) return false;` -> `if (HasReencodeOverride(options) && info.Width < 0) return false;` | D3_KopyaYolundaYenidenKodlamaIsteyenGecersizKilmaUygulaniyor (5) |
| M6 (D4) etkisiz cozunurluk istegi yine not uretir | `else if (plan.Height > enginePlan.Height)` -> `else if (plan.Height > 0)` | D4_EtkisizTabanIstegiNotUretmiyor (1) |
| M7 (D1) kullanicinin fps tabani yok sayilir | `var minFps = options.MinFps is double f ? Math.Max(floors.MinFps, f) : floors.MinFps;` -> `var minFps = floors.MinFps;` | D1_KareHiziTabaniFfmpegKomutSatirindakiFpsiDegistiriyor, D4_EtkiliFpsTabanNotuPlaninGercekFpsiniTasiyor, F2_KarsilanabilenTabanIstegiKarsilanmadiDemiyor, K2_SabitlenenDegerFfmpegKomutSatirindaGorunuyor, K4_HerKalemNotuIkiAlaniDolduruyor (5) |
| M8 (K1) ses kanali gecersiz kilmasi varsayilana sizar | `if (info.HasAudio && options.AudioChannels != AudioChannelOverride.Auto)` -> `if (info.HasAudio)` | K1_VarsayilanT165OncesiMotorlaBirebirAyni (5) |
| M9 (K1) varsayilan bitrate hesabi degisir | `totalK * ContainerOverhead - audioK` -> `totalK - audioK` | K1_VarsayilanT165OncesiMotorlaBirebirAyni (5) |
| M10 (F1) kaynak ustu hedef kirpma orani degisir | `SourceSizeCap = 0.95` -> `SourceSizeCap = 0.80` | F1_KaynakUstuHedefKaynaginYuzde95ineKirpiliyor (1) |
| M11 (F2) karsilanmayan fps tabani susar | `if (plan.Fps < requestedMinFps - 0.01)` -> `if (plan.Fps < 0)` | F2_KaynagiAsanFpsTabaniYenidenKodlamaYolundaKarsilanmadiDeniyor (1) |
| M12 (F2) karsilanmayan cozunurluk tabani susar | `if (plan.Height < requestedMinHeight)` -> `if (plan.Height < 0)` | F2_KaynagiAsanCozunurlukTabaniYenidenKodlamaYolundaKarsilanmadiDeniyor (1) |

On ikisinin de en az bir kolu düşüyor. Ham çıktı:

```
### M1 (K6a) sabitlenen on ayar yok sayilir
plan.Preset = manualPreset;
  ->  plan.Preset = enginePreset;
Basarisiz! - Basarisiz:     2, Basarili:    53, Atlanan:     0, Toplam:    55, Sure: 63 ms - VidShrink.Tests.dll (net8.0)
dusen kollar: D3_KopyaYolundaYenidenKodlamaIsteyenGecersizKilmaUygulaniyor, K2_SabitlenenDegerFfmpegKomutSatirindaGorunuyor

### M2 (K6b) CRF sabitken bitrate yine butceden gelir
plan.VideoBitrateK = (int)Math.Round(Math.Max(VideoBitrateK(bppfAtCrf, best.Width, best.Height, best.Fps), MinVideoBitrateK));
  ->  plan.VideoBitrateK = plan.VideoBitrateK;
Basarisiz! - Basarisiz:     1, Basarili:    54, Atlanan:     0, Toplam:    55, Sure: 62 ms - VidShrink.Tests.dll (net8.0)
dusen kollar: K3_AyniCrfFarkliHedeflerdeAyniCrfiVeriyor

### M3 (K6c) on ayar gerekcesi uretilmez
ReasonCode.ManualPresetOverride
  ->  ReasonCode.PredictedQualityEstimated
Basarisiz! - Basarisiz:     2, Basarili:    53, Atlanan:     0, Toplam:    55, Sure: 60 ms - VidShrink.Tests.dll (net8.0)
dusen kollar: K2_SabitlenenDegerFfmpegKomutSatirindaGorunuyor, K4_HerKalemNotuIkiAlaniDolduruyor

### M4 (D2) karsilanmayan donanim istegi susar
ReasonCode.ManualEncoderPathUnmet
  ->  ReasonCode.ManualEncoderPathOverride
Basarisiz! - Basarisiz:     1, Basarili:    54, Atlanan:     0, Toplam:    55, Sure: 61 ms - VidShrink.Tests.dll (net8.0)
dusen kollar: D2_DonanimYokkenIstekKarsilanmadiDeniyor

### M5 (D3) kopya yolu yeniden kodlama istegini yine yutar
if (HasReencodeOverride(options)) return false;
  ->  if (HasReencodeOverride(options) && info.Width < 0) return false;
Basarisiz! - Basarisiz:     5, Basarili:    50, Atlanan:     0, Toplam:    55, Sure: 56 ms - VidShrink.Tests.dll (net8.0)
dusen kollar: D3_KopyaYolundaYenidenKodlamaIsteyenGecersizKilmaUygulaniyor

### M6 (D4) etkisiz cozunurluk istegi yine not uretir
else if (plan.Height > enginePlan.Height)
  ->  else if (plan.Height > 0)
Basarisiz! - Basarisiz:     1, Basarili:    54, Atlanan:     0, Toplam:    55, Sure: 59 ms - VidShrink.Tests.dll (net8.0)
dusen kollar: D4_EtkisizTabanIstegiNotUretmiyor

### M7 (D1) kullanicinin fps tabani yok sayilir
var minFps = options.MinFps is double f ? Math.Max(floors.MinFps, f) : floors.MinFps;
  ->  var minFps = floors.MinFps;
Basarisiz! - Basarisiz:     5, Basarili:    50, Atlanan:     0, Toplam:    55, Sure: 61 ms - VidShrink.Tests.dll (net8.0)
dusen kollar: D1_KareHiziTabaniFfmpegKomutSatirindakiFpsiDegistiriyor, D4_EtkiliFpsTabanNotuPlaninGercekFpsiniTasiyor, F2_KarsilanabilenTabanIstegiKarsilanmadiDemiyor, K2_SabitlenenDegerFfmpegKomutSatirindaGorunuyor, K4_HerKalemNotuIkiAlaniDolduruyor

### M8 (K1) ses kanali gecersiz kilmasi varsayilana sizar
if (info.HasAudio && options.AudioChannels != AudioChannelOverride.Auto)
  ->  if (info.HasAudio)
Basarisiz! - Basarisiz:     5, Basarili:    50, Atlanan:     0, Toplam:    55, Sure: 60 ms - VidShrink.Tests.dll (net8.0)
dusen kollar: K1_VarsayilanT165OncesiMotorlaBirebirAyni

### M9 (K1) varsayilan bitrate hesabi degisir
totalK * ContainerOverhead - audioK
  ->  totalK - audioK
Basarisiz! - Basarisiz:     5, Basarili:    50, Atlanan:     0, Toplam:    55, Sure: 59 ms - VidShrink.Tests.dll (net8.0)
dusen kollar: K1_VarsayilanT165OncesiMotorlaBirebirAyni

### M10 (F1) kaynak ustu hedef kirpma orani degisir
SourceSizeCap = 0.95
  ->  SourceSizeCap = 0.80
Basarisiz! - Basarisiz:     1, Basarili:    54, Atlanan:     0, Toplam:    55, Sure: 57 ms - VidShrink.Tests.dll (net8.0)
dusen kollar: F1_KaynakUstuHedefKaynaginYuzde95ineKirpiliyor

### M11 (F2) karsilanmayan fps tabani susar
if (plan.Fps < requestedMinFps - 0.01)
  ->  if (plan.Fps < 0)
Basarisiz! - Basarisiz:     1, Basarili:    54, Atlanan:     0, Toplam:    55, Sure: 70 ms - VidShrink.Tests.dll (net8.0)
dusen kollar: F2_KaynagiAsanFpsTabaniYenidenKodlamaYolundaKarsilanmadiDeniyor

### M12 (F2) karsilanmayan cozunurluk tabani susar
if (plan.Height < requestedMinHeight)
  ->  if (plan.Height < 0)
Basarisiz! - Basarisiz:     1, Basarili:    54, Atlanan:     0, Toplam:    55, Sure: 55 ms - VidShrink.Tests.dll (net8.0)
dusen kollar: F2_KaynagiAsanCozunurlukTabaniYenidenKodlamaYolundaKarsilanmadiDeniyor
```

Tur 2'nin ızgarasında **sıfır kol düşüren bir mutasyon** vardı (F1); yerini yukarıdaki on iki
mutasyonluk ızgara aldı ve on ikisi de düşürüyor.

---

## K7 — Kol sayısı

```
dotnet test -c Release --filter "FullyQualifiedName~ManualOverrideTests" --list-tests   ->  55
dotnet test -c Release --filter "FullyQualifiedName~CodecLockTests"      --list-tests   ->  14
```

İkisi de koşuldu: 55/55 ve 14/14 geçti. Sıfır bulan kol yok.

Ayrıca plan hesabına ve argüman üretimine dokunan on altı sınıf (`PlanCalculatorTests`,
`PlanCalculatorProbeTests`, `KestirimPlanTests`, `AdviceCoverageTests`, `FillBandTests`,
`FpsDropTests`, `ExtremeCompressionTests`, `SessizDusurmeTests`, `SpeedModeTests`,
`QualityTargetTests`, `TurboTavanTests`, `HardwareRateControlTests`, `CodecLockTests`,
`ManualOverrideTests`, `FfmpegArgumentsTests`, `ConversionArgumentsTests`) birlikte
koşuldu:

```
Basarili! - Basarisiz: 0, Basarili: 354, Atlanan: 6, Toplam: 360, Sure: 4 m 7 s
```
