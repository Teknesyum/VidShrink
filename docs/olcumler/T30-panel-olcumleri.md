# T30 — Karşılaştırma paneli ölçümleri

**Tarih:** 24.08.2026 · **Sözleşme:** `.claude/relay/contracts/T30.md`

Bu sözleşme yalnız sayı üretti. `src/` altına tek satır yazılmadı; ölçüm aracı
`tools/VidShrink.Bench/Program.cs` içindeki `panel` komutu.

## Ortam

| Alan | Değer |
|---|---|
| Makine | DESKTOP-630ME6G, Windows 11 Pro 26100 |
| ffmpeg | 9.0-full_build (gyan.dev) |
| .NET | 8.0 (`%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`) |
| Panel genişliği (varsayım) | 960 px |
| Yakınlaştırma | 4× → 3840 px istenen |

## Kullanılan test klipleri

Depoda klip yok, `%USERPROFILE%\Downloads` altında da yoktu. Dördü de ffmpeg ile üretildi:

```
ffmpeg -f lavfi -i "testsrc2=size=1920x1080:rate=30:duration=60" -c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p 1080p_h264.mp4
ffmpeg -f lavfi -i "testsrc2=size=1920x1080:rate=30:duration=60" -c:v libx265 -preset veryfast -crf 25 -pix_fmt yuv420p -tag:v hvc1 1080p_hevc.mp4
ffmpeg -f lavfi -i "testsrc2=size=3840x2160:rate=30:duration=60" -c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p 4k_h264.mp4
ffmpeg -f lavfi -i "testsrc2=size=3840x2160:rate=30:duration=60" -c:v libx265 -preset veryfast -crf 25 -pix_fmt yuv420p -tag:v hvc1 4k_hevc.mp4
```

| Klip | Süre | Çözünürlük | Codec | Boyut | Bit hızı |
|---|---|---|---|---|---|
| 1080p_h264.mp4 | 60 sn @30 | 1920x1080 | h264 | 40,9 MB | ~5,7 Mbps |
| 1080p_hevc.mp4 | 60 sn @30 | 1920x1080 | hevc | 52,3 MB | ~7,3 Mbps |
| 4k_h264.mp4 | 60 sn @30 | 3840x2160 | h264 | 141,4 MB | ~19,8 Mbps |
| 4k_hevc.mp4 | 60 sn @30 | 3840x2160 | hevc | 184,1 MB | ~25,7 Mbps |

**Kaynağın temsil sınırı:** `testsrc2` yüksek entropili sentetik bir desen. Bit hızları
gerçek kamera kaydına yakın çıktı, ama kare içi karmaşıklık gerçek içerikten yüksek; kod
çözme maliyeti bu yüzden iyimser değil, kötümser tarafta. Anahtar kare aralığı x264/x265
varsayılanı (250 kare ≈ 8,3 sn) — gerçek dosyalarda da olağan aralık.

## Ö1 — Tek kare çekme gecikmesi

`ffmpeg -ss T -i dosya -frames:v 1 -vf scale=960:-2 -f image2pipe -vcodec png -`, PNG
stdout'tan okunuyor. Her kombinasyon için klibe eşit aralıkla dağıtılmış 12 zaman damgası.

**stderr tuzağı:** `GrabAsync` stdout'u `BaseStream`'den, stderr'i `ReadToEndAsync` ile
okuyor; **iki görev de beklemeye girmeden önce başlatılıyor.** Boşaltılmasaydı boru dolar
ve ffmpeg asılırdı (Jellyfin #17429, `docs/taramalar/RAPOR.md:27`). Ölçüm boyunca hiçbir
süreç asılmadı.

| Klip | Çözünürlük | Codec | Durum | n | Medyan ms | p95 ms |
|---|---|---|---|---|---|---|
| 1080p_h264 | 1920x1080 | h264 | soğuk | 12 | 171,7 | 775,2 |
| 1080p_h264 | 1920x1080 | h264 | sıcak | 12 | 175,4 | 649,6 |
| 1080p_hevc | 1920x1080 | hevc | soğuk | 12 | 329,7 | 1141,6 |
| 1080p_hevc | 1920x1080 | hevc | sıcak | 12 | 329,3 | 522,1 |
| 4k_h264 | 3840x2160 | h264 | soğuk | 12 | 529,4 | 1306,3 |
| 4k_h264 | 3840x2160 | h264 | sıcak | 12 | 595,0 | 1238,3 |
| 4k_hevc | 3840x2160 | hevc | soğuk | 12 | 831,2 | 1774,7 |
| 4k_hevc | 3840x2160 | hevc | sıcak | 12 | 963,5 | 1474,3 |

**"Soğuk" ne demek:** soğuk geçiş her zaman damgasına o koşumdaki ilk çekim, sıcak geçiş
aynı damgaların hemen ardından tekrarı. Windows dosya önbelleği yönetici aracı olmadan
güvenilir biçimde boşaltılamıyor, bu yüzden "soğuk" işletim sistemi önbelleği soğuk
demek değil. Sonuç zaten farkın yok denecek kadar küçük olduğunu gösteriyor: gecikmeyi
belirleyen disk değil, **süreç açılışı + anahtar kareden itibaren kod çözme.**

**Süreç açılış tabanı** (aynı makinede 20 tekrar,
`ffmpeg -f lavfi -i nullsrc=s=64x64 -frames:v 1 -f null -`):
medyan **53,4 ms**, p95 **94,7 ms**, min 47,2 ms. Yani 1080p H.264'teki 172 ms medyanın
~53 ms'i daha tek kare açılmadan gidiyor. Konseyin "süreç sağanağı" uyarısı ölçülmüş oldu.

**p95'in medyanın 4-5 katı olmasının sebebi** zaman damgasının anahtar kareye uzaklığı.
Anahtar kareye denk gelen damga hızlı, 8 saniye sonrasına düşen damga tüm GoP'u çözdürüyor.
Bu sürgüde rastgele bir noktaya atlarken gerçekten yaşanacak bir dağılım.

## Ö4 — Bitmap bellek maliyeti

Kare `-f rawvideo -pix_fmt bgra` ile stdout'tan alınıp bayt sayıldı.

| Klip | Çözünürlük | Ölçülen bayt | MB | Beklenen W×H×4 |
|---|---|---|---|---|
| 1080p_h264 | 1920x1080 | 8.294.400 | 7,91 | 8.294.400 |
| 1080p_hevc | 1920x1080 | 8.294.400 | 7,91 | 8.294.400 |
| 4k_h264 | 3840x2160 | 33.177.600 | 31,64 | 33.177.600 |
| 4k_hevc | 3840x2160 | 33.177.600 | 31,64 | 33.177.600 |

Ölçülen değer W×H×4 ile **birebir** aynı; sürpriz yok. Konseyin tahmin ettiği "4K BGRA
kare ~33 MB" doğrulandı (33.177.600 bayt = 31,64 MiB).

Önbellek tavanı için sayı: panel aynı anda **iki** kare tutuyor (sol orijinal, sağ
kodlanmış). 4K'da tek çift 63,3 MB. Önbellek adet değil bayt tavanıyla sınırlanmalı;
**128 MB tavan** 4K'da iki çift, 1080p'de sekiz çift demek.

## Ö5 — Yakınlaştırmanın kare talebine etkisi

Aynı 12 zaman damgası, iki istenen genişlikte. İstenen genişlik kaynağın kendi
genişliğiyle tavanlanıyor.

| Klip | İstenen px | Teslim px | Kaynak tavanı | n | Medyan ms | p95 ms |
|---|---|---|---|---|---|---|
| 1080p_h264 | 960 | 960 | hayır | 12 | 176,7 | 859,2 |
| 1080p_h264 | 3840 | 1920 | **evet** | 12 | 191,7 | 819,0 |
| 1080p_hevc | 960 | 960 | hayır | 12 | 420,2 | 922,5 |
| 1080p_hevc | 3840 | 1920 | **evet** | 12 | 492,0 | 899,7 |
| 4k_h264 | 960 | 960 | hayır | 12 | 641,7 | 1158,0 |
| 4k_h264 | 3840 | 3840 | hayır | 12 | 607,5 | 1231,6 |
| 4k_hevc | 960 | 960 | hayır | 12 | 831,7 | 1240,5 |
| 4k_hevc | 3840 | 3840 | hayır | 12 | 877,1 | 1544,1 |

**Bulgu: yakınlaştırılmış kare istemek neredeyse bedava.** Fark 1080p H.264'te +8,5%,
1080p HEVC'de +17%, 4K H.264'te **-5%** (ölçüm gürültüsü içinde), 4K HEVC'de +5,5%.
Sebep açık: maliyet kod çözmede, ölçeklemede değil. Kaynak zaten tam çözünürlükte
çözülüyor; `scale` filtresi ondan sonra çalışıyor ve küçültme ile büyütme arasında
anlamlı fark yok.

**Tasarım sonucu:** kare servisine "panel genişliği × yakınlaştırma" istemenin ek bedeli
yok. Opus'un yakalattığı hata — kullanıcının bizim ölçeklememizi sıkıştırma hatası
sanması — ucuza kapatılabilir. Tekerlek durduktan sonraki yenilemenin gecikmesi
yakınlaştırmadan değil, **Ö1'deki taban gecikmeden** geliyor.

Kaynak tavanı devreye girdiğinde (1080p kaynakta 4× istemek) teslim edilen genişlik
1920'de duruyor ve servis bunu bildirebiliyor — panelde "1:1" ve "kaynak sınırı"
durumlarını göstermek için gereken bilgi ölçüm yolunda zaten var.

## Ö3 — Örnek kodlama süresi

Zincir `FfprobeClient.ProbeAsync` → `ComplexityProbe.RunAsync` → `PlanCalculator.BuildDetailed`
(taslak) → `CalibrationProbe.RunAsync`. Yeni bir yol açılmadı, o kodun kendi maliyeti ölçüldü.

| Klip | Plan | Kalibrasyon toplam s | Örnek fps | Kare | Toplam hıza göre 2 sn pencere |
|---|---|---|---|---|---|
| 1080p_h264 | 1306x734@30 libx264/slow | 2,45 | 147,3 | 360 | 0,41 sn |
| 4k_h264 | 1920x1080@30 libx264/slow | 5,92 | 60,8 | 360 | 0,99 sn |

Kalibrasyon 3 pencere × 2 CRF = **6 örnek** koşuyor (`CalibrationProbe.cs:11` `MaxWindows`,
`:13` `CrfGap`), her örnek 2 saniyelik pencere (`:10` `WindowSeconds`), yazılım codec'inde
eşzamanlılık 4 (`:14` `SoftwareConcurrency`).

**0,41 / 0,99 sn toplam hıza göre türetilmiş sayılar, tek bir pencerenin süresi değil.**
6 örnek eşzamanlılık 4'te iki dalgada bitiyor; yalnız koşan tek bir pencerenin duvar
saati bir dalgadan uzun olamaz: 1080p'de ≤ 1,2 sn, 4K'da ≤ 3,0 sn. Tek pencere yalnız
koşarken ayrıca ölçülmedi.

### `CalibrationProbe` kodladığı dilimi diskte tutuyor mu?

**Hayır.** `CalibrationProbe.cs:208` çıkışı `-f null -` ile bağlıyor; kodlanmış piksel
hiçbir yere yazılmıyor, yalnız stderr'deki `video:NNNKiB` ve `frame=NN` özeti okunuyor
(`:224`). Dosya adı üreten, geçici klasöre yazan tek satır yok.

Sonraki sözleşme için doğrudan sonuç: **örnek kareyi almak için yeniden kodlama gerekiyor
ama yeni bir kodlama maliyeti gerekmiyor.** `-f null -` yerine bir çıkış hedefi verilirse
zaten harcanan kodlama işi diske ya da boruya düşer; ek maliyet yalnız yazma. Konseyin
"hâlihazırda atılan bir çıktı" tespiti kodda birebir doğrulandı.

## Ö2 — Kodlama sürerken aynı işlem

Gerçek kodlama `EncodeRunner.RunAsync` ile, 20 MB hedefe. Önce boş koşum ölçüldü, sonra
aynı kodlama tekrar koşarken 960 px kare çekme döngüsü arka planda **aralıksız** döndü.

| Koşum | Klip | Boş kodlama s | Kare çekerken s | Kodlama yavaşlaması % | Çekim n | Medyan ms | p95 ms |
|---|---|---|---|---|---|---|---|
| 1 | 1080p_h264 | 14,19 | 16,72 | **17,8** | 36 | 371,6 | 1000,6 |
| 2 | 1080p_h264 | 13,97 | 16,77 | **20,0** | 33 | 382,2 | 1029,5 |
| 2 | 4k_h264 | 29,58 | 37,98 | **28,4** | 30 | 1468,7 | 2026,4 |

İki bağımsız koşumda 1080p için 17,8% ve 20,0% çıktı; ölçüm tekrarlanabilir.

**İki sayı:**

1. **Kare çekme gecikmesi:** 1080p'de medyan 175 ms → 372-382 ms, yani **2,1-2,2 kat**.
   4K'da 529 ms → 1469 ms, **2,8 kat**.
2. **Kodlamanın kendisi:** 1080p'de %17,8-20,0, 4K'da %28,4 yavaşladı.

**Ölçümün sınırı:** çekim döngüsü boşluk bırakmadan döndü, yani bu **en kötü hâl** —
kullanıcının sürgüyü aralıksız sürüklediği durum. Tek bir çekim koşum boyunca bir kez
yapılırsa bedeli o çekimin süresi kadar bir çekirdek. Ama eşiğin sorduğu şey tam olarak
"gösterge ölçtüğü şeyi bozuyor mu" ve cevap her iki çözünürlükte de %5'in çok üstünde.

Boş koşum önce koştuğu için dosya önbelleği ikinci koşumun lehine; yani ölçülen yavaşlama
gerçeğin **alt sınırı**.

## Kapı kararı

**Ö1 → üçüncü kapı: yalnız önceden çekilmiş sabit noktalar.**

Konseyin ölçütü 1080p'de p95. Ölçülen p95 soğukta **775 ms**, sıcakta **650 ms**; ikisi de
400 ms eşiğinin üstünde. Medyan 172 ms ile "gecikmeli" bandına düşse de karar p95'e göre
verilecekti ve p95 net biçimde üçüncü kapıda. Sürgü sürüklenirken canlı kare yenilemesi
bu makinede yapılamaz.

p95'i medyanın 4-5 katına çıkaran şey anahtar kare uzaklığı: damga GoP'un sonuna düşünce
ffmpeg 8 saniyeye kadar kare çözüyor. Bu, sabit noktaların **anahtar karelere
hizalanması** gerektiğini söylüyor. Anahtar kare damgalarındaki gecikme bu sözleşmede
ayrıca ölçülmedi.

**Ö2 → %5 eşiği aşıldı, açık farkla.** Kodlama yavaşlaması 1080p'de %17,8-20,0, 4K'da
%28,4. Konseyin kuralı gereği **kodlama sürerken kare çekimi tamamen kapatılacak ve son
önbellekli kare gösterilecek.**

**Ö5 → yakınlaştırılmış kare istemek bedava sayılır** (+8,5% ile -5% arası). Kare servisi
her zaman "panel genişliği × yakınlaştırma, kaynak çözünürlüğüyle tavanlanmış" istemeli;
Opus'un yakalattığı "kullanıcı bizim ölçeklememizi sıkıştırma hatası sanar" hatası ek bir
gecikme bedeli ödemeden kapatılabilir.

**Ö4 → önbellek tavanı bayt cinsinden 128 MB.** 4K'da iki kare çifti, 1080p'de sekiz çift.

## Doğrulama

| Kontrol | Sonuç |
|---|---|
| `dotnet build VidShrink.sln -c Release` | 0 uyarı, 0 hata |
| `dotnet test VidShrink.sln -c Release` | 218 başarılı, 0 başarısız, 6 atlanan |
| `src/` altına değişiklik | yok |

K1 kanıtı — T30'un commit'i yalnız üç dosyaya dokunuyor:

```
$ git diff --name-only HEAD~1..HEAD
.claude/relay/contracts/T30.md
docs/olcumler/T30-panel-olcumleri.md
tools/VidShrink.Bench/Program.cs

$ git diff --stat HEAD~1..HEAD -- src/
(bos)
```

Ölçüm komutu:

```
bench panel <klip,...> --only o1,o2,o3,o4,o5 [--panel-width 960] [--zoom 4] [--samples 12] [--target 20]
```

## Ölçülemeyenler

- İşletim sistemi dosya önbelleği güvenilir biçimde boşaltılamadığı için "gerçekten soğuk"
  disk okuması ölçülemedi. Sonuçlar gecikmenin diskle değil kod çözmeyle belirlendiğini
  gösteriyor.
- Tek bir 2 saniyelik kalibrasyon penceresinin yalnız koşarken süresi ölçülmedi; yalnızca
  dalga aritmetiğinden üst sınır verildi.
- Anahtar kare damgalarındaki kare çekme gecikmesi ölçülmedi.
- Donanım kodlayıcı (av1_amf) ile Ö2 tekrarlanmadı; ölçümler libx264/slow planıyla.

