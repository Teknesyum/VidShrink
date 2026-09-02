# Sahne yer gerçeği

Sahne haritasının kaçırdığı kesimi ölçen düzenek. T101 kurdu, T105 kullanıyor.
Sayılar `docs/olcumler/sahne-haritasi.md` içinde; burada onları **üreten** şey var.

- `gercek-kesimler.txt` — gözle işaretlenmiş 28 kesim, pencere (144,117 – 333,300].
  Bu dosya elle üretildi ve **yeniden üretilemez**; en değerli parça budur.
- `tara.ps1` — `baseThreshold=0.01` ile geniş aday taraması (12.686 aday).
- `ciftler.ps1` — her aday için kesim öncesi/sonrası kare çifti; gözle ayrım buradan.
- `pencere.ps1` — pencereyi kaynaktan keser.
- `kodla.ps1` — kodlayıcı başına doğrulama kodlaması (x264/x265/svtav1).
- `maliyet.ps1` — sonda maliyeti; kare atlatma karşılaştırması.

Kaynak `.calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4`, mutlak yolla verilir.

**Çıktıları buraya yazma.** mkv/png/log `.calisma/` altına gider; hepsi
yeniden üretilebilir. Buraya yalnız betik, yer gerçeği ve özet CSV girer.

Ölçüm koşarken iş parçacığını sabitle (`-threads N`, x265'te `pools`) —
makine paylaşımlıysa sabitlemeyen koşum koşumdan koşuma farklı çıktı verir.
