# Bekleme Bütçeleri: Vazgeçme Süresi Ölçüsü

Makine: DESKTOP-0J80KVV, Windows 11 22631. Tarih: 18 Eylül 2026. Dal `t0/yol-d-orta-a`,
taban `94d1f46a`. Yapay yük yok.

Kullanıcının masaüstündeki kurulumuna ve `%APPDATA%\VidShrink`'e dokunulmadı; gerçek
HKCU'ya yazılmadı. Testler kendi geçici kurulum kökünü kuruyor (`BekleyenKlasoru`),
günlükler `.calisma/` altına düştü.

## Ölçülen İddia

`KurulumBekleyeni` üç bekleme bütçesini koldan koda çeviriyor:

```
YuvaBeklemesi(elle)  => elle ? 3 sn  : 0
IndirmeKilidi(elle)  => elle ? 20 sn : 0
KurulumKilidi(elle)  => elle ? 20 sn : 10 dk
```

Yol D denetçisinin ORTA-A bulgusu: bu üç işlev gövdesi mutasyona uğradığında hiçbir test
kırmızıya dönmüyordu. Eski pim (`ElleBeklemeKurmaParametresineGidiyor`) sabiti sabitle
karşılaştırıyordu; elle kolu ölçen tek test de yuvanın kısa devresinde durduğu için
indirme ve kurulum kilitleri o kolda hiç koşmuyordu.

## Düzenek

`tests/VidShrink.Tests/BaslaticiPanelsizTests.cs`. Her ölçü ilgili mutex'i testin kendi
iş parçacığında tutuyor (`MutexTutucu`), üretim çağrısını ayrı bir iş parçacığında
başlatıyor (`Arka`) ve **vazgeçme süresini** duvar saatiyle sayıyor. Hangi kolun koştuğunu
ölçünün kendisi kanıtlıyor: bekleme boyunca indirme sayacı 0'da, `app\a.txt` `v1`'de,
sürüm işareti boşta kalıyor; aynı çağrı kilit boşken indirmeyi 1'e çıkarıp `a.txt`'yi
`v2`, işareti `9.9.9` yapıyor.

`Calistir`/`Kur` dönüş değeri "başlatıcı geçişi gerekiyor mu" demek, "kurulum oldu mu"
demek değil; sahnede başlatıcı dosyası olmayan düzenekte kurulum başarılıyken de `false`
dönüyor. Bu yüzden ölçü dönüş değerine değil gözlenebilir etkiye bakıyor.

## Sayılar

| Bütçe | Kol | Tutulan kilit | Ölçülen vazgeçme | Pin |
| --- | --- | --- | --- | --- |
| `YuvaBeklemesi` | elle (3 sn) | bekleyen yuvası | 3030 ms | `YuvaButcesiVazgecmeSuresiniBelirler` |
| `YuvaBeklemesi` | arka plan (0) | bekleyen yuvası | 0 ms | aynı test |
| `IndirmeKilidi` | elle (20 sn) | güncelleme kilidi | 20000 ms | `IndirmeKilidiButcesiVazgecmeSuresiniBelirler` |
| `IndirmeKilidi` | arka plan (0) | güncelleme kilidi | 0 ms | aynı test |
| `KurulumKilidi` | elle (20 sn) | güncelleme kilidi | 20011 ms | `ElleKurulumKilidiButcesiVazgecmeSuresiniBelirler` |
| `KurulumKilidi` | arka plan (10 dk) | güncelleme kilidi | vazgeçmiyor; 6 sn tutmada bekliyor, kilit bırakılınca kuruyor (6002 ms) | `ArkaPlanKurulumKilidiKisaTutmadaVazgecmez` |

Elle "Yükle" yolunun uygulamayı doğurma süresi (`ElleYukleArkaPlanIndirirkenAcilisiGeciktirmez`,
gerçek başlatıcı süreci, yuva başka kopyada tutuluyor):

```
3110  3115  3129  3138  3142  3155  3178   ms
n=7, ortanca 3138 ms, aralık 3110-3178 ms
son üç koşum zorla temiz derlenmiş ikili üzerinde
```

Bu sayı daha önce hiçbir belgede yazılı değildi; `Updater.cs` docstring'inde tek örnek
olarak "3128 ms" geçiyordu. Yeniden ölçüldü: 3128 ms bu aralığın içinde, tek koşumun
değeri olarak doğru ama ortanca 3138 ms.

## Mutasyon Tablosu

Her mutasyon `src/VidShrink.Launcher/KurulumBekleyeni.cs` içindeki **işlev gövdesine**
uygulandı, `VidShrink.Launcher` ve test projesi `--no-incremental` derlendi, sonra
`BaslaticiPanelsizTests` koşuldu. Düzenek: `tools/bekleme-butceleri/mutasyon.sh`
(ham günlük varsayılan olarak `.calisma/bekleme-butceleri/mutasyon.log`).

| # | Mutasyon | Sonuç | Kırmızıya dönen test | Kırmızı çıktısı |
| --- | --- | --- | --- | --- |
| M1a | `YuvaBeklemesi(elle) => TimeSpan.Zero` | öldü | `YuvaButcesiVazgecmeSuresiniBelirler` (3 ms) | `Assert.InRange() Failure: Value not in range / Actual: 0` |
| M1b | `YuvaBeklemesi(elle) => TimeSpan.FromSeconds(30)` | öldü | `YuvaButcesiVazgecmeSuresiniBelirler` (14 s) | `arka plan turu yuva tutuluyorken 1999 ms sonra hâlâ bekliyordu` |
| M2a | `IndirmeKilidi(elle) => TimeSpan.Zero` | öldü | `IndirmeKilidiButcesiVazgecmeSuresiniBelirler` (4 ms) | `Assert.InRange() Failure: Value not in range / Actual: 0` |
| M2b | `IndirmeKilidi(elle) => TimeSpan.FromSeconds(60)` | öldü | `IndirmeKilidiButcesiVazgecmeSuresiniBelirler` (5 s) | tek kırmızı, aralık ihlali |
| M3a | `KurulumKilidi(elle) => TimeSpan.Zero` | öldü | `ElleKurulumKilidiButcesiVazgecmeSuresiniBelirler` (7 ms) + `ArkaPlanKurulumKilidiKisaTutmadaVazgecmez` (7 ms) | `Assert.InRange() Failure: Value not in range / Actual: 4` |
| M3b | `KurulumKilidi(elle) => TimeSpan.FromSeconds(60)` | öldü | `ElleKurulumKilidiButcesiVazgecmeSuresiniBelirler` (35 s) | `elle kurulum kilidi bütçesini aştı, 35000 ms sonra hâlâ bekliyordu` |

M1b ayrıca `ElleYukleArkaPlanIndirirkenAcilisiGeciktirmez` ve `IkinciBekleyenIndirmezBeklemez`
testlerini de kırmızıya çeviriyor. Temiz ağaçta aynı süzgeç:

```
Başarılı!  - Başarısız:     0, Başarılı:    29, Atlanan:     0, Toplam:    29, Süre: 1 m 23 s
```

## ORTA-B Ve ORTA-C: Yeri Olmayan İki Bulgu

Bu turda kapatılması istenen diğer iki bulgunun ağaçta yazılı bir yeri yok; ikisi de
bağımsız taramayla arandı.

**ORTA-B (yanlış atfedilen öldüren test).** `ORTA-B` etiketi `docs/` ve `.claude/` altında
hiç geçmiyor; `.claude/relay/audits/` yalnız daha eski T-numaralı denetimleri tutuyor.
`docs/` altındaki tek mutasyon atıf tablosu `docs/olcumler/ornekleme.md:410-420` ve başka
bir yola ait; orada bir yanlışlık gösteren kanıt bulunamadı. Bu turda düzeltilen gerçek
yanlış atıf `Updater.cs` docstring'indeydi: tek bir ölçü ("BaslaticiPanelsizTests,
3128 ms") üç bekleme bütçesinin hepsinin ölçüsü gibi yazılmıştı; oysa o ölçü yuvanın kısa
devresini sayıyor ve indirme ile kurulum kilitleri o kolda hiç koşmuyor. Her sayı artık
kendisini üreten testle birlikte yazılı.

**ORTA-C ("3 sn pompa bütçesi" gerekçesi).** Böyle bir gerekçe hiçbir yerde yazılı değil:
`pompa bütçe`, `pump budget`, `3 saniyelik pompa`, `3 sn'lik pompa` desenleri repo genelinde
sıfır sonuç veriyor. `PlaybackStripShowDelay` gerçekten `00:00:00`
(`src/VidShrink.App/Themes/Playback.axaml:121`) ama yanındaki gerekçe zaten doğru:
`:43` "ölçü değil: şerit beklemeden belirir (T70/K5)", ve pim
`tests/VidShrink.Tests/ComparisonPanelTests.cs:691` `Assert.Equal(TimeSpan.Zero, stripShow)`.
"3" sayısının pompayla yan yana durduğu tek yer
`tests/VidShrink.Tests/OynaticiGercekGirdiTests.cs:88`
`DenetimSurucu.Pump(view, () => view.SeritRevealed, 3)` — bu bir bütçe değil, bekleme üst
sınırı; ikizinde (`OynaticiYolHaritasiTests.cs:716`) aynı koşul 2 saniyeyle pompalanıyor ve
ikisinin de yazılı gerekçesi yok. Düzeltilecek yanlış bir gerekçe olmadığı için hiçbir
satır değiştirilmedi; bulgu burada kayda geçiyor.

## Artımlı Derleme Tuzağı

İlk turda `ElleKurulumKilidi...` pimi yerelde kararsız göründü: 35 s'de bitmiyor, 90 s
bütçeyle 60015 ms'de vazgeçiyordu. Kuramsal üst sınır 30 s (klasörün boşalması 10 sn +
kilit 20 sn) olduğu için sayı üretimde bir kusura işaret ediyor gibiydi. Aşamaları tek tek
ölçen geçici bir teste göre `BosalincaAl` 4 ms'de dönüyor, `WaitOne(20 sn)` 20006 ms'de
vazgeçiyor, ama `WaitOne(KurulumKilidi(elle: true))` 44998 ms bekleyip kilit bırakılınca
`true` dönüyordu. Kaynak temizdi; **derlenmiş `VidShrink.Launcher.dll` bir önceki mutasyon
turundan kalma `KurulumKilidi => TimeSpan.FromSeconds(60)` gövdesini taşıyordu.** 60015 ms
o mutasyonun kendisiydi. `--no-incremental` derlemeden sonra ölçü 20003 ms'ye oturdu.

Sonuç: mutasyon turu `.bak`'tan geri yüklenince artımlı derleme değişikliği görmüyor;
mutasyon betiği her adımda **zorla** yeniden derlemek zorunda.
