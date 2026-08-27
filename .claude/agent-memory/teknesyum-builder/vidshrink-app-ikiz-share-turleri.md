---
name: vidshrink-app-ikiz-share-turleri
description: VidShrink.App kendi ShareTarget/ShareTargetTable kayitlarini tasiyor, VidShrink.Core.Share icindekilerle ayni ada sahip iki ayri tur - koprüleyen kod ve test alias istiyor
metadata:
  type: project
---

`VidShrink.App` (MainWindow.axaml.cs sonu) kendi `internal ShareTarget` ve
`ShareTargetTable` kayitlarini tasir; `VidShrink.Core.Share` icinde ayni adla baska
turler vardir. App'inki ayarlar listesinin gordugu satir (uc nokta tasimaz), Core'unki
motorun konustugu satir (endpoints tasir). Ikisi ayni JSON dosyasindan okur.

**Why:** T35/T36 arayuz tarafini motordan once yazdi; sema ortak birakildi ama tur
paylasilmadi.

**How to apply:** Arayuzden motoru cagiran kod kimlikle koprüler
(`CoreShare.ShareTargetTable.Load().Find(app.Id)`), `using CoreShare = VidShrink.Core.Share;`
alias kullanir. Ayni ad ayni ad alaninda oldugu icin App icinde belirsizlik cikmaz,
ama **test projesi ikisini birden import ettigi icin CS0104 verir** - test dosyasinin
basina `using ShareTarget = VidShrink.Core.Share.ShareTarget;` gibi alias koy.
