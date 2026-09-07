# Boru Kapanışı: Ölçülen Süreler ve Türetilen Sabitler

T182. Makine: Windows 11 Pro 10.0.22631, .NET SDK 9.0.316, ffmpeg 9.0 (Gyan full build).
Ölçüm düzeneği `tools/boru-kapanis/`. Tablolar yeniden üretilebilir:
`dotnet run -c Release --project tools/boru-kapanis/BoruKapanis.csproj -- hepsi`
(alt komutlar: `stderr`, `arama`, `oldurme`, `pay`).
Tüm koşumlar Release, ffmpeg sıralı (eşzamanlı kodlama yok).

## 1. Asılmanın kökü: boru tamponu

`GirdiKlipFixture` klip üretirken `RedirectStandardError = true` verip akışı hiç okumuyordu.
ffmpeg'in bu komut için stderr'e yazdığı bayt sayısı ölçüldü:

Bayt sayısı **sabit değil**: ffmpeg çıktı yolunu stderr'de yankıladığı için yolun uzunluğuna
göre değişir. `tools/boru-kapanis stderr` aynı komutu üç farklı çıktı adıyla koşar:

| çıktı dosyası | tam yol uzunluğu | stderr bayt |
| --- | --- | --- |
| `s.mkv` | 100 | 4730 |
| `stderr-olcumu.mkv` | 112 | 4742 |
| `cok-daha-uzun-bir-cikti-adi-ornegi.mkv` | 133 | 4763 |
| Windows anonim boru tamponu | - | 4096 |

Denetçi aynı komutu başka yollarda 4747 / 4787 / 4794 ölçtü; bunlar aynı olgunun farklı yol
uzunlukları. Maddi iddia yolla değişmiyor: **her ölçümde stderr > 4096**.

| ölçüm | değer |
| --- | --- |
| akış okunmadan `WaitForExit` | 30 sn'de dönmedi (dosya yazılmış, süreç bloke) |
| akış sürekli boşaltılarak | 0,1 sn |

Ölçülen bayt sayısı 4096'yı aştığı için ffmpeg son yazışta bloke oluyor, çıkamıyor, `WaitForExitAsync`
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

## 5. Tur 2: `Surec_disaridan_oldurulunce...` testindeki yarış

Denetçi bu testi `7743974d` üzerinde 15 koşumun 4'ünde, tabanda (`ed42c2db`) 6 koşumun
2'sinde kırmızı ölçtü. Yarış testin kendisinde, üründe değil: çözücü ffmpeg arka planda
ileri doğru okumaya devam ediyor, `SeekAsync` döndükten sonra da önbelleği doldurmayı
sürdürüyor. Test dönüş ile öldürme arasında bir işlemci dilimi kaybederse:

- hedef kare (2 sn) çoktan önbellekte olur, `SeekAsync(2)` süreç başlatmadan döner ve
  `ProcessesStarted > startedBeforeCrash` düşer;
- ya da süreç klibi tümüyle çözüp kendiliğinden çıkmıştır, `TestOnly_KillVideoProcess`
  false döner.

İki dal da aynı kökten: **öldürmenin isabet etmesi gereken pencere ölçülmemişti.**

`tools/boru-kapanis pay` bu pencereyi ölçer — `SeekAsync(1.0)` döndükten sonra çocuk
ffmpeg daha ne kadar yaşıyor:

| klip | anahtar aralığı | anahtar kare | pay ms (3 koşum) |
| --- | --- | --- | --- |
| 640x480, 6 sn (tur 1 klibi) | 0,1 | 60 | 29 / 33 / 20 |
| 640x480, 20 sn | 0,05 | 400 | 214 / 158 / 158 |
| 1280x720, 20 sn (seçilen) | 0,05 | 400 | 530 / 501 / 500 |

Testin içine yapay gecikme koyup taranan kırılma noktası aynı sayıyı bağımsız doğruluyor:

| klip | 100 ms | 200 ms | 300 ms | 450 ms | 600 ms |
| --- | --- | --- | --- | --- | --- |
| 640x480, 6 sn, 0,1 | kırmızı | kırmızı | kırmızı | kırmızı | kırmızı |
| 640x480, 20 sn, 0,05 | yeşil | kırmızı | kırmızı | kırmızı | kırmızı |
| 1280x720, 20 sn, 0,05 | yeşil | yeşil | yeşil | yeşil | kırmızı |

Tur 1 klibinde pencere **~29 ms**. Denetçinin gördüğü kararsızlık tam bu ölçekte.

### Alınan iki önlem

1. **Hedef kare artık kanıtlanabilir biçimde önbellekte değil.** Çözücü yalnız ileri
   okur (`_decodeStartIndex`'ten `_decodeCursorIndex`'e), bu yüzden test ileri bir
   noktadan (1,0 sn) başlatıp öldürdükten sonra **geriye** (0,0 sn) arıyor. Başlangıç
   noktasının gerisi hiçbir zaman çözülmediği için isabet imkânsız; kurtarma iddiası
   önbellek isabetiyle karışamaz. Önkoşul testte de pimli:
   `Assert.False(pipe.TestOnly_CacheHasStampAt(0.0))`.
2. **Öldürme penceresi 29 ms'den ~500 ms'ye çıkarıldı**: `UzunSessizKlip` 640x480/6 sn
   yerine 1280x720/20 sn, anahtar aralığı 0,05. Bedeli tek seferlik ~0,9 sn kodlama.

Sayaç gizlenmedi: `ProcessesStarted > startedBeforeCrash` yerinde duruyor, artık yalnız
gerçekten önbellekte olmayan bir kare için ölçüyor.
