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
