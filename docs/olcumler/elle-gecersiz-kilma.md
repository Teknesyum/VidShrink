# Elle geçersiz kılma — ölçümler

T165, tur 4. Tur 1 bağımsız denetimde `verdict: failed` döndü ve belge baştan yazıldı;
tur 2 denetimi dört bulgu (F1-F4), tur 3 denetimi dört bulgu daha (H1-H4) döndürdü.
Bu sürüm H1-H4'ü kapatıyor.

Ölçü dosyası: `tests/VidShrink.Tests/ManualOverrideTests.cs`.
Ham çıktı: `dotnet test -c Release --filter "FullyQualifiedName~ManualOverrideTests" --logger "console;verbosity=detailed"`.

Bu belgedeki ham çıktının tamamı belgenin kendi içinde; kanıt olarak `.calisma/` altına
atıf yok (F4: `.calisma/` `.gitignore`da, dal birleşince kanıt kaybolur). `.calisma/`
yalnız aşağıdaki tarifte **geçici dizin** olarak geçiyor; oradan hiçbir sayı okunmuyor.

---

## K1 — Varsayılan hiçbir şeyi değiştirmiyor

Beklenen değerler bu ağaçtan değil, **T165 öncesi motordan** ölçüldü ve tur 3'te bu
ölçüm gerçekten koşuldu (F3: tur 2'de iddia doğruydu ama gösterilen kanıt aynı süreçten
çıkıyordu, iki motoru ayırt edemiyordu).

Taban koşumunu üreten düzenek **depoda**: `tools/VidShrink.PlanBaseline` (H3; tur 3'te
düzenek "tek kullanımlık" diye atılmıştı, AGENTS.md ise ölçümü üreten düzeneğin `tools/`a
taşınmasını istiyor). Tarif ve alan listesi `tools/VidShrink.PlanBaseline/AGENTS.md`de.

Girdi eksiksiz — beş bileşimin `MediaInfo` alanlarının hepsi, `ManualOverrideTests.Info()`
ile birebir aynı:

| alan | değer |
|---|---|
| `FilePath` | `sample.mp4` |
| `FileSizeBytes` | 500 MB (`500L * 1024 * 1024`) |
| `DurationSeconds` | bileşime göre 120 / 300 / 45 / 600 / 30 |
| `Width` x `Height` @ `Fps` | bileşime göre (tablodaki kaynak sütunu) |
| `VideoCodec` | `h264` |
| `TotalBitrateBps` | `35_000_000` |
| `AudioCodec` | `aac` |
| `AudioBitrateBps` | `128_000` |
| `AudioChannels` | `2` |

`PlanOptions`ta yalnız `TargetMb` ve `Codec = Auto` var; hiçbir `Locked*` alanı
kullanılmıyor — bu yüzden düzenek T165 öncesi ağaçta da derleniyor. Kodlayıcı yoklaması
`AllWorking`: altı kodlayıcının altısı da çalışır durumda.

Koşum (bu ağaç ve 9b092e9, ayrı çalışma ağacı):

```
dotnet run -c Release --project tools/VidShrink.PlanBaseline

git worktree add .calisma/taban 9b092e9
cp -r tools/VidShrink.PlanBaseline .calisma/taban/tools/
dotnet run -c Release --project .calisma/taban/tools/VidShrink.PlanBaseline
git worktree remove .calisma/taban --force
```

Ham çıktı — **bu ağaç** (T165 tur 4):

```
# VidShrink.PlanBaseline
# kaynak|hedefMB -> kodek|kip|videoK|crf|WxH@fps|sesK/kanal|preset
1920x1080@30|25 -> libsvtav1|2pass|1567k|crf=-|1920x1080@30|ses 128k/kaynak|preset 6
1280x720@24|8 -> libsvtav1|2pass|188k|crf=-|1202x676@24|ses 26k/1|preset 6
3840x2160@60|50 -> libsvtav1|2pass|9016k|crf=-|3840x2160@60|ses 128k/kaynak|preset 6
1920x1080@30|6 -> libsvtav1|2pass|80k|crf=-|690x388@30|ses 0k/kaynak|preset 6
1280x720@30|100 -> libx264|2pass|27305k|crf=-|1280x720@30|ses 128k/kaynak|preset slow
```

Ham çıktı — **ayrı çalışma ağacı, 9b092e9** (T165'in ebeveyni, sözleşme öncesi motor):

```
# VidShrink.PlanBaseline
# kaynak|hedefMB -> kodek|kip|videoK|crf|WxH@fps|sesK/kanal|preset
1920x1080@30|25 -> libsvtav1|2pass|1567k|crf=-|1920x1080@30|ses 128k/kaynak|preset 6
1280x720@24|8 -> libsvtav1|2pass|188k|crf=-|1202x676@24|ses 26k/1|preset 6
3840x2160@60|50 -> libsvtav1|2pass|9016k|crf=-|3840x2160@60|ses 128k/kaynak|preset 6
1920x1080@30|6 -> libsvtav1|2pass|80k|crf=-|690x388@30|ses 0k/kaynak|preset 6
1280x720@30|100 -> libx264|2pass|27305k|crf=-|1280x720@30|ses 128k/kaynak|preset slow
```

İki çıktı satır satır aynı. 9b092e9 ağacının gerçekten T165 öncesi olduğu doğrulandı:
o `PlanOptions` içinde `LockedCrf`, `LockedMode`, `LockedPreset`, `LockedAudioKbps`,
`AudioChannels`, `MinResolutionHeight`, `MinFps`, `EncoderPath` alanlarının **hiçbiri yok**.

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

## H1 — Sekiz kalemin kalem kalem taraması

Aynı kusur sınıfı — *karşılanmamış isteği sessizce düşürmek* — bu sözleşmede dört ayrı
giriş noktasında çıktı (D2 kodlayıcı yolu, D3 kopyalama yolu, F2 tabanlar, H1 ses).
Tur 4'te tek tek kovalamak bırakıldı: **sekiz kalemin her biri** için soru soruldu —
bu kalem hangi koşulda karşılanamaz, ve o koşulda plan bunu söylüyor mu?

Tarama sekiz kalemi de kapsıyor ve **beş yeni açık koşul** buldu (tabloda ⚠ ile).
Hepsi kapatıldı.

| # | kalem | karşılanamayacağı koşul | önce plan ne diyordu | şimdi | ölçü |
|---|---|---|---|---|---|
| 1 | EncodeMode | `LockedCrf` de verilmiş (CRF öncelikli, `else if` kip dalını hiç görmüyor) ⚠ | hiçbir şey | `ManualModeSupersededByCrf` | `H2_CrfKipIleCelistigindeCrfKazandigiYaziliyor` |
| 1 | EncodeMode | plan zaten istenen kipte | not yok (D4 gereği doğru; istek etkisiz) | değişmedi | `H2_CrfKipIleUyumluysaCelismeNotuYok` |
| 1 | EncodeMode | kopyalama yolu | yol `HasReencodeOverride` ile kapanıyor, istek uygulanıyor | değişmedi | `D3_KopyaYolundaYenidenKodlamaIsteyenGecersizKilmaUygulaniyor(kip)` |
| 2 | CRF değeri | istenen değer kodeğin `CrfRange` aralığının dışında (x264 10-45, av1 18-55) ⚠ | `Math.Clamp` sessizce kırpıyor **ve** gerekçe kırpılmış değeri "kullanıcı sabitledi" diye yazıyordu | `ManualCrfClamped`; cümle de "plan kırpılmış CRF ... ile çıkıyor"a döndü | `H1_KodekAraliginiAsanCrfKirpildigiYaziliyor` (2 kol) |
| 2 | CRF değeri | aralık içindeyse | — | kırpma notu yazılmıyor | `H1_AraliktakiCrfKirpilmaNotuUretmiyor` |
| 3 | preset / hız | geçersiz ad | `ArgumentException` fırlıyor — sessiz değil | değişmedi | `PlanCalculator.cs:LockedPreset` dalı, `IsValidPreset` |
| 3 | preset / hız | turbo ilk geçiş (`SpeedMode.Fast` + libx265 + 2pass) birinci geçişi tavana indiriyor ⚠ | hiçbir şey | `ManualPresetFirstPassRelaxed` | `H1_TurboIlkGecisteOnAyarinGevsedigiYaziliyor` |
| 3 | preset / hız | turbo kapalıyken | iki geçişte de kullanıcının ön ayarı | değişmedi | `H1_TurboKapaliykenOnAyarIkiGecisteDeGecerli` |
| 3 | preset / hız | **başka koşul yok** — altı kodlayıcının altısında da çıktı geçişinde görünüyor | — | — | `H1_SabitlenenOnAyarHerKodlayicidaCiktiGecisindeGorunuyor` (6 kol) |
| 4 | ses hedefi (kbps) | kaynakta ses akışı yok (`HasAudio == false`) ⚠ | hiçbir şey; `HasReencodeOverride` kopyalama yolunu da kapattığı için `AddPassThroughDropNotes` de koşmuyordu | `ManualAudioBitrateUnmet` | `H1_SessizKaynaktaSesHedefiKarsilanmadiDeniyor` |
| 4 | ses hedefi (kbps) | aynı anda `AudioChannels = None` ⚠ | "96 kbps sabitlendi" diyip sesi atıyordu | `ManualAudioBitrateSupersededByChannels` | `H2_SesHedefiKanalNoneIleCelistigindeHangisiKazandiYaziliyor` |
| 5 | ses kanalı | kaynakta ses akışı yok ⚠ | hiçbir şey | `ManualAudioChannelsUnmet` | `H1_SessizKaynaktaSesKanaliKarsilanmadiDeniyor` (3 kol) |
| 5 | ses kanalı | kaynak mono iken `Stereo` isteniyor | istek karşılanıyor: `-ac 2`, ffmpeg upmix ediyor | değişmedi | `K2_...(ses-stereo)` |
| 6 | çözünürlük tabanı | taban kaynağın üstünde (motor yukarı ölçeklemiyor) | F2'de kapatıldı | `ManualMinResolutionUnmet` | `F2_KaynagiAsanCozunurlukTabaniYenidenKodlamaYolundaKarsilanmadiDeniyor` |
| 6 | çözünürlük tabanı | kopyalama yolu | D3'te kapatıldı | `ManualOverrideDroppedOnPassThrough` | `D3_KopyaYolundaUygulanamayanIstekSessizceDusmuyor` |
| 7 | kare hızı tabanı | taban kaynağın üstünde | F2'de kapatıldı | `ManualMinFpsUnmet` | `F2_KaynagiAsanFpsTabaniYenidenKodlamaYolundaKarsilanmadiDeniyor` |
| 7 | kare hızı tabanı | kopyalama yolu | D3'te kapatıldı | `ManualOverrideDroppedOnPassThrough` | `D3_KopyaYolundaUygulanamayanIstekSessizceDusmuyor` |
| 8 | kodlayıcı yolu | makinede o yolda çalışan kodlayıcı yok | D2'de kapatıldı | `ManualEncoderPathUnmet` | `D2_DonanimYokkenIstekKarsilanmadiDeniyor` |
| 8 | kodlayıcı yolu | aynı anda `LockedCodec` verilmiş ve kilitli kodek karşı yolda ⚠ | hiçbir şey — `lockedCodec is null` kapısı bütün bloğu atlıyordu | `ManualEncoderPathSupersededByCodec` | `H2_KodekKilidiKodlayiciYoluIleCelistigindeKodekKazandigiYaziliyor` |
| 8 | kodlayıcı yolu | kopyalama yolu | D3'te kapatıldı | `ManualOverrideDroppedOnPassThrough` | `D3_KopyaYolundaUygulanamayanIstekSessizceDusmuyor` |

**"Karşılanamayacağı koşul yok" diyen tek satır 3. kalemin son satırı** ve iddia değil,
ölçü: `H1_SabitlenenOnAyarHerKodlayicidaCiktiGecisindeGorunuyor` altı kodlayıcıyı tek tek
koşuyor ve her birinde sabitlenen ön ayarın komut satırında olduğunu doğruluyor. Ham çıktı:

```
| hevc_nvenc | p6 | -preset p6 | kip=2pass |
| av1_nvenc | p5 | -preset p5 | kip=2pass |
| libx264 | veryslow | -preset veryslow | kip=2pass |
| libx265 | slower | -preset slower | kip=2pass |
| libsvtav1 | 3 | -preset 3 | kip=2pass |
| h264_nvenc | p7 | -preset p7 | kip=2pass |
```

Sessiz kaynakta ses hedefi — ham çıktı:

```
sessiz kaynak + LockedAudioKbps=96 -> ses 0k codec=-
args: ffmpeg -hide_banner -y -hwaccel auto -i sessiz.mp4 -vf scale=1690:950:flags=lanczos
      -c:v libx264 -preset slow -b:v 1695k -maxrate 2542k -bufsize 3390k -g 300
      -keyint_min 30 -pix_fmt yuv420p -an -movflags +faststart out.mp4
gerekce: kullanici ses hedefini 96kbps olarak sabitledi ama kaynakta ses akisi yok;
         istek karsilanmadi, cikti sessiz; ...
```

Sessiz kaynakta ses kanalı — üç halin ham çıktısı:

```
sessiz kaynak + AudioChannels=Stereo -> kanal kaynak codec=-
gerekce: kullanici ses kanalini Stereo olarak sabitledi ama kaynakta ses akisi yok; ...
sessiz kaynak + AudioChannels=Mono -> kanal kaynak codec=-
gerekce: kullanici ses kanalini Mono olarak sabitledi ama kaynakta ses akisi yok; ...
sessiz kaynak + AudioChannels=None -> kanal kaynak codec=-
gerekce: kullanici ses kanalini None olarak sabitledi ama kaynakta ses akisi yok; ...
```

Aralık dışı CRF — ham çıktı (kırpma notu **önce**, uygulanan değeri anlatan cümle sonra):

```
codec=libx264 aralik=(10, 45) istek=4 -> crf=10
gerekce: ... istenen CRF 4 libx264 icin gecerli 10-45 araliginin disinda; istek
         karsilanmadi ve aralik ucuna, CRF 10'e kirpildi; plan kirpilmis CRF 10 ile
         cikiyor; hedef boyut artik zorlanmiyor, 82097k yalniz bir tahmin — motor
         2pass kipinde 2pass@1567k secmisti
codec=libx264 aralik=(10, 45) istek=60 -> crf=45
gerekce: ... istenen CRF 60 libx264 icin gecerli 10-45 araliginin disinda; istek
         karsilanmadi ve aralik ucuna, CRF 45'e kirpildi; plan kirpilmis CRF 45 ile
         cikiyor; ...
```

Turbo ilk geçiş — iki geçişin ham komut satırı:

```
codec=libx265 turbo=True preset=veryslow
1. gecis: ... -c:v libx265 -preset veryfast -b:v 1567k ... -pass 1 ...
2. gecis: ... -c:v libx265 -preset veryslow -b:v 1567k ... -pass 2 ...
gerekce: ... kullanici on ayari veryslow olarak sabitledi; motor slow secmisti;
         turbo ilk gecis libx265 icin birinci gecisi veryfast ile kosuyor; sabitlenen
         veryslow yalniz ciktinin uretildigi ikinci geciste gecerli
```

---

## H2 — Kalemler arası çelişki: hangisinin kazandığı yazılıyor

İki kalem birbirini geçersiz kıldığında plan artık **hangisinin kazandığını** söylüyor.
Üç çelişki bulundu; üçü de aynı kalıpta kapatıldı (`...SupersededBy...` sebep kodu,
kaybeden isteğin değeri `ManualOverrideValue`da, kazananın adı `EngineWouldHaveChosen`de).

| çelişki | kazanan | önce | sonra | negatif kontrol |
|---|---|---|---|---|
| `LockedAudioKbps` + `AudioChannels = None` | kanal isteği (çıktı sessiz) | "96 kbps sabitlendi" + `-an` | `ManualAudioBitrateSupersededByChannels` | `H1_SesliKaynaktaKarsilanmadiNotuYazilmiyor` |
| `LockedCrf` + `LockedMode` (kip ≠ Crf) | CRF | kip isteği hiç görülmüyordu | `ManualModeSupersededByCrf` | `H2_CrfKipIleUyumluysaCelismeNotuYok` |
| `LockedCodec` + `EncoderPath` (kilitli kodek karşı yolda) | kodek kilidi | yol isteği hiç görülmüyordu | `ManualEncoderPathSupersededByCodec` | `H2_KodekKilidiKodlayiciYoluIleUyumluysaCelismeNotuYok` |

Ham çıktı:

```
96k + None -> ses 0k codec=-
args: ... -c:v libx264 -preset slow -b:v 1695k ... -an -movflags +faststart out.mp4
gerekce: kullanici ses hedefini 96kbps olarak sabitledi ama ayni anda ses kanali=None
         dedi; kanal istegi kazandi, cikti sessiz ve 96kbps uygulanmadi; kullanici ses
         kanalini None olarak sabitledi; motor source secmisti; ...

LockedCrf=19 + LockedMode=TwoPass -> kip=crf crf=19
gerekce: ... kullanici CRF'i 19 olarak sabitledi; hedef boyut artik zorlanmiyor, 29026k
         yalniz bir tahmin — motor 2pass kipinde 2pass@1567k secmisti; kullanici kodlama
         kipini TwoPass olarak da sabitlemisti ama acik CRF sayisi onceliklidir; kip crf
         oldu ve TwoPass istegi uygulanmadi

LockedCodec=libx264 + EncoderPath=Hardware -> codec=libx264
gerekce: kullanici kodlayici yolunu Hardware olarak sabitledi ama ayni anda kodegi
         libx264 olarak kilitledi; kodek kilidi onceliklidir, yol istegi uygulanmadi ve
         kullanilan libx264; ...
```

Negatif kontroller boş değil: M20 (koşulsuz "karşılanmadı") on iki kolu, M21 (koşulsuz
kırpma notu) ve M22 (yol/kodek uyumunu denetlemeyen not) birer kolu düşürüyor.

---

## H4 — Üst üste iki docstring

`PlanCalculator.cs`te `WithoutManualFloors`un başında iki `<summary>` bloğu üst üste
duruyordu; birincisi `EffectiveFloors`a aitti ve o metot belgesiz kalmıştı. Birinci blok
`EffectiveFloors`ın başına taşındı, ikincisi `WithoutManualFloors`ta kaldı. İki metodun
da tek ve kendi docstring'i var.

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

Yirmi iki mutasyon, her birinden önce `dotnet build -c Release --no-incremental`;
`--no-build` kullanılmadı. M1-M12 tur 3'ün, M13-M22 tur 4'ün. Tur 3'ün ham çıktısı bu
bölümün sonunda; tur 4'ünki M22'den sonraki blokta.

M8 tur 4'te yeniden koşuldu: ses bloğu H1 için yeniden yapılandırıldı, eski formül artık
ağaçta yok. Aşağıdaki M8 satırı **yeni şekle karşı** koşulan mutasyondur.

| # | mutasyon | düşen kollar (sayı) |
|---|---|---|
| M1 (K6a) sabitlenen on ayar yok sayilir | `plan.Preset = manualPreset;` -> `plan.Preset = enginePreset;` | D3_KopyaYolundaYenidenKodlamaIsteyenGecersizKilmaUygulaniyor, K2_SabitlenenDegerFfmpegKomutSatirindaGorunuyor (2) |
| M2 (K6b) CRF sabitken bitrate yine butceden gelir | `plan.VideoBitrateK = (int)Math.Round(Math.Max(VideoBitrateK(bppfAtCrf, best.Width, best.Height, best.Fps), MinVideoBitrateK));` -> `plan.VideoBitrateK = plan.VideoBitrateK;` | K3_AyniCrfFarkliHedeflerdeAyniCrfiVeriyor (1) |
| M3 (K6c) on ayar gerekcesi uretilmez | `ReasonCode.ManualPresetOverride` -> `ReasonCode.PredictedQualityEstimated` | K2_SabitlenenDegerFfmpegKomutSatirindaGorunuyor, K4_HerKalemNotuIkiAlaniDolduruyor (2) |
| M4 (D2) karsilanmayan donanim istegi susar | `ReasonCode.ManualEncoderPathUnmet` -> `ReasonCode.ManualEncoderPathOverride` | D2_DonanimYokkenIstekKarsilanmadiDeniyor (1) |
| M5 (D3) kopya yolu yeniden kodlama istegini yine yutar | `if (HasReencodeOverride(options)) return false;` -> `if (HasReencodeOverride(options) && info.Width < 0) return false;` | D3_KopyaYolundaYenidenKodlamaIsteyenGecersizKilmaUygulaniyor (5) |
| M6 (D4) etkisiz cozunurluk istegi yine not uretir | `else if (plan.Height > enginePlan.Height)` -> `else if (plan.Height > 0)` | D4_EtkisizTabanIstegiNotUretmiyor (1) |
| M7 (D1) kullanicinin fps tabani yok sayilir | `var minFps = options.MinFps is double f ? Math.Max(floors.MinFps, f) : floors.MinFps;` -> `var minFps = floors.MinFps;` | D1_KareHiziTabaniFfmpegKomutSatirindakiFpsiDegistiriyor, D4_EtkiliFpsTabanNotuPlaninGercekFpsiniTasiyor, F2_KarsilanabilenTabanIstegiKarsilanmadiDemiyor, K2_SabitlenenDegerFfmpegKomutSatirindaGorunuyor, K4_HerKalemNotuIkiAlaniDolduruyor (5) |
| M8 (K1) ses kanali gecersiz kilmasi varsayilana sizar | `if (options.AudioChannels != AudioChannelOverride.Auto)` -> `if (info.DurationSeconds > 0)` | K1_VarsayilanT165OncesiMotorlaBirebirAyni (5) |
| M9 (K1) varsayilan bitrate hesabi degisir | `totalK * ContainerOverhead - audioK` -> `totalK - audioK` | K1_VarsayilanT165OncesiMotorlaBirebirAyni (5) |
| M10 (F1) kaynak ustu hedef kirpma orani degisir | `SourceSizeCap = 0.95` -> `SourceSizeCap = 0.80` | F1_KaynakUstuHedefKaynaginYuzde95ineKirpiliyor (1) |
| M11 (F2) karsilanmayan fps tabani susar | `if (plan.Fps < requestedMinFps - 0.01)` -> `if (plan.Fps < 0)` | F2_KaynagiAsanFpsTabaniYenidenKodlamaYolundaKarsilanmadiDeniyor (1) |
| M12 (F2) karsilanmayan cozunurluk tabani susar | `if (plan.Height < requestedMinHeight)` -> `if (plan.Height < 0)` | F2_KaynagiAsanCozunurlukTabaniYenidenKodlamaYolundaKarsilanmadiDeniyor (1) |
| M13 (H1) sessiz kaynakta ses hedefi istegi susar | `if (!info.HasAudio)` -> `if (!info.HasAudio && info.DurationSeconds < 0)` (ses hedefi dalı) | H1_SessizKaynaktaSesHedefiKarsilanmadiDeniyor (1) |
| M14 (H1) sessiz kaynakta ses kanali istegi susar | `if (!info.HasAudio)` -> `if (!info.HasAudio && info.DurationSeconds < 0)` (ses kanalı dalı) | H1_SessizKaynaktaSesKanaliKarsilanmadiDeniyor (3) |
| M15 (H2) ses hedefi/kanal celiskisi susar | `else if (audioSilenced)` -> `else if (audioSilenced && info.DurationSeconds < 0)` | H2_SesHedefiKanalNoneIleCelistigindeHangisiKazandiYaziliyor (1) |
| M16 (H2) crf/kip celiskisi susar | `if (options.LockedMode is EncodeMode supersededMode && supersededMode != EncodeMode.Crf)` -> `... && info.DurationSeconds < 0)` | H2_CrfKipIleCelistigindeCrfKazandigiYaziliyor (1) |
| M17 (H2) kodek kilidi/yol celiskisi susar | `if (lockedCodec is not null && options.EncoderPath != ... )` -> `if (lockedCodec is not null && info.DurationSeconds < 0 && ...)` | H2_KodekKilidiKodlayiciYoluIleCelistigindeKodekKazandigiYaziliyor (1) |
| M18 (H1) crf kirpma susar | `if (crfClamped)` -> `if (crfClamped && info.DurationSeconds < 0)` | H1_KodekAraliginiAsanCrfKirpildigiYaziliyor (2) |
| M19 (H1) turbo ilk gecis gevsemesi susar | `if (plan.TurboFirstPass && plan.ModeEnum == EncodeMode.TwoPass)` -> `if (plan.TurboFirstPass && info.DurationSeconds < 0 && ...)` | H1_TurboIlkGecisteOnAyarinGevsedigiYaziliyor (1) |
| M20 (H1 negatif) ses istegi kosulsuz "karsilanmadi" yazar | iki ses dalında `if (!info.HasAudio)` -> `if (!info.HasAudio \|\| info.DurationSeconds > 0)` | D3_KopyaYolunda... (2), K2_Sabitlenen... (4), K4_HerKalem... (4), H1_SesliKaynaktaKarsilanmadiNotuYazilmiyor, H2_SesHedefiKanalNone... (12) |
| M21 (H1 negatif) kirpma notu kosulsuz yazilir | `if (crfClamped)` -> `if (crfClamped \|\| info.DurationSeconds > 0)` | H1_AraliktakiCrfKirpilmaNotuUretmiyor (1) |
| M22 (H2 negatif) yol/kodek uyumu denetlenmez | `&& CodecModel.IsHardware(codec) != (options.EncoderPath == EncoderPathOverride.Hardware))` -> `&& info.DurationSeconds > 0)` | H2_KodekKilidiKodlayiciYoluIleUyumluysaCelismeNotuYok (1) |

Yirmi ikisinin de en az bir kolu düşüyor; sıfır ölçü düşüren mutasyon yok. Tur 3'ün ham
çıktısı:

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

Tur 2'nin ızgarasında **sıfır kol düşüren bir mutasyon** vardı (F1); yerini yukarıdaki
ızgara aldı ve yirmi ikisi de düşürüyor.

Tur 4'ün ham çıktısı (`dotnet test -c Release --no-build --filter
"FullyQualifiedName~ManualOverrideTests"`, her mutasyondan önce `--no-incremental` build):

```
### M8 (K1) ses kanali gecersiz kilmasi varsayilana sizar   [yeni sekle karsi]
if (options.AudioChannels != AudioChannelOverride.Auto)  ->  if (info.DurationSeconds > 0)
Basarisiz! - Basarisiz: 5, Basarili: 71, Atlanan: 0, Toplam: 76
dusen kollar: K1_VarsayilanT165OncesiMotorlaBirebirAyni (bes bilesimin besi de)

### M13 (H1) sessiz kaynakta ses hedefi istegi susar
if (!info.HasAudio)  ->  if (!info.HasAudio && info.DurationSeconds < 0)
Basarisiz! - Basarisiz: 1, Basarili: 75, Atlanan: 0, Toplam: 76
dusen kol: H1_SessizKaynaktaSesHedefiKarsilanmadiDeniyor

### M14 (H1) sessiz kaynakta ses kanali istegi susar
if (!info.HasAudio)  ->  if (!info.HasAudio && info.DurationSeconds < 0)
Basarisiz! - Basarisiz: 3, Basarili: 73, Atlanan: 0, Toplam: 76
dusen kollar: H1_SessizKaynaktaSesKanaliKarsilanmadiDeniyor (Stereo, Mono, None)

### M15 (H2) ses hedefi/kanal celiskisi susar
else if (audioSilenced)  ->  else if (audioSilenced && info.DurationSeconds < 0)
Basarisiz! - Basarisiz: 1, Basarili: 75, Atlanan: 0, Toplam: 76
dusen kol: H2_SesHedefiKanalNoneIleCelistigindeHangisiKazandiYaziliyor

### M16 (H2) crf/kip celiskisi susar
if (options.LockedMode is EncodeMode supersededMode && supersededMode != EncodeMode.Crf)
  ->  ... && info.DurationSeconds < 0)
Basarisiz! - Basarisiz: 1, Basarili: 75, Atlanan: 0, Toplam: 76
dusen kol: H2_CrfKipIleCelistigindeCrfKazandigiYaziliyor

### M17 (H2) kodek kilidi/yol celiskisi susar
if (lockedCodec is not null && options.EncoderPath != EncoderPathOverride.Auto
  ->  if (lockedCodec is not null && info.DurationSeconds < 0 && options.EncoderPath != ...
Basarisiz! - Basarisiz: 1, Basarili: 75, Atlanan: 0, Toplam: 76
dusen kol: H2_KodekKilidiKodlayiciYoluIleCelistigindeKodekKazandigiYaziliyor

### M18 (H1) crf kirpma susar
if (crfClamped)  ->  if (crfClamped && info.DurationSeconds < 0)
Basarisiz! - Basarisiz: 2, Basarili: 74, Atlanan: 0, Toplam: 76
dusen kollar: H1_KodekAraliginiAsanCrfKirpildigiYaziliyor (istek 4 ve istek 60)

### M19 (H1) turbo ilk gecis gevsemesi susar
if (plan.TurboFirstPass && plan.ModeEnum == EncodeMode.TwoPass)
  ->  if (plan.TurboFirstPass && info.DurationSeconds < 0 && plan.ModeEnum == ...)
Basarisiz! - Basarisiz: 1, Basarili: 75, Atlanan: 0, Toplam: 76
dusen kol: H1_TurboIlkGecisteOnAyarinGevsedigiYaziliyor

### M20 (H1 negatif) ses istegi kosulsuz "karsilanmadi" yazar
iki ses dalinda if (!info.HasAudio)  ->  if (!info.HasAudio || info.DurationSeconds > 0)
Basarisiz! - Basarisiz: 12, Basarili: 64, Atlanan: 0, Toplam: 76
dusen kollar: D3_KopyaYolundaYenidenKodlamaIsteyenGecersizKilmaUygulaniyor (ses-kanali,
  ses-kbps), K2_SabitlenenDegerFfmpegKomutSatirindaGorunuyor (ses-mono, ses-stereo,
  ses-kbps, ses-yok), K4_HerKalemNotuIkiAlaniDolduruyor (ses-mono, ses-stereo, ses-yok,
  ses-kbps), H1_SesliKaynaktaKarsilanmadiNotuYazilmiyor,
  H2_SesHedefiKanalNoneIleCelistigindeHangisiKazandiYaziliyor

### M21 (H1 negatif) kirpma notu kosulsuz yazilir
if (crfClamped)  ->  if (crfClamped || info.DurationSeconds > 0)
Basarisiz! - Basarisiz: 1, Basarili: 75, Atlanan: 0, Toplam: 76
dusen kol: H1_AraliktakiCrfKirpilmaNotuUretmiyor

### M22 (H2 negatif) yol/kodek uyumu denetlenmez
&& CodecModel.IsHardware(codec) != (options.EncoderPath == EncoderPathOverride.Hardware))
  ->  && info.DurationSeconds > 0)
Basarisiz! - Basarisiz: 1, Basarili: 75, Atlanan: 0, Toplam: 76
dusen kol: H2_KodekKilidiKodlayiciYoluIleUyumluysaCelismeNotuYok
```

Mutasyonların hepsi geri alındı; `PlanCalculator.cs` mutasyonsuz hâline döndü ve
76/76 + 14/14 yeşil koştu.

---

## K7 — Kol sayısı

```
dotnet test -c Release --filter "FullyQualifiedName~ManualOverrideTests" --list-tests   ->  76
dotnet test -c Release --filter "FullyQualifiedName~CodecLockTests"      --list-tests   ->  14
```

İkisi de koşuldu: 76/76 ve 14/14 geçti. Sıfır bulan kol yok. (Tur 3'te 55 idi; tur 4
yirmi bir kol ekledi.)

Ayrıca `PlanCalculator`a dokunan yirmi sınıf üç öbekte koşuldu:

```
CodecLock, EncoderAvailability, EncoderCapabilities, EncoderStateConsumption,
ExtremeCompression, FillBand
  -> Basarisiz: 0, Basarili: 90, Atlanan: 4, Toplam: 94

FpsDrop, HardwareFlag, HardwareRateControl, HdrArguments, KestirimPlan,
ManualOverride, OluUye
  -> Basarisiz: 1, Basarili: 172, Atlanan: 3, Toplam: 176

PlanCalculatorProbe, PlanCalculator, QualityHint, QualityTarget, SpeedMode,
TurboTavan, UretimYolu
  -> Basarisiz: 0, Basarili: 144, Atlanan: 0, Toplam: 144
```

Toplam 406 geçti, 1 düştü. **Düşen kol T165'in değil**: `OluUyeTests` `cfad38e`de de
düşüyor (o commit'te `git stash` ile doğrulandı, 1 düştü / 10 geçti). Ayrıntı aşağıda.

### Kapatılamayan bulgu: `OluUyeTests` `cfad38e`de kırık

```
OluUyeTests.TheZeroConsumerSetIsThePinnedSet
Expected: [..., "EncoderVendor.Software  varsayilan-kol", ...]
Actual:   [..., "EncoderPathOverride.Software  yalniz-disarida", "EncoderVendor.Software ...
```

`EncoderPathOverride.Software` üretimde hiçbir yerde okunmuyor — motor yolu yalnız
`== EncoderPathOverride.Hardware` ile sınıyor, `Software` ise ikili kararın öbür yarısı
olduğu için ada gerek kalmıyor. Üye T165 tur 1'de eklendi ve `OluUyeTests`in pinli ölü
üye kümesi güncellenmedi.

`tests/VidShrink.Tests/OluUyeTests.cs` bu sözleşmenin `owns` listesinde **değil**;
düzeltilmedi, bildiriliyor.
