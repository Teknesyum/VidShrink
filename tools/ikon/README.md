# tools/ikon

`uret.py` VidShrink ikonunu (`src/VidShrink.App/Assets/VidShrink.ico`, 9 boy) ve
uygulama logosunu (`src/VidShrink.App/Assets/VidShrink.png`, 1024) deterministik
uretir. Kaynak geometri `src/VidShrink.App/Assets/VidShrink.svg` ile ayni Bezier
noktalarini elle tasir; makinede cairosvg/resvg/inkscape yoktu, bu yuzden betik
SVG'yi rasterlemek yerine ayni koordinatlari dogrudan Pillow + numpy ile
supersample cizer (arms icin egri uzerine dizilmis disklerin birlesimi, ucgen icin
kose yuvarlatilmis poligon + gradyan maskesi).

## Calistirma

```
python tools/ikon/uret.py
```

Bagimlilik: `Pillow`, `numpy` (bu makinede onceden kuruluydu; yoksa
`pip install pillow numpy` proje disina dokunmaz).

Kucuk boylar (<=32px) buyuk cizimden kucultulmez: her boy kendi kol kalinligi
carpaniyla (`stroke_boost`) ayrica cizilir, okunurluk boylece korunur.
