# Tepe-tavan egrisi (T108)

Durum: donanim izgarasi (60 hucre) tamam, yazilim izgarasi kosuyor.
Olculen commit: `0d34f08` (dal `T108-tepe-egrisi`).

## Olcum duzenegi

`tools/tepe-egrisi/` — `FfmpegArguments.Build`'in urettigi **uretim** argumanlarini alir ve
yalniz `-maxrate`/`-bufsize` ciftini degistirir. Kabiliyet yoklamasi canli degil, sabit bir
tablodur: paylasimli makinede canli yoklama bir satirda HDR'i koruyup digerinde tonemap'e
dusuruyordu, yani satirlar arasinda boru hatti degisiyordu. HDR korunmazsa kosum durur.
Her satir `pixfmt`/`renk`/`akis` sutunlariyla kendi kapisini tasir.

Klipler: `.calisma/t108/kaynak/{durgun,hareketli}-20sn.mkv`, tam kaynagin 40-60 sn
(T105'in P4-durgun penceresi) ve 800-820 sn (P5-hareketli penceresi) araliklarindan,
`-c copy` ile. Ikisi de 1224 kare, 20,40 sn, 1920x1080, yuv420p10le, bt2020nc/smpte2084.

## libvmaf `log_path` — surucu harfi ffmpeg filtre grafigini kiriyor

Bu kusur once **bu is dalinda** cikti: duzenek `log_path`'i kacisli ama **tirnaksiz**
veriyordu, donanim izgarasi ilk VMAF'ta cikis 1 ile dustu. Uretim kodunun kalibi
(`QualityMeter.EscapeFilterPath`) tirnak **da** koyuyor ve o hal calisiyor.

### Kiran komut (tam)

```
ffmpeg -hide_banner -loglevel error -nostdin \
  -f lavfi -i testsrc2=size=64x64:rate=5:duration=1 \
  -f lavfi -i testsrc2=size=64x64:rate=5:duration=1 \
  -lavfi "[0:v][1:v]libvmaf=model=version=vmaf_v0.6.1neg:n_threads=1:log_fmt=json:log_path=C\:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/T108/.calisma/t108/logpath/b2.json" \
  -f null -
```

Cikis kodu 127. Tam `stderr`:

```
[AVFilterGraph @ 00000152c0f46d80] No option name near '/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/T108/.calisma/t108/logpath/b2.json'
[AVFilterGraph @ 00000152c0f46d80] Error parsing a filter description around:
[AVFilterGraph @ 00000152c0f46d80] Error parsing filterchain '[0:v][1:v]libvmaf=model=version=vmaf_v0.6.1neg:n_threads=1:log_fmt=json:log_path=C\:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/T108/.calisma/t108/logpath/b2.json' around:
Error : Invalid argument
```

### Duzelen komut (tam)

```
ffmpeg -hide_banner -loglevel error -nostdin \
  -f lavfi -i testsrc2=size=64x64:rate=5:duration=1 \
  -f lavfi -i testsrc2=size=64x64:rate=5:duration=1 \
  -lavfi "[0:v][1:v]libvmaf=model=version=vmaf_v0.6.1neg:n_threads=1:log_fmt=json:log_path='C\:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/T108/.calisma/t108/logpath/c2.json'" \
  -f null -
```

Cikis kodu 0, `c2.json` yazildi. Tek fark tirnak.

Duzenek bu yolu secmedi; `log_path`'i tumden iki nokta ustusuz birakti — ffmpeg VMAF
JSON'unun dizininde kosuyor, `log_path` yalniz dosya adi (`tools/tepe-egrisi/Program.cs`,
`Vmaf`). Uretimin kalibi zaten calisiyordu, ayni kalibi kopyalamak icin bir neden yoktu.

### Kirilmanin kosulu — olculdu

Ayni sentetik cifte 9 bicim kosuldu (ffmpeg 8.0, Windows 11, `libvmaf` 3.x):

| # | `log_path=` | tirnak | `:` kacisi | cikis | JSON |
|---|---|---|---|---|---|
| A | `a.json` | yok | - | 0 | yazildi |
| E | `alt dizin/e.json` | yok | - | 0 | yazildi |
| F | `'alt dizin/f.json'` | var | - | 0 | yazildi |
| H | `/Users/.../h.json` (surucu harfsiz mutlak) | yok | - | 0 | yazildi |
| D | `C:/.../d.json` | yok | yok | 127 | yok |
| G | `'C:/.../g.json'` | var | yok | 127 | yok |
| I | `'C:/.../alt dizin/i.json'` | var | yok | 127 | yok |
| B | `C\:/.../b2.json` | yok | var | 127 | yok |
| C | `'C\:/.../c2.json'` | var | var | 0 | yazildi |

Kosul **surucu harfinin iki nokta ustusudur**, mutlaklik degil ve bosluk degil:
- Mutlaklik degil: H mutlak ama surucu harfsiz, geciyor.
- Bosluk degil: E ve F boslukludur, ikisi de geciyor; I bosluklu **ve** iki nokta
  ustuslu, dusuyor — dusuren iki nokta.
- Iki noktanin gecmesi icin **hem kacis hem tirnak** gerekiyor: yalniz kacis (B) ve
  yalniz tirnak (G) ayri ayri dusuyor, ikisi birlikte (C) geciyor.

Baska surucu harfleri (D:, ag yolu `\sunucu\pay`), baska ffmpeg surumleri ve
`log_path` disindaki filtre secenekleri **olculmedi**.

### Uretim yolu icin not

`QualityMeter.EscapeFilterPath` tirnak + kacis uretiyor, yani yukaridaki C satiri: bu
bicim gecti. Bu olcum uretim yolunda bir kirilma **gostermiyor**. Ayri duran risk sudur:
filtre yine de kirilirsa `QualityMeter` `if (!File.Exists(logPath)) return null;` ile
sessizce `null` doner — kirilma hata olarak degil, "olcu yok" olarak gorunur. Bu satir
bu sozlesmenin `owns`'unda degil, olculmedi ve degistirilmedi.

## Sahne kurali ve bolen (T109 borcu) — olculdu

Uretimin kurdugu harita T109'dan beri **turetilen** olan: `SceneDetector.BuildMapAsync`
-> `SceneMap.BuildDerived`, kural `ThresholdRule.Measured`. Bolen bu kuralla yeniden
sayildi (`tools/tepe-egrisi sahne`, tam kaynak, el ile isaretlenmis
`tools/sahne-yer-gercegi/gercek-kesimler.txt` penceresi):

```
tarama: aday=10900 kare=62159 sure_sn=225,8
turetilen:      gercek=28 uretilen=28 yakalanan=28 kacan=0 yp=0 bolen=1,000
sabit-0.105:    gercek=28 uretilen=28 yakalanan=28 kacan=0 yp=0 bolen=1,000
turetilen harita: sahne=67 esik=NaN kural=var ust_sinir_sn=5,333
```

Bolen 1,000; esikten kurala gecmek boleni oynatmadi.

`SceneMapThresholdOfRecord` kaldirildi, yerine `SceneMapRuleOfRecord` geldi — **kuralin
kimligi**, uclari degil. Gerekce: turetilen haritada `Threshold` NaN, karar `Rule`'da;
yalniz Floor/Ceiling pinlenirse Offset, Slope, NeighbourhoodSeconds ve Percentile yesil
kalarak kayabilir ve dordu de bolusu degistirir. Tuzak sabit-sabit degil: kesim listesini
iki kez turetip karsilastiriyor, alanlari yer degistirmis bir kurali da yakalar.

Kuralin alti sayisindan **besi yuk tasiyor** (dusuk kipirtida Offset, Slope,
NeighbourhoodSeconds, Percentile; yuksek kipirtida Ceiling), her biri iki yonde de
bolusu degistiriyor. **Floor tasimiyor ve tasiyamaz**: kipirti negatif olmadigi ve Slope
pozitif oldugu icin `Offset + Slope * kipirti >= 0,08 > 0,05`, alt kiskaca ulasilmaz.
Esdeger mutasyon, test acigi degil.

NaN sizintisi arandi: uretim kodunda `SceneMap.Threshold`'u okuyan **yok** (yalniz
`SceneMapTests` ve olcum duzenegi). `Turetilen_haritanin_NaN_esigi_ust_sinira_sizmiyor`
bunu olcu olarak tutuyor.

## Sessiz cikisin koku — olculdu

Bir hucre (`hareketli-av1_nvenc-2gecis-4_636-t1_50`) stderr'i bos, hata metni olmadan,
"Press [q] to stop, [?] for help" satirindan sonra sifir disi kodla dustu. Uc aday vardi:
stdin EOF, nvenc es zamanli oturum tukenmesi, ve baska bir ajanin toplu `taskkill`i
(T107 bunu `_sorun.log`'a kendisi yazdi: uc surec oldurulmus, en az biri kendisinin degil).

Ucunun de imzasi ayri ayri olculdu. Duzenek: `tools/tepe-egrisi/nostdin-ayrim.sh`,
`nvenc-oturum.sh`, `oldurme-imzasi.sh`.

| Aday | Kosum | Cikis kodu | stderr imzasi |
|---|---:|---:|---|
| stdin EOF (`-nostdin` yok) | 12 | 0 (12/12) | tam, normal |
| `-nostdin` var (kontrol kolu) | 12 | 0 (12/12) | tam, normal |
| nvenc oturum tukenmesi | 14 es zamanli, 2 dustu | 127 | 2022 bayt, **gurultulu** |
| hedefli `taskkill /F /PID` | 3, 2'si kodlama ortasinda vuruldu | 1 | ilerleme satirinin ortasinda **kesiliyor**, hata metni yok |

nvenc tukenmesinin tam metni (`OpenEncodeSessionEx failed: incompatible client key (21)`
sonra `No capable devices found` sonra `Conversion failed!`, `frame= 0`): kodlayici
**acilmadan** dusuyor, tek kare uretmiyor ve bagirarak dusuyor. Gozlenen hucre ise
kodlayici acildiktan sonra, hata metni birakmadan sustu.

stdin EOF kolu, ayni firlatma kosullarinda (`-nostdin`li kolla es zamanli, arka plan
gorevinde stdin `/dev/null`) 12 kosumda hic dusmedi. Toplu bir dis oldurme iki kolu ayni
turda vururdu; hicbir turda iki kol birlikte dusmedi.

**Sonuc: disaridan oldurulduğu olculdu** — ama olcum imza eslesmesiyle, oldurmenin kendisi
gozlenerek degil. Iki ic aday olculdu ve imzalari gozlenen hucreye uymuyor; kesilen-stderr
+ hata-metni-yok imzasi yalniz hedefli oldurmede uretildi.

**Olculmedi:** dusen surecin kendi cikis kodu. Duzenek o zaman cikis kodunu kaydetmiyordu;
`FfKos` artik hata metnini `[cikis N]` on ekiyle yaziyor, yani ayni sey bir daha olursa
karar tek satirda verilir. Eski gunluk uzerine yazildigi ve T107'nin `_sorun.log` satiri
zaman damgasi tasimadigi icin o hucre icin geriye donuk cikis kodu **elde edilemez**.

Yan sonuc: `-nostdin` eklenince yeniden deneme sayisi 0'a dustu ama bu **`-nostdin`in
duzeltmesi degil** — ablasyonda `-nostdin`siz kol da 12/12 gecti. Neden bir daha olusmadi,
o kadar.

## K1 — oran x tepe izgarasi (donanim)

60 hucre: 2 kaynak x 2 kodlayici x 5 taban orani x 3 tepe carpani. Ham satirlar
`.calisma/t108/donanim/olcum.csv`; ozetleyici `tools/tepe-egrisi/ozet.py`.

**Payda tanimi.** Bu belgedeki `teslim/butce` sutunu **video butcesi**ne boler:
`bitrate_k x sure`. Kap payi ve ses yok (her satir tek video akisi). T98 ayni sayiyi
kullanicinin gordugu **hedef dosya boyutu**na bolmustu; iki tanim ayni sey degil ve
karsilastirilirken bu ayrim korunmali.

Boru hatti kapisi (pix_fmt, renk, akis sayisi) 60 satirin 60'inda gecti.
Tonemap'e dusen satir yok.

| kaynak | kodlayici | oran | p10 1,02 | p10 1,10 | p10 1,50 | p10 yayilim | yon | boyut tek yonlu mu | butceyi asan |
|---|---|---:|---:|---:|---:|---:|---|---|---:|
| durgun | av1_nvenc | 2,600 | 90,874 | 91,349 | 90,836 | 0,513 | yon yok | HAYIR | 1/3 |
| durgun | av1_nvenc | 4,636 | 93,519 | 93,283 | 93,289 | 0,236 | yon yok | HAYIR | 0/3 |
| durgun | av1_nvenc | 7,500 | 94,782 | 94,573 | 94,616 | 0,209 | yon yok | HAYIR | 1/3 |
| durgun | av1_nvenc | 10,236 | 95,229 | 95,021 | 95,073 | 0,208 | yon yok | HAYIR | 0/3 |
| durgun | av1_nvenc | 16,000 | 95,633 | 95,650 | 95,704 | 0,071 | tek yonlu artan | evet | 0/3 |
| durgun | hevc_nvenc | 2,600 | 86,085 | 86,026 | 87,636 | 1,611 | yon yok | evet | 2/3 |
| durgun | hevc_nvenc | 4,636 | 90,937 | 91,659 | 91,888 | 0,951 | tek yonlu artan | HAYIR | 1/3 |
| durgun | hevc_nvenc | 7,500 | 93,071 | 93,713 | 93,958 | 0,888 | tek yonlu artan | HAYIR | 0/3 |
| durgun | hevc_nvenc | 10,236 | 94,564 | 94,560 | 94,683 | 0,123 | yon yok | HAYIR | 0/3 |
| durgun | hevc_nvenc | 16,000 | 95,025 | 95,147 | 95,374 | 0,349 | tek yonlu artan | evet | 0/3 |
| hareketli | av1_nvenc | 2,600 | 31,299 | 32,615 | 42,761 | **11,462** | tek yonlu artan | HAYIR | 3/3 |
| hareketli | av1_nvenc | 4,636 | 50,025 | 56,141 | 57,410 | **7,385** | tek yonlu artan | HAYIR | 3/3 |
| hareketli | av1_nvenc | 7,500 | 63,366 | 66,899 | 67,499 | 4,133 | tek yonlu artan | HAYIR | 3/3 |
| hareketli | av1_nvenc | 10,236 | 69,723 | 72,372 | 72,297 | 2,649 | yon yok | HAYIR | 3/3 |
| hareketli | av1_nvenc | 16,000 | 78,250 | 78,475 | 78,525 | 0,275 | tek yonlu artan | evet | 2/3 |
| hareketli | hevc_nvenc | 2,600 | 27,454 | 29,124 | 39,129 | **11,675** | tek yonlu artan | HAYIR | 3/3 |
| hareketli | hevc_nvenc | 4,636 | 46,751 | 51,221 | 53,069 | **6,318** | tek yonlu artan | evet | 3/3 |
| hareketli | hevc_nvenc | 7,500 | 60,236 | 62,659 | 63,617 | 3,381 | tek yonlu artan | evet | 3/3 |
| hareketli | hevc_nvenc | 10,236 | 67,996 | 69,702 | 69,616 | 1,706 | yon yok | evet | 3/3 |
| hareketli | hevc_nvenc | 16,000 | 76,091 | 76,588 | 76,404 | 0,496 | yon yok | evet | 3/3 |

Sure sutunu bu izgarada **makine paylasimliydi** damgasi tasir (on dort ajan ayni makinede).
Kalite ve boyut sayilari yuke bagli degil; onlara damga basilmadi.

## K2 — kodlayici basina egri

Iki donanim kodlayicisi ayni sekli veriyor. Hareketli kaynakta p10 yayilimi taban orani
buyudukce sonuyor: av1_nvenc 11,462 sonra 0,275, hevc_nvenc 11,675 sonra 0,496.
Durgun kaynakta her iki kodlayicida da yayilim her oranda 1,611'in altinda.

Yazilim kodlayici blogu (libx265, medium) **kosuyor**; bu bolum tamamlanmadi.
Simdiye kadar tek tam satir grubu (hareketli, oran 4,636): p10 50,615 / 50,456 / 50,226,
yayilim 0,389, yon yok; teslim/butce 0,9869 / 0,9886 / 0,9886. Yani yazilim yolunda tepe
carpanini acmak ne kaliteyi ne boyutu oynatiyor. Tek grup, genellenmedi.

## K3 — iki kaynak, T98'in +3,665'i

T98'in bulgusu: 4,636 oraninda tepeyi acmak p10'u 69,812 sonra 71,241 sonra 73,477
tasiyor, toplam **+3,665**; 10,236 oraninda siralama kayboluyor.

- **Hareketli kaynakta dogrulandi ve asildi.** av1_nvenc 4,636: +7,385 (50,025 sonra
  56,141 sonra 57,410), uc noktada tek yonlu. hevc_nvenc ayni oranda +6,318. 10,236
  oraninda iki kodlayicida da yon kayboluyor (av1 2,649, hevc 1,706) — T98'in ikinci
  cumlesi de dogrulandi.
- **Durgun kaynakta dogrulanmadi.** av1_nvenc 4,636: yayilim 0,236 ve yon yok.
  hevc_nvenc 4,636: +0,951, tek yonlu ama T98'in olcusunun dortte biri.

Yani kazanci belirleyen sey taban orani degil **kaynagin hareketliligi**. Ayni oranda ayni
kodlayiciyla iki kaynak arasindaki fark 7,385'e karsi 0,236 — otuz kat.

### T98'in klibi kimliklenemedi

Parmak izi denemesi: T98'in 4,636/1,02 hucresi 8,895 MiB veriyor. Ayni duzenekle uc
kaynagin ilk 20 sn'si (`.calisma/t108/t98/olcum.csv`):

| Klip | teslim MiB (4,636/1,02) | p10 |
|---|---:|---:|
| `parca-1` | 8,8976 | 40,609 |
| `parca-2` | 7,7357 | 95,412 |
| `parca-3` | 8,9809 | 52,734 |

Boyut `parca-1`i gosteriyor (fark 0,0026 MiB), ama ayni hucrenin p10'u 40,609 ve T98'in
69,812'siyle 29 puan ayriliyor. Ustelik 3686k'da 20 sn'lik iki gecisli bir kodlama her
kaynakta yaklasik 8,9 MiB verir, yani **boyut zayif bir ayirt edicidir**; `parca-3` de
0,086 MiB icinde. Kalite sayisi hicbir klipte tutmadigi icin **T98'in klibi
kimliklenemedi**.

`parca-1` uzerinde tam ucluyu de kosturdum (4,636'da p10 40,609 / 46,385 / 48,266):
**yon ve buyukluk T98'inkiyle uyusuyor** (+7,657, tek yonlu), yalnizca seviye uyusmuyor.
Ayni nominal kodlamanin teslim boyutu tutup kalite seviyesi 29 puan ayrilmasi, farkin
kodlamada degil **kalite olcumunde** oldugunu soyler; hangi olcum farki oldugu
**olculmedi** (T98 `tools/VidShrink.Bench measure` kullanmis, ben dogrudan libvmaf).

Sure varsayimi kapandi: `parca-1` 1200 kare / 20,000 sn, `parca-2` 1199 / 20,002 sn,
`parca-3` 1199 / 19,999 sn. 20,401 sn bu klip ailesinde yok (o benim kendi kliplerimin
suresi). T98'in 4,636 hucresindeki video butcesi bu sureyle 8,7897 MiB.

T98'in satirlari **benim paydamla** (video butcesi = bitrate_k x 20,000 sn) yeniden
hesaplanmis hali:

| Taban orani | Tepe | T98 teslim MiB | T98'in paydasi | benim paydam |
|---:|---:|---:|---:|---:|
| 4,636 | 1,02 | 8,895 | 0,9669 | **1,0120** |
| 4,636 | 1,10 | 8,594 | 0,9341 | 0,9778 |
| 4,636 | 1,50 | 8,723 | 0,9482 | 0,9924 |
| 10,236 | 1,02 | 19,637 | 0,9818 | **1,0118** |
| 10,236 | 1,10 | 18,902 | 0,9451 | 0,9740 |
| 10,236 | 1,50 | 19,128 | 0,9564 | 0,9856 |

T98'in "uc degerin ucu de hedefin altinda" cumlesi kendi paydasinda dogru; video
butcesine bolununce alti hucrenin **ikisi** asiyor ve ikisi de en dar tepede (1,02).
Bu bir tanim farki, T98'in cumlesinin curutulmesi degil.

### Yon tersine donuyor: hareketli kaynakta kazanc dusuk oranda en buyuk

Bugunku egri tepeyi taban orani 6,0'in altinda **dar** tutuyor ve 11,4'e dogru aciyor.
Olculen sekil bunun tersi: hareketli kaynakta kazanc 2,600 oraninda en buyuk (11,462) ve
16,000'de kayboluyor (0,275). Iki kodlayicida da ayni.

Egrinin gerekcesindeki "yuksek oranda dar tepe hedefin altinda kaliyor" onculu bu iki
kaynakta **tekrarlanmadi**: 10,236 oraninda dar tepe hareketli kaynakta 1,0330 ile
asiyor, durgun kaynakta 0,9809 ile hedefe en yakin degeri veriyor.

Dar tepenin boyut faydasi yalnizca en dusuk oranda gorunuyor: 2,600'de hareketli/av1
1,0351 (dar) karsisinda 1,1116 (1,10) ve 1,0800 (1,50). 4,636'da fark kayboluyor
(1,0305 / 1,0305 / 1,0380) — yani orada tepeyi acmak boyutta yuzde 0,7'ye mal olup p10'da
7,385 kazandiriyor.

## K4 — boyut asimi, hucre basina sayildi

Gozlemden degil, sayimla: **60 hucrenin 34'u** video butcesini asiyor.
En buyuk asim **1,1116** — hareketli, av1_nvenc, oran 2,600, tepe 1,10.
Asim her taban oraninda goruluyor (2,600 / 4,636 / 7,500 / 10,236 / 16,000).

Kaynak kirilimi: hareketli 30/30, durgun 4/30. Yani asim da kaynagin hareketliligine
bagli.

Boyutun tepeyle **tek yonlu gitmedigi** satir sayisi: 20 satirin **12'si**. Tepe carpanini
buyutmek dosyayi tek yonlu buyutmuyor.

"Uc degerin ucu de hedefin altinda" turu bir cumle bu izgaradan yazilamaz.
