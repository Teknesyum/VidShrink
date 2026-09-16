# Kurulum Çubuğu Açılış Atağı

Tarih: 16 Eylül 2026. Komut: `bench bar-burst 5` (`tools/VidShrink.Bench/CubukAtagi.cs`).
Pencere açılmadı: ölçüm `InstallProgress`'i doğrudan sürüyor. Adımlar
`UpdateStaging`'in gerçek sırası: `Step(0, 10)` manifest, manifest gelince `Step(12, 20)`.

## Neyi Ölçüyor

- **Kare aralığı:** `Thread.Sleep(16)` ile 250 tik. Bu makinede ortanca 30,9 ms, p90 31,6 ms:
  Windows'un bekleme çözünürlüğü 16 ms'lik kareyi iki katına çıkarıyor. `Advance(elapsed)`
  süreye göre ilerlediği için çubuk "ideal" (16 ms) ve "ölçülen" karelerde neredeyse aynı yerde.
- **Manifest gecikmesi:** GitHub'daki gerçek manifest adresine 5 istek; ortanca 370 ms (ilki 815 ms, soğuk bağlantı).
- **Senaryolar:** atak var/yok × manifest 370 ms'de geliyor/hiç gelmiyor × ideal/ölçülen kare.
  "atak=yok" ölçüm için atak kareleri önceden tüketilerek kuruluyor.

## Sonuç

| senaryo (ölçülen kare) | 100 ms | 200 ms | 384 ms | 1000 ms | %5'e | %8'e |
|---|---|---|---|---|---|---|
| manifest 370 ms, atak var | 4,19 | 6,97 | 9,17 | 13,09 | 136 ms | 323 ms |
| manifest 370 ms, atak yok | 0,38 | 0,83 | 2,10 | 12,45 | 464 ms | 588 ms |
| manifest yok, atak var | 4,19 | 6,97 | 8,65 | 8,93 | 136 ms | 323 ms |
| manifest yok, atak yok | 0,38 | 0,83 | 1,35 | 3,15 | 1861 ms | — |

Atak 24 kare = 384 ms. Çubuk atakla %5'e 136 ms'de, ataksız 464 ms'de varıyor (manifest
370 ms'de gelirse); manifest hiç gelmezse ataksız %5 1,86 saniye sürüyor. Atak tavanı
geçmiyor: manifest yokken çubuk %10 tavanının altında, 2 saniyede %9,27'de.

## Ham Çıktı

```
kare-suresi-sabiti-ms 16
atak-kare 24 atak-ms 384
tik-16ms-gercek ortanca 30.92 p90 31.56 maks 36.62
manifest-gecikme-ms 815 370 361 363 373 ortanca 370
senaryo manifest=370 atak=var kare=ideal
  cubuk@ms 50:2.84 100:4.42 200:6.62 384:9.16 500:10.80 800:12.51 1000:13.08 1500:14.25 2000:15.23
  esige-ms 1%:32 5%:144 8%:320 10%:464 12%:624
senaryo manifest=370 atak=var kare=olculen
  cubuk@ms 50:3.18 100:4.19 200:6.97 384:9.17 500:10.97 800:12.59 1000:13.09 1500:14.30 2000:15.27
  esige-ms 1%:26 5%:136 8%:323 10%:464 12%:619
senaryo manifest=370 atak=yok kare=ideal
  cubuk@ms 50:0.24 100:0.41 200:0.75 384:2.05 500:6.89 800:11.39 1000:12.42 1500:13.71 2000:14.78
  esige-ms 1%:288 5%:464 8%:560 10%:704 12%:864
senaryo manifest=370 atak=yok kare=olculen
  cubuk@ms 50:0.27 100:0.38 200:0.83 384:2.10 500:7.25 800:11.66 1000:12.45 1500:13.77 2000:14.84
  esige-ms 1%:291 5%:464 8%:588 10%:712 12%:853
senaryo manifest=yok atak=var kare=ideal
  cubuk@ms 50:2.84 100:4.42 200:6.62 384:8.65 500:8.71 800:8.84 1000:8.93 1500:9.11 2000:9.26
  esige-ms 1%:32 5%:144 8%:320 10%:- 12%:-
senaryo manifest=yok atak=var kare=olculen
  cubuk@ms 50:3.18 100:4.19 200:6.97 384:8.65 500:8.72 800:8.85 1000:8.93 1500:9.12 2000:9.27
  esige-ms 1%:26 5%:136 8%:323 10%:- 12%:-
senaryo manifest=yok atak=yok kare=ideal
  cubuk@ms 50:0.24 100:0.41 200:0.75 384:1.34 500:1.75 800:2.60 1000:3.16 1500:4.32 2000:5.29
  esige-ms 1%:288 5%:1856 8%:- 10%:- 12%:-
senaryo manifest=yok atak=yok kare=olculen
  cubuk@ms 50:0.27 100:0.38 200:0.83 384:1.35 500:1.79 800:2.66 1000:3.15 1500:4.35 2000:5.32
  esige-ms 1%:291 5%:1861 8%:- 10%:- 12%:-
```