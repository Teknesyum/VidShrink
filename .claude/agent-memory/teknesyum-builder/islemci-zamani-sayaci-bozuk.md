---
name: islemci-zamani-sayaci-bozuk
description: Bu makinede Process.TotalProcessorTime 3-7 kat eksik okuyor; islemci maliyeti olcen her is once sayaci is parcacigi duzeyinde kalibre etmeli
metadata:
  type: project
---

Bu makinede `Process.TotalProcessorTime` guvenilir degil. Bilinen sureli, tek is
parcacikli, cekirdegi dolduran bir yuk bes kez olculdu: sayac gercegin **1/3,2 ile
1/7,4'u** arasinda degisen bir kismini yaziyor. Sapma kosumdan kosuma degisiyor,
sabit bir duzeltme katsayisi yok. Butun okumalar 15,625 ms'nin katlari — sayac
tick ornekleme ile calisiyor ve burada asagi sapiyor.

**Why:** T63'te kodlamanin islemci maliyeti olculecekti ve ilk uc olcum tamamen
tutarsiz cikti. Once orneklem kucuklugu, sonra taban cikarmasi sanildi; ikisi de
degildi, sayacin kendisi bozuktu.

**How to apply:** Islemci maliyeti olcen her iste sayaci **once kalibre et**, ve
kalibrasyonu **is parcacigi duzeyinde** al (`GetThreadTimes` P/Invoke). Surec
duzeyinde alma: paralel kosan bir test konaginda baska is parcacikları da islemci
yakar, delta yakim suresinden buyuk cikar ve oran hep 1'e kirpilir — yani "saglam
sayac" ile "mesgul surec" ayirt edilemez. Bu tam olarak T63 tur 1'de yasandi ve
denetim yakaladi.

Bu projede calisan alternatif olcu: gecisi `-threads 1` ile kostur ve **duvar
saati / klip suresi** oranini al. Tek cekirdek calisirken duvar saati dogrudan
cekirdek-saniyedir. Oncılu dogrula: ayni gecisi serbest is parcacigiyla da kostur,
hizlanma varsa is gercekten islemciye baglidir. Donanim kodlayicisinda (nvenc)
hizlanma yok — orada duvar saati cekirdek maliyeti **degildir**, o sayiyi
"cekirdek" diye etiketleme. Kalip: `src/VidShrink.Ffmpeg/PerformanceProbe.cs`,
olcu `tests/VidShrink.Tests/PerformanceCheckTests.cs`.
