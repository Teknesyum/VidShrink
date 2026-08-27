---
name: vidshrink-panel-olcek-anlami
description: VidShrink oynatma panelinde yakınlaştırma panelin boyunu ölçekler, videonun içini değil; taban boy PanelMinHeight'ın iki katı
metadata:
  type: project
---

T52'de yakınlaştırmanın anlamı değişti: jest **panelin boyunu** ölçekler, görüntü panelin
içinde `FitScale` ile sığar ve ayrıca büyümez. `ZoomGesture.ContentZoom` yok, `PanelScale`
var; `ZoomTo` yerine `ScaleTo`. Ekranda okunan yüzde panel ölçeğidir.

Orta kademe eşiği artık aralığın ortası değil, terfi ölçeğinin (`PlaybackHoverZoom` = 2)
parametre karşılığı — 4x tavanda T = 1/3. Fare panele girince panel iki katına çıkar ve
aynı anda kök katmana terfi eder.

`PlaybackStageMinHeight` = `PlaybackIdleMinHeight` = `PanelMinHeight` × 2 = 512.

**Why:** Kullanıcı "%200 dediğim şey panel" dedi; T46'da yanlışlıkla videonun içi
ölçeklenmişti. Taban boyun iki kat olması da aynı isteğin ikinci yarısı.

**How to apply:** Bu paneli tekrar ellerken ContentZoom aramaya kalkma. Taban boyu 512'ye
çıkarmak tasarım boyunda sayfayı kaydırıyor: sayfayı tutan sütun sol ayar sütunundan orta
sütuna geçiyor ve plan paneli kendi tavanına (512) varamayıp 320'de kalıyor.

T52'de sekiz yerlesim testi kırmızıydı ama **tek sebep yoktu**: beşi ölçüm düzeneğinden
(`WindowLayoutTests` canlı ekranı okuyor, bkz. [[vidshrink-makine-ekrani]]), kalanı bu
512'lik tabandan. Ayrımı yapmadan "hepsi benim değişikliğim" ya da "hepsi düzenek" deme —
değişiklik olmadan bir taban koşumu al.

T0'ın kararı: ne `WindowLayoutTests` eşikleri ne `PlanPanelMaxHeight` oynatılacak. Çözüm
T54'ün orta sütun satır düzeninde — önizleme esneyen satıra, plan paneli kendi içeriğine.
Düzeneği T59 onarıyor. İkisi birleşince ölçüm yeniden koşturulacak.
[[windowlayouttests-sabit-sayilar]] ile birlikte oku.
