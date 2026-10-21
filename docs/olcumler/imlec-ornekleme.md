# İmleç Örneklemesi

Kayıt sonrası yakınlaştırma (later 114) için kayıt sırasında imleç konumu `PointerSampler` ile
`GetPhysicalCursorPos`'tan 30 Hz'de, BelowNormal öncelikli tek bir iş parçacığında okunur; tık ve
tuşlar zaten açık olan girdi kancasından gelir. Bu ölçü örneklemenin kayıt yükünü artırıp
artırmadığına bakar.

Komut: `dotnet run --project tools/VidShrink.Bench -c Release -- pointer-sampling --seconds 30 --runs 3`
(2026-10-21, 16 çekirdek, Windows 11, aynı makinede paralel çalışan başka ajanlar vardı).

## Örnekleyicinin Kendi Yükü

Süreç CPU zamanı (`Process.TotalProcessorTime`) örnekleyici kapalıyken ve açıkken 30 sn boyunca.

| Kol | Okuma | Gerçek hız | CPU |
|---|---|---|---|
| Kapalı | 0 | — | 0,0 ms |
| 30 Hz | 910 | 30,3 Hz | 0,0 ms |
| 120 Hz | 1933 | 64,4 Hz | 0,0 ms |

Windows süreç CPU sayacı 15,6 ms adımla ilerler; 30 sn'de bir adım bile dolmadı. Yani örnekleyici
saniyede 0,5 ms'den az CPU harcıyor, tek çekirdeğin binde beşinden az. 120 Hz istenince gerçek hız
64 Hz'de kalıyor (`Thread.Sleep` zamanlayıcı çözünürlüğü); uygulama 30 Hz kullandığı için önemli değil.

## Kayıtta Düşen Kare

gdigrab, 1280x720, 30 fps, 15 sn, libx264 ultrafast, `-threads 2`. Turlar sırayı değiştirerek
koşuldu (kapalı-açık, açık-kapalı, kapalı-açık).

| Tur | Örnekleyici | Kare | Düşen | ffmpeg CPU | Okuma |
|---|---|---|---|---|---|
| 1 | Kapalı | 449 | 1 | 172 ms | 0 |
| 1 | Açık | 450 | 0 | 266 ms | 457 |
| 2 | Açık | 450 | 0 | 62 ms | 458 |
| 2 | Kapalı | 450 | 0 | 203 ms | 0 |
| 3 | Kapalı | 449 | 1 | 156 ms | 0 |
| 3 | Açık | 446 | 4 | 1359 ms | 459 |

Aynı günün ilk koşusu (`--seconds 10 --runs 2`): kapalı 1 ve 2 düşen kare, açık 0 ve 0.

## Hüküm

Örnekleyicinin kendi yükü ölçüm çözünürlüğünün altında. Düşen kare iki kolda da 0-4 arasında
dağılıyor ve örnekleyiciye bağlı bir yön göstermiyor; 3. turdaki 4 kare ve 1359 ms ffmpeg CPU,
aynı anda koşan başka işlerin gürültüsü (ffmpeg CPU'su aynı kolda 62 ms ile 1359 ms arasında oynadı).
Kayıt yükü örneklemeyle artmıyor. Bu makine paylaşımlıydı; sessiz makinede yeniden koşmak ölçüyü
keskinleştirir ama hükmü değiştirmesi beklenmez.
