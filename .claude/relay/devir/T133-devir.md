sozlesme: T133
dal: T133-anahtar-kare-tavani
son-commit: af75556

## Nerede kaldim

- **K1 (izgara)** — HIC KOSULMADI. Duzenek hazir ve sinandi, kaynak yok.
- **K2 (atlama maliyeti + tavanin gerekcesi)** — GECTI. Gerekce sorusu cevaplandi:
  olculmus, varsayim degil. Kendi atlama tablom kosulmadi (kaynak yok), ama koddaki
  olculmus atlama sayilari bulundu ve rapora gecti.
- **K3 (haritanin kolu izgaranin neresinde)** — HIC BASLANMADI, K1'e bagli.
- **K4 (esik olcumden once)** — GECTI. Commit `1e541b1`, `2026-09-02 23:29:26 +0300`,
  itilmis. Hicbir olcum sonucu gorulmeden yazildi.
- **K5 (recete)** — HIC BASLANMADI, K1+K3'e bagli.
- **K6 (donanim kolu)** — GECTI. Karar: kapsam disi, uc yazili gerekceyle.

## Olctugum sayilar

Hepsi bu makinede, `ffmpeg version 9.0-full_build-www.gyan.dev`.

### Duzenek duman testi — K1 VERISI DEGIL

Kaynak: `.calisma/t57/hareketli-1080p30-120s.mp4`'ten 8 sn kesit, libx264 crf 14.
Bu benim urettigim yerel kesit; sozlesmenin dort kaynagi DEGIL. Tek amaci
`izgara.sh`'in uctan uca kostugunu gostermek.

```
$ bash tools/anahtar-kare/izgara.sh .calisma/t133/duman/kaynak.mkv 2000 2 5
# kaynak=kaynak fps=30.000000 sure=8.000000 bitrate=2000k
# ffmpeg version 9.0-full_build-www.gyan.dev
tavan_sn  g    boyut_bayt  vmaf_ort  vmaf_p10  vmaf_min  ikare  gercek_aralik_sn  kosum
2         60   2021744     90.3337   88.8835   86.1607   4      2.000             1
5         150  2022237     90.4667   88.8534   86.4865   2      5.000             1
```

Boyut farki %0,024 (esitleme yontemi calisiyor). Gerceklesen aralik tam 2.000 ve
5.000 — kati izgara, `auto-mod.md`'nin bulgusuyla tutarli.

### Atlama olcusu — duman testi, K2 VERISI DEGIL

```
$ bash tools/anahtar-kare/atlama.sh .calisma/t133/izgara/kaynak/g150.mkv 4
# taban_ms=217  (ffmpeg acilis maliyeti, cikariliyor)
hedef_sn  onceki_anahtar_sn  cozulen_kare  ham_ms  gecikme_ms
1.600     0.000              48            782     565
3.200     0.000              96            266     49
4.800     0.000              144           1021    804
6.400     5.000              42            263     46

$ bash tools/anahtar-kare/atlama.sh .calisma/t133/izgara/kaynak/g60.mkv 4
# taban_ms=193  (ffmpeg acilis maliyeti, cikariliyor)
hedef_sn  onceki_anahtar_sn  cozulen_kare  ham_ms  gecikme_ms
1.600     0.000              48            888     695
3.200     2.000              36            247     54
4.800     4.000              24            813     620
6.400     6.000              12            215     22
```

ONEMLI: duvar saati gurultulu — ayni is icin 49 ms ile 804 ms cikiyor (disk
onbellegi). Sinyali `cozulen_kare` tasiyor ve deterministik: 4.800 sn hedefinde
5 sn tavani 144 kare cozuyor, 2 sn tavani 24 kare. Alti kat. Devralan
puan/atlama odunlesmesini `cozulen_kare` uzerinden kurmali, `gecikme_ms`
uzerinden kurmamali.

### Donanim kolu — gercek sinama

```
$ for enc in h264_nvenc h264_amf h264_qsv; do ffmpeg ... -c:v $enc -f null - ; done
h264_nvenc   yok
h264_amf     CALISIYOR
h264_qsv     yok
```

### Kaynak havuzu yok — ham kanit

```
$ ls /c/Users/
All Users
Default
Default User
Public
Teknesyum
desktop.ini

$ whoami
Teknesyum

$ ls /c/Users/Administrator/Desktop/Projeler/Vidshrink/.calisma/kaynak
ls: cannot access '...': No such file or directory

$ ls /c/Users/Teknesyum/Desktop/Projeler/VidShrink/.calisma/
T140
T145
t57
```

Tek surucu C:. `/c/Users/Administrator` hicbir surucude yok.

## Olctuklerim ile varsaydiklarim

**Gercek kosum var:**

- Yukaridaki butun tablolar (duman izgarasi, atlama, donanim sinamasi, havuz
  kanitlari).
- `izgara.sh`, `atlama.sh`, `oz.py` uctan uca kostu.
- `hazirla.sh` KOSMADI — kaynak olmadigi icin sozdizimi disinda sinanmadi.

**Kosum yok, koddan/dokumandan OKUNDU (benim olcumum degil, alinti):**

- `FfmpegArguments.cs:184-185` medyan/ortalama tablosu (10,00 sn vs 5,62 sn,
  atlama 202,6 vs 154,9 ms).
- `FfmpegArguments.cs:243-252` tavan taramasi (2/5/10/20 sn).
- `auto-mod.md:276` `-g 120 -> 300` satiri ve `:300-304` anahtar kare sayimi.
- `ab-duzenegi.md:324-328` uc parcanin 483k VMAF degerleri (93,70 / 47,71 / 13,72).

**Cikarim, olcum degil:**

- "Kaynak 60 fps" — `-g 120 = 2,00 sn` ve `-g 300 = 5,00 sn` satirlarindan
  ARITMETIKLE turetildi. Dosyayi acip fps'i dogrudan okumadim (havuz yok).
  Devralan havuza erisince `ffprobe` ile teyit etsin.
- "Iki olcum ters isaret veriyor ve dordu de esigin altinda" — yukaridaki alinti
  sayilar uzerinde benim hesabim.

## Guvenilmeyecek seyler

- `docs/olcumler/anahtar-kare-tavani.md` icinde K4 bolumundeki "Olcum oncesi
  bilinenler" basligi yalniz iki olcumu aniyor. Sonradan ucuncusunu buldum
  (`FfmpegArguments.cs:184-185`) ve onu K2 bolumune yazdim. K4 bolumu bu yuzden
  EKSIK okunuyor; esik degismedi ama gerekce cumlesi guncellenmedi.
- Ayni dosyada K4 bolumu "harita kolunun bugunku etkisi kucuk bir KAYIP yonunde"
  diyor. K2 bolumundeki ucuncu olcum bunun tersini soyluyor. Iki bolum birbiriyle
  celisiyor; K2 bolumu daha sonra yazildi ve daha eksiksiz. Devralan K4'un o
  cumlesini duzeltsin.
- Sozlesmenin kendi oncululu yanlis: `-g 300 = 10 sn @ 30 fps` diyor, dogrusu
  5,00 sn @ 60 fps. Sozlesme dosyasini duzeltmedim (benim owns'im degil).
- Sozlesmenin sonundaki "Kayit noktasi (builder)" bolumu onceki bir kosuma ait
  (`59011c6`, `89c9f68`, `.calisma/t133/kaynak/` altinda dort hazir kaynak). O
  commit'lerin HICBIRI depoda yok, o klasor de yok. Yani o kayit noktasi
  gecersiz; ben sifirdan kurdum.
- `atlama.sh`'in `gecikme_ms` sutunu gurultulu, karar dayanagi yapilmamali.

## Dokundugum dosyalar

owns icinde:

- `docs/olcumler/anahtar-kare-tavani.md` (yeni)
- `tools/anahtar-kare/ortak.sh`, `hazirla.sh`, `izgara.sh`, `atlama.sh`, `oz.py`
  (yeni)

owns DISINA CIKTIM, duzeltildi:

- Duzenegi ilk yazarken kabuk calisma dizini ana depo kokunde kalmis; bes betik
  once `/VidShrink/tools/anahtar-kare/` altina, yani `main`in calisma agacina
  yazildi. Fark edilip T133 worktree'sine TASINDI, ana agac dogrulandi:
  `ls tools/ | grep anahtar-kare` -> bos. Ana agacta bana ait kalinti yok.
  Ana agacta duran `kapsam.json` ve `docs/olcumler/kalan-alti-bant.md` T145'in.

`.calisma/kaynak/` klasorune dokunulmadi (zaten bu makinede yok).
`main`e hicbir sey birlestirilmedi.

## Siradaki adim

Devralanin ilk isi kaynak havuzuna erisilebilir bir yol bulmak; bu cozulmeden
K1/K3/K5 kosamaz ve sentetik kaynak T0 tarafindan gerekceli olarak reddedildi.
Havuz gelince sira sabit: `hazirla.sh` uc parcanin sesini atip kare sayisi
esitligini kapi olarak dogrular (`parca-1` sessiz, `parca-2/3` sesli — bu
asimetri daha once bir A/B'yi haksiz yapti), sonra `izgara.sh` dort kaynak x bes
tavan (2/5/10/15/20 sn) SIRAYLA kosar; makinede es zamanli baska sozlesmeler var,
paralel kosturulmamali. Ilk kosulacak hucre K4'un gurultu hucresi olmali (kaynak
1, tavan 10 sn, bes kez) cunku butun esikler `max(sabit, 2xG)` olarak yazildi ve
`G` olmadan hicbir hucre karara sokulamaz. Izgara dolduktan sonra K3 tablosu
mekanik: her kaynak icin `FfmpegArguments.KeyframeCeilingSeconds(map)` hangi
tavani secerdi, o hucre en iyi mi, degilse kac puan uzakta. Beklentim — ve bu bir
beklenti, olcum degil — kararin "(a) kolu koru" ile "hicbiri, esik asilmadi"
arasinda cikmasi, cunku eldeki uc alinti olcumun dordu de esigin altinda ve ikisi
ters isaretli; (c) ise taramada 10 sn ile 20 sn'nin ayni uc I-kareyi ayni yerlere
koymasi yuzunden zayif gorunuyor, ama bu tek kaynakta olculdu ve bagimsiz
dogrulanmali.
