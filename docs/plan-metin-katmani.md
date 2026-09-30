# Plan: Metin Katmanı, Dalga 1

Yöntem `docs/rapor-metin-katmani.md`: tek `.ass` dosyası hem önizlemeyi (libmpv `sub-add`/`sub-reload`)
hem dışa aktarmayı (ffmpeg `ass=`) besler. drawtext yok.

## Core (`src/VidShrink.Core/Editing/`)

- `TextLayer.cs`: `TextLayer` ve `TextKeyframe`. Zaman çizelge tick'i (çıktı zamanı), konum 0..1 kesir,
  boyut kare yüksekliğinin yüzdesi, renk `0xRRGGBB`. Anahtar kareler katman başına göre ofset.
- `EditCommands.cs`: `TextCommand` — metin listesini kurucuda yakalar; kliplerle aynı undo yığınına girer.
- `EditTimeline.cs`: `Texts`, `AddText`, `MoveText`, `TrimText`, `DeleteText`, `UpdateText`.
- `AssWriter.cs`: stil/olay satırları, `\pos`/`\move`, `\fad`/`\fade`, kaçış, santisaniye yuvarlama, BOM.
  Önizleme için EDL eşlemesi (hız/geri parçalarda satır parça sınırında bölünür).
- `TextFonts.cs`: `sub-fonts-dir` ve `fontsdir` için tek klasör.
- `EditExport.cs`: metin varsa Tam; `[vcat]ass=filename=...:fontsdir=...[vout]`; plan `Subtitle*` alanları.

## Ffmpeg / Player

- `EditExportRunner`: `.ass`'i iş klasörüne BOM'lu yazar.
- `IPlaybackEngine` + `MpvEngine`: `AddOverlay`, `ReloadOverlay`, `RemoveOverlay`, `SetSubtitleFontsDir`.

## App (`src/VidShrink.App/Editing/`)

- `EditorTimeline`: cetvel ile V1 arasında T1 izi; metin klibi çizimi, taşıma, kenar kırpma.
- `EditorView`: "Metin ekle" düğmesi, `EditorCommand.AddText` (T), özellik paneli (metin, boyut, renk),
  önizleme bağlantısı (metin yoksa yükleme yok), dışa aktarma notu.
- `Themes/Editor.axaml`, `Themes/Theme.axaml`: yeni belirteçler.
- `Locales/*/main.json`: yeni anahtarlar 42 dilde; `BiciminTests`/`BaslikKapsamiTests` pinleri.

## Testler

`DuzenleyiciMetinTests` (model, ASS, dışa aktarma planı, ffmpeg canlı kol, libmpv canlı kol),
yerleşim kolları, mutasyonlar (kaçış, `\move`, Tam'a düşme, `sub-reload`).
