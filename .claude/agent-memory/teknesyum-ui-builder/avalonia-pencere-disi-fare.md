---
name: avalonia-pencere-disi-fare
description: Fare pencere dışındayken PointerMoved atmaz; "fare gitti" kararı TopLevel PointerExited ister
metadata:
  type: project
---

Yalnız `PointerMoved` dinleyen bir "fare üstünde mi" durumu, fare pencerenin dışına
çıktığında son değerinde takılı kalır — pencere dışında hareket olayı hiç atmaz. Gecikmeli
gizleme/iniş sayacı bu yüzden hiç kurulmaz.

**Why:** T44/K2 bu yüzden denetimden kaldı; tam kademede panel bütün pencereyi kapladığı
için kullanıcının en olağan hareketi (fareyi pencereden çıkarmak) hiçbir şey tetiklemiyordu.

**How to apply:** terfi/kaplama sırasında `TopLevel` üstüne
`AddHandler(PointerExitedEvent, ..., Tunnel | Bubble | Direct)` bağla (olayın hangi
strateji ile kayıtlı olduğuna güvenme) ve `ReferenceEquals(e.Source, sender)` ile çocuktan
kabaran çıkışları ele. Terfi kalkarken sök. Testte gerçek olayı
`window.RaiseEvent(new PointerEventArgs(InputElement.PointerExitedEvent, window, new Pointer(...), window, ...))`
ile atabilirsin — dinleyicinin bağlı olduğunu bu kanıtlar.
Bkz. [[avalonia-zamanlayici-olcumu]].
