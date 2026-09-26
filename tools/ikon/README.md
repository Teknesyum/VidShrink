# tools/ikon

**Tek kaynak `src/VidShrink.App/Assets/VidShrink.svg`.** Elle yazilmis kubik Bezier
`path`'lerle kurulu: parantez kollari `stroke-linecap="round" stroke-linejoin="round"`
kalin bir stroke (kontur icin siyah + uzerinde gradyan renkli iki kopya), govdenin
ic kavisinde ayrica ince yari-saydam beyaz bir "parlama" stroke'u; ok govdesi+basi
ayni `<g>` icinde kolla ayni gradyan dolguyu tasiyan, acikca ayrisan bir sekil
(`paint-order="stroke"` ile siyah kontur dolgunun altina/etrafina ciziliyor); oynat
ucgeni kose yuvarlatilmis path + ikinci gradyan + camlanma ucgeni.

Kucuk boylar icin `VidShrink-kucuk.svg`: ayni yol/gradyan verisi, yalnizca stroke
kalinliklari buyutulmus (16/20/24px'te gercek SVG motorunun anti-alias'i konturu
inceltiyor, kalin kontur bunu telafi ediyor). `VidShrink.svg` elle degistiginde bu
dosya da elle esitlenir (script otomatik turetmiyor - iki dosya kucuk ve kasitli
farkli).

**Rasterleme `uret.py`, gercek bir SVG motoruyla: headless Microsoft Edge**
(`msedge --headless=new --screenshot=... --window-size=N,N
--default-background-color=00000000`, SVG'yi saran seffaf-zeminli bir HTML
uzerinden). Betik SVG'yi DEGISTIRMEZ, yalnizca okur ve her ICO boyunda
(16, 20, 24, 32, 40, 48, 64, 128, 256) + logo boyunda (1024) render alip
`VidShrink.ico`/`VidShrink.png`'yi yazar. `KUCUK_SINIR` sabiti (`uret.py` icinde,
su an 24) bu boy ve altinda `VidShrink-kucuk.svg`'yi kullanir.

## Calistirma

```
python tools/ikon/uret.py
```

Bagimlilik: Microsoft Edge (herhangi bir surum, headless calisir - makinede
`C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe` bulundu), Pillow
(yalniz ICO paketlemek icin, rasterlemede degil).

## Gecmis

Onceki iki surum (poligon-birlesimi + Pillow/numpy supersample) denetci
tarafindan reddedildi: koseler poligondan geldigi icin keskindi, ok govdeyle
kaynasip ayirt edilemiyordu, dolgu duzdu. Bu surum SVG'yi hand-authored kubik
Bezier + gercek tarayici motoruyla cozdu.

Ucuncu turda geometri T0'in kendi elle cizdigi SVG ile degisti; uretim duzeni
(headless Edge + `uret.py`) aynen kaldi.
