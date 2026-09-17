# HandBrake Kıyası: CLI Ürün Yolu (B1, B5)

## Düzenek

- Koşum: `handbrake-kiyas.yml` 35248903679, `isler=handbrakecli`, dal `t0/hb-a45-arm-kiyas`, commit `97f1fa68`.
- Kapılar ölçümden önce `d8eb6565`'te `tools/kalite-paketi-3/hb.ps1` içindeki `$script:CliKapi`'ye pimlendi; sonra değişmedi.
- Ürün: `vidshrink kucult <girdi> --hedef <MB> --json` (`src/VidShrink.Cli`, ölçümlü yol). Ürün çıkış kodu ekran 2000'de 2 (bant altı kabul), diğerlerinde 0.
- HandBrake: HandBrakeCLI 1.11.2, x265 slow 2 geçiş turbo, ürünün baytına eş (±%2). Hız kolu: HandBrake'in kendi SVT-AV1'i, tek geçiş, ürünün preset'iyle.
- Kaynaklar: Sintel 1080p (sha256 `97F1DBC6…8EFA`), kesitler karanlık, parlak, hareketli; ekran Xiph `Debugging_1920x1080_30fps_8bit_420.y4m` (sha256 `8004BE81…4E8E`).

## Kapılar (fable B1, B5)

| Kapı | Koşul |
|---|---|
| B1 VMAF-NEG | Δ (ürün − HB) ≥ −0,3 |
| B1 XPSNR | Δ ≥ −0,2 dB |
| B1 karanlık PSNR | Δ ≥ −0,5 dB |
| B1 CAMBI | Δ ≤ +1,0 |
| Eş bayt | HB baytı ürünün ±%2'si |
| B5 hız | ürün CLI toplam süresi / HB süresi ≤ 1,0 (kodlama oranı yalnız bilgi) |

## Sonuç

| Kesit | kbit | Ürün | Ürün kbps | HB x265 kbps | Bayt sapma % | Δ VMAF-NEG | Δ XPSNR | Δ karanlık PSNR | Δ CAMBI | B1 | x265 oran toplam (kodlama) | B5 x265 | SVT bayt sapma % | SVT oran toplam (kodlama) | B5 SVT |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| karanlık | 600 | libx265 slow | 601,7 | 601,5 | −0,03 | +1,09 | +0,36 | +0,22 | +0,146 | geçti | 2,67 (1,44) | **kaldı** | 0,95 | 0,21 (0,11) | geçti* |
| karanlık | 2000 | libx265 slow | 2003,6 | 1974,9 | −1,43 | +0,55 | +0,21 | +0,19 | +0,046 | geçti | 3,01 (1,86) | **kaldı** | 0,89 | 0,28 (0,18) | geçti* |
| parlak | 600 | libsvtav1 6 | 598,9 | 598,0 | −0,15 | +2,53 | +1,39 | +1,69 | +0,036 | geçti | 1,81 (0,55) | **kaldı** | −0,65 | 3,18 (0,97) | **kaldı** |
| parlak | 2000 | libsvtav1 6 | 2035,4 | 2039,9 | +0,22 | +0,26 | +0,37 | +1,19 | +0,042 | geçti | 1,84 (1,17) | **kaldı** | 1,97 | 4,34 (2,75) | **kaldı** |
| hareketli | 600 | libsvtav1 6 | 604,5 | 598,6 | −0,98 | +5,56 | +1,66 | +1,41 | +0,520 | geçti | 1,92 (0,82) | **kaldı** | −0,20 | 3,80 (1,64) | **kaldı** |
| hareketli | 2000 | libsvtav1 6 | 2020,8 | 2002,1 | −0,93 | +0,60 | +0,67 | +1,44 | +0,700 | geçti | 1,64 (0,66) | **kaldı** | −0,28 | 4,41 (1,78) | **kaldı** |
| ekran | 600 | libsvtav1 6 | 598,4 | 593,3 | −0,85 | +11,94 | +12,90 | +13,60 | −4,723 | geçti | 3,73 (1,24) | **kaldı** | 1,09 | 2,40 (0,80) | **kaldı** |
| ekran | 2000 | libsvtav1 6 | 1823,0 | 1814,1 | −0,49 | +4,82 | +22,13 | +24,74 | −0,784 | geçti | 3,68 (1,76) | **kaldı** | −9,39 | 3,24 (1,55) | **kaldı**† |

Son iki sütun ("SVT bayt sapma %", "SVT oran") ve "B5 SVT" hükmü **geçersiz: preset eşleme hatası** — aşağıya bakın.

**B1: 8/8 geçti.** Dört alt kapının hepsi her hücrede geçti; eş bayt 8/8 sağlandı.

**B5 (x265): 8/8 kaldı.** Toplam süre oranı 1,64–3,73. Yalnız kodlama süresi alınsa bile 3 hücre ≤1 (parlak 600, hareketli 600 ve 2000) ve 5 hücre >1 kalıyor. Farkın büyüğü ürünün ölçümlü deneme turlarından (1–4 deneme) geliyor.

**B5 (SVT): geçersiz — preset eşleme hatası.** Yukarıdaki iki SVT sütunu (SVT bayt sapma %, SVT oran) ve
"B5 SVT" hükmü geçerli değil: kol, ürünün x265 preset **adını** ("slow") HandBrake'in `svt_av1` kodlayıcısına
`--encoder-preset` olarak geçirdi. HandBrake orada sayı bekliyor; ad verildiğinde kendi varsayılanına düşüyor.
Geçerli sonuç aşağıdaki "B5 SVT (düzeltilmiş düzenek)" bölümünde.

## B5 SVT (düzeltilmiş düzenek)

- Koşum: `handbrake-kiyas.yml` 35257923394, `isler=handbrakecli-svt`, dal `t0/hb-a45-arm-kiyas`, commit `2c07ba77`.
- Düzeltme: `hb.ps1` içinde `HbSvtPresetNo` ürünün x265 preset adını SVT sayısına çeviriyor
  (veryslow→4, slower→5, slow→6, medium→8, fast→9, faster→10, veryfast→11, ultrafast→12; sayı geldiyse olduğu gibi).
  Ürün zaten `libsvtav1 6` seçtiği hücrelerde geçen değer değişmedi; karanlıkta `slow` → `6` oldu.
- Kapılar değişmedi: aynı `$script:CliKapi` (eş bayt ±%2, hız oranı tavanı 1,0).
- Yalnız SVT hücreleri koştu: `handbrakecli-svt` kolu ürün CLI + HB SVT + kapı satırı üretiyor, x265 ve negatif kolları yok.

| Kesit | kbit | Ürün kodlayıcı/preset | HB `--encoder-preset` | Ürün kbps | HB SVT kbps | Bayt sapma % | Eş bayt | Ürün kodlama/toplam sn | HB SVT sn | Oran toplam (kodlama) | B5 SVT | Δ VMAF-NEG | Δ XPSNR |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| karanlık | 600 | libx265 slow | 6 | 601,7 | 602,6 | +0,15 | evet | 77,6 / 144,4 | 28,6 | 5,05 (2,71) | **kaldı** | −2,03 | −0,82 |
| karanlık | 2000 | libx265 slow | 6 | 2003,0 | 2040,3 | +1,86 | evet | 146,6 / 224,1 | 30,2 | 7,42 (4,85) | **kaldı** | +0,01 | −0,25 |
| parlak | 600 | libsvtav1 6 | 6 | 600,1 | 604,6 | +0,75 | evet | 24,9 / 86,7 | 33,8 | 2,57 (0,74) | **kaldı** | +0,32 | −0,09 |
| parlak | 2000 | libsvtav1 6 | 6 | 2019,2 | 2000,9 | −0,91 | evet | 93,6 / 147,8 | 33,6 | 4,40 (2,79) | **kaldı** | +0,79 | +0,15 |
| hareketli | 600 | libsvtav1 6 | 6 | 604,6 | 607,9 | +0,55 | evet | 51,4 / 121,6 | 30,1 | 4,04 (1,71) | **kaldı** | +0,69 | +0,11 |
| hareketli | 2000 | libsvtav1 6 | 6 | 2010,2 | 2006,8 | −0,17 | evet | 52,3 / 114,9 | 29,9 | 3,84 (1,75) | **kaldı** | +0,22 | +0,28 |
| ekran | 600 | libsvtav1 6 | 6 | 598,4 | 605,1 | +1,12 | evet | 21,7 / 68,1 | 30,3 | 2,25 (0,72) | **kaldı** | +3,03 | +8,91 |
| ekran | 2000 | libsvtav1 6 | 6 | 1823,6 | 1657,7 | −9,10 | **hayır** | 41,7 / 83,8 | 29,2 | 2,87 (1,43) | **kaldı**‡ | +4,06 | +21,47 |

**B5 (SVT), düzeltilmiş: 0/8 geçti.** Toplam süre oranı 2,25–7,42. Yalnız kodlama süresi alınsa 2 hücre ≤1
(parlak 600, ekran 600), 6 hücre >1. Eski tablodaki karanlık hücrelerinin "geçti" görünmesi
düzenek hatasıydı: HB o hücrelerde 405–433 sn kodluyordu, doğru preset ile 28,6 ve 30,2 sn.

Kod değişikliği yapılmadı, sonuç olduğu gibi raporlanıyor.

İki uyarı, tabloyu okuyan için. Karanlık 600 satırında iki taraf aynı pikseli kodlamıyor: ürün 1574x670,
HB 1920x818. O satırın Δ VMAF-NEG ve Δ XPSNR sayıları eşit piksel sayısından gelmiyor, kalite sütunu
ürünün lehine değil. Süre oranları da iki ayrı koşucudan: aynı karar, aynı geometri, aynı deneme sayısı
ve aynı kbps ile ürünün karanlık hücreleri eski koşumda 46,0 ve 75,9 sn, yeni koşumda 77,6 ve 146,6 sn
kodladı. Hüküm ikisinde de kaldı, ama oran sabit bir ölçü değil; koşucu hızı raporun içine giriyor.

## Notlar

- \* Karanlık kesitte ürün x265 seçti ve kol ürünün preset'ini ("slow") HandBrake'in `svt_av1`'ine geçirdi. HB SVT süresi 405–433 sn çıktı; bu iki hücrenin SVT oranı hız kıyası olarak anlamlı değil. Düzenek 2c07ba77'de düzeltildi, geçerli sonuç "B5 SVT (düzeltilmiş düzenek)" bölümünde.
- † Ekran 2000'de HB SVT eş bayta oturmadı (−%9,39, 1651,8 kbps). Bu hücrenin SVT oranı ve kalite farkı eş bayt dışında.
- ‡ Düzeltilmiş koşumda da ekran 2000'de HB SVT eş bayta oturmadı (−%9,10, 1657,7 kbps): HandBrake'in bit hızı arayışı bu kesitte ürünün baytına yaklaşamıyor, iki bağımsız koşumda aynı yerde durdu.
- Ham ürün süreleri (kodlama/toplam sn): karanlık 46,0/85,3 ve 75,9/122,8; parlak 31,4/103,3 ve 92,7/146,2; hareketli 50,2/116,7 ve 53,7/133,1; ekran 21,0/63,1 ve 39,7/83,2. HB x265: 31,9; 40,8; 57,0; 79,3; 60,9; 81,4; 16,9; 22,6.
- Negatif kontrol: yarım bitli HB x265 her hücrede ürünün altında kaldı (`negatif_ayirdi=True` 8/8). Ölçer kaliteyi ayırıyor.
