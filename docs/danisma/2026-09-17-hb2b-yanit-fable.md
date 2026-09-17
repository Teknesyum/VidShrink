# HandBrake Dalga 2 — İkinci Danışma Yanıtı

Girdi: `hb2b-soru.md`, dört tablo, önceki yanıt. Kod yazılmadı, ölçüm koşulmadı.

## Soru A — Ölçer: (i) Geçersiz, Hüküm Yalnız (ii) İle

**Hüküm.** (i) okuması düşer; kontrolü tutmadı (4,05 ≠ 0,404) ve tutamazdı. Hata benim: CAMBI referanssız ve
bit derinliğine duyarlı bir ölçü; 8 bitlik kaynak 10 bitlik kaba konunca "kendine eş" bir kontrol değil, 4 kodluk
basamakları olan bir 10 bit görüntü olur. Bu kaynakla (i)'nin geçerli bir kontrolü yok, hüküm veremez. (ii)'nin
kontrolü tuttu (0,404) ve 8 bitlik e0'da eski ölçerle birebir (9,309); kollar (ii) ile geçerli sayılır.

**Soru 1 kapanmaz, açık kalır.** (ii) ile hiçbir kol 7,48/7,49'u geçmiyor; kolların arası 9,01–9,38, e0 9,31/9,39.
`karanlik`ta 6 bit kopyanın yalnız +0,57 oynattığı ölçekte bu farklar gürültü: **10 bit ve dört ayar CAMBI'yi hiç
oynatmadı.** grain8 örtü, 3× süre — düşer. Kural 2 ve 3'e bakmaya gerek kalmadı. Ölçülen tek gerçek: eski ölçerin
10bit 3,23'ü swscale dither hediyesiydi (doğrulandı); HB x265 8 bitin 6,48'i ise gerçek bir kodlayıcı farkı.

**B6'nın 2,84'ü.** Evet, aynı tuzak. 2000'de HB VT (ii) 8,96 ≈ ürün 9,06 — B6'nın "10 bit imzası" CAMBI'de yoktu.
5500'de kalan fark gerçek ama küçük: HB 6,89 / ürün 8,17 / 10bit-sınırsız 7,16. B6:22-23 ve önceki yanıtın Soru 1(a)
dayanağı ile Soru 6 "imza" cümlesi belgede düzeltilir; XPSNR imzası ayakta, CAMBI imzası düşer.

**Açık tanım sorusu, bu dalganın dışı.** (ii) "dither'sız 8 bit panel"; ürünün kendi oynatıcısı libmpv 10→8'de dither
uygular, 10 bit panel hiç indirmez. İddia "kullanıcının ekranı" ise okuma eski ölçere yakın çıkar ve 10 bit çıktı
gerçekten az bantlaşır. Hangi izleyici için hüküm verildiği ölçüm değil ürün kararı; kural koymuyorum, karar sizin.

## Soru B — VT: 10 Bit + Maxrate Girer, Soru 6 Açık Kalır

**Kuralın öngörmediği durum, karar.** İki kapı vardır ve önceki yanıt ikisini tek cümleye sıkıştırmıştı: (1) ürüne
girme kapısı — bugünkü ürüne göre hiçbir film hücresinde bant dışı gerileme yok, `ekran` önde kalır; (2) HB eşitlik
kapısı — 6 film hücresi HB VT'ye karşı bantta. (1) geçen değişiklik girer; (2) geçmedikçe Soru 6 açık yazılır.
"Geçen en küçük küme" kuralı (2) içindir; (2)'yi kimse geçmiyorsa (1) uygulanır, "değişiklik yok" değil.

10 bit + maxrate (1)'i geçiyor: ürüne göre ΔVMAF-NEG / ΔXPSNR(ii) k2000 +0,02/−0,11, k5500 +0,15/−0,05,
p2000 +1,11/+0,30, p5500 +0,86/+0,46 (−%5,6 baytta), h2000 −0,10/−0,07, h5500 +0,11/+0,29. Eksiler bantta,
artılar bant dışı. HB açığı toplamda XPSNR −2,04 → −1,20 dB. Dürüst sayım: katı kapı (2)'de ürün 3/6, 10 bit 2/6
(k5500 −0,17'ye karşı −0,23; 0,06 dB gürültü). **Girer**, "HB'yi yakaladı" diye değil, "ürünü geriletmeden yaklaştı" diye.

**Sınırsız düşer.** parlak 2000 −2,33 / harmonik 77,06 (HB 83,66), hareketli 2000 −0,48: tavan olmayınca VT düşük bit
hızında çöküyor. 5500'de en iyi kol olması bit hızına bağlı tavan icat etmeye yetmez. `prio_speed 0` ve `spatial_aq 1`
üç kesitte bayta kadar aynı sonuç: ölü ya da varsayılan; VT deterministik değil, md5 kanıtı kurulamaz — listeden çıkar.

**(2) parlak 5500 hücresi geçersiz, yeniden ölçülür.** Kol −%5,6 baytta; kbps bandı ±%2 ihlal. Ürün 8 bit aynı hücrede
`deneme=2` ile 5080'e çıkmış; ürün yolu bunu kapatır. Hüküm ürün yoluyla, HB ile eş baytta verilir; +%6 baytın
−0,27'yi kapatıp kapatmayacağını tahmin etmiyorum, ölçülür.

**(3) Süre kuralı çıplak kodlamaya.** Kol yalnız kodlamayı değiştiriyor; sondalar ayrı tasarım. Çıplak: 10 bit 2,8–4,3 sn,
HB VT 5,6–6,8 — geçer. Ürün toplamının 30–54 sn'si ayrı ve gerçek bir açık: 5 sn'lik kodlamaya 25–45 sn sonda.
VT'de bir sonda bir tam kodlama kadar; yeniden deneme döngüsü zaten kalibre ediyor. Bu bir sonraki soru, kolun değil.

**CI kabul ölçütü** (`vt` işi, macos-15, 4 kesit × 2000/5500, ürün yolu, HB VT eş bayt):
- 8 hücrede `ffprobe` `pix_fmt=yuv420p10le`, `profile=Main 10`; değilse iş kırmızı.
- Gerileme kapısı, pin bu koşumun `urun-vt` satırları: 6 film hücresinde ΔVMAF-NEG ≥ −0,3, ΔXPSNR(ii) ≥ −0,2;
  `ekran` 2 hücre HB VT'nin ≥ +0,3 önünde.
- kbps her hücrede HB VT ±%2; dışında kalan hücre "ölçülmedi" yazılır, hüküm sayılmaz.
- HB kapısı bloklamaz, sayılır: bantta/önde film hücresi bugün 2/6; azalırsa kırmızı.
- Çıplak kodlama sn ≤ 1,5 × HB VT sn, hücre başına.
- Negatif: `-foo 1` exit ≠ 0; 8 bit tek geçiş satırı referans olarak kalır.

## Soru C — Social: p4 Girer, Kapsam Social, Hareketli Şart

**(1) Girer.** B1 kuralı eş baytta: p4 parlak −0,22 / −0,01, karanlık +0,29 / +0,01 — iki hücre bantta; süre HB'nin
1,31×/1,39× ≤ 2×. p6 parlak −0,61/−0,42 ile bant dışı; x265 slow önde ama 4,5× ile düşer. Tek geçen p4.

**Kapsam yalnız Social ön ayarları.** bpp eşiği bu veriden türemez: `handbrake` 2000@24 (0,040 bpp) p6 önde,
Social ~6,1 Mbps@60 (0,049 bpp) p6 geride, 6000@24 (0,12 bpp) geride — fps ve mutlak bit hızı karışık, üç nokta.
Genelleme ikinci dalga: `handbrake` işinde p4 vs p6, 2000 ve 6000 × 4 kesit; 6000'de p4 dört kesitte bantta ya da önde
ve ≤ 2× ise eşik ölçülen kesişimden yazılır, değilse Social'da kalır. p5 ölçülmedi; kol olur, bloklamaz.

**(2) Yetmez; `hareketli` şart, 4 kesit `handbrake` şart değil.** Önceki yanıt Social işini 3 kesit yazdı; 60 fps'de
ön ayar farkı en çok harekette görünür (B5'te en duyarlı kesit). Social kapsamı için Social işi kapıdır; `handbrake`
işi yalnız kapsam genişletirken. **Kanıt md5 yerine günlük:** SVT deterministik değil; iki geçişin de stderr'inde SVT
`[config]` bloğunda `preset` değeri 4 okunur (doğrulanacak: satır varsayılan log düzeyinde yazılıyor mu). Uydurma
anahtar için kanıt "Error parsing option" uyarısı + `preset` satırının değişmemesi.

**(3) Onay: merkez kalır.** 17/42 = %40, p90 şartının çok altında; kural "tutmuyorsa merkez kalır" diyordu. %3–4
tasarım bedeli belgeye yazılır. Not: `urun-otomatik` parlak HB'nin −%5,3 baytında; p4 hükmü eş bayt kollarla verildi,
ürün yolu bu bedeli taşımaya devam eder — Social CI'da kapı eş bayt kolda ölçülür, ürün yolu ayrı satırdır.

**CI kabul ölçütü** (`social` işi, 1080p60 25 MB, 3 kesit, eş bayt HB ±%2):
- parlak, karanlık, hareketli: VMAF-NEG ≥ HB − 0,3, XPSNR ≥ HB − 0,2; süre ≤ 2 × HB. Hareketli geçmeden birleşmez.
- Günlük kanıtı: her iki geçişte `preset : 4`; p6 satırı referans kalır.
- Birim: ön ayar tablosu `preset=4`ü yalnız Social profillerine verir, `handbrake`/diğerleri p6 kalır (pimli test).
- Negatif: yarım bit kolu −2 civarı kalır; uydurma anahtar uyarı + `preset` değişmez.
- CAMBI karanlık 8,84 kapı değil, kayıt (p6 8,37 ile farkı gürültü bandında).

## Doğrulanacaklar

(i) yolunun yapısal geçersizliği için libvmaf CAMBI `enc_bitdepth` davranışı (kaynağa `enc_bitdepth=8` 0,404 verirse
teşhis doğru; hüküm değişmez). SVT `[config]` `preset` satırının varsayılan log düzeyinde basıldığı. VT'de sonda
maliyetinin toplam/kodlama oranı (bir sonraki soru). B6 ve önceki yanıtın CAMBI cümlelerinin düzeltildiği.
