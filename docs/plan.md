# Oynatıcı Panelleri Ve Sağ Menü

Kaynak: kullanıcı istekleri (2026-09-27), danışma `docs/danisma/015-fable-oynatici-menu-panel.md`.

## Dalga A — Sağ Menü (ajan, kendi dalı)
- `Keymap.cs`, `PlayerView.axaml.cs BuildMenu`, `PlayerView.Tracks/Window/Tools.cs`
- 12 üst düzey satır: Oynat/Duraklat, Tam ekran, Dosya konumunu aç, —, Ses, Altyazı, Görüntü ▸, Oynatma ▸, Tekrar ve yer imleri ▸, —, Ekran görüntüsü, Araçlar ▸, Son dosyalar ▸, Ayarlar ▸
- "Dosya konumunu aç": `explorer /select,<yol>`; Windows dışı klasörü açar.

## Dalga B — Üst Panel (ajan, kendi dalı)
- `MainWindow.axaml(.cs)`, `Themes/Controls.axaml`, `Themes/Icons.axaml`
- Oynatıcı sekmesinde tek üst katman, tek görünürlük bağı; opak gradyan ve mavi alt çizgi yerine alt scrim'in aynası.
- Başlık düğmeleri anahatsız: hover'da renk-2 yazı, ortadan açılan alt çizgi; seçili sekme gövde renginde sabit çizgi.
- Teknesyum düğmesinden `IconCode` kalkar.
- Tetik bölgesi sabit piksel belirteci (üst 48), scrim 96.

## Dalga C — Alt Şerit ve Oynatma (T0)
- `Themes/Playback.axaml`, `PlayerView.axaml`, `PlayerView.Serit.cs`
- Kart/kenarlık/dış boşluk kalkar, kontroller scrim üstüne; arama çubuğu ince, düğme satırı 36.
- Fare çıkınca 0 ms kapanış; ThumbChip şeritle kapanır. Tetik 96, scrim 144.
- Duraklatma simgesi girişi `MotionInstant` (40 ms, 2x), çıkış `MotionFast`.
- Sondayken Oynat: sonraki dosya varsa ona, yoksa baştan.

Ölçü: dokunulan alanın testleri yerelde yeşil, sonra main CI yeşil, 0.13.0.
