# Devir notu — 24.08.2026

Oturum masaüstüne devrediliyor. Bu dosya devralan oturumun ilk okuyacağı yer.

**Depo durumu:** main temiz, her şey push'lu. `dotnet test` → **333 başarılı, 0 başarısız,
6 atlanan.** `dotnet build -c Release` 0 uyarı.

---

## 1. İlk iş — devralan oturum buradan başlasın

**libmpv hiç ölçülmedi ve karar ona bağlı.**

T33 boru yaklaşımını ölçtü: 2×960×540'ta 309 fps (G1 geçti), 2×1920×1080'de 37,5 fps
(G2 kaldı). Kullanıcının açıkça istediği **60+ fps 1080p'de boruyla karşılanamıyor.**
Verebilecek tek aday libmpv ve sırası hiç gelmedi.

Ondan önce **iki ucuz şey denenmeli** — T33 ikisini de denemediğini yazdı ve ikisi de
duvarı kaldırabilir:

1. **Paylaşımlı bellek** (boru yerine)
2. **Daha büyük boru tamponu**

Ucuzsa boru yolu 1080p'de kurtarılır ve libmpv'nin +60-100 MB'ı, karışık GPLv2+/LGPL
lisansı ve süreç içi çökme riski hiç ödenmez. Pahalıysa libmpv ölçülür.

Sözleşme: `.claude/relay/contracts/T33.md` (`status: active`, mühürsüz).
Ölçüm: `docs/olcumler/T33-oynatma-olcumleri.md`.

## 2. Kullanıcıya sorulacak tek şey

**180 MB'ın kaynağı yok.**

Kullanıcı üç etiket istedi: 16 → WhatsApp için önerilen, 128 → paylaşım için en fazla,
180 → WhatsApp için en fazla. İlk ikisi ölçülmüş sayılar (128 = uguu.se'nin ölçülmüş
tavanı). **180'i destekleyen hiçbir kaynak bulunamadı.**

T36 WhatsApp'ın yardım sayfasını okumayı denedi ve okuyamadı: `faq.whatsapp.com` sayfaları
JavaScript ile kuruluyor, dört makale çekildi, üçü "Sayfa bulunamadı" döndü. Doğrulanabilen
tek iki sayı: **sohbet içi medya 16 MB, belge 2 GB.**

T36'nın kararı doğruydu: yongayı ekledi ama etiketi "WhatsApp için en fazla" değil
**"Yalnız belge olarak"** yaptı, doğrulanmış 16 MB tavanından kurdu.

**Soru:** 180 nereden geliyordu? Kaynağı yoksa etiket böyle mi kalsın?

## 3. Bu oturumda kapananlar

| İş | Sonuç |
|---|---|
| **T32** kare servisi | main'de (`53d1564`). p95 kuyruğu **süreç açılışı** çıktı, anahtar kare uzaklığı değil |
| **T35** paylaşım sağlayıcıları | main'de (`4e0805f`). storage.to + uguu.se, Drive silindi, `t31-drive` etiketi atıldı |
| **T36** ayarlar sekmesi | main'de (`b34f3ba`). Sekme, yedi yonga, kalite panelleri, güncelleme ayarı taşındı |
| **T33** oynatma ölçümü | main'de (`621d41e`). G1 geçti, G2 kaldı, libmpv ölçülmedi |
| Oynatıcı planı | `docs/PLAN-karsilastirma-oynatici.md` |
| Paylaşım taraması | `docs/taramalar/anonim-kisa-omurlu-video.md` |

Dördü de **mühürsüz** — hiçbiri denetlenmedi. Denetlenmemiş `submitted` sözleşmeler de
birikti; bir denetim turu gerekiyor.

## 4. Ölçülmüş — tekrar ölçme

### Türkiye engeli (bu makineden, gerçek dosyayla)

- **Engelli:** 0x0.st ve bashupload.com (`88.255.216.16` sinkhole), litterbox'ın sunum
  alan adı `litter.catbox.moe` (`195.175.254.2`, sahte sertifika `CN=localhost.localdomain`),
  qu.ax ve transfer.sh (bağlantı sıfırlanıyor).
- **litterbox tuzağı — kalıcı kural:** API çalışıyor, dosya yükleniyor, bağlantı dönüyor;
  engel yalnız indirme alan adında. **Ana sayfanın açılması dosya bağlantısının açıldığı
  anlamına gelmiyor.** Yeni bir aday değerlendirilirken sunum alan adı ayrıca sınanacak.

### Sağlayıcılar

- **storage.to:** üç adımlı anonim akış çalışıyor, `owner_token` ile silme çalışıyor
  (410 Gone), CDN `video/mp4` + `Accept-Ranges` + `206`, ömür 1-7 gün seçilebiliyor,
  tavan 25 GB. Paylaşılacak bağlantı **`file.url`**, CDN URL'i değil (o ~30 dk'da ölüyor).
  Paylaşım sayfasında `<video controls playsInline>` var — **doğrulandı**, T35'in
  "teyit edilmedi" notu `HttpClient`'a çıkan Cloudflare 403'ünden, tarayıcıda sorun yok.
- **uguu.se:** 3 saat, 128 MiB, `video/mp4` + `206`, **silme jetonu yok**.
- **pixeldrain:** anonim yükleme kapalı (`401`). **catbox:** kalıcı ve silinemiyor.

### Oynatma

- Boru duvarı: 2×1080p'de ffmpeg 172 fps üretiyor, boru 37 teslim ediyor — **%78 taşımada
  kayboluyor.** Duvar boru, kod çözme değil.
- Boru yolunda 2×1080p'nin pratik tavanı **30 fps**.
- **fps düşer, çözünürlük düşmez** kararı ölçümle desteklendi.

## 5. T35 ve T36'nın bıraktıkları

- Sağlayıcılar **arayüze bağlanmadı** — `paylasim-hedefleri.json` ile gerçek okuma denemesi
  ve pencereyi açıp ayarlar sekmesinin yerleşim ölçümü yapılmadı.
- Sürdürülebilir (resumable) yükleme yok — büyük dosyada kopan bağlantı baştan başlar.

## 6. Açık kalan eski işler

T20 (donanım ilk deneme aşımı), T28 (ilk kurulumda GPU teşhisi), T4, T5, T9.

## 7. Bu oturumda tekrarlayan bir sorun

**Dört ajan işi commit etmeden durdu**, biri worktree'si temizlenirken işini neredeyse
kaybetti, ikisi sözleşme dosyası commit'li olmadığı için hiç başlayamadı.

Sözleşmelere "her adımdan sonra commit at" maddesini yazmak yetmedi — ajanlar duraklarken
uygulamıyor. Devralan oturum ajan açarken **sözleşmeyi önce commit etsin** ve ajan
durduğunda worktree'yi kendi kontrol etsin.
