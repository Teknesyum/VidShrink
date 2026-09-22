# Danışma 011 girdi: Soru: hevc_nvenc'te preset p4 -> p7 ürüne alınsın mı?

Ajana giden metin:

---

[[danisma:011]]

# Soru: hevc_nvenc'te preset p4 -> p7 ürüne alınsın mı?

## Olgular (docs/olcumler/nvenc-5-uhq-p7.md, ham veri docs/olcumler/nvenc-5-ham.json)
- Ölçümden önce yazılmış kapı: 18 hücre (3 kesit × 2 kodek (hevc_nvenc, av1_nvenc) × 3 hedef bit hızı).
  Şartlar: ≥15/18 hücrede VMAF-NEG ≥ taban+0,10; hiçbir hücrede taban−0,30'dan kötü değil;
  süre ≤ taban×2,0; tavan aşımı yok.
- +p7 kolu: 8/18 geçti, en kötü −0,02, ortalama +0,142, tavan aşımı 0. Kapı düştü.
- Ayrıştırınca: hevc'in 9 hücresinde fark +0,07..+0,61, 8'i ≥+0,10, kayıp yok. av1'de (p6→p7)
  −0,02..+0,08, hiçbiri ≥+0,10. Teslim oranı tabanla ±0,007 içinde (aynı bayt).
- Hız: saf kodlayıcıda hevc p4→p7 ×1,71..1,79 (2,0 sınırının altında); ürün içinde fark
  görünmüyor (×0,95..0,98), çünkü 4 çekirdekte FFV1 çözme ve ölçekleme darboğaz.
- HandBrake'e karşı p7 11/18 hücrede önde (taban 9/18).
- uhq kolu ayrıca 3/18 ile düştü; onu kapatma önerisi var, bu soru onu kapsamaz.
- Kullanıcının kuralı: HandBrake'i her alanda geçmek. Kapılar ölçümden önce yazılır.

## Soru
Kapı ölçümden önce yazıldı ve 18 hücrede düştü. hevc'e sınırlı bir kapıyı (9 hücre) ölçüm
sonrasında açmak kapıyı sonradan kaydırmak mı olur? Seçenekler:
(a) hevc p7'yi şimdiki veriyle ürüne al (9 hücrede 8/9, kayıpsız);
(b) önceden yazılmış hevc-yalnız yeni bir kapıyla yeni kesitlerde (başka içerik) tekrar ölç, sonra karar ver;
(c) kapat, ürün p4'te kalsın.
Hangisi, neden? Kısa, Türkçe.
