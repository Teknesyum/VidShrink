---
name: vidshrink-saha-olcumu-kanit
description: VidShrink sözleşmelerinde saha ölçümü (tahmin hatası, bias, süre) her zaman builder'ın kendi beyanı olarak geliyor; denetçiye komut çıktısı verilmiyor
metadata:
  type: project
---

VidShrink kabul kriterlerinin çoğu ölçüm istiyor ("180 MB'da hata %5'in altı", "52 sn ve
2 saatlik kaynakta tarama süresi", "pencere sapması"). Bu sayılar sözleşmenin Çıktı'sında
düz metin olarak duruyor; denetçiye ilişkin komut çıktısı (bench koşusu, ffmpeg logu)
eklenmiyor. T2b, T3 ve T2c'de aynı durum çıktı.

**Neden:** Ölçüm gerçek 830 MB'lık bir dosyada dakikalar süren ffmpeg koşusu; builder onu
bir kez çalıştırıyor, denetçinin çalıştırma aracı yok.

**Nasıl uygula:** Ölçüm maddesini iki parçaya ayır. (a) Yapısal yarı — kodda izlenebilir
olan (nokta sayısı süreden bağımsız mı, `-read_intervals` var mı, kırpma duruyor mu)
doğrudan denetlenir ve ✓ verilir. (b) Sayısal yarı — yalnızca beyan varsa
`? kanıtsız: bench çıktısı verilmedi` diye işaretle, sayıyı onaylama. Sırf bu yüzden tur
başlatma; T0'a "sayıyı doğrulamak istiyorsan bench çıktısını ekle" diye yaz.

İkinci ve bağlantılı risk: **tek dosyaya uydurulan sabitler.** `ScanWarmupSeconds = 0,75`
(T2c) doğrulama dosyasının bilinen doğru cevabına göre seçilmiş ve builder'ın verdiği
merdiven (0 → 1,1437 · 0,5 → 1,1636 · 0,75 → 1,1865; gerçek 1,191) seçilen noktada hâlâ
yükseliyor, yani yakınsadığı gösterilmemiş. [[vidshrink-fillband-ceiling-margin]] aynı
türden. Böyle bir sabit gördüğünde: aynı dosyayla hem ayarlanmış hem doğrulanmış mı diye
sor, cevabı notta belirt.
