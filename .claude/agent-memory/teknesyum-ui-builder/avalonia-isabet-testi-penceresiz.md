---
name: avalonia-isabet-testi-penceresiz
description: InputHitTest penceresiz koşuda her yerde null döner; fare saydamlığını özellik ve olay yoluyla ölç
metadata:
  type: feedback
---

`Visual.InputHitTest(point)` bu projenin penceresiz ölçüm kalıbında **her zaman `null`**
döner — panonun tam ortasında, altında gerçek bir yüzey varken bile. Çizim yüzeyi
olmadan çalışmıyor. Sonuç `null` olduğu için "rozet fareyi yutmadı" testi hiçbir şey
ölçmeden yeşil verir.

**Why:** T49/K3 tam olarak bu tuzağı ölçtürüyordu. İlk yazılan test geçti, ama sonda
konunca isabetin `yok` döndüğü ve testin boş olduğu görüldü — projenin tekrar eden
kusuru (kod doğru, onu özetleyen cümle yanlış) buradan girecekti.

**How to apply:** Fare saydamlığını üç parçayla ölç: (1) `IsHitTestVisible == false`
öğede **ve** çocuğunda — Avalonia'da bu özellik miras almıyor, çocuk `true` kalır ve
"metin isabet testine açık" diye düşer, ikisine de açıkça yazılmalı; (2) öğeden kalkan
bir `PointerMoved` üst kabın kendi işleyicisine ulaşıyor mu (`RaiseEvent` + geçici
dinleyici); (3) davranışın kendisi — T49'da rozet gösterilirken iniş sayacının hâlâ
karar verebilmesi. Testin özet açıklamasına `InputHitTest`in neden kullanılmadığını yaz,
yoksa sonraki ajan onu "eksik ölçüm" sanar.
