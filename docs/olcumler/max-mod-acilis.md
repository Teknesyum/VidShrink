# Max Sikistirma Modu — Acilis Olcumu (T193)

Bu sayfanin **her olcum sayisi** `.calisma/T193/` altindaki dosyalardan
okunur (`tools/sahne-butcesi/09-max-mod-raporu.py`); hicbiri elle tasinmaz. Govde
cumleleri, esik sabitleri (`+0,50` / `+1,00` / `0,30`) ve duzenek tarifleri ureticin
kendi metnindedir — onlar olcum degil, olcumun cercevesidir.

ffmpeg: `ffmpeg version 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers`

## K0 — Kaynak Sapmasi

T114'un `kaynak-1080p60-hdr-17dk-yalniz-video.mkv` dosyasi bu makinede **yok**
(`VIDSHRINK_KAYNAK`, butun surucu harfleri ve `.calisma` disi klasorler arandi;
`C:/Users` altinda 400 MB ustu tek bir video dosyasi bulunamadi). Pencereler elde
olan uc 60 sn'lik 1080p60 HDR parcadan uretildi.

| Pencere | Kaynak | Sure (sn) | Harita sahnesi |
|---------|--------|-----------|----------------|
| `p1-karisik` | parca-1 0-60 sn (T193 sapmasi: T114'un 189 sn'lik penceresi degil) | 60,0 | 8 |
| `p2-durgun` | parca-2 0-60 sn (T193 sapmasi: T114'un 186 sn'lik penceresi degil) | 60,0 | 3 |
| `p3-hareketli` | parca-3 0-60 sn (T193 sapmasi: T114'un 189 sn'lik penceresi degil) | 60,0 | 2 |

**Sapma.** Pencereler ~189 sn yerine ~60 sn. Hedef boyut (60 MB) degismedigi icin
bit hizi ucuyor. T114'te `p1` plani `806x454`e dusuyordu; bu kosumun `maks` planlari:

| Pencere | Kodlayici | Mod | Bit hizi | Plan cozunurlugu |
|---------|-----------|-----|----------|------------------|
| `p1-karisik` | `libsvtav1` | 2pass | 8230k | 1920x1080 |
| `p2-durgun` | `libsvtav1` | 2pass | 1823k | 1920x1080 |
| `p3-hareketli` | `libsvtav1` | 2pass | 8230k | 1920x1080 |

Bu kosumun sayilari T114'un hucreleriyle **dogrudan karsilastirilamaz**; taban bu
kosumda yeniden olculdu.

## K2 — `libsvtav1` Destek Kapisi

`08-svtav1-kapisi.sh`: 6 sn'lik pencere, iki gecis, `preset 6`, `-b:v 8230k`.
Destek ancak `fark > gurultu x 2` **ve** `fark > cikti/100` iken yazilir.

| Anahtar | A | B | A bayt | B bayt | Fark | Gurultu | Gurultu x2 | Cikti/100 | Destek |
|---------|---|---|--------|--------|------|---------|------------|-----------|--------|
| kontrol | lp=6 x8 (min) | lp=6 x8 (max) | 6085018 | 6118325 | 33307 | — | — | — | — |
| `qcomp` | -svtav1-params qcomp=0.40 | -svtav1-params qcomp=0.75 | 6101726 | 6097414 | 4312 | 33307 | 66614 | 61017 | **hayir** |
| `qcomp-ffmpeg` | -qcomp 0.40 | -qcomp 0.95 | 6075743 | 6098429 | 22686 | 33307 | 66614 | 60757 | **hayir** |
| `qp-scale-compress-strength` | =0 | =3 | 6091645 | 6267380 | 175735 | 33307 | 66614 | 60916 | **evet** |
| `zones` | b=2.00 | b=0.50 | 6090445 | 6098105 | 7660 | 33307 | 66614 | 60904 | **hayir** |

### Tekrar Gurultusu — Kac Kosumdan Olculdugu Kararin Kendisidir

Tekrar gurultusu **8** ayni-parametre kosumunun araligidir: 33307 bayt
(kosum baytlari: 6092918, 6093374, 6101470, 6088047, 6085018, 6112410, 6097190, 6118325).
Kosum 1-4 yapicinin, 5-8 denetcinin ayni duzenekteki bagimsiz kosumudur
(`.calisma/T193/k2kapi/KAYNAK.txt`).

Daha az kosum **gurultuyu oldugundan kucuk gosteriyor**:

| Kosum sayisi | Aralik (bayt) | Sekiz kosumun araligina orani |
|--------------|---------------|-------------------------------|
| 2 (ilk cift) | 456 | 73,0 kat kucuk |
| 4 (yapicinin ilk kosumu) | 13423 | 2,5 kat kucuk |
| 8 (bu sayfanin tabani) | 33307 | — |

T114 gurultuyu **tek ciftten** oluyordu; T193'un ilk kosumu **dort kosumdan**.
Ikisi de sinirdaki bir adayi yanlislikla gecirebilir. Bu sayfanin butun destek
kararlari 8 kosumun araligina (33307 bayt) ve esigine (66614 bayt) gore verildi.

**Ucuncu soru — `qcomp` varsayilan yolda gorunuyor mu: hayir.** Iki ad alani da
denendi: `-svtav1-params qcomp=...` ve ffmpeg'in kendi `-qcomp` secenegi.
Ikisinde de fark tekrar gurultusunun iki katinin (66614 bayt) altinda:
`-svtav1-params` yolunda 4312 bayt, `-qcomp` yolunda 22686 bayt.
Kapi iki yolda da **desteklenmiyor** diyor; bu yuzden K2'nin geri kalani — deger
taramasi — **kosulmadi**.

Nitel yari da ham dosyada duruyor. `08-svtav1-kapisi.sh` her kodlamanin stderr'ini
`.calisma/T193/k2kapi/<ad>.p1.err` ve `<ad>.p2.err` olarak saklar;
`Error parsing option` dizgesi 8 dosyada, toplam 8 kez gecer:

```
qcomp-a.p1.err: [libsvtav1 @ 00000148d5c50b00] Error parsing option qcomp: 0.40.
zones-a.p1.err: [libsvtav1 @ 000001757d807940] Error parsing option zones: 0,179,b=2.00.
```

Kodlama yine de **cikis kodu 0** donuyor: betik `set -euo pipefail` ile kosuyor,
sifirdan farkli bir kod donseydi cikti dosyasi hic olusmazdi. Anahtarin
ayristirilamadigi ffmpeg'in kendi hata satirinda yazili; kabuk bunu gormuyor.

Ayni kapida iki yan sonuc: `zones` da `libsvtav1`de olu (7660 bayt fark,
gurultunun altinda) — T114'un bulgusu bagimsiz dogrulandi. Buna karsilik SVT-AV1'in
kendi karsiligi `qp-scale-compress-strength` **calisiyor** (175735 bayt fark);
o kosumlarin stderr'inde `Error parsing option` yok, SVT'nin kendi yapilandirma dokumu
`QP scale compress strength` degerini iki kosumda `0` ve `3` olarak basiyor.

T114 izgarasinin `libsvtav1 / qcomp` satiri zaten `qcomp` degil
`qp-scale-compress-strength` deniyordu; bu yuzden T114'un qcomp cumlesi literal
anahtarin olcumu degildi.

## K1 — `maks` Kolunun Kalite Kapisi (Ilk Kez Kosuldu)

T114'te `maks` kolunun K5 hucreleri hic kosulmamisti: kapi `ZonesFlag` uzerinden
kuruluydu, `libsvtav1` icin `null` donuyor ve kol sessizce atlaniyordu. Kapi
`ParamsFlag`e cevrildi (`-svtav1-params`) ve uc pencerede de kosuldu.

| Pencere | Kol | Boyut (MB) | Band (MB) | Band ici | VMAF-NEG ort | p10 | min | En kotu sahne |
|---------|-----|------------|-----------|----------|--------------|-----|-----|---------------|
| `p1-karisik` | taban | 59,34 | 58,3–60,0 | evet | 91,203 | 89,083 | 79,146 | 89,035 |
| `p1-karisik` | dagitim | 59,33 | 58,3–60,0 | evet | 91,202 | 89,064 | 79,302 | 88,874 |
| `p2-durgun` | taban | 11,03 | 58,3–60,0 | **hayir** | 95,929 | 95,323 | 93,980 | 95,317 |
| `p2-durgun` | dagitim | 10,85 | 58,3–60,0 | **hayir** | 95,918 | 95,318 | 94,006 | 95,305 |
| `p3-hareketli` | taban | 58,95 | 58,3–60,0 | evet | 86,107 | 81,776 | 75,701 | 82,794 |
| `p3-hareketli` | dagitim | 58,95 | 58,3–60,0 | evet | 86,095 | 81,755 | 76,048 | 82,674 |

Kazanc = `dagitim - taban`, VMAF-NEG puani. Bayt farki da **isaretli**: eksi isaret
dagitim kolunun daha kucuk ciktigi anlamina gelir.

| Pencere | p10 kazanci | En kotu sahne kazanci | Ort kazanc | Taban bayt | Dagitim bayt | Bayt farki (dagitim - taban) | Bagil fark |
|---------|-------------|-----------------------|------------|------------|--------------|------------------------------|------------|
| `p1-karisik` | -0,019 | -0,161 | -0,001 | 62221002 | 62209607 | -11395 | %0,018 |
| `p2-durgun` | -0,005 | -0,012 | -0,011 | 11569906 | 11376078 | -193828 | %1,675 |
| `p3-hareketli` | -0,021 | -0,119 | -0,012 | 61808654 | 61817030 | +8376 | %0,014 |

Isaretler **karisik** — iki pencerede dagitim kolu kucuk, birinde buyuk. Tek yonlu
bir etki degil, gurultu okumasidir.

### K5 Kapisinin Sayimi

`ESIKLER.md`'nin dort sarti, `maks` kolu icinde, olculen 3 pencere uzerinden:

1. p10 kazanci >= +0,50, en az iki pencerede — **gecen pencere: 0/3** (yok) -> **saglanmadi**
2. En kotu sahne kazanci >= +1,00, ayni pencerelerde — **gecen pencere: 0/3** (yok) -> **saglanmadi**
3. Hicbir pencerede p10 kaybi > 0,30 — **asan pencere: 0** -> saglandi
4. K6: her kosum hedef bandin icinde — **band disi kosum: 2** -> **saglanmadi**

Band disi 2 kosumun hepsi `p2-durgun` penceresinde, ve o pencerede
**her iki kol da** band disinda. Sart 4 bu yuzden kollari ayirt etmez.

Sebep plan seviyesinde: `p2-durgun` icin plan bit hizi 1823k, diger pencerelerde 8230k.
60 sn'lik pencerede 1823k hedef bandin (58,3–60,0 MB) altinda kalir; olculen 11,03 MB.

Kollari ayirt eden ve karari veren sayi sart 1 ve 2'dir.

**Bant disi pencerenin kalite satiri nitelikli kanit degildir.** `ESIKLER.md`'nin
4. sarti kaliteyi **esit boyda** karsilastirmak icin var; iki kolu da bant disinda
olan pencerede o esitlik yok. Olculen bagil boy farklari:

- `p1-karisik`: %0,018
- `p2-durgun`: %1,675 — iki kol da bant disi, **boy eslenmemis**
- `p3-hareketli`: %0,014

Bu yuzden sayim iki turlu okunur: uc pencere uzerinden **0/3**, boy eslenmis
(iki kolu da bant icinde) pencereler uzerinden **0/2**.
Hukum ikisinde de ayni.

**K5 kapisi: gecmedi.**

Sebep: `libsvtav1` `zones` anahtarini yok sayiyor, yani `dagitim` kolu `taban` ile
ayni kodlamadir. Kolun p10 kazanci uc pencerede de **sifirin altinda**; mutlak
degerlerin en buyugu **0,021 puan**, esik +0,50.

Bayt tarafinda ayrimi tek cumleye sigdirmamak gerekiyor. Olculen tekrar gurultusu
33307 bayt, ama o gurultu `p1`in bit hizinda (`8230k`) ve **6 sn'lik** bir
ciktida olculdu; asagida karsilastirilan pencereler ise **60 sn**. Olcekler bire bir
degil: 6 sn'lik cikti 60 sn'lik ciktinin gurultusunu **kucuk gosterir**, yani bu
ayrim muhafazakar yondedir.

- 6 sn'lik gurultunun **altinda** kalan pencere: `p1-karisik`, `p3-hareketli`.
- 6 sn'lik gurultunun **ustunde** kalan pencere: `p2-durgun`.

Ustte kalan pencerede tekrar gurultusu kendi bit hizinda ve kendi suresinde
olculmedi; o pencere icin gurultunun icinde ya da disinda demek olculmus bir
iddia olmaz. Ama o pencerede de VMAF kazanci sifirin altinda (`p2-durgun` p10 -0,005),
yani bayt farki kaliteye kazanc olarak donmemistir.

Bu bir az kazandi durumu degil, **tasiyicinin yoklugudur**.

## K12 — `qp-scale-compress-strength` Kalite Olcumu

Destek kapisindan **evet** ile cikan tek anahtarin kalite kazanci olculdu.
`dagitim` kolu `zones` yerine `-svtav1-params qp-scale-compress-strength=3` ile
kuruldu; `taban` kol K5'in taban ciktisidir (SVT varsayilani `=0`). Deger ikilisi
kapida olculen ikilidir, tarama yok. `p2-durgun` disarida: iki kolu da bant disi
oldugu icin kalite satiri boy eslenmemis olurdu.

| Pencere | Kol | Boyut (MB) | Band (MB) | Band ici | VMAF-NEG ort | p10 | min | En kotu sahne |
|---------|-----|------------|-----------|----------|--------------|-----|-----|---------------|
| `p1-karisik` | taban | 59,34 | 58,3–60,0 | evet | 91,203 | 89,083 | 79,146 | 89,035 |
| `p1-karisik` | dagitim | 61,50 | 58,3–60,0 | **hayir** | 91,198 | 89,187 | 78,821 | 89,574 |
| `p3-hareketli` | taban | 58,95 | 58,3–60,0 | evet | 86,107 | 81,776 | 75,701 | 82,794 |
| `p3-hareketli` | dagitim | 59,08 | 58,3–60,0 | evet | 86,559 | 82,247 | 77,214 | 82,545 |

| Pencere | p10 kazanci | En kotu sahne kazanci | Ort kazanc | Taban bayt | Dagitim bayt | Bayt farki (dagitim - taban) |
|---------|-------------|-----------------------|------------|------------|--------------|------------------------------|
| `p1-karisik` | +0,104 | +0,539 | -0,005 | 62221002 | 64485791 | +2264789 |
| `p3-hareketli` | +0,471 | -0,248 | +0,452 | 61808654 | 61947904 | +139250 |

Ayni dort sart, olculen 2 pencere uzerinden:

1. p10 kazanci >= +0,50 — **gecen pencere: 0/2** (yok)
2. En kotu sahne kazanci >= +1,00 — **gecen pencere: 0/2** (yok)
3. Hicbir pencerede p10 kaybi > 0,30 — **asan pencere: 0**
4. K6: her kosum hedef bandin icinde — **band disi kosum: 1** (`p1-karisik`/dagitim)

**Sonuc: belirsiz.** Bant disina cikan kosum var, yani iki kol **esit boyda
karsilastirilmadi**; bu haliyle kalite farki kazanc olarak okunamaz.
`p1-karisik`/dagitim: 61,50 MB, band 58,3–60,0 MB.

Anahtar calisiyor — bayt farki bunu soyluyor — ama `=3` ile cikan dosya hedef
bandi asiyor: kalite kazanci sorusu once bir hedef boyut sorusudur. Uydurma
yapilmaz; mod bu anahtar icin ne acilir ne kapanir.

## K3 — Sure Sayilari

Bu olcum ayni makinede es zamanli baska islerle birlikte kosuldu. ffmpeg cagrilari
kendi icinde **sirayla** kosuldu; duzenegin paralel kolu (eski
`00-pencereleri-kes.sh` icindeki `&`/`wait`) kaldirildi.

Kalite (VMAF) ve bayt sayilari islemci rekabetinden etkilenmez ve bu sayfadaki
butun hukumler yalniz onlara dayanir. **Sure sayisi bu sayfada hic raporlanmamistir**
— es zamanlilik yuzunden guvenilmez olurdu.

## Olculmeyenler

- `qcomp` deger taramasi (`0,40 / 0,50 / 0,60 / 0,75`): kosulmadi, cunku destek
  kapisi `libsvtav1` icin her iki ad alaninda da **hayir** dedi. `uyumlu` (`libx264`)
  ve `yedek` (`libx265`) kollarinda `qcomp` calisiyor, ama uretimin varsayilani o
  kollar degil.
- `qp-scale-compress-strength` **taramasi**: yalniz kapida olculen `0` / `3` ikilisi
  denendi; ara degerler ve `variance-boost` ailesi olculmedi.
- **Bit kitliginin oldugu dusuk cozunurluklu plan rejimi bu kosumda olculmedi**: uc
  pencere de `1920x1080` planlandi, oysa T114'un 189 sn'lik `p1`i `806x454`e
  dusuyordu. Sahne basina bit dagitiminin en cok ise yarayacagi rejim budur ve bu
  sayfada yok. Hukum kazanc sayisina degil anahtar destegine dayandigi icin bu bosluk
  hukmu cevirmez; yine de olculmemistir.
- K7 (bozuk harita bedeli) `maks` kolunda kosulmadi: K5 kazanci sifir oldugu icin
  karsilastirilacak kazanc yok.
- T114 ile sayisal kiyas: pencere suresi ve plan cozunurlugu farkli.
- Sure / duvar saati: bkz. K3.

## Hukum

**Max sikistirma modu sahne dagitimi uzerine acilmaz**: `maks` kolunun kalite
kapisi bu depoda ilk kez kosuldu ve p10 esigini gecen pencere 0/3, en kotu sahne esigini gecen pencere 0/3 cikti —
cunku uretimin varsayilan kodlayicisi `libsvtav1` hem dagitimi tasiyacak anahtari
(`zones`, fark 7660 bayt) hem ayakta kalan tek isareti
(`qcomp`, `-svtav1-params` yolunda 4312 bayt, `-qcomp` yolunda 22686 bayt) destek esiginin (gurultu x 2 = 66614 bayt) altinda birakiyor.

Ayakta kalan tek anahtar `qp-scale-compress-strength` icin sonuc **belirsiz**:
kalite olculdu ama `=3` kolu hedef bandin disina cikti (`p1-karisik`/dagitim), yani iki kol esit boyda degil.
Bu anahtar icin mod ne acilir ne kapanir; onunde duran soru kalite degil hedef
boyuttur.
