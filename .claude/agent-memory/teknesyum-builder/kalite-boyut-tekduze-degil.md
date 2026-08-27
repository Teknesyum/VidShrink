---
name: kalite-boyut-tekduze-degil
description: VidShrink plan motorunda PredictedQuality hedef MB ile tekduze artmiyor; ayrica WindowLayoutTests'te 5 test main uzerinde surekli kirmizi
metadata:
  type: project
---

`PlanCalculator.BuildDetailed`'in dondurdugu `PredictedQuality` hedef MB ile **tekduze
artmiyor**. T57'de olculdu: sentetik uc kaynak x iki niyet, tam aralikta 1206 ornek ->
42 ters donus, en buyugu 16,8 puan; ffmpeg klipleriyle olculmus profilde 242 ornek ->
12 ters donus, 7,9 puan.

**Why:** Duzen aramasinin olcek/kare hizi adaylari, `CompressionStrategy` rejim esikleri
ve `PickAudio` ses butcesi basamakli. Daha buyuk hedef, daha kotu puanlayan bir duzen
satin alabiliyor.

**How to apply:** Bu egri uzerinde arama yazarken duz ikiye bolme kullanma - ucurumun
yanlis tarafindan cevap verir. Calisan kalip (T57, `TargetMbForQuality`): logaritmik
kaba tarama ilk gecisi kusatir, ikiye bolme yalniz kusak icinde kosar. `BuildDetailed`
bu makinede ~0,18 ms, 25 cagri ~4,5 ms - kaydiricida her adimda kosturulabilir.
Kalite tabani `MinVideoBitrateK`*sure, tavani kaynak*`SourceSizeCap`; kisa kliplerde
taban zaten 70+ puan veriyor, yani 60/100 istegi cogu kaynakta ulasilamaz ve bunu
sessizce kirpmak yerine bildirmek gerekiyor.

**Ayri bir sey ama her turda karsina cikiyor:** `WindowLayoutTests` icinde 5 test
(`ThePageStopsScrollingAtThisHeight` x4, `TheMeasurementRigCarriesTheHeightItIsGiven`)
main uzerinde zaten kirmizi - T54 ve T57'de ayni. Kendi degisikligini suclamadan once
`git stash -u` ile taban olc.

Ilgili: [[no-build-bayat-derleme]], [[vidshrink-build-and-probe]]
