# kuyruk-anatomisi (T122)

`docs/olcumler/kuyruk-anatomisi.md`'nin her sayısı buradan yeniden üretilir.

- `kuyruk.py` — ortak yardımcılar: kare kare VMAF okuma, p10 ara değeri,
  kötü kare eşiği, kümeleme.
- `bitler.py` — kare başına paket boyutu. mp4 varsa `ffprobe`'dan, yoksa
  `paket-t122/` arşivinden okur; ikisi de aynı sayıyı verir.
- `k1-konum.py` `k2-kesisim.py` `k2b-cozunurluk.py` `k3-gop-fazi.py`
  `k3b-kume-faz.py` `k4-sahne.py` `k4-icerik.py` `k5-bit.py`
  `k5b-ayrisan.py` `k6-ozet.py` — kabul kriterlerinin hesapları.
- `k45-uret.sh` `k45-olc.sh` `k6-gop.sh` `k6-boyutesle.sh` — kodlama ve
  kilitli ölçüm. Tek seferde tek ağır kodlama, `-threads 4` + `lp=4`.
- `vmaf-t122/` — beş koşumun kilitli VMAF-NEG çıktısı (gzip).
- `paket-t122/` — aynı beş koşumun kare başına paket boyutu (gzip csv).

K1–K3 T111'in arşivinden (`tools/auto-mod-olcumu/vmaf-t111/`, dal
`T111-auto-mod`) hesaplandı; betikler onu `.calisma/t122/vmaf/` altında bekler.
K4–K6 buradaki arşivden koşar, yeniden kodlama gerekmez.

**Kilitsiz ölçüm bu klasöre girmedi.**
