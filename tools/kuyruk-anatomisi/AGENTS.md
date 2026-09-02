# kuyruk-anatomisi (T122)

`docs/olcumler/kuyruk-anatomisi.md`'nin her sayısı buradan yeniden üretilir.
Ek dosya gerekmez; yeniden kodlama da gerekmez.

- `kuyruk.py` — ortak yardımcılar: kare kare VMAF okuma, p10 ara değeri,
  kötü kare eşiği, kümeleme. `arsiv(ad)` T111 koşumlarını, `t122(ad)` bu
  sözleşmenin koşumlarını verir.
- `bitler.py` — kare başına paket boyutu. mp4 varsa `ffprobe`'dan, yoksa
  `paket-t122/` arşivinden; ikisi de aynı sayıyı verir.
- `hareket.py` — kaynağın ardışık kare farkı (`hareket-t122/`).
- `k1-konum.py` `k2-kesisim.py` `k2b-cozunurluk.py` `k3-gop-fazi.py`
  `k3b-kume-faz.py` `k4-sahne.py` `k4-icerik.py` `k4b-yapi.py`
  `k4c-hareket.py` `k5-bit.py` `k5b-ayrisan.py` `k6-ozet.py` — kabul
  kriterlerinin hesapları.
- `k45-uret.sh` `k45-olc.sh` `k6-gop.sh` `k6-boyutesle.sh` — kodlama ve
  kilitli ölçüm. `k4b-kaynak.sh` — kaynağın kare özdeşliği ve hareketi.
  Tek seferde tek ağır kodlama, `-threads 4` + `lp=4`.
- `vmaf-t122/` — kilitli VMAF-NEG çıktıları (gzip). `arsiv-*` T111'in
  koşumları (K1–K3), geri kalanı bu sözleşmenin koşumları (K4–K6).
- `paket-t122/` — kare başına paket boyutu (gzip csv).
- `hareket-t122/` — kaynağın kare başına ardışık fark ölçüsü ve kare
  özdeşliği (gzip csv).

**AV1'de paket boyutu kare maliyeti değil.** SVT-AV1 görüntü karelerinin
yarısını 3 baytlık `show_existing_frame` olarak yazar. Kare başına bit
hesaplayan her betik `s > 3` süzgecini kullanır; süzgeçsiz sayı `auto` için
%98 düşük çıkar. Yeni betik yazarken bunu atlamayın.

**Kilitsiz ölçüm bu klasöre girmedi.**
