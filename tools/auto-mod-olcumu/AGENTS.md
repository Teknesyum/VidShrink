# Auto mod ölçümü

Auto modun uzman ayarına göre nerede durduğunu ölçen düzenek. T102 kurdu.
Sayılar `docs/olcumler/auto-mod.md` içinde; burada onları **üreten** şey var.

- `harness/` — auto modu **başsız** koşturan küçük program. `tools/VidShrink.Bench`
  `PlanOptions.Codec`'i kurmadığı için auto modu ölçemiyor (`Program.cs:663`);
  bu düzenek onun yerine geçti. T106 bench'i düzeltince buna gerek kalmayabilir.
- `kos.sh` · `uzman.sh` · `hb.sh` — üç satırı (auto / uzman-biz / uzman-handbrake)
  eş boyutta üretir.
- `keyint.sh` · `bitrate.sh` · `kuyruk.sh` — K4'ün ayrıştırması; tek değişken.
- `hizalama.sh` — anahtar kare yerleşimini aralıktan ayıran A/B (T102 tur 3).
- `vmaf.sh` · `psnr.sh` · `olceksiz.sh` — ölçüm. `olceksiz.sh` ölçeklemenin
  kusur 4'ün sebebi olmadığını gösteren kontrol koşumu.
- `tablolar.py` · `tablo.py` · `birlestir.py` — belgedeki tabloları ham VMAF
  JSON'undan üretir. Elle yazılan sayı yok.
- `psnr-auto.log` · `psnr-hb.log` — kusur 4'ün kanıtı: 0 puanlı 26 karede PSNR 46-48 dB.
- `vmaf/*.json.gz` — belgedeki her VMAF sayısının ham kare kare kaynağı;
  `gunzip -k vmaf/*.gz` `tablolar.py`'nin beklediği dizini geri verir.

Kaynak `.calisma/kaynak/` altında, mutlak yolla verilir.

**Çıktıları buraya yazma.** mkv/json/log `.calisma/` altına gider.

Ölçüm koşarken iş parçacığını sabitle (`-threads N`, x265'te `pools`).
