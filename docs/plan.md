# Plan — 15 işlik tur: simge takımı, anahat dili, oynatıcı barları, güncelleme paneli

Girdi: kullanıcının 13 Eylül 2026 turu (15 madde) ve iki eski cümlesi (aşağıda verbatim).
`docs/arastirma/ikon-estetigi.md` araştırması bu planla paralel koşuyor; 5-8, 10 ve 14
numaralı maddelerin geometri kararı o rapor gelince kesinleşir.

## Kullanıcının kendi cümleleri (2. madde buna bakıyor)

**11 Eylül 2026, 13:48** — başlık çubuğu düğmeleri:

> üstteki tuşların anahattı ince görünmez bir gri gibi olsun daha karemsi olsun kenarları
> fare ile üzerlerine geldiğimde ancak mavi şuanki anahat olsun ayarlar sağda teknesyum
> sponsor vb yazan yerin hemen solunda (dil ayarlarının sağında) olsun
> anahat header içinde mükemmel yükseklikte olamalı fare üzerinde değilken anahat yok gibi olmalı

"Keskin köşe" diye not ettiğim şey buradaki **"daha karemsi olsun kenarları"**. 10. madde
aynı kuralın bütün programa genelleştirilmiş hâli.

**12 Eylül 2026, 18:34** — güncelleme paneli (4. ve 13. madde buna bakıyor):

> yeni sürüm uyarısında tek satır güncelleme kodu veriyor powershell için ancak yükle tuşu
> da olması lazım hatta powershell vermesin yükle tuşu çıksın sadece güncelleme sonrası
> otomatik güncelleme ayarımız değişmemeli

`pp`'de güncelleme panelinin tarzı **yazmıyor**. Özel rafta yalnız şu var
(`private/tercihler/depo.md:11`): "Kur penceresi ilk kurulum içindir; sil-baştan-kur yalnız
`-Onar` ile, günlük güncelleme uygulamanın kendi senkronunda." Panelin biçimine dair bir
satır yok; bu turda yazılıyor.

## Kesitler

Bağımlılık tek yönlü: **E → F → G**, **H** bağımsız.

### Kesit E — anahat dili (10, 2)

Tek bir durum sözleşmesi, üç yerde aynı: başlık çubuğu düğmeleri, üst sekme şeridi, sayfa
içi düğmeler.

- Dinlenirken: kenarlık `HeaderRestBorder` (zaten var, "yok gibi gri"), kalınlık `BorderThin`.
- Fare üstündeyken: kenarlık `NeonBlueBorderStrong`, kalınlık yeni `BorderRegular` belirteci.
- Köşe: `RadiusChip` (6) yerine daha karemsi bir yeni belirteç.

Renk ve ölçü uydurulmuyor: kenarlık fırçaları paletten, yeni kalınlık ve yarıçap
`Themes/Theme.axaml` belirteci olarak bir kez tanımlanıyor.

### Kesit F — simge takımı (5, 6, 7, 8, 14)

`Themes/Icons.axaml` elle çizilmiş 26 `StreamGeometry` taşıyor. Sorunlar ölçülebilir:

| # | Simge | Kusur |
|---|---|---|
| 5 | `IconPause` | `M 9,4 V 20 M 15,4 V 20` — iki çubuk 9 ve 15'te, `Stretch="Uniform"` yalnız 9..15 mürekkebini ölçüyor; `IconPlay` 6..20 ölçülüyor. İki simge aynı kutuda farklı büyüyor, duraklat sağa kayıyor. |
| 6 | `IconMinimize` | `M 4,12 H 20` — tek yatay çizgi. |
| 7 | hepsi | Mürekkep sınırı 24×24 tasarım kutusundan küçük olduğunda `Uniform` mürekkebi ortalıyor, kutuyu değil. `IconCoffee` bunu iki boş `MoveTo` ile çözmüş; geri kalan 25 simgede aynı çözüm yok. |
| 8 | `IconCode` | `M 9,6 L 3,12 L 9,18 M 15,6 L 21,12 L 15,18` — Teknesyum imzasının `<>` işareti. |
| 14 | `IconSettings` | Nokta + sekiz kısa çentik. Önceki simge `Content="⚙"` metin karakteriydi (`06f2112b` öncesi `MainWindow.axaml:65`). |

Karar: takım araştırmanın önerdiği **lisansı temiz hazır setten** alınıp `StreamGeometry`'ye
çevriliyor, elle yeniden çizilmiyor. 7'nin yapısal çözümü her simgeye `IconCoffee`'nin iki
boş `MoveTo`'su — 24×24 kutuyu sabitleyen iki komut.

### Kesit G — oynatıcı barları (9)

`PlayerView.axaml:131` ses, `:175` hız. İkisi de `PlaybackSlider`.

- İkisi de uzar: genişlik belirteçten, `SpaceSm` boşlukla.
- Ses: `TickFrequency=5`, `IsSnapToTickEnabled` — 5'in katlarına oturur.
- Hız: `TickFrequency=0.05`.
- `BtnSeritMute`'un hız karşılığı yok; hız simgesi düğmeye dönüyor. ×1 değilken basınca 1'e,
  1'deyken basınca bir önceki hıza döner. Önceki hız alanda tutulur.

### Kesit H — güncelleme paneli ve geliştirici sekmesi (4, 13, 12)

- `MainWindow.axaml:167-180`: `TxtNoticeCommand` ve `BtnNoticeCopy` kaldırılıyor.
  `BtnNoticeInstall` panelin tek eylemi. Kullanıcının 18:34 cümlesi zaten bunu diyordu;
  13. madde onun tekrarı.
- Kaldırılan iki anahtar 42 dilden düşüyor → `BiciminTests` sayım pinleri yeniden ölçülür.
- `TabItem main.tab.advanced` görünürlükten çıkıyor. Hakkında sekmesinde sürüm satırına
  arka arkaya tıklamak sekmeyi açıyor; sekmenin içinde onu tekrar kapatan bir düğme var.
  Kaç tık gerektiği ve sayaç penceresi kod tarafında tek yerde, testle pimli.
- Davranış değiştiği için iki README aynı commit'te güncelleniyor.

## Kapsam dışı

- 3. madde: cevap verildi, iş yok.
- 11. madde: araştırma alt ajanda, çıktısı `docs/arastirma/ikon-estetigi.md`.
- 15. madde: kaydedicinin reddi — kullanıcı neyin eksik olduğunu söylemeden yeniden
  kurulmuyor, `.claude/jobs.md`'de gerekçeli açık duruyor.
