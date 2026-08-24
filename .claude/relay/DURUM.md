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
| **T33** | oynatma mimarisi ölçüm kapısı | **main'e birleşti** (`621d41e`), `status: active`, mühürsüz. Boru: **G1 geçti** (2×960×540'ta 309 fps, p99 5,02 ms), **G2 kaldı** (2×1080p'de 37,5 fps, p99 30,5 ms), G3 karara bağlanmadı (ön sonuç %36-84). **libmpv hiç ölçülmedi** |
| ~~T35~~ | storage.to + uguu.se sağlayıcıları | **main'e birleşti** (`4e0805f`). 296 test yeşil, canlı ağ denemesi iki sağlayıcıda da geçti, Drive'ın yedi dosyası silindi, `t31-drive` etiketi atıldı |
| **T36** | ayarlar sekmesi + kalite panelleri | **main'e birleşti** (`b34f3ba`), `status: open`, tur 1. Build 0 uyarı, 343 test yeşil. Kalan: `paylasim-hedefleri.json` ile gerçek okuma denemesi ve pencereyi açıp yerleşim ölçümü |

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

## T33'ün en önemli bulgusu — sonraki kararı bu belirliyor

**Duvar borunun kendisi, kod çözme değil.** 2×1080p'de ffmpeg 172 fps üretebiliyor ama
boru 37 teslim ediyor; kapasitenin %78'i taşımada kayboluyor. Kanıt CPU sütununda:
çözünürlük yükseldikçe ffmpeg'in CPU'su **düşüyor** (%638 → %137) — süreç kareyi
hesaplamakla değil boruya yazmakla meşgul.

Sonucu: **boru yolunda 2×1080p'nin pratik tavanı 30 fps.** Kullanıcının açıkça istediği
60+ fps 1080p'de boruyla karşılanamıyor. 2×1280×720'de (153 fps) ve 2×960×540'ta
(309 fps) rahat karşılanıyor.

**60+ fps'i 1080p'de verebilecek tek aday libmpv ve o hiç ölçülmedi.** Devam eden oturumun
ilk işi Ö7 olmalı — karar kuralı onsuz işletilemiyor.

T33 iki şeyin de denenmediğini yazdı ve ikisi duvarı kaldırabilir: **paylaşımlı bellek**
ve **daha büyük boru tamponu**. Ö7'den önce bunlar denenmeli; ucuzsa boru yolu 1080p'de
kurtarılabilir.

## 180 sorusunun cevabı geldi — karar kullanıcıda

T36 WhatsApp'ın yardım sayfasını okumayı denedi ve **okuyamadı**: `faq.whatsapp.com`
sayfaları JavaScript ile kuruluyor, dört makale çekildi, üçü "Sayfa bulunamadı" döndü,
biri kırpılmış geldi ve hiçbirinde sayı yoktu.

WhatsApp'ın kendi SSS metninden alıntılanmış hâlde doğrulanabilen iki sayı var:
**sohbet içi medya 16 MB, belge 2 GB.** 180 MB'ı destekleyen hiçbir kaynak bulunamadı.

T36'nın kararı: yonga eklendi (kullanıcı istedi) ama etiketi **"WhatsApp için en fazla"
değil, "Yalnız belge olarak"** — doğrulanmış 16 MB tavanından kuruldu. Doğru davranış:
yongayı atmadı, uydurma da yapmadı.

**Kullanıcıya sorulacak:** 180 nereden geliyor? Kaynağı yoksa etiket "Yalnız belge olarak"
kalsın mı, yoksa yonga sayısız mı bırakılsın?
