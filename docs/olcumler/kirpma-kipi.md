# Kırpma Kipi: Otomatik Ve Temkinli — 7 Ekim 2026

`--kirpma-kipi` (HandBrake `--crop-mode`) için iki soru ölçüldü: temkinli kip `cropdetect`
eşiğini mi düşürmeli, yoksa örneklerin birleştirilme kuralını mı değiştirmeli. ffmpeg 9.0
(gyan full), tek süreç, `-threads 2`, libx264 ultrafast `-crf 18`, kaynak `testsrc2` 640x480 25 kare/sn.

Yoklama komutu `CropProbe` ile aynı: `-ss <an> -frames:v 2 -vf cropdetect=limit=<eşik>:round=2:skip=0:reset=0`.

## Ölçüm 1: Eşik

Üstte ve altta 60 piksellik düz gri bant (`drawbox ... color=0x<gri>:t=fill`), 2 sn klip, an 1 sn.
Bandın parlaklığı `signalstats` `YAVG` ile okundu.

| Bant rengi | Bant parlaklığı (YAVG) | `limit=24` | `limit=16` |
|---|---|---|---|
| `0x000000` | 16 | 640:360:0:60 | 640:360:0:60 |
| `0x040404` | 19 | 640:360:0:60 | 640:480:0:0 (kırpma yok) |
| `0x080808` | 23 | 640:360:0:60 | 640:480:0:0 (kırpma yok) |
| `0x0c0c0c` | 26 | 640:480:0:0 (kırpma yok) | 640:480:0:0 (kırpma yok) |

Eşiği 16'ya indirmek "daha az kırpmak" değil: tam siyah (16) dışındaki her bandı, yani gürültülü
ya da hafif yükseltilmiş gerçek siyah bantları da görüntü sayıyor. Bant ya tümüyle kırpılıyor ya
hiç; ara değer yok. Bu yüzden temkinli kip eşiğe dokunmuyor, iki kip de `limit=24` ile yokluyor.

## Ölçüm 2: Birleştirme Kuralı

6 sn klip: ilk 1,8 sn 60 piksellik, kalanı 100 piksellik siyah bant. `CropProbe` on örnek alıyor.

| Örnek anı (sn) | `cropdetect` |
|---|---|
| 0,3 · 0,9 · 1,5 | 640:360:0:60 |
| 2,1 · 2,7 · 3,3 · 3,9 · 4,5 · 5,1 · 5,7 | 640:280:0:100 |

| Kip | Kural | Sonuç | İlk 1,8 sn'de kesilen görüntü |
|---|---|---|---|
| `auto` | en çok görülen dikdörtgen (`CropProbe.Decide`) | 640:280:0:100 | üstten ve alttan 40'ar piksel |
| `conservative` | örneklerin birleşim dikdörtgeni (`CropProbe.DecideConservative`) | 640:360:0:60 | yok |

Tek bant boyu taşıyan kaynakta iki kip aynı dikdörtgeni veriyor. Örneklerden biri tam kare
görürse temkinli kip hiç kırpmıyor ve bunu söylüyor (`result.crop-conservative-none`).

## Karar

- `auto`: bugünkü `--kirp` yolu, değişmedi.
- `conservative`: aynı yoklama, aynı eşik; karar örneklerin birleşimi. Hiçbir örneğin görüntü
  saydığı piksel kesilmez. Tek sayılı kenar kaynak izin veriyorsa dışarı çifte yuvarlanır.
- `none`: yoklama koşmaz. `custom`: yalnız `--suzgec crop=` değerini kullanır.

## Ölçülmeyen

HandBrake'in kendisi koşturulmadı ve temkinli kipinin kuralı kaynağından doğrulanmadı (ağ çağrısı
ve indirme yok). Bilinen yalnız belgedeki tanımı: otomatikten daha az kırpan kip. Birleşim bu
tanımın en az kırpan ucudur; karşılık **HandBrake ile birebir değil**, aynı kaynakta HandBrake
farklı bir dikdörtgen verebilir. Gerçek film kaynağında (gürültülü bant, bant içi altyazı) ölçüm yapılmadı.

Canlı kol: `KirpmaKipiTests.CanliKlipteTemkinliKipGenisDikdortgeniBuluyor`.
