# 020 — Kullanıcı Ön Ayar Kütüphanesi Arayüzden Nasıl Açılsın (Yanıt)

18 Eylül 2026. Soru metni: [020-onayar-arayuzu-girdi.md](020-onayar-arayuzu-girdi.md).
Fable'ın yanıtı olduğu gibi aşağıdadır.

---

**1. Nereden açılmalı — yonga şeridinin sonunda "+" yongası.**
Kullanıcı ön ayarı zaten yonga olarak *kullanıyor*; kendi ön ayarını da aynı şeritte bekler. Yol: Küçült sekmesi → şeridi tarar → sonunda "+" görür → basar. Ayarlar sekmesine koyarsan yol "Küçült'ten çık, Ayarlar'a gir, geri dön" olur ve kaydettiği şey nereden çıkacağını göstermez. Kaydedilen kullanıcı yongaları gömülü 8'in *sonuna*, "+"ın önüne dizilir; "+" her zaman son eleman kalır. Sekiz yongaya kullanıcının kaçını ekleyeceği bilinmiyor; şeridin taşma davranışı (kaydırma mı, satır kırma mı) ölçülmeli.

**2. Kaydet eylemi — "+" yongası, yalnız ad sorulur.**
"+"a basınca küçük bir yerleşik düzenleme alanı (popup/flyout, ayrı pencere değil) açılır: tek metin kutusu "Ad", altında "Kaydet". Kaydedilen, o anki tüm alanlar (hedef, niyet, kodek, doldurma, kısa kenar, ses, kap). Alan seçtirme yok; yonga bugün de bunların hepsini birlikte değiştiriyor, kısmi ön ayar kullanıcının şeritten öğrendiği modeli bozar. Ad boşsa kaydetme; aynı ad varsa `SaveUser` üstüne yazıyor — bunu kullanıcıya "Üstüne yazılsın mı?" diye tek satır sormak gerekir, çünkü sessiz üstüne yazma geri alınamaz.

**3. Ayırt edilmeli — biçimle: kullanıcı yongası, gömülülerden sonra ve dikey ince bir ayırıcının ardında; simgesi yok, kenarlığı kesikli ya da dış çizgili (outline) stil.**
Kullanıcı bunları ancak silebildiği için nerede duracağını bilmeli; yerleşim (ayırıcı + sıra) tek başına yeterli, kesikli kenarlık "benim, kalıcı değil" anlamını simgeye gerek kalmadan taşır. Metin eklenmez, 42 dile bir anahtar girmez. Kullanılacak kenarlık ölçüsü `Themes/Theme.axaml`'de yoksa eklenmesi gerekir; renk değil, kalınlık/kesiklik.

**4. İçe/dışa aktarma bu turda girmesin; kaydet/uygula/sil çıksın.**
Import/Export SaveFilePickerAsync'i ilk kez sokar, 42 dile en az dört yeni anahtar (İçe aktar, Dışa aktar, hata metinleri) ekler ve henüz bir ön ayarı olmayan kullanıcı için değeri yok. Eksik kalacak olan: kullanıcı ön ayarlarını başka bir makineye ya da kişiye taşıyamaz, presets.json'u elle kopyalamak zorunda kalır.

**5. Silmede onay penceresi yok; geri alma şeridi.**
Silme kullanıcı yongasına sağ tık değil (arayüzde o kalıp yok), yonganın kendi üzerinde beliren küçük "×" ile olur; basınca yonga şeritten kalkar, durum satırında "'Ad' silindi — Geri al" 5–6 saniye durur. Onay penceresi tek yongayı silmek için ağır, geri alma şeridi ise mevcut durum satırı kalıbını kullanır ve hata maliyetini sıfıra indirir. `LoadUser`/`SaveUser` zaten dosyayı yeniden yazdığı için geri alma, silinen profili tekrar `SaveUser` etmekten ibaret. "×"ın hangi jestte görüneceği (hover mı, her zaman mı) dokunmatik olmayan Windows'ta hover kabul edilebilir ama ölçülmeli.
