# Max Sikistirma Modu — Acilis Olcumu (T193)

Bu sayfa **tumuyle** `.calisma/T193/` altindaki olculen dosyalardan
uretilir (`tools/sahne-butcesi/09-max-mod-raporu.py`). Elle yazilan sayi yoktur.

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
| kontrol | lp=6 x4 (min) | lp=6 x4 (max) | 6088047 | 6101470 | 13423 | — | — | — | — |
| `qcomp` | qcomp=0.40 | qcomp=0.75 | 6099506 | 6084174 | 15332 | 13423 | 26846 | 60995 | **hayir** |
| `qp-scale-compress-strength` | =0 | =3 | 6094997 | 6257700 | 162703 | 13423 | 26846 | 60949 | **evet** |
| `zones` | b=2.00 | b=0.50 | 6091965 | 6091235 | 730 | 13423 | 26846 | 60919 | **hayir** |

Tekrar gurultusu **dort** ayni-parametre kosumunun araligidir: 13423 bayt
(kosum baytlari: 6092918, 6093374, 6101470, 6088047).
Ayni dort kosumun ilk **cifti** 456 bayt veriyor — gercek araligin
29 kati kucuk. T114 gurultuyu tek ciftten oluyordu;
bu, sinirdaki bir adayi yanlislikla gecirebilirdi.

**Ucuncu soru — `qcomp` varsayilan yolda gorunuyor mu: hayir.** ffmpeg anahtari
ayristiramiyor (`[libsvtav1] Error parsing option qcomp: 0.40.`) ama **cikis kodu 0**
donuyor. Iki deger arasindaki 15332 baytlik fark tekrar
gurultusunun iki katinin (26846 bayt) altinda. Kapi
**desteklenmiyor** diyor; bu yuzden K2'nin geri kalani — deger taramasi —
**kosulmadi**.

Ayni kapida iki yan sonuc: `zones` da `libsvtav1`de olu (730 bayt fark,
gurultunun altinda) — T114'un bulgusu bagimsiz dogrulandi. Buna karsilik SVT-AV1'in
kendi karsiligi `qp-scale-compress-strength` **calisiyor** (162703 bayt fark).

T114 izgarasinin `libsvtav1 / qcomp` satiri zaten `qcomp` degil
`qp-scale-compress-strength` deniyordu; "qcomp `libsvtav1`'de calisiyor" cumlesi
literal anahtarin olcumu hic olmadi.

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

Kazanc = `dagitim - taban`, VMAF-NEG puani:

| Pencere | p10 kazanci | En kotu sahne kazanci | Ort kazanc | Taban bayt | Dagitim bayt | Bayt farki |
|---------|-------------|-----------------------|------------|------------|--------------|------------|
| `p1-karisik` | -0,019 | -0,161 | -0,001 | 62221002 | 62209607 | 11395 |
| `p2-durgun` | -0,005 | -0,012 | -0,011 | 11569906 | 11376078 | 193828 |
| `p3-hareketli` | -0,021 | -0,119 | -0,012 | 61808654 | 61817030 | 8376 |

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

**K5 kapisi: gecmedi.**

Sebep: `libsvtav1` `zones` anahtarini yok sayiyor, yani `dagitim` kolu `taban` ile
ayni kodlamadir. Kolun p10 kazanci uc pencerede de **sifirin altinda**; mutlak
degerlerin en buyugu **0,021 puan**, esik +0,50.

Bayt tarafinda ayrimi tek cumleye sigdirmamak gerekiyor. Olculen tekrar gurultusu
13423 bayt, ama o gurultu `p1`in bit hizinda (`8230k`, 6 sn) olculdu:

- Gurultunun **altinda** kalan pencere: `p1-karisik`, `p3-hareketli`.
- Gurultunun **ustunde** kalan pencere: `p2-durgun`.

Ustte kalan pencerede tekrar gurultusu **kendi bit hizinda olculmedi**, o yuzden
"gurultunun icinde" denemez. Ama o pencerede de VMAF kazanci sifirin altinda
(`p2-durgun` p10 -0,005),
yani bayt farki kaliteye kazanc olarak donmemistir.

Bu bir "az kazandi" degil, **tasiyicinin yoklugudur**.

## K3 — Sure Sayilari

Bu olcum ayni makinede es zamanli baska islerle birlikte kosuldu. ffmpeg cagrilari
kendi icinde **sirayla** kosuldu; duzenegin paralel kolu (eski
`00-pencereleri-kes.sh` icindeki `&`/`wait`) kaldirildi.

Kalite (VMAF) ve bayt sayilari islemci rekabetinden etkilenmez ve bu sayfadaki
butun hukumler yalniz onlara dayanir. **Sure sayisi bu sayfada hic raporlanmamistir**
— es zamanlilik yuzunden guvenilmez olurdu.

## Olculmeyenler

- `qcomp` deger taramasi (`0,40 / 0,50 / 0,60 / 0,75`): kosulmadi, cunku destek
  kapisi `libsvtav1` icin **hayir** dedi. `uyumlu` (`libx264`) ve `yedek` (`libx265`)
  kollarinda `qcomp` calisiyor, ama uretimin varsayilani o kollar degil.
- `qp-scale-compress-strength` taramasi: bu sozlesmenin sorusu degil. Destek
  kapisindan **evet** ile ciktigi olculdu; kalite ve boyut etkisi olculmedi.
- K7 (bozuk harita bedeli) `maks` kolunda kosulmadi: K5 kazanci sifir oldugu icin
  karsilastirilacak kazanc yok.
- T114 ile sayisal kiyas: pencere suresi ve plan cozunurlugu farkli.
- Sure / duvar saati: bkz. K3.

## Hukum

**Max sikistirma modu acilmaz**: `maks` kolunun kalite kapisi bu depoda ilk kez
kosuldu ve p10 esigini gecen pencere 0/3, en kotu sahne esigini gecen pencere 0/3 cikti —
cunku uretimin varsayilan kodlayicisi `libsvtav1` hem dagitimi tasiyacak anahtari
(`zones`, fark 730 bayt) hem ayakta kalan tek isareti
(`qcomp`, fark 15332 bayt) destek esiginin (gurultu x 2 = 26846 bayt) altinda birakiyor.
