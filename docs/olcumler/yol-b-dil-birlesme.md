# Yol B Kabuk — Dil Kataloğu Birleşme Ölçümü

`t0/yol-b-kabuk` dalına `origin/main` birleşirken 42 `Locales/*/main.json` çakıştı.
Beklenti üç yönlü: `(B | (O−B) | (T−B)) − (B−O) − (B−T)`. Birleşim (`O ∪ T`) yanlış beklentiydi:
dal `main.player.menu.settings-all` anahtarını P3'te bilerek sildi, birleşim onu geri getiriyordu.

```
UCYONLU	ar	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	bg	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	bn	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	cs	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	da	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	de	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	el	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	en	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	es	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	et	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	fa	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	fi	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	fr	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	he	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	hi	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	hr	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	hu	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	id	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	it	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	ja	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	ko	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	lt	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	lv	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	ms	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	nb	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	nl	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	pl	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	pt	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	ro	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	ru	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	sk	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	sl	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	sr	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	sv	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	sw	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	ta	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	th	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	tr	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	uk	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	ur	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	vi	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
UCYONLU	zh-Hans	taban=476	dal=485	main=491	dal-ekledi=10	main-ekledi=15	dal-sildi=['main.player.menu.settings-all']	birlesik=500	dusen=0	fazla=0
SAYIM	katalog	42
SAYIM	cakisan-katalog	42
SAYIM	essiz-anahtar-sayisi	[500]
SAYIM	hatali-katalog	0
```
