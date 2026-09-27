# palet-kontrast

Proje paletlerinin tohumunu (`src/VidShrink.App/Themes/Palette/seeds.json`) canlı kontrast ölçüsüne göre eşiğe çeker.
Girdi, `KontrastTests`'in `UC_KLASOR=<klasör>` ve `UC_ASAMA=<aşama>` verilince yazdığı TSV'dir.

- `analiz.mjs <klasör> <aşama>`: eşik altı (palet, anahtar) çiftlerini ve zeminlerini yazar; son satır `toplam N`.
- `ayar.mjs <klasör> <aşama> [--yaz]`: açığı olan tohumun yalnız HSL açıklığını kaydırır, hedef eşik + 0,15 (metin 7, simge 3).
  `--yaz` verilmezse yalnız önerir.

Sıra: KontrastTests TSV yazar, `ayar.mjs --yaz`, `dotnet run --project tools/VidShrink.PaletteGen`, çıktıyı CRLF'e çevir
(PaletteGen LF yazar, BOM yok), KontrastTests yeniden. Tohum değişince türeyen zeminler yeni açık verebilir; uç 3'te iki tur
sürdü (`docs/ui-denetim/2026-09-27-uc2.md`). `kaynak` alanı `vidshrink` olmayan standart paletleri atlar; Synthwave
proje paletidir, uç 3'te açığı olmadığı için değişmedi.
