# T114 esikleri — olcumden once yazildi

Bu dosya **hicbir sayi olculmeden** yazildi ve kendi commit'inde durur. Sonradan
secilen esik kanit degildir; bu yuzden karar kurallari once, sayilar sonra.

Gecerlilik: bu commit'in oncesinde `.calisma/T114/` bostur ve
`docs/olcumler/sahne-butcesi.md` yoktur.

## Olculen uc dagitim

Her kaynak penceresi icin sahne basina **bit payi** (o sahnenin bitleri /
penceredeki toplam bit) uc ayri yoldan bulunur:

- `hak-edilen` — her sahne **ayri ayri**, ayni sabit CRF ile kodlanir. Sahnenin
  sabit kalitede istedigi bit budur. Referans budur.
- `verilen` — pencere **butun halinde** bugunku planla (hedef boyut, iki gecis)
  kodlanir; her sahne araligina dusen bit sayilir. Bu **kodlayicinin karari**.
- `harita` — `SceneMap.Scenes[i].Bits` (sonda cikti; x264 ultrafast crf23,
  640 genislik). Bu **bizim onerimiz**.

Karsilastirma olcusu: pay dizileri arasinda Spearman korelasyonu ve ortalama
mutlak pay hatasi (MAE, yuzde puani).

## K2 kapisi — is burada biter mi?

Kapanma sarti (**ucu birden**):

1. `Spearman(verilen, hak-edilen) >= 0,80` — kodlayici referansi zaten takip
   ediyor.
2. `MAE(verilen, hak-edilen) <= MAE(harita, hak-edilen)` — bizim onerimiz
   kodlayicinin kararindan **daha iyi degil**.
3. Ters dusen sahne orani `< %20` — `verilen` ile `harita`'nin referanstan
   sapmasi zit isaretli oldugu sahneler.

Ucu de saglaniyorsa: **"olculdu, kodlayici zaten daha iyi dagitiyor"** yazilir,
K3 ve K4 zorlanmaz, dogrudan K5'e (dogrulama olcumu) gecilir ve kod degismez.

Kapanmiyorsa K3–K4 acilir.

## K5 kapisi — dagitim koda girer mi?

Girme sarti (**dordu birden**):

1. VMAF-NEG **p10** kazanci `>= +0,50` puan, uc kaynagin **en az ikisinde**.
2. **En kotu sahne** kazanci `>= +1,00` puan, ayni iki kaynakta.
3. Hicbir kaynakta p10 kaybi `> 0,30` puan.
4. K6 saglanir: her kosumda gerceklesen boyut hedef bandin icinde
   (`FillBand.For(hedef)`), asan kosum orani **%0**.

Dordu saglanmiyorsa dagitim **koda girmez**; olculen sayi rapora girer.

Not: ortalama VMAF kazanci karar olcusu **degildir**; asil sayi p10 ve en kotu
sahnedir. Harmonik ortalama karar olcusu degildir (T106 onu sorusturuyor).

## K7 kapisi — bozuk harita bedeli

Bozuk haritayla (eksik kesim / fazla kesim, ikisi ayri) olculen p10 **kaybi**,
K5'te olculen p10 **kazancindan** buyukse dagitim koda girmez — kazanc
haritanin dogru olmasina bagliysa ve harita durgun icerikte kesim kaciriyorsa
(T105) kazanc guvenilir degildir.

## Cevap uretemeyen sonda

Bir kodlama ya da olcum zaman asimina ugrar veya cikti uretmezse sonuc
**`bilinmiyor`** yazilir; varsayilana dusulmez, ortalamaya karistirilmaz.
Raporda ayri satirda sayilir.

## Olcum hijyeni

- Tek ffmpeg surumu: rapora yazilan `ffmpeg -version` ciktisi butun kosumlar
  icin aynidir. Surum sinirini gecen kiyas yapilmaz.
- Is parcacigi sabit: `-threads 8`, x265'te `pools=8`. Makine paylasimli
  oldugu icin **sure** sayilarina damga basilir; bit ve kalite sayilarina
  basilmaz.
- Rapordaki her ozet sayisi betikten uretilir, elle tasinmaz.
