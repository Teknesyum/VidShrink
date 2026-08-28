---
name: islemci-zamani-sayaci-bozuk
description: Bu makinede Process.TotalProcessorTime gercegin ~1/5'ini yaziyor; islemci maliyeti olcen her is bunu once dogrulamali
metadata:
  type: project
---

Bu makinede `Process.TotalProcessorTime` guvenilir degil: iki saniye boyunca tek bir
cekirdegi dolduran bir is parcacigina 0,34 saniye yaziyor. ffmpeg'de de ayni —
`libx264 -threads 1` ile 4,3 saniye koşan bir kodlama 0,8 saniye raporluyor.
Sapma sabit degil, koşumdan koşuma 1x ile 6x arasinda geziyor.

**Why:** T63'te kodlamanin islemci maliyeti olculecekti. Ilk uc olcum tamamen
tutarsiz cikti (ayni is icin 0,029 ile 1,036 arasinda gercek zaman cekirdegi);
sebep once orneklem kucuklugu, sonra taban cikarmasi sanildi, ikisi de degildi.
Sayacin kendisi bozuktu.

**How to apply:** Islemci maliyeti olcen her iste once sayaci kalibre et — bilinen
sureli tek is parcacikli bir yakim koştur, duvar saatiyle karsilastir. Oran 1'e
yakin degilse `TotalProcessorTime`'a karar bagalama. Bu projede calisan alternatif:
olcum gecisini `-threads 1` ile koştur ve **duvar saati / klip suresi** oranini al —
tek cekirdek calisirken duvar saati dogrudan cekirdek-saniyedir, makine yukune de
sasirtici derecede dayanikli (15 cekirdeklik yapay yuk altinda %83 kaydi, karar
degismedi). Kalip: `src/VidShrink.Ffmpeg/PerformanceProbe.cs`.
