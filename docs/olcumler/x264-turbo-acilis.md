# x264 turbo ilk geçişi açılabilir mi, açılmalı mı

**Karar: açılmadı.** `weightp` duvarı gerçekten aşılabiliyor — iki geçişe aynı `weightp`
yazmak yetiyor, denenen dört yazım biçiminin dördü de çalışıyor. Ama kazanç ölçüldüğünde
x264 turbosu üretim borusunda toplam süreyi **%0,58 – %4,44** kısaltıyor ve VMAF'tan
**0,35 – 0,83** puan götürüyor. Aynı ölçümde `libx265` turbosu **%29,6 – %33,5**
kazandırıyor ve VMAF'ı düşürmüyor (+0,117 ve +0,538).

Sözleşmenin eşiği "kabaca %10'un altında kazanç → açma" idi. Ölçülen kazanç eşiğin
altında, üstelik karşılığında ödenen VMAF var. `CodecModel`'in `libx264` satırı
`Safe: false` kaldı, `PlanCalculator` kolu değişmedi, `FfmpegArguments` `weightp`
üretmiyor. Kodda davranış değişikliği yok; değişen tek şey eskiyen üç docstring.

## Düzenek

Kaynak: `.calisma/kaynak/parca-1.mkv` (HEVC 10 bit, 1920x1080, 60 fps, 60,399 sn,
92 577 316 B, 12 262 099 bit/sn). Yalnız okundu.

İki kesit **aynı komutla** çıkarıldı, tek fark başlangıç saniyesi:

```
ffmpeg -y -ss <SS> -i parca-1.mkv -t 20 \
  -vf scale=1280:720:flags=lanczos,fps=30 \
  -c:v libx264 -preset veryslow -crf 12 -pix_fmt yuv420p -an klip-<SS>.mp4
```

| kesit | `-ss` | süre | ölçek / fps | boyut | bit hızı | karakter |
|---|---|---|---|---|---|---|
| klip 5  | 5 sn  | 20 sn | 1280x720 / 30 | 37,4 MB | 15 674 kbit/sn | hareketli |
| klip 35 | 35 sn | 20 sn | 1280x720 / 30 | 15,2 MB | 6 355 kbit/sn  | sakin |

Her kol iki geçişli ABR, hedef **1500 kbit/sn**, `-maxrate 2250k -bufsize 4500k`,
`-g 60`, x264'te `-keyint_min 30`, x265'te
`-x265-params keyint=60:min-keyint=30:scenecut=40`, `-pix_fmt yuv420p`, ses yok.
VMAF: ffmpeg `libvmaf`, `vmaf_v0.6.1`, referans kendi `klip-<SS>.mp4` dosyası.

Toplam **160 `ffmpeg` süreci** doğdu; hepsi sıra ile, hiç paralel koşum yok.

## K1 — Önce ffmpeg'e soruldu

Girdi `klip-35.mp4`, geçiş 1 `veryfast`, geçiş 2 `slow`. Ham çıktılar
`.calisma/T155/k1/ham-*.txt`.

| kol | iki geçişe de verilen | p1 exit | p2 exit | g1 ms | g2 ms | çıktı B |
|---|---|---|---|---|---|---|
| A | (yok) | 0 | **127** | 798 | 79 | **0** |
| B | `-x264-params weightp=2` | 0 | 0 | 571 | 1829 | 3 715 728 |
| C | `-x264-params weightp=1` | 0 | 0 | 599 | 1810 | 3 712 615 |
| D | `-weightp 2` | 0 | 0 | 554 | 1854 | 3 716 815 |
| E | `-weightp 1` | 0 | 0 | 546 | 1795 | 3 717 749 |

A kolunun ikinci geçişi, T153'ün bildirdiği duvarın aynısı:

```
[libx264] different weightp setting than first pass (2 vs 1)
[vost#0:0/libx264] Error while opening encoder - maybe incorrect parameters such as bit_rate, rate, width or height.
[out#0/mp4] Nothing was written into output file, because at least one of its streams received no packets.
```

**Dört yazımın dördü de geçiyor** ve dördü de gerçekten uygulanıyor: ikinci geçişin
x264 başlık satırı B/D'de `weightp=2`, C/E'de `weightp=1` yazıyor. `-weightp N` bayrağı
`-x264-params weightp=N` ile aynı sonucu veriyor; ne "unrecognized option" ne de
yoksayma uyarısı çıktı.

`weightp` eşitlendiğinde **başka hiçbir uyuşmazlık çıkmadı**: `me`, `subme`, `trellis`,
`8x8dct` için ikinci geçişten tek bir şikâyet yok, dört kolun dördü de exit 0 ve makul
boyutta çıktı verdi. Uyuşmazlık listesi tek maddelik: `weightp`.

K1 yeşil → K2'ye geçildi.

## K2 — İki düzenek, iki farklı cevap

Ölçüm iki kez kuruldu ve **ikisi aynı şeyi söylemedi**; ayrım kararı belirlediği için
ikisi de yazılıyor.

### Ara düzenek — girdi `klip-<SS>.mp4` (720p30 x264, ucuz çözme)

Süreler ms. x264 kolları **5 koşum**, x265 kolları **2 koşum**. Boyut ve VMAF son
yazılan çıktıdan.

| parça | kol | geçiş1 | geçiş2 | toplam süreler (ms) | ort. | boyut B | VMAF |
|---|---|---|---|---|---|---|---|
| klip 5  | bugün      | slow     | slow | 3466 3270 3463 3235 3483 | 3383,4 | 3 634 844 | 79,762 |
| klip 5  | turbo wp2  | veryfast | slow | 2956 3130 2939 2981 3013 | 3003,8 | 3 644 498 | 78,816 |
| klip 5  | turbo wp1  | veryfast | slow | 2906 3026 2880 2937 2929 | 2935,6 | 3 649 372 | 78,895 |
| klip 5  | x265 bugün | slow     | slow | 21883 23223 | 22553,0 | 3 607 160 | 84,408 |
| klip 5  | x265 turbo | veryfast | slow | 14753 15256 | 15004,5 | 3 663 125 | 84,963 |
| klip 35 | bugün      | slow     | slow | 2687 2726 2609 2633 2583 | 2647,6 | 3 723 224 | 93,115 |
| klip 35 | turbo wp2  | veryfast | slow | 2417 2415 2380 2631 2509 | 2470,4 | 3 718 245 | 92,705 |
| klip 35 | turbo wp1  | veryfast | slow | 2419 2380 2343 2443 2359 | 2388,8 | 3 718 472 | 92,685 |
| klip 35 | x265 bugün | slow     | slow | 16380 16783 | 16581,5 | 3 726 932 | 93,723 |
| klip 35 | x265 turbo | veryfast | slow | 10979 10980 | 10979,5 | 3 747 787 | 93,889 |

Kazanç: klip 5 wp2 %11,22 · wp1 %13,23; klip 35 wp2 %6,69 · wp1 %9,77;
x265 %33,47 ve %33,78.

### Üretim düzeneği — girdi doğrudan `parca-1.mkv` (`-ss` / `-t` ile kesilerek)

Üretim yolu kullanıcının kaynak dosyasını okur: her iki geçiş de HEVC 10 bit 1080p60
çözer, lanczos ile ölçekler, 60 fps'ten 30'a düşürür. Ara düzenek bu maliyeti
taşımıyordu. x264 kolları **3 koşum**, x265 kolları **2 koşum**.

| parça | kol | geçiş1 | geçiş2 | toplam süreler (ms) | ort. | boyut B | VMAF |
|---|---|---|---|---|---|---|---|
| klip 5  | bugün      | slow     | slow | 4679 4477 4518 | 4558,0  | 3 634 982 | 79,420 |
| klip 5  | turbo wp2  | veryfast | slow | 4380 4400 4457 | 4412,3  | 3 642 284 | 78,670 |
| klip 5  | turbo wp1  | veryfast | slow | 4347 4371 4349 | 4355,7  | 3 654 924 | 78,595 |
| klip 5  | x265 bugün | slow     | slow | 22841 23345 | 23093,0 | 3 605 126 | 84,216 |
| klip 5  | x265 turbo | veryfast | slow | 15369 15366 | 15367,5 | 3 630 584 | 84,754 |
| klip 35 | bugün      | slow     | slow | 3840 3895 3848 | 3861,0  | 3 729 471 | 92,921 |
| klip 35 | turbo wp2  | veryfast | slow | 3853 3831 3832 | 3838,7  | 3 722 831 | 92,573 |
| klip 35 | turbo wp1  | veryfast | slow | 3810 3808 3800 | 3806,0  | 3 722 680 | 92,540 |
| klip 35 | x265 bugün | slow     | slow | 16744 16716 | 16730,0 | 3 729 034 | 93,457 |
| klip 35 | x265 turbo | veryfast | slow | 11545 12022 | 11783,5 | 3 739 470 | 93,574 |

| parça | kol | süre kazancı | VMAF farkı | boyut farkı |
|---|---|---|---|---|
| klip 5  | turbo wp2  | **%3,20** | −0,751 | +%0,20 |
| klip 5  | turbo wp1  | **%4,44** | −0,825 | +%0,55 |
| klip 5  | x265 turbo | %33,45    | +0,538 | +%0,71 |
| klip 35 | turbo wp2  | **%0,58** | −0,348 | −%0,18 |
| klip 35 | turbo wp1  | **%1,42** | −0,381 | −%0,18 |
| klip 35 | x265 turbo | %29,57    | +0,117 | +%0,28 |

### İki düzenek neden ayrışıyor

x264 `slow` ikinci geçiş, üretim düzeneğinde toplam sürenin 2,38 – 2,80 sn'si. İlk
geçişin 1,4 – 2,0 sn'sinin büyük bölümünü ise ön ayar değil **çözme ve ölçekleme** yiyor.
`slow` yerine `veryfast` koymak klip 35'te ilk geçişi ortalama 1446,7 ms'den 1407,0 ms'ye
indiriyor: 39,7 ms, toplamın %1,0'i. Kazancın bir kısmı da ikinci geçişte geri gidiyor
(2414,3 ms yerine 2431,7 ms), toplama kalan 22,3 ms — %0,58. Ara düzenekte çözme ucuz
olduğu için aynı fark %6,7 – %13,2 gibi görünüyordu. Kullanıcının gördüğü sayı üretim
düzeneğininki.

x265'te durum tersi: kodlayıcı toplam sürenin neredeyse tamamı, ilk geçiş `slow`'dan
`veryfast`'e inince 11,3 sn'den 3,7 sn'ye düşüyor (klip 5, üretim düzeneği: 11248 ve
11434 ms yerine 3680 ve 3653 ms). Turbo x265'te işe yarıyor çünkü orada
kısaltılan şey gerçekten kodlayıcının kendisi.

**Karar: açılmıyor.** İki parçanın ikisinde de kazanç eşiğin altında (%0,58 ve %3,20;
wp1 ile %1,42 ve %4,44) ve bedava değil — VMAF dört ölçümün dördünde de düşüyor.

`weightp` değerinin seçimi de bu yüzden yazıldı ama uygulanmadı: wp1 biraz daha hızlı,
wp2 hareketli parçada biraz daha iyi VMAF veriyor (78,670 / 78,595) ve `slow`un kendi
varsayılanı olduğu için bugünkü çıktıyı daha az kaydırırdı. Açılsaydı wp2 seçilirdi.

## K3 — Uygulanmadı

K2 açmayı söylemediği için üç noktanın hiçbirine dokunulmadı: `FfmpegArguments`
`weightp` üretmiyor, `CodecModel`'in `libx264` satırı `Safe: false`, `PlanCalculator`ın
kolu yalnız `libx265` açıyor. Yarım açılmış turbo T140'ta sıfır bayt ürettiği için
"üçü birden ya da hiçbiri" kuralı geçerliydi; seçilen taraf "hiçbiri".

## K4 — Hiçbir ölçünün beklentisi değişmedi

Turbo tavan kümesi (`libx264`, `libx265`) ve güvenli küme (`libx265`) aynı kaldığı için
`TurboTavanTests`in dördü de, `TurboFirstPassTests`in `:62`, `:71`, `:190` satırlarındaki
üç küme pimi de olduğu gibi duruyor.
`Vaat_edilen_tavan_kumesi_guvenli_kumeden_genis_ve_fark_libx264`ün beklentisi ancak
açılsaydı değişecekti; fark hâlâ `libx264` ve boş değil, `Assert.NotEmpty` koruması
yerinde.

Değişen tek şey davranışla çelişen üç docstring: `CodecModel.TurboFirstPassCeilings`,
`PlanCalculator.TurboFirstPassIsSafe` ve `TurboTavanTests` sınıf açıklaması. Üçü de
"x264 turbosu açılamaz çünkü çıktı sıfır bayt" diyordu; duvarın aşılabildiği ölçüldüğü
için üçü de "aşılabiliyor ama kazanç ödemiyor" diyecek şekilde güncellendi ve bu ölçüme
bağlandı.

## K5 — Mutasyon

K2 açmamayı söylediği için mutasyon karar yönünde koşturuldu: `CodecModel.cs`'te
`["libx264"] = new("veryfast", Safe: false)` yerine `Safe: true`, ardından
`dotnet build -c Release --no-incremental` ve verify komutu.

```
Başarısız VidShrink.Tests.TurboTavanTests.Guvensiz_tavan_hicbir_hiz_kipinde_uretim_yolunda_acilmiyor
   Assert.NotEmpty() Failure: Collection was empty
Başarısız VidShrink.Tests.TurboTavanTests.Vaat_edilen_tavan_kumesi_guvenli_kumeden_genis_ve_fark_libx264
   Assert.Equal() Failure: Collections differ
Başarısız VidShrink.Tests.TurboTavanTests.X264_icin_tavan_vaat_ediliyor_ama_o_tavan_guvenli_degil
   Assert.False() Failure
Başarısız VidShrink.Tests.TurboTavanTests.Uretim_yolunun_actigi_turbo_kumesi_guvenli_kumeyle_ayni
   Assert.Equal() Failure: Collections differ
Başarısız! - Başarısız: 4, Başarılı: 75, Atlanan: 0, Toplam: 79
```

Beklenen dördü öldü ve hiçbiri ffmpeg çağrısı gerektirmedi. Mutasyon geri alındı,
`--no-incremental` yeniden derlendi, `git diff` kod dosyalarında boş döndü, süit 79/79
yeşile döndü.

## K6 — Verify kollarının test sayısı

`dotnet test tests/VidShrink.Tests -c Release --no-build --list-tests --filter <kol>`:

| kol | bulunan test |
|---|---|
| `TurboTavanTests` | 4 |
| `TurboFirstPassTests` | 75 |
| verify komutunun kendisi (iki kol birlikte) | 79 |

Sıfır bulan kol yok; 4 + 75 = 79, birleşik kolun bulduğu sayıyla tutuyor.

Yerel koşum: `Başarılı! - Başarısız: 0, Başarılı: 79, Atlanan: 0, Toplam: 79`.

CI: koşum `33752128362` (`T155-x264-turbo-acilis`, commit `a10743a`),
`completed success` — https://github.com/Teknesyum/VidShrink/actions/runs/33752128362

## Sapmalar ve sınırlar

- `-maxrate` / `-bufsize` üretimin `PeakRateFactor` hesabından değil sabit çarpanlardan
  (1,5x / 3x) alındı. Sapma bütün kollara aynı uygulandığı için karşılaştırmayı
  kaydırmaz, mutlak VMAF değerlerini kaydırabilir. T146 düzeneğindeki sapmanın aynısı.
- x265 kollarına üretimin psy/AQ bayrakları (`psy-rd=2:psy-rdoq=1:aq-mode=2`) verilmedi.
  İki x265 koluna da verilmediği için kontrol kolonu kendi içinde tutarlı; x264 ile
  mutlak VMAF karşılaştırması için değil, hız oranı için okunmalı.
- Ölçüm tek makinede ve tek plan sınıfında yapıldı: 1080p60 HEVC 10 bit kaynak, 720p30
  çıktı. Kaynağın çözmesi ucuzsa (hafif bir 720p h264 girdi gibi) x264 turbosunun payı
  ara düzenekteki %6,7 – %13,2'ye yaklaşır. O sınıf ölçülmedi; açma kararının dayanması
  gereken sınıf üretimin taşıdığı ağır kaynaktır ve orada kazanç %4,44'ü geçmedi.
- Düzenek ve ham çıktılar `.calisma/T155/` altında; `.gitignore`'da, git'e girmez.
