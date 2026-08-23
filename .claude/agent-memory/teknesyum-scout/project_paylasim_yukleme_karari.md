---
name: paylasim-yukleme-karari
description: VidShrink'in "tek tıkla yükle ve bağlantı paylaş" özelliği için 2026-08-24 Streamable araştırmasının sonucu — Streamable'a otomatik yükleme önerilmiyor
metadata:
  type: project
---

VidShrink'e sıkıştırılan videoyu tek tıkla yükleyip bağlantı paylaşma özelliği düşünülüyor.
2026-08-24'te Streamable araştırıldı; rapor `docs/taramalar/streamable.md`.

Sonuç: **Streamable'a otomatik yükleme önerilmedi.** Üç bağlayıcı bulgu (hepsi birincil kaynaktan,
tarihler raporda):
- Streamable'ın kendi API belgesi yüklemeyi desteklemediğini yazıyor; yalnız `oembed.json` ve
  `videos/{shortcode}` okuma uç noktaları belgeli.
- Kullanım şartları "Use automated means to submit or edit User Content" fiilini açıkça yasaklıyor.
- Eski `POST /upload` uç noktası hâlâ 401 dönüyor ama belgesiz; kimlik doğrulaması Basic Auth, yani
  kullanıcının **hesap parolasını** masaüstünde saklamak gerekir. İptal edilebilir jeton yok.

Önerilen sıra: (1) tarayıcıda Streamable'ı açan pasif "Paylaş" düğmesi, (2) kullanıcının kendi deposu
(WebDAV en ucuz, S3 ön imzalı URL en iyi gizlilik), (3) Vimeo — OAuth 2 var ama uygulama kaydı ve
yükleme erişimi için elle onay gerekiyor.

**Why:** Proje AGPL-3.0-or-later, tek bakımcı, henüz yayınlanmamış. Sözleşme ihlali riskini ve parola
saklama yükünü tek bakımcı taşıyamaz; ihlalde kapanan hesap kullanıcınınki olur.

**How to apply:** Yükleme/paylaşım konusu tekrar açılırsa sıfırdan araştırma yapma, önce o raporu oku.
Basic Auth ile parola saklayan hiçbir yükleme yolunu önerme. Plan/kota rakamlarını arayüze sabit
yazmayı önerme — Streamable ve Vimeo bu sayıları yılda birkaç kez değiştiriyor.
