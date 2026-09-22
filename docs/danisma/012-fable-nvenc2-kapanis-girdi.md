# Danışma 012 girdi: Soru: NVENC 2 satırı kapansın mı?

Ajana giden metin:

---

[[danisma:012]]

# Soru: NVENC 2 satırı kapansın mı?

Defter satırı: "NVENC 2: aynı baytta kalitede HandBrake'in önüne geç (9 hücrenin 8'inde geride)".
Kullanıcının genel hedefi: "HandBrake her alanda geçilsin".

Bugünkü durum (hepsi ölçüm, docs/olcumler/):
- h264_nvenc: HandBrake'ten +2,32 VMAF-NEG önde.
- av1_nvenc: −0,10 geride.
- hevc_nvenc: adil hücrelerde −0,20 / −0,22 ort geride (p10'da +0,057'ye kadar kapanan kol var).
- Denenen ve kapıda kalan kollar: GOP 5→10 sn (8/9 ort ≥0 ama hevc ort kapanmadı), tam kare, lookahead 20,
  boş bütçe yukarı denemesi, uhq tune, p7 (NVENC 5: 8/9; NVENC 6 yeni içerik: 5/9, gradyan −0,12, süre ×2,003),
  multipass qres/fullres (fark yok), b_ref_mode each (+0,18 ama −%3,6 bayt), AQ açık/kapalı, tepe/tampon çarpanları.
- Anahtar kare tavanı 5 sn ürünün arama bütçesi sabiti; GOP 10 sn açığı yalnız daraltıyor.

Seçenekler:
(a) Satırı kapat: hevc/av1 açığı ölçüm gürültüsü mertebesinde (≤0,22), algılanabilir değil; h264'te açık ara öndeyiz.
(b) Açık tut, tek bir kol daha dene (hangisi?).
(c) Açık tut ama "karar kullanıcının" diye bekle.

Kısa cevap: harf + en fazla 4 cümle gerekçe. (b) ise tek kolu ve kapısını yaz.
