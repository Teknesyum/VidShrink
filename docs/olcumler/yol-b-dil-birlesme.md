# Yol B Kabuk — Dil Kataloğu Birleşme Ölçümü

`t0/yol-b-kabuk` dalına gerçek `origin/main` (`9c4b9907`) birleşirken 42 `Locales/*/main.json` çakıştı.
Ölçümün üç noktası: taban `f7f3fcfa` (ortak ata, 491 anahtar), dal tepesi `006c8702` (500),
main tepesi `9c4b9907` (492), birleşmiş ağaç 501. Bu belgenin ilk sürümü `f7f3fcfa`'yı
`origin/main` diye etiketlemişti; o nokta main'in 44 commit gerisindeki bir atasıydı,
tablodaki `main=491 / birlesik=500` satırları oradan geliyordu. Sayılar aşağıda yeniden temellendi.

Beklenti üç yönlü: `(B | (O−B) | (T−B)) − (B−O) − (B−T)`. Birleşim (`O ∪ T`) yanlış beklentiydi:
dal `main.player.menu.settings-all` anahtarını P3'te bilerek sildi, birleşim onu geri getiriyordu.
main'in tek eklemesi `main.update.maintenance-failed`; çakışan `BiciminTests.cs` o anahtarın
satırıyla dalın dokuz anahtarı birleştirilerek çözüldü.

```
UCYONLU	ar	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	bg	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	bn	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	cs	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	da	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	de	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	el	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	en	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	es	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	et	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	fa	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	fi	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	fr	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	he	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	hi	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	hr	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	hu	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	id	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	it	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	ja	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	ko	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	lt	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	lv	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	ms	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	nb	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	nl	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	pl	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	pt	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	ro	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	ru	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	sk	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	sl	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	sr	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	sv	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	sw	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	ta	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	th	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	tr	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	uk	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	ur	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	vi	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
UCYONLU	zh-Hans	taban=491	dal=500	main=492	dal-ekledi=10	main-ekledi=1	dal-sildi=['main.player.menu.settings-all']	main-sildi=[]	birlesik=501	dusen=0	fazla=0
SAYIM	katalog	42
SAYIM	cakisan-katalog	42
SAYIM	essiz-anahtar-sayisi	[501]
SAYIM	hatali-katalog	0
```
