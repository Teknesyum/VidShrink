# HandBrake Dalga 2 — Üçüncü Danışma Yanıtı

Girdi: `hb2c-soru.md`, `Saturation.cs`, `EncodeRunner.cs:170-341`, `SaturationTests.cs`, `TasmaKarariTests.cs:92-115`,
`hb.ps1:355-383`, `ci.yml` (ffmpeg kurulumu). Kod yazılmadı, ölçüm koşulmadı.

## Soru 1 — "Bayt Artmadı" Koşulu Girer, %1 Toleransla

**Hüküm.** `AtEncoderFloor`'a dördüncü koşul: `later.ActualMb <= earlier.ActualMb * 1.01` (adı sizin, `FloorBytesRise`
gibi bir sabit). Pencere `[0,95; 1,01] × earlier` olur.

**Gerekçe.** Tabanın tanımı "istek düşerken bayt tutunuyor"; bayt **artıyorsa** kodlayıcı isteği görmezden gelmiyor,
oran denetimi sapıtıyor — bu taban değil, gürültü. Ölçülmüş üç gerçek taban oranı 0,978 / 1,000 / 0,987; yanlış
pozitif 1,038. Tolerans şart, çünkü gerçek taban eşitliğin tam üstünde oturuyor (0,392→0,392); sıfır tolerans
bir sonraki koşumda 0,3921'de düşer. %1, karanlik'ın en geniş oynamasının (%2,2, aşağı yönlü) yarısı ve yanlış
pozitifin dörtte biri. VT sorusu boş: `comparable` yalnız 2-pass, VT bu dala hiç girmiyor.

İkinci bir etki daha var: artan `later.ActualMb` `StepLayoutDown`'ın ölçeğine giriyor, `sqrt(0,9×1,406/2,005) = 0,79`
— şişmiş sayı düzeni gereğinden fazla indiriyor. Kural 1 bunu da keser.

**CI kabul ölçütü.**
- Birim: `AtEncoderFloor((1174, 1.931, over), (855, 2.005, over))` → false; sınır çifti `(98, 0.392) → (29, 0.395)`
  true, `(29, 0.397)` false. Mutasyon: artış koşulu silinince 2,005 testi kırılır.
- `rampa` 1200 `e0-duzen`: izde "encoder floor" satırı **yok**, `Saturated` yok, teslim ≥ 0,703 MB (%50). e0 %75
  verdi ama rampada aynı 855k istek iki kolda 1,478 ve 2,005 MB üretti (%36 fark) — bu kaynakta bayt kapısı
  konmaz, yalnız iz biçimi pimlenir.
- Karanlik hücrelerinde deneme sayısı değişmez (100: 3, 300: 1, 1200: 1).

## Soru 2 — Geri Dönüş Kodu Yazılmaz

**Hüküm.** Düzen adımından sonra çöküş için ayrı koruma girmez; mevcut ölü verim kuralı kalır.

**Gerekçe.** Üç sebep. (1) Tek gözlenen örnek Kural 1'le ortadan kalkıyor; ikinci bir örnek yok. (2) Çöküşün
kendisi kaynağın ürünü: 855k ve 1002k'da **aynı** 0,116 MB — kodlayıcı isteği hiç okumuyor. Titreşimli rampa
1558x876'ya inince dither ortalamaya gidiyor, geriye kodlanacak bir şey kalmayan düz bir gradyan kalıyor. Hiçbir
filmde küçültme içeriği sıfırlamaz; bu bir ürün durumu değil, yapay kaynağın imzası. (3) Geri dönüş tavan üstü
dosyayı ister; o dosya siliniyor (`EndRun`/`TryDelete`), yani "geri dön" beşinci bir kodlama demek. Ölçülmemiş
durum için deneme bütçesi büyütülmez.

Kod yerine kayıt: mevcut yol sessiz değil, `Saturated` yazıyor ve dört denemede duruyor
(`usedDeadYieldStep` → `EndRun`). Gerçek bir kaynakta taban→düzen→ölü verim zinciri görülürse o zaman ölçülür.

**CI kabul ölçütü.** Yeni hücre yok. Pim: `rampa` 1200 `e0-duzen` Kural 1 sonrası deneme ≤ 3, izde "did not
answer" satırı yok. Bu satır bir daha görünürse hücre kırmızı değil "ölçülmedi, kaynak yapay" yazılır.

## Soru 3 — Ölçüt e0-duzen'e Taşınır, Yol Testte Kalır, Açık 5 Kapanır

**Hüküm.** Önceki ölçüt yanlış kola yazılmıştı; e0 kolu `--no-resolution-drop` + `-an` ile yapısal olarak düzen
adımı alamaz, bu bir kusur değil tanım. Taban→düzen yolunu koşturan yeni bir CI hücresi **gerekmez**; bayrak da
icat edilmez.

**Gerekçe.** Planın kendisi 1036x442'yi seçip ilk denemede bantta kaldı — bu ürünün **doğru** davranışı; taban
adımı planın arkasındaki emniyet kemeri. "Plan 1920x818'de başlasın" bayrağı ürün koduna test iskelesi sokar ve
planı bilerek yanıltarak hiçbir ürün özelliğini ölçmez. Yolun kendisi iki yerde gerçek kodlayıcıyla koşuyor:
`TasmaKarariTests.SonDenemedeDeSorulur…` (4 sn kaynak, 0,001 MB hedef, iz dalı pimli; `ci.yml` ffmpeg 9.0
kurduğu için CI'da atlanmıyor) ve `SaturationTests` (ardışık küçük `Correct` adımlarında en erken örnekten
tespit, mutasyonla doğrulanmış). Yeterli.

Dürüst hücre bayrakla değil veriyle bulunur: mevcut bantlasma/dusuk CSV'lerinde 2-pass'te ≥ 2 tavan üstü deneme
yapan gerçek bir hücre var mı, bir kez taranır. Yoksa belgeye "ürün yolunda taban adımını tetikleyen gerçek
kaynak ölçülmedi" yazılır; çıkarsa hücre eklenir.

**CI kabul ölçütü** (`bantlasma`, `karanlik`):
- 100 `e0-duzen`: dosya var, ≤ 0,117 MB, bantta, **1 deneme**, yükseklik < 818. CAMBI 7,47 kapı değil kayıt.
- 100 `e0`: dosya yok, `CeilingExceeded`, **3 deneme** (4 değil — ek deneme yalnız taban adımı varken), izde
  "encoder floor" satırı yok. Yapısal negatif.
- 300 `e0-duzen` ve `e0`: 1 deneme, bantta; e0 1920x818.
- 1200 üç kol: 1 deneme.
- Birim + `TasmaKarariTests` iz dalı: `--filter` ile yeşil, CI'da atlanmadan koşar (ffmpeg adımı sabit).

**Açık 5 cümlesi (kapat).** "Taban→düzen yolu üründe planın arkasında emniyet kemeri: karanlik 100'de plan
1036x442'yi seçip ilk denemede bantta (0,118 MB, koşum 35173328586). Yol gerçek ffmpeg'le `TasmaKarariTests`'te ve
birimde koşuyor; CI'da pozitif hücre `e0-duzen` (≤ 0,117 MB, 1 deneme), yapısal negatif `e0` (dosyasız, 3 deneme).
Ürün yolunda adımı tetikleyen gerçek kaynak ölçülmedi; bulunursa hücre eklenir, bayrak eklenmez."

## Doğrulanacaklar

Rampa 855k'da 1,478 / 2,005 farkının kaynağı (aynı istek, aynı düzen, aynı girdi): SVT oran denetimi mi, pass-log
paylaşımı mı — iki kol sıralı koştuğu için çakışma beklenmez ama 36 puan büyük, bir kez bakılır. 0,116 MB'ın
1558x876'da içerik kaybı olduğu: sabit `-b:v 855k` ile 1920x818 ve 1558x876 tek kodlama, bayt oranı. Sınır testinde
1,01 çarpanının ondalık yuvarlamaya takılmadığı (0,392 × 1,01 = 0,39592).
