# Biçim Gövdeleri Taraması — 18 Eylül 2026

Kapsam: `src/` altı, `obj/` ve `bin/` hariç. Süre biçimlendirmesi `Core/Saat`'e indi
(kod borcu 10); bu tarama **kalan** aileleri sayıyor. Kod değiştirilmedi, yalnız okundu.

Kullanıcı isteği: *"düzenlerimiz tek ve belli ulaşılabilir bir yerde olmalı aynı
stringlerimizi çevirmek nasıl kolaysa bunları çevirmekte öyle kolay olmalı."*

## Ortak gövdeler (önce bunlar)

Ayrı bir `Bicim`/`Format` sınıfı **yok**. Bunun yerine üç ayrı, birbirinden habersiz mini
yardımcı var:

| Yardımcı | Dosya:satır | Kültür | Kapsam |
|---|---|---|---|
| `MainWindow.Num(double, string)` | `src/VidShrink.App/MainWindow.axaml.cs:749` | `Strings.Culture` | App genelinde ~74 çağrı, **biçimi çağıran seçiyor** — yani gövde değil, sadece kültür taşıyıcısı |
| `CliApp.Num(double, string)` | `src/VidShrink.Cli/CliApp.cs:441` | `InvariantCulture` | CLI; App'teki ikiziyle **farklı kültür** |
| `MainWindow.Percent(double)` | `src/VidShrink.App/MainWindow.axaml.cs:756` | `Strings.Culture` | Tek çağrı yeri (3578) |
| `MainWindow.DescribeBytes(long)` | `src/VidShrink.App/MainWindow.axaml.cs:2161-2168` | `Num` üzerinden | Yalnız paylaşım tavanı, ikilik birim |
| `ShareErrorClassifier.Size(long)` | `src/VidShrink.Core/Share/ShareErrorClassifier.cs:264-279` | **yok** (current culture) | `DescribeBytes`'in ikizi, ondalık birim adı |
| `RecorderTray.Megabytes(double, CultureInfo)` | `src/VidShrink.App/Recorder/RecorderTray.cs:51` | dışarıdan | Yalnız tepsi |

`Num` bir gövde gibi görünüyor ama biçim dizgisi parametre olduğu için aileleri hiç
tekleştirmiyor: aynı "MB" değeri çağrı yerine göre `0.0`, `0.##`, `0.00`, `0.000`, `0`
yazılıyor.

---

## 1. Dosya boyutu (MB/GB/KiB)

| Dosya:satır | Satır (kısa) | Biçim | Invariant? |
|---|---|---|---|
| `App/MainWindow.axaml.cs:2163-2167` | `units = { "B","KiB","MiB","GiB","TiB" }` … `$"{Num(size,"0.##")} {units[unit]}"` | `0.##` + ikilik ad | `Strings.Culture` |
| `App/MainWindow.axaml.cs:3237` | `TxtSize.Text = Say("main.unit.mb-value", Num(info.FileSizeMb,"0.0"))` | `0.0` | `Strings.Culture` |
| `...:3451` | `Say("main.unit.mb-value", Num(target,"0.##"))` | `0.##` | ↑ |
| `...:3537` | `Say("main.unit.mb-value", Num(size.ExpectedMb,"0.0"))` | `0.0` | ↑ |
| `...:3573` | `Say("main.unit.mb-value", Num(estimate.ExpectedMb,"0.0"))` | `0.0` | ↑ |
| `...:3577` | `Say("main.unit.mb-range", Num(LowMb,"0.0"), Num(HighMb,"0.0"))` | `0.0` | ↑ |
| `...:3690,3692,3694` | `Num(note.Mb,"0.0")`, `Num(note.TargetMb,"0.##")` | `0.0` / `0.##` karışık | ↑ |
| `...:3699` | `Num(note.AudioMb,"0.00")` | `0.00` | ↑ |
| `...:3703` | `Num(note.BandLowerMb,"0.0")`, `Num(note.TargetMb,"0.0")` | `0.0` | ↑ |
| `...:3713` | `Num(note.Mb,"0.0"), Num(note.TargetMb,"0.##")` | karışık | ↑ |
| `...:3715` | `Num(note.Mb,"0.##"), Num(note.TargetMb,"0.##")` | `0.##` | ↑ |
| `...:4235` | `Say("main.run.no-space", Num(neededMb,"0"))` | `0` | ↑ |
| `...:4254,4263,4331` | `Say("main.unit.mb-value", Num(...,"0.0"))` | `0.0` | ↑ |
| `...:4266` | `Num(_info.FileSizeMb,"0.0"), Num(result.OutputMb,"0.0")` | `0.0` | ↑ |
| `...:4268` | `Num(result.OutputMb-targetMb,"0.00"), Num(targetMb,"0.##")` | `0.00`/`0.##` | ↑ |
| `...:4336-4346` | `Num(prompt.ActualMb,"0.0")`, `Num(prompt.TargetMb,"0.##")`, `Num(prompt.OverMb,"0.0")` | karışık | ↑ |
| `...:4394` | `Num(plan.KeptBytes / 1024.0 / 1024.0, "0.00")` | `0.00` + elle bölme | ↑ |
| `...:4584` | `Say("main.run.converted", Num(...,"0.0"), Num(...,"0.0"))` | `0.0` | ↑ |
| `App/ShrinkJobWindow.axaml.cs:285` | `(result.OutputMb - request.TargetMegabytes).ToString("0.00", Invariant)` | `0.00` | evet |
| `App/ShrinkJobWindow.axaml.cs:295` | `result.OutputMb.ToString("0.0", Invariant)` | `0.0` | evet |
| `App/Recorder/RecorderView.axaml.cs:168` | `result.OutputMb.ToString("0.0", Strings.Culture)` | `0.0` | hayır |
| `App/Recorder/RecorderTray.cs:51` | `mb.ToString("0.0", culture)` | `0.0` | hayır |
| `App/Recorder/RecorderView.Otomatik.cs:151` | `megabytes!.Value.ToString("0.#", Strings.Culture)` | `0.#` | hayır |
| `App/ShellMenu.cs:239-240` | `(megabytes/1024).ToString(Invariant) + " GB"` / `+ " MB"` | biçimsiz + elle " MB" | evet |
| `Core/ShellIntegration.cs:30-31` | `$"{megabytes / 1024} GB"` / `$"{megabytes} MB"` | biçimsiz interpolasyon | **HAYIR** |
| `Core/Share/ShareErrorClassifier.cs:266-278` | `units={"B","KB","MB","GB","TB"}`; `$"{Math.Round(value)} {units[unit]}"` / `$"{value:0.#} {units[unit]}"` | ham `Round` + `0.#` | **HAYIR** |
| `Cli/CliApp.cs:265,266` | `Num(DiskSpaceGuard.RequiredBytes(targetMb)/1024.0/1024.0, "0")` | `0` | evet |
| `Cli/CliApp.cs:297` | `Num(info.FileSizeMb,"0.0")` | `0.0` | evet |
| `Cli/CliApp.cs:299,301` | `Num(decision.TargetMb,"0.#")` / `"0.##"` | `0.#`/`0.##` | evet |
| `Cli/CliApp.cs:305` | `Num(ExpectedMb/LowMb/HighMb,"0.0")` | `0.0` | evet |
| `Cli/CliApp.cs:326` | `Num(result.OutputMb,"0.0"), Num(decision.TargetMb,"0.##"), Num(...FileSizeMb,"0.0")` | karışık | evet |
| `Cli/CliApp.cs:336` | `Num(result.OutputMb,"0.000")` | `0.000` | evet |
| `Core/PlanCalculator.cs:378,592,614,624,634,653,659,802,884,1119,1129,1130` | `$"... {effectiveTargetMb:0.##} MB ..."`, `{aimMb:0.0} MB`, `{actualMb:0.0} MB`, `{audioMb:0.00} MB` | `0.0`,`0.##`,`0.00` | **HAYIR** |
| `Core/BudgetFill.cs:56` | `{deliveredMb:0.###} MB … {targetMb:0.###} MB` | `0.###` | **HAYIR** |
| `Core/CeilingGuard.cs:52` | `{ceilingMb:0.###} MB` | `0.###` | **HAYIR** |
| `Core/Saturation.cs:31,45,84,85` | `{targetMb:0.###} MB`, `{actualMb:0.###} MB` | `0.###` | **HAYIR** |
| `Core/PlanParser.cs:124` | `$"…estimate to {estimated:0.0} MB, above the {options.TargetMb:0.##} MB target."` | `0.0`/`0.##` | **HAYIR** |
| `Core/PromptBuilder.cs:16,22` | `{info.FileSizeMb:0.0} MB`, `{options.TargetMb:0.##} MB` | `0.0`/`0.##` | **HAYIR** |
| `Ffmpeg/EncodeRunner.cs:376` | `{effectiveTargetMb:0.##} MB … {actualMb:0.0} MB` | `0.0`/`0.##` | **HAYIR** |

**Ayrı biçim sayısı: 9** — `0`, `0.#`, `0.##`, `0.0`, `0.00`, `0.000`, `0.###`, biçimsiz
(`{megabytes}`), `Math.Round(value)`.

**Ayrı birim tablosu: 3** — ikilik ad (`KiB/MiB/GiB`), ondalık ad ikilik bölmeyle
(`KB/MB/GB` ama `/1024`, `ShareErrorClassifier.cs:272` — **birim adı yanlış**), düz
`" MB"/" GB"` birleştirmesi (iki ayrı kopya: `ShellMenu.cs:239` ve `ShellIntegration.cs:30`).

**Invariant verilmeyen satır: 30+.**

---

## 2. Yüzde

| Dosya:satır | Satır (kısa) | Biçim | Invariant? |
|---|---|---|---|
| `App/MainWindow.axaml.cs:756` | `ratio.ToString("P1", Strings.Culture)` | `P1` | hayır |
| `App/MainWindow.axaml.cs:3578` | `+ Percent(estimate.ExpectedMb / Math.Max(_info.FileSizeMb, 0.01))` | ↑ | ↑ |
| `App/MainWindow.axaml.cs:3684` | `Num(note.ScalePercent, "0.#")` | `0.#` | `Strings.Culture` |
| `App/MainWindow.axaml.cs:3707` | `Num(note.Factor * 100, "0.#")` | `0.#` + elle `*100` | ↑ |
| `App/MainWindow.axaml.cs:3708` | `Num((note.TargetMb - note.BandLowerMb) / Math.Max(note.TargetMb,0.01) * 100, "0.#")` | `0.#` + `*100` | ↑ |
| `App/MainWindow.axaml.cs:3711` | `Num((1 - note.Factor) * 100, "0.#")` | `0.#` + `*100` | ↑ |
| `App/MainWindow.axaml.cs:4264-4266` | `var saved = 100 - result.OutputMb / _info.FileSizeMb * 100;` … `Num(saved,"0.#")` | `0.#` | ↑ |
| `App/MainWindow.axaml.cs:4339` | `Num(prompt.OverPercent, "0.#")` | `0.#` | ↑ |
| `App/Playback/ComparisonPanel.axaml.cs:539-540` | `_gesture.PanelScale * 100.0` … `percent.ToString("0", Strings.CultureOf(_language))` | `0` + `*100` | **hayır** |
| `App/Playback/PlayerView.axaml.cs:806` | `(_zoom.PanelScale * 100).ToString("0")` | `0` + `*100` | **HAYIR (aşırı yük yok)** |
| `App/Playback/PlayerView.Tracks.cs:297` | `(_subtitles.Scale * 100).ToString("0", CultureInfo.CurrentCulture)` | `0` + `*100` | **HAYIR (açıkça CurrentCulture)** |
| `Cli/CliApp.cs:455,462` | `(int)Math.Clamp(Math.Floor(value.Fraction*100),0,100)` | tamsayı, biçimsiz | dolaylı |
| `Core/PlanCalculator.cs:378` | `{SourceSizeCap * 100:0.#}%` | `0.#` + `*100` | **HAYIR** |
| `Core/PlanCalculator.cs:516,521` | `{halvingSaving:0.#}%` | `0.#` | **HAYIR** |
| `Core/PlanCalculator.cs:544` | `({best.Scale * 100:0.#}% of source)` | `0.#` + `*100` | **HAYIR** |
| `Core/PlanCalculator.cs:623` | `{crfStep * 100:0.#}%` … `{band.RelativeWidth * 100:0.#}%` | `0.#` + `*100` | **HAYIR** |
| `Core/PlanCalculator.cs:1120` | `+{TwoPassUncertainty * 100:0.#}%` | `0.#` + `*100` | **HAYIR** |
| `Player/MpvEngine.Advanced.cs:149` | `(int)Math.Round(value * 100, …)` | tamsayı | – |

**Ayrı biçim sayısı: 4** — `P1`, `0.#`+`*100`, `0`+`*100`, ham tamsayı `*100`. Tek `P1`
kullanımı (`:3578`) ile yanındaki `0.#` yüzdeler (`:3707`, `:4266`) **aynı ekranda iki
türlü** yazıyor. **Invariant verilmeyen: 14.**

---

## 3. Bit hızı

| Dosya:satır | Satır (kısa) | Biçim | Invariant? |
|---|---|---|---|
| `App/MainWindow.axaml.cs:3241` | `$"{info.AudioCodec} {Say("main.unit.k-value", info.AudioBitrateBps / 1000)}"` | tamsayı bölme, biçimsiz | dolaylı |
| `App/MainWindow.axaml.cs:3242` | `Say("main.unit.kbps-value", info.TotalBitrateBps / 1000)` | biçimsiz | dolaylı |
| `App/MainWindow.axaml.cs:3531` | `$"{Say("main.unit.k-value", plan.VideoBitrateK)} · …"` | biçimsiz | dolaylı |
| `App/MainWindow.axaml.cs:3535` | `$"{plan.AudioCodec} {Say("main.unit.k-value", plan.AudioBitrateK)}{channels}"` | biçimsiz | dolaylı |
| `App/MainWindow.axaml.cs:1898` | `Say("main.advanced.now", plan!.AudioBitrateK.ToString(Invariant))` | biçimsiz | evet |
| `App/Playback/PlayerView.Window.cs:494` | `(details.BitsPerSecond / 1000).ToString("0", CultureInfo.CurrentCulture)` | `0` | **HAYIR** |
| `App/Recorder/RecorderView.Otomatik.cs:134,135` | `budget.VideoKbps.ToString("N0", Strings.Culture)` | `N0` | hayır |
| `Cli/CliApp.cs:302` | `text.Format("plan.video", …, plan.VideoBitrateK)` | biçimsiz | dolaylı |
| `Core/PlanCalculator.cs:421,427,433` | `$"…{manualAudioK}kbps olarak sabitledi…"` | biçimsiz + bitişik `"kbps"` | **HAYIR** |
| `Core/PlanCalculator.cs:533,653,659,802` | `{videoK:0}k`, `{plan.VideoBitrateK}k`, `{deliverK}k` | `0` / biçimsiz, `"k"` soneki | **HAYIR** |
| `Core/PromptBuilder.cs:16` | `total bitrate: {info.TotalBitrateBps / 1000} kbps` | biçimsiz | **HAYIR** |
| `Core/PromptBuilder.cs:29` | `{localPlan.VideoBitrateK}k video / {localPlan.AudioBitrateK}k audio` | biçimsiz | **HAYIR** |
| `Core/Saturation.cs:31,84,85` | `{earlier.VideoBitrateK}k`, `{nextK}k`, `{budgetK}k` | biçimsiz | **HAYIR** |
| `Core/BudgetFill.cs:56` / `Core/CeilingGuard.cs:52` | `asks for {k}k` | biçimsiz | **HAYIR** |

**Ayrı biçim sayısı: 4** — `N0`, `0`, biçimsiz `ToString()`, kataloğa bırakılan ham `int`.
Birim eki de üç türlü: katalog `{0} kbps` / katalog `{0}k` / kod içinde bitişik `"k"` ve
`"kbps"`. `bps/1000` bölmesi tamsayı (`:3241`, `:3242`, `PromptBuilder.cs:16`) ile `double`
(`PlayerView.Window.cs:494`) arasında tutarsız. **Invariant verilmeyen: 15+.**

---

## 4. Sayı / ondalık (genel)

| Dosya:satır | Satır (kısa) | Biçim | Invariant? |
|---|---|---|---|
| `App/MainWindow.axaml.cs:1148,1149,1769,1773,3169,3850,3936,3957,3969` | `….ToString("0.##", Invariant)` | `0.##` | evet |
| `App/MainWindow.axaml.cs:1624` | `AdvancedMinFpsCandidates.Select(c => c.ToString("0.##", Invariant))` | `0.##` | evet |
| `App/MainWindow.axaml.cs:3452,3586` | `Say("main.unit.score-value", Num(score,"0.#"))` | `0.#` | `Strings.Culture` |
| `App/MainWindow.axaml.cs:3690,3694,3703,3705,3743` | `Num(note.Crf,"0")` / `Num(note.BudgetCrf,"0.#")` | `0` ve `0.#` (**aynı CRF**) | ↑ |
| `App/MainWindow.axaml.cs:3699` | `Num(note.Factor,"0.###")` | `0.###` | ↑ |
| `App/MainWindow.OnAyar.cs:146` | `mb.ToString("0.##", Invariant)` | `0.##` | evet |
| `App/Playback/PlayerView.Advanced.cs:271` | `(value ?? 0).ToString("0.##", Invariant)` | `0.##` | evet |
| `App/Playback/PlayerView.Serit.cs:268` | `_volume.ToString("0", Invariant)` | `0` | evet |
| `App/Playback/PlayerView.Serit.cs:269` | `_speed.ToString("0.00", Invariant) + "×"` | `0.00` + elle `"×"` | evet |
| `App/Playback/PlayerView.axaml.cs:809` | `Strings.Get("main.player.volume", _volume.ToString("0"))` | `0` | **HAYIR** |
| `App/Playback/PlayerView.axaml.cs:811` | `Strings.Get("main.player.speed", _speed.ToString("0.##"))` | `0.##` | **HAYIR** |
| `App/Playback/PlayerView.axaml.cs:813` | `_loopStart.ToString("0.##")` / `_loopEnd.ToString("0.##")` | `0.##` | **HAYIR** |
| `App/Playback/PlayerView.Tracks.cs:302` | `_subtitles.Position.ToString("0", CurrentCulture)` | `0` | **HAYIR** |
| `App/Playback/SubtitleOptions.cs:135` | `(value < 0 ? "−" : "+") + Math.Abs(value).ToString("0.##", CurrentCulture)` | `0.##` + elle işaret | **HAYIR** |
| `App/Playback/Keymap.cs:242` | `Math.Abs(amount).ToString("0.##", CurrentCulture)` | `0.##` | **HAYIR** |
| `App/Recorder/RecorderView.Serit.cs:264,265` | `progress.Frames.ToString("N0", Strings.Culture)` | `N0` | hayır |
| `App/Recorder/RecorderView.Otomatik.cs:150,275` | `sn.ToString("N0", Strings.Culture)` | `N0` | hayır |
| `App/Recorder/RecorderView.Kirpma.cs:41` | `result.RemovedSeconds.ToString("0.0", Strings.Culture)` | `0.0` | hayır |
| `App/Recorder/RecorderView.Gelismis.cs:44,56` | `_settings.AudioGainDb.ToString("0.##", Invariant)` | `0.##` | evet |
| `App/Recorder/RecorderView.Hedef.cs:65` | `_settings.Quality.ToString("0.##", Invariant)` | `0.##` | evet |
| `Cli/CliApp.cs:299` | `Num(quality.RequestedQuality,"0.#")` | `0.#` | evet |
| `Core/PlanCalculator.cs:363,516,563,592,664` | `{complexity.MeanLuma:0.#}`, `{MotionExponent:0.00}`, `{VmafNeg:0.##}`, `{budgetCrf:0.#}`, `{best.Score:0.#}/100` | dört ayrı | **HAYIR** |
| `Core/PlanCalculator.cs:532,664` | `{best.Bppf:0.0000}`, `{ReferenceBppf:0.0000}` | `0.0000` | **HAYIR** |
| `Core/Saturation.cs:45,84` | `{scale:0.###}`, `{DeadYield:0.0}` | `0.###`,`0.0` | **HAYIR** |
| `Core/PlanCalculator.cs:1119,1129,1130` | `{e:0.###}`, `{factor:0.###}` | `0.###` | **HAYIR** |

ffmpeg/mpv **argümanı** üreten `0.###`/`0.######` yazımları (`ClipExport.cs:101`,
`SegmentEncoder.cs:383`, `ToolsOptions.cs:132`, `MpvEngine.cs:504`, `OvershootTrimmer.cs:123`,
`FrameGrabber.cs:266`) kullanıcıya gösterilmediği için sayılmadı; `_trace.Add(...)` satırları
test izidir.

**Ayrı biçim sayısı: 9** — `0`, `0.#`, `0.##`, `0.0`, `0.00`, `0.###`, `0.0000`, `N0`, ham.
Aynı CRF değeri `MainWindow.axaml.cs:3690` (`"0"`) ile `:3692` (`"0.#"`) arasında iki türlü.
**Invariant verilmeyen: ~20.**

---

## 5. Tarih / saat damgası

| Dosya:satır | Satır (kısa) | Biçim | Invariant? |
|---|---|---|---|
| `App/MainWindow.axaml.cs:2109` | `expires.ToLocalTime().ToString("d MMMM HH:mm", Strings.Culture)` | `d MMMM HH:mm` | hayır (doğru tercih) |
| `App/Recorder/RecorderView.Paylas.cs:105` | aynı dizgi, `Strings.Culture` | ↑ | hayır |
| `App/ShrinkJobWindow.Paylas.cs:115` | aynı dizgi, `Strings.CultureOf(_language)` | ↑ | **kültür kaynağı diğer ikisinden farklı** |
| `App/Recorder/RecorderSettings.cs:402` | `"kayit_" + now.ToString("yyyy-MM-dd_HH-mm-ss", Invariant)` | `yyyy-MM-dd_HH-mm-ss` | evet |
| `App/Recorder/RecorderSettings.cs:416` | `"kare_" + now.ToString("yyyy-MM-dd_HH-mm-ss", Invariant)` | ↑ | evet |

**Ayrı biçim sayısı: 2.** Dizgi üç yerde elle kopyalanmış ama biçim aynı; tek gerçek
tutarsızlık `ShrinkJobWindow.Paylas.cs:115`'in `CultureOf(_language)` kullanması.
**Bu aile en derli toplu olanı.**

---

## 6. Çözünürlük / kare hızı

| Dosya:satır | Satır (kısa) | Biçim | Invariant? |
|---|---|---|---|
| `App/MainWindow.axaml.cs:1902` | `Say("main.advanced.now", $"{plan!.Width}x{plan.Height}")` | küçük `x` | **HAYIR** |
| `App/MainWindow.axaml.cs:3238` | `TxtResolution.Text = $"{info.Width}x{info.Height}"` | küçük `x` | **HAYIR** |
| `App/MainWindow.axaml.cs:3533` | `AddPlanFact(…, $"{plan.Width}x{plan.Height}")` | küçük `x` | **HAYIR** |
| `App/MainWindow.axaml.cs:3687` | `note.Width, note.Height, Num(note.Fps,"0.##")` | katalog | `Strings.Culture` |
| `App/Playback/PlayerView.Window.cs:487-488` | `Width.ToString(Invariant) + "×" + Height.ToString(Invariant)` | **çarpı `×`** | evet |
| `App/Playback/ToolsOptions.cs:126` | `Invariant($"mini={MiniWidth:0}x{MiniHeight:0}")` | küçük `x` | evet |
| `Core/PlanCalculator.cs:533,544,576,653,659,802` | `{best.Width}x{best.Height}@{best.Fps:0.##}` | `x` + `@` | **HAYIR** |
| `Core/Saturation.cs:45` | `from {plan.Width}x{plan.Height} to {width}x{height}` | `x` | **HAYIR** |
| `Core/PromptBuilder.cs:14,29` | `{info.Width}x{info.Height} @ {info.Fps:0.##} fps` | `x` + ` @ ` | **HAYIR** |
| `App/MainWindow.axaml.cs:3239` | `TxtFps.Text = Num(info.Fps,"0.##")` | `0.##`, **birim yok** | `Strings.Culture` |
| `App/MainWindow.axaml.cs:3534` | `Say("main.unit.fps-value", Num(plan.Fps,"0.##"))` | `0.##` + katalog `FPS` | ↑ |
| `App/MainWindow.axaml.cs:1903,3685,3737,3739` | `Num(note.Fps,"0.##")` | `0.##` | ↑ |
| `App/Playback/PlayerView.Window.cs:491` | `details.FramesPerSecond.ToString("0.###", CurrentCulture)` | **`0.###`** | **HAYIR** |
| `Cli/CliApp.cs:297,302` | `Num(info.Fps,"0.##")` / `Num(plan.Fps,"0.##")` | `0.##` | evet |
| `Core/EncodePlan.cs:183` | `$"fps: {Fps:0.##} → {other.Fps:0.##}"` | `0.##` | **HAYIR** |
| `Core/PlanCalculator.cs:538,550,695,702,931` | `{best.Fps:0.##}`, `{info.Fps:0.##} fps` | `0.##` | **HAYIR** (`:931` tek başına Invariant) |

**Ayrı biçim sayısı: çözünürlükte 3** (`WxH`, `W×H`, `WxH@fps`); **fps'te 3** (`0.##`
birimsiz, `0.##` + katalog `FPS`, `0.###`). Aynı uygulamada çözünürlük iki ayrı ayraçla
(`:3238` `x`, `PlayerView.Window.cs:488` `×`) ve fps iki ayrı ondalıkla yazılıyor.
**Invariant verilmeyen: 15+.**

---

## Süre — `Saat` dışında kalanlar

`Saat.` yalnız sekiz dosyada kullanılıyor. Kapsam dışında kalanlar:

- `App/Recorder/RecorderView.Kirpma.cs:41` — `RemovedSeconds.ToString("0.0", Strings.Culture)`.
- `App/Recorder/RecorderView.Otomatik.cs:150` — `sn.ToString("N0", Strings.Culture)`.
- `Core/PromptBuilder.cs:13` — `$"- duration: {info.DurationSeconds:0.##} s"`, Invariant yok.
- `Core/CeilingGuard.cs:53` — `$"… a {VbvWindowSeconds:0} s buffer"`, Invariant yok.
- `Player/MpvEngine.cs:235` — `$"libmpv did not load the file within {…TotalSeconds:0} s."`
  (kullanıcıya atılan istisna metni), Invariant yok.
- `App/Recorder/RecorderView.Tepsi.cs:40` — süreyi `ElapsedText` olarak şeritten dizgi
  kopyalıyor (`RecorderView.Serit.cs:34`); `Saat` dolaylı ama ikinci bir yoldan.

---

## Hüküm

**En dağınık aile dosya boyutu:** dokuz ayrı ondalık biçim, üç ayrı birim tablosu (biri
`1024`'e bölüp `KB/MB/GB` yazarak **birimi yanlış adlandırıyor**), iki kopyalanmış
`" MB"/" GB"` birleştirmesi ve otuzdan fazla kültürsüz satırla, aynı megabayt değeri
arayüzün farklı köşelerinde beş türlü yazılabiliyor.

Toplam: **altı aile, 32 ayrı biçim, ~110 çağrı yeri, 95+ kültürsüz satır.**
