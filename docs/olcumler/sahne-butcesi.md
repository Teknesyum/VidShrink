# Sahne basina bit dagitimi — harita plana bagli degil (T114)

Bu sayfadaki her sayi `tools/sahne-butcesi/` altindaki duzenekten cikar ve
bu sayfayi da o duzenek yazar (`SahneButcesi rapor`). Ozet cumleler elle
yazilmaz, tablodan hesaplanir.

Karar kurallari olcumden **once** yazildi: `tools/sahne-butcesi/ESIKLER.md`,
commit `eb9165c`. Sonradan secilen esik kanit degildir.

## Olcum ortami

- `ffmpeg version 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers` — butun kosumlar tek surumle. Surum sinirini gecen kiyas yok.
- Is parcacigi sabit: `-threads 8`, x265 `pools=8`,
  x264 `threads=8`, SVT-AV1 `lp=8`.
- **Makine paylasimliydi**; paralelde baska ajanlarin olcumleri kosuyordu.
  Bu damga yalniz **sure** sayilarindadir; bit, boyut ve kalite sayilari
  is parcacigi sabitken yukten etkilenmez. Sayfadaki tek sure sayisi
  "Olculemeyenler" altindaki hucre maliyetidir: paylasimli makinede
  olculdugu icin **karsilastirma degil, buyukluk mertebesi** olarak okunur.
- Dal `T114-sahne-butcesi`. Olculen **uretim kodu**: `src/`in son commit'i
  `f542dc22efeaa6ace0522e8d41eeed6f56526f87 T135 ve T130 borclari: manset sayilari, verify filtresi, stdout utf-8, kapanmayan etiket`. Butun kodlamalar bu koddan `--no-incremental`
  derlenmis ikiliyle kosuldu; `--no-build` kullanilmadi. Bu commit'ten
  sonraki degisiklikler yalniz `tools/` ve `docs/` icindedir, plani
  etkilemez.
- Ham cikti: `C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/T114/.calisma/T114` (gitignore'lu).

## Hangi sayi hangi komuttan cikti

Butun olcum tek komutla bastan kosar: `bash tools/sahne-butcesi/01-olcumu-kos.sh`.
Ikili her seferinde `--no-incremental` derlenir; `--no-build` kullanilmaz.

| Bolum | Ureten komut | Ham dosya |
|-------|--------------|-----------|
| Kaynaklar | `00-pencereleri-kes.sh`, sonra `SahneButcesi harita maks <pencere>` | `harita-<pencere>.json` |
| K1, K2 | `SahneButcesi k1 <kol> <pencere>` | `k1-<kol>-<pencere>.json` / `.csv` |
| K3 | olcum degil; kural `tools/sahne-butcesi/Butce.cs` | — |
| K3 eki (denetim) | `SahneButcesi dogrula <kol>` | `dogrula-<pencere>.csv` |
| K3 eki (mutasyon) | `bash tools/sahne-butcesi/03-duzenek-mutasyonu.sh` | `duzenek-mutasyon.csv` |
| K4 | `SahneButcesi k4 maks p1-karisik` | `k4-izgara.csv` |
| K4 eki | `SahneButcesi k4b <kol> <pencere>` | `k4b-<kol>-<pencere>.csv` |
| K5, K6 | `SahneButcesi k5 <kol> <pencere>` | `k5-<kol>-<pencere>.json`, `.zones.txt` |
| Tekrar gurultusu | `SahneButcesi tekrar <kol> <pencere>` | `tekrar-<kol>-<pencere>.csv` |
| K7 | `SahneButcesi k7 <kol> <pencere>` | `k7-<kol>-<pencere>.json`, `.zones.txt` |
| Karar kodu denemesi | `bash tools/sahne-butcesi/04-kapi-denemesi.sh` | `kapi-denemesi.csv` |
| Yorum cumlesi denemesi | `bash tools/sahne-butcesi/06-tekrar-denemesi.sh` | `tekrar-denemesi.csv` |
| Dosya tamligi | `bash tools/sahne-butcesi/05-cikti-denetimi.sh` | `cikti-denetimi.csv` |
| K9 | `bash tools/sahne-butcesi/02-mutasyon.sh` | `k9-mutasyon.txt` |
| bu sayfa | `SahneButcesi rapor` | — |

Kollar: `maks`, `uyumlu`, `yedek`. Pencereler: `p1-karisik`, `p2-durgun`, `p3-hareketli`.

## Olculen dosyalarin tamligi

Bu sayfadaki bitler dosya uzunluklarindan geliyor; yarim kalmis bir
kodlama sessizce kucuk bir "hak edilen" ya da "verilen" uretir. Kodlamalar
`<ad>.yarim.mkv`e yazilip basarida yerine tasinir; ayrica her dosyanin
suresi `ffprobe` ile olculup beklenen sahne/pencere suresiyle
karsilastirilir (esik 0,5 sn).

Denetlenen dosya **127** — referans sahnesi 102, 
kodlama ciktisi 25. Suresi sapan: **0**.

Uretim: `bash tools/sahne-butcesi/05-cikti-denetimi.sh`, ham dosya
`cikti-denetimi.csv`. Olcum bittikten sonra kosar.

## "N hucre" diyen cumleler bagimsiz sayildi

Bu sayfadaki ozet cumleleri `SahneButcesi rapor` uretir; ayni programin
kendi sayisini dogrulamasi kanit degildir. Asagidaki satirlar ayri bir
betikle, ham `.json` ve `.csv` dosyalarindan **yeniden** sayildi; MAE
degerleri de rapordan alinmadi, `HakEdilen`/`Verilen`/`Harita`
dizilerinden bastan hesaplandi.

| Olcu | Bagimsiz sayim | Sayfadaki iddia | Sonuc |
|------|----------------|-----------------|-------|
| K1/K2 olculen hucre | 8 | 8 | tuttu |
| kodlayici en az esit | 5 | 5 | tuttu |
| sahne >= 4 hucre | 5 | 5 | tuttu |
| sahne >= 4, harita onde | 3 | 3 | tuttu |
| K4 eki olculen hucre | 5 | 5 | tuttu |
| tabani gecen hucre | 4 | 4 | tuttu |
| zones kazandi | 1 | 1 | tuttu |
| qcomp kazandi | 3 | 3 | tuttu |
| olculemeyen satir | 27 | 27 | tuttu |

Denetlenen iddia 9, tutmayan **0**. Uretim:
`bash tools/sahne-butcesi/07-sayim-denetimi.sh`, ham dosya
`sayim-denetimi.csv`. Betik sayfayi da okur; sayfa degisirse tekrar kosar.

## Sorulan tek soru

Kodlayicinin kendi hiz denetimi sahne basina biti zaten dogru dagitiyor mu?
Uc dagitim yan yana olculur:

| Ad | Nasil olculdu | Kimin karari |
|----|---------------|--------------|
| hak edilen | her sahne **ayri ayri**, sabit `CRF 26` | referans |
| verilen | pencere butun halinde bugunku planla (iki gecis, hedef boyut); paket boyutlari sahne araligina toplanir | kodlayici |
| harita | `SceneMap.Scenes[i].Bits` — sonda ciktisi (x264 ultrafast crf23, 640 genislik) | bizim onerimiz |

Paylar penceredeki toplama normalize edilir; birim yuzde puani (pp).

## Kaynaklar

Uretim: `bash tools/sahne-butcesi/00-pencereleri-kes.sh`, sonra
`SahneButcesi harita maks <pencere>`; ham dosya `harita-<pencere>.json`.

Uc pencere de `kaynak-1080p60-hdr-17dk-yalniz-video.mkv` icinden kesildi
(1920x1080 hevc 10-bit HDR, 60 fps). Ses akisi yok: T63'te A/B'yi haksiz
yapan ses farki bu olcume giremez. Pencere sinirlari T105'in yer gercegi
pencereleridir; gercek kesim sayilari oradan gelir.

| Pencere | Icerik | Gercek kesim (T105) | Harita sahnesi | Sure (sn) |
|---------|--------|---------------------|----------------|-----------|
| `p1-karisik` | oyun + menu + diyalog, 28 gercek kesim | 28 | 28 | 189.2 |
| `p2-durgun` | menu / egitim ekrani, 7 gercek kesim | 7 | 6 | 186.4 |
| `p3-hareketli` | kesintisiz dovus, 0 gercek kesim | 0 | 2 | 189.0 |

**Uc pencere de tek kaynaktan gelir.** Uc ayri film degil; icerik rejimi
uc ayri olsa da kodlayici davranisi ayni kamera ve ayni kodlama gecmisi
uzerinde olculmustur. Bu sayfanin en zayif yani budur.

## K1 — bugunku dagitimin hatasi

Uretim: `SahneButcesi k1 <kol> <pencere>`; ham dosya
`k1-<kol>-<pencere>.json` ve `.csv`.

### maks / p1-karisik

Plan: `libsvtav1` 2pass 2610k 1920x1080@60 preset `6` hedef 60 MB. Referans toplami 2019.3 Mbit, plan ciktisi 495.5 Mbit (referans/plan = 4.08x).

| Sahne | Bas (sn) | Sure (sn) | Karmasiklik | hak edilen (pp) | verilen (pp) | harita (pp) | verilen−hak | harita−hak |
|-------|----------|-----------|-------------|-----------------|--------------|-------------|-------------|------------|
| 0 | 0.0 | 14.9 | 0.96 | 6.49 | 5.71 | 7.51 | -0.79 | +1.02 |
| 1 | 14.9 | 11.1 | 0.76 | 4.80 | 3.74 | 4.45 | -1.05 | -0.35 |
| 2 | 25.9 | 13.0 | 0.64 | 4.36 | 3.77 | 4.39 | -0.59 | +0.03 |
| 3 | 38.9 | 2.4 | 0.79 | 0.77 | 0.81 | 1.01 | +0.04 | +0.24 |
| 4 | 41.4 | 4.6 | 0.70 | 1.48 | 1.34 | 1.70 | -0.14 | +0.22 |
| 5 | 45.9 | 10.6 | 0.75 | 3.25 | 2.97 | 4.22 | -0.28 | +0.97 |
| 6 | 56.6 | 2.1 | 0.94 | 0.84 | 0.81 | 1.03 | -0.02 | +0.20 |
| 7 | 58.6 | 6.1 | 0.74 | 1.97 | 1.76 | 2.39 | -0.21 | +0.42 |
| 8 | 64.8 | 5.0 | 0.76 | 1.75 | 1.69 | 2.03 | -0.07 | +0.27 |
| 9 | 69.8 | 2.1 | 0.77 | 0.56 | 0.49 | 0.84 | -0.07 | +0.28 |
| 10 | 71.8 | 39.6 | 1.77 | 41.49 | 46.91 | 37.02 | +5.42 | -4.47 |
| 11 | 111.4 | 3.1 | 0.71 | 0.79 | 1.04 | 1.16 | +0.26 | +0.37 |
| 12 | 114.5 | 6.3 | 0.93 | 4.10 | 3.02 | 3.09 | -1.08 | -1.01 |
| 13 | 120.8 | 4.3 | 0.65 | 1.23 | 1.22 | 1.49 | -0.01 | +0.26 |
| 14 | 125.1 | 3.8 | 0.74 | 1.92 | 1.70 | 1.47 | -0.22 | -0.45 |
| 15 | 128.9 | 6.2 | 0.68 | 1.86 | 1.52 | 2.24 | -0.33 | +0.38 |
| 16 | 135.1 | 8.3 | 0.76 | 1.82 | 2.06 | 3.30 | +0.24 | +1.48 |
| 17 | 143.4 | 7.2 | 0.90 | 3.41 | 2.69 | 3.42 | -0.72 | +0.01 |
| 18 | 150.5 | 3.6 | 0.65 | 0.86 | 0.94 | 1.24 | +0.08 | +0.39 |
| 19 | 154.2 | 5.2 | 0.69 | 1.56 | 1.45 | 1.90 | -0.10 | +0.34 |
| 20 | 159.4 | 2.8 | 0.90 | 1.88 | 1.21 | 1.34 | -0.66 | -0.54 |
| 21 | 162.2 | 3.5 | 0.62 | 0.93 | 1.01 | 1.14 | +0.07 | +0.21 |
| 22 | 165.7 | 5.7 | 0.86 | 2.68 | 2.22 | 2.61 | -0.46 | -0.07 |
| 23 | 171.4 | 2.1 | 0.62 | 0.49 | 0.46 | 0.68 | -0.02 | +0.19 |
| 24 | 173.4 | 2.0 | 0.98 | 1.20 | 1.12 | 1.02 | -0.08 | -0.18 |
| 25 | 175.4 | 2.3 | 0.71 | 0.53 | 0.39 | 0.88 | -0.14 | +0.34 |
| 26 | 177.8 | 5.7 | 0.87 | 1.46 | 1.62 | 2.64 | +0.16 | +1.19 |
| 27 | 183.5 | 5.7 | 1.25 | 5.53 | 6.33 | 3.77 | +0.80 | -1.76 |

### maks / p2-durgun

Plan: `libsvtav1` 2pass 1636k 1920x1080@60 preset `6` hedef 60 MB. Referans toplami 184.9 Mbit, plan ciktisi 219.6 Mbit (referans/plan = 0.84x).

| Sahne | Bas (sn) | Sure (sn) | Karmasiklik | hak edilen (pp) | verilen (pp) | harita (pp) | verilen−hak | harita−hak |
|-------|----------|-----------|-------------|-----------------|--------------|-------------|-------------|------------|
| 0 | 0.0 | 22.3 | 0.97 | 14.49 | 14.19 | 11.61 | -0.30 | -2.87 |
| 1 | 22.3 | 50.2 | 0.87 | 27.32 | 24.18 | 23.33 | -3.15 | -4.00 |
| 2 | 72.4 | 38.3 | 0.88 | 17.11 | 15.52 | 18.03 | -1.59 | +0.92 |
| 3 | 110.7 | 33.9 | 0.85 | 16.77 | 15.42 | 15.53 | -1.35 | -1.24 |
| 4 | 144.6 | 28.5 | 1.58 | 17.13 | 25.60 | 24.11 | +8.47 | +6.98 |
| 5 | 173.2 | 13.2 | 1.04 | 7.18 | 5.10 | 7.39 | -2.08 | +0.21 |

### maks / p3-hareketli

Plan: `libsvtav1` 2pass 2612k 1920x1080@60 preset `6` hedef 60 MB. Referans toplami 3230.4 Mbit, plan ciktisi 494.3 Mbit (referans/plan = 6.54x).

| Sahne | Bas (sn) | Sure (sn) | Karmasiklik | hak edilen (pp) | verilen (pp) | harita (pp) | verilen−hak | harita−hak |
|-------|----------|-----------|-------------|-----------------|--------------|-------------|-------------|------------|
| 0 | 0.0 | 41.9 | 1.01 | 19.08 | 20.68 | 22.34 | +1.60 | +3.26 |
| 1 | 41.9 | 147.1 | 1.00 | 80.92 | 79.32 | 77.66 | -1.60 | -3.26 |

### uyumlu / p1-karisik

Plan: `libx264` 2pass 2610k 1458x820@60 preset `slow` hedef 60 MB. Referans toplami 314.9 Mbit, plan ciktisi 491.4 Mbit (referans/plan = 0.64x).

| Sahne | Bas (sn) | Sure (sn) | Karmasiklik | hak edilen (pp) | verilen (pp) | harita (pp) | verilen−hak | harita−hak |
|-------|----------|-----------|-------------|-----------------|--------------|-------------|-------------|------------|
| 0 | 0.0 | 14.9 | 0.96 | 4.73 | 7.78 | 7.51 | +3.05 | +2.78 |
| 1 | 14.9 | 11.1 | 0.76 | 2.76 | 5.02 | 4.45 | +2.26 | +1.69 |
| 2 | 25.9 | 13.0 | 0.64 | 3.71 | 5.88 | 4.39 | +2.16 | +0.68 |
| 3 | 38.9 | 2.4 | 0.79 | 0.58 | 0.96 | 1.01 | +0.38 | +0.43 |
| 4 | 41.4 | 4.6 | 0.70 | 1.50 | 2.17 | 1.70 | +0.68 | +0.20 |
| 5 | 45.9 | 10.6 | 0.75 | 2.52 | 4.17 | 4.22 | +1.64 | +1.69 |
| 6 | 56.6 | 2.1 | 0.94 | 0.65 | 1.11 | 1.03 | +0.46 | +0.38 |
| 7 | 58.6 | 6.1 | 0.74 | 1.51 | 2.52 | 2.39 | +1.02 | +0.88 |
| 8 | 64.8 | 5.0 | 0.76 | 1.68 | 2.55 | 2.03 | +0.88 | +0.35 |
| 9 | 69.8 | 2.1 | 0.77 | 0.53 | 0.84 | 0.84 | +0.31 | +0.31 |
| 10 | 71.8 | 39.6 | 1.77 | 53.46 | 32.03 | 37.02 | -21.43 | -16.44 |
| 11 | 111.4 | 3.1 | 0.71 | 0.81 | 1.30 | 1.16 | +0.49 | +0.35 |
| 12 | 114.5 | 6.3 | 0.93 | 2.25 | 3.79 | 3.09 | +1.54 | +0.84 |
| 13 | 120.8 | 4.3 | 0.65 | 1.25 | 1.69 | 1.49 | +0.44 | +0.25 |
| 14 | 125.1 | 3.8 | 0.74 | 1.18 | 1.70 | 1.47 | +0.51 | +0.29 |
| 15 | 128.9 | 6.2 | 0.68 | 1.68 | 2.24 | 2.24 | +0.56 | +0.56 |
| 16 | 135.1 | 8.3 | 0.76 | 1.72 | 2.26 | 3.30 | +0.54 | +1.58 |
| 17 | 143.4 | 7.2 | 0.90 | 2.62 | 3.46 | 3.42 | +0.84 | +0.80 |
| 18 | 150.5 | 3.6 | 0.65 | 0.74 | 1.03 | 1.24 | +0.29 | +0.50 |
| 19 | 154.2 | 5.2 | 0.69 | 1.44 | 1.96 | 1.90 | +0.52 | +0.46 |
| 20 | 159.4 | 2.8 | 0.90 | 0.95 | 1.43 | 1.34 | +0.48 | +0.39 |
| 21 | 162.2 | 3.5 | 0.62 | 0.89 | 1.27 | 1.14 | +0.38 | +0.25 |
| 22 | 165.7 | 5.7 | 0.86 | 2.01 | 2.89 | 2.61 | +0.88 | +0.60 |
| 23 | 171.4 | 2.1 | 0.62 | 0.50 | 0.68 | 0.68 | +0.18 | +0.18 |
| 24 | 173.4 | 2.0 | 0.98 | 0.75 | 1.13 | 1.02 | +0.38 | +0.27 |
| 25 | 175.4 | 2.3 | 0.71 | 0.58 | 0.81 | 0.88 | +0.24 | +0.30 |
| 26 | 177.8 | 5.7 | 0.87 | 1.61 | 2.23 | 2.64 | +0.62 | +1.03 |
| 27 | 183.5 | 5.7 | 1.25 | 5.39 | 5.10 | 3.77 | -0.29 | -1.62 |

### uyumlu / p2-durgun

Plan: `libx264` 2pass 1636k 1920x1080@60 preset `slow` hedef 60 MB. Referans toplami 91.7 Mbit, plan ciktisi 301.0 Mbit (referans/plan = 0.30x).

| Sahne | Bas (sn) | Sure (sn) | Karmasiklik | hak edilen (pp) | verilen (pp) | harita (pp) | verilen−hak | harita−hak |
|-------|----------|-----------|-------------|-----------------|--------------|-------------|-------------|------------|
| 0 | 0.0 | 22.3 | 0.97 | 12.66 | 13.34 | 11.61 | +0.68 | -1.05 |
| 1 | 22.3 | 50.2 | 0.87 | 26.64 | 25.89 | 23.33 | -0.75 | -3.31 |
| 2 | 72.4 | 38.3 | 0.88 | 18.72 | 16.90 | 18.03 | -1.82 | -0.69 |
| 3 | 110.7 | 33.9 | 0.85 | 18.00 | 15.21 | 15.53 | -2.79 | -2.47 |
| 4 | 144.6 | 28.5 | 1.58 | 16.86 | 21.88 | 24.11 | +5.03 | +7.25 |
| 5 | 173.2 | 13.2 | 1.04 | 7.12 | 6.76 | 7.39 | -0.35 | +0.27 |

### uyumlu / p3-hareketli

Plan: `libx264` 2pass 2612k 1458x820@60 preset `slow` hedef 60 MB. Referans toplami 586.9 Mbit, plan ciktisi 492.4 Mbit (referans/plan = 1.19x).

| Sahne | Bas (sn) | Sure (sn) | Karmasiklik | hak edilen (pp) | verilen (pp) | harita (pp) | verilen−hak | harita−hak |
|-------|----------|-----------|-------------|-----------------|--------------|-------------|-------------|------------|
| 0 | 0.0 | 41.9 | 1.01 | 21.23 | 20.56 | 22.34 | -0.67 | +1.11 |
| 1 | 41.9 | 147.1 | 1.00 | 78.77 | 79.44 | 77.66 | +0.67 | -1.11 |

### yedek / p1-karisik

Plan: `libx265` 2pass 2610k 1728x972@60 preset `slow` hedef 60 MB. Referans toplami 496.4 Mbit, plan ciktisi 490.7 Mbit (referans/plan = 1.01x).

| Sahne | Bas (sn) | Sure (sn) | Karmasiklik | hak edilen (pp) | verilen (pp) | harita (pp) | verilen−hak | harita−hak |
|-------|----------|-----------|-------------|-----------------|--------------|-------------|-------------|------------|
| 0 | 0.0 | 14.9 | 0.96 | 5.29 | 6.92 | 7.51 | +1.64 | +2.22 |
| 1 | 14.9 | 11.1 | 0.76 | 3.61 | 5.41 | 4.45 | +1.80 | +0.84 |
| 2 | 25.9 | 13.0 | 0.64 | 3.94 | 5.52 | 4.39 | +1.58 | +0.45 |
| 3 | 38.9 | 2.4 | 0.79 | 0.64 | 0.97 | 1.01 | +0.33 | +0.37 |
| 4 | 41.4 | 4.6 | 0.70 | 1.39 | 1.85 | 1.70 | +0.46 | +0.31 |
| 5 | 45.9 | 10.6 | 0.75 | 2.69 | 3.92 | 4.22 | +1.23 | +1.53 |
| 6 | 56.6 | 2.1 | 0.94 | 0.73 | 1.00 | 1.03 | +0.27 | +0.31 |
| 7 | 58.6 | 6.1 | 0.74 | 1.61 | 2.38 | 2.39 | +0.77 | +0.78 |
| 8 | 64.8 | 5.0 | 0.76 | 1.60 | 2.33 | 2.03 | +0.74 | +0.43 |
| 9 | 69.8 | 2.1 | 0.77 | 0.50 | 0.77 | 0.84 | +0.27 | +0.33 |
| 10 | 71.8 | 39.6 | 1.77 | 49.26 | 32.31 | 37.02 | -16.95 | -12.24 |
| 11 | 111.4 | 3.1 | 0.71 | 0.82 | 1.22 | 1.16 | +0.40 | +0.34 |
| 12 | 114.5 | 6.3 | 0.93 | 3.07 | 4.49 | 3.09 | +1.43 | +0.02 |
| 13 | 120.8 | 4.3 | 0.65 | 1.20 | 1.62 | 1.49 | +0.43 | +0.30 |
| 14 | 125.1 | 3.8 | 0.74 | 1.50 | 2.21 | 1.47 | +0.71 | -0.02 |
| 15 | 128.9 | 6.2 | 0.68 | 1.67 | 2.11 | 2.24 | +0.43 | +0.57 |
| 16 | 135.1 | 8.3 | 0.76 | 1.76 | 2.22 | 3.30 | +0.45 | +1.54 |
| 17 | 143.4 | 7.2 | 0.90 | 2.77 | 3.82 | 3.42 | +1.06 | +0.65 |
| 18 | 150.5 | 3.6 | 0.65 | 0.79 | 1.10 | 1.24 | +0.31 | +0.45 |
| 19 | 154.2 | 5.2 | 0.69 | 1.41 | 1.82 | 1.90 | +0.41 | +0.49 |
| 20 | 159.4 | 2.8 | 0.90 | 1.33 | 1.80 | 1.34 | +0.46 | +0.01 |
| 21 | 162.2 | 3.5 | 0.62 | 0.88 | 1.26 | 1.14 | +0.38 | +0.26 |
| 22 | 165.7 | 5.7 | 0.86 | 2.16 | 3.18 | 2.61 | +1.01 | +0.45 |
| 23 | 171.4 | 2.1 | 0.62 | 0.50 | 0.74 | 0.68 | +0.24 | +0.18 |
| 24 | 173.4 | 2.0 | 0.98 | 0.90 | 1.23 | 1.02 | +0.32 | +0.12 |
| 25 | 175.4 | 2.3 | 0.71 | 0.55 | 0.69 | 0.88 | +0.13 | +0.32 |
| 26 | 177.8 | 5.7 | 0.87 | 1.53 | 2.08 | 2.64 | +0.56 | +1.12 |
| 27 | 183.5 | 5.7 | 1.25 | 5.90 | 5.03 | 3.77 | -0.87 | -2.13 |

### yedek / p2-durgun

- **bilinmiyor**: plan passthrough (hevc); kodlama yok, sahneye bit dagitilmiyor

### yedek / p3-hareketli

Plan: `libx265` 2pass 2612k 1728x972@60 preset `slow` hedef 60 MB. Referans toplami 923.0 Mbit, plan ciktisi 494.6 Mbit (referans/plan = 1.87x).

| Sahne | Bas (sn) | Sure (sn) | Karmasiklik | hak edilen (pp) | verilen (pp) | harita (pp) | verilen−hak | harita−hak |
|-------|----------|-----------|-------------|-----------------|--------------|-------------|-------------|------------|
| 0 | 0.0 | 41.9 | 1.01 | 20.24 | 19.36 | 22.34 | -0.88 | +2.10 |
| 1 | 41.9 | 147.1 | 1.00 | 79.76 | 80.64 | 77.66 | +0.88 | -2.10 |

## K2 — kodlayicinin dagitimi bizim onerimizle yan yana

Uretim: K1 ile ayni kosum (`SahneButcesi k1 <kol> <pencere>`);
bu bolum ayni ham dosyalari baska bir kapidan okur.

Kapi (`ESIKLER.md`, olcumden once): (1) `rho(verilen,hak) >= 0,80`,
(2) `MAE(verilen) <= MAE(harita)`, (3) ters dusen sahne orani `< %20`.
Ucu birden saglaniyorsa is biter ve kod degismez.

| Kol | Pencere | Sahne | rho(verilen,hak) | rho(harita,hak) | MAE verilen (pp) | MAE harita (pp) | Ters dusen | K1 kapi | K2 kapi | K3 kapi |
|-----|---------|-------|------------------|-----------------|------------------|-----------------|------------|---------|---------|---------|
| maks | `p1-karisik` | 28 | 0.969 | 0.925 | 0.50 | 0.63 | 16/28 (57%) | evet | evet | **hayir** |
| maks | `p2-durgun` | 6 | 0.943 | 0.943 | 2.82 | 2.70 | 2/6 (33%) | evet | **hayir** | **hayir** |
| maks | `p3-hareketli` | 2 | 1.000 (n=2, anlamsiz) | 1.000 | 1.60 | 3.26 | 0/2 (0%) (n=2, anlamsiz) | evet | evet | evet |
| uyumlu | `p1-karisik` | 28 | 0.990 | 0.979 | 1.55 | 1.29 | 0/28 (0%) | evet | **hayir** | evet |
| uyumlu | `p2-durgun` | 6 | 0.829 | 0.657 | 1.90 | 2.51 | 2/6 (33%) | evet | evet | **hayir** |
| uyumlu | `p3-hareketli` | 2 | 1.000 (n=2, anlamsiz) | 1.000 | 0.67 | 1.11 | 2/2 (100%) (n=2, anlamsiz) | evet | evet | **hayir** |
| yedek | `p1-karisik` | 28 | 0.986 | 0.969 | 1.27 | 1.03 | 1/28 (4%) | evet | **hayir** | evet |
| yedek | `p2-durgun` | 6 | bilinmiyor | bilinmiyor | bilinmiyor | bilinmiyor | bilinmiyor | — | — | — |
| yedek | `p3-hareketli` | 2 | 1.000 (n=2, anlamsiz) | 1.000 | 0.88 | 2.10 | 2/2 (100%) (n=2, anlamsiz) | evet | evet | **hayir** |

**K2 kapisi kapanmadi.** Olculen 8 hucreden 1 tanesinde ucu birden saglandi.

- K1 kapisi (`rho(verilen,hak) >= 0,80`): 8/8 hucre
- K2 kapisi (`MAE(verilen) <= MAE(harita)`): 5/8 hucre
- K3 kapisi (ters dusen orani `< %20`): 3/8 hucre

Olculemeyen 1 hucre (varsayilana dusurulmedi, ayri satir):
- **bilinmiyor** — yedek/p2-durgun: plan passthrough (hevc); kodlama yok, sahneye bit dagitilmiyor

Sahne sayisi 4'un altindaki pencerede sira korelasyonu anlamsizdir ve o
sutunda isaretlidir; o pencerede karari MAE tasir. Sahne sayisi icerigin
kendisidir: kesimi olmayan pencerede dagitilacak sahne de yoktur.

## K3 — kural `SceneMap`'in kendi sayilarindan cikiyor mu

Evet. Aday kural `Butce.ZoneCarpanlari` yalniz su ucunu okur:

| Girdi | Kaynak | Yeni sonda kosumu |
|-------|--------|-------------------|
| `Scene.Complexity` | `SceneMap.cs:13` — sonda ciktisi | yok |
| `Scene.Bits` | `SceneMap.cs:12` — sonda ciktisi | yok |
| sahne suresi | `Scene.Start` / `Scene.End` | yok |

Kural: `b_i = clamp(Complexity_i^gamma, 0.25, 4.0)`, sure agirlikli ortalamasi 1,0'a normalize.
`gamma = 1 - qcomp = 0.40` (x264/x265 varsayilan `qcomp = 0.60`).

`gamma` telafi sabiti degil: iki gecis hiz denetimi biti karmasikliga
`qcomp` ussuyle dagitir, harita tam oranli dagitim onerir (us 1,0);
us farki tam olarak `1 - qcomp`'tur. Normalizasyon K6'nin sartidir —
carpanlar biti yeniden bolusturur, toplami degistirmez.

**T96'nin %10,4'luk sonda maliyeti artmaz**: kural mevcut haritanin
ustunde calisir, yeni tarama acmaz.

### K3 eki — kural duzenegin icinde denetleniyor

Dagitim koda girmeyebilir, ama K5'in A/B'sini ureten sey bu kuraldir:
kuralda sessiz bir hata olsaydi K5 zayif bir kazanc olcer ve biz onu
"dagitim ise yaramiyor" diye okurduk. Bu yuzden kural duzenek icinde
denetleniyor: `SahneButcesi dogrula <kol> [pencere]`.

Denetlenen sartlar: carpan sayisi sahne sayisina esit, her carpan
`[0.25, 4.00]`
kiskaci icinde, sure agirlikli ortalama `1,0` (kiskac baglamadikca),
karmasiklik sirasi carpan sirasiyla ayni, zone kare araliklari artan
ve cakismasiz. Uc harita da denetlenir: dogru, eksik kesim, fazla kesim.

| Pencere | Harita | Sahne | Zone | En kucuk b | En buyuk b | Aralik |
|---------|--------|-------|------|------------|------------|--------|
| `p1-karisik` | dogru | 28 | 28 | 0.842 | 1.279 | 0.437 |
| `p1-karisik` | eksik-kesim | 15 | 15 | 0.878 | 1.262 | 0.384 |
| `p1-karisik` | fazla-kesim | 56 | 56 | 0.842 | 1.279 | 0.437 |
| `p2-durgun` | dogru | 6 | 6 | 0.944 | 1.207 | 0.263 |
| `p2-durgun` | eksik-kesim | 4 | 4 | 0.949 | 1.072 | 0.123 |
| `p2-durgun` | fazla-kesim | 12 | 12 | 0.944 | 1.207 | 0.263 |
| `p3-hareketli` | dogru | 2 | 2 | 0.999 | 1.003 | 0.004 |
| `p3-hareketli` | eksik-kesim | 2 | 2 | 0.999 | 1.003 | 0.004 |
| `p3-hareketli` | fazla-kesim | 4 | 4 | 0.999 | 1.003 | 0.004 |

Denetlenen pencere 3, denetimden gecen 3.

**Aralik sutunu kazancin ust sinirini soyluyor.** `1,0` "kodlayicinin
verecegi kadar ver" demektir; aralik daraldikca dagitim kodlayicinin
kararindan uzaklasamaz. `p3-hareketli`'de aralik sifira yakin: o
pencerede dagitim taban kosumuyla neredeyse ayni dosyayi uretir ve
kazanc olcusu oradan gelemez. Bu bir olcum kusuru degil, kuralin
kesintisiz hareket iceren kaynakta soyleyecek sozu olmamasidir.

Denetimin kendisi de olculdu: kural bilerek bozuldu ve denetimin
kirildigi goruldu (`bash tools/sahne-butcesi/03-duzenek-mutasyonu.sh`).

| Mutasyon | Ne degisti | Denetim |
|----------|------------|---------|
| M0 | temiz agac | gecti |
| M1 | normalizasyon kaldirildi (ortalama 1,0'a cekilmiyor) | **kirildi** |
| M2 | us isareti ters (karmasik sahneye az bit) | **kirildi** |
| M3 | zone araliklari cakisabilir hale getirildi | **kirildi** |

Bozucu mutasyon 3, denetimi kiran 3; temiz agac: gecti.

## K4 — aday x kodlayici izgarasi

Cikis kodunun sifir olmasi destek sayilmaz: x264/x265 ve SVT-AV1 parametre
ayristiricilari tanimadiklari anahtari uyariyla geciyor. Her hucre **iki
farkli degerle** kodlandi; once ayni parametreyle iki kosum yapilip tekrar
gurultusu olculdu. Fark gurultunun iki katini ve ciktinin %1'ini asmadikca
destek yazilmaz.

| Kodlayici | Aday | Destek | A (bayt) | B (bayt) | Fark | Gurultu | Not |
|-----------|------|--------|----------|----------|------|---------|-----|
| `libx265` | kontrol | - | 411929 | 411929 | 0 |  | ayni parametreyle iki kosum — tekrar gurultusu |
| `libx264` | kontrol | - | 411100 | 411100 | 0 |  | ayni parametreyle iki kosum — tekrar gurultusu |
| `libsvtav1` | kontrol | - | 754841 | 755945 | 1104 |  | ayni parametreyle iki kosum — tekrar gurultusu |
| `hevc_nvenc` | kontrol | - | 410646 | 410646 | 0 |  | ayni parametreyle iki kosum — tekrar gurultusu |
| `av1_nvenc` | kontrol | - | 482409 | 482409 | 0 |  | ayni parametreyle iki kosum — tekrar gurultusu |
| `libx265` | zones | evet | 689507 | 237221 | 452286 | 0 | iki deger belirgin farkli cikti uretti |
| `libx265` | qcomp | evet | 407044 | 439695 | 32651 | 0 | iki deger belirgin farkli cikti uretti |
| `libx264` | zones | evet | 674048 | 240681 | 433367 | 0 | iki deger belirgin farkli cikti uretti |
| `libx264` | qcomp | evet | 404471 | 444705 | 40234 | 0 | iki deger belirgin farkli cikti uretti |
| `libsvtav1` | zones | hayir | 761333 | 756503 | 4830 | 1104 | fark tekrar gurultusunun icinde — parametre etkisiz ya da yok sayildi |
| `libsvtav1` | qcomp | evet | 755279 | 720981 | 34298 | 1104 | iki deger belirgin farkli cikti uretti |
| `libsvtav1` | aq | hayir | 751182 | 755908 | 4726 | 1104 | fark tekrar gurultusunun icinde — parametre etkisiz ya da yok sayildi |
| `hevc_nvenc` | zones | hayir |  |  |  |  | kodlayicida zone parametresi yok |
| `hevc_nvenc` | aq | evet | 410646 | 399896 | 10750 | 0 | iki deger belirgin farkli cikti uretti |
| `av1_nvenc` | zones | hayir |  |  |  |  | kodlayicida zone parametresi yok |
| `av1_nvenc` | aq | evet | 482409 | 473898 | 8511 | 0 | iki deger belirgin farkli cikti uretti |

**Tabloda `zones` denenen 5 kodlayicinin 2 tanesinde
parametre isliyor:** `libx265`, `libx264`. Islemeyen 3:
`libsvtav1`, `hevc_nvenc`, `av1_nvenc`.

Uretimin varsayilan kolu (`maks` -> `libsvtav1`) islemeyen listede.
Dagitim koda girse bile varsayilan yolda **etkisiz kalir**; kazanc
yalniz `libx264` ve `libx265` yollarinda mumkun.

### K4 eki — iki aday yan yana, K1 farkini hangisi kapatiyor

K4'un izgarasi "parametre isliyor mu" sorusunu yanitlar; kabul kriteri
ayrica **hangi adayin K1 farkini daha cok kapattigini** sorar. Olcu K1'in
kendi olcusudur: `MAE(verilen, hak edilen)`, yuzde puani. Uc kosum ayni
plan ve ayni hedef boyutla yapilir, degisen tek sey parametredir.

- `taban` — bugunku plan, ek parametre yok (K1'in `verilen` sutunu).
- `zones` — sahne araligina `b` carpani; carpanlar haritadan.
- `qcomp` — iki gecis yanliligi. Kodlayici biti `karmasiklik^qcomp` ile
  dagitir, harita `karmasiklik^1` onerir; ikisini esitleyen deger
  `qcomp = 1,0`'dir. Telafi sabiti degil, haritanin onerisinin ayni
  denklemdeki karsiligi.

| Yazilim kolu | Pencere | Aday | Parametre | MAE (pp) | Tabana gore |
|--------------|---------|------|-----------|----------|-------------|
| maks | `p1-karisik` | zones | `-` | **bilinmiyor**: libsvtav1 zone parametresini yok sayiyor (K4 izgarasi) | — |
| maks | `p1-karisik` | qcomp | `-` | **bilinmiyor**: duzenek olcmedi: qcomp libsvtav1'de calisiyor (K4 izgarasi) ama duzenek her iki adayi da ZonesFlag'in bayragindan geciriyor | — |
| maks | `p2-durgun` | zones | `-` | **bilinmiyor**: libsvtav1 zone parametresini yok sayiyor (K4 izgarasi) | — |
| maks | `p2-durgun` | qcomp | `-` | **bilinmiyor**: duzenek olcmedi: qcomp libsvtav1'de calisiyor (K4 izgarasi) ama duzenek her iki adayi da ZonesFlag'in bayragindan geciriyor | — |
| maks | `p3-hareketli` | zones | `-` | **bilinmiyor**: libsvtav1 zone parametresini yok sayiyor (K4 izgarasi) | — |
| maks | `p3-hareketli` | qcomp | `-` | **bilinmiyor**: duzenek olcmedi: qcomp libsvtav1'de calisiyor (K4 izgarasi) ama duzenek her iki adayi da ZonesFlag'in bayragindan geciriyor | — |
| uyumlu | `p1-karisik` | taban | `-` | 1.552 | — |
| uyumlu | `p1-karisik` | zones | `zones=<harita>` | 1.553 | +0.001 |
| uyumlu | `p1-karisik` | qcomp | `qcomp=1.0` | 1.546 | -0.006 |
| uyumlu | `p2-durgun` | taban | `-` | 1.901 | — |
| uyumlu | `p2-durgun` | zones | `zones=<harita>` | 2.757 | +0.856 |
| uyumlu | `p2-durgun` | qcomp | `qcomp=1.0` | 2.583 | +0.682 |
| uyumlu | `p3-hareketli` | taban | `-` | 0.674 | — |
| uyumlu | `p3-hareketli` | zones | `zones=<harita>` | 0.609 | -0.065 |
| uyumlu | `p3-hareketli` | qcomp | `qcomp=1.0` | 0.077 | -0.597 |
| yedek | `p1-karisik` | taban | `-` | 1.273 | — |
| yedek | `p1-karisik` | zones | `zones=<harita>` | 1.229 | -0.044 |
| yedek | `p1-karisik` | qcomp | `qcomp=1.0` | 1.272 | -0.001 |
| yedek | `p2-durgun` | zones | `-` | **bilinmiyor**: plan passthrough (hevc) | — |
| yedek | `p2-durgun` | qcomp | `-` | **bilinmiyor**: plan passthrough (hevc) | — |
| yedek | `p3-hareketli` | taban | `-` | 0.877 | — |
| yedek | `p3-hareketli` | zones | `zones=<harita>` | 0.800 | -0.077 |
| yedek | `p3-hareketli` | qcomp | `qcomp=1.0` | 0.215 | -0.662 |

Iki adayin da olculdugu hucre 5; hucre basina dusuk MAE'yi veren aday: `qcomp` 4, `zones` 1.

Hangi adayin kazandigi tek basina bir sey soylemez: kazanc, kapatilmasi
istenen K1 acigi ile yan yana konmadan okunamaz. Acik, ayni hucrede
`MAE(verilen) - MAE(harita)`; kazanc, `MAE(taban) - MAE(en iyi aday)`.

| Yazilim kolu | Pencere | K1 acigi (pp) | En iyi aday | Kazanc (pp) | Acigin kapanan orani |
|--------------|---------|---------------|-------------|-------------|----------------------|
| uyumlu | `p1-karisik` | +0.261 | qcomp | +0.006 | 2.3% |
| uyumlu | `p2-durgun` | -0.606 | qcomp | -0.682 | acik yok |
| uyumlu | `p3-hareketli` | -0.434 | qcomp | +0.597 | acik yok |
| yedek | `p1-karisik` | +0.244 | zones | +0.044 | 18.0% |
| yedek | `p3-hareketli` | -1.223 | qcomp | +0.662 | acik yok |

Olculen 5 hucrenin 4 tanesinde en iyi aday tabani
gecti; gorulen en buyuk kazanc 0.662 pp.
Bu sutunlar kazancin buyuklugunu soyler, isaretini degil: kucuk ama
pozitif bir fark da olcum gurultusu icinde kalabilir. K5'in kalite
kapisi bu sayfada karari veren yerdir, bu tablo degil.

Tabani gecen 4 hucrenin 1 tanesini `zones`, 3 tanesini `qcomp` kazandi. Bu ayrim sozlesmenin sorusu acisindan belirleyicidir: **haritanin sahne basina sayilarini kodlayiciya tasiyan tek aday `zones`**. `qcomp` tek bir kuresel skalerdir; hangi sahnenin ne kadar karmasik oldugu bilgisini tasimaz, `SceneMap` olmadan da ayni deger verilebilir. Dolayisiyla `qcomp` kazandigi hucre "sahne basina dagitim ise yariyor" kanitina sayilmaz; olsa olsa iki gecis yanliliginin bugunku varsayilaninin bu icerikte en iyi olmadigini soyler.

### Tekrar gurultusu — ayni hucre iki kez kosuldu

K4 ekindeki kazanclar pp cinsindendir; kazancin gurultunun ustunde olup
olmadigi ancak ayni parametreyle ikinci bir kosumla anlasilir. K4
izgarasindaki `kontrol` satiri bayt uzerinden olcer, bu tablo pp uzerinden.

| Kol | Pencere | sha256 | Boyut farki (bayt) | MAE kosum 1 | MAE kosum 2 | Tekrar gurultusu (pp) |
|-----|---------|--------|--------------------|-------------|-------------|-----------------------|
| yedek | `p1-karisik` | farkli | -60414 | 1.273 | 1.275 | 0.002 |

Kosulan hucre 1; olculen en buyuk tekrar gurultusu 0.002 pp.
`zones`in kazandigi 0.044 pp gurultunun 22.0 kati; gurultunun ustunde.

Uretim: `SahneButcesi tekrar <kol> <pencere>`, ham dosya `tekrar-<kol>-<pencere>.csv`.
Yalniz `zones`in kazandigi hucrede kosuldu; diger hucrelerin maliyeti
hucre basina bir tam iki gecisli kodlamadir.

## K5 ve K6 — kalite kazanci ve hedef boyut

Kapi (olcumden once): p10 kazanci `>= +0,50`, en kotu sahne kazanci
`>= +1,00`, ayni iki kaynakta; hicbir kaynakta p10 kaybi `> 0,30`;
her kosum hedef bandin icinde ve asan kosum orani %0.

**Kapinin "kaynak" dedigi sey burada "pencere"dir.** `ESIKLER.md`
uc ayri kaynak varsayarak yazildi; elde uc pencere var ve ucu de ayni
dosyadan kesildi. Kapi bu haliyle yazildigindan daha zayiftir: iki
pencerede tutan bir kazanc, iki ayri kaynakta tuttugunu gostermez.
Esik olcumden once sabitlendigi icin degistirilmedi, sinir burada yazili.

**Puanlar yalniz kol icinde karsilastirilabilir.** Plan cozunurlugu kola
gore degisiyor (T107 sonrasi ayni pencerede libx264 `1458x820`,
libx265 `1728x972`, libsvtav1 `1920x1080`); farkli cozunurlukten cikan
VMAF puanlari yan yana konmaz. `taban` ile `dagitim` ayni kolda ayni
cozunurluktedir — A/B icinde bu sorun yoktur.

| Yazilim kolu | Pencere | Kol | Boyut (MB) | Band | Band icinde | VMAF-NEG ort. | p10 | en dusuk kare | en kotu sahne |
|--------------|---------|-----|------------|------|-------------|---------------|-----|---------------|---------------|
| uyumlu | `p1-karisik` | taban | 58.66 | 58.3–60.0 | evet | 72.538 | 69.074 | 22.119 | 41.296 |
| uyumlu | `p1-karisik` | dagitim | 58.71 | 58.3–60.0 | evet | 72.500 | 69.081 | 19.215 | 41.197 |

### Dagitimin gercekte ne kadar oynadigi

Zone carpani `1,0` demek "bu sahneye kodlayicinin verecegi kadar ver"
demektir. Carpanlarin araligi dar kaldiginda dagitimin kaliteye
yapabilecegi etki de dar kalir; asagidaki fark sutunu kazanc
beklentisinin ust sinirini gosterir.

| Yazilim kolu | Pencere | Zone sayisi | En kucuk b | En buyuk b | Aralik |
|--------------|---------|-------------|------------|------------|--------|
| uyumlu | `p1-karisik` | 28 | 0.842 | 1.279 | 0.437 |
| uyumlu | `p2-durgun` | 6 | 0.944 | 1.207 | 0.263 |

### Dagitimli − dagitimsiz

Bir satir bir **yazilim kolu x pencere** ciftidir. Boyut farki sutunu
A/B'nin adil olup olmadigini gosterir: iki kol ayni boyutta degilse
kalite farki dagitimdan degil bit farkindan gelebilir.

| Yazilim kolu | Pencere | Δ ortalama | Δ p10 | Δ en kotu sahne | Δ boyut (MB) |
|--------------|---------|------------|-------|-----------------|--------------|
| uyumlu | `p1-karisik` | -0.039 | +0.007 | -0.099 | +0.04 |

**K5/K6 kapisi gecmedi** — olculen cift 1; p10 esigini (>= +0,50) gecen 0, en kotu sahne esigini (>= +1,00) gecen 0, esikten fazla p10 kaybeden 0; olculen 2 kosumdan hedefi **asan** 0, bandin **altinda** kalan 0.

Esik metni "uc kaynagin en az ikisinde" der; kaynak = pencere. Kollar
esikten sonra eklendi, o yuzden sayim **kol icinde** yapilir: bir kolun
kendi uc penceresinin en az ikisi esigi gecmelidir. Ayri kollardan birer
pencere toplanip "iki kaynak" sayilmaz. Bu netlestirme commit `a965416`,
ilk `k5-*.json` yazilmadan once: `git log -1 --format=%cI a965416` ile
`.calisma/T114/k5-*.json` zaman damgalari karsilastirilabilir.

| Yazilim kolu | Olculen pencere | p10 esigini gecen | En kotu sahne esigini gecen | Kalite sarti (1-3) |
|--------------|-----------------|-------------------|-----------------------------|--------------------|
| uyumlu | 1/3 | 0 | 0 | **hayir** |

Kalite sartlari (1-3) tek basina: **saglanmadi** (0 kolda saglandi, esikten fazla p10 kaybeden 0). K6 sarti (4) tek basina: **saglandi**.

Hedefi asan kosum orani: 0.0%
(0/2). Bandin altinda kalma bu duzenegin ozelligidir:
`EncodeRunner`'in kapali dongu duzeltmesi kosmuyor, tek iki gecis var.
Iki kol da ayni duzenekten geciyor, bu yuzden band disiligi kollari
**ayirt etmez**; K6'nin asil sorusu olan asan kosum orani ayri yazildi.

## K7 — harita yanlisken dagitimin bedeli

Uretim: `SahneButcesi k7 <kol> <pencere>`; ham dosya
`k7-<kol>-<pencere>.json` ve `.zones.txt`.

**bilinmiyor** — K7 kosulmadi.

## Karari veren kodun kendisi olculdu

Asagidaki karar bir programdan cikiyor; o program hep "gecti" diyorsa
sayfadaki butun sayilar bosa gider. Bu yuzden kapi kodu uydurma girdiyle
kosuldu: once dort sartin da saglandigi bir girdi (karar **degismeli**),
sonra her seferinde tek bir sarti bozan girdiler.

| Senaryo | Ne degisti | Beklenen karar | Cikan karar | Sonuc |
|---------|------------|----------------|-------------|-------|
| `temiz` | dort kapi da saglaniyor | girer | girer | gecti |
| `p10-kaybi` | bir pencerede p10 kaybi 0,50 (esik 0,30) | girmez | girmez | gecti |
| `band-asan` | bir kosum hedef bandin ustunde | girmez | girmez | gecti |
| `k7-bedeli` | bozuk harita kaybi kendi hucresinin kazancini asiyor | girmez | girmez | gecti |
| `olcum-yok` | k5 ve k7 dosyasi yok | karar-yok | karar-yok | gecti |

Denenen senaryo 5, beklenen karari veren 5. Uretim
`SahneButcesi rapor` cagrisidir; girdi `tools/sahne-butcesi/kapi-fikstur.py`,
kosum `bash tools/sahne-butcesi/04-kapi-denemesi.sh`. Bu tablodaki sayilar
uydurma; olculen sey kapinin **ayirt edip etmedigi**.

### Tekrar gurultusunu yorumlayan cumle de olculdu

`zones`in kazanci gurultunun ustunde mi altinda mi — bunu bir cumle
soyluyor. O cumle hep ayni seyi diyorsa hukum de bosa gider. Uydurma
tekrar dosyasiyla uc senaryo kosuldu: gurultu sifir, kazanctan kucuk,
kazanctan buyuk.

| Senaryo | Kosum 1 (pp) | Kosum 2 (pp) | Beklenen cumle | Cikan cumle | Sonuc |
|---------|--------------|--------------|----------------|-------------|-------|
| `gurultu-sifir` | 1.273 | 1.273 | ustunde | ustunde | gecti |
| `gurultu-kucuk` | 1.273 | 1.293 | ustunde | ustunde | gecti |
| `gurultu-buyuk` | 1.273 | 1.373 | altinda | altinda | gecti |

Denenen senaryo 3, beklenen cumleyi veren 3. Girdi
`tools/sahne-butcesi/tekrar-fikstur.py`, kosum
`bash tools/sahne-butcesi/06-tekrar-denemesi.sh`. Bu tablodaki pp degerleri
uydurma; olculen sey yorumun **yon degistirip degistirmedigi**.

## Sonuc

**Karar verilemedi.** Asagidaki kapilardan en az biri olculemedi; olculmemis kapi gecmemis sayilmaz, `bilinmiyor` kalir.

- K2 (kodlayici zaten dogru dagitiyor mu): kapanmadi
- K5/K6 (kalite kazanci ve hedef boyut): **gecmedi** — olculen cift 1; p10 esigini (>= +0,50) gecen 0, en kotu sahne esigini (>= +1,00) gecen 0, esikten fazla p10 kaybeden 0; olculen 2 kosumdan hedefi **asan** 0, bandin **altinda** kalan 0
- K7 (bozuk harita bedeli): **bilinmiyor** — olculmedi

**Sozlesmenin sorusu "haritayi plana baglamali miyiz" idi. Olculen 8 hucrenin 5 tanesinde harita kodlayiciyi geride birakmiyor**: `MAE(verilen) <= MAE(harita)` (K2 tablosu), yani o hucrelerde kodlayicinin kendi dagitimi haritanin onerisi kadar ya da ondan daha dogru. Plana baglanacak sey haritanin sahne basina sayilaridir; o sayilar cogunlukta kodlayicinin kendi kararindan daha iyi degilse, baglamanin tasiyacagi bilgi de yoktur. Sozlesmenin "olculdu, kodlayici zaten daha iyi dagitiyor" secenegi bu satirdan okunur.


**Bu sayi sahne sayisina gore ikiye ayrilir ve yon degistirir.** Dort sahneden az olan pencerede dagitilacak sahne neredeyse yoktur; o hucreler sayfada zaten `anlamsiz` diye isaretli. Sahne sayisi dort ve ustu olan 5 hucreye bakildiginda harita 3 tanesinde kodlayiciyi geciyor, 2 tanesinde gecmiyor. Yani "5/8" cogunlugunu kucuk pencereler tasiyor. Ikisi de dogru ve ikisi de burada: genis pencerede harita daha sik onde, ama K5'in kalite kapisi bu onculugun kullaniciya bir sey kazandirdigini gostermedi.
Dagitim parametresinin kendisi ayri bir sorudur ve ayri olculdu. **Sahne basina dagitim 5 hucrenin 1 tanesinde tabani gecti; kazanc 0.044 pp (yedek/p1-karisik), K1 aciginin %18.0'i.** Haritanin sahne basina sayilarini kodlayiciya tasiyan tek aday `zones`; olculen 5 hucrenin tabani gecen 4 tanesinde `zones` 1 kez kazandi, `qcomp` 3 kez. `qcomp` tek bir kuresel skalerdir, `SceneMap` olmadan da verilebilir — kazandigi hucre dagitimin degil, iki gecis yanliliginin bugunku varsayilaninin bu icerikte en iyi olmadiginin kanitidir. `zones` 5 hucreden 1 tanesinde kazandi ve en buyuk kazanc 0.044 pp; bu buyukluk tek basina karar tasimaz, karari K5'in kalite kapisi verir.

**Bu kazanci bugunku varsayilan yol alamaz:** uretimin varsayilan kodlayicisi `libsvtav1` `zones` parametresini hic okumuyor (K4 izgarasi). Yani olculen kazanc, kullanicinin varsayilan ayarlarla yaptigi sikistirmaya ulasmiyor; ancak kodlayici elle `libx265` ya da `libx264` secildiginde gorunur.

Bu bulgu K4'un izgarasiyla yan yana okunmali: `zones` denenen 5 kodlayicinin yalniz 2 tanesinde calisiyor (`libx265`, `libx264`); uretimin varsayilan kodlayicisi (`libsvtav1`) parametreyi sessizce yok sayiyor, nvenc kollarinda parametre hic yok. Yani dagitimin lehine cikan her kanit bes kodlayicinin ikisiyle ve varsayilan olmayan yolla sinirlidir. Ikisi birlikte: elde `zones` lehine 1 hucrelik kucuk bir isaret var ve o isaret zaten uretimin varsayilan yolunda gecerli degil.

Kapilarin sayisal esikleri `tools/sahne-butcesi/ESIKLER.md` icinde ve
bu olcumden onceki commit'te sabitlendi.

## Olculemeyenler

Asagidaki hucreler bir sayi uretmedi. Hicbiri varsayilana dusurulmedi ve
hicbiri ortalamaya karistirilmadi; kapilarda "gecmedi" degil `bilinmiyor`
sayilirlar. Sebep sutunu olcumun kendi ciktisindan gelir, elle yazilmadi.

| Bolum | Hucre | Sebep |
|-------|-------|-------|
| K1/K2 | yedek/p2-durgun | plan passthrough (hevc); kodlama yok, sahneye bit dagitilmiyor |
| K1/K2 | yedek/p2-durgun | referans bit toplami sifir; hucre karara girmedi |
| K4 eki | maks/p1-karisik `zones` | libsvtav1 zone parametresini yok sayiyor (K4 izgarasi) |
| K4 eki | maks/p1-karisik `qcomp` | duzenek olcmedi: qcomp libsvtav1'de calisiyor (K4 izgarasi) ama duzenek her iki adayi da ZonesFlag'in bayragindan geciriyor |
| K4 eki | maks/p2-durgun `zones` | libsvtav1 zone parametresini yok sayiyor (K4 izgarasi) |
| K4 eki | maks/p2-durgun `qcomp` | duzenek olcmedi: qcomp libsvtav1'de calisiyor (K4 izgarasi) ama duzenek her iki adayi da ZonesFlag'in bayragindan geciriyor |
| K4 eki | maks/p3-hareketli `zones` | libsvtav1 zone parametresini yok sayiyor (K4 izgarasi) |
| K4 eki | maks/p3-hareketli `qcomp` | duzenek olcmedi: qcomp libsvtav1'de calisiyor (K4 izgarasi) ama duzenek her iki adayi da ZonesFlag'in bayragindan geciriyor |
| K4 eki | yedek/p2-durgun `zones` | plan passthrough (hevc) |
| K4 eki | yedek/p2-durgun `qcomp` | plan passthrough (hevc) |
| K5/K6 | maks/p1-karisik | kosulmadi: `k5-*.json` yok |
| K5/K6 | maks/p2-durgun | kosulmadi: `k5-*.json` yok |
| K5/K6 | maks/p3-hareketli | kosulmadi: `k5-*.json` yok |
| K5/K6 | uyumlu/p2-durgun | kosulmadi: `k5-*.json` yok |
| K5/K6 | uyumlu/p3-hareketli | kosulmadi: `k5-*.json` yok |
| K5/K6 | yedek/p1-karisik | kosulmadi: `k5-*.json` yok |
| K5/K6 | yedek/p2-durgun | kosulmadi: `k5-*.json` yok |
| K5/K6 | yedek/p3-hareketli | kosulmadi: `k5-*.json` yok |
| K7 | maks/p1-karisik | kosulmadi: `k7-*.json` yok |
| K7 | maks/p2-durgun | kosulmadi: `k7-*.json` yok |
| K7 | maks/p3-hareketli | kosulmadi: `k7-*.json` yok |
| K7 | uyumlu/p1-karisik | kosulmadi: `k7-*.json` yok |
| K7 | uyumlu/p2-durgun | kosulmadi: `k7-*.json` yok |
| K7 | uyumlu/p3-hareketli | kosulmadi: `k7-*.json` yok |
| K7 | yedek/p1-karisik | kosulmadi: `k7-*.json` yok |
| K7 | yedek/p2-durgun | kosulmadi: `k7-*.json` yok |
| K7 | yedek/p3-hareketli | kosulmadi: `k7-*.json` yok |

Toplam 27 satir, 4 bolumde: K1/K2 2, K4 eki 8, K5/K6 8, K7 9.

"Kosulmadi" diyen 17 satirin sebebi tek: olcum penceresi
icinde sira gelmedi. Kapasite eksigi degil, sure eksigi.
Olculen 1 K5 hucresinin gozlenen suresi (ilk kodlama
dosyasindan sonuc JSON'una) en fazla 36 dk
(`uyumlu-p1-karisik`); hucre basina iki tam iki gecisli kodlama arti iki VMAF
kosumu vardir. Bu sure dosya zaman damgalarindan hesaplandi.

## K9 — kural koda girdiyse mutasyon kaniti

`src/VidShrink.Core/SceneBudget.cs` **yok**: dagitim kurali uretim koduna
girmedi, dolayisiyla kiracak yeni bir olcu de yok. Kural duzenekte kaldi
(`tools/sahne-butcesi/Butce.cs`) ve orada denetleniyor — K3 ekindeki uc
mutasyon o kurali kiriyor.

Sozlesmenin dogrulama komutu su an `[dotnet test -c Release --filter "PlanCalculatorTests"]`.
Ilk halinde filtre `"SceneBudgetTests|PlanCalculatorTests"` idi;
`SceneBudgetTests.cs` agacta olmadigi icin o kol **sifir test esliyor**
ve sessizce geciyordu. Sessiz gecen kol birakilmadi, filtreden cikarildi.
Kosan testlerin hepsi `PlanCalculatorTests`'tir; "verify yesil" cumlesi
bu sayfada yalnizca onu kapsar.

Mutasyon duzenegi silinmedi; kosuldugunda sessizce gecmek yerine
reddettigi gorulsun diye ciktisi buraya alindi:

```
/c/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/T114/src/VidShrink.Core/SceneBudget.cs yok — dagitim kurali koda girmedi, mutasyon kaniti uygulanamaz.
```

## Bu sayfanin bilinen sinirlari

1. **Uc pencere tek kaynaktan.** Icerik rejimi uc ayri (kesik cok /
   durgun / kesintisiz hareket) ama kamera, kodlama gecmisi ve gren
   ayni. Kaynaklar arasi genelleme bu sayfadan cikmaz.
2. **`p3-hareketli` iki sahneli, `p2-durgun` alti.** Iki sahnede sira
   korelasyonu ve "ters dusen orani" anlamsizdir; tabloda isaretli.
   Istatistik agirligi tasiyan tek pencere 28 sahneli `p1-karisik`.
3. **Duzenek kapali dongu duzeltmesi kosmuyor.** `EncodeRunner`'in hedef
   boyut duzeltme dongusu yok, tek iki gecis var; band uyeligi urunun
   degil duzenegin ozelligi. K6'nin asil sorusu olan **asan** kosum
   orani ayri yazildi.
4. **Referans sahneleri `-ss` ile kesiliyor.** Kesim noktasi kare
   sinirina yuvarlanabilir; hata butun sahnelerde ayni yonde ve paylar
   normalize edildigi icin kucuk, ama sifir degil.
5. **`libsvtav1` kolunda dagitim hic denenemedi.** K4 zone parametresini
   sessizce yok saydigini gosterdi; uretimin varsayilan kodlayicisi bu.
   Dagitim koda girse bile varsayilan yolda **etkisiz kalirdi**.
6. **Referans calisma noktasi planinkiyle ayni degil.** Sabit
   `CRF 26` her kodlayicida farkli bir bit hizina
   dusuyor; her K1 basliginda `referans/plan` orani yazili. Oran 1'den
   uzaklastikca "hak edilen" dagitimi planin gercek calistigi hizdan
   uzak bir noktada olculmus olur — sira genelde korunur ama paylar
   hizla birlikte kayar. Kollar arasi hak-edilen kiyasi bu yuzden
   yapilmaz; her kol kendi referansiyla karsilastirilir.
7. **Kuralin oynatabildigi aralik dar.** Zone carpani `1,0` etrafinda
   kaliyor: en genis pencere `p1-karisik` 0.437,
   en dar `p3-hareketli` 0.004 (K3 eki tablosu).
   Kazancin ust siniri bu araliktan gelir; `gamma`yi buyutmek araligi
   acardi ama `gamma = 1 - qcomp` turetilmis bir sayidir, telafi sabitine
   cevrilmedi.

