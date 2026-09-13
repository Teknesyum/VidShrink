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

## Durum — 13 Eylül 2026

E, F, G ve H kuruldu. Ölçüler `docs/arastirma/ikon-estetigi.md`'den geldi;
`IkonKutusuTests` 26 yolun kutusunu, `GelistiriciSekmesiTests` gizli sekmenin
sayacını pimliyor. Kanıt kareleri `.calisma/kesit-ef/`.

Açık kalan iki madde: **1** (ölü `v0.4.3` etiketi — kanca hem Bash'i hem
PowerShell'i durduruyor, tek cümlelik onay bekliyor) ve **15** (kaydedici reddi —
hangi özelliğin eksik olduğu söylenmeden yeniden kurulmuyor).


---

# Plan — Kaydedicide otomatik varsayılan, hedef süre ve hedef MB (13 Eylül 2026)

Girdi: kullanıcının 13 Eylül 2026 ikinci turu, 2c maddesi.

> ayrıca en iyi ayarı program nasıl ayarlıyorsa shrink için burdada en iyi ayarı kullanıcı
> değil program ayarlayacak kullanıcı dilerse istediği tahmini süre ve istediği tahmini mb
> yi ayarlayabilecek ancak ayarlamasa bile otomatik en iyi sonuçlarla işlem yapıcağız

## Karar

Otomatik kip **varsayılan** olur. Onay kutusunun anlamı ters çevrilir: `ChkAuto`
("Otomatik ayar") gider, yerine `ChkManual` ("Kendim ayarlayacağım") gelir ve işaretsiz
başlar. Elle panel yalnız bu kutu işaretlenince görünür.

Hedef süre ve hedef MB **iki isteğe bağlı kutu**. İkisi de doluysa bit hızı hesaplanır ve
aday merdiveninin üstüne yazılır; biri boşsa kalite kolu (CRF/CQP) olduğu gibi kalır.

Formül OBS'in tampon hesabının tersi, katsayı OBS'in yazımı:

    video_kbps = (hedef_MB × 8 × 1024 × 1024 / 1000) / süre_sn − ses_kbps × iz_sayısı

Kayıt gerçek zamanlı olduğu için iki geçiş yok; tavanlı bit hızı kullanılır —
`BitrateKbps = MaxBitrateKbps = kbps`, `BufferKbits = 2 × kbps`. Hedef süre ayrıca
`MaxDuration`'a yazılır, yani kayıt kendi kendine biter.

Hesap taban bit hızının altına düşerse (`RecorderBudget.MinimumVideoKbps`) bütçe
uygulanmaz ve ekranda "hedef çok küçük" denir; sessizce bozuk kayıt üretilmez.

## Dokunulan dosyalar

1. `src/VidShrink.Core/RecorderBudget.cs` — yeni, saf hesap.
2. `src/VidShrink.Core/RecorderAutoPlan.cs` — `ApplyBudget`.
3. `src/VidShrink.App/Recorder/RecorderSettings.cs` — `ManualMode`, `TargetSeconds`,
   `TargetMegabytes`; eski `autoMode` anahtarı okunmaya devam eder.
4. `src/VidShrink.App/Recorder/RecorderView.axaml` — kutunun tersi, iki hedef kutusu.
5. `src/VidShrink.App/Recorder/RecorderView.Otomatik.cs` — `AutoMode` artık `!ManualMode`.
6. `src/VidShrink.App/Recorder/RecorderView.Hedef.cs` — bütçenin isteğe yazılması.
7. `src/VidShrink.App/Locales/<42 dil>/main.json` — yeni anahtarlar.
8. `tests/VidShrink.Tests/KayitButceTests.cs` — yeni ölçü.

## Ölçüler

- Boş hedef → bütçe yok, kalite kolu korunuyor (negatif kontrol).
- 10 MB / 30 sn → OBS katsayısıyla 2796 kbps toplam, eksi 160 ses = 2636 video.
- Süre var MB yok, MB var süre yok, sıfır ve negatif değerler → `null`.
- Taban altı hedef → `null`, sebep `TooSmall`.
- `ApplyBudget` kalite kolunu bit hızı koluna çeviriyor ve `Validate`'ten geçiyor.
- Varsayılan açılışta elle panel gizli, otomatik özet görünür.

# 2d — Güncelleme Paneli Ölçüte Getiriliyor

Ölçüt `pp/guncelleme-paneli.md`. Panel iki kanal: başlatıcının kurulum penceresi ve
uygulamanın günlük güncelleme rozeti. Bu tur başlatıcı kanalını ölçüte getiriyor.

Tek köprü bir durum nesnesi. İş tarafı ekrana yalnız `Step(yüzde, tavan, cümle)` ile
konuşuyor, çizen taraf `Advance()` ile bir kare ilerletip okuyor. Sayı arayüzde
uydurulmuyor.

**Tavan kuralı:** çubuk yüzdeye fark × 0,08 (en az 0,2) ile yaklaşır, yüzde durursa
tavana fark × 0,006 ile sürünür, yenileme 16 ms, yüzde geri gitmez. Uzayan adımda panel
yaşar ama sonraki adımın alanını yemez.

Ekranda her zaman üç şey var: cümle, yüzde, son dokuz günlük satırı. Son satır gövde
rengiyle vurgulu, öncekiler sönük; satır sarmıyor, GDI'nın `DT_END_ELLIPSIS`'i kırpıyor.

Durum renkleri: çalışırken vurgu, bitince `NeonSuccessColor`, hatada `NeonEmberColor`.

## Dokunulan dosyalar

1. `src/VidShrink.Core/InstallProgress.cs` — yeni, köprü ve tavan kuralı.
2. `src/VidShrink.Launcher/Splash.cs` — `Arm(InstallProgress)`, yüzde kutusu, günlük,
   belirli kipe geçen çubuk, dolan kısmın üstündeki tarama ışığı.
3. `src/VidShrink.Launcher/Program.cs` — dört adımın `Step` çağrısı ve `Finish`.
4. `tools/VidShrink.SplashGen/Program.cs` — panel dokuz satır günlük ve yüzde sütunu
   kadar büyüdü; `percent`, `log` kutuları ve `LogLines` belirteci gömülüyor.
5. `tests/VidShrink.Tests/KurulumIlerlemesiTests.cs` — yeni ölçü.
6. `tests/VidShrink.Tests/SplashTests.cs` — yeni yerleşim ve sözleşme pimleri.

## Ölçüler

- Çubuk yüzdeye fark × 0,08 ile yaklaşıyor, en az 0,2 adımla.
- Yüzde durunca tavana sürünüyor ve tavanı geçmiyor (20 500 kare).
- Geriye yazan adım yüzdeyi düşürmüyor; 0–100 dışı kırpılıyor.
- Günlük ekranda dokuz satır, diskte tamamı.
- Görüntüye gömülü `LogLines` ile `InstallProgress.LogLines` eşit.
- Panel yüksekliği başlık + durum + dokuz satır + çubuk aralıklarının toplamı.
- Durum cümlesi ile yüzde sütunu aynı satırda ve çakışmıyor.
- Başlatıcı dört `Step` ve bir `Finish` çağırıyor; tavanlar tek yönlü sıralı.
