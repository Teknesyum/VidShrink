# Anahtar kare tavanı — T133

> **Bu dosyanın şu anki hâli yalnız K4'tür: ölçüm öncesi kilitlenen eşik.**
> Izgara, atlama tablosu ve karar henüz koşulmadı. Aşağıdaki eşikler hiçbir ölçüm
> sonucu görülmeden yazıldı; sonradan gevşetilirse commit geçmişi bunu gösterir.

---

## K4 — Eşik, ölçümden önce

### Ölçüm öncesi bilinenler

Bunlar benim ölçümüm değil; depoda **zaten yazılı** olan ve ızgaranın neyi kapsaması
gerektiğini belirleyen sayılar. Ölçüme başlamadan okundu ve buraya olduğu gibi alındı.

**1. `FfmpegArguments.cs:243-252`'deki tavan taraması** — `parca-1-20sn`
(1920x1080@60 HDR PQ, libx264, 2 geçiş, 20 MiB hedef, teslim boyutu tarama boyunca
%0,3 içinde):

| tavan | ortalama | p10 | I-kare | gerçekleşen aralık |
|---|---|---|---|---|
| 2 sn | 88,637 | 85,933 | 13 | 1,539 sn |
| 5 sn | 88,878 | 86,641 | 6 | 3,334 sn |
| 10 sn | 88,951 | 86,674 | 3 | 6,667 sn |
| 20 sn | 88,954 | 86,751 | 3 | 6,667 sn |

**2. `docs/olcumler/auto-mod.md:276`** — `parca-2` (1920x1080@60), tek değişen ayar
`-g`: `120 → 300`, boyut 11,35 MiB (**-%24,5**), kilitli ölçümde ortalama **+0,181**,
p10 **+0,235**.

### Sözleşmenin öncülünde aritmetik hata var

Sözleşme şöyle diyor:

> `-g 300` = 10,0 s @ 30 fps = **tavanın üst ucu**. Yani ölçülmüş kazanan, haritanın
> asla aşamayacağı ve haritanın devreye girmesiyle yalnızca uzaklaşılabilecek nokta.

**Kaynak 30 fps değil, 60 fps.** `auto-mod.md:300-304`, üretilen dosyalardaki anahtar
kare zamanları `ffprobe -skip_frame nokey` ile **doğrudan sayılmış**:

| çıktı | anahtar kare | en kısa aralık | en uzun aralık |
|---|---|---|---|
| auto (`-g 120`) | 31 | 2,00 sn | 2,00 sn |
| `-g 300` | 13 | **5,00 sn** | **5,00 sn** |

`-g 120 → 2,00 sn` ve `-g 300 → 5,00 sn` olması fps'i tek başına belirliyor: **60**.
Dolayısıyla ölçülmüş kazanan `-g 300` = **5,0 sn**, tavanın üst ucu (10,0) değil,
clamp'in **alt ucu**. Harita kolunun aralığı `[5,0 ; 10,0]` bu noktayı *tam olarak
içeriyor* — harita 5,0'ı seçebilir.

Sözleşmenin "ölçülmüş kazanan haritanın asla aşamayacağı nokta" cümlesi bu yüzden
**tersine dönmüş.** Ölçülmüş karşılaştırma 10 sn ile 5 sn arasında değil, **2 sn ile
5 sn** arasındaydı; ve 2 sn bugünün varsayılanı bile değil (o eski `fps × 2` sabitiydi).

Sözleşmenin **endişesi** yine de ayakta, ama gerekçesi başka: harita `-g`'yi 10,0'dan
yalnızca aşağı çekebiliyor ve yukarıdaki tarama 10 sn'yi 5 sn'den **hafifçe iyi**
gösteriyor (+0,073 ortalama / +0,033 p10). Yani harita kolunun bugünkü etkisi, tek
ölçülmüş taramada, küçük bir **kayıp** yönünde. Bu bir sonuç değil, ızgaranın
sınaması gereken hipotez — ve sözleşmenin yazdığından farklı bir hipotez.

Bu düzeltme ızgarayı değiştiriyor: asıl ayrım noktası **5 sn ile 10 sn arası** ve
**10 sn üstünün bağlayıcı olup olmadığı.** Sözleşmenin istediği 2/5/10/15/20 sn
karşılıkları korunuyor, çünkü 2 sn tarafı ikinci kaynakta bağımsız doğrulanmalı.

### Kilitlenen eşikler

Bütün karşılaştırmalar **eş teslim boyutunda** (±%0,5) ve **aynı ffmpeg sürümü
içinde** (9.0-full_build, CI ile aynı) yapılır. Boyut ±%0,5'e getirilemeyen hücre
atılmaz, **"eş boyut değil" damgasıyla tabloda kalır** ve karara sokulmaz.

**Gürültü tabanı önce ölçülür.** Bir hücre (kaynak 1, tavan 10 sn) **5 kez** tek
başına koşturulur; ortalama ve p10 için gözlenen en büyük fark `G` olarak yazılır.
Aşağıdaki eşiklerin hepsi `max(sabit eşik, 2 × G)` olarak okunur — yani gürültü
beklediğimden büyük çıkarsa eşik kendiliğinden yükselir, düşmez.

**Anlamlılık eşiği:** bir fark ancak **p10'da ≥ +0,20 ve ortalamada ≥ +0,10** ise
kazanç sayılır. Gerekçe: depodaki iki ölçülmüş `-g` etkisi (+0,235 ve +0,333 p10) bu
büyüklüğün üstünde; bunun altı, aynı depoda ölçülmüş koşumlar arası oynamayla
karışıyor.

**Bağlayıcılık şartı.** Bir tavanın "denendiği" sayılması için gerçekten bağlaması
gerekir: her hücrede `ffprobe -skip_frame nokey` ile I-kare sayısı ve gerçekleşen
aralık ölçülür. İki tavan aynı I-kare yerleşimini veriyorsa (yukarıdaki taramada 10
sn ile 20 sn'nin verdiği gibi) o hücre **"tavan bağlamadı"** damgası alır ve
kazanç/kayıp iddiasına sokulmaz.

**Karar kuralı — dört kaynak üzerinden:**

- **(a) Kolu korurum** — haritanın seçtiği kısa tavan, sabit 10 sn varsayılanını
  **en az 2 kaynakta** eşiği aşarak yenerse **ve** hiçbir kaynakta eşiği aşarak
  kaybetmezse. Bu durumda hangi kaynak sınıfında kazandığı yazılır ve eşik ölçüye
  bağlanır.
- **(b) Kolu kaldırırım / tavanı tek değere sabitlerim** — hiçbir kaynakta eşiği
  aşarak kazanmazsa **ve en az 1 kaynakta** eşiği aşarak kaybederse.
- **(c) Tavanı yükseltirim** — 10 sn üstündeki bir tavan, **en az 2 kaynakta** 10
  sn'yi eşiği aşarak yenerse **ve** o hücrelerde tavan gerçekten bağlarsa
  (bağlayıcılık şartı). Bağlamıyorsa (c) reddedilir, çünkü ölçülen şey tavan değil
  kodlayıcının kendi sahne kesmesidir.
- **Hiçbiri** — yukarıdakilerin hiçbiri sağlanmazsa karar **"eşik aşılmadı, bugünkü
  davranış korunur"** olur ve bu açıkça yazılır. Ölçüm sonrası eşik gevşetilmez.

**Atlama maliyeti (K2) ayrı eksendir** ve puanla toplanmaz. Kalite eşiği uzun `-g`
lehine aşılsa bile, ortalama atlama gecikmesi 10 sn tavanına göre **2 katından fazla**
büyüyorsa bu, tavsiyenin yanına açık bir ödünleşim cümlesi olarak yazılır; kalite
kararını tek başına ters çevirmez.

**Tekrar sayısı.** Gürültü hücresi 5 kez; kalan her hücre **1 kez**, ve eşiğe
`2 × G`'den yakın düşen her hücre **3 kez** tekrarlanır. Kaç kez koşturulduğu her
hücrede yazılır. Makinede eş zamanlı başka sözleşmeler koştuğu için ölçümler
**sırayla** yapılır, paralel koşturulmaz.

---

## K1 — Izgara

*Koşulmadı.*

## K2 — Atlama maliyeti

*Koşulmadı.* Tavanın yazılı gerekçesinin ölçülmüş mü varsayım mı olduğu sorusunun
cevabı yukarıda kısmen görünüyor (`FfmpegArguments.cs:243-252` bir tarama içeriyor,
yani gerekçe **ölçülmüş**); atlama gecikmesi tarafı ayrıca ölçülecek.

## K3 — Haritanın kolu ızgaranın neresine düşüyor

*Koşulmadı.*

## K5 — Reçete

*Yazılmadı.*

## K6 — Donanım kolu

*Karar verilmedi.*
