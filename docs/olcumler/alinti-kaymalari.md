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
