# Görev paketi — HandBrake ile aramızdaki açığı ölç

Sole için yazıldı. Depo dalı: `main` (kendi dalını aç: `sole/handbrake-acigi`).
**Bu pakette hiçbir şey düzeltilmiyor. Yalnız ölçülüyor.**

## Neden

Kullanıcı 17 dakikalık bir videoyu hedef boyuta küçülttü: HandBrake'in çıktısı iyi,
bizimki kötü. Motorda neyin ne kadar pahalıya geldiğini bilmeden düzeltme yazmak
tahmin olur. Bu paket o açığı sayıya çeviriyor.

Şüpheli üç karar — hangisinin ne kadar kötü olduğunu ölçüm söyleyecek:

1. **Çözünürlük düşürme.** `CompressionStrategy.AllowsResolutionDrop` `Light` dışında
   her rejimde açık. HandBrake hedef boyutta çözünürlüğü korur, kaliteyi düşürür.
2. **Kare hızı düşürme.** `AllowsFpsDrop` `Aggressive` ve `Extreme`'de açık.
3. **Donanım kodlayıcı.** Aynı bit hızında nvenc/qsv/amf, x264/x265 `slow`'un belirgin
   altında kalır. Plan hız için donanımı seçiyorsa kaliteyi orada kaybediyoruz.

## Ölçüm düzeni

Ölçüm aracı zaten var: `tools/VidShrink.Bench` ve `QualityMeter` (VMAF, XPSNR, SSIM).
Yeni bir düzenek kurmadan önce onu kullan; yetmiyorsa büyüt, yerine yenisini yazma.

Karşılaştırma **teslim edilen boyut üzerinden** yapılır, ayar üzerinden değil: iki araç
da aynı hedef boyuta koşturulur, çıkan dosyaların gerçek boyutu ±%2 içinde değilse ölçüm
geçersizdir. Aynı boyutta hangi resim daha iyi — soru bu.

HandBrake tarafı `HandBrakeCLI` ile koşulur. Kurulu değilse kur ve hangi sürümü
kullandığını yaz. HandBrake'in hangi ön ayarını seçtiğini ve neden onu seçtiğini yaz —
"Fast 1080p30" ile "H.265 MKV 1080p30" aynı şey değil.

### Kaynaklar

En az üç kaynak: kullanıcının 17 dakikalık videosu (yolu ayrıca verilecek), depoda
kullanılan canlı kaynak, ve bir yüksek hareketli kısa klip. Kullanıcının videosu
elinde yoksa **bekleme**, ikisiyle başla ve eksik olduğunu raporda söyle.

### Hedefler

Her kaynak için üç oran: kaynağın **1/2**, **1/6** ve **1/20** boyutu. Bunlar
`CompressionStrategy.RegimeFor` sınırlarının (`1.5`, `6.0`, `30.0`) iki yanına düşüyor,
yani üç ayrı rejim ölçülmüş oluyor.

## İş 1 — açık ne kadar

Her kaynak × her hedef için tabloya şunlar girer: teslim edilen boyut, VMAF, XPSNR,
duvar saati süresi, seçilen kodlayıcı, çözünürlük, kare hızı. Bir satır VidShrink,
bir satır HandBrake.

Tek cümlelik cevap: **aynı boyutta VMAF farkı kaç puan.** Ortalama değil, en kötü
durumu da yaz.

## İş 2 — açığı üç karara dağıt

Aynı kaynak ve aynı hedefte VidShrink'i dört kez koştur:

1. Olduğu gibi (taban).
2. Çözünürlük düşürme kapalı.
3. Kare hızı düşürme kapalı.
4. Kodlayıcı yazılıma sabitlenmiş (`libx265` ya da `libx264`, `slow`).

Her koşumda VMAF ve süreyi yaz. Cevap: **her kararın kaç VMAF puanına ve kaç saniyeye
mal olduğu.** Kapatma yollarını kalıcı seçenek olarak eklemene gerek yok; ölçüm için
geçici bir yol açman yeterli, ama açtığın yolu raporda göster.

Kaynak dosyanın **hangi sahnesinin** bozulduğunu da göster: en düşük VMAF'lı saniyeyi
bul, iki çıktıdan da o karenin görüntüsünü al, yan yana koy.

## İş 3 — HandBrake ne yapıyor da biz yapmıyoruz

Kullandığın HandBrake ön ayarının ürettiği komut satırını al ve bizimkiyle karşılaştır.
Bizde karşılığı olmayan her şeyi listele — süzgeç, hız/kalite ayarı, kodlayıcı
parametresi, ses kararı, kap seçeneği. Listeyi **değer sırasına** koy: hangisi kaliteyi
gerçekten değiştiriyor, hangisi süs.

Bu liste bir sonraki paketin gündemi olacak, o yüzden eksiksiz olsun.

## Çıktı

Tek dosya: `docs/olcumler/handbrake-acigi.md`. İçinde tablolar, sayılar, kullanılan
sürümler (ffmpeg, HandBrakeCLI, kodlayıcılar), koşulan komut satırları ve karşılaştırma
kareleri. Rapora giren her sayı gerçekten koştuğun bir ölçümden gelsin.

## Sınırlar

- **Motor kodunu düzeltme.** Bu pakette teşhis var, tedavi yok. Ölçüm için açtığın
  geçici yollar dışında `src/**` altına yazma.
- Ara dosyalar `.calisma/` altına; iş bitince kendi bıraktığını sil, rapora giren sayı
  `docs/olcumler/`e kalır.
- `dotnet test -c Release` tamamı yeşil. Taban: 958 ölçü, 941 geçiyor, 17 atlanıyor,
  0 başarısız. Atlanan sayısı artmasın.
- Yorum yazma; mevcut yorumları koru.
- Kendi dalında çalış (`sole/handbrake-acigi`), bitince **it**. `main`e sen birleştirme.
