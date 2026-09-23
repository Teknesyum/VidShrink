# Danışma 013 girdi: VP9 Cpu-Used Ürün Kararı — Danışma Girdisi

Ajana giden metin:

---

[[danisma:013]]

# VP9 Cpu-Used Ürün Kararı — Danışma Girdisi

Proje: VidShrink (hedef boyuta sıkıştıran video aracı, ffmpeg). VP9 küçültme yolu yeni açıldı
(libvpx-vp9, WebM, iki geçişli VBR). Ölçüm tam metni: `docs/olcumler/vp9-cpu-used-crf.md`
(ham veri `docs/olcumler/vp9-cpu-used-crf-ham.json`). Kendin oku; aşağıdaki özet onun yerine geçmez.

## Olgular

- Ürün varsayılanı `-cpu-used 4`, Hızlı mod `5` (`FfmpegArguments.DefaultPreset`, `PlanCalculator`).
- Ölçüt ölçümden önce yazıldı: taban cpu-used 0; bir adım hücrede VMAF-NEG ≥ taban − 0,3 ise kabul;
  ürün değeri altı hücrenin hepsinde kabul edilen en hızlı adım; hücrede adımların bit hızı %3'ten
  fazla sapıyorsa hücre "bayt tutmadı" ve hükme girmez.
- Altı hücreden yalnız gren/4000 bayt tuttu. Orada kabul edilen: cpu 0 (354 sn) ve cpu 1 (225 sn).
  Ölçütün kelimesiyle hüküm "önerilir: cpu-used 1". cpu 1 ürünün 4'ünden 3,9× yavaş (225/57 sn),
  1,78 VMAF-NEG iyi.
- Bayt tutmayan beş hücrede de cpu 4, cpu 0'a göre −0,03..−3,17 VMAF-NEG; cpu 2 toplam süre 402 sn
  (cpu 4: 204, cpu 1: 607, cpu 0: 1083). Kesitler sentetik (lavfi), 1080p24, 10 sn.
- Hızlı mod 5: altı hücrenin altısında 4'ten hem düşük kalite (−0,18..−1,31) hem daha uzun toplam
  süre (238/204 sn). cpu 6-8 de 4'ten yavaş (236-239 sn).
- Kullanıcı hedefi: HandBrake'i her alanda geçmek. HandBrake VP9'da varsayılan olarak libvpx
  "speed" ayarını kalite ön ayarlarına bağlar.

## Sorular

1. Ürün varsayılanı 4'te mi kalmalı, 1'e (ya da 2'ye) mi inmeli? Tek bayt tutan hücreye dayanan
   hüküm ürünü değiştirmeye yeter mi, yoksa önce gerçek kesitli tekrar mı şart?
2. Hızlı mod 5, 4'ten yavaş ve kötü ölçüldü. Hızlı mod VP9'da ne olmalı (4'e eşitlemek, kaldırmak,
   başka bir değer)?
3. Kalite/yavaş mod (varsa) için bir değer önerir misin?

Kısa ve gerekçeli cevap ver; her öneride hangi sayıya dayandığını yaz.
