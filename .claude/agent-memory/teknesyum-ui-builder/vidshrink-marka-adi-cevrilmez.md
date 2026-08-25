---
name: vidshrink-marka-adi-cevrilmez
description: VidShrink'te "Buy me a coffee" ve "Teknesyum" marka adıdır, LanguageCatalog'a çeviri girdisi yazılmaz
metadata:
  type: feedback
---

`Buy me a coffee` ve `Teknesyum` marka adıdır; `LanguageCatalog.EnglishSource` içine
Türkçe karşılık girilmez. `Names` sözlüğündeki `["teknesyum"] = "Teknesyum"` yalnız
yazımı sabitler, çeviri değildir.

**Why:** Kullanıcının kendi cümlesi: "kahve ısmarla diye bi çeviri yapma Buy Me Coffie
kalsın". Bir tur önce iyi niyetle eklenen çeviri girdisi denetimden kaldı.

**How to apply:** İmza/sponsor bloğuna dokunurken çeviri girdisi ekleme. Metin
`MainWindow.axaml`de iki yerde geçiyor (`AutomationProperties.Name` ve `TxtSponsor`);
ikisi de iki dilde aynı kalır. Bkz. [[vidshrink-metin-geciti]].
