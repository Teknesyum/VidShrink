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

## K3 — Karşılaştırmayı adil kılan yöntem (ölçmeden önce yazıldı)

Kırpılmış çıktı 1920x804, kırpmasız çıktı 1920x1080. VMAF iki girdiyi kare kare
hizalar ve aynı çerçeveyi bekler; farklı çerçeveli iki dosyanın puanı doğrudan
karşılaştırılamaz. İki yöntem koşuldu — aynı ölçüme iki ayrı soru soruyorlar.

**A yöntemi — yalnız aktif görüntü alanında puanlama (birincil, karar buna bağlı).**
Referans, kaynak dosyanın aktif alanı: `crop=1920:H:0:Y`. Kırpmasız kolun çıktısı
aynı `crop` ile aktif alana indirilir; kırpmalı kolun çıktısı zaten o çerçevededir.
Her iki kol da 1920xH karede ve aynı referansa karşı puanlanır.

- **Tuttuğu:** izleyicinin gerçekten baktığı piksellerde kalite farkı. "Bit banda
  mı görüntüye mi gidiyor" sorusunun doğrudan cevabı budur.
- **Kaçırdığı:** bandın içinde olan hiçbir şey. Kırpmalı kol bandı hiç taşımıyor;
  A yöntemi bunu ne ödüllendirir ne cezalandırır. KD'de bant gürültülüdür,
  kırpma o gürültüyü tamamen atar — A yöntemi bu farkı görmez. Kırpmanın
  oynatma tarafındaki sonucunu (dosyanın en-boy oranı artık 2,39:1, oynatıcı
  kendi siyahını koyuyor) da ölçmez.

**B yöntemi — geri doldurup tam karede puanlama (ikincil).**
Kırpmalı kolun çıktısı `pad=1920:1080:0:Y:black` ile özgün çerçeveye döndürülür;
her iki kol da tam 1920x1080 karede, referans olarak kaynağın kendisiyle
puanlanır.

- **Tuttuğu:** kırpmanın bant içeriğini kaybetmesinin bedeli. Kaynağın bandı tam
  siyah değilse geri doldurma o farkı ceza olarak yazar. Bant sahne içinde
  kalkıyorsa gerçek görüntüyü kesmenin bedelini de yazar — K5(b)'nin ölçüsü
  budur.
- **Kaçırdığı:** tam siyah bantta iki kol da bant bölgesinde neredeyse kusursuz
  puan alır ve puanın büyük kısmı karenin kolay dörtte birinden gelir; bu,
  aktif alandaki gerçek farkı **seyreltir**. Karar bu yüzden A'ya bağlandı.

**İkisinin de kaçırdığı.** VMAF-NEG algısal bir vekildir. Kırpmanın asıl
kullanıcı faydası — aynı ekranda daha büyük görüntü, daha az ölü piksel —
oynatma tarafındadır ve bu belgede ölçülmüyor.

### İki kolun aynı kaynaktan aynı aralıkta olduğunun kanıtı

Bu depoda ölçüm parçalarının sessizce farklı çıktığı görüldü. Burada o hata
yapısal olarak imkânsız kılındı: **iki kol tek bir kaynak dosyayı okur.**
Kırpmasız kol dosyayı olduğu gibi, kırpmalı kol aynı dosyaya `crop` filtresi
ekleyerek alır. Ayrı kesim, ayrı `-ss`, ayrı ara kodlama yok.

<!-- BETIK-KANIT-BASLANGIC -->
Bekliyor.
<!-- BETIK-KANIT-BITIS -->

## K1 — Kaynak sınıfı

<!-- BETIK-K1-BASLANGIC -->
Bekliyor.
<!-- BETIK-K1-BITIS -->

## K2 — Tespit güvenilirliği

<!-- BETIK-K2-BASLANGIC -->
Bekliyor.
<!-- BETIK-K2-BITIS -->

### Tekil karelerin birleştirilmesi

10 tek kareyi ayrı ayrı yoklayıp sonuçları birleştirmek (HandBrake'in önizleme
taraması bu şekle benziyor) tek bir tamamen siyah kareye karşı kırılgandır:
siyah karede `cropdetect` tam kareyi döndürür, birleşim de kırpmayı iptal eder.
Aynı on kareden **medyan** almak bu kareyi yutar. İkisi de aşağıda.

<!-- BETIK-K2B-BASLANGIC -->
Bekliyor.
<!-- BETIK-K2B-BITIS -->

### `limit` eşiği taraması

`cropdetect`'in tek ayarı `limit`. Düşük tutmak gürültülü bandı bant saymaz
(kırpma kaçar), yüksek tutmak koyu görüntüyü bant sanar (görüntü kesilir). KE,
yalnız bu taramayı sınamak için üretilmiş ağır gürültülü bant taşıyan kaynaktır;
K3 ızgarasına girmez.

<!-- BETIK-LIMIT-BASLANGIC -->
Bekliyor.
<!-- BETIK-LIMIT-BITIS -->

### Yoklamanın süresi

İlk koşumda makinede başka sözleşmelerin `ffmpeg` süreçleri vardı ve süreler
kararsız çıktı; süre ölçümü ayrıca, makine boşken tekrarlanarak alındı.

<!-- BETIK-SURE-BASLANGIC -->
Bekliyor.
<!-- BETIK-SURE-BITIS -->

## K3 — Kazanç ızgarası

<!-- BETIK-K3-BASLANGIC -->
Bekliyor.
<!-- BETIK-K3-BITIS -->

## K5 — Zarar tarafı

<!-- BETIK-K5-BASLANGIC -->
Bekliyor.
<!-- BETIK-K5-BITIS -->
