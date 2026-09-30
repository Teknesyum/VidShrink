# Düzenleyici — Klip Özellikleri (Kırpma, Döndürme, Ses, Solma)

Seçili klibe dört özellik: kırpma (oranlı, ortalı), döndürme 90/180/270 + yatay/dikey çevirme,
ses (dB, sessiz), giriş/çıkış solması (görüntü + ses). Her değişiklik tek geri alma adımı.

| Parça | Dosyalar | İş |
| --- | --- | --- |
| Model | `Core/Editing/ClipEffects.cs`, `EditClip.cs`, `EditTimeline.cs` | `ClipEffects` (değer eşitlikli), `EditClip.Effects`; bölme/kırpma/hız etkiyi taşır, solma bölmede kenarında kalır; `SetEffects` birden çok klibe tek adım |
| Süzgeç | `Core/Editing/ClipFilters.cs`, `EditExport.cs` | kaynak boyutu (SAR düzeltilmiş) → çift pikselli `crop`, `transpose`/`hflip`/`vflip`, ortak tuval `scale+pad`, `fade`/`afade`/`volume`; etkili çizelgede Hızlı/Akıllı Tam'a düşer (`EffectsForcedFull`), etkisiz çizelgenin grafiği bayt bayt aynı |
| Önizleme | `Player/IPlaybackEngine.cs`, `MpvEngine.Advanced.cs`, `App/Playback/EdlPreviewDriver.cs` | geometri parça başına `@vsclipgeo` (parça değişince), solma ve ses EDL zamanında `enable=` ile sabit `@vsclipfade`/`@vsclipaudio`; yalnız etki değişince EDL yeniden açılmaz |
| Arayüz | `EditorView.axaml`, `EditorView.Klip.cs`, `EditorView.Teslim.cs`, `Locales/*/editor.json` | klip paneli (metin seçili değilken), Tam'a düşme notu, 42 dil |
| Test | `DuzenleyiciKlipOzellikTests.cs`, `AGENTS.md`, `docs/olcumler/duzenleyici-klip-ozellik.md` | model/undo, argüman + negatif kontroller, canlı ffmpeg ≤3 sn 320x240, libmpv kolu, sahte motorla arayüz, ≥3 mutasyon |
# Düzenleyici — Premiere Pro Uyumu (D6)

> "düzenleyici bölümünün işleri öncelikli çok saçma ordaki herşey adobe premiere pro gibi olmalıyız kullanışlı"

Kaynak tek video kalır. Hedef Premiere'in el alışkanlıkları: araç paleti, Program monitörü,
ripple düzenleme, Q/W, yakalama, zaman kodu, ses dalga biçimi. Boşluk tablosu 20 madde:
2 VAR, 9 KISMEN, 9 YOK (kanıtlar koddan, satır numaralı).

## Dalga 1 — Davranış ve Kısayollar (yerleşim değişmez)

| Parça | Dosyalar | İş |
| --- | --- | --- |
| 1A Core | `Core/Editing/EditTimeline.cs` | `RippleTrimHead/Tail` (Q/W), `TrimEdge`, `DeleteMany` (tek geri alma), `EditPoints` |
| 1B Çizelge | `EditorTimeline.cs`, `EditorPlayhead.cs` | `SnapEnabled`, `ZoomToFit`, `Next/PrevEditPoint`, `FollowPlayhead`, Ctrl/Shift-tık çoklu seçim |
| 1C Tuşlar | `EditorKeymap.cs`, `EditorView.Kisayol.cs`, `EditorView.axaml.cs`, `Locales/*/editor.json` | S yakalama, Ctrl+K böl, Shift+Delete/Delete ripple, `'` extract, Q/W, Up/Down, Shift+Sol/Sağ 5 kare, `=` `-` `\`, Shift+I/O, Ctrl+R |

## Dalga 2 — Program Monitörü, Araç Paleti, Kenar Kırpma

| Parça | Dosyalar | İş |
| --- | --- | --- |
| 2A Yerleşim | `EditorView.axaml(.cs)`, `EditorView.Teslim.cs`, `Playback/PlayerView.axaml(.cs)` | Monitör altında zaman kodu kutusu + ikonlu taşıma butonları, solda V/C/B paleti, dışa aktarma sağ üstte (Ctrl+M) |
| 2B Etkileşim | `EditorTimeline.cs`, `EditorClip.cs`, `EditorPlayhead.cs` | Araç kipi, jilet tıklaması, kenardan sürükleyerek kırpma, V1/A1 başlıkları, karelere inen cetvel |
| 2C Zaman kodu | yeni `Core/Editing/EditTimecode.cs`, `Themes/Editor.axaml`, `Locales/*/editor.json` | `Format`/`TryParse` (25, 29,97, 60 fps), yeni takma ad belirteçleri |

## Dalga 3 — Ses Dalga Biçimi

Yeni `Core/Editing/AudioPeaks.cs` (ffmpeg s16le → min/max tepe, iptal, önbellek) ve
`EditorTimeline.DrawWaveform` (görünür klipler, hız ve ters aynası). Tepeler gelmeden düz ayna.

## Kabul

Her parçanın testleri yerelde yeşil, Release `-warnaserror` derlemesi temiz, birleşmeden sonra main CI yeşil.
Bekçi testler: `DuzenleyiciCizelgeTests` sabit renk/ölçü, belirteç yorumu, yerelleştirme anahtarı.
