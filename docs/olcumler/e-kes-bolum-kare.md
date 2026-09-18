# E: `--kes` Kare Eki ve `--bolum` — Gerçek Koşum Kanıtı

Plan `docs/plan.md`. Boşluk kurulu HandBrake 1.11.2 `--help` ile saptandı
(`docs/olcumler/handbrake-cli-yetenek.md:84-104`): `-c/--chapters 1-3` ve
`--start-at/--stop-at frames:`. İkisi de bizde yoktu.

## Kaynak

`.calisma/bolum/kaynak.mkv` — 60 s, 640x360, 30 fps, h264 + aac. Üç bölüm işareti
ffmetadata ile gömüldü; ffprobe okuyor:

```
chapter,1,1/1000000000,0,0.000000,12000000000,12.000000,Giris
chapter,2,1/1000000000,12000000000,12.000000,30000000000,30.000000,Orta
chapter,3,1/1000000000,30000000000,30.000000,60000000000,60.000000,Kapanis
```

## Ölçüm

Dört koşum, hepsi `vidshrink kucult ... --hedef 3MB --olcumsuz`. Çıktı süresi ve
baytı ffprobe'dan:

| kol | beklenen pencere | çıktı süresi | bayt |
| --- | --- | --- | --- |
| `--bolum 2` | 12–30 s | 18.100 | 889034 |
| `--bolum 2-3` | 12–60 s | 48.023 | 2777318 |
| `--kes 360f-900f` | 12–30 s (30 fps) | 18.100 | 889034 |
| `--kes 12-30` | 12–30 s | 18.100 | 889034 |

Son üç satır **bayt bayt aynı**: bölüm işaretinden kurulan pencere, kareden
kurulan pencere ve saniyeden kurulan pencere aynı kesiti veriyor. Kare kolu
kaynağın hızına bölünüyor; 30 fps'te 360. kare 12. saniye.

Dördüncü satır olumsuz kontrol: eksiz sayı eskisi gibi saniye sayılıyor, yeni
kol eski yazımı kaydırmadı.

## Olumsuz kontroller

```
--bolum 4      → Bölüm numarası geçersiz ya da kaynakta yok: 4
--bolum 2-4    → Bölüm numarası geçersiz ya da kaynakta yok: 2-4
--bolum 1 (bölümsüz kaynakta) → Kaynakta bölüm işareti yok.
--kes 12-30 --bolum 2 → --kes ile --bolum birlikte verilemez; ikisi de kesit penceresi kurar.
```

İlk koşumda iki metin kusuru çıktı ve düzeltildi: aralık dışı bölüm cümlesi
`{0}` yer tutucusunu ham basıyordu (çözüm yolu `text[...]` çağırıyordu,
`text.Format` değil), birleşim cümlesi ise anlamsız bir sayı ekliyordu
(`... birlikte verilemez: 2`). İkisi de teste bağlandı
(`KesBolumKareTests.AralikDisiBolumCumlesiNumarayiTasiyor`,
`BirlikteVerilemezCumlesindeYerTutucuYok`).

## Mutasyon

| mutasyon | sonuç |
| --- | --- |
| `sf / info.Fps` → `sf` (kare hızına bölme kaldırıldı) | 3 kırmızı (`KareKaynaginHiziylaCevriliyor`, üç fps) |
| `Chapters[to - 1]` → `Chapters[from - 1]` (bölüm sonu indeksi) | 1 kırmızı (`BolumSinirlariKaynaktanGeliyor`) |

İki mutasyon da `0 Hata` ile derlendi; kırmızılar bayat ikiliden değil.

## Kapsam dışı

`pts:` bu turda yazılmadı: 90 kHz sayacı saniyeyle aynı bilgiyi veriyor,
kullanıcıya bir şey kazandırmıyor.
