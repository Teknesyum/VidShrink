# Tepe-tavan egrisi (T108)

Durum: izgaralar kosuyor. K1-K5 tablolari henuz **olculmedi**; bu dosyada su an yalniz
olculmus olanlar var.

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

## Henuz olculmemis olanlar

K1 (oran x tepe izgarasi), K2 (kodlayici basina egri), K3 (T98'in +3,665'inin
dogrulanmasi), K4 (boyut asimi sayimi), K5 (CRF yolunda VBV ara degerleri):
**olculmedi**, izgaralar kosuyor.
