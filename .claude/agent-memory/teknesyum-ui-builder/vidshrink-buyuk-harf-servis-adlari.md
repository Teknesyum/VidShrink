---
name: vidshrink-buyuk-harf-servis-adlari
description: Title() geçidi "storage.to"yu "Storage.to" yapar; noktalı servis adları LanguageCatalog.Names'e ilk sözcüğüyle yazılır
metadata:
  type: project
---

`LanguageCatalog.Title()` her kelimenin ilk harfini büyütüyor. `storage.to` ve `uguu.se`
gibi küçük harfli servis adları ekranda `Storage.to` diye çıkıyor.

**Why:** Title, kelimenin gövdesinde büyük harf görürse dokunmuyor; tümü küçükse
büyütüyor. Kaçış yolu `Names` sözlüğü.

**How to apply:** `Names` anahtarı **nokta öncesi ilk sözcük** olmalı, tam ad değil.
Title, `TakeWhile(char.IsLetterOrDigit)` ile token'ı ayırıyor, yani `storage.to` için
aradığı anahtar `storage`. Doğru giriş `["storage"] = "storage"`; sonuç `storage` + `.to`
olarak birleşiyor. `["storage.to"]` yazmak işe yaramaz.

Yan etki: aynı sözcük başka bir cümlede geçerse o da küçük kalır. Ad seçerken buna bak.

Bağlantılı tuzaklar [[vidshrink-metin-geciti]] ve [[vidshrink-ipucu-metin-kaynaklari]]
içinde.
