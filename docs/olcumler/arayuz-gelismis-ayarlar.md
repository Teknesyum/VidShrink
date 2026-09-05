# T163 — Arayüz: gelişmiş ayarlar bölümü ve önizleme/plan yer paylaşımı

**Tarih:** 05.09.2026 · **Sözleşme:** `.claude/relay/contracts/T163.md` (tur 2, tur 3 eklemeleriyle)

Ölçüm yöntemi: `tests/VidShrink.Tests/AdvancedPanelTests.cs` ve
`tests/VidShrink.Tests/WindowLayoutTests.cs` içindeki başsız yerleşim tekniği —
pencere hiç gösterilmiyor, gerçek ekran gerekmiyor. Sayılar ve hata metinleri
aşağıda birebir alıntılanıyor; koşum sırasında `.calisma/T163/` altında tutulan
ham log dosyaları iş bitince silindi (proje kuralı, dosyalar zaten git'e girmiyordu).

## K1 — Nedenler bugün sığıyor mu?

Ölçüm: `TheMostReasonProducingPlanIsMeasuredBeforeAnyCeilingIsChanged`. En çok
gerekçe üreten gerçek bileşim (kilit kodek + preset + CRF + kip + ses kbps + ses
kanalı + min çözünürlük + min fps + kodlayıcı yolu) kurulup `PlanScroll.Extent.Height`
tasarım genişliğinde ölçülüyor.

```
gerekce sayisi: 11
kodlar: ManualEncoderPathSupersededByCodec, ManualAudioBitrateOverride,
        ManualAudioChannelsOverride, ResolutionScaled, BudgetBelowCeilingTwoPass,
        PredictedQualityEstimated, ManualMinResolutionOverride,
        ManualMinFpsOverride, ManualCrfOverride, ManualModeSupersededByCrf,
        ManualPresetOverride
PlanBody istenen yukseklik: 232 px
taban (PlanPanelMinHeight): 320 px, tavan (PlanPanelMaxHeight): 512 px
```

**Sonuç:** 11 gerekçe 232 px'e sığıyor; bugünkü taban (320) zaten bu yüksekliğin
üstünde. Kullanıcının şikâyeti panelin küçüklüğü değildi — danışma turunun
(`docs/danisma/004-arayuz-yonu-gorusu.md`) uyardığı tam da bu ihtimaldi.

## K2 — Varsayılan konum, olçüden mi geliyor?

K1'in ölçtüğü içerik (232 px) bugünkü tabana (320 px) zaten sığıyor. **`PlanPanelMinHeight`/
`PlanPanelMaxHeight`'e dokunulmadı** — dokunmak yeni bir sayı uydurmak olurdu, K1
bunu gerektirmediğini gösterdi. Ayırıcının varsayılan konumu bu iki belirtecin
arasında, sürüklenmemiş satır `Auto` kalıyor (bkz. K3).

## K3 — Ayırıcı

Üç ölçü, `DraggingTheSplitterStaysWithinFloorAndCeiling` / `TheSplitterPositionSurvivesAReopen`
/ `UntouchedSplitterRowStaysContentDriven` (21/21 yeşil, bkz. K9 çıktısı):

- Sürükleme taban/tavan içinde kalıyor (1, 5000, 420 istekleri hepsi kırpılıyor).
- Konum `layout.json`'a yazılıyor, yeniden açılışta geri yükleniyor.
- Sürüklenmemiş satır içerik güdümlü kalıyor (piksele dönmüyor) — T54/K4'ün "sabit
  oran yok, içeriğe göre büyüme hiç yok" kuralı korunuyor.

## K4 — Gelişmiş ayarlar bölümü, dokuz kalem

`ThereAreExactlyNineAdvancedControls` dokuzu sayıyor:
`CmbAdvMode, CmbAdvCrf, CmbAdvPreset, CmbAdvAudioKbps, CmbAdvAudioChannels,
CmbAdvMinResolution, CmbAdvMinFps, CmbAdvEncoderPath, CmbAdvCodecLock`.

`EachAdvancedControlDefaultsToAutomaticAndShowsWhatTheEngineChose` bu dokuzun her
biri için ayrı kol olarak koşuyor (9 InlineData) ve doğruluyor: varsayılan seçim
`Otomatik` (index 0), hint satırı motorun seçtiği değeri boş bırakmıyor.
`TheAdvancedSectionIsCollapsedByDefault` bölümün kapalı açıldığını doğruluyor.

**Tur 3'te kapatıldı — aşağıdaki "Q1" bölümüne bakın.** Tur 2'nin bulgusu şuydu:
`TheAdvancedSectionIsCollapsedByDefault` "kapalıyken bugünkü sayfa görünümü değişmez"
diyor ama bunu hiç ölçmüyordu; ölçülünce cümlenin **doğru olmadığı** çıktı.

## K5 — CRF sabitken hedef alanı

`TheTargetFieldReportsWhenCrfWins(lockCrf: true/false)` — CRF sabitlenince
`TxtTargetCrfLockedNotice` görünür oluyor ve boş kalmıyor; sabitlenmemişken gizli.

İki dil:

- tr: "CRF gelişmiş ayardan sabitlendi; hedef boyut artık zorlanmıyor, bir tahmine dönüştü."
- en: "CRF is fixed from the advanced settings; the target size no longer forces the plan, it is now only an estimate."

## K6 — İki dil eksiksiz

Eklenen anahtar sayısı: **en 40, tr 40**, anahtar kümeleri birebir aynı (`diff` boş).

```
main.reason.manual-*        19 anahtar (19 ManualReasonDebt kolu)
main.advanced.*             19 anahtar (9 kalem × label/now + mode/audio-channels/
                             encoder-path alt seçenekleri + title/error)
main.plan.splitter           1 anahtar
main.target.crf-locked       1 anahtar
Toplam                       40 anahtar, iki dilde de.
```

Ham çıktı — `en/main.json`'a eklenen 40 anahtar (`tr/main.json`'daki küme birebir aynı,
`diff` sonucu "ANAHTARLAR ESIT"):

```
main.advanced.audio-channels.label
main.advanced.audio-channels.mono
main.advanced.audio-channels.none
main.advanced.audio-channels.stereo
main.advanced.audio-kbps.label
main.advanced.codec-lock.label
main.advanced.crf.label
main.advanced.encoder-path.hardware
main.advanced.encoder-path.label
main.advanced.encoder-path.software
main.advanced.error
main.advanced.min-fps.label
main.advanced.min-resolution.label
main.advanced.mode.crf
main.advanced.mode.label
main.advanced.mode.two-pass
main.advanced.now
main.advanced.preset.label
main.advanced.title
main.plan.splitter
main.reason.manual-audio-bitrate-override
main.reason.manual-audio-bitrate-superseded
main.reason.manual-audio-bitrate-unmet
main.reason.manual-audio-channels-override
main.reason.manual-audio-channels-unmet
main.reason.manual-crf-clamped
main.reason.manual-crf-override
main.reason.manual-encoder-path-override
main.reason.manual-encoder-path-superseded
main.reason.manual-encoder-path-unmet
main.reason.manual-min-fps-override
main.reason.manual-min-fps-unmet
main.reason.manual-min-resolution-override
main.reason.manual-min-resolution-unmet
main.reason.manual-mode-override
main.reason.manual-mode-superseded-by-crf
main.reason.manual-override-dropped-on-pass-through
main.reason.manual-preset-first-pass-relaxed
main.reason.manual-preset-override
main.target.crf-locked
```

## K7 — Renk ve ölçü uydurulmadı

`Theme.axaml` bu turda **hiç değişmedi** (`git diff main...HEAD -- Themes/Theme.axaml`
boş). Tek yeni belirteç `Controls.axaml`'de:

| Belirteç | Kaynak |
|---|---|
| `PlanSplitter.Height` | `{StaticResource SpaceMd}` — var olan boşluk belirteci |
| `PlanSplitter.Background` | `{StaticResource NeonBlueBorder}` — var olan renk belirteci |
| `PreviewPlanGrid.RowSpacing` | `{StaticResource SpaceMd}` |

Yeni bir piksel/renk değeri **uydurulmadı**.

## K8 — Mutasyon

Her mutasyondan önce `dotnet build -c Release --no-incremental` çalıştırıldı,
sonra ilgili filtreli koşum, sonra kod geri alındı ve build tekrar doğrulandı.

| # | Mutasyon | Kırılan ölçü |
|---|---|---|
| a | `OnSplitterMoved` kaydı kaldırıldı | `AdvancedPanelTests.TheSplitterPositionSurvivesAReopen` — "Ayırıcı konumu diske yazılmadı." |
| b | `CmbAdvCrf` → `PlanOptions.LockedCrf` bağı kesildi | `AdvancedPanelTests.TheTargetFieldReportsWhenCrfWins(lockCrf: True)` — Expected True, Actual False |
| c | `main.plan.splitter` yalnız `en`'de bırakıldı (tr'den silindi) | `LocalizationTests.KodunCagirdigiHerAnahtarIkiKatalogdaDaVar` ve `LocalizationTests.SevkiyattakiDillerinAnahtarKumesiIngilizceyleBirebirAyni` — ikisi de kırmızı |

Üç mutasyon da geri alındı; `git diff --stat` mutasyon sonrası yalnız
`WindowLayoutTests.cs`'i (P4 düzeltmesi) gösteriyor.

## K9 — Kol sayısı

```
dotnet test -c Release --no-build --filter "FullyQualifiedName~AdvancedPanelTests" --list-tests
```

21 kol bulundu (sıfır-kol riski yok):

```
TheMostReasonProducingPlanIsMeasuredBeforeAnyCeilingIsChanged
TheCeilingFitsTheMostReasonProducingContent
DraggingTheSplitterStaysWithinFloorAndCeiling(requested: 1)
DraggingTheSplitterStaysWithinFloorAndCeiling(requested: 5000)
DraggingTheSplitterStaysWithinFloorAndCeiling(requested: 420)
TheSplitterPositionSurvivesAReopen
UntouchedSplitterRowStaysContentDriven
EachAdvancedControlDefaultsToAutomaticAndShowsWhatTheEngineChose(comboName: "CmbAdvMode", ...)
EachAdvancedControlDefaultsToAutomaticAndShowsWhatTheEngineChose(comboName: "CmbAdvCrf", ...)
EachAdvancedControlDefaultsToAutomaticAndShowsWhatTheEngineChose(comboName: "CmbAdvPreset", ...)
EachAdvancedControlDefaultsToAutomaticAndShowsWhatTheEngineChose(comboName: "CmbAdvAudioKbps", ...)
EachAdvancedControlDefaultsToAutomaticAndShowsWhatTheEngineChose(comboName: "CmbAdvAudioChannels", ...)
EachAdvancedControlDefaultsToAutomaticAndShowsWhatTheEngineChose(comboName: "CmbAdvMinResolution", ...)
EachAdvancedControlDefaultsToAutomaticAndShowsWhatTheEngineChose(comboName: "CmbAdvMinFps", ...)
EachAdvancedControlDefaultsToAutomaticAndShowsWhatTheEngineChose(comboName: "CmbAdvEncoderPath", ...)
EachAdvancedControlDefaultsToAutomaticAndShowsWhatTheEngineChose(comboName: "CmbAdvCodecLock", ...)
ThereAreExactlyNineAdvancedControls
TheAdvancedSectionIsCollapsedByDefault
TheTargetFieldReportsWhenCrfWins(lockCrf: False)
TheTargetFieldReportsWhenCrfWins(lockCrf: True)
AnInvalidAdvancedCombinationIsReportedInsteadOfCrashing
```

Tam koşum: `Başarılı! - Başarısız: 0, Başarılı: 21, Atlanan: 0, Toplam: 21, Süre: 9 s`.

## P4 — WindowLayoutTests etkisi (risk kapatıldı)

`PreviewPlanGrid`'e üçüncü satı (`GridSplitter`, K3) eklenince orta sütunun satır
sayısı 2'den 3'e çıktı ve sayfa içeriği büyüdü. Filtresiz öncesi koşum
**11/40 kırmızı** verdi:

- `TheThreeColumnsStartAtTheSameTop` (4 kol): orta sütun satır sayısı pin'i 2 idi,
  gerçek 3 — leftover boş satır değil, ayırıcının kendi gerçek satırı. Pin 3'e
  güncellendi.
- `ThePageStopsScrollingAtThisHeight`: boş sayfa 1039-1129 → gerçek 1200; dolu
  sayfa 1007-1097 → gerçek 1168. Pinler 1155-1245 / 1123-1213'e taşındı (±45
  korunarak).
- `ThePageContentStaysAtItsPinnedHeight` (4 kol): dört aralık da ~115 px büyüdü
  (939-1039→1054-1154, 960-1060→1075-1175, 906-1006→1021-1121 iki kez). Aralık
  genişliği (100) korunarak yeniden temellendi.
- `ThePageScrollsAtMostDownAtTheDesignSize(loaded: true)`: tasarım boyutunda dolu
  sayfa artık +106 px dikey taşıyor. Boş sayfa için zaten kabul edilmiş olan
  "yalnız sayfanın kendi kaydırıcısı" toleransı dolu hâle de genişletildi; yatay
  eksen hâlâ hiç kaymıyor.

Kaynak: her zaman görünen gelişmiş ayarlar başlığı (K4) sol sütunu, ayırıcının
satırı (K3) orta sütunu uzattı — ikisi de bu sözleşmenin istediği, kaçınılmaz
büyüme. Düzeltme sonrası koşum: **40/40 yeşil**, 6 dk 52 sn.

## P5 — OluUyeTests beklenen kırılma (owns dışı, düzeltilmedi)

`tests/VidShrink.Tests/OluUyeTests.cs` bu sözleşmenin `owns` listesinde değil.
Filtreli koşum: **12/13 yeşil, 1 kırmızı** (`TheZeroConsumerSetIsThePinnedSet`). 19 `ReasonCode.Manual*` artık MainWindow'un
gerekçe→cümle anahtarında tüketiliyor (K4/K6), bu yüzden "sıfır tüketici" pinli
kümesinden düşüyorlar; ayrıca `EncoderPathOverride.Software` yan etkiyle
`yalniz-disarida`'dan `varsayilan-kol`'a geçti. Beklenen kırılma bu — **düzeltilmedi**,
T0 ayrı bir sözleşmeyle kapatır.


---

# Tur 3 — Q1 ve Q2

Tur 3'te iki bulgu kapatıldı. Aşağıdaki bütün sayılar `dotnet test -c Release` ile
ölçüldü; her ölçümden önce `dotnet build -c Release --no-incremental` koştu.

## Q1 — Kapalı bölümün sayfa yüksekliğine katkısı artık sıfır

### Neyin ölçüldüğü

Sayfanın boyunu tasarım boyutunda (1560x1060) **sol ayar sütunu** belirliyor. Üç sütunun
istediği yükseklik, dolu sayfa, tur 3 sonrası:

| sütun | istenen yükseklik |
|---|---|
| sol (kaynak + hedef panelleri) | **940 px** |
| orta (önizleme + ayırıcı + plan) | 906 px |
| sağ (çıktı) | 512 px |

Sayfa içeriği = 940 + 16 (üst kenar boşluğu) = 956; tasarım boyutunun görüş alanı 965.
Yani dolu sayfa tasarım boyutunda **tam oturuyor**.

### T163 öncesiyle karşılaştırma

`main` üzerindeki T163 öncesi commit `8da585f` aynı düzenekle ölçüldü:

| ölçü | 8da585f (T163 öncesi) | T163 tur 2 | T163 tur 3 |
|---|---|---|---|
| sol sütun (dolu) | 940 | 1043 | **940** |
| hedef paneli | 657 | 669 | **657** |
| orta sütun (dolu) | 882 | 906 | 906 |
| `PageShrink` dikey taşma (dolu, tasarım boyutu) | 0 | +106 | **0** |

Tur 3'ün sol sütunu T163 öncesiyle **birebir aynı**. Yani "kapalıyken bugünkü sayfa
görünümü değişmez" (K4) artık ölçüyle karşılanıyor.

### Fark nereden geliyordu — iki ayrı kaynak

Tur 2'nin +115'i tek bir şey değildi, iki şeydi:

1. **+103 px — gelişmiş ayarlar kendi `Border` panelindeydi.** Panelin dolgusu, her zaman
   görünen `H2` başlığı, katlama düğmesi ve sol sütunun `SpaceLg` (16) aralığı birlikte
   sol sütunu 940'tan 1043'e çıkarıyordu. Kapalı bir bölümün her zaman görünen başlığı
   olduğu sürece bu bedelin sıfır olmasının tek yolu, başlığın **zaten var olan** bir
   satıra girmesidir.

   **Yapılan:** katlama düğmesi hedef panelinin başlık satırına (`main.target.title` +
   bilgi rozeti) taşındı; gövde (`AdvancedBody`) aynı panelin en altına girdi. Satır zaten
   vardı, bedel sıfır oldu. Düğmenin kendi boyu satırı büyütmesin diye
   `Width`/`Height` = `TargetMinSize` (24) ve `Padding="0"` verildi — ikisi de var olan
   belirteç/sıfır, **yeni ölçü uydurulmadı** (K7).

   Ölçü: bu sabitleme olmadan `GhostButton`'ın `ButtonPaddingSm` dolgusu satırı 24'ten
   35'e çıkarıyor ve sol sütun 951 oluyordu (+11).

2. **+12 px — K5'in uyarı satırı ızgaraya altıncı bir satır açıyordu.** `TxtTargetCrfLockedNotice`
   gizliyken bile `RowDefinitions` beş satırdan altıya çıkmıştı; `RowSpacing` (`SpaceMd`
   = 12) beşinci aralığı ekliyordu. Gizli `TextBlock`'un kendi yüksekliği sıfırdı, bedeli
   ödeyen **satır aralığıydı**.

   **Yapılan:** uyarı satırı, yonga (`WrapPanel`) satırıyla aynı ızgara satırını paylaşan
   bir `StackPanel`'e alındı; ızgara beş satıra döndü. `StackPanel` aralığı görünmeyen
   çocuk için oluşmuyor, yani gizliyken bedel yine sıfır.

### Kriterin iki yarısını da ölçen kol

`AdvancedPanelTests.TheCollapsedAdvancedSectionCostsThePageNoHeight` —
`TheAdvancedSectionIsCollapsedByDefault`'un yerine geçti. Üç ölçü:

```
sol sutun, bolum kapali: 940 px
sol sutun, bolum yerlesimden cikarilmis: 940 px
sol sutun, bolum acik: 1822 px
kapali bolumun bedeli: 0 px
```

- **Birinci yarı:** `AdvancedBody.IsVisible == false`.
- **İkinci yarı:** sol sütun iki kez ölçülüyor — bölüm olduğu gibi (kapalı) ve katlama
  kolu yerleşimden tümüyle çıkarılmış hâlde. Fark 0.
- **Üçüncü ölçü, boş eşitliğe karşı:** bölüm açılınca sütun büyümeli (940 → 1822).
  Büyümüyorsa ilk iki sayının eşitliği hiçbir şey söylemiyordur ve kol düşer.

Üçüncü ölçü bir kez **gerçekten kurtardı**: ilk yazımda `LayOutAt` bir görünürlük
değişikliğinden sonra hiç yeniden ölçüm koşturmuyordu (başsız pencerede ölçüm önbelleği
geçerli kalıyor), üç sayı da 973 çıkıyordu ve "fark 0" boş bir eşitlikti. Kol bunu
yakaladı; düzeltme `Relayout(...)` yardımcısı — ölçümden önce bütün alt düğümlerde
`InvalidateMeasure()`.

### Bu kol hangi mutasyonla düşer

| # | mutasyon | sonuç | düşen kol |
|---|---|---|---|
| M1 | Katlama düğmesinden `Width`/`Height`/`Padding` sabitlemesini kaldır | sol sütun 940 → **951** | `TheCollapsedAdvancedSectionCostsThePageNoHeight`: *"kapalı bölümün sayfa yüksekliğine katkısı sıfır olmalı. Sol sütun bölümle 951, bölüm yerleşimden çıkarılınca 940 (fark 11 px)."* |
| M2 | K5 uyarısını yeniden kendi ızgara satırına al (altıncı `RowDefinition`) | dolu sayfa **+3 px** taşar | `ThePageScrollsAtMostDownAtTheDesignSize(loaded: True)`: *"Dolu pencerede 1560x1060: PageShrink: dikey +3, yatay +0"* |
| M3 | (tur 2'de koşuldu) Ayırıcı konumunun kaydını kaldır | — | `TheSplitterPositionSurvivesAReopen` |

M1 ve M3 aynı kolu vurmuyor; ikisi Q1'in iki ayrı kaynağına karşılık geliyor. Her ikisinden
önce `dotnet build -c Release --no-incremental` koştu, `--no-build` kullanılmadı.

## Q2 — Gevşetilen tolerans geri alındı, pinler eski değerlerine döndü

Tur 2 dört ölçüyü yeniden temellendirmiş ve bir toleransı genişletmişti. **Tur 3'te
beşinin dördü tümüyle geri alındı**, biri (satır sayısı) meşru olarak kaldı:

| ölçü | tur 2'de | tur 3'te | durum |
|---|---|---|---|
| `ThePageScrollsAtMostDownAtTheDesignSize(loaded: true)` | dolu hâl için tolerans genişletildi (+106 kabul) | **eski katı hâli**: dolu sayfa hiç kaymaz | geri alındı |
| `ThePageContentStaysAtItsPinnedHeight` (4 kol) | 939-1039 → 1054-1154 vb. | **939-1039, 960-1060, 906-1006, 906-1006** | geri alındı |
| `ThePageStopsScrollingAtThisHeight` (2 kol) | 1039-1129 → 1155-1245 vb. | **1039-1129, 1007-1097** | geri alındı |
| `TheThreeColumnsStartAtTheSameTop` orta sütun satır sayısı | 2 → 3 | **3 kalıyor** | meşru: `GridSplitter` gerçek bir satır (K3), boş kalıntı değil |

Koşum: `dotnet test -c Release --filter "FullyQualifiedName~WindowLayoutTests"` →
**40/40 yeşil**, 4 dk 39 sn. Yani iddia zayıflatılarak değil, **yerleşim düzeltilerek**
geçti.

Orta sütun ayırıcı yüzünden 882'den 906'ya çıktı ve bu **kalıyor** — ama orta sütun
sayfanın boyunu belirlemiyor (940 > 906), o yüzden sayfaya yansımıyor.

## Tur 3 koşumları

| kol kümesi | sonuç | süre |
|---|---|---|
| `dotnet build -c Release --no-incremental` | 0 uyarı 0 hata | ~3 sn |
| `--filter "FullyQualifiedName~AdvancedPanelTests" --list-tests` | **21 kol** (sıfır-kol riski yok) | — |
| `--filter "FullyQualifiedName~AdvancedPanelTests"` | **21/21 yeşil** | 10 sn |
| `--filter "FullyQualifiedName~WindowLayoutTests"` | **40/40 yeşil** | 4 dk 39 sn |

`OluUyeTests`in beklenen kırılması (`owns` dışı) tur 3'te **ele alınmadı** — dokunulmadı,
T0'ın ayrı sözleşmesinde kalıyor.

---

# Tur 4 — R1, R2, R3

## R1 — Katlama düğmesi bağlı değildi

Tur 3 düğmeyi hedef panelinin var olan başlık satırına taşırken (Q1'in "kapalı bedel
0 px" kazancı) **olay bağlantısını birlikte taşımadı**. `9580bb5`'te
`MainWindow.axaml:299-313` içindeki `BtnAdvancedToggle`da `Click` yok; tur 2'de
(`9f740f1:src/VidShrink.App/MainWindow.axaml:486`) `Click="OnToggleAdvanced"` vardı.

Sonuç: çalışan uygulamada `AdvancedBody.IsVisible` sonsuza dek `False` — dokuz gelişmiş
kontrolün hiçbirine kullanıcı erişemiyordu. K4'ün "bölüm katlanır" yarısı
karşılanmıyordu.

Düzeltme tek satır: `MainWindow.axaml:302` `Click="OnToggleAdvanced"`. Yeni renk, yeni
ölçü, yeni belirteç yok; `Theme.axaml` tur 4 diff'inde de **hiç yok**.

Bu, bu deponun ölçülmüş kusur sınıfının bu sözleşmedeki **ikinci** tekrarı: ölçüyü
düzeltirken açılan yeni giriş noktası kusuru kapatmaz, taşır.

## R2 — Yirmi bir kolun hiçbiri R1'i göremiyordu

Bütün kollar bölümü `window.ExpandAdvanced()` **test kancasıyla** açıyordu, düğmeye
basarak değil. Düğme tümden silinse 21/21 yeşil kalırdı.

Üç değişiklik:

- Yeni yardımcı `ClickAdvancedToggle(window)` — kancayı hiç kullanmıyor, gerçek
  `Button.ClickEvent` yükseltiyor. Bağlantı kopuksa hiçbir şey olmuyor.
- Yeni kol `TheAdvancedSectionOpensAndClosesFromItsButton` — kapalı başlıyor, birinci
  tıklamada açılıyor, ikincide kapanıyor; yön oku da ölçülüyor.
- `TheCollapsedAdvancedSectionCostsThePageNoHeight`in üçüncü ölçüsü artık düğmeye
  tıklayarak açıyor, kancayla değil.

Ham çıktı (bağlantı yerinde):

```
baslangic: gorunur=False, ok=▾
birinci tiklama: gorunur=True, ok=▴
ikinci tiklama: gorunur=False, ok=▾
sol sutun, bolum kapali: 940 px
sol sutun, bolum yerlesimden cikarilmis: 940 px
sol sutun, bolum acik: 1822 px
```

**Mutasyon** — `Click="OnToggleAdvanced"` silindi,
`dotnet build -c Release --no-incremental` (0 hata), sonra filtreli koşum:

```
Başarısız VidShrink.Tests.AdvancedPanelTests.TheAdvancedSectionOpensAndClosesFromItsButton [321 ms]
   R1: düğmeye basıldı ama gelişmiş bölüm açılmadı — Click bağlantısı yok, çalışan uygulamada dokuz kontrole erişilemez.
Başarısız VidShrink.Tests.AdvancedPanelTests.TheCollapsedAdvancedSectionCostsThePageNoHeight [612 ms]
   Ölçü boşa düşüyor: bölüm açılınca sol sütun büyümedi (kapalı 940, açık 940).
Başarısız! - Başarısız: 2, Başarılı: 20, Atlanan: 0, Toplam: 22
```

`Click` kalktığında **iki kol** düşüyor. Tur 3'te sıfır kol düşerdi.

## R3 — `TheCeilingFitsTheMostReasonProducingContent` boş geçiyordu

Assert yalnız `Ceiling >= Floor` (512 >= 320) kıyaslıyordu; `MeasureMaxReasonLayout`
pahalı yerleşim ölçüsünü kurup **atıyordu**. Kolun adı ve K2 iddiası ise "tavan, ölçülen
içeriğe sığar" diyor.

Assert artık ölçülen yüksekliği tavanla karşılaştırıyor (`Ceiling >= PlanBodyHeight`),
ve boş ölçüye karşı `PlanBodyHeight > 0` bekçisi var. Ham çıktı:

```
olculen icerik: 232 px, taban: 320 px, tavan: 512 px
```

512 >= 232 — K1'in sayısı değişmedi, iddia artık gerçekten ölçülüyor.

## Tur 4 koşumları

| kol kümesi | sonuç | süre |
|---|---|---|
| `dotnet build -c Release --no-incremental` | 0 uyarı 0 hata | ~3 sn |
| `--filter "FullyQualifiedName~AdvancedPanelTests" --list-tests` | **22 kol** (sıfır-kol riski yok) | — |
| `--filter "FullyQualifiedName~AdvancedPanelTests"` | **22/22 yeşil** | 10 sn |
| `--filter "FullyQualifiedName~WindowLayoutTests"` | **40/40 yeşil** | 6 dk 35 sn |

`OluUyeTests`in beklenen kırılması `owns` dışında; tur 4'te de dokunulmadı.
