---
name: vidshrink-makine-ekrani
description: Bu makinenin ekranı 1024x768; başka makinede pinlenen yerleşim sayıları burada kırmızı, ölçüm boyutları türetilmeli
metadata:
  type: project
---

Ajanların koştuğu bu makinenin ekranı **1024x768** (çalışma alanı 1024x728) — VidShrink
penceresinin kendi tabanından (`MinWidth`/`MinHeight` = 1040x720) küçük. `Screens` bunu
doğru bildiriyor, sorun ölçümde değil.

**Why:** T23 tur 1'i yazan ajan başka bir makinedeydi ve taban boyuttaki taşmayı
`InRange(100, 150)` diye pinlemişti; burada aynı ölçüm 395 veriyor ve test **HEAD'de,
hiçbir değişiklik olmadan** kırmızı. Sayı panel yüksekliklerine, o da kullanılabilir
kodlayıcı kümesine bağlı.

**How to apply:** Yerleşim ölçümüne elle boyut yazma. Açılış boyutunu
`Screens.WorkingArea`'dan, tasarım boyutunu `Theme.axaml` belirteçlerinden
(`WindowPreferredWidth`/`Height`), taban boyutu pencerenin `MinWidth`/`MinHeight`
değerlerinden türet. Mutlak taşma miktarı yerine yönü (kayıyor mu, kaymıyor mu) ve
sayfanın kaymayı bıraktığı yüksekliği pinle. Bir testin kırmızı olduğunu görünce önce
`git stash` ile HEAD'de de kırmızı mı diye bak — miras kalmış olabilir.

İlgili: [[avalonia-bassiz-yerlesim-olcumu]]
