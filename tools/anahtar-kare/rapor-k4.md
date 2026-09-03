## K4 — Onceden yazilan karar esigi

### Olcut

- **Birincil olcu:** VMAF-NEG **p10**. Bu depoda kuyruk kalemi kararlari p10'a
  baglandi (`docs/olcumler/auto-mod.md`, K4 ayristirmasi).
- **Ikincil olcu:** VMAF-NEG **ortalama**. Isaret birincil olcuyle ayni degilse
  hucre "karisik" damgasi alir, kazanan sayilmaz.
- **Boyut esitligi:** iki pass, sabit `-b:v`, teslim edilen boyut kaynak icindeki
  butun `-g` hucrelerinde **%0,5** bandinda kalmali. Bandin disina cikan hucre
  atilmaz, **"es boyut degil"** damgasiyla tabloda kalir ve kazanan secilemez.
- **En kotu kare:** raporlanir, karara girmez (tek kare gurultuye acik).

### Esik degeri: **0,20 p10**

Iki uctan temellendirildi, ikisi de bu depoda olculmus sayilar:

- Bu depoda "gercek ama ikinci derece" diye kabul edilen en kucuk etki
  **+0,179 p10** (kesime hizalama, `FfmpegArguments.cs:172-174`). Esik bunun
  hemen ustunde.
- Bugunku tavan araliginin icindeki bilinen adim **5 s -> 10 s = +0,033 p10**
  (`FfmpegArguments.cs:184-187`). Esik bunun alti kat ustunde, yani bugunku
  kelepcenin ic gurultusuyle karistirilamaz.

### Karar kurali (olcumden once sabit)

Her kaynak icin `en_iyi_hucre` = p10'u en yuksek, es boyut damgasi temiz hucre.
`h10` = 10,0 s hucresi (bugunku haritasiz varsayilan, HandBrake ile ayni).

1. **(a) Kolu korurum** — dort kaynagin **en az ikisinde** `en_iyi_hucre`
   suresi **10,0 s'nin altinda** ve `p10(en_iyi) - p10(h10) >= 0,20` ise. O
   zaman kazanan kaynak sinifi yazilir ve esik olcuye baglanir.
2. **(b) Kolu kaldiririm / tavani tek degere sabitlerim** — hicbir kaynakta
   1. madde saglanmiyorsa. Kisa `-g` hicbir sinifta 0,20 p10 kazandirmiyorsa
   harita kolunun `-g` uzerindeki tek etkisi optimumdan uzaklasmaktir.
3. **(c) Tavani yukseltirim** — dort kaynagin **en az ikisinde**
   `en_iyi_hucre` suresi **10,0 s'nin ustunde** ve
   `p10(en_iyi) - p10(h10) >= 0,20` ise. (a) ve (c) ayni anda cikarsa kaynak
   sinifina gore ayrisma yazilir, tek sayiya indirgenmez.
4. Hicbiri saglanmiyorsa sonuc **(b)**: `-g`'nin 2 s ustundeki secimi olcunun
   ayirt edemedigi bir bolgede duruyor demektir, ve ayirt edilemeyen bir eksende
   dallanan kol tasinmaz.

### Kosum kosullari (olcumden once sabit)

- Kodlayici: `libx264` (`FfmpegArguments.cs:179`'daki pinli taramayla ayni
  kodlayici), iki pass, `-preset medium`, sabit `-threads 8`.
- `-g` degerleri: 2, 5, 10, 15, 20 saniye karsiliklari (kaynagin kendi fps'i ile
  kareye cevrilir). `-keyint_min` uretim koduyla ayni kural: `round(fps * 1,0)`.
- Dort kaynak, her biri 20 sn: kesik cok / durgun / hareketli / yuksek cozunurluklu.
- VMAF-NEG (`vmaf_v0.6.1neg`), tam klip, kaynak cozunurlugunde.
- Ek olarak `libsvtav1` preset 4 ile tek kaynakta isaret dogrulamasi yapilir;
  bu ana izgaranin karari degildir, yalniz kodlayici bagimliligini isaretler.
