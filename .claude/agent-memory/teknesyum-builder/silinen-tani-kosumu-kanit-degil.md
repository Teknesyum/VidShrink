---
name: silinen-tani-kosumu-kanit-degil
description: Tani kosumunu silersen iddian dayanaksiz kalir; sozlesmenin zorunlu kildigi olcuyu terk ediyorsan gerekcesi ham dosyada durmali
metadata:
  type: feedback
---

Bir sozlesmenin zorunlu kildigi olcuyu terk ediyorsan, terk etme gerekcesini
ureten kosum **kalici bir olcu** olmali ve sayilari ham olcum dosyasina yazmali.
Iskele olarak kurup silme.

**Why:** T63 tur 1'de sayacin bozuk oldugunu gecici bir tani testiyle olctum,
sonra "iskeleyi kaldir" diye sildim. Rapordaki uc sayi ("2 s'de 0,34 s" gibi)
ham dosyada yoktu, ustelik dosyada kalan iki kalibrasyon okumasi (1,129x, 1x)
tersini soyluyordu — kalibrator bozuk oldugu icin. Denetim bunu KRITIK olarak
yakaladi: manset sayi, saklanmayan ve saklanan veriyle celisen bir kanita
dayaniyordu. Bu projenin en sik tekrarlayan kusuru.

**How to apply:** Olcumu birakmadan once sor: "bu sayiyi denetci ham dosyada
bulabilir mi?" Bulamiyorsa ya kosumu kalici `[FfmpegFact]` yap ve dosyaya
yazdir, ya da iddiayi kaldir. Kaldirmak, dayanaksiz birakmaktan iyidir. Ayrica
bir kalibratoru pinleyen olcu cürütülebilir olmali — `Assert.True(x >= 1)` gibi
clamp yuzunden hep gecen bir olcu hicbir sey baglamaz.
