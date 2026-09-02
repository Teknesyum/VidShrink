# Siyah kenar kırpma — ölçüm

T134. `docs/inceleme/handbrake-motoru.md` § 6.5, madde 5: siyah kenar kırpma
VidShrink'te hiç yok, HandBrake varsayılan olarak yapıyor. Bugüne kadarki tüm
ölçümlerimiz kenarsız kaynaklarla yapıldı; letterbox'lı kaynak sınıfı hakkında
elimizde sıfır veri vardı. Bu belge o veriyi üretiyor.

## Hüküm

<!-- BETIK-HUKUM-BASLANGIC -->
Ölçüm henüz koşulmadı.
<!-- BETIK-HUKUM-BITIS -->

Bu paragraf elle yazılmaz. `tools/siyah-kenar/oz.py` tabloları okuyup cümleyi
üretir ve iki işaretin arasına yazar.

## K4 — Karar eşiği (ölçümden önce yazıldı)

Bu bölüm K3 koşulmadan önce commit edildi. Sonradan seçilen eşik kanıt değildir.

**Karar ölçüsü:** VMAF-NEG p10 — kare puanlarının 10. yüzdeliği — **aktif görüntü
alanında** ölçülür, teslim edilen dosya boyutu eşitlenmiş iki kol arasında.
Kırpmalı kolun p10'u eksi kırpmasız kolun p10'u = **kazanç**.

| Karar | Koşul |
|---|---|
| **Değer** | Dört letterbox'lı kaynağın kazanç ortalaması **≥ +1,00** ve en az üçünde kazanç pozitif |
| **Şu sınıfta değer** | Ortalama kazanç **+0,30 ile +1,00** arasında, ya da kazanç bant yüzdesiyle ayrışıyor. Sınıf sınırını veri çizer: kesim noktası kazancın +1,00'ı geçtiği en düşük bant yüzdesidir |
| **Değmez** | Ortalama kazanç **< +0,30** |

**Eşikten bağımsız iki veto.** Hüküm "değer" çıksa bile bunlar sağlanmazsa
kırpma **varsayılan açık gelemez**:

1. **Yanlış kırpma vetosu.** K5(a)'daki iki kenarsız kaynağın herhangi birinde
   `cropdetect` sıfırdan farklı bir kırpma öneriyorsa. Bir piksel fazla kırpmak
   görüntüyü keser; bu, kazanılan puanla telafi edilmez.
2. **Yoklama maliyeti vetosu.** `cropdetect` yoklaması klip başına **2,0
   saniyeyi** aşıyorsa. Bu maliyet doğrudan kullanıcının bekleme süresine biner.

Vetolar tetiklenirse üst sınır "kullanıcı onaylı seçenek"tir, varsayılan değil.

**Hangi hedef boyutta.** Üç bitrate noktası koşulur: **1000k, 2000k, 4000k**
(1920x1080@60, libx264, 2 geçiş, preset slow, sessiz). **Karar noktası
2000k'dir**; 1000k ve 4000k duyarlılık satırıdır ve hükmü değiştirmez, yalnız
hükmün bitrate'e ne kadar bağlı olduğunu gösterir. Bu satır da K3
koşulmadan önce yazıldı.

**+1,00 ve +0,30 nereden geliyor.** +0,30 alt sınırı, uygulama maliyetinin
karşılığı: kırpma yeni bir yoklama geçişi, yeni bir plan alanı ve yeni bir
hata sınıfı (yanlış kırpma) getiriyor; bundan küçük bir kazanç bu bedeli
ödemez. +1,00 üst sınırı, bu depoda bir satırda tek başına anlamlı sayılan
fark mertebesi (T125'te p10'da +0,86 "puanı değişti" diye raporlandı).

## K1 — Kaynak sınıfı

Bekliyor.

## K2 — Tespit güvenilirliği

Bekliyor.

## K3 — Kazanç

Bekliyor.

## K5 — Zarar tarafı

Bekliyor.
