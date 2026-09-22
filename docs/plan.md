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

### Adım 7 ölçümü — 19 Eylül 2026

**Planın adım 7'si yanlış kurulmuştu.** "`MainWindow.Num`, `CliApp.Num`,
`RecorderTray.Megabytes` kaldırılsın, çağrı yeri kalmadığı taramayla kanıtlansın"
diyordu; tarama bunun tersini gösterdi: `Num`'un ~30, `CliApp.Num`'un ayrıca canlı
çağrı yeri var. Yazdıkları sayılar (CRF, bppf, ayrıntı üsteli, puan) altı ailenin
hiçbirine girmiyor — bunlar **motorun gerekçe sayıları**, ayrı bir aile ve ayrı bir iş.
Silme değil, yeni bir adım. Adım 7 bu yüzden ailenin dışarıda kalan megabayt
çağrı yerlerine çevrildi.

`MainWindow.Percent` zaten `Bicim.Yuzde.Isaretli`'ye tek satırlık bir takma ad,
duruyor. `MpvEngine.Percent` kapsam dışı: kullanıcıya yazılan metin değil, mpv'ye
verilen tamsayı.

İki gerçek kusur kapandı, ikisi de `ShrinkJobWindow`'da:

- Hedefi aşan teslimde sapma `InvariantCulture` ile yazılıyordu — Türkçe arayüz
  `1,23` yerine `1.23` okuyordu.
- Tavan aşımı satırındaki boyut da aynı şekilde `12.0` diye noktayla yazılıyordu.

Pencere kendi dilini (`_language`) taşıdığı için doğru kültür `Strings.CultureOf(_language)`.
İki satır ölçülebilsin diye `UpdateBadge.Compose` kalıbıyla saf işlevlere ayrıldı
(`BittiSatiri`, `HataSatiri`) — eskiden yalnız uçtan uca bir kodlama koşumunda görünüyorlardı.

Temiz taban 0 kırmızı / 149 yeşil. Mutasyon:

| Kesim | Kırmızı |
| --- | --- |
| N1 iş penceresi sapması tek ondalık | **0** |
| N2 iş penceresi kültürü değişmez | **0** |
| N3 kaydedici sonucu hedef yazımıyla | **0** |
| N4 tepsi kırpması kalkar | 1 |
| N5 tepsi sapma yazımıyla | 1 |

Üç sıfır da eşdeğer değildi — hiçbir ölçü bu üç satırı okumuyordu. Üç pim yazıldı;
kaydedici satırı için `RecorderView.ResultText` erişimcisi eklendi. Yeniden koşum:
**N1 / N2 / N3 ve ek olarak N3b (kaydedici satırının kültürü) dördü de 1 kırmızı.**

### Adım 8 ölçümü — 19 Eylül 2026

Süre ailesi. `Saat` üç yüzey kazandı: `Sure` (tek ondalık, dilin ayracı),
`TamSaniye` (basamak ayracı yok) ve `Tani.Saniye` (motorun İngilizce metni,
`InvariantCulture`). Dört çağrı yeri bu yüzeylere indi.

İlk mutasyon koşumunda **sekiz kesimin sekizi de sıfır kırmızı** verdi: ailenin
tamamı pimsizmiş. Ölçerken ikinci bir kusur çıktı — `PromptBuilder`'ın modele giden
İngilizce istemi **beş sayıyı** araya biçim koymadan yazıyordu, yani makinenin
kültürünü okuyordu. Türkçe bir makinede istem `duration: 12,34 s`, `23,98 fps`,
`target size: 7,25 MB` diyordu; aynı kaynağın iki makinedeki istemi
karşılaştırılamaz hale geliyordu. Beşi de `Tani` yüzeylerine indi.

`CeilingGuard`'ın gerekçesindeki tavan MB ve nişan oranı da aynı sızıntıyı
taşıyordu; ikisi de `InvariantCulture`'a bağlandı. `MpvEngine` `VidShrink.Core`'a
başvurmuyor (bilerek) — oradaki tek sızıntı `Saat` çağrısıyla değil, yerinde
`InvariantCulture` ile kapatıldı.

Temiz taban 0 kırmızı / 91 yeşil. Pimlerden sonraki mutasyon:

| Kesim | Kırmızı |
| --- | --- |
| S1 `Sure` ondalığı düşer | 2 |
| S2 `Sure` kültürü sabitlenir | 2 |
| S3 `TamSaniye` basamak ayracı alır | 2 |
| S4 `Tani` kültürü makineden gelir | 2 |
| S5 kırpma bildirimi tam saniyeye düşer | 1 |
| S6 bütçe satırı ondalıklı yazar | 1 |
| S7 istem süresi makine kültürünü okur | 1 |
| S7b istem hedefi makine kültürünü okur | 1 |
| S7c istem kare hızı makine kültürünü okur | 1 |
| S8 tavan tamponu boş yazar | 1 |
| S9 tavan MB makine kültürünü okur | 1 |
| S10 nişan makine kültürünü okur | 1 |

Sıfır yok. Yeni ölçüler: `SaatTests`'te üç yüzey pimi, `IstemKulturuTests`
(yeni dosya, olumlu kontrollü), `CeilingGuardTests.BekciGerekcesiMakineninKulturunuOkumaz`,
`BoslukKirpmaTests.KirpmaBildirimindekiSaniyeAileninYazimiyla`,
`KaydediciHedefTests.YalnizSureButcesiBasamakAyraciYazmaz`.

### Adım 9 ölçümü — 19 Eylül 2026

Motorun gerekçe sayıları. Adım 7'de görülmüştü: `MainWindow.Num` ile `CliApp.Num`
aynı sayıları (CRF, bppf, ayrıntı üsteli, puan, MB) yazıyor ama **iki ayrı kültürle**.
Pencere `Strings.Culture` okuyor, komut satırı `InvariantCulture`. `--dil tr` ile
Türkçe cümleler kuran komut satırı sayıyı `12.5` diye yazıyordu; aynı programın
penceresi aynı sayıyı `12,5` diye yazıyor.

Kapanan kusur: `CliText` artık seçilen dilin kültürünü taşıyor (`Culture`), `Num` ve
`Format` onu okuyor. **İngilizce değişmez kültürde kaldı** — çıktısı makine tarafından
da okunuyor ve eskiden beri nokta yazıyor. `--json` yolu etkilenmiyor: JSON
`Utf8JsonWriter` ile kuruluyor, insan metninden geçmiyor.

`MainWindow.Num` duruyor ve doğru: `Strings.Culture` okuyor, `BiciminTests` dört
yerden pimliyor. Planın "kaldırılsın" maddesi adım 7'de yanlış kurulmuştu.

Temiz taban 0 kırmızı / 67 yeşil. Mutasyon:

| Kesim | Kırmızı |
| --- | --- |
| C1 `Format` değişmez kültürde kalır | 1 |
| C2 `Culture` her dilde değişmez | 2 |
| C3 `Culture` İngilizcede de tr-TR | 2 |
| C4 `Num` kültürü dilden almaz | 1 |

C1 ilk koşumda **0 kırmızı** verdi. Eşdeğer değil ama kusur da değil: bugünkü çağrı
yerlerinin hepsi `Format`'a ya dizge ya tamsayı geçiyor, ham ondalık geçen yok. Yani
`Format`'ın kültürü kapanan bir kusuru değil, ileride araya girecek ham bir ondalığa
karşı bir kapı — ve kapı pimsizdi. İki satırlık ham ondalık asertiyle pimlendi,
yeniden koşumda 1 kırmızı. Bu dürüstçe yazılıyor: adım 9'un kapattığı tek gerçek
kusur `Num`'un kültürü.

### Adım 10 ölçümü — 19 Eylül 2026

Oynatıcı paneli. Taramanın (`.ToString("0…")` kültürsüz + `CultureInfo.CurrentCulture`)
bıraktığı son yığın buradaydı: 22 izleme satırı ve altı kullanıcı satırı.

İki ayrı karar, çünkü iki ayrı okuyucu var:

- **İzleme satırları** (`_trace`) ölçü okuyor, kültürden kopuk olmalı — hepsi
  `Saat.Tani.Konum`'a indi. Eskiden makinenin kültürünü okuyorlardı; Türkçe bir
  makinede ölçünün okuduğu satır `seek 12,345` yazıyordu.
- **Panelin kendi satırları** arayüzün dilini okumalı — konum, yakınlaştırma, ses,
  hız ve A-B döngüsü `Strings.Culture`'a bağlandı. Kapanan gerçek kusur: aynı
  pencerenin küçültme sekmesi `1,25` derken oynatıcı şeridi `1.25` diyordu.

Dört yeni yüzey: `Saat.Konum` (konum, üç ondalık — aramanın adımı milisaniye),
`Saat.Adim` (arama miktarı ve altyazı gecikmesi), `Bicim.Kat` (hız çarpanı),
`Bicim.Yuzde.HazirTam` (zaten yüzde taşıyan tam sayı).

Temiz taban 0 kırmızı / 58 yeşil. Mutasyon:

| Kesim | Kırmızı |
| --- | --- |
| O1 `Konum` ondalığı düşer | 1 |
| O2 `Konum` kültürü sabitlenir | 1 |
| O3 `Tani.Konum` makineden okur | 1 |
| O4 `Adim` ondalığı düşer | 1 |
| O5 `Kat` kültürü sabitlenir | 2 |
| O6 `HazirTam` yüzle çarpar | 1 |
| O7 hız satırı makineden okur | 1 |
| O8 ses satırı makineden okur | **0** |

**O8 eşdeğer, kör nokta değil.** Ses düzeyi tam sayı; `65` her kültürde `65`.
Aynı şey altyazı boyu menüsünde ve kısayol etiketinde de geçerli — ölçek ve arama
miktarı tam sayı, ayraç hiç görünmüyor. Bu üç satırda kapanan şey görünen bir kusur
değil, kaynağın doğruluğu: bugün eşdeğer olmaları değerlerin bugünkü halinden
geliyor, yüzeyin kendisinden değil. Bu yüzden düşürülemeyen bir ölçü yazılmadı;
yüzeyler (`Yuzde.HazirTam`, `Saat.Adim`) kendi pimlerini taşıyor (O4, O6).

### Adım 11 ölçümü — 19 Eylül 2026

Kaydedicinin hedef kutuları ve tarayıcı ölçüsü. `tests/VidShrink.Tests/KaydediciHedefYazimiTests.cs`
ile `tests/VidShrink.Tests/BicimDisiYazimTests.cs`. Temiz taban **0 kırmızı / 7 yeşil**.

Kesim | Ne bozuldu | Kırmızı
--- | --- | ---
K1 | Saniye kutusu okurken `Strings.Culture` → `CultureInfo.CurrentCulture` | 1
K2 | MB kutusu okurken aynısı | 4
K3 | Saniye kutusu yazarken aynısı | 1
K4 | MB kutusu yazarken aynısı | 2
K5 | Gelişmiş panelin sayı kutusu okurken aynısı | 1
K6 | Tarayıcı deseni eşleşmeyecek hale getirilir | 1
K7 | Muafiyet listesine karşılığı olmayan satır eklenir | 1
K8 | Kaynak ağacına kültürsüz bir `ToString("0.00")` eklenir | 1

Sıfır kırmızı veren kesim yok.

**Ölçüm bir öncülü düzeltti.** Kusurun "on kat büyük hedef" olduğunu yazmıştım:
Türkçe arayüzde `12.5` yazan kullanıcının `125` MB alacağını varsaymıştım. Ölçüm
hükmü `Süre ve boyut sıfırdan büyük sayı olmalı.` döndürdü — `NumberStyles.Float`
binlik ayracını kabul etmiyor, yani sayı büyümüyor, **geçersiz sayılıp düşüyor**.
Kusur daha küçük değil, sadece başka: kullanıcı hedefini hiç yazamıyor. Test ve
gerekçesi ölçülen hükme göre yeniden yazıldı, varsayılana göre değil.

**Tarayıcı ölçüsü artık var.** Bu bölümün Ölçü başlığında söz verilmişti ve on adım
boyunca yoktu: yüzeyler tek tek kapanıyordu ama geri dönüşü durduran bir şey yoktu.
Üç kolu var — kalan yazım sayısı (K8 bunu pimliyor), desenin kör olmadığı (K6) ve
muafiyet listesinin bayatlamadığı (K7). Muafiyet listesi elle yazılan bir beklenen
küme değil: `tests/VidShrink.Tests/Veri/bicim-muafiyetleri.txt`, her satırı gerekçeli,
karşılığı kalmayan satır ölçüyü kırıyor.

**Muafiyet tek satır:** `ShareErrorClassifier.cs`. Oradaki `CurrentCulture` yalnız
başına bir kusur değil, daha büyüğünün parçası — aynı gövde `"sınırsız"`, `"saniye"`,
`"dakika"` diye sabit Türkçe metin yazıyor. Core'un içinde, dil katmanından geçmeden.
İngilizce arayüzde kullanıcı Türkçe kelime okuyor. Kültürü tek başına düzeltmek bunu
gizlerdi; ayrı defter satırı olarak duruyor.

### Paylaşım tablosunun arama sırası — 19 Eylül 2026

**Önce yanlış bir öncül.** Bir alt ajanın raporu "tablo yoksa ana pencere hiç açılmıyor"
diyordu. Doğrulamaya gidildi ve tutmadı: `InitializeShareUi`'nin çağırdığı `Load()`
arayüzün kendi kaydına ait ve her istisnayı yutup `Fallback` dönüyor; Core'un fırlatan
`Load()`'unun üç çağrı yerinin üçü de yakalıyor. Çökme yok. Düzeltme ve ölçüsü geri
alındı, ölçü `trash/` altına taşındı.

**Ama doğrulama sırasında gerçek olan çıktı.** `paylasim-hedefleri.json`'ı iki tür
okuyor: şeridi kuran `VidShrink.App.ShareTargetTable` ve yüklemeyi yapan
`Core.Share.ShareTargetTable`. Şema T35'te sabitlendi, iki taraf da onu okuyor — ama
**arama sırası** sabitlenmemişti:

| | kullanıcının kopyası (`%APPDATA%\VidShrink`) | uygulamanın yanı | üst dizinler |
|---|---|---|---|
| Core (yükleme) | 1. | 2. | 3., 8 kademe |
| App (şerit) | **hiç bakmıyor** | 1. | 2., sınırsız |

Core'un belgesi kullanıcı kopyasının niye ilk sırada olduğunu da yazıyor: bir uç nokta
ölünce kullanıcı sürüm beklemeden düzeltebilsin. Kullanıcı o kopyayı düzenlediğinde
şerit paketteki tavanları gösteriyor, yükleme kullanıcının uç noktasına gidiyordu —
görünen sınır ile gidilen adres ayrı dosyalardan. Hata yok, uyarı yok.

**Düzeltme:** App kendi aramasını bıraktı. `AramaSirasi()` Core'unkini döndürüyor,
`Load()` Core'un `Locate()`'ini kullanıyor, arama `Load(Func<string?>)` ile dışarıdan
verilebiliyor — ölçü gerçek `%APPDATA%`'ya dokunmadan koşsun diye.

**Ölçü** `tests/VidShrink.Tests/PaylasimAramaSirasiTests.cs`, beş olgu; ikisi olumsuz
kontrol. Taban 0 kırmızı / 18 yeşil (`SettingsTabTests` ile birlikte).

| kesim | kırmızı |
|---|---|
| K1 App kendi aramasına dönüyor | 3 |
| K2 `Load` verilen aramayı yok sayıyor | 5 |
| K3 `Load` koşulsuz `Fallback` | 3 |
| K4 Core kullanıcı klasörünü atlıyor | 1 |
| K5 okunamayan dosya yutulmuyor | 1 |

Sıfır yok. `ShareTargetTable.Locate(string)` kaldırıldı; onu kullanan iki eski ölçü
yeni yüzeye taşındı.

## Paylaşım hatalarının dili — plan

**Kusur.** `Core/Share/ShareErrorClassifier.cs` kullanıcıya gösterilen 27 cümleyi
Türkçe, sabit metin olarak yazıyor. Core dil katmanını göremez (`Strings` App'te),
o yüzden İngilizce arayüzde kullanıcı Türkçe cümle okuyor. Üç yerden görünüyor:
`MainWindow.axaml.cs:2118`, `:2153`, `ShrinkJobWindow.Paylas.cs:124`,
`RecorderView.Paylas.cs:114` — hepsi `result.Message`'ı olduğu gibi yazıyor.

Yanında iki kusur daha aynı gövdede:

- `Size()` biçimi `CultureInfo.CurrentCulture` ile yazıyor — arayüzün dili değil
  makinenin kültürü. `bicim-muafiyetleri.txt`'deki tek muafiyet bu satır.
- `step` kullanıcıya gösteriliyor ve değerleri karışık: `"yükleme"`, `"hazırlık"`,
  `"yoklama"`, `"silme"` Türkçe; `"init"` ve `"confirm"` ham protokol sözcüğü.
  Türkçe arayüzde bile cümlenin ortasında İngilizce teknik terim çıkıyor.

**Yerleşik çözüm var, paylaşım onu kullanmıyor.** `Core/Subtitles` aynı sorunu
çözmüş: Core `SubtitleOutcome` hükmü döndürüyor, App `PlayerView.Subtitles.cs:174`'te
anahtara çeviriyor. Cümle Core'da hiç doğmuyor. Paylaşım tarafı `ShareFailure`
hükmünü zaten taşıyor ama yanında bir de hazır cümle taşıyor.

**Neden hüküm tek başına yetmiyor.** `ShareFailure` cümlelerden kaba: tek
`NetworkFailure` beş ayrı cümleye, `ServiceError` dörde çıkıyor. O yüzden çeviri
anahtarı hükümden ayrı taşınacak.

### Adımlar

1. `ShareStep` numaralandırması (`Prepare/Init/Upload/Confirm/Probe/Delete`);
   iki sağlayıcıdaki altı dizge onunla değişir.
2. `ShareDiagnosis`'te `Message` yerine `Key` + `Args`. `Detail` olduğu gibi kalır
   (geliştiriciye ait, hiçbir yerde gösterilmiyor — ayrı defter satırı).
3. `ShareErrorClassifier` cümle kurmayı bırakır: anahtar ve argüman döndürür.
   `Size` ve `Wait` biçimleri App'e geçer, `CurrentCulture` gövdeden düşer ve
   muafiyet satırı `bicim-muafiyetleri.txt`'den silinir.
4. Dört gösterim yeri `Say(result.Key, result.Args)` ile yazar.
5. Yeni anahtarlar 42 dilde. Çeviri alt ajanlara dağıtılır; her dil dosyası
   `KeysAreCompleteInEveryLanguage` ölçüsünden geçer.

### Ölçü

Core'da kullanıcıya giden cümle kalmadığı taranır (`BicimDisiYazimTests` deseniyle
aynı yordam), her anahtarın 42 dilde bulunduğu ve yer tutucularının korunduğu
sınanır, dört gösterim yerinin ham `Message` yazmadığı pimlenir. Mutasyon: anahtarı
sabitlemek, argüman sırasını bozmak, `Size`'ı makine kültürüne döndürmek.

### Sonuç

Anahtar sayısı **27 değil 37** çıktı. Tarama, planın saymadığı 28. cümleyi buldu:
`PresignedUploadProvider.cs:163` silme jetonu yokken Türkçe cümleyi Core'da kuruyordu
(`share.error.token-lost`). Kalan fark biçim ve adım anahtarlarından:
`share.size.unlimited`, `share.wait.*`, altı `share.step.*`.

Tarama ilk koşumda yedi Türkçe dizge listeledi; altısı `new …Exception(` gövdesindeydi.
İstisna metni geliştiriciye ait — `FromException` onu anahtara çeviriyor, ham metin yalnız
hiçbir yerde gösterilmeyen `Detail`'de kalıyor. Tarayıcı bu kurulumları atlıyor; atlamanın
kör bir muafiyet olmadığı `IShareProvider.cs` üstünden pozitif kontrolle pimli.

`bicim-muafiyetleri.txt`'den `ShareErrorClassifier.cs` satırı düştü (5 → 4).

| kesim | kırmızı |
|---|---|
| taban | 0 |
| K1 bayt biçimlenmeden geçiyor | 2 |
| K2 sınırsız eşiği kayıyor | 1 |
| K3 bekleme birimi sınırı kayıyor | 1 |
| K4 adımın anahtarı yanlış | 1 |
| K5 tavan aşımında sığan hedef söylenmiyor | 3 |
| K6 bir dilde anahtar eksik (ru) | 2 |
| K7 bir dilde yer tutucu düşüyor (de) | 1 |
| K8 Core yeniden cümle kuruyor | 2 |

Sıfır yok. Toplam 77 ölçü; geri alındıktan sonra taban yine 0/77.

## Kurucunun sağ tık etiketi 42 dile — plan

**Kusur.** Sağ tık menüsünün etiketini üç yer yazıyor, ikisi iki dille sınırlı:

- `Install-VidShrink.ps1:389,405` — `if ($choice -eq 'tr')` ile Türkçe, değilse İngilizce.
- `src/VidShrink.Core/Setup/ShellRegistration.cs:47,50` — aynı üçlü, gömülü metinle.
- `src/VidShrink.App/MainWindow.KabukMenusu.cs:98,100` — `shell.menu.open` /
  `shell.menu.shrink` anahtarlarından, 42 dilin hepsinde çevrili.

Almanca Windows'ta kurup uygulamayı hiç açmadan bir videoya sağ tıklayan kullanıcı
İngilizce etiket görüyor. Uygulama ilk açılışta `RelabelShellMenu` ile düzeltiyor —
yani kusur kalıcı değil, ama kurulumla ilk açılış arasındaki her sağ tıkta duruyor.

**Çeviri zaten var, kurucu ona bakmıyor.** `shell.menu.open` 42 dilde çevrili ve
`Locales\**\*.json` yayına kopyalanıyor; menü yazıldığı anda (`Mark("dosyalar-yerinde")`
sonrası) `app\Locales\<dil>\main.json` diskte duruyor. Kurucunun okuması yeterli.

**Neden `Strings` kullanılamıyor.** `Strings` App'te; `VidShrink.Setup`
`InvariantGlobalization=true` ile derleniyor ve `CultureInfo` orada güvenilir değil —
`KulturTuzakTeliTests` bu ayrımı zaten pimliyor. O yüzden dil kodu Win32'den
(`GetUserDefaultLocaleName`) alınacak, çeviri dosyadan okunacak.

### Adımlar

1. `ResolveLanguage` artık `tr`/`en`'e indirmez: seçim ya da işletim sisteminin dil
   etiketi, `Locales` altında karşılığı olan klasör adına eşlenir; yoksa `en`.
2. `OpenLabel`/`ShrinkLabel` dosyadan okur (`<kurulum>pp\Locales\<dil>\main.json`),
   dosya yoksa bugünkü gömülü İngilizce metne düşer.
3. `VidShrink.Setup/Program.cs:132` LCID kıyası yerine `GetUserDefaultLocaleName`.
4. `Install-VidShrink.ps1` aynı dosyayı `ConvertFrom-Json` ile okur; iki yazıcının
   aynı metni ürettiği sınanır.

### Ölçü

Üç yazıcının aynı dilde aynı etiketi verdiği, `Locales`'te olmayan dilin `en`'e düştüğü,
`Locales` klasörü hiç yokken kurulumun çakmadığı sınanır. Mutasyon: dosya okumasını
sabit metne çevirmek, düşüş kolunu kaldırmak, dil eşlemesini `tr`/`en`'e geri indirmek.


### Sonuç — 19 Eylül 2026

Üç yazıcı da aynı dosyayı okuyor. Kurucunun dil kodu artık `GetUserDefaultLocaleName`'den
geliyor ve `Locales` klasör adlarıyla en uzun eşleşmeye iniyor: `zh-Hans-CN` → `zh-Hans`,
`pt-BR` → `pt`, karşılığı olmayan `kl-GL` → `en`. Klasör yerinde değilse eski iki dilli
kol duruyor.

`SetupRunner`'ın klasörü geçirdiği ilk turda **sıfır kırmızı** verdi: gövde doğruydu ama
çağrı yeri pimsizdi, klasör hiç geçirilmese her dil İngilizceye düşerdi. Ayrı pim yazıldı.

| kesim | kırmızı |
|---|---|
| taban | 0 |
| M1 etiket dosyadan okunmuyor | 7 |
| M2 eşleme tr/en'e iniyor | 4 |
| M3 yalnız tam etiket eşleniyor | 6 |
| M4 düşüş kolu `en` yerine `tr` | 4 |
| M5 gömülü metin `tr`'ye bakmıyor | 1 |
| M6 kurucu klasörü geçirmiyor | **0 → 1** |
| M7 betik çevirilere bakmıyor | 1 |

Toplam 54 ölçü; geri alındıktan sonra taban yine 0/54.

## Bit hızı birimi tek yazıma — plan

Aynı pencerede aynı birim beş ayrı yazımla görünüyor. Türkçe kurulumda:

| yer | anahtar | bugün |
| --- | --- | --- |
| kaynak bilgisi, ses | `main.unit.k-value` | `128k` |
| kaynak bilgisi, toplam | `main.unit.kbps-value` | `2500 kbps` |
| oynatıcı bilgi paneli | `player.info.bitrate` | `Bit hızı: 2500 kb/s` |
| kaydedici bütçesi | `recorder.budget.result` | `Hedef bit hızı: 3000 kbit/sn` |
| hızlı düşür notu | `main.fast-gpu.bitrate-floor` | `… 2000 kbit/s …` |
| plan gerekçesi | `main.reason.manual-audio-bitrate-override` | `… 128kbps …` |
| ayar etiketi | `main.advanced.audio-kbps.label` | `Ses hedefi (kbps)` |

Tarama 42 dilin sekiz dosyasında birim taşıyan **16 anahtar, 553 satır** buldu
(`.calisma/birim-tum-anahtarlar.txt`). Üçü yanlış pozitif: `main.convert.crf-label.tip`,
`main.convert.audio-bitrate.tip` ve `recorder.advanced.noise-suppression` birimi rakamla
değil sözcükle anıyor (ar/fa/he), biri de "kbt" değil "kbt etmek" anlamında.

### Kural

Her dilin tek birimi var ve o birim **yalnız `main.unit.kbps-value` içinde yazılı**.
Birim, o dilin bugün `recorder.budget.result`'ta kullandığı yazım: 37 dilde `kbit/s`,
`tr` `kbit/sn`, `ru` `кбит/с`, `uk` `кбіт/с`, `ar` `كيلوبت/ث`, `he` `קילוביט/שנייה`,
`fa` `کیلوبیت بر ثانیه`.

Değer taşıyan cümleler birimi bırakır, çağıran taraf sayıyı `main.unit.kbps-value` ile
biçimlendirip geçirir. Etiketler (`(kbps)` gibi) cümle değil, birim sözcüğünü doğrudan
taşır — onlar da aynı yazıma çekilir.

`recorder.advanced.buffer` kapsam dışı: birimi `kbit`, saniye başına değil.

### Adımlar

1. 42 dilde `main.unit.kbps-value` → `{0} <birim>`; `main.unit.k-value` silinir.
2. Değer cümlelerinden birim düşer: `player.info.bitrate`, `recorder.budget.result`,
   `recorder.budget.too-small`, `main.fast-gpu.on-usable`, `main.fast-gpu.bitrate-floor`,
   üç `main.reason.manual-audio-bitrate-*`.
3. Etiketlerde yazım birleşir: `main.advanced.audio-kbps.label`,
   `recorder.advanced.bitrate`, `recorder.advanced.max-bitrate`.
4. Sekiz çağrı yeri sayıyı önceden biçimlendirir (`MainWindow.axaml.cs` 3235/3236/3525/
   3529/1094/1101/3716/3718/3720, `PlayerView.Window.cs:503`,
   `RecorderView.Otomatik.cs:134/135`).
5. `BitHiziBirimiTests`: 42 dilde birim taşıyan tek anahtarın `main.unit.kbps-value`
   olduğunu, etiketlerin o dilin birimini kullandığını, muafiyet listesinin kapalı
   olduğunu pimler.

### Ölçü

Mutasyon: bir dilde birim geri getirilir, bir dilde birim bozuk yazılır, `k-value`
geri eklenir, bir çağrı yeri ham sayı geçirir. Her kesim kırmızı vermeli.

### Sonuç — 19 Eylül 2026

Ledger satırı "üç ayrı yazım" diyordu; tarama Türkçe kurulumda **beş** buldu: `128k`,
`2500 kbps`, `2500 kb/s`, `3000 kbit/sn`, bitişik `128kbps`. Bir de dil içi sapma:
Arapça hızlı düşür notu `كبت/ث`, kaydedici `كيلوبت/ث` yazıyordu; Farsça ve İbranice
notlar Latin `kbit/s`'te kalmıştı.

Yapılan: 42 dilde `main.unit.kbps-value` o dilin birimini aldı, `main.unit.k-value`
silindi (336 değer cümlesinden birim düştü, 126 etiket aynı yazıma çekildi). Sekiz
çağrı yeri `Strings.BitHizi` üzerinden geçiyor; anahtarın adı kaynakta yalnız
`Strings.cs`'te geçiyor ve ölçü bunu pimliyor.

| kesim | kırmızı |
| --- | --- |
| taban | 0 |
| B1 tr kaydedici cümlesi birimi geri alıyor | 2 |
| B2 de etiketi eski yazıma dönüyor | 1 |
| B3 ikinci birim anahtarı geri geliyor | 1 |
| B4 oynatıcı ham sayı geçiriyor | 1 |
| B5 tr birimi `kbps`'e dönüyor | 4 |
| B6 biçim anahtar yerine sabit yazıyor | 8 |
| B7 kaydedici ham sayı geçiriyor | **0 → 2** |

B7 ilk turda sıfır verdi: kaydedicinin bütçe notunu birim açısından okuyan ölçü yoktu.
`KaydediciHedefYazimiTests.ButceNotuBirimiSozluktenAliyor` yazıldı, kesim 2 kırmızıya
döndü. Toplam 72 ölçü; geri alındıktan sonra taban yine 0/72.

Yan etki: `BaslikKapsamiTests`'in nüfus sayımları kaydı. Pimler `40592 → 42140` ve
`1628 → 1649` (en 193 → 195, tr 66 → 67) yenilendi; artış paylaşım işinin otuz yedi
anahtarından geliyor, `main.unit.k-value`'nun düşmesi gezileni bir azalttı. `kayip`
yine 0. Bu iki pim `5ba0e973`'ün CI koşumunu kırmızıya düşürmüştü.

## Kurucunun dili — plan

Uygulama 42 dil konuşuyor, kurucu yalnız Türkçe. `VidShrink-Setup.exe`'yi indiren bir
İngiliz kullanıcı ilk karşılaştığı yüzeyde Türkçe okuyor: "Son yayın aranıyor...",
"Kurulum klasörü kilitli", mimari reddi, `--help`.

Kurucu uygulamanın çeviri katmanını göremiyor. Kırpılmış, tek dosya, kendi kendine yeten
bir exe; `VidShrink.App`'e referansı yok ve `InvariantGlobalization=true` bilerek açık
(`KulturTuzakTeliTests` bunu pozitif kontrol olarak kullanıyor). Üstelik kurucu, yayın
paketini indirip açmadan **önce** de konuşuyor — o anda diskte hiç `Locales` klasörü yok.

### Ölçüm — 19 Eylül 2026

`.calisma/core-turkce.py` Core'da dil katmanından geçmeyen **72 cümle / 12 dosya** buldu
(daha önce 98/13; paylaşımın 27'si `5ba0e973`'te kapandı). Üç kova ayrıldı:

| kova | sayı | nerede | kullanıcı görüyor mu |
| --- | --- | --- | --- |
| kurucu | 50 | `Setup/SetupRunner.cs` 19, `Setup/SetupDownloads.cs` 15, `Setup/LockedFolder.cs` 7, `Setup/ShellRegistration.cs` 1, `UpdateCheck.cs` 2 (`ArchitectureDecision.Note`), `VidShrink.Setup/Program.cs` 6 | **evet**, konsola |
| iç tanı | 18 | `UpdateCheck.cs` 14, `UpdateStaging.cs` 4 | hayır |
| programcı hatası | 11 | `ShrinkRequest`, `SingleInstanceChannel`, `IShareProvider`, `ShareTargets`, `Multipart`/`PresignedUploadProvider` | hayır |

Ek olarak `Program.cs`'teki `--help` bloğu (26 satır) düz Türkçe; satır tarayıcısı ham
dizge (`"""`) olduğu için onu saymadı.

İç tanı kovası kullanıcıya hiç çıkmıyor: `MainWindow.Guncelleme.cs` istisnayı yutup
`Say("main.update.failed")` yazıyor. `ArchitectureDecision.Note` ilk taramada ölü sanıldı;
`SetupRunner.cs:30` onu `log`'a basıyor, yani kurucu kovasına girer.

### Karar

Gömülü **tr + en** tablosu, `Core/Setup` içinde. 42 dil gömmek reddedildi: birkaç saniye
görünen bir konsol için 50 × 42 = 2100 cümle, kırpılmış tek dosyaya ayrıştırıcı yükü ve
doğrulanamayan çeviri. Yalnız İngilizce de reddedildi: sağ tık etiketi bile yerel dilde
yazılırken kurucunun kırmızı hatası tek İngilizce yüzey kalırdı.

Dil seçimi yeni kural istemiyor: `ShellRegistration.ResolveLanguage(choice, uiLanguage)`
klasör verilmediğinde zaten yalnız `tr` ve `en` tanıyor. `SetupHost` `MenuLanguage` ve
`UiLanguage`'ı hâlihazırda taşıyor.

### Adımlar

1. `Core/Setup/SetupText.cs`: anahtar → (tr, en) tablosu, `Use(dil)`, `Get(anahtar, args)`.
   Biçimleme `CultureInfo.InvariantCulture`, karşılaştırma `Ordinal`; `InvariantGlobalization`
   ve `KulturTuzakTeliTests` dokunulmadan kalır.
2. `VidShrink.Setup/Program.cs` açılışta `SetupText.Use(ShellRegistration.ResolveLanguage(...))`.
   `--menu-language` ayrıştırılmadan önceki hatalar işletim sisteminin diliyle yazılır.
3. 50 cümle anahtara taşınır; `--help` bloğu iki dilde yazılır.
4. `ShellRegistration`'ın gömülü `tr`/`en` menü etiketi ikilisi aynı tabloya iner — iki
   ayrı gömülü çeviri deposu kalmaz.
5. Ölçü `KurucuDiliTests`: her anahtar iki dilde dolu, yer tutucuları aynı, kullanılmayan
   anahtar yok, ve **kaynak pimi** — `Core/Setup` ile `VidShrink.Setup` altında Türkçe
   cümle taşıyan tek dosya `SetupText.cs`.

### Ölçü

Mutasyon: her kesimde kaç ölçü kırmızıya döner. Sıfır kırmızı veren kesim kör nokta sayılır
ve ölçü yazılana kadar kapanmaz.

## Kapsam dışı

- Kurucunun **iç tanı** ve **programcı hatası** cümleleri (29 satır) Türkçe kalır: kullanıcıya
  çıkmıyorlar, deponun kod içi dili Türkçe ve yalnız istisna gövdelerini İngilizceye çevirmek
  karışık dil üretirdi. Tarayıcının kapsamı "kullanıcıya çıkan yüzey" diye daraltılır.
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

---

# `ShareDiagnosis.Detail` Düşürülüyor (Defter 35)

**Ölçüm.** `Detail` üretimde hiçbir gösterim yerinde okunmuyor. Paylaşım sonucu üç yerde
tek satır olarak yazılıyor — `MainWindow.axaml.cs:2119,2154`, `RecorderView.Paylas.cs:115`,
`ShrinkJobWindow.Paylas.cs:125` — ve üçü de yalnız `ShareMessage.Of(result)` çağırıyor,
o da `result.Key` + `result.Args` okuyor. `Detail`'i okuyan tek satır bir testte.

**Karar: düşürülsün.** Sunucunun ham metni zaten cümle kurulamayan iki kolda
`Args`'a giriyor — `share.error.unexpected-detail` (`{3}`) ve
`share.error.unexpected-exception` (`{1}`). Cümlesi kurulabilen kollarda ham gövde
kullanıcıya gürültüdür; ayrıntı satırına bağlamak tek satırlık durum metnini bozar.
Yersiz kalan `Detail` ise ikinci bir depo: dolduruluyor, taşınıyor, hiç okunmuyor.

Yer tutucusuz dört anahtar (`cancelled`, `file-missing`, `file-locked`, `disk-full`)
üç argümanlı kurucuyu yalnız `Detail`'i doldurmak için kullanıyordu; onlarda alan
tamamen ölüydü.

**Adımlar**

1. `ShareDiagnosis`'ten `Detail` çıkar, üç argümanlı kolaylık kurucusu `(failure, key)` olur.
2. `ShareResult`'tan `Detail` ve `Failed`'deki atama çıkar; sınıf belgesi düzeltilir.
3. 24 kurulum yeri (`ShareErrorClassifier` 23, `PresignedUploadProvider` 1) `Detail`
   argümanını bırakır; `RetryAfter` bir sıra öne kayar.
4. `ShareProviderTests.AMultipartServerErrorIsClassifiedNotThrown` ham gövde yerine
   sınıflandırmanın kendisini ölçer (anahtar + argümanlar), yani geçirgen alanı değil davranışı.
5. `PaylasimHataDiliTests`'e alanın geri gelmemesi için yansımalı pim.

**Ölçü.** Alanı geri eklemek derlemeyi kırmaz, sessizce ikinci depoyu geri getirir —
`OrtakOdakTests.KonumDeposuCurrentMediaDaYok` ile aynı desen: yüzey yansımayla pimlenir.

## Sabit Çıktı Klasörü Gerçekten Çalışsın (Defter 28 Sınıfı)

**Ölçü.** Ayarlar sekmesindeki "Çıktı klasörü" seçimi — `RbOutputBesideSource` /
`RbOutputFixed` ve yanındaki klasör kutusu — yalnız kaydediliyor, geri yükleniyor ve
satırın görünürlüğünü açıp kapatıyor (`MainWindow.axaml.cs:255, 1330, 1365, 1400`).
Çıktı yolunu kuran tek yer `ShrinkEngine.UniqueOutputPath(inputPath, ...)` ve o yalnız
**kaynağın klasörünü** okuyor (`ShrinkEngine.cs:81`). Dört çağrı yerinin dördü de
(`MainWindow.axaml.cs:3546, 4224, 4539, 4561`) klasörü hiç geçirmiyor.

Yani kutucuğun kendi ipucu — "Sabit klasör her çıktıyı kaynağın yanı yerine hep aynı
yere gönderir" — yapılmayan bir şeyi vaat ediyor. Kullanıcı klasörü seçiyor, ayar
diske yazılıyor, çıktı yine kaynağın yanına düşüyor; hata yok, uyarı yok.

**Karar: ayar bağlansın, kaldırılmasın.** CLI tarafında karşılığı zaten var
(`CliApp.cs:228`, `izle` akışı `OutputDirectory` ile aynı işi yapıyor); eksik olan
yalnız arayüzün bağlantısı.

**Sessiz geri düşme olmayacak.** Seçilen klasör yoksa ya da yazılamıyorsa çıktı
kaynağın yanına düşer **ve durum satırında söylenir**. Sessizce yanına yazmak, bugünkü
kusurun daha kibar bir biçimi olurdu.

**Adımlar**

1. `ShrinkEngine.UniqueOutputPath`'e `string? outputDirectory = null` eklenir; verilince
   aday yol o klasörde kurulur, çakışma sayacı ve `_shrunk` tekrar koruması aynı kalır.
2. `MainWindow.BuildUniqueOutputPath` ayarı okur: kip `Sabit` ve klasör yazılabilirse
   onu geçirir, değilse kaynağın yanı.
3. Klasör kullanılamadığında `settings-tab.output-folder.unusable` anahtarı durum
   satırına yazılır; anahtar 42 dilde.
4. Ölçü: seçilen klasöre yazıldığı, kip `Kaynağın yanı` iken yazılmadığı (olumsuz
   kontrol), olmayan klasörde kaynağın yanına düşüp uyarı verdiği, çakışma sayacının
   hedef klasörde işlediği.
5. CLI'ın `--cikti`'sı dokunulmaz: orada yol kullanıcının verdiği yoldur.

## HandBrake E3/E7/E8/E9 Açığı (Defter 23)

Dört hafif açık, hepsi yerinde ölçüldü — `src/` altında hiçbirinin karşılığı yok:
`modulus`, `align-av`, `ipod-atom`, `automatic-naming` ve tanı günlüğü için hiçbir
arama sonuç vermiyor.

**E9 — Ölçek modülü.** Bugün tek bir kural var ve üç yerde ayrı ayrı yazılı:
`PlanCalculator.cs:1373` ve `ComplexityProbe.cs:799` aynı `EvenDown`'u `private static`
olarak taşıyor, çarpan 2'ye gömülü. HandBrake 2/4/8/16 sunuyor; eski donanım kodlayıcıları
16'nın katını ister. Kural Core'da tek yere iner (`Olcek.Modul`), varsayılan 2 kalır,
Gelişmiş panelde ve CLI'da (`--modul`) seçilir.

**E3 — Otomatik adlandırma deseni.** Çıktı adı bugün `{ad}_shrunk.{uzanti}` olarak
gömülü. Sabit klasör işi bittiğine göre (`docs/olcumler/sabit-cikti-klasoru.md`) sıradaki
yalan adlandırmada: kullanıcı klasörü seçebiliyor ama adı seçemiyor. Desen ayarda tutulur
(`{ad}`, `{kalite}`, `{cozunurluk}`, `{tarih}`), tanınmayan yer tutucu reddedilir ve
çakışma sayacı desenden **sonra** işler.

**E8 — Kap uyumluluğu.** HandBrake'in `--align-av`'si ffmpeg'de
`-avoid_negative_ts make_zero` + `aresample` karşılığına, `--ipod-atom`'u ise ffmpeg'in
ayrı `ipod` muxer'ına düşüyor. İkisi de ölçülmeden yazılmayacak: gerçek ffmpeg 9.0
koşumuyla çıktının okunduğu bir ölçüm dosyası olmadan kabul edilmez.

**E7 — Tanı günlüğü.** HandBrake'in Activity Log'u, kullanıcı sorun bildirirken
kopyalayacağı tek yer. Bizde ffmpeg'in komutu ve çıktısı hiçbir yere yazılmıyor.
Son koşumun komutu, sürümü ve stderr kuyruğu `%APPDATA%\VidShrink\gunluk` altına
yazılır; CLI'da `--gunluk`, arayüzde Ayarlar'da "Günlüğü aç". Kişisel veri sızmayacak:
yalnız dosya adı, tam yol değil.

**Sıra:** E9 → E3 → E8 → E7. E9 üç dosyadaki kopyayı tek yere indirdiği için önce;
E3 sabit klasör işinin doğrudan devamı; E8 ölçüm koşumu ister; E7 en geniş yüzey.

**Her adımın ölçüsü mutasyonla kapanır** ve `docs/olcumler/` altına yazılır.

### E3 Kararı: Yer Tutucu Kümesi (2026-09-19)

Plandaki dört yer tutucu yazılırken bir sorun çıktı: `{kalite}` adı boyutu gösterirse
ad kullanıcıya yalan söyler — defter 28'in tam konusu. Küme dürüst adlarla kapandı:

| Yer tutucu | Değer | Yoksa |
|---|---|---|
| `{ad}` | kaynağın adı (`_shrunk` eki kırpılır) | — |
| `{hedef}` | hedef boyut, `25mb` | boş |
| `{kalite}` | kodlayıcının kalite kolu: `crf26` ya da `2500k` | boş |
| `{cozunurluk}` | çıktı yüksekliği, `720p` | boş |
| `{kodek}` | `h264` / `hevc` / `av1` | boş |
| `{tarih}` | `2026-09-19` | — |

Tanınmayan yer tutucu reddedilir; değeri olmayan yer tutucu boşa düşer ve arta kalan
ayırıcılar sadeleşir. Çakışma sayacı desenden **sonra** işler.

**Yolda çıkan ikinci kopya.** `ShrinkJobWindow.axaml.cs:359` kendi `UniqueOutputPath`'ini
taşıyor: sabit çıktı klasörünü de, deseni de görmüyor. Kuyruğa atılan dosyaların çıktısı
ayardan bağımsız olarak hep kaynağın yanına düşüyor. Kopya silinir, kuyruk da motorun
tek yolunu kullanır.

**CLI kapsam dışı:** `--cikti` zaten tam yolu veriyor, desen orada ikinci bir yol olurdu.

### E7 Planı: Tanı Günlüğü (2026-09-19)

E8 kapandı (`4c431103`, `docs/olcumler/e8-kap-uyumlulugu.md`). Sıradaki E7, dört açığın
en genişi: Core, Ffmpeg, Cli, App, 42 dil ve testler.

**Bugünkü durum ölçüldü.** ffmpeg'in komutu tek yerde dizgeye çevriliyor
(`FfmpegArguments.ToCommandLine`, `FfmpegArguments.cs:715`) ve yalnız ekrana gidiyor
(`CliApp.cs:311`, `MainWindow.axaml.cs:3605`). stderr iki koşucuda toplanıyor
(`FfmpegRunner.cs:110` son 8 satır, `EncodeRunner.cs:673` son 15 satır) ve hata olursa
yalnız istisna mesajına gömülüyor. Diske hiçbir şey yazılmıyor. `Gunluk`/`ActivityLog`
diye bir kavram kodda yok; diske satır yazan tek örnek `InstallProgress.WriteLog`
(`InstallProgress.cs:198`), kurulum paneline ait.

**Yer.** `UpdateSettings.DefaultPath`'in klasörü + `gunluk\` (`UpdateCheck.cs:461`,
`VIDSHRINK_SETTINGS_PATH`'i dinleyen tek doğru kaynak). Dosya `vidshrink.log`, 1 MB'ı
aşınca `vidshrink.1.log`'a devrilir ve tek yedek tutulur — sınırsız büyüyen günlük
kullanıcının diskini yiyor, ikiden fazla yedek de kimsenin işine yaramıyor.
`ShareResult.cs:152` ve `ShareTargets.cs:216` klasörü elle kuruyor ve ortam değişkenini
dinlemiyor; **bu iş o hatayı tekrarlamayacak**, yol tek yerden gelecek.

**Kayıt.** Her koşum bir blok: zaman damgası, uygulama sürümü, ffmpeg sürümü
(`ToolLocator.GetFfmpegVersion`), komut satırı, çıkış kodu, süre, stderr kuyruğu.
Günlük **her zaman açık** — sorun bildirirken "önce günlüğü aç, sonra tekrarla" demek
kullanıcıyı ikinci kez çalıştırmak demektir.

**Gizlilik tek kural, ölçünün ağırlığı da orada:** günlüğe düşen hiçbir satırda tam yol
bulunmayacak. Komut satırındaki girdi/çıktı yolları ve stderr'in taşıdığı yollar dosya
adına indirilir (`C:\Users\Ad\Videolar\tatil.mkv` → `tatil.mkv`). Kullanıcı adı, klasör
ağacı ve sürücü harfi günlüğe hiç girmez. Negatif kontrol: indirgemeyi kaldıran mutasyon
ölçüyü kırar.

**Yüzeyler.**
1. `src/VidShrink.Core/Gunluk.cs` — yol, yazma, devirme, yol indirgeme. Saf ve test edilebilir.
2. `EncodeRunner.RunCommandAsync` ve `FfmpegRunner.RunAsync` — blok yazan iki çağrı yeri.
3. `CliParser` — `--gunluk`, komut öncesi kısayol (`-h`/`-v` ile aynı raf, `CliRequest.cs:136`):
   günlüğün yolunu ve içeriğini stdout'a basıp 0 ile çıkar. Yardım metni `Locales/{en,tr}.json`.
4. Ayarlar sekmesi — "Günlüğü aç" düğmesi, sıfırlama panelinin komşusu
   (`MainWindow.axaml:1427`), `Platform.Reveal` ile (`Platform.cs:9`).
5. `AppDataReset` — günlük klasörü veri sıfırlamaya girer, `VeriSifirlamaTests.cs:42` pimi yenilenir.
6. Yeni anahtarlar 42 dile; `AltyaziIndirmeTests.cs:1238` desenine `InlineData` satırı.

**Ölçü.** `TaniGunluguTests.cs`: yol ortam değişkenini izliyor, blok altı alanı da
taşıyor, tam yol hiçbir satırda yok (pozitif kontrol: indirgenmemiş metinde tarayıcının
gerçekten bulduğu), devirme 1 MB'da bir kez oluyor ve tek yedek kalıyor, CLI kolu 0
dönüyor ve günlük yokken de çakmıyor. Kabul mutasyonla kapanır, tablo
`docs/olcumler/e7-tani-gunlugu.md`.

### E4 Planı: Başlık ve Kaynak Tarama (2026-09-19)

**Bugünkü durum.** Yoklama tek noktadan geçiyor: `FfprobeClient.ProbeAsync`
(`src/VidShrink.Ffmpeg/FfprobeClient.cs:10`), argümanları `-show_format -show_streams
-show_chapters` (`:15-21`). Video akışı olarak `attached_pic` olmayan **ilk** akış
alınıyor (`:37-43`). Yani kaynak ne olursa olsun tek bir başlık varsayılıyor: çok programlı
bir yayında yanlış program seçilebiliyor, DVD klasörü ise hiç açılmıyor.

Bölüm desteği tamam ve uçtan uca bağlı (`--bolum/--chapters`, `CliRequest.cs:210-217`,
`-map_chapters`). Eksik olan bir üst katman: **başlık**.

**Ölçülen ffmpeg yeteneği (9.0-full_build, 19 Eylül 2026).** `dvdvideo` demuxer var ve
`-title`, `-angle`, `-chapter_start/-chapter_end`, `-preindex` seçeneklerini taşıyor
(`ffmpeg -h demuxer=dvdvideo`). Blu-ray için demuxer **yok** (`-demuxers` listesinde
`bluray` geçmiyor), bu yüzden BD bu turda kapsam dışı ve gerekçesiyle belgelenecek.
ffprobe `-show_programs` taşıyor; yerelde iki programlı bir TS üretilip doğrulandı
(`.calisma/e4/coklu.ts`, programlar 1 ve 2).

**Kaynak türleri.** Üç tür ayırt edilecek: düz dosya (tek başlık), çok programlı MPEG-TS
(her program bir başlık), DVD-Video klasörü/ISO (her başlık bir title numarası). Model
`src/VidShrink.Core/SourceTitles.cs`: `SourceTitle(Number, DurationSeconds, Width, Height,
StreamCount, ChapterCount, Label)` ve seçim kuralları — numarayla seçim, en uzunu seçen
`--ana-icerik`, kısa başlıkları eleyen `--asgari-sure`.

**Yüzeyler.**
1. `FfprobeClient` — `-show_programs` eklenir, programlar başlığa çevrilir.
2. `MediaInfo` — `Titles` listesi ve seçili başlık numarası.
3. Girdi argümanı — TS programında `-map 0:p:<id>`, DVD'de `-f dvdvideo -title N -angle N`.
4. CLI — `--tarama/--scan` envanteri basar (kodlama yok), `--baslik/--title`,
   `--ana-icerik/--main-feature`, `--aci/--angle`, `--asgari-sure/--min-duration`.
5. Metinler — en/tr yardım satırları ve yeni `error.*` anahtarları, iki README'nin takma ad
   tablosu, `CliTests.BeklenenTakmaAdSayisi` pimi.
6. Arayüz — başlık seçici yalnız birden çok başlık varsa görünür; tek başlıklı dosyada
   arayüz bugünkü gibi kalır.

**Ölçü.** `BaslikTaramaTests.cs`: iki programlı gerçek TS taranınca iki başlık çıkıyor,
`--ana-icerik` en uzunu seçiyor, `--asgari-sure` kısayı eliyor, olmayan başlık numarası
hata veriyor, `--tarama` hiçbir şey kodlamıyor, DVD kolu doğru demuxer argümanını kuruyor.
Kabul mutasyonla kapanır, tablo `docs/olcumler/e4-baslik-tarama.md`.

## E8 — Süzgeç yüzeyi (19 Eylül 2026)

**Bulgu.** HandBrake bayrak denetimi üç bağımsız grupta aynı boşluğu gösterdi: motor
`src/VidShrink.Core/VideoFilterChain.cs` kırpma, keskinleştirme, gürültü giderme, deblock,
deband, döndürme, dolgu ve gri tonlamayı tam taşıyor, ama `VideoFilterChain.Parse` üretimde
hiçbir yerden çağrılmıyor. `new PlanOptions` kuran üç yerin hiçbiri `Filters` vermiyor
(ölçüldü: `grep -rn "new PlanOptions" src -A6 | grep -c Filters` -> 0). Yani eksik olan
özellik değil, yüzey; tek bir kapı on kadar HandBrake bayrağını birden kapatıyor.

**Yüzeyler.**
1. CLI — `--suzgec/--filters` tek dizge alır ve `VideoFilterChain.Parse`'a verir; ayrıştırma
   hatası `error.bad-filter` ile 64 döner.
2. `CliRequest` — çözümlenen `VideoFilterOptions` plan seçeneklerine girer.
3. Arayüz — Gelişmiş panelde süzgeç satırı; boş bırakılırsa bugünkü davranış.
4. Metinler — en/tr yardım satırı, hata anahtarı, 42 dilde arayüz etiketi.
5. Ölçü — `SuzgecYuzeyiTests`: dizge motora ulaşıyor, argüman zincirinde görünüyor,
   bozuk dizge 64 döndürüyor, boş dizge bugünkü argümanı değiştirmiyor.

**Kabul.** Mutasyon turu; her kol en az bir kırmızı vermeli.

**Yapıldı (19 Eylül 2026).** Beş yüzeyin beşi açıldı, ölçü `SuzgecYuzeyiTests` (5 kol,
5/5 yeşil), sonuç ve mutasyon tablosu `docs/olcumler/e8-suzgec-yuzeyi.md`.

## E9 — Anamorfik kaynak: piksel en-boy oranı (19 Eylül 2026)

### Bulgu

`grep -rn "sample_aspect_ratio\|setsar\|display_aspect" src tests tools` → **0 satır**. Depo
piksel en-boy oranını (PAR) hiçbir yerde okumuyor; `MediaInfo.Width` ffprobe'un `width`
alanı, yani depolanan genişlik. Kare piksel varsayımı DVD dışında doğru, DVD'de değil.

E4 DVD kolunu açtı: 720x480 NTSC kaynak SAR 8:9 ile 640x480 (4:3), SAR 32:27 ile 853x480
(16:9) gösterilir. Bugün ikisi de 720x480 sayılıyor, ölçek merdiveni yanlış orandan
iniyor ve çıktı yassı ya da uzun kodlanıyor.

HandBrake'in `--non-anamorphic`, `--auto-anamorphic`, `--itu-par`, `--keep-display-aspect`
bayraklarının dördü de bu tek eksikte birleşiyor
(`docs/handbrake/bayrak-hukumleri-2026-09-19.md`).

### Karar

HandBrake'in `--non-anamorphic` davranışı alınır: çıktı **kare pikselli**. Yükseklik
korunur, genişlik gösterim genişliğine çevrilir. Anamorfik metadata taşıyan çıktı
üretmiyoruz — küçültme aracının çıktısı paylaşılacak dosya, oynatıcı uyumu kare pikselde
daha yüksek.

### Adımlar

1. `MediaInfo`: `ParNum`/`ParDen` (varsayılan 1/1), türeyen `DisplayWidth` ve
   `IsAnamorphic`. `Pixels` gösterim genişliğinden hesaplanmaz — bit hızı modeli
   kodlanan piksele bakar, o değişmiyor.
2. `FfprobeClient`: `sample_aspect_ratio` okunur. Yokluk, `"0:1"`, `"N/A"` ve sıfır payda
   1:1'e düşer. 90° dönüşte PAR de ters çevrilir, `DisplayDimensions` ile aynı kapıdan.
3. `PlanCalculator.Dimensions`: merdiven `info.Width` yerine `info.DisplayWidth`'ten iner.
   Tek huni, üç çağıran da oradan geçiyor.
4. `VideoFilterChain.Filters` ve `ConversionArguments.VideoFilters`: kaynak anamorfikse
   ölçekten sonra `setsar=1`. Yoksa `scale` süzgeci SAR metadata'sını olduğu gibi taşır ve
   çıktı ikinci kez esner.
5. Ölçü: `tests/VidShrink.Tests/AnamorfikTests.cs`. Kollar — ffprobe ayrıştırması (8:9,
   32:27, `"0:1"`, eksik alan), dönüşte ters çevirme, merdivenin gösterim genişliğinden
   inmesi, `setsar=1`'in iki argüman üreticisinde de çıkması, kare pikselli kaynakta
   zincirin **değişmemesi** (olumsuz kontrol).
6. Mutasyon turu: beş kesim, sıfır kırmızı kalan kol bırakılmaz.


### Sonuç — kapandı (19 Eylül 2026)

Altı adımın altısı da yapıldı. Ölçü `AnamorfikTests` 6/6 yeşil; mutasyon turu altı kesimin
altısını da kırmızıya çeviriyor, taban ve geri kolları 0/6 (`docs/olcumler/e9-anamorfik.md`).
Adım 6 beş kesim öngörüyordu, altı koştu: `setsar` iki üreticide ayrı ayrı kesildi.

## K8 borcu 7 — `--kirp`: kırpma yoklaması kullanıcıya açılıyor (19 Eylül 2026)

### Bulgu

`CropProbe` motorda hazır: `RunAsync` on kare örnekliyor, `Decide` modu seçiyor, sonuç
`CropDetection.Rect`. Üretimde çağıran tek satır yok — yoklamayı yalnız testler ve ölçüm
düzeneği koşturuyor. `PlanCalculator` yalnız `options.DetectedCrop` **verilmişse**
`SuggestedCrop`'a yazıyor; doldurmuyor.

Elle kırpma zaten var: `--suzgec crop=W:H:X:Y`. Eksik olan **otomatik** kol — kullanıcı
siyah bantların ölçüsünü kendi bulmak zorunda.

### Karar

CLI'a `--kirp` / `--crop` bayrağı eklenir. Yoklama koşar, bulunan dikdörtgen süzgeç
seçeneklerine kırpma olarak girer ve plandan kodlamaya aynı yoldan iner. `--suzgec crop=`
ile birlikte verilirse **elle verilen kazanır**: açık niyet yoklamayı ezer.
Bant bulunmazsa satır sessiz, kırpma yok.

### Adımlar

1. `CliRequest.AutoCrop` (bool) ve `--kirp`/`--crop` ayrıştırması.
2. `CliApp.ProcessFileAsync`: `Decide`'dan önce yoklama, sonuç `Filters.WithCrop`'a.
   Elle kırpma varsa yoklama hiç koşmaz.
3. `progress.crop` ve `result.crop` kalıpları tr + en.
4. Ölçü: `tests/VidShrink.Tests/OtomatikKirpmaTests.cs` — bantlı kaynakta dikdörtgen
   bulunuyor, bantsız kaynakta sessiz (olumsuz kontrol), elle kırpma yoklamayı eziyor,
   bayraksız koşumda yoklama hiç çalışmıyor.
5. README + README.tr: bayrak satırı.
6. Mutasyon turu: her kesim kırmızı, taban ve geri 0/N.

### Sonuç — kapandı (19 Eylül 2026)

Altı adımın altısı yapıldı. `OtomatikKirpmaTests` 6/6 yeşil, mutasyon turunda beş kesimin
beşi kırmızı, taban ve geri 0/6 (`docs/olcumler/k8-kirpma-2026-09-19.md`).

Planda öngörülmeyen tek şey `CliTests.BeklenenTakmaAdSayisi` pimiydi: takma adlar kaynaktan
sayıldığı için yeni `--kirp`/`--crop` çifti sayımı 21'den 22'ye çıkardı ve pim kırmızı verdi.
Pimin işi buydu — sayı elle 22 yapıldı, iki README'de de bayrak satırı duruyor.


## K8 borcu 6 — ön ayar kütüphanesi kullanıcıya açılıyor (19 Eylül 2026)

### Bulgu

`src/VidShrink.Core/Presets/platformlar.json` on sekiz profil taşıyor: 5 General,
9 Platform, 4 Device. Bunların **yalnız sekizinin** `chip` alanı var ve arayüz şeridi
yalnız o sekizi çiziyor. Kalan on profil — `discord-free`, `discord-nitro-basic`,
`discord-nitro`, `telegram`, `email-gmail`, `email-outlook`, `device-chromecast-gen1-2`,
`device-chromecast-gen3`, `device-nest-hub`, `device-apple-tv-hd` — depoda duruyor ve
hiçbir kullanıcı hiçbir yoldan seçemiyor.

`PresetKind`'ın dört üyesi de bu yüzden ölü (`OluUyeTests.cs:635-642`): profilleri
türüne göre ayıran tek üretim kolu yok, yalnız testler `OfKind` ile okuyor.
`PresetLibrary.Find` de üretimde çağrılmıyor.

### Karar

Kütüphane CLI'dan açılır; arayüz şeridi sekiz yongada kalır (şerit yeri sınırlı, on sekiz
yonga kullanıcıyı boğar).

1. `profiller` / `presets` komutu: kütüphaneyi **türüne göre bölümlenmiş** listeler —
   General, Platform, Device, sonra kullanıcının kendi profilleri. Dört `PresetKind`
   üyesinin dördü de burada okunur.
2. `--profil <id>` / `--profile <id>`: profil plan seçeneklerine iner. Profilin verdiği
   alanlar `Intent`, `Codec`, `FillPolicy`, `FixedResolution`, `LockedAudioKbps`,
   `Container` ve hedef boyut.
3. **Öncelik tek yönlü:** elle verilen bayrak kazanır. `--profil telegram --hedef 100MB`
   hedefi 100 MB yapar, profilin öbür alanları durur. Açık niyet kütüphaneyi ezer.
4. Tanınmayan kimlik `error.bad-profile` ile 64 döndürür; kimlikler kütüphaneden gelir,
   testte tekrarlanmaz.

### Adımlar

1. `CliCommand.Profiller` + `profiller`/`presets` ayrıştırması, `CliRequest.ProfileId`
   ve `--profil`/`--profile` kolu.
2. `CliRequest.ToPlanOptions`: profil tabanı, üstüne elle verilen alanlar.
3. Listeleme gövdesi `CliApp`te; başlıklar `presets.kind.*` anahtarlarından, tr + en.
4. Ölçü: `tests/VidShrink.Tests/OnAyarKitapligiTests.cs` — listede dört bölüm başlığı ve
   chipsiz on profilin kimliği görünüyor, `--profil` alanları plana indiriyor, elle verilen
   hedef profili eziyor (olumsuz kontrol), tanınmayan kimlik 64 veriyor, iki dilde anahtarlar.
5. `OluUyeTests`'teki dört `PresetKind` pimi kalkıyor.
6. README + README.tr: komut ve bayrak satırı.
7. Mutasyon turu: her kesim kırmızı, taban ve geri 0/N.

### Sonuç — kapandı (19 Eylül 2026)

Yedi adımın yedisi yapıldı. `OnAyarKitapligiTests` 10/10 yeşil, mutasyon turunda altı
kesimin altısı kırmızı, taban ve geri 0/10
(`docs/olcumler/k8-onayar-kitapligi-2026-09-19.md`).

Planda iki sapma var. Birincisi: profilin `Container` alanı plana inmedi, çünkü
`PlanOptions`'ta kap alanı yok — kap E2'den beri çıktı uzantısından türüyor. İkincisi:
beşinci adım "dört `PresetKind` pimi kalkıyor" diyordu; yalnız `User` kalktı, kalan üçü
tarayıcıda `varsayilan-kol` biçimine döndü ve borçtan meşruya çevrildi. Adlandırmak aynı
tabloyu iki kez yazmak olurdu.

Ayrıca hedefsiz dört cihaz profili için planda olmayan bir hata kolu gerekti
(`error.profile-no-target`), ve `CliTests.BeklenenTakmaAdSayisi` 22'den 24'e çıktı.

## K8 borcu 21 — hiç bağlanmamış kare kesme yolu (19 Eylül 2026)

`FrameGrabber` üretimde **bir kez bile** kurulmadı: `git log -S "new FrameGrabber" -- src/`
boş dönüyor. `5a44468b` sınıfı ekledi, bağlayan commit hiç gelmedi; karşılaştırma paneli
T176'dan beri libmpv yolundan besleniyor (`EngineComparisonFrameSource`).

Onu besleyen `PreviewState` / `PreviewStatus` makinesi de aynı durumda: `Derive`'ın tek
çağıranı testler, `AllowsFrameGrab`'ın tek çağıranı da ölü sınıfın kendisi. Kendi belgesi
"arayüz kendi koşullarından durum uydurmaz, bunu çağırır" diyor — çağıran arayüz yok.

Kapalı küme, dışarı sızmıyor: `FramePair` ve `FramePairRequest` yalnız `FrameGrabber.cs`
içinde, `PreviewTimeline` yalnız kendi dosyasında ve testlerinde. `PreviewSegment` canlı
kalıyor (`SegmentEncoder` kullanıyor), o yüzden `PreviewQuality`'nin iki pimi bu işin
dışında.

### Adımlar

1. `src/VidShrink.Ffmpeg/FrameGrabber.cs`, `src/VidShrink.Core/PreviewTimeline.cs`,
   `tests/VidShrink.Tests/FrameGrabberTests.cs`, `tests/VidShrink.Tests/PreviewTimelineTests.cs`
   → `trash/`.
2. `OluUyeTests` pimleri düşer: dokuz düz satır (`FrameGrabber.*` dördü, `FramePair.*` ikisi,
   `PreviewTimeline.*` ikisi, ayrıca kümede görünenler) ve üç `PreviewState` borç satırı.
3. Prose gönderimleri düzeltilir: `RecorderArguments.cs:254/1100`, `RecorderSession.cs:218`,
   `OlcekModuluTests.cs:15`, `PreviewSegmentTests.cs:220`'nin dosya listesi.
4. Ölçü: süit yeşil **ve** ölü üye tarayıcısının kümesi tam bu üyeler kadar küçülür,
   başka satır kaymaz. Kayarsa taşıma değil, taşımanın yan etkisi ölçülmüş olur.

### Sonuç — kapandı (19 Eylül 2026)

Plandan iki sapma. Birincisi: `FrameGrabberTests.cs` yalnız kendi testlerini taşımıyordu —
depo genelinde kullanılan dört xUnit geçidi (`FfmpegFact`, `TonemapFact`,
`HardwareEncoderFact`, `QuietMachineFact`) ve `NoEncoders` o dosyanın başında yaşıyordu.
Taşıma derlemeyi kırdı; geçitler `tests/VidShrink.Tests/TestGecitleri.cs`'e ayrıldı.

İkincisi: planda üç dosya vardı, dört oldu. `KeyframeIndex`'in tek okuyucusu `FrameGrabber`
idi; taşıma bittikten sonra tür kendi başına ölü kaldı ve ölçü bunu ikinci turda kırmızıyla
söyledi. O da `trash/`'a gitti.

Pim dökümü plandakinden geniş: dokuz değil **21 düz pim** ve 3 borç pimi düştü
(`GrabbedFrame` altı, `TimelinePoint` beş, `KeyframeIndex` iki de listede duruyordu).
Ölçüm `docs/olcumler/k8-olu-kare-yolu-2026-09-19.md`.

## K8 borcu 5 — paylaşım hatasının kullanıcıya ulaşmayan yarısı (19 Eylül 2026)

Bulgu borcun yazdığından farklı çıktı. Borç "`ShareFailure`'ın 11 üyesinden 8'i
okunmuyor, sınıflandırma mı fazla arayüz mü eksik" diyordu. Ölçüm: ayrım kullanıcıya
**zaten ulaşıyor** — enum üzerinden değil, `ShareDiagnosis.Key` üzerinden; 11 üyeye
karşı 22 anahtar var ve hepsi 42 dilde pimli.

Ölü olan enum değil, **`RetryAfter` ve `SuggestedTargetId`**. Sınıflandırıcı "120 saniye
sonra yeniden denenebilir" ve "bu dosya için şu hedef yeter" bilgisini hesaplıyor, üç
gösterim yüzeyinin üçü de atıyor (`grep -n "\.Failure" src/` iki okuma yeri veriyor,
`RetryAfter` sıfır). Kullanıcı "başarısız: <cümle>" görüyor ve elle baştan başlıyor.

Danışma: fable `b-dar` dedi — tekrar deneme düğmesi eklensin, en büyük risk yeniden
denemenin idempotent olmaması (bayt gittikten sonraki ağ hatasında ikinci yükleme).

### Adımlar

1. `ShareDiagnosis` ve `ShareResult` başarısızlığın **hangi adımda** olduğunu taşısın
   (`ShareStep`). Bugün adım sınıflandırıcıya giriyor ama tanıda durmuyor; idempotentlik
   kararı onsuz verilemez.
2. `Core/Share/ShareRetry.cs`: tek karar gövdesi. Yeniden deneme yalnız bayt
   işlenmemişken açılır — `RateLimited` her adımda (429 isteği baştan reddeder),
   `NetworkFailure`/`ServiceError` yalnız `Prepare`/`Init` adımında. Kalan sekiz üye ve
   `Upload`/`Confirm` adımı olumsuz kol.
3. Üç gösterim yüzeyi (`MainWindow`, `RecorderView.Paylas`, `ShrinkJobWindow.Paylas`)
   aynı kalıbı yazıyor; düğme tek yerde kurulsun. Üç durum: yeniden denenebilir →
   "Tekrar dene"; `RetryAfter` varsa geri sayımla kilitli; `SuggestedTargetId` varsa
   "{0} ile dene" ve o hedefe gider. Hiçbiri yoksa düğme görünmez.
4. Üç yeni anahtar 42 dile.
5. Ölçü: her `ShareFailure` üyesinin sınıflandırıcıdan **üretilebildiği** küme eşitliğiyle;
   yeniden deneme kümesinin adıma göre açılıp kapandığı, `Upload` adımındaki ağ hatasında
   düğmenin çıkmadığı olumsuz kontrolle. Mutasyon: kümeyi genişletmek ve adım şartını
   kaldırmak ayrı ayrı kırmızı vermeli.

## K8 borcu 3 — geçici ölçüm arayüzünün sökülmesi (19 Eylül 2026)

`IEncoderMeasurementState` kendini "T129 birleşince kalkar" diye ilan ediyor. T129 birleşti
(`EncoderProbeResult.State`, `IEncoderAvailability.EncoderState`, `IHdr10ProbeAvailability`)
ama arayüz kalkmadı, çünkü borç yanlış yazılmıştı: arayüz yalnız durum taşımıyor, arka plan
yoklamasını **kuyruğa alan tetik** de o. `DeferredEncoderAvailability.IsMeasured` çağrısı
`Ready(...)` üzerinden `Measure(...)` doğuruyor; iki Core çağrı yerini silmek yoklamayı
tümden durdururdu.

1. Tetiği üç durumlu yüze taşı: adaptörün `EncoderState`'i cevabı okumadan önce `Ready`yi
   çağırsın; `Hdr10State` aynısını hdr10 anahtarı için yapsın (adaptör
   `IHdr10ProbeAvailability`'yi uygulasın).
2. `EncoderAvailabilityState.KnownState` tek satıra insin: `availability.EncoderState(codec)`.
3. `HdrResolver.SupportsHdr10`'un `IsHdr10Measured` kolu `Hdr10State(codec) == Unmeasured`'a
   dönsün.
4. `IEncoderMeasurementState` silinsin; üç test sahtesi yeni yüze geçsin.
5. Ölçü: yoklamanın **kaç kez koştuğunu** sayan mevcut ölçüler korunur (kuyruklama
   davranışı budur) ve `Unmeasured`ın "çalışmıyor"a çökmediği ayrı pimlenir. Mutasyon:
   tetiği kaldırmak, üçüncü durumu iki duruma çökertmek, hdr10 kolunu düşürmek.

## C1-7 — Ön ayar içe/dışa aktarma pencereye bağlanıyor (22 Eylül 2026)

### Bulgu

A2 planının 2. ve 3. maddesi yarım: `PresetLibrary.Import`/`Export` ve
`HandBrakePresetImport.TranslateFile` Core'da hazır, `main.preset.handbrake.*` anahtarları 42
dilde duruyor, ama pencerede de CLI'da da onları çağıran tek kol yok.

### Karar

1. "+" açılır penceresine iki düğme: **İçe aktar…** ve **Dışa aktar…**.
2. İçe aktarma önce VidShrink dosyası dener; `HandBrakeFile` hatası gelirse HandBrake
   çevirisine geçer. Kaydedilen her profil kullanıcı türünde.
3. Aynı kimlik ezilmez: var olan (kullanıcı ya da gömülü) kimliğe `-2`, `-3` eklenir, ad
   ` (2)` alır. İçe aktarma kullanıcının elindekini sessizce silmez.
4. Bildirim: kaç ön ayar geldiği; HandBrake'te taşınmayan her alan
   `alan: sebep` satırıyla (`PresetFieldNote.LocaleKey`).
5. Dışa aktarma kullanıcının ön ayarlarını tek dosyaya yazar; hiç yoksa söyler.
6. Yeni anahtarlar 42 dilde. Ölçü: `OnAyarAktarimTests` — iki dosya türü, çakışma, not
   satırı, dışa-içe gidiş dönüş; her kolun olumsuz kontrolü ve mutasyon.

## C1-8 — CLI ön ayar dosyası okuyor (22 Eylül 2026)

Bulgu: pencere içe aktarıyor (C1-7), CLI hâlâ yalnız kütüphanedeki kimliği alıyor; HandBrakeCLI'nin
`--preset-import-file`'ına denk yüzey yok.

1. `--profil-dosyasi, --preset-file YOL`: VidShrink ya da HandBrake dosyası. Dosyadaki profiller
   aramada kütüphaneden önce gelir; kitaplığa yazılmaz (CLI kullanıcının dosyasını değiştirmez).
2. `--profil` kimlik ya da ad (büyük/küçük harf duyarsız) alır. Dosyada tek profil varsa `--profil`
   gerekmez; birden çoksa ve seçilmediyse 64 ve adlar listelenir.
3. HandBrake dosyasında stderr'e ön ayar başına bir özet: aktarılan/yaklaşık/düşen sayısı ve
   yaklaşık alanların adı. `--json` stdout'u bozulmaz.
4. Okunamayan dosya 64 değil 1 (girdi hatası), sebep cümlede.
5. Metin: tr ve en CLI dilleri, yardım metnine satır.
6. Test: `CliOnAyarDosyasiTests` — VidShrink dosyası, HandBrake dosyası + özet, çok profilde seçim
   zorunlu, ad ile seçim, bozuk dosya, dosyasız `--profil` davranışı değişmedi (olumsuz kontrol).

## B1a — Çok kanallı ses: ac3/eac3 kodlama, flac kapsam dışı (19 Eylül 2026)

HandBrake açığının B1a maddesi "flac/ac3/eac3 kodlama" diyordu. Danışma maddeyi ikiye böldü
ve yarısını bilerek kapsam dışına aldı.

**flac küçültme yoluna girmez.** Aracın sözleşmesi "ses bütçesi videodan düşer"; flac'in bit
hızı kaynağa bağlıdır, kodlamadan önce bilinmez ve sınırlanamaz. Bütçeye ancak tahminle
girer, o da hedef boyu ya ıskalar ya videoyu belirsiz miktarda ezer. Kayıpsız kaynağı
korumak isteyen kullanıcının iki yolu zaten var: kaynak flac ise `CopyableAudio` mp4 ve
mkv'de kopyalıyor, kaynak pcm ise dönüştürücü yolu duruyor. Gerçek boşluk yalnız
dönüştürücüde: orada `pcm_s16le` var, flac yok. flac pcm'den her durumda küçüktür, o yüzden
oraya eklenir.

**ac3/eac3 gerçek bir boşluk kapatıyor, ama dar.** Kaynak zaten ac3/eac3 ise passthrough onu
taşıyor. Kapatmadığı vaka `StreamMapping.cs:150`'de duruyor: `NeverPassedThrough` listesi
truehd/mlp/dts. Bugün 5.1 DTS kaynak aac'ye gidiyor ve kanal seçilmemişse `:289`'da 2'ye
iniyor. Boşluk şu üçlüde: DTS/TrueHD kaynak + çok kanallı çıkış isteği + yalnız ac3 okuyan
TV/AVR. Stereoda ac3'ün aac'ye karşı bir gerekçesi yok, o yüzden kodek seçimi kanal
seçiminden bağımsız sunulmaz.

### Adımlar

1. `AudioChannelOverride`'a **`Source`** eklenir: "kaynaktaki gibi". Bugün `Auto`, kanal
   seçilmemişse 2'ye iniyor; `Source` o inişi atlar. İniş kararı `StreamMapping.Decide`'da
   olduğu için oraya açık bir bayrak geçer, `audioChannels = null`'ın iki anlamı olmaz.
2. `PlanOptions`'a **`AudioCodecChoice { Auto, Aac, Ac3, Eac3 }`** eklenir; varsayılan
   `Auto`, `PickAudioCodec()` dokunulmaz. Seçim `Decide`'a taşınır.
3. Kap kapısı: ac3/eac3 yalnız Mp4/Mkv/Mov. WebM'de istek düşer, `StreamNote` ile söylenir.
   `!IsMp4Family(container) && codec == "aac" → libopus` satırı ac3/eac3'ü ezmemeli.
4. Bit hızı: ac3'ün merdiveni ffmpeg'de kapalı bir kümedir, uydurulmaz — **ölçülür** ve
   `docs/olcumler/b1a-ac3-merdiveni.md`'ye yazılır. Bütçeden seçilen `audioK`'nın altındaki
   en yakın basamak alınır; hiçbir basamak tutmuyorsa aac'ye dönülür ve not düşer.
5. `PlanParser.AllowedAudioCodecs`'e `ac3`, `eac3` eklenir.
6. Arayüz: küçültme ekranında değil, Gelişmiş panelde. Önce kanal kutusuna "kaynaktaki gibi"
   satırı, sonra yeni bir ses kodeği kutusu; kodek kutusu yalnız kanal "kaynaktaki gibi"
   iken anlamlı olduğu için varsayılanı `Auto` kalır. `AppSettings`'e `advAudioCodec`.
7. Dönüştürücüye flac: `ConversionArguments` kap izin listeleri ve `CmbConvertAudio`.
8. Ölçüler: kap kapısı, merdiven seçimi, kanal korunması, WebM düşüşü, gidiş-dönüş
   (`GelismisAyarGidisDonusTests` konumla okuduğu için yeni kutu oraya da girer), 42 dil.

### Ölçü

Her kolun negatif kontrolü olacak ve en az iki mutasyonla kırmızı döndüğü ölçülecek.
Merdiven testi sayıyı elle yazmaz, ölçüm belgesinden okur.

## B1 Kalanı: Flac, Ses Normalleştirme, Harici Altyazı, Kapak, Forced, Yakma

Dal `worktree-agent-ac648426fe8f56b4a`. Arayüz (MainWindow.axaml) bu işin dışında; yüzey Core + CLI.

1. **flac:** `AudioCodecChoice.Flac`, `PlanParser.AllowedAudioCodecs`. Kap listesi `CopyableAudio`'dan
   (Mp4/Mkv evet, WebM/Mov hayır → kabın kodeğine düşer, not). Bütçe PCM üst sınırı
   (örnekleme × kanal × 16 bit, `-sample_fmt s16`); %15 payını aşarsa düşer, not. `-b:a` yazılmaz.
2. **loudnorm/gain:** `PlanOptions.AudioLoudnorm`, `AudioGainDb` (-20..+20). Zincir
   `aresample hizalama → volume → loudnorm → aresample=<kaynak hızı>`. İstenince passthrough kapanır;
   açık `copy` izinde süzgeç kurulamaz, not düşer. CLI `--ses-normal`, `--ses-kazanc`.
3. **forced:** çıktıda varsayılan altyazı yoksa forced bayraklı iz varsayılan olur (tercih dili önce).
4. **Harici SRT/ASS:** `PlanOptions.ExternalSubtitles`; `-i` ek girdi, `N:0` eşlemi, MP4'te mov_text,
   MKV'de kopya, platformda düşer; baytı bütçeye girer. CLI `--altyazi <dosya>`, `--yan-altyazi`.
5. **Kapak:** ilk `attached_pic` MP4/MKV'de `-c:v:1 copy -disposition:v:1 attached_pic` ile taşınır,
   video süzgeci o zaman `-filter:v:0`'a yazılır; baytı yoklamada paketten ölçülür, bütçeye girer.
6. **Yakma:** metin altyazı `subtitles=` süzgeci A1 zincirinin sonunda; görüntü altyazı (PGS) `overlay`
   ister ve `-filter_complex` gerektirir. CLI `--yak <n>`.

Ölçü: her madde yeni sınıf, olumsuz kontrol, ≤3 sn lavfi canlı kol, ≥2 mutasyon
(`docs/olcumler/b1-kalan-mutasyonlar.md`).

## C1-3 — Klasör ve Çoklu Dosya Bırakma (22 Eylül 2026)

### Bulgu

Ana pencere tek dosya kabul ediyor; klasör ya da iki dosya bırakılınca "Klasör bırakılamaz"
diyor. Kuyruk penceresi (`ShrinkJobWindow`) zaten sırayla küçültüyor ama yalnız kabuk
menüsünden besleniyor ve her işi `new PlanOptions { TargetMb }` ile kuruyor: pencerede
seçilen kodek, çözünürlük, iz ve gelişmiş kollar kuyruğa geçmiyor. Çıktı uzantısı da
`"mp4"` diye sabit; MKV'ye düşen plan `.mp4` adıyla yazılırdı.

### Karar

- `Core/DroppedMedia.Collect`: bırakılan yolları sırayla gezer; dosya `WatchFolder.IsCandidate`
  ise alınır, klasörün yalnız **üst düzeyi** taranır (alt klasöre inilmez, büyük bir arşivi
  yanlışlıkla sıraya sokmamak için), sonuç ad sırasında ve tekrarsız.
- Tek dosya bugünkü gibi ana pencereye yüklenir. İki ve üstü video kuyruk penceresine gider,
  **o anki ayarların kopyasıyla** (`PlanCalculator.WithTarget` açılır). Boyut tavanı olmayan
  yongada hedef her dosyanın kendi `QualityCeilingTargetMb`'sinden hesaplanır.
- Kuyruk penceresinin çıktı uzantısı plandan (`plan.Streams.Extension`), ön ayar kabı
  seçiliyse ondan gelir; kabuk menüsü kolu değişmez.
- Bırakma yüzü sayıyı söyler: "Bırakın: 5 video sıraya girecek"; video yoksa "Burada
  küçültülecek video yok". Eski `main.drop.single` / `main.drop.no-folder` kalkar.

### Ölçü

`KlasorBirakmaTests`: toplama (gizli dosya, video olmayan, alt klasör, tekrar, sıra — olumsuz
kontroller), kuyruk penceresinin şablonu plana taşıması (kodek ve hedef), tavansız yongada
dosya başına hedef, uzantının plandan gelmesi, yeni iki anahtarın 42 dilde. En az iki mutasyon.

## C1-2 + C1-4 — Kuyruk Düzenleme ve "Bitince" Eylemi (22 Eylül 2026)

### Bulgu

Kuyruk penceresi (`ShrinkJobWindow`) bekleyenleri `Queue<ShrinkRequest>` içinde saklıyor ve
göstermiyor: sıradaki dosyayı çıkarmak, öne almak ya da sırayı durdurmak yok. İş bitince
pencere altı saniye sonra kapanıyor; HandBrake'in "bitince uyut / kapat" seçeneği yok.

### Karar

- Bekleyenler `List<ShrinkRequest>`; pencerede ad listesi, her satırda yukarı / aşağı / çıkar.
  Koşan dosya listede değil, başlıkta.
- "Sırayı duraklat": koşan dosya biter, sıradaki başlamaz; "Sürdür" pompayı yeniden açar.
- "Bitince" seçimi: hiçbir şey / çıktı klasörünü aç / uyut / kapat. Sıra boşalınca ve
  duraklatılmamışken çalışır. Uyut ve kapat **60 saniyelik geri sayımla** gelir, "Vazgeç"
  düğmesi durdurur; pencere bu sırada kapanmaz.
- Sistem çağrısı `IQueueEndActions` arkasında (`SystemQueueEndActions`: Windows `rundll32
  powrprof` ve kapatma aracı, macOS `pmset`/`osascript`, Linux `systemctl`). Testler sahte
  verir; gerçek eylem test sürecinde asla çalışmaz.
- 15 yeni anahtar, 42 dil.

### Ölçü

`KuyrukDuzenlemeTests`: duraklatılmış kuyrukta sıra, çıkarma, taşıma (sınırda no-op);
bitince eylemi sahteyle — geri sayım bitmeden çağrı yok, bitince tek çağrı, vazgeçince hiç,
"hiçbir şey" seçiliyken hiç, duraklatılmışken hiç (olumsuz kontroller); anahtarlar 42 dilde.
En az üç mutasyon.

## C1-5 — Süzgeç Paneli (22 Eylül 2026)

Bugün süzgeçler Gelişmiş'te tek metin kutusu (`TxtAdvFilters`), ve kutu izlenmiyor: yazılan
belirtim plana ancak başka bir seçenek değişince giriyor. Panel metni tek kaynak tutar:

- Core `VideoFilterChain.Format` — `Parse`'ın tersi; `Parse(Format(o)) == o`.
- Açılır kutular: taramasızlaştırma (otomatik/kapalı/açık), gürültü (kapalı/NLMeans/hqdn3d) +
  güç, keskinlik, döndür/çevir, renk matrisi. Onay kutuları: telesine geri alma, blok, bant, gri.
- Denetim değişince metin `Format(Parse(metin) with {...})` olur; kırpma ve kenar metinde kalır.
  Metin değişince geçerliyse denetimler eşitlenir, plan yeniden hesaplanır (kayıp `Watch`).
- Anahtarlar 42 dilde; yerleşim denetçisi 0 kusurda kalır.

`SuzgecPaneliTests`: gidiş-dönüş her seçenekte (olumsuz kontrol: varsayılan boş metin), denetim →
metin → `PlanOptionsForTest().Filters`, metin → denetim, bozuk metin denetimleri bozmaz, kırpma
denetim değişiminde korunur, anahtarlar 42 dilde. En az üç mutasyon.

## C1-6 — Ses ve Altyazı Kolları (22 Eylül 2026)

B1'in ses yüksekliği, kazanç, dış altyazı ve altyazı yakma kolları motorda ve CLI'da vardı,
arayüzde yoktu. Ses bölümüne iner (`MainWindow.Izler.cs`):

- `loudnorm` onay kutusu ve -12..+12 dB kazanç (0 dB plana yazılmaz).
- "Altyazı dosyası ekle" (çoklu seçim) ve kaynak açıkken bırakılan `.srt/.ass/.ssa/.vtt`;
  liste satırı ad · dil ve çıkarma düğmesi. Tekrar eklenmez.
- Yakma listesi yalnız kaynağın metin altyazılarını sunar; PGS listede yok, altyazısız
  kaynakta kutu kapalı. Seçim altyazılar içindeki 0 tabanlı sıraya iner.
- Dış altyazı ve yakma o videoya ait: yeni kaynakta sıfırlanır, kuyruk penceresine gitmez.
- Bölüm adı "Ses ve Altyazı"; on anahtar 42 dilde.

`IzPaneliTests`: ses kolları plana (0 dB olumsuz kontrol), ekleme/tekrar/çıkarma, yakma listesi
yalnız metin, altyazısız kaynak, yeni kaynak sıfırlaması, kuyruğun izleri taşımaması (ses
kolları geçer), anahtarlar 42 dilde. Beş mutasyonun beşi kırmızı.
