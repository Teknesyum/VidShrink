# Plan — Biçimlerin Tek ve Ulaşılabilir Gövdeye İnmesi

Kullanıcı isteği (18 Eylül 2026): *"düzenlerimiz tek ve belli ulaşılabilir bir yerde olmalı
aynı stringlerimizi çevirmek nasıl kolaysa bunları çevirmekte öyle kolay olmalı."*

Ölçüm: `docs/inceleme/bicim-govdeleri-2026-09-18.md` — **altı aile, 32 ayrı biçim,
~110 çağrı yeri, 95+ kültürsüz satır.** Süre ailesi `Core/Saat`'e indi (kod borcu 10);
bu plan **kalan beş aileyi** kapsıyor. Dokunulan dosya sayısı yirmiyi geçtiği için K0
gereği önce bu plan.

## Neden tek gövde

Bugün aynı megabayt değeri arayüzün farklı köşelerinde `0`, `0.#`, `0.##`, `0.0`, `0.00`,
`0.000`, `0.###` ve biçimsiz olmak üzere sekiz türlü yazılabiliyor. Bir biçimi değiştirmek
istendiğinde 110 çağrı yerinin hangisinin o yüzeye ait olduğu aranarak bulunuyor —
`Strings` kataloğunda bir dizgiyi değiştirmek tek satırken.

`MainWindow.Num(double, string)` gövde değil: biçim dizgisi **parametre** olduğu için
hiçbir aileyi tekleştirmiyor, yalnız kültür taşıyor. `CliApp.Num` ikizi ise farklı kültür
kullanıyor. Yani bugünkü "ortak yardımcı" tutarsızlığı gizliyor.

## Gövde: `src/VidShrink.Core/Bicim.cs`

`Saat` ile aynı desen: **adlandırılmış yüzeyler**, biçim dizgisi çağrı yerinden alınmaz.
Yüzeyin adı ne olduğunu söyler, ondalık sayısı gövdede yazılıdır ve tek yerden değişir.

Kültür dikişi: kullanıcıya görünen yüzeyler `CultureInfo` parametresi alır (App
`Strings.Culture`, Recorder `Strings.CultureOf(...)` verir). Motorun İngilizce tanı
metinleri için ayrı ve `InvariantCulture` sabitli `Bicim.Tani` grubu olur — bunlar
çevrilmiyor, kültüre göre virgül alırlarsa günlükler karşılaştırılamaz hale gelir.

| Yüzey | Ondalık | Neden bu ondalık |
|---|---|---|
| `Bicim.Boyut.Mb(double, culture)` | `0.0` | Ailenin baskın yazımı; kaynak/çıktı boyutu |
| `Bicim.Boyut.Hedef(double, culture)` | `0.##` | Hedef kullanıcının girdiği sayı; `50` yazdıysa `50,0` değil `50` görmeli |
| `Bicim.Boyut.Sapma(double, culture)` | `0.00` | Hedefle çıktı farkı küçük; `0.0` sapmayı sıfır gösteriyor |
| `Bicim.Boyut.Bayt(long, culture)` | ikilik `B/KiB/MiB/GiB/TiB` | `DescribeBytes` zaten böyle; 1024'e bölünce ikilik ad doğru olan |
| `Bicim.Yuzde(double oran, culture)` | `0.#` + `%` | `P1` tek çağrı yerinde ve `:3707`/`:4266` ile **aynı ekranda** çelişiyor; çoğunluk `0.#` |
| `Bicim.BitHizi.Kbps(int, culture)` | tam sayı + katalog birimi | Bit hızında ondalık hiç anlam taşımıyor |
| `Bicim.Cozunurluk(int w, int h)` | `W×H` (çarpı) | İki yazım var; `×` doğru tipografik işaret, `PlayerView` zaten öyle |
| `Bicim.Kare(double fps, culture)` | `0.##` | Baskın yazım `0.##`; `PlayerView.Window.cs:491`'in `0.###`'ü tek aykırı |
| `Bicim.Damga(DateTimeOffset, culture)` | `d MMMM HH:mm` | Üç yerde aynı dizgi elle kopyalanmış, biçim zaten tutarlı |
| `Bicim.DosyaDamgasi(DateTimeOffset)` | `yyyy-MM-dd_HH-mm-ss`, Invariant | Dosya adı; kültür girerse iki nokta ve yerel ay adı gelir |
| `Bicim.Tani.*` | aynı ondalıklar, **Invariant sabit** | Motorun İngilizce gerekçe metinleri |

## Düzeltilecek gerçek kusur

`src/VidShrink.Core/Share/ShareErrorClassifier.cs:266-278` **1024'e bölüp `KB/MB/GB`
yazıyor.** Birim adı yanlış: 1024 tabanında doğru ad `KiB/MiB/GiB`. Kullanıcı paylaşım
hatasında olduğundan ~%5 küçük görünen bir sayı okuyor. Bu, "kullanıcıya yalan yok"
işinin ilk somut maddesi ve gövdeye inerken kendiliğinden kapanıyor.

## Adımlar

1. `Core/Bicim.cs` yazılır; her yüzeyin docstring'i ondalık kararının **gerekçesini**
   taşır (`Saat` deseni). Kod yazılmadan önce `BicimTests` yazılır.
2. **Dosya boyutu ailesi** taşınır (en dağınık: 30+ çağrı yeri, 9 biçim, 3 birim tablosu).
   `ShareErrorClassifier.Size`, `MainWindow.DescribeBytes`, `ShellMenu.cs:239-240`,
   `ShellIntegration.cs:30-31` tek gövdeye iner; `ShareErrorClassifier` kusuru burada kapanır.
3. **Yüzde** taşınır. `*100` çarpımı çağrı yerlerinden gövdeye alınır — yüzey **oran**
   alır, yüzde üretir; 8 çağrı yerindeki elle çarpım düşer.
4. **Bit hızı** taşınır. `bps/1000` bölmesi tamsayı/double arasında tutarsız; gövde tek
   bölme yapar. Birim eki üç türlüydü (`kbps`, `k`, bitişik), katalog anahtarına bağlanır.
5. **Çözünürlük ve kare hızı** taşınır. `x` → `×` değişimi kullanıcıya görünen metinde;
   ffmpeg argümanı üreten `WxH` yazımlarına **dokunulmaz** (`ToolsOptions.cs:126` gibi).
6. **Tarih damgası** taşınır (en derli toplu aile, tek tutarsızlık
   `ShrinkJobWindow.Paylas.cs:115`'in farklı kültür kaynağı).
7. `MainWindow.Num`, `MainWindow.Percent`, `CliApp.Num`, `RecorderTray.Megabytes` kaldırılır.
   Kalan çağrı yeri olmadığı bir taramayla gösterilir.
8. Süre ailesinin `Saat` dışında kalan altı satırı (`RecorderView.Kirpma.cs:41`,
   `RecorderView.Otomatik.cs:150`, `PromptBuilder.cs:13`, `CeilingGuard.cs:53`,
   `MpvEngine.cs:235`, `RecorderView.Tepsi.cs:40`) `Saat`'e bağlanır.

Adımlar bağımsız değil: 1 bitmeden diğerleri başlamaz, 2-6 sırayla tek dalda gider.

## Ölçü

Her yüzey için `tests/VidShrink.Tests/BicimTests.cs`:

- Türkçe kültürde ondalık ayracı virgül, İngilizcede nokta (kültür dikişi gerçekten
  çalışıyor mu — `Saat`'te `InvariantCulture` mutasyonu 21/21 yeşilden geçmişti,
  aynı kör noktaya düşülmeyecek).
- `Bicim.Boyut.Bayt` 1024 tabanında **ikilik ad** yazıyor (pozitif ve negatif kontrol).
- Her yüzeyin ondalık sayısı pimli; ondalığı değiştirmek testi kırmalı.
- **Tarayıcı ölçüsü:** `src/` altında kullanıcıya görünen elle biçim kurma kalmadığını
  kaynak okuyarak sayan test (`BicimDisiYazimYok`). Desen taraması eksik saymasın diye
  beklenen küme dosyadan türetilir, elle listelenmez.

Mutasyon: her yüzeyin ondalığı ve kültür parametresi tek tek bozulup kaç test kırmızı
döndüğü ölçülür; sıfır kırmızı veren yüzey pimsiz sayılır ve testi yeniden yazılır.


### Adım 1 ölçümü — 19 Eylül 2026

`Core/Bicim.cs` + `BicimTests` yazıldı: **19/19 yeşil**. On üç mutasyon tek tek koşuldu,
**on üçü de kırmızı** döndü — pimsiz yüzey yok:

| Mutasyon | Kırmızı |
|---|---|
| `Mb` kültürü → Invariant | 1 |
| `Mb` ondalığı `0.0` → `0.00` | 3 |
| `Hedef` `0.##` → `0.0` | 1 |
| `Sapma` `0.00` → `0.0` | 1 |
| `Bayt` ikilik ad → ondalık ad (`KiB`→`KB`) | 7 |
| `Bayt` kültürü → Invariant | 1 |
| `Yuzde` `*100` çarpımı düşürüldü | 2 |
| `Kbps` ondalık eklendi | 1 |
| `Cozunurluk` `×` → `x` | 1 |
| `Kare` `0.##` → `0.###` | 2 |
| `Damga` kültürü → Invariant | 1 |
| `DosyaDamgasi` iki nokta | 1 |
| `Tani` Invariant → CurrentCulture | 1 |

Kültür dikişi ayrı ayrı pimlendi: `Saat`'te `InvariantCulture` mutasyonu 21 yeşilden
geçmişti, burada dört ayrı kültür mutasyonunun dördü de kırmızı.


### Adım 2 ölçümü — 19 Eylül 2026

Dosya boyutu ailesinin dört gövdesi `Bicim`'e indi: `ShareErrorClassifier.Size`,
`MainWindow.DescribeBytes`, `ShellMenu.TargetLabel`, `ShellIntegration.FormatQuickShrinkLabel`.
Son ikisi birbirinin kopyasıydı ve biri kültür veriyor, diğeri vermiyordu; ikisi de
`Bicim.HedefEtiketi`'ne indi. Derleme `-warnaserror` ile 0 uyarı 0 hata, 138 test yeşil.

**Kullanıcıya gösterilen yalan kapandı:** `ShareErrorClassifier` 1024'e bölüp `KB/MB/GB`
yazıyordu. İki test bu yanlış birimi **pimliyordu** (`ShareProviderTests` `"128 MB"`);
yani ölçü kusuru korumuştu. Pimler doğru birime çevrildi ve yanlış adın dönmediği
olumsuz kontrolle bağlandı.

| Mutasyon | Kırmızı |
|---|---|
| Paylaşım boyutu eski yalana döndürüldü | 2 |
| `DescribeBytes` kültürü düşürüldü | 1 |
| `ShellMenu` etiketi GB kolunu kaybetti | **0 → 1** |
| Kabuk etiketi yanlış sayı yazdı | 1 |

Üçüncü satır kör noktaydı: gerçek hedef listesinde 1024 ve 2048 var, yani GB kolu
üretimde koşuyor ama hiçbir test onu okumuyordu. `MenuEtiketiTekGovdedenGeliyor` yazıldı
— iki yazımın tek gövdeden geldiğini ve GB kolunu pimliyor; aynı mutasyon artık kırmızı.

### Adım 3 ölçümü — 19 Eylül 2026

Yüzde ailesi indi. `*100` çarpımı yedi çağrı yerinden gövdeye alındı; yüzey artık
**oran** alıyor, değeri zaten 0-100 taşıyan iki yer (`ScalePercent`, `OverPercent`)
ayrı bir kapıdan (`Hazir`) geçiyor — bu ayrım hiçbir yerde yazılı değildi ve ikinci
bir çarpım tek bir testi kırmıyordu.

Katalog dizgilerinin dördü yüzde işaretini kendi taşıyor (`{2}%`); o dört yerde
sayı-yalnız biçim kullanıldı, çıktıları birebir aynı kaldı. Değişen tek yüzey
`MainWindow.Percent`: `P1` tam değerde `%50,0` yazıyordu, aile yazımı `%50`.

| Mutasyon | Kırmızı |
|---|---|
| Kültür deseni yok sayıldı, işaret hep sona | 1 |
| İşaret kültürden değil sabit `%` | **0 → 1** |
| `Hazir` ikinci kez çarpıyor | 2 |
| `Tam` ondalık yazıyor | **0 → 1** |
| `Orandan` iki ondalık | **0 → 1** |

Üç kör nokta vardı ve hepsinin kökü aynı: tr/en/de üçü de `%` kullanıyor, üçünü
pimlemek işareti pimlemiyordu. On kültür ölçüldü (`.calisma/yuzde-olcu`); fa-IR
`٪` (U+066A) yazıyor. Ölçü o kola bağlandı.

### Adım 4 ölçümü — 19 Eylül 2026

Bit hızı indi: `MainWindow` iki bilgi satırı ve iki plan gerçeği, `PlayerView.Describe`,
kaydedici bütçe notu. İki gerçek kusur kapandı — `MainWindow`'un iki bilgi satırında
bölme **tamsayıydı ve kırpıyordu** (1.499.600 bps ekranda 1499 kbps), bütçe notu ise
`N0` ile basamak ayracı yazıp aynı birimi uygulamanın geri kalanından farklı
gösteriyordu.

Oynatıcı için önce "orada da kırpma var" yazmıştım; **yanlıştı ve ölçüm düzeltti**.
`MediaDetails.BitsPerSecond` `double`, yani oradaki bölme zaten ondalıklıydı ve `"0"`
biçimi yuvarlıyordu. Oynatıcının `CultureInfo.CurrentCulture` okuması da — tam sayıda
her kültür aynı yazımı verdiği için — ekranda hiçbir fark üretmiyordu. Oynatıcı
değişikliği bir düzeltme değil, **ortak gövdeye bağlama**dır.

**`BitHizi` kültür almıyor, çünkü alamaz.** Ölçüldü (`.calisma/bithizi-olcu`): .NET'in
bildiği **her** özgül kültürde `"0"` biçimi bu değerlere tek yazım veriyor. Kültür
parametresi tutulsaydı hiçbir mutasyonun kıramayacağı, yani hiçbir zaman
pimlenemeyecek bir söz olurdu; parametre kaldırıldı ve gerekçe gövdenin docstring'ine
yazıldı.

Mutasyon, **temiz tabanda** (0 kırmızı / 98 yeşil) ölçüldü. İlk tablo 1 kırmızılı
tabanda alınmıştı ve sayıları kirliydi; raporlanmadı, yeniden koşuldu.

| mutasyon | kırmızı |
| --- | --- |
| gövde kırpıyor (`Math.Round` → tamsayı bölme) | 4 |
| gövde basamak ayracı koyuyor (`"0"` → `"N0"`, tr-TR) | 8 |
| kaynak hızı gövdeye uğramıyor | 2 |
| ses hızı gövdeye uğramıyor | 2 |
| oynatıcı gövdeye uğramıyor | **0** |
| bütçe gövdeye uğramıyor | 1 |

Oynatıcının sıfırı bir kör nokta değil, **eşdeğer mutant**: kol eski ifadeye geri
döndürüyor, eski ifade de `double` bölüp `"0"` ile yuvarlıyordu — iki taraf da aynı
basamakları yazıyor, kıracak bir fark yok. Yüzeyin pimli olduğu bunun yerine gerçek
bir kusurla ölçüldü (`.calisma/mutasyon-oynatici.py`):

| mutasyon | kırmızı |
| --- | --- |
| oynatıcı kırpıyor (`(long)` bölme) | 2 |
| gövde çağrılıyor ama kırpılmış değer veriliyor | 2 |

Ölçünün kendi kusuru da burada kapandı: oynatıcı kolu beklenen değeri biçim
ifadesinden **kopyalıyordu**, yani gövdeyi bozan mutasyon beklentiyi de bozuyor ve kol
hep yeşil kalıyordu. Beklenen basamaklar artık elle yazılı.

### Adım 5 ölçümü — 19 Eylül 2026

Çözünürlük ve kare hızı indi. Çözünürlük **üç** yazımdaydı: `1920x1080` (kaynak bilgisi,
plan gerçeği, gelişmiş panel), `1920×1080` (oynatıcı, kaydedici özeti), `1920 × 1080`
(hazır boyut listesi, çizim penceresinin ölçü etiketi). Yedisi de `Bicim.Cozunurluk`'a
bağlandı; ffmpeg argümanı üreten `WxH` yazımlarına dokunulmadı.

Kare hızında tek aykırı oynatıcıydı: `0.###` ile **üç** ondalık yazıyor ve kültürü
`CultureInfo.CurrentCulture`'dan, yani arayüzün dilinden değil **makineden** okuyordu.
Türkçe arayüz İngilizce makinede `23.976` yazıyordu; artık `23,98`.

Mutasyon, temiz tabanda (0 kırmızı / 125 yeşil):

| mutasyon | kırmızı |
| --- | --- |
| gövde çarpı yerine `x` yazıyor | 5 |
| gövde üç ondalık yazıyor | 3 |
| gövde kültürü yok sayıyor | 3 |
| kaynak çözünürlüğü gövdeye uğramıyor | 2 |
| kaynak kare hızı gövdeye uğramıyor | 1 |
| plan gerçeği gövdeye uğramıyor | **0** |
| gelişmiş panel gövdeye uğramıyor | **0** |
| oynatıcı çözünürlüğü gövdeye uğramıyor | 2 |
| oynatıcı kare hızı gövdeye uğramıyor | 1 |
| kaydedici özeti gövdeye uğramıyor | **0** |
| hazır boyut listesi gövdeye uğramıyor | 1 |
| çizim etiketi gövdeye uğramıyor | 1 |

Üç sıfır gerçek kör noktaydı — eşdeğer mutant değil, **hiçbir ölçü o satırı
okumuyordu**. Üçüne pim yazıldı (`PlanVeGelismisPanelCozunurluguCarpiIsaretiyle`,
`OtomatikOzetCozunurlugunuCarpiIsaretiyleYazar`) ve aynı mutasyonlar yeniden koşuldu:

| mutasyon | kırmızı |
| --- | --- |
| plan gerçeği gövdeye uğramıyor | 2 |
| gelişmiş panel gövdeye uğramıyor | 2 |
| kaydedici özeti gövdeye uğramıyor | 1 |

Oynatıcının canlı bilgi paneli ölçüsü burada da beklenen değeri biçim ifadesinden
kopyalıyordu (bit hızındaki aynı kusur). Yanına elle yazılmış basamaklı bir pim kondu.

### Adım 6 ölçümü — 19 Eylül 2026

Tarih damgası iki ayrı birim: kullanıcıya gösterilen damga (`d MMMM HH:mm`) üç dosyada,
dosya adına giren damga (`yyyy-MM-dd_HH-mm-ss`) iki dosyada elle kopyalanmıştı. Beşi de
`Bicim.Damga` / `Bicim.DosyaDamgasi` üstüne alındı.

Planda "kültürü başka kaynaktan alan aykırı" diye yazılan satır — `ShrinkJobWindow.Paylas.cs:115`
— **aykırı değilmiş**. O pencere kendi dilini (`_language`) taşıyor ve bütün metnini
`Strings.GetIn(_language, …)` ile yazıyor; damgada `Strings.CultureOf(_language)` okuması
doğru olandı. Bu yüzden `Damga` kültürü parametre olarak alıyor: kalıp tek yerde, kültür
çağrı yerinde kalıyor. Adım 6 bir kusur kapatmadı, yalnız kalıbı teke indirdi.

`UpdateBadge`'in `HH':'mm` saati kapsam dışı: o bir damga değil, rozet ölçütünün istediği
24 saatlik saat, ve zaten tek yerde sabit.

Temiz taban 0 kırmızı / 137 yeşil. Mutasyon:

| Kesim | Kırmızı |
| --- | --- |
| M1 gösterilen damga kalıbı bozulur | 1 |
| M2 kültür yok sayılır | 1 |
| M3 `ToLocalTime` düşürülür | **0** |
| M4 dosya damgası tarihsiz kalır | 1 |
| M5 dosya damgası kültürlü yazılır | **0** |
| M6 kare dosyası kayıt gövdesini alır | 1 |

İki sıfır ayrı ayrı incelendi, ikisi de eşdeğer değildi:

- **M3 gerçek kör nokta.** Makine +03:00; `ToLocalTime` düşünce gösterilen saat üç saat
  kayıyor ve hiçbir ölçü bunu okumuyordu. Pim `DamgaYerelSaatiYazar`, beklenen saati
  makinenin kendi diliminden hesaplıyor — sabit saat yazılsaydı ölçü yalnız +03:00'te
  doğru olurdu.
- **M5 bu makinede eşdeğer, genelde değil.** `tr-TR` Gregoryen olduğu için kültürü
  değiştirmek çıktıyı değiştirmiyordu; takvimi başka olan `th-TH`'de yıl 2569 yazılırdı.
  Pim `DosyaDamgasiTakvimiDeSabitler` ölçüyü o kültürle koşuyor.

İki pim eklendikten sonra M3 ve M5 yeniden koşuldu: **ikisi de 1 kırmızı**.

## Kapsam dışı

- ffmpeg/mpv **argümanı** üreten biçimler (kullanıcıya gösterilmiyor, ondalığı protokol
  belirliyor): `ClipExport.cs:101`, `SegmentEncoder.cs:383`, `ToolsOptions.cs:132`,
  `MpvEngine.cs:504`, `OvershootTrimmer.cs:123`, `FrameGrabber.cs:266`.
- `_trace.Add(...)` satırları — test izi, kullanıcı yüzeyi değil.

### Onizleme kusuru — 19 Eylul 2026

Kullanicinin makinesinde cikan "Onizleme Ornegi Kodlanamadi" ekrani
(`.claude/kanit/onizleme-hatasi-2026-09-19.jpg`) olculdu. Kok neden plan **passthrough**
oldugunda ortaya cikiyor: kaynak zaten hedefin altindaysa `PlanCalculator` plani
"oldugu gibi kopyala" diye kuruyor ve bu planda `Codec` kaynagin **cozucu** adini
(`h264`), `Preset` ise `copy` tasiyor. `BuildSegment` bu plani kodlama argumanina
ceviriyordu; ffmpeg'in gercek cevabi:

```
x264 [error]: invalid preset 'copy'
[vf#0:0] Task finished with error code: -22 (Invalid argument)
[enc:libx264] Could not open encoder before EOF
Nothing was written into output file
```

Kor nokta: `BuildSegment` yedi cagri yerinde yalnizca **dizgi** olarak pimliydi, uretilen
arguman hicbir olcude ffmpeg'e verilmiyordu. Once kosan olcu yazildi (`OnizlemeParcasiKosarTests`,
`OnizlemeKaynakTaramaTests`), kusur onunla ureretildi, sonra duzeltildi.

Duzeltme teslimin kendisiyle ayni: `EncodeRunner` passthrough'u kopyalayarak teslim ediyor,
parca da `-c:v copy` ile kopyalaniyor. Boylece onizleme teslim edilecek goruntunun benzerini
degil birebir kendisini gosteriyor; `PreviewQuality.Kopya` bu yuzden "yaklasik" rozetini almaz.

| Mutasyon | Kirmizi |
|---|---|
| passthrough kolu dusuruldu (eski kusur geri geldi) | 3 |
| kopya `-c:v copy` yerine kodlayiciya cevrildi | 1 |
| `Kopya` kalitesi `Desteklenmiyor` oldu | 1 |
| yaklasik rozeti kopyayi da isaretledi | 1 |

Temel: 31 yesil, 0 kirmizi.
