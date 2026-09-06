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

Kendi saydim: `grep -c True .calisma/T172/k1-ham.txt` = 44 satir `AudioDropped=True`
(taranan 4x28=112 satirin 44'unde tetikliyor); tam liste ham dosyada satir satir
gorulebilir.

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

## K3 - Hedef hala tutuyor mu: KIRMIZI (3/5 kombinasyonda regresyon)

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

```
[probe] hasAudio=False kanal=0 audioBps=0
[plan] audioK=0 audioCodec=null notlar=CodecUpgradeRecommended,ExtremeRatioWarning,ResolutionReduced,TargetEnforcedTwoPass
[encode] basarili=True ciktiMB=0,39
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
| `PlanCalculatorTests\|SesTabaniTests\|ManualOverrideTests` | 122 (ham: `.calisma/T172/final-verify1.txt`; K6 testi eklenmeden once 121 idi, `.calisma/T172/k7-liste1.txt`) |
| `OluUyeTests\|LanguageTests` | 66 (ham: `.calisma/T172/k7-liste2.txt`) |

## Verify sonucu (nihai)

- `PlanCalculatorTests|SesTabaniTests|ManualOverrideTests`: **121/122 yesil, 1 kirmizi.**
  Kirmizi: `ManualOverrideTests.K1_VarsayilanT165OncesiMotorlaBirebirAyni` (1920x1080@30,
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
