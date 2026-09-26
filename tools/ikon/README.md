# tools/ikon

**Tek kaynak bu betiktir.** `uret.py` VidShrink ikonunu (`src/VidShrink.App/Assets/VidShrink.ico`,
9 boy), uygulama logosunu (`src/VidShrink.App/Assets/VidShrink.png`, 1024) VE
`src/VidShrink.App/Assets/VidShrink.svg`'yi AYNI geometri sabitlerinden (`ARM_INNER_TOP`,
`ARM_OUTER_TOP`, `TRIANGLE_OUTER/INNER`) uretir - `emit_svg()` bu sabitleri okuyup SVG
yazar, raster taraf onlari Pillow+numpy ile supersample cizer. `VidShrink.svg` elle
duzenlenmez; degisiklik `uret.py`'de yapilir ve betik yeniden calistirilir. Makinede
cairosvg/resvg/inkscape yoktu, bu yuzden raster taraf SVG'yi geri okumaz (ayni sayilari
bagimsiz cizer) - ama kaynak sayilar (geometri + renk) tek yerde, `uret.py`'de.

Kol siluetti eski ikonun piksel olcumunden turetildi (ic/dis kenar profili, bkz.
`ARM_INNER_TOP`/`ARM_OUTER_TOP` docstring'i): tek kapali poligon - kanca + govde + ok
hep ayni parca, sonradan birlestirilen ayri sekiller degil. Ucgen icin kose yuvarlatilmis
poligon + gradyan maskesi.

## Calistirma

```
python tools/ikon/uret.py
```

Bagimlilik: `Pillow`, `numpy` (bu makinede onceden kuruluydu; yoksa
`pip install pillow numpy` proje disina dokunmaz).

Kucuk boylar (<=32px) buyuk cizimden kucultulmez: her boy kendi kol kalinligi
carpaniyla (`stroke_boost`) ayrica cizilir, okunurluk boylece korunur.
