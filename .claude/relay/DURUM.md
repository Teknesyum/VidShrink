# Durum — 24.08.2026, duruş noktası

Kullanıcı "müsait zamanda duralım" dedi. Üç ajan kayıt noktasında durduruldu, hiçbiri
mühürlenmedi. Bu dosya devam eden oturumun ilk okuyacağı yer.

## Bu oturumda kapanan işler

- **T32 — kare servisi.** `submitted`, main'e birleşti (`53d1564`). 256 test yeşil.
  Bulgusu sonraki her şeyi etkiliyor: p95 kuyruğu (692-800 ms) **anahtar kare uzaklığı
  değil süreç açılışı**. Hiç kare çözmeyen `ffprobe -show_format` bile aynı kuyruğu
  gösteriyor. Yani maliyet süreç başına, kare başına değil — özelliği daraltmak bunu
  düşürmez, kalıcı süreç düşürür.
- **Paylaşım hedefi kararı.** storage.to birincil, uguu.se ikincil. Google Drive iptal.
- **Oynatıcı planı.** `docs/PLAN-karsilastirma-oynatici.md`.
- **Konsey §2 hükümsüz.** "Duran kare, oynatma yok" maddesi kullanıcı talebiyle geçersiz.

## Koşan üç sözleşme — hepsi `open`, hiçbiri mühürlü değil

| # | İş | Nerede kaldı |
|---|---|---|
| **T33** | oynatma mimarisi ölçüm kapısı | boru duvarı bulundu, fps/çözünürlük sorusu karara bağlandı. **Ö7 (libmpv) sırası geldi mi belirsiz** — raporuna bakılacak |
| ~~T35~~ | storage.to + uguu.se sağlayıcıları | **main'e birleşti** (`4e0805f`). 296 test yeşil, canlı ağ denemesi iki sağlayıcıda da geçti, Drive'ın yedi dosyası silindi, `t31-drive` etiketi atıldı |
| **T36** | ayarlar sekmesi + kalite panelleri | iki dilli metin geçişi bitti, testler commit'siz kalmıştı |

Üçü de kendi worktree'sinde. Devam ederken **worktree'leri main'e birleştir**, yeni ajan
açma — işleri yarım ve bağlamları kendi transcript'lerinde.

## Kullanıcıya sorulacak tek açık soru

**180 MB'ın kaynağı yok.** Kullanıcı üç etiket istedi: 16 → WhatsApp için önerilen,
128 → paylaşım için en fazla, 180 → WhatsApp için en fazla.

16 ve 128 ölçülmüş sayılar (128 = uguu.se'nin ölçülmüş tavanı). **180 doğrulanmadı.**
T36'ya WhatsApp'ın kendi yardım sayfasından okumasını ve okuyamazsa bildirmesini söyledim;
cevabı raporunda. `RULES.md` ölçü uydurmayı yasaklıyor ve ipucu metni olgusal bir iddia —
yanlışsa kullanıcıya yalan söylemiş oluruz.

## Ölçülmüş, tekrar ölçülmeyecek

Bu oturumda bu makineden gerçek dosyayla ölçüldü (ayrıntı:
`docs/taramalar/anonim-kisa-omurlu-video.md`):

- **Türkiye'de engelli:** 0x0.st ve bashupload.com (`88.255.216.16` sinkhole),
  litterbox'ın sunum alan adı `litter.catbox.moe` (`195.175.254.2`, sahte sertifika
  `CN=localhost.localdomain`), qu.ax ve transfer.sh (bağlantı sıfırlanıyor).
- **litterbox tuzağı:** API çalışıyor ve bağlantı dönüyor, engel yalnız indirme alan
  adında. Ana sayfanın açılması dosya bağlantısının açıldığı anlamına gelmiyor —
  yeni bir aday değerlendirilirken **sunum alan adı ayrıca sınanacak**.
- **storage.to:** üç adımlı anonim akış çalışıyor, `owner_token` ile silme çalışıyor
  (410 Gone), CDN `video/mp4` + `Accept-Ranges` + `206`, ömür 1-7 gün seçilebiliyor.
  Paylaşılacak bağlantı `file.url`, CDN URL'i **değil** (o ~30 dakikada sona eriyor).
- **uguu.se:** 3 saat, 128 MiB, `video/mp4` + `206`, **silme jetonu yok**.
- **pixeldrain:** anonim yükleme kapalı (`401 authentication_required`).
- **catbox:** kalıcı ve anonim yükleme silinemiyor (`No userhash provided!`).

## Sonraki adımlar

1. Üç worktree'yi main'e birleştir, raporlarını oku.
2. 180 sorusunu kullanıcıya sor.
3. T33 biterse kazanan mimariye göre **T38'i yaz** (kare kaynağı) — plan §7'de sıra var.
4. Denetlenmemiş `submitted` sözleşmeler birikti; bir denetim turu gerekiyor.

## Açık kalan eski işler

T20 (donanım ilk deneme aşımı), T28 (ilk kurulumda GPU teşhisi), T4, T5, T9.

## T35'in bıraktıkları — devam eden oturuma

- Sağlayıcılar **arayüze bağlanmadı**. `paylasim-hedefleri.json` şemaya göre yazıldı ama
  T36'nın arayüzüyle birlikte görülmedi; son hâli sayılmamalı.
- Sürdürülebilir (resumable) yükleme yok — büyük dosyada kopan bağlantı baştan başlar.
- T35 "storage.to paylaşım sayfasının tarayıcıda oynadığı teyit edilmedi" diye yazdı,
  sebebi `HttpClient`'a dönen Cloudflare 403'ü. **Bu zaten doğrulandı** — T0 aynı oturumda
  sayfayı çekti, içinde `<video controls playsInline preload="metadata">` ve imzalı CDN
  kaynağı var, CDN başlıkları da ölçüldü (`video/mp4`, `Accept-Ranges`, `206`).
  Tekrar ölçmeye gerek yok; 403 tarayıcı olmayan istemciye çıkan bot koruması.
