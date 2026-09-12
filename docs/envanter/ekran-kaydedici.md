# Ekran kaydedici envanteri (12 Eylul 2026)

8. dalga plani bu rapora dayaniyor. Ajanin dondurdugu metin oldugu gibi asagida; satir
numaralari raporun alindigi andaki `main` (`e48dbbaa`) agacina aittir.

---

## 1. ffmpeg surec katmani (`src/VidShrink.Ffmpeg`)

- `FfmpegRunner.cs:82` — `public static Task<FfmpegRun> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct)` (:87). Arguman **uretmez**, yalniz kosturur; sonuc `FfmpegRun(bool Ok, int ExitCode, string StandardError, TimeSpan Elapsed, IReadOnlyList<string>? DroppedOptions)` (:62). Karar surecten ayri: `internal static FfmpegRun Decide(int, string, TimeSpan)` (:136).
- `EncodeRunner.cs:82` — `Task<EncodeResult> RunAsync(...)` (:87), `Task<ConversionResult> ConvertAsync(...)` (:255), arguman uretimi `static IReadOnlyList<string> EncodeArguments(MediaInfo, EncodePlan, string outputPath, int pass, string? passLogPrefix, IEncoderAvailability, SceneMap?)` (:341) -> asil uretici `VidShrink.Core.FfmpegArguments.Build`.
- Ilerleme okuma tek yerde: `EncodeRunner.cs:358` `RunCommandAsync(...)` — argumanlarin basina `-progress pipe:1 -nostats` ekler (:366), stdout'tan `out_time_ms` / `total_size` ayristirip `EncodeProgress(Fraction, Elapsed, Remaining, OutputMb, Stage)` (:8) raporlar; stderr ayri gorevde `StderrWatch` (:426) ile yutulur.
- Diger surec baslaticilar hep ayni "baslat-bitir-oku" kalibinda: `FfprobeClient.cs:23`, `FrameGrabber.cs:273`, `CalibrationProbe.cs:261`, `ComplexityProbe.cs:691/785/852/972`, `EncoderCapabilities.cs:255/298/332/427`, `QualityMeter.cs:163`.
- **Uzun sureli kayit icin hazir yol yok.** Her cagri sureci baslatir, bitmesini bekler, iptal = `TryKill` (sureci oldurur — `FfmpegRunner.cs` remarks, :78-81). Argumanlarin nerdeyse tumu `-nostdin` (`ClipExport.cs:58,67`, `SegmentEncoder.cs:175`, `FrameGrabber.cs:259`, `ComplexityProbe.cs:878` ...), yani ffmpeg'e `q` yazarak nazik durdurma deseni **hic kurulmamis**. Tek istisna: `FfmpegRunner.cs:97` `RedirectStandardInput = true` — acik ama kullanilmiyor; kayit icin "baslat -> stdin'e q -> temiz mux" yolunun tutunacagi tek yer burasi.
- Suresi bilinmeyen bir iste ilerleme okuma da calismaz: `RunCommandAsync` kesri `durationSeconds`'a boler (:391) — canli kayitta sure yok, yeni bir ilerleme kipi gerekir.

## 2. Ekran/pencere yakalama izi

**Kaynakta sifir.** `gdigrab`, `x11grab`, `avfoundation`, `dshow`, `pipewire`, `ScreenCapture` icin `src/` altinda hic eslesme yok. Yalniz sunlar var:

- `src/VidShrink.App/Locales/en/performance.json:25` — acikca sinir ciziyor: "VidShrink does not capture video... not what a capture tool does with it".
- `docs/danisma/001-depo-aciklamasi-fable.md:38` ve `docs/netlestirme/001-...md:42` — "Ekran kaydedici yok, gdigrab/ScreenCapture sifir eslesme" notu.
- `tests/VidShrink.Tests/QualityTargetTests.cs:51` `LongScreenCapture()` — sadece bir test *girdi profili* (ekran kaydi benzeri video), yakalama kodu degil.
- `player.view.screenshot*` anahtarlari (`Locales/*/playback.json:26-29`) tek kare PNG kaydi icindir, akis kaydi degil.

## 3. Ses girisi

Mikrofon/sistem sesi yakalama **yok**. `FfmpegArguments.cs:387` tek girdi kurar (`-i info.FilePath`), coklu girdi/cihaz yok.

- NAudio yalniz depoya dahil olmayan olcum aracinda: `tools/VidShrink.PlayerProbe/VidShrink.PlayerProbe.csproj:21`, `Program.cs:4,388` (`WasapiOut`); `tools/VidShrink.PlayerProbe/AGENTS.md:15` `.sln`'e eklenmesini yasakliyor.
- Emeklilik pimi: `tests/VidShrink.Tests/OynaticiKarsilastirmaTests.cs:448,493` — `DecoderPipe|AudioSink|NAudio` icin canli basvuru ararsa test kirmizi verir (`Assert.Empty(naudio)` :543). Yeni bir ses yakalama yolu NAudio uzerinden kurulursa bu pim duser.
- Bugun ses cikisi libmpv uzerinden: `src/VidShrink.App/Playback/PreviewAudio.cs:9,25`, motor `src/VidShrink.Player/MpvEngine.cs`.

## 4. Arayuz iskeleti — `src/VidShrink.App/MainWindow.axaml`

`TabControl x:Name="Tabs"` (:107), `SelectedIndex="1"`. Sira:

| # | Satir | x:Name | Baslik anahtari |
|---|---|---|---|
| 0 | :185 | `TabPlayer` | `main.tab.player` (ozel `TabItem.Header` StackPanel'i, :186-191; icerik `playback:PlayerView x:Name="Player"`) |
| 1 | :196 | — (icerigi `PageShrink`) | `main.tab.shrink` |
| 2 | :803 | — | `main.tab.convert` |
| 3 | :1050 | `TabSettings` (`IsVisible="False"`) | `main.tab.settings` |
| 4 | :1190 | — | `main.tab.about` |
| 5 | :1215 | — | `main.tab.advanced` |

`TabControl.Tag` (:108-184) sekme degil; guncelleme seritleri orada duruyor.

Yeni sekme icin dokunulacaklar: (a) `MainWindow.axaml` TabItem tanimi; (b) `Locales/*/main.json` `main.tab.<ad>` anahtari (en: `main.json:21-25,417`) — **her dil** icin; (c) `MainWindow.axaml.cs` baglama: `SettingsTabIndex`/`PlayerTabIndex` benzeri indeks alani ve `Tabs.SelectedIndex` kullanicilari (:148-150, :722-731); (d) test pimleri: `WindowLayoutTests.cs:184` (`TabPlayer` ilk olmali) ve :202 (acilis hala Shrink), `SettingsTabTests.cs:29-53` (XAML'den sekme govdesi kesen `Tab(key)` yardimcisi), `VisibleTextTests.cs:79`, `OynaticiGirdiTests.cs:319-361` (baslik metni + Theme tutarliligi), `AyarYuzeyiTests.cs:42`. **Uyari:** sekmeyi 0. siraya koymak `WindowLayoutTests.cs:191`'i kirar.

## 5. Dil anahtari duzeni

`src/VidShrink.App/Locales/en/`: `main.json`, `playback.json`, `performance.json`, `settings.json`, `advanced.json`, `tools.json`, `tracks.json`. Desen `<alan>.<grup>.<ad>`, nokta ayrilmis kucuk harf + tire: orn. `"playback.control.restart"`, `"playback.error.exit-code"`, `"playback.badge.original"` (`Locales/en/playback.json:15,6,2`). Bicim pimi: `LocalizationTests.cs:119` (`KeyShape` regex, nokta.ayrilmis.kucuk-harf).

Bir anahtar/alan eklenince bakan olcumler:

- `LocalizationTests.cs:106-120` cikitya kopyalanma + anahtar bicimi; `:132-159` ayni dilde iki dosyada cakisma yasagi; `:165-175` her dil x her alan dosyasi var mi; `:305-330` `KnownDead` olu ceviri **sayim kaydi** (kullanilmayan anahtar eklenirse kirmizi); `:336-352` `Strings` kapi sayisi (`SeedOverloads`).
- `LanguageTests.cs:30` `Domains = { "main", "playback", "performance", "settings" }` ve `LocalizationTests.cs:171` ayni dort ad — **yeni bir `recorder.json` acarsan bu iki listeye eklenmeli**, yoksa `Locales.Read` yeni alani hic gormez (ve `advanced/tools/tracks` bugun bu listelerin disinda kalmis).
- `LanguageTests.cs:526-539` her `en` anahtarinin Turkcesi yazilmis mi; `:516` `SameInEveryLanguage` muafiyeti.
- Kopyalama kurali hazir: `VidShrink.App.csproj:28-29` `Locales\**\*.json`.

## 6. `src/VidShrink.App/Playback/` dosya duzeni

Ornek alinacak cekirdek desen (yeni modul icin birebir izlenebilir): gorunum `PlayerView.axaml` + `PlayerView.axaml.cs` ve konuya gore bolunmus kismi siniflar — `PlayerView.Window.cs`, `PlayerView.Tools.cs`, `PlayerView.Tracks.cs`, `PlayerView.Advanced.cs`, `PlayerView.Serit.cs`, `PlayerView.Fare.cs`; girdi `Keymap.cs` + `PlayerInputMap.cs`; kalici ayar `PlayerSettings.cs`; belirtecler `Themes/Playback.axaml`; dil `Locales/*/playback.json`.

Kisaca isleri: `ComparisonPanel.axaml(.cs)` iki yarili karsilastirma paneli - `ComparisonSurface.cs` yarilari cizen yuzey - `EngineComparisonFrameSource.cs` iki mpv orneginden kare besleme - `PanelHost.cs` panelin pencere/yerlesim konagi - `ControlStrip.axaml(.cs)` alt denetim seridi - `HoverZone.cs` seridi gosterip gizleyen kenar bolgesi - `ZoomGesture.cs` boy kademesi jesti - `SeekMarks.cs` zaman cizgisi isaretleri - `PlaybackHistory.cs`/`RecentFiles.cs` konum ve son dosya kaydi - `FolderNavigator.cs` klasorde ileri/geri - `SegmentEncoder.cs` onizleme cifti uretimi (`PreviewClip`, :19) - `ClipExport.cs` parca disa aktarimi - `PreviewAudio.cs` mpv ile ses - `SubtitleOptions.cs`/`ToolsOptions.cs`/`PlayerAdvanced.cs` + `PlayerAdvancedPanel.*`/`PlayerShortcutsPanel.*`/`TrackButtons.*` panel ve secenek yuzeyleri.

Kayit modulu icin en yakin ornekler: **`SegmentEncoder.cs`** (ffmpeg'i UI'dan surme + hata anahtari), **`PlayerSettings.cs`** (ayar kaliciligi), **`Keymap.cs`** (`PlayerInput`/`PlayerAction`/`KeymapRow`, :17-48, `Rows` :94).

## 7. Belirtecler

`Themes/Theme.axaml` olcu deseni: `<AdKoku><Kademe>` — `SpaceXs/Sm/Md/Lg/Xl` = 4/8/12/16/24 (:206-210), `RadiusChip/Cell/Control/Panel` = 6/8/12/16 (:238-241), `BorderThin` (:233) + `BorderThinScalar` (:234), `PanelPadding`/`ButtonPadding`/`ButtonPaddingSm` (:212-215), `NoticeMargin`/`SectionMargin` (:226,231), `TargetMinSize` 24 (:251), `PanelMinHeight` 256 (:264), `FontSizeSm/Md/Lg/Hero` (:195-198).

Modul basina ayri dosya deseni: `Themes/Playback.axaml` (54 belirtec) yalniz `Playback*` onekli adlar tanimlar (`PlaybackSeparatorWidth` :110 ... `PlaybackThumbnailWidth` :132) ve **yeni sayi uretmez** — her biri Theme.axaml'daki bir belirtecin degerini alir, turetme dosya basindaki yorumda tek tek yazili (`Playback.axaml:5-20`, orn. `PlaybackStageMinHeight <- PanelMinHeight x 2`). Kayit yuku: `App.axaml:13` `<ResourceInclude .../Themes/Playback.axaml>`, **Theme.axaml'dan sonra** (yorum :11-12). Olcumler bu zinciri App.axaml'dan yuruyerek okur (`tests/VidShrink.Tests/ThemeSources.cs:24-42`), dolayisiyla `Themes/Recorder.axaml` gibi bir dosya App.axaml'a eklendigi anda testlerce kendiliginden gorulur; sayilarin turetilmisligini insan gozu + `OynaticiGorunumTests.cs:226` tarzi nokta olcumler denetliyor.
