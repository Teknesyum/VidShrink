# Alıntı kaymaları — T126

Denetim aracı: `tools/alinti-denetimi/alinti-denetimi.py` (T124). Bu belge yedi kaymanın
düzeltmesini kaynaktan doğrulanmış haliyle ve araç çıktısıyla kaydeder.

## K1 — Başlangıç durumu

Düzeltmeye başlamadan önce:

```
SATIR KAYDI  docs/inceleme/argumanlar.md:56  -> ConversionArguments.cs:56
    `palettePath = outputPath`
    src/VidShrink.Core/ConversionArguments.cs icinde var ama :62, kunye :56

KAYMA  docs/inceleme/handbrake-motoru.md:347  -> docs/olcumler/auto-mod.md:209
    `HandBrakeCLI -e x265_10bit --encoder-preset slow --multi-pass --turbo \   -E ca_aac -B 128 -w...`
    docs/olcumler/auto-mod.md icinde yok (cit blogu)

SATIR KAYDI  docs/inceleme/model-strateji.md:46  -> CompressionStrategy.cs:40
    `targetMb <= 0`
    src/VidShrink.Core/CompressionStrategy.cs icinde var ama :48, kunye :40

KAYMA  docs/inceleme/plancalculator.md:94  -> EncodeRunner.cs:62
    `actual < LowerMb`
    src/VidShrink.Ffmpeg/EncodeRunner.cs icinde yok

KAYMA  docs/inceleme/uygulama-katmani.md:54  -> EncodeRunner.cs:185
    `ct.Register(TryKill)`
    src/VidShrink.Ffmpeg/EncodeRunner.cs icinde yok

KAYMA  docs/inceleme/uygulama-katmani.md:90  -> LanguageCatalog.cs:7
    `"Target Size Media Compression & Media Converter"`
    src/VidShrink.App/LanguageCatalog.cs icinde yok

SATIR KAYDI  docs/olcumler/ab-duzenegi.md:556  -> src/VidShrink.Ffmpeg/QualityMeter.cs:147
    `var harmonic = scores.Count / scores.Sum(x => 1.0 / Math.Max(x, 1.0));`
    src/VidShrink.Ffmpeg/QualityMeter.cs icinde var ama :241, kunye :147

KAYMA  docs/olcumler/surecler-arasi-olcu-yalitimi.md:116  -> LanguageTests.cs:13
    `[assembly: CollectionBehavior(DisableTestParallelization = true)]`
    tests/VidShrink.Tests/LanguageTests.cs icinde yok

belge: 98  denetlenen iddia: 9  atlanan: 39
KAYMA: 5  SATIR KAYDI: 3  SUPHELI: 0
```

Taban tutuyor (`KAYMA: 5  SATIR KAYDI: 3`), kontrat metniyle uyuşuyor.

## K2 — Yedi düzeltme, kaynaktan doğrulanmış

Her satır kendi gözlemimle; denetçinin tablosu kopyalanmadı.

**1. `docs/inceleme/argumanlar.md:56`** — `ConversionArguments.cs:56` → `:62`.
`src/VidShrink.Core/ConversionArguments.cs:62` içinde `palettePath = outputPath`
(`plan.Gif` dalındaki `a.AddRange(...)` içinde) doğrulandı. Tablo doğru.
Commit `39a877e`.

**2. `docs/inceleme/handbrake-motoru.md:347`** — `auto-mod.md:209` → `:231`, ayrıca
alıntılanan komut aslında **tek satır**; belgedeki `\` ile ikiye bölünmüş hali sahte.
`docs/olcumler/auto-mod.md:231` içinde `HandBrakeCLI -e x265_10bit --encoder-preset slow
--multi-pass --turbo -E ca_aac -B 128 -w 1920 -l 1080 --crop-mode none -r 60 --cfr -b 1900`
tek satırda doğrulandı. Tablo doğru, ek olarak satır kırılması da düzeltildi (tablo bunu
söylemiyordu, kendi gözümle gördüm). Commit `bb4e317`.

**3. `docs/inceleme/model-strateji.md:46`** — `CompressionStrategy.cs:40` → `:48`.
`src/VidShrink.Core/CompressionStrategy.cs:48`, `RegimeFor(double sourceMb, double
targetMb)` içinde `var ratio = targetMb <= 0 ? 1.0 : sourceMb / targetMb;` — bu metot
`ratio < 1.5` olduğunda `Light` döndürüyor, belgenin "sessizce **Light**" iddiasıyla
örtüşüyor. Aynı dosyada satır 56'daki ayrı `Ratio(...)` metodu **yanlış hedef** olurdu —
kontrol ettim, kullanmadım. Tablo doğru. Commit `e3f1242`.

**4. `docs/inceleme/plancalculator.md` madde 6** — `EncodeRunner.cs:62` → `:92`, alıntı
`actual < LowerMb` → `actualMb < band.LowerMb`. `src/VidShrink.Ffmpeg/EncodeRunner.cs:92`:
`var belowBand = !over && fillPolicy == FillPolicy.FillTarget && actualMb < band.LowerMb;`
— çevresi (85-99) `belowBand` → `underBand` → yeniden deneme zincirini doğruluyor. Tablo
doğru, hem satır hem alıntı metni yanlıştı, ikisi de düzeltildi. Commit `2e37025`.

**5. `docs/inceleme/uygulama-katmani.md:54`** — `EncodeRunner.cs:185` → `:269`, alıntı
`ct.Register(TryKill)` → `ct.Register(() => TryKill(process))`.
`src/VidShrink.Ffmpeg/EncodeRunner.cs:269`: `using var cancellationRegistration =
ct.Register(() => TryKill(process));`; `:185` bir `finally` bloğu, ilgisiz. Tablo doğru.
Commit `200bf82`.

**7. `docs/olcumler/surecler-arasi-olcu-yalitimi.md:116`** — `LanguageTests.cs:13` →
`:17`, `Xunit.` ön eki eklendi. `tests/VidShrink.Tests/LanguageTests.cs:17`:
`[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]`. Tablo doğru.
Commit `23f4d3b`.

## K3 — 6 numara: uydurma künye, uydurma künyeyle değiştirilmedi

`docs/inceleme/uygulama-katmani.md:90` — iddia edilen `LanguageCatalog.cs:7` dizgesi
(`"Target Size Media Compression & Media Converter"`) kaynakta yok; `LanguageCatalog.cs:7`
gerçekte bir `// T27:` yorum satırı.

Cümlenin ne anlattığını önce anladım: bulgunun tamamı — `LanguageCatalog.cs`'in bir
`TurkishToEnglish` çeviri sözlüğü olduğu, XAML'in `MainWindow.xaml` (`Run` elemanlarına
bölünmüş) olduğu — bugünkü mimariyle **uyuşmuyor**:

- `src/VidShrink.App/LanguageCatalog.cs` çeviri sözlüğü değil, başlık büyütme yardımcısı
  (`Title(text, turkish)`, `Names` sabit-yazım tablosu). `grep -rn "TurkishToEnglish" src/`
  → sıfır sonuç.
- Uygulama artık WPF değil **Avalonia** (`.axaml`, `MainWindow.xaml` değil
  `MainWindow.axaml`), yerelleştirme `src/VidShrink.App/Locales/{en,tr}/*.json` (387
  anahtar) + `{loc:Text key}` işaretleme uzantısı üzerinden çalışıyor
  (`Localization/Text.cs`, `Localization/Strings.cs`).
- Belgenin örnek verdiği "başlık" bugün `MainWindow.axaml:65-67`'de iki ayrı `TextBlock`
  ile `main.tagline.lead` / `main.tagline.converter` anahtarlarına bağlı; her iki anahtar
  da **aktif kullanılıyor** ve iki dilde de doğru çevrili (`Locales/en|tr/main.json:4-5`).
  Yani "ölü, İngilizce kalmış birleşik anahtar" iddiası bu örnek için yanlış.

Davranış (gerçekten kullanılmayan JSON anahtarları var mı) hâlâ doğru olabilir mi diye
387 anahtarı `src/VidShrink.App` altındaki tüm `.axaml`/`.cs` kullanımına karşı taradım:
**3 gerçek ölü anahtar** bulundu — `main.plan.fact.estimated-size`,
`main.plan.reasons-count`, `main.quality.loss-points` (hepsi `Locales/en/main.json:126,
117,70`'de tanımlı). Kod bunların yerine benzer adlı **aktif** anahtarları kullanıyor:
`main.plan.fact.estimate`, `main.plan.reasons`, `main.quality.loss`+`main.quality.points`
(`MainWindow.xaml.cs:1788,1796,1704` — bkz. doğrulama grep çıktısı, `.calisma/T126/`
kalıcı değil ama komut burada tekrarlanabilir: `grep -rn "plan.fact\.\|plan.reasons\|
quality.loss" src/VidShrink.App --include=*.cs --include=*.axaml`).

Sonuç: uydurma künye yazmadım. Cümleyi düzelttim — "Ölü anahtarlar (2)" → "(3)", örnek
dizge ve künye, gerçekten ölü olan üç JSON anahtarıyla ve doğru dosya/satırlarıyla
değiştirildi. Commit `200bf82` (5 numarayla aynı commit, ikisi de `uygulama-katmani.md`
içinde bitişik).

T0'ın geri bildirimiyle (yanlış atfı sessizce silme, "sanılıyordu/değil/gerçek yer"
biçiminde açıkça yaz) madde `f155dea`'de yeniden düzenlendi: eski yanlış iddia
("`LanguageCatalog.cs:7` bir çeviri sözlüğünün ölü anahtarı") **silinmedi**, önce ne
sanıldığı ve neden yanlış olduğu ("o dizge kaynakta hiç yok, satır 7 bir `// T27:` yorum
satırı, `LanguageCatalog.cs` hiçbir zaman çeviri sözlüğü olmadı") yazıldı, ardından
çevirilerin gerçek yeri (`Locales/{en,tr}/*.json`) ve gerçek ölü anahtarlar belirtildi.

> **Geri çekildi (bkz. "Tur 3" bölümü aşağıda).** Buradaki "hiçbir zaman çeviri
> sözlüğü olmadı" ifadesi tur 1 denetiminde KALDI aldı — `LanguageCatalog.cs`
> gerçekten bir çeviri sözlüğü taşımıştı (`19af115`). İki ayrı olay var, tek cümlede
> birleştirilmemeli: sözlük *mekanizması* (`EnglishToTurkish`/`TurkishToEnglish`/
> `Localize`) `b976332`'de (T83, 2026-08-30) kaldırıldı; alıntılanan spesifik *dizge*
> ise ayrı ve daha erken bir olay — kaynaktan `774b187`'de (2026-08-22, Avalonia
> geçişi) düştü, mekanizmadan sekiz gün önce. Tur 2'de "künye doğruydu, bayatladı"
> olarak düzeltildi, ama tur 2'nin kendi "hangi commit kaldırdı" iddiası da yanlış
> çıktı (bkz. Tur 3). Doğru hâli `docs/inceleme/uygulama-katmani.md` madde 90'da
> duruyor.
Yeniden kosturma sonrası araç bu paragraftan **KAYMA üretmedi** (eski yanlış dizge hâlâ
metinde ama artık bir künyeye bitişik "iddia" olarak okunmuyor — `KAYMA: 0  SATIR
KAYDI: 1` (T125'in bulgusu), `.calisma/T126/mutasyon-sonuc.txt`'ten sonraki son kosum).

**T0'a bildirilecek:** Bu bulgunun ötesinde, `## 4. Çeviri bütünlüğü` bölümünün tamamı
(satır 87-88, 3, 17) hâlâ aynı eski mimariyi anlatıyor — "112 anahtar, 112 farklı değer",
"`TurkishToEnglish` ters sözlüğü (`LanguageCatalog.cs:121`)", "`LanguageCatalog` 112 çift
tutar". Bu satırlar T126'nın yedi bulgusuna dahil değildi (araç onları format-A/B ile
yakalamadı, çünkü arkalarında alıntılanmış bir dizge yok) ama aynı yanlış öncülü
tekrarlıyorlar. Dokunmadım — kapsam dışı, tek satır not olarak K7'de de var.

## K4 — Son durum, araçla pimlenmiş

Yedi düzeltme sonrası (mutasyon testinden önceki temiz hal):

```
SATIR KAYDI  docs/olcumler/ab-duzenegi.md:556  -> src/VidShrink.Ffmpeg/QualityMeter.cs:147
    `var harmonic = scores.Count / scores.Sum(x => 1.0 / Math.Max(x, 1.0));`
    src/VidShrink.Ffmpeg/QualityMeter.cs icinde var ama :241, kunye :147

belge: 98  denetlenen iddia: 7  atlanan: 40
KAYMA: 0  SATIR KAYDI: 1  SUPHELI: 0
```

Kalan tek bulgu `ab-duzenegi.md:556` — T125'in `owns`unda, dokunmadım. Yedi bulgu düştü,
sıfır bulgu çıkmadı (çıksaydı `owns` ihlali anlamına gelirdi, durup bildirecektim).

## K5 — Susturulan üç formül

Aracın `duzyazi-formulu`/`sozdizim-yok` diye atladığı üç iddiayı `--atlananlar` ile
listeleyip tek tek açtım:

- **`docs/inceleme/handbrake-motoru.md:386`** (kontrat `:387` diyor — T126'nın kendi
  düzeltmesi #2, tek satırlık komuma indirgeme, dosyayı bir satır kaydırdı; bu benim
  kendi işimin yan etkisi, ayrı bir kayma değil) → `FfmpegArguments.cs:162` — `-g =
  max(2, round(fps × 2))`. `src/VidShrink.Core/FfmpegArguments.cs:162` içinde bu formülün
  karşılığı var (kod matematik notasyonuyla değil C# ifadesiyle yazıldığı için araç
  string eşleşmesi arayamıyor, doğru susturma).
- **`docs/inceleme/plancalculator.md:91`** → `ComplexityProfile.cs:142` — `bppf =
  reference · 2^((refCrf − crf)/step)`. Satır 142'nin kendisi (`WithoutSampleContainerBias`
  imzası) bu formülü içermiyor ama kavram dosyada dağınık halde gerçekten var:
  `ReferenceBppf`, `HalvingStep`, `Math.Pow(2, ...)`, `Math.Log2(...)` (satır 106, 114,
  161, 189, 226). Doğru susturma — formül prose, kod birebir dize değil.
- **`docs/olcumler/testler.md:81`** → `DiskSpaceGuardTests.cs:8-21` — `hedef*3 + 200 MB`
  formülü. Araç bunu `duzyazi-formulu` değil **`sozdizim-yok`** diye atlıyor (kontrattaki
  üçünün aynı sebeple susturulduğu iddiası tam doğru değil, sebep kodu farklı). İçerik
  yine de doğrulandı: `tests/VidShrink.Tests/DiskSpaceGuardTests.cs:17-21`
  `RequiredBytesMatchesTargetTimesThreePlusTwoHundred` → `Assert.Equal(500L * 1024 * 1024,
  DiskSpaceGuard.RequiredBytes(100))`, yani `100*3+200=500` — formül doğru.

Üçü de belgenin kendi formülü, doğru susturuluyor. Dokunmadım.

## K6 — Ters mutasyon

İki düzeltilmiş künye bilerek bozuldu, araç koşturuldu, ikisi de yakalandı, geri alındı.
Ham çıktı `.calisma/T126/mutasyon-sonuc.txt`'te duruyor (silinmedi).

1. **Satır kaydı mutasyonu:** `argumanlar.md:56`'daki `ConversionArguments.cs:62` →
   `:56`'ya geri bozuldu.
2. **Dizge hatası mutasyonu:** `uygulama-katmani.md:54`'teki `ct.Register(() =>
   TryKill(process))` → eski yanlış hali `ct.Register(TryKill)`'e geri bozuldu.

Araç çıktısı:

```
SATIR KAYDI  docs/inceleme/argumanlar.md:56  -> ConversionArguments.cs:56
    `palettePath = outputPath`
    src/VidShrink.Core/ConversionArguments.cs icinde var ama :62, kunye :56

KAYMA  docs/inceleme/uygulama-katmani.md:54  -> EncodeRunner.cs:269
    `ct.Register(TryKill)`
    src/VidShrink.Ffmpeg/EncodeRunner.cs icinde yok

SATIR KAYDI  docs/olcumler/ab-duzenegi.md:556  -> src/VidShrink.Ffmpeg/QualityMeter.cs:147
    `var harmonic = scores.Count / scores.Sum(x => 1.0 / Math.Max(x, 1.0));`
    src/VidShrink.Ffmpeg/QualityMeter.cs icinde var ama :241, kunye :147

belge: 98  denetlenen iddia: 7  atlanan: 40
KAYMA: 1  SATIR KAYDI: 2  SUPHELI: 0
```

İkisi de yakalandı (biri SATIR KAYDI, biri KAYMA — beklenen sınıflarda). `git checkout --
docs/inceleme/argumanlar.md docs/inceleme/uygulama-katmani.md` ile geri alındı, K4'teki
temiz duruma dönüldüğü doğrulandı.

## K7 — Ölçülmeyenler

- **Künye doğru, cümle yine de yanlış olabilir mi — ölçülmedi.** Yedi künyenin hepsi artık
  kaynağı doğru gösteriyor, ama etraflarındaki cümlenin **anlamı** ayrıca doğrulanmadı
  (K2'de yaptığım okuma bunun kısmi bir kontrolü, sistematik değil). En riskli olan #6:
  künye artık gerçek, ama `## 4. Çeviri bütünlüğü` bölümünün geri kalanı (satır 3, 17,
  87-88) hâlâ eski `LanguageCatalog`-sözlük mimarisini anlatıyor — bkz. K3'ün sonundaki
  T0 notu. Bölümün tamamını yeniden yazmak bu sözleşmenin kapsamı dışında, dokunulmadı.
- **`.xaml`/`.axaml` uzantı tutarsızlığı — düzeltilmedi.** `docs/inceleme/uygulama-katmani.md`
  boyunca `MainWindow.xaml`/`MainWindow.xaml.cs` yazıyor, gerçek dosya adı
  `MainWindow.axaml`/`MainWindow.axaml.cs` (Avalonia, WPF değil). Bu belgenin **kendi
  tutarlı üslubu** (baştan sona aynı şekilde yazılmış), K3'teki yeni citations da aynı
  üslubu taklit etti. Dokunulmadı — kapsam dışı, ayrı bir sözleşme konusu.
- **K5'in üç susturmasının davranışı, formülün kendisinin doğruluğu dışında bir şey
  ölçmüyor.** Örn. `plancalculator.md:91`'deki isabet oranı tahmini (%23) ayrıca
  doğrulanmadı, yalnız formülün kaynakta var olduğu doğrulandı.
- **`--supheli` (format C) taraması bu sözleşmenin dışında.** Aracın normal modu (format
  A/B) dışındaki şüpheli-blok bulguları K5 dışında incelenmedi.

## Bulgu — kontrat tablosundan sapma

K5'te not edildiği gibi iki küçük sapma bulundu (düzeltme değil, kayıt):
1. `handbrake-motoru.md:387` → gerçekte `:386` (T126'nın kendi #2 düzeltmesinin yan etkisi).
2. `testler.md:81`'in susturma sebebi kontratın dediği `duzyazi-formulu` değil,
   `sozdizim-yok` — içerik yine de doğru, sebep kodu farklı.

## Tur 2 — K8, K9, K10, K11

Denetim tur 1 **KALDI**: 6 numaranın düzeltmesi eski kaymayı yeni bir kaymayla
değiştirmişti — `uygulama-katmani.md:92-93` "`LanguageCatalog.cs` hiçbir zaman çeviri
sözlüğü olmadı" diyordu, oysa depo geçmişi tam tersini gösteriyor.

### K8 — teşhis düzeltildi

Madde 90 artık "uydurmaydı" demiyor. Doğru teşhis: künye `19af115` commit'inde
**doğruydu**, `b976332` (T83, 2026-08-30) çeviri sözlüğünü kaldırınca **bayatladı**.
"hiçbir zaman" ifadesi kaldırıldı. Üç ölü JSON anahtarı ve `Locales/{en,tr}/*.json`
yeri değişmedi (onlar zaten doğruydu). Commit `6d7ce6b`.

### K9 — çelişki kapatıldı

Satır 87-88 hâlâ `TurkishToEnglish` ters sözlüğünü var sayıyordu; madde 90 ile aynı
listede iki zıt iddia duruyordu. Bullet başlığına "(T83 öncesi — artık geçersiz)"
eklendi, gövdeye sözlüğün `b976332` ile kaldırıldığı ve bugünkü karşılığının
`Locales/{en,tr}/*.json` olduğu yazıldı. Commit `6d7ce6b` (K8 ile aynı commit, bitişik
satırlar).

### K10 — teşhis kaynağından üretildi

Zorunlu komut ve çıktısı:

```
$ git log -S 'Target Size Media Compression & Media Converter' --all -- src/ --oneline
commit 774b18715fc799638440ee43159fe018008a2f91
    Move the interface to Avalonia and reach three platforms
commit 19af1155c4989e7cc2f1686d351ab82bf3ad6051
    Update product description wording
```

`19af115` dizgeyi ekleyen/değiştiren commit; `774b187` (Avalonia geçişi) dizgeyi
kaldırdı — ama gerçek kaldırma T0'ın belirttiği gibi `b976332` (T83) ile, sözlüğün
tamamının silinmesiyle oldu (`774b187` WPF→Avalonia geçişinde LanguageCatalog zaten
başka bir haldeydi; kronolojik sıra `19af115` → ... → `b976332` → ... → `774b187`).

**Tur 4 düzeltmesi — yukarıdaki cümle yanlış (silinmiyor, çürütülüyor).** İlk yarısı
doğru: `774b187` gerçekten dizgeyi kaldırdı. Ama "ama gerçek kaldırma ... `b976332`
ile ... oldu" ve tersine çevrilmiş kronoloji (`19af115` → `b976332` → `774b187`)
yanlış — iki ayrı olayı (dizge ve sözlük *mekanizması*) tek olay saymış, sırayı da
tersine çevirmiş. `b976332`, dizgenin düşüşünden 488 commit sonra, sözlük
*mekanizmasını* kaldırdı; dizgeyle ilgisi yok, çünkü dizge o noktada zaten sekiz
gündür kaynakta değildi. Doğru kronoloji ve bağımsız komut kanıtı için bkz. aşağıdaki
"## Tur 3 — K12" bölümü, "Bağımsız doğrulama" alt bölümü — orada aynı komutlar ayrıca
koşturulup doğru sıra (`19af115` → `774b187` → `b976332`) kayıt altına alındı.

**Diligence — diğer üç "kaynakta hiç yok" iddiası da aynı komutla tekrar kontrol edildi**
(K10'un zorunlu kıldığı yalnız 6 numaraydı, kalanlar ek özen):

```
$ git log -S 'actual < LowerMb' --all -- src/ --oneline
(sonuç yok — hiç var olmadı)

$ git log -S 'ct.Register(TryKill)' --all -- src/ --oneline
(sonuç yok — hiç var olmadı)

$ git log -p --all -- tests/VidShrink.Tests/LanguageTests.cs | grep CollectionBehavior
238: [assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
659: [assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
922:+[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
(satır dosyanın tüm geçmişinde hep "Xunit." önekiyle var — hiç bayatlamamış)
```

Üçü de gerçekten "hiç yok" — 6 numaranın aksine "artık yok" değil. K2'deki düzeltmeler
değişmedi.

### K11 — aracın duyarlılık tavanı

`KAYMA: 0` bir kapanış değil. Araç yalnız **format A/B** görüyor: bir `dosya:satır`
künyesinin hemen ardından backtick içinde alıntılanmış bir dizge geldiğinde, o dizgeyi
hedef dosyada arıyor. **Çıplak `dosya:satır` atıflarını** — künyenin arkasından alıntı
gelmeyen, yalnız davranışı düzyazıyla anlatan atıfları — hiç denetlemiyor. Bu sınıf
kayma araçla yakalanamaz; yalnız kaynağı elle açıp okumakla bulunur.

Denetçinin elle bulduğu, araçın görmediği üç kayma (**düzeltilmedi, yalnız kayıt
altına alınıyor** — bu üçü T126'nın yedi bulgusuna dahil değildi, `owns` içindeki
dosyalarda ama farklı bir kusur sınıfı):

1. `docs/inceleme/model-strateji.md` "§2.4" — `PlanCalculator.cs:92`'ye atıfta bulunup
   `totalK` 0 çıkınca `MinVideoBitrateK` tabanına düşme davranışını anlatıyor.
   `PlanCalculator.cs:92` gerçekte `DeliveryReserveK(codec)` yardımcı metodu, ilgisiz;
   gerçek davranış `:155-157`'de (`totalK = aimMb * KbitPerMib / ...`, `videoK =
   Math.Max(MinVideoBitrateK, totalK * ContainerOverhead - ...)`) — **63 satır fark**.
2. Aynı belge "§2.3" — `CompressionStrategy.cs:41-43` künyesinden sonra kısaltılmış
   `:57` ile `AllowsResolutionDrop`'a atıfta bulunuyor. Gerçek satır `:65`
   (`public static bool AllowsResolutionDrop(...) => regime != CompressionRegime.Light;`).
3. `docs/inceleme/plancalculator.md` madde 6-7 — `EncodeRunner.cs:92` künyesinden sonra
   kısaltılmış `:295` (`Correct` 2pass'e geçiş iddiası) ve `:271` (`RetryAimMb` ölçümlü
   dalı iddiası) ve `EncodeRunner.cs:64` (4. deneme yok iddiası) ile devam ediyor.
   Üçü de yanlış hedefte: `:295` gerçekte ffmpeg `out_time_ms` ilerleme ayrıştırması,
   `:271` gerçekte `var stderrTask = Task.Run(async () => ...)` — stderr okuma görevinin
   başlangıcı, ilgisiz (5 numaralı düzeltmenin kendi hedefi olan `ct.Register(() =>
   TryKill(process))` iki satır yukarıda, `:269`'da — bu satır burada yanlışlıkla `:271`
   diye yazılmıştı, düzeltildi), `:64` gerçekte metot başındaki yerel değişken
   tanımları. Üçü de anlatılan davranışla ilgisiz.

Ortak desen: **kısaltılmış bare `:N` künyeler**, önceki tam künyenin dosyasını miras
alıyor ama satır numarası bağımsız kayıyor; format A/B bunları hiç görmüyor çünkü
aralarında alıntılanmış bir dizge yok, yalnız düzyazı iddiası var.

## Tur 3 — K12 (kaldırıcı commit yanlış teşhis edildiydi)

Tur 2 kaldı: madde 90'a yazdığım "sözlük `b976332` (T83) ile kaldırılınca dizge
kaynaktan düştü" cümlesi **yanlıştı.** Kaldıran commit `774b187` (2026-08-22, Avalonia
geçişi) — `b976332` (2026-08-30, T83) değil. Bu iki olayı tek olay sanmışım; hatanın
kaynağı T0'ın tur 2 talimat metnindeki bir yanlış varsayımdı ama **çürüten kanıt
kendi koşturduğum `git log -S` çıktısındaydı** — çıktı `774b187`'yi gösteriyordu, ben
onu görmezden gelip talimattaki sıralamayı yazdım. Ders: elimdeki veri talimatla
çelişince veriyi tutmalıydım.

### Bağımsız doğrulama (kendi kendime tekrar koşturdum, T0'ın verdiği komutları
kopyalamadım — aynı sonuca ayrı ayrı vardım)

```
$ git log -S 'Target Size Media Compression & Media Converter' --all --format='%h %ad %s' --date=short -- src/
774b187 2026-08-22 Move the interface to Avalonia and reach three platforms
19af115 2026-08-17 Update product description wording

$ git grep -c 'Target Size Media Compression & Media Converter' 774b187^ -- src/
src/VidShrink.App/LanguageCatalog.cs:1        <- var (774b187'den ONCE dizge var)

$ git grep -c 'Target Size Media Compression & Media Converter' 774b187 -- src/
(eslesme yok, exit 1)                          <- 774b187'nin KENDISINDE yok

$ git grep -c 'Target Size Media Compression & Media Converter' b976332^ -- src/
(eslesme yok, exit 1)                          <- T83'ten (b976332) 1 commit ONCE de yok

$ git rev-list --count 774b187..b976332
488
```

Sonuç: dizge tam `774b187`'de düşüyor (öncesinde var, kendisinde yok). `b976332`'nin
bir commit öncesinde de zaten yoktu — T83 onu "kaldıramazdı", 488 commit önce zaten
kaynaktan düşmüştü. Doğru kronoloji: `19af115` (dizge var) → `774b187` (dizge düşüyor,
Avalonia geçişi) → … 486 commit … → `b976332`/T83 (çeviri sözlüğü *mekanizması*
kaldırılıyor, dizgeyle ilgisiz, çünkü dizge zaten yoktu).

### K12 — madde 90 çürütüldü, silinmedi

`docs/inceleme/uygulama-katmani.md` madde 90'daki yanlış sıralama cümlesi **silinmedi**;
yanlış olduğu, neden yanlış olduğu ve doğru kronoloji yukarıdaki komut çıktılarıyla
birlikte aynı yere yazıldı (T126, tur 3 etiketiyle). Commit aşağıda.

**Dokunulmadı (T0'ın işaretlediği doğru kalanlar):** `uygulama-katmani.md:87-88`'deki
"sözlük `b976332` ile kaldırıldı" cümlesi — bu, sözlük *mekanizması* için doğru, dizge
için değil, ikisi ayrı önerme; üç ölü JSON anahtarı; `Locales/{en,tr}/*.json` yeri.

### Genel kural (K12'nin istediği)

**Bir commit'i "şu dizgeyi kaldıran" ilan etmeden önce onu `git log -S '<dizge>' --all
-- <yol>` çıktısında görmüş olmak zorunludur.** Çıktıda görünmeyen bir commit'e kaldırma
atfetme — görünüyor olması bile yetmez, kaldırma iddiasını `git grep -c '<dizge>'
<commit>^ -- <yol>` (öncesinde var, çıktı ≥1) ile `git grep -c '<dizge>' <commit> --
<yol>` (kendisinde yok, çıktı 0/hata) çiftiyle doğrula. Tur 2'nin hatası tam bu adımı
atlamaktı: `git log -S` çıktısını gördüm ama çıktıdaki commit'i (`774b187`) değil,
talimatta verilen commit'i (`b976332`) yazdım.

### İki küçük düzeltme (T0'ın işaret ettiği)

1. K11'in 3. maddesindeki `EncodeRunner.cs:271` yanlış transkripsiyondu — gerçek satır
   `:269`. Yukarıdaki "K11" bölümünde düzeltildi (`var stderrTask = Task.Run(...)`
   gerçekten `:271`'de, `ct.Register(() => TryKill(process))` `:269`'da).
2. Tur 1'in geri çekilen "hiçbir zaman çeviri sözlüğü olmadı" ifadesinin göründüğü
   paragrafa (yukarıda, K3 bölümünde) geri çekildiğine işaret eden bir not eklendi —
   önceden yalnız 100 satır aşağıdaki "Tur 2" bölümünde geri çekiliyordu, yukarıdan
   okuyan fark etmiyordu.

## Tur 4 — K13 (mekanik tarama)

Danışman gerekçesi: KRİTİK 2, T0'ın hatasının kalıntısı değil, tur 3'ün kendi
düzeltmesinin geri tepmesiydi — "kaldıran commit `774b187`" dersi öğrenilip yanlış
cümleye (sözlük) taşındı. Bu turun kuralı: **cümleyi okuyup karar verme, komut
çıktısına bak.** `git grep -n -E "774b187|b976332" -- docs/` çıktısının tamamı (36
satır) aşağıda, satır satır, "dizge" ya da "mekanizma" etiketiyle.

### Kanıt blokları (tablo bunlara referans verir)

**Dizge** (`Target Size Media Compression & Media Converter`):
```
$ git grep -c '<dizge>' 774b187^ -- src/   →  1  (774b187'den önce var)
$ git grep -c '<dizge>' 774b187  -- src/   →  0  (774b187'nin kendisinde yok)
$ git grep -c '<dizge>' b976332^ -- src/   →  0  (b976332'den önce de yok)
$ git grep -c '<dizge>' b976332  -- src/   →  0
```
Kaldıran: **`774b187`**.

**Mekanizma** (`EnglishToTurkish` — sözlük/`Localize` kümesinin izi):
```
$ git grep -c EnglishToTurkish 774b187^ -- src/  →  3  (774b187'den önce var)
$ git grep -c EnglishToTurkish 774b187  -- src/  →  4  (774b187'nin KENDİSİNDE de var — kaldırılmadı)
$ git grep -c EnglishToTurkish b976332^ -- src/  →  5  (b976332'den önce var)
$ git grep -c EnglishToTurkish b976332  -- src/  →  0  (b976332'nin kendisinde yok)
```
Kaldıran: **`b976332`**.

### Tablo

| # | Konum | Etiket | Kanıt | Durum |
|---|-------|--------|-------|-------|
| 1 | uygulama-katmani.md:89 | mekanizma | mekanizma-bloğu | doğru |
| 2 | uygulama-katmani.md:97 | dizge | dizge-bloğu | doğru |
| 3 | uygulama-katmani.md:98 | dizge | dizge-bloğu | doğru |
| 4 | uygulama-katmani.md:99 | dizge | dizge-bloğu | doğru |
| 5 | uygulama-katmani.md:100 | dizge | dizge-bloğu | doğru |
| 6 | uygulama-katmani.md:101 | dizge | dizge-bloğu | doğru |
| 7 | uygulama-katmani.md:102 | dizge | dizge-bloğu | doğru |
| 8 | uygulama-katmani.md:103 | dizge | dizge-bloğu | doğru (tur 2'nin yanlış cümlesini alıntılayıp çürütüyor) |
| 9 | uygulama-katmani.md:104 | mekanizma | mekanizma-bloğu | doğru |
| 10 | uygulama-katmani.md:107 | dizge | dizge-bloğu | doğru |
| 11 | alinti-kaymalari.md:135 | mekanizma | mekanizma-bloğu | **YANLIŞTI → tur 4'te (a) ile düzeltildi** |
| 12 | alinti-kaymalari.md:259 | mekanizma | mekanizma-bloğu | doğru |
| 13 | alinti-kaymalari.md:267 | mekanizma | mekanizma-bloğu | doğru |
| 14 | alinti-kaymalari.md:277 | dizge | dizge-bloğu | doğru (git log -S ham çıktısı) |
| 15 | alinti-kaymalari.md:283 | dizge | dizge-bloğu | **YANLIŞTI → tur 4'te (b) ile çürütüldü** |
| 16 | alinti-kaymalari.md:284 | dizge | dizge-bloğu | **YANLIŞTI → tur 4'te (b) ile çürütüldü** |
| 17 | alinti-kaymalari.md:285 | dizge | dizge-bloğu | **YANLIŞTI → tur 4'te (b) ile çürütüldü** |
| 18 | alinti-kaymalari.md:286 | dizge | dizge-bloğu | **YANLIŞTI → tur 4'te (b) ile çürütüldü** |
| 19 | alinti-kaymalari.md:344 | dizge | dizge-bloğu | doğru |
| 20 | alinti-kaymalari.md:345 | dizge | dizge-bloğu | doğru |
| 21 | alinti-kaymalari.md:346 | dizge | dizge-bloğu | doğru |
| 22 | alinti-kaymalari.md:348 | dizge | dizge-bloğu | doğru |
| 23 | alinti-kaymalari.md:357 | dizge | dizge-bloğu | doğru (git log -S ham çıktısı) |
| 24 | alinti-kaymalari.md:360 | dizge | dizge-bloğu | doğru |
| 25 | alinti-kaymalari.md:361 | dizge | dizge-bloğu | doğru |
| 26 | alinti-kaymalari.md:363 | dizge | dizge-bloğu | doğru |
| 27 | alinti-kaymalari.md:364 | dizge | dizge-bloğu | doğru |
| 28 | alinti-kaymalari.md:366 | dizge | dizge-bloğu | doğru |
| 29 | alinti-kaymalari.md:367 | dizge | dizge-bloğu | doğru |
| 30 | alinti-kaymalari.md:369 | mekanizma | — (`774b187..b976332` mesafesi, 488) | doğru (bkz. "Düzelmeyecekler" — 486/487/488 tutarsızlığı burada değil, `:376`'da) |
| 31 | alinti-kaymalari.md:373 | dizge | dizge-bloğu | doğru |
| 32 | alinti-kaymalari.md:375 | dizge | dizge-bloğu | doğru |
| 33 | alinti-kaymalari.md:376 | mekanizma | mekanizma-bloğu | doğru (rakam "486" burada — bilinen tutarsızlık, bu tur dokunulmadı) |
| 34 | alinti-kaymalari.md:386 | mekanizma | mekanizma-bloğu | doğru |
| 35 | alinti-kaymalari.md:396 | dizge | dizge-bloğu | doğru |
| 36 | alinti-kaymalari.md:397 | dizge | dizge-bloğu | doğru (tur 2'nin `b976332` talimatını yanlış yazdığını anlatıyor) |

**Toplam 36 satır.** Dizge: 28 (satır 2-8, 10, 14-18, 19-29, 31-32, 35-36).
Mekanizma: 8 (satır 1, 9, 11-13, 30, 33-34). Yanlış çıkan: **2 iddia** — #11
(`:135`, tek satır) ve #15-18 (`:283-286`, tek cümle/4 satır) — ikisi de bu turda
(a) ve (b) ile düzeltildi, satır numaraları taramanın koşturulduğu commit'e göredir
(düzeltmeler eklendikten sonra dosyadaki gerçek satır numaraları kayar).

**Not:** satır 30 ve 33'teki "486/487/488" tutarsızlığı (gerçek sayı `git rev-list
--count 774b187..b976332` → **488**, doğrulandı) bu taramada görüldü ama K13'ün
"dokunulmayacaklar" listesinde — düzeltilmedi, mühür notuna borç olarak kalıyor.
