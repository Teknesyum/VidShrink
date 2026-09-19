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

## Kapsam dışı

- ffmpeg/mpv **argümanı** üreten biçimler (kullanıcıya gösterilmiyor, ondalığı protokol
  belirliyor): `ClipExport.cs:101`, `SegmentEncoder.cs:383`, `ToolsOptions.cs:132`,
  `MpvEngine.cs:504`, `OvershootTrimmer.cs:123`, `FrameGrabber.cs:266`.
- `_trace.Add(...)` satırları — test izi, kullanıcı yüzeyi değil.
