# T163 — Arayüz: gelişmiş ayarlar bölümü ve önizleme/plan yer paylaşımı

**Tarih:** 05.09.2026 · **Sözleşme:** `.claude/relay/contracts/T163.md` (tur 2)

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

**Bulgu (K1'in bir uzantısı):** `TheAdvancedSectionIsCollapsedByDefault`'un kendi
yorumu "kapalıyken bugünkü sayfa görünümü değişmez" diyor ama bunu hiç ölçmüyor —
yalnız `AdvancedBody.IsVisible == false`'u kontrol ediyor. P4'ün ölçtüğü
`WindowLayoutTests` bunun **doğru olmadığını** gösterdi: bölümün her zaman görünen
başlık satırı (başlık + katlama düğmesi) tek başına sayfayı ~106-115 px uzattı
(bkz. aşağıdaki P4 bölümü). Cümle tabloyla çelişiyor — bu depoda tekrarlayan kusur
sınıfı. Düzeltme bu turun kapsamı dışında (yeni bir ölçü/tasarım kararı gerektirir);
**bildiriliyor**, T0 karar verir.

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
