---
name: vidshrink-makine-ekrani
description: Yerleşim ölçümü canlı ekranı okuyor ve ekran oturumlar arasında değişiyor; başka koşumda pinlenen sayılar burada kırmızı çıkar
metadata:
  type: project
---

`WindowLayoutTests` açılış boyutunu **canlı ekrandan** türetiyor
(`tests/VidShrink.Tests/WindowLayoutTests.cs:131-139` — `Screens.Primary`'nin çalışma alanı
ölçeklemeye bölünüyor). Pencere `Maximized` açıldığı için ölçülen boy o anki ekrana bağlı.

Ekran sabit değil. T23 sırasında 1024x768 (çalışma alanı 1024x728) ölçülmüştü — pencerenin
kendi tabanından (`MinWidth`/`MinHeight` = 1040x720) küçük. T52 sırasında aynı makinede
2560x1440 (çalışma alanı 2560x1400) ölçüldü. T0'ın ölçümüyle taban 625 / tasarım 858 iken
ikisi de 895'e çıkmıştı ve T51'in koruması bunu yakaladı.

**Why:** İki kez aynı tuzağa düşüldü. T23'te başka makinede pinlenen `InRange(100, 150)`
burada 395 verdi. T52'de sekiz kırmızının beşi ana ağaçta, hiçbir değişiklik olmadan zaten
kırmızıydı; ölçüm düzeneğinden geliyordu, sözleşmenin değişikliğinden değil.

**How to apply:** Yerleşim ölçümüne elle boyut yazma. Açılış boyutunu
`Screens.WorkingArea`'dan, tasarım boyutunu `Theme.axaml` belirteçlerinden
(`WindowPreferredWidth`/`Height`), taban boyutu pencerenin `MinWidth`/`MinHeight`
değerlerinden türet. Mutlak taşma miktarı yerine yönü (kayıyor mu, kaymıyor mu) ve sayfanın
kaymayı bıraktığı yüksekliği pinle.

Bir yerleşim testinin kırmızı olduğunu görünce **önce değişikliksiz bir taban koşumu al**
(`git stash`, ya da ana ağaçta koştur). Kırmızıyı ne bütünüyle kendi değişikliğine ne de
bütünüyle düzeneğe yaz — T52'de ikisi üst üste binmişti ve ayrım yapılmadan iki kez yanlış
rapor edildi. Düzeneğin kendisini T59 onarıyor.

İlgili: [[avalonia-bassiz-yerlesim-olcumu]], [[windowlayouttests-sabit-sayilar]],
[[vidshrink-panel-olcek-anlami]]
