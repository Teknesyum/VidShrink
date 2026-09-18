# Kaydedici Başlatma El Sıkışması: Biten Kayıt "Başlatamadı" Sayılıyordu

`main` CI koşumu `35316250545` (`fe3805ee`) tek testte kırmızıydı:

```
VidShrink.Tests.KaydediciHedefTests.BoyutSiniriDolunacaKayitKendiBiter [FAIL]
System.InvalidOperationException : ffmpeg kaydi baslatamadi (0): [libx264 @ 000002773a33b000] frame I:2     Avg QP: 0.00  size:161164
[libx264 @ 000002773a33b000] frame P:22    Avg QP: 0.00  size:    56
[libx264 @ 000002773a33b000] kb/s:1082.39
  at VidShrink.Ffmpeg.RecorderSession.StartSegmentAsync(...) RecorderSession.cs:line 437
  at VidShrink.Ffmpeg.RecorderSession.StartAsync(...) RecorderSession.cs:line 203
  at VidShrink.Tests.KaydediciHedefTests.BoyutSiniriDolunacaKayitKendiBiter() KaydediciHedefTests.cs:line 212
```

İki şey çelişiyor: çıkış kodu **0** ve yakalanan stderr **normal bir x264 bitiş özeti**.
ffmpeg işini bitirmiş; "başlatamadı" diyen taraf bizim el sıkışmamız.

## Kusur

`StartSegmentAsync` ilk `progress=` bloğunu bekliyor:

```csharp
_ = await Task.WhenAny(firstBlock.Task, process.WaitForExitAsync(ct), Task.Delay(StartTimeoutMs, ct));
if (firstBlock.Task.IsCompletedSuccessfully && firstBlock.Task.Result) { ... }
throw new InvalidOperationException($"ffmpeg kaydi baslatamadi ({_lastExitCode}): ...");
```

`MaxMegabytes = 0.05` → `-fs 52428`. İlk I karesi tek başına 161 164 bayt, yani sınır
**ilk karede** doluyor ve ffmpeg el sıkışma tamamlanmadan 0 ile kapanıyor. Beklenen blok
ya hiç yazılmıyor ya da boruda beklerken çıkış görevi yarışı kazanıyor. Her iki durumda da
bitmiş bir kayıt başlatma hatası sayılıyor.

## Yerel yeşil, CI kırmızı

Aynı test bu makinede 4 saniyede yeşil geçti; zamanlamaya bağlı olduğu için yerel koşum
kusuru göstermiyor. Bu yüzden belirti dikişle zorlandı: `RecorderSession.IlerlemeyiYut`
ilerleme bloğunu hiç görmemiş gibi davranıyor — CI'nın gördüğü halin aynısı.

## Düzeltme

```csharp
var kazanan = await Task.WhenAny(firstBlock.Task, process.WaitForExitAsync(ct), Task.Delay(StartTimeoutMs, ct));

if (!ReferenceEquals(kazanan, firstBlock.Task) && process.HasExited)
{
    _ = await Task.WhenAny(_stdoutPump, Task.Delay(StartTimeoutMs, ct));
}

var bittiSayilir = firstBlock.Task.IsCompletedSuccessfully && firstBlock.Task.Result;
if (!bittiSayilir && process.HasExited && process.ExitCode == 0)
{
    bittiSayilir = File.Exists(path) && new FileInfo(path).Length > 0;
}
```

İki kol: süreç çıkışı yarışı kazandıysa önce boru boşaltılır (blok hâlâ bekliyorsa görülür),
ve **0 ile çıkıp dolu dosya bırakmış** ffmpeg başlatma hatası sayılmaz. Gerçek başarısızlık
kolu (kod ≠ 0, ya da 0 bayt) olduğu gibi duruyor.

## Mutasyon

| # | Kesim | Dosya | Sonuç |
|---|-------|-------|-------|
| M8 | Çıkış-0 kolu kaldırıldı (`bittiSayilir` yalnız `firstBlock`'tan) | `RecorderSession.cs` | 1 kırmızı, CI iletisinin aynısı |

```
VidShrink.Tests.KaydediciHedefTests.IlerlemeBloguGelmese_deSifirlaBitenKayitBaslatamadiSayilmaz [FAIL]
System.InvalidOperationException : ffmpeg kaydi baslatamadi (0): [libx264 @ 0000011dad518380] frame I:5     Avg QP: 0.00  size:  1684
[libx264 @ 0000011dad518380] frame P:58    Avg QP: 0.00  size:  1312
[libx264 @ 0000011dad518380] kb/s:160.98
  at VidShrink.Ffmpeg.RecorderSession.StartSegmentAsync(...) RecorderSession.cs:line 444
Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: 4 s
```

Düzeltme yerindeyken, kaydediciye dokunan üç sınıf:

```
Başarılı!  - Başarısız:     0, Başarılı:    32, Atlanan:     0, Toplam:    32, Süre: 1 m 2 s
```

## Denenip Bırakılan Dikiş

İlk dikiş pompayı geciktiriyordu (`PompaGecikmeMs = 400`, sonra `3000`); ikisi de yeşil
geçti. Sebep: pompa uyurken stdout borusu doluyor ve ffmpeg **bu yüzden** çıkmıyor —
gecikme yarışı zorlamak yerine ortadan kaldırıyor. Dikiş belirtiye çevrildi.
