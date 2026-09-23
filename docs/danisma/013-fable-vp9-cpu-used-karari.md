# Danışma 013: VP9 Cpu-Used Ürün Kararı — Danışma Girdisi

claude-fable-5-1, 8.023 çıktı token, 132 sn. Girdi: [013-fable-vp9-cpu-used-karari-girdi.md](013-fable-vp9-cpu-used-karari-girdi.md). Cevap olduğu gibi:

---

**Danışma 013 — cevap**

Ölçüm dosyasını (`docs/olcumler/vp9-cpu-used-crf.md`) ve kodu (`FfmpegArguments.cs:51`, `PlanCalculator.cs:1770`, `CompressionStrategy.cs:5`) okudum. Özetteki sayılar tabloyla uyuşuyor; bir fark yok.

**1. Varsayılan: cpu-used 1'e in, ölçütün hükmüyle. Tekrar, 1 ile 2 arasını ayırmak için yapılır; 4'ten çıkmak için beklenmez.**

- Hüküm tek hücreye dayanıyor ama *yön* altı hücrenin altısında aynı: cpu 1, cpu 4'ten 0,11..2,61 VMAF-NEG önde (gren 1500: −0,56'ya karşı −3,17; hareket 1500: −0,11'e karşı −1,50). Bayt tutmayan hücreler yavaş adımların *aleyhine* eğik: gradyan/4000'de cpu 0-2 hedefin %38-41 altında bayt harcayıp yine de cpu 4'ten 0,22..0,33 yukarıda. Yani geçersiz hücreler cpu 1'in üstünlüğünü abartmıyor, küçümsüyor. Büyüklük belirsiz, yön değil.
- 4'ün tek savunması süre (204 sn'ye karşı 607, tek geçerli hücrede 3,9×). VP9 bu üründe HandBrake VP9/WebM ön ayarından geliyor (`PresetLibrary.cs:439,529`); o kullanıcı HandBrake'le kıyaslıyor. HandBrake'in VP9 ön ayarları libvpx speed'e bağlanıyor ve "medium"ın cpu-used 2'ye denk geldiğini hatırlıyorum — **bunu `libhb/encavcodec.c`'de doğrulayın, ölçülmüş değil.** Doğruysa 4'te kalmak her hücrede HandBrake varsayılanının altında kalmak demek (cpu 2 − cpu 4: +1,98, +1,35, +0,30, −0,11, +1,20, +0,02).
- 2, tek geçerli hücrede eşiği 0,13'le kaçırıyor (−0,43'e karşı −0,3) ve süreyi yarıya indiriyor (402 / 607). Ölçüt yazılıp hüküm çıktıktan sonra eşiği 0,45'e çekmek, hafızadaki "özet veriden kayıyor" hatasının kendisi olur. 2'yi meşru kılacak tek yol gerçek kesitli tekrar.
- Tekrar tasarımı: yalnız cpu 1, 2, 4 (27 kodlama yerine 9×kesit); hedef bit hızı hücre başına CRF eğrisinden, kesiti dolduracak yerden seçilir (gradyan 4000k'yı hiçbir adımda dolduramadı, tasarım hatası buydu); ürünün Windows GyanD ffmpeg'iyle koşulur (libvpx derlemesi farkı "Sınırlar"da açık).

**2. Hızlı mod: 5 kaldırılır, 4 olur — ölçümsüz, bugün.**

- 5, altı hücrenin altısında 4'ten hem kötü (−0,18..−1,31) hem yavaş (238 / 204 sn; hücre hücre 50/49, 68/57, 25/23, 23/16, 28/26, 43/33). Baskılanmış bir adım; ürünün hiçbir kolunda yeri yok.
- 5-8 arası tek bir plato (236-239 sn, VMAF farkları ±0,3 içinde). Bu, libvpx'in `-deadline good` altında cpu-used'ı 5'e kırptığı bilinen davranışla uyuşuyor; 4'ün üstünde kazanılacak hız yok. Hızlı modun VP9'da hız kazancı isteniyorsa kaynağı başka yerdir (`-deadline realtime`, mevcut TurboFirstPass); ölçmeden açılmaz.
- Değişecek satır: `PlanCalculator.cs:1770` — `"5"` → `FfmpegArguments.DefaultPreset(codec)` ile aynı değer. Varsayılan 1'e inerse Hızlı=4 üç kat süre farkı taşır, mod anlamlı kalır. `FfmpegArguments.cs:29`'daki 0-8 listesini 0-5'e kırpmak ayrı ve küçük bir iş, bu turun değil.

**3. Kalite/yavaş mod: yok ve eklenmesin.**

- `SpeedMode { Quality, Fast }` — Quality varsayılan olan. Ayrı bir "daha yavaş" mod yok.
- Aday değer cpu 0 olurdu; tek geçerli hücrede cpu 1'den 0,01 *geride*, en iyi hücrede 0,56 önde (gren 1500), süresi 1,8× (1083 / 607). Ölçülebilir kazanç yok, mod açmaya değmez.
- Varsayılan 4'te bırakılacaksa 1 için taşıyıcı ancak bir "Kalite" modu olur; o da 1. sorunun kararını ertelemekten ibaret.

**Öncelik sırası:** Hızlı=4 (kanıt tam, ürün kodu bir satır) → varsayılan=1 (ölçütün hükmü, yön altı hücrede sağlam) → gerçek kesitli kısa tekrar (1 mi 2 mi; HandBrake eşlemesi doğrulanmış olarak).

Yok
