# Boru Kapanışı: Ölçülen Süreler ve Türetilen Sabitler

T182. Makine: Windows 11 Pro 10.0.22631, .NET SDK 9.0.316, ffmpeg 9.0 (Gyan full build).
Ölçüm düzeneği `.calisma/olcum*` altındaydı; koşum sonrası silindi, üreten kod bu dosyada.
Tüm koşumlar Release, ffmpeg sıralı (eşzamanlı kodlama yok).

## 1. Asılmanın kökü: boru tamponu

`GirdiKlipFixture` klip üretirken `RedirectStandardError = true` verip akışı hiç okumuyordu.
ffmpeg'in bu komut için stderr'e yazdığı bayt sayısı ölçüldü:

| ölçüm | değer |
| --- | --- |
| ffmpeg stderr bayt sayısı | 4730 |
| Windows anonim boru tamponu | 4096 |
| akış okunmadan `WaitForExit` | 30 sn'de dönmedi (dosya yazılmış, süreç bloke) |
| akış sürekli boşaltılarak | 0,1 sn |

4730 > 4096 olduğu için ffmpeg son yazışta bloke oluyor, çıkamıyor, `WaitForExitAsync`
dönmüyor. testhost'un altında canlı bir ffmpeg kalıyor; `--blame-hang` bunu
`testhost +- ffmpeg` ağacı olarak döküyor.

Testte pimlenen sayı: üretilen stderr **4096 baytı aşmalı**, aksi halde test kusuru yakalamaz.

## 2. Arama gecikmesi ve `SeekAbsoluteTimeout`

`DecoderPipe` üzerinde 120 rastgele arama (tohum 7, 0-19 sn), üç düzenek:

| düzenek | n | p50 | p90 | p99 | max | başlatılan ffmpeg |
| --- | --- | --- | --- | --- | --- | --- |
| 320x180, önbellek 2 kare | 120 | 43,5 | 46,9 | 52,0 | 77,6 | 93 |
| 1920x1080, önbellek 2 kare | 120 | 60,2 | 66,8 | 74,3 | 76,1 | 92 |
| 1920x1080, varsayılan önbellek | 120 | 1,6 | 59,3 | 67,5 | 71,3 | 24 |

Birim ms. 360 aramanın hiçbiri null dönmedi, hiçbiri sızıntı bırakmadı.

En kötü tek arama: **77,6 ms**.
`SeekAsync`'in yeniden başlatma bütçesi: `MaxRestartAttemptsPerSeek`(8) x
`ForwardWaitTimeout`(250 ms) = **2000 ms**.

`SeekAbsoluteTimeout = 3000 ms` seçildi: 2000 ms'lik meşru yeniden başlatma bütçesinin
üstünde (yani sağlıklı kurtarmayı kesmiyor) ve ölçülen en kötü aramanın 38 katı.

## 3. Öldürme süresi ve `KillWaitMs`

Gerçek çözücü argümanlarıyla açılan 20 ffmpeg süreci, 60 ms sonra `Kill(true)` +
`WaitForExit`:

| n | min | p50 | p90 | p99 | max |
| --- | --- | --- | --- | --- | --- |
| 20 | 11,6 | 12,8 | 15,0 | 24,3 | 24,3 |

Birim ms. `KillWaitMs = 500` seçildi: ölçülen en kötü ölüm süresinin 20 katı.
`KillAttempts = 2`, yani en kötü durumda 1000 ms bekleyip "öldüremedim" diye döner.

## 4. Ölçüm düzeneği

Klip: `ffmpeg -f lavfi -i testsrc=size=<WxH>:rate=30:duration=20
-force_key_frames expr:gte(t,n_forced*1) -pix_fmt yuv420p -c:v libx264 -preset ultrafast -an`

Arama gecikmesi: `new DecoderPipe(<tavan>)`, `OpenAsync(klip)`, 120 kez
`SeekAsync(Math.Round(rnd.NextDouble()*19.0, 3))` (`new Random(7)`), her çağrı
`Stopwatch` ile ölçüldü; `Dispose` sonrası `Process.GetProcessesByName("ffmpeg")`
farkı sızıntı sayısı olarak alındı.

Öldürme: `RestartVideo`'nun ürettiği argüman dizisiyle süreç açıldı, 60 ms beklendi,
`Kill(true)` + `WaitForExit()` arası `Stopwatch` ile ölçüldü.
