---
name: paylasim-yukleme-karari
description: VidShrink'in "tek tıkla yükle ve bağlantı paylaş" özelliği için platform kararı — ölçüt 2026-08-24'te "büyük şirket API'si"nden "anonim, kısa ömürlü, tarayıcıda oynayan"a değişti; storage.to ve filebin.net öne çıktı
metadata:
  type: project
---

VidShrink'e sıkıştırılan videoyu tek tıkla yükleyip bağlantı paylaşma özelliği düşünülüyor.
Üç tarama yapıldı, hepsi 2026-08-24.

**Ölçüt 2026-08-24'te değişti.** Eski ölçüt "büyük şirketin belgelenmiş API'si"ydi ve ilk iki
tarama (`docs/taramalar/streamable.md`, `docs/taramalar/yukleme-platformlari.md`) bu yüzden
**yanlış ölçüte göre yapıldı**. Yeni ölçüt kullanıcının kendi cümlesi: hesapsız, anahtarsız,
kısa ömürlü, alıcının indirmeden tarayıcıda izleyebildiği bir hedef.

**Tarama 3 — `docs/taramalar/anonim-kisa-omurlu-video.md`.** 19 aday yedi şarta karşı ölçüldü.
Yedisini de geçen iki aday:
- **storage.to** — anonim REST API, anahtar yok; paylaşım sayfasında gerçek `<video controls>`;
  3 gün (1–7 seçilebilir); `owner_token` ile anonim silme; ToS §3 resmî API'yi açıkça muaf
  tutuyor; 25 GB tavan. Uçtan uca test edildi ve silme doğrulandı.
- **filebin.net** — anonim POST, anahtar yok; dosya `Content-Disposition: inline` +
  `video/mp4` ile servis ediliyor; 6 gün sabit; URL'yi bilen herkes silebiliyor; OpenAPI
  belgesi araç yazmayı teşvik ediyor. Dosya başına boyut tavanı belgesiz.

Elenenlerin öğretici olanları: **pixeldrain** anonim yüklemeyi kapatmış (2024-08 ile 2024-11
arasında, kesin tarih doğrulanamadı); **catbox.moe** 2 yıl saklıyor; **0x0.st** minimum 30 gün
saklıyor ve otomatik istemcilere düşman; **bashupload** tek indirme hakkı verdiği için oynatıcı
çalışmıyor; **send.vis.ee** tarayıcıda şifrelediği için sunucuda oynatılabilir URL yok.

**Why:** Proje AGPL-3.0-or-later, tek bakımcı. Kaynak yayımlandığı için ikiliye gizli anahtar
konamaz — anahtar isteyen her aday bu tek gerekçeyle eleniyor.

**How to apply:** Yükleme/paylaşım konusu tekrar açılırsa sıfırdan araştırma yapma, üç raporu
oku; `anonim-kisa-omurlu-video.md` güncel ölçüte göre olanı. Anahtar isteyen, hesap isteyen ya
da alıcıyı indirmeye zorlayan hiçbir hedefi önerme. İki somut tuzak: (1) storage.to ToS'u
uygulama içi hotlink/gömme'yi custom plan'a bağlıyor — VidShrink içine önizleme oynatıcısı
koyma, yalnız bağlantıyı ver. (2) filebin'de bin adını asla kendin türetme, sunucunun ürettiği
rastgele adı kullan; adlar çakışabilir ve tahmin eden herkes bin'i silebilir. Kota/tavan
rakamlarını arayüze sabit yazma, sunucu hatasıyla göster.

**Ağ notu:** Ölçümler Türkiye'den yapıldı. `0x0.st`, `bashupload.com`, `gofile.io`, `qu.ax`
yükleme yolları ve `litter.catbox.moe` bu ağdan erişilemedi (bağlantı sıfırlaması / TLS zincir
hatası). Catbox FAQ'si Türkiye'yi engelli ülkeler arasında sayıyor. Hedefin Türkiye'den
erişilebilir olması ayrı bir şart gibi ele alınmalı.
