---
name: paylasim-yukleme-karari
description: VidShrink'in "tek tıkla yükle ve bağlantı paylaş" özelliği için platform kararı — Streamable elendi (2026-08-24), Google Drive drive.file önerildi (2026-08-24)
metadata:
  type: project
---

VidShrink'e sıkıştırılan videoyu tek tıkla yükleyip bağlantı paylaşma özelliği düşünülüyor.
İki tarama yapıldı, ikisi de 2026-08-24.

**Tarama 1 — Streamable (`docs/taramalar/streamable.md`): elendi.** Belgelenmiş yükleme API'si yok;
kullanım şartları "Use automated means to submit or edit User Content" fiilini açıkça yasaklıyor;
eski `POST /upload` uç noktası hâlâ 401 dönüyor ama belgesiz ve Basic Auth, yani kullanıcının hesap
parolasını masaüstünde saklamak gerekir.

**Tarama 2 — yedi büyük platform (`docs/taramalar/yukleme-platformlari.md`).** Kullanıcının ölçütü:
büyük ve köklü şirket, belgelenmiş yükleme API'si, tek tıkla yükle ve bağlantı al.

Önerilen: **Google Drive, `drive.file` kapsamı.** Ayırt edici bulgu: `drive.file` non-sensitive
olduğu için doğrulanmamış yayımlanmış uygulamada 100 kullanıcı tavanı ve "tehlike" onay ekranı
uygulanmıyor (bunlar yalnız sensitive/restricted kapsamlar için). Elle inceleme kuyruğu yok,
15 GB ücretsiz, `client_secret` installed-app akışında opsiyonel (AGPL kaynak kod için önemli).
İkinci sırada OneDrive (kuyruk yok ama 5 GB).

Elenenler ve nedenleri:
- **YouTube:** `videos.insert` kotası **proje başına günde 100** — bütün kullanıcılar VidShrink'in
  tek OAuth istemcisini paylaşır, 100 kullanıcıda kişi başı günde 1 video kalır. Üstüne
  denetimden geçmemiş projelerin yüklediği videolar zorla `private` kalıyor.
- **Vimeo:** ücretsiz hesap için yükleme erişimi elle inceleme, "up to five business days";
  ücretsiz plan hesap ömrü boyunca toplam 1 GB.
- **Dropbox:** 50 kullanıcıdan sonra iki hafta içinde production onayı alınmazsa yeni bağlantı donuyor.
- **Cloudflare Stream:** ücretsiz katman yok; kullanıcı hesap kimliği + API jetonu yapıştırmalı.
- **Bunny Stream:** BunnyWay d.o.o. — "büyük şirket" ölçütünü karşılamıyor.

**Why:** Proje AGPL-3.0-or-later, tek bakımcı, henüz yayınlanmamış. Sözleşme ihlali riskini, parola
saklama yükünü ve onay kuyruğu bakımını tek bakımcı taşıyamaz. Kullanıcı kendi hesabını girecek;
uygulamanın gömülü hesabı olmayacak.

**How to apply:** Yükleme/paylaşım konusu tekrar açılırsa sıfırdan araştırma yapma, o iki raporu oku.
Basic Auth ile parola saklayan hiçbir yükleme yolunu önerme. Plan/kota rakamlarını arayüze sabit
yazmayı önerme — taranan yedi belgeden beşi son 60 gün içinde güncellenmişti; kotayı sunucudan sor
(Drive'da `about.get`), sınırı sunucu hatasıyla göster. Drive'ın iki bilinen eksiğini karar
verirken hatırla: `type: anyone` bağlantısına süre konamıyor (`expirationTime` yalnız user/group)
ve 15 GB Gmail + Photos ile paylaşımlı.
