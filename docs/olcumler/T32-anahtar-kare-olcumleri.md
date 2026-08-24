# T32 — Anahtar kare hizalı çekme ölçümleri

**Tarih:** 24.08.2026 · **Sözleşme:** `.claude/relay/contracts/T32.md` · **Dayanak:** T30

T30 kare çekme p95'ini 1080p'de 775 ms buldu, paneli en kötü kapıya soktu ve sebebi
**anahtar kare uzaklığı** olarak işaret etti — ama bunu ölçmedi. K1 tam olarak o ölçüm.

## Ortam

| Alan | Değer |
|---|---|
| Makine | DESKTOP-630ME6G, Windows 11 Pro 26100 |
| ffmpeg | 9.0-full_build (gyan.dev) |
| .NET | 8.0 (`%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`) |
| Klipler | T30'un ürettiği dördü, aynı `testsrc2` komutlarıyla |
| Panel genişliği | 960 px |

Ölçüm komutu:

```
bench panel <klip,...> --only o6 [--panel-width 960] [--samples 100]
```

Ham örnekler CSV olarak `%TEMP%\vidshrink-o6-ornekler.csv` dosyasına yazılıyor.

## Yöntem

Anahtar kare damgaları `ffprobe -select_streams v:0 -show_entries packet=pts_time,flags`
ile çıkarılıyor, `K` bayraklı paketler alınıyor. Aynı zaman damgaları iki kez çekiliyor:
bir kez istenen anda (**hizasız**), bir kez o andan önceki en yakın anahtar karede
(**hizalı**). Soğuk geçiş ve hemen ardından sıcak geçiş, T30'daki tanımla aynı.

**T30'un p95'i n=12 ile alınmıştı; o örneklem sayısında p95 pratikte 12 örneğin
maksimumu demek** ve tek bir zamanlama tökezlemesi sayıyı belirliyor. Bu ölçümde n=100
ve n=200 kullanıldı, soğuk+sıcak havuzlandı.

## Ö6.1 — Anahtar kare dizinini çıkarmanın maliyeti

| Klip | Anahtar kare | Ortalama aralık s | İlk çıkarma ms | Tekrar ms |
|---|---|---|---|---|
| 1080p_h264 | 8 | 8,57 | 81,1 | 122,3 |
| 1080p_hevc | 8 | 8,57 | 89,5 | 81,5 |
| 4k_h264 | 8 | 8,57 | 226,9 | 212,6 |
| 4k_hevc | 8 | 8,57 | 159,4 | 153,1 |

**Tekrar çağrı ucuzlamıyor** — ffprobe her seferinde dosyanın paket listesini baştan
geziyor. Yani bu, her sürgü hareketinde ödenecek bir maliyet değil: **bir kere çıkarılıp
saklanacak.** 1080p'de ~85 ms, 4K'da ~200 ms, dosya açılışında bir kez.

Anahtar kare aralığı x264/x265 varsayılanı: 60 saniyelik klipte **8 anahtar kare**,
aralarında 8,57 sn. Bu, hizalı gezinmenin çözünürlüğünü de belirliyor (aşağıda).

## Ö6.2 — Kod çözmesiz taban

Aynı dosyayı açan ama hiçbir kare çözmeyen bir çağrı (`ffprobe -show_format`). Kuyruğun
kod çözmeden mi yoksa süreç açılışından mı geldiğini ayırmak için.

| Klip | n | p50 ms | p90 ms | p95 ms | max ms | >400ms |
|---|---|---|---|---|---|---|
| 1080p_h264 | 100 | 73,3 | 121,3 | 181,6 | **783,5** | 4 |
| 1080p_hevc | 100 | 70,2 | 107,4 | 130,9 | **848,6** | 2 |
| 4k_h264 | 100 | 83,6 | 125,8 | 149,5 | **778,2** | 3 |
| 4k_hevc | 100 | 60,6 | 103,2 | 123,6 | **716,6** | 2 |

**Bulgu:** hiç kare çözmeyen bir çağrıda bile örneklerin %2-4'ü 400 ms'i aşıyor ve
maksimumlar 700-850 ms bandında. Yani ~800 ms'lik kuyruk **kod çözmenin değil, süreç
açılışının** kuyruğu. T30'un 20 tekrarlı taban ölçümü (p95 95 ms) bu kuyruğu yakalayamamıştı;
`nullsrc` kullandığı için gerçek bir dosya da açmıyordu.

## Ö6.3 — Hizalı ve hizasız çekme, havuzlanmış (soğuk+sıcak)

n=100 × 2 geçiş = 200 örnek/satır.

| Klip/hizalama | n | p50 ms | p90 ms | p95 ms | p99 ms | 600-1000ms | >400ms % |
|---|---|---|---|---|---|---|---|
| 1080p_h264/hizasız | 200 | 194,0 | 771,6 | 820,2 | 853,6 | 26 | 13,0 |
| **1080p_h264/hizalı** | 200 | **134,1** | **260,2** | **691,6** | 850,6 | 11 | **5,5** |
| 1080p_hevc/hizasız | 200 | 369,0 | 890,9 | 991,1 | 1169,0 | 18 | 42,0 |
| **1080p_hevc/hizalı** | 200 | **140,7** | **186,5** | **799,6** | 846,3 | 11 | **5,5** |
| 4k_h264/hizasız | 200 | 577,3 | 1123,3 | 1169,8 | 1238,1 | 48 | 74,5 |
| 4k_h264/hizalı | 200 | 217,8 | 1101,1 | 1158,5 | 1244,1 | 40 | 37,5 |
| 4k_hevc/hizasız | 200 | 977,7 | 1489,8 | 1555,5 | 1611,5 | 51 | 89,5 |
| 4k_hevc/hizalı | 200 | 261,3 | 784,4 | 851,0 | 875,9 | 37 | 18,5 |

**Hizalama medyanı ve p90'ı büyük ölçüde düşürüyor:**

| Klip | p50 değişimi | p90 değişimi |
|---|---|---|
| 1080p_h264 | 194 → 134 ms (−%31) | 772 → 260 ms (−%66) |
| 1080p_hevc | 369 → 141 ms (−%62) | 891 → 187 ms (−%79) |
| 4k_h264 | 577 → 218 ms (−%62) | 1123 → 1101 ms (−%2) |
| 4k_hevc | 978 → 261 ms (−%73) | 1490 → 784 ms (−%47) |

**Ama p95 hizalamayla düşmüyor.** 1080p'de hizalı p95 692 ms ve 800 ms. Sebebi Ö6.2:
örneklerin %5,5'i ~700-850 ms bandındaki süreç açılışı durakalmasına düşüyor ve p95 tam o
bandın sınırında duruyor. Aynı yapılandırma iki geçişte 262 ms ve 796 ms p95 verebiliyor —
kuyruk oranı %5'in bir altına ya da bir üstüne düştüğü için.

**Yani T30'un hipotezi kısmen doğrulandı, kısmen çürütüldü:** anahtar kare uzaklığı
medyanın ve p90'ın gerçek sebebiydi, ama p95'in sebebi o değildi.

## Kapı kararı

Sözleşmenin kapı tablosu 1080p'de **hizalı p95**'e bakıyor. Ölçülen: H.264'te **692 ms**,
HEVC'de **800 ms**. İkisi de 400 ms eşiğinin üstünde.

> **Zaman ekseninde gezinme üçüncü kapıda kalıyor: kare sürükleme sırasında yenilenmeyecek,
> panel yalnız anahtar karelere hizalı duraklarda yenilenecek.**

Kapının kendisi değişmedi ama **sebebi değişti ve bu panel sözleşmesini ilgilendiriyor:**

1. Hizalama gerçekten çalışıyor. Gezinmelerin %90'ı artık 1080p'de 260 ms'in altında
   dönüyor (hizasızda 772 ms'ti). Duraklar anahtar karelere oturtulduğunda tipik yenileme
   **134-141 ms**, yani kullanıcının gördüğü şey hızlı.
2. Kapıyı üçüncü bantta tutan şey artık kare çekme maliyeti değil, **her çekimde yeni bir
   ffmpeg süreci açmanın %5'lik kuyruğu.** Bu kuyruk hiç kare çözmeyen bir çağrıda da var.
   Özelliği daraltmak bu kuyruğu düşürmez; süreç başına ödenen bedeli kaldırmak düşürür.
   Kalıcı süreç ya da boru tabanlı bir çekim yolu **ölçülmedi**, bu yüzden önerilmiyor —
   ama üçüncü kapıyı ikinciye taşıyacak tek aday o.
3. **Durakların çözünürlüğü kaba.** 60 saniyelik klipte 8 anahtar kare var, aralarında
   8,57 sn. Karşılaştırma paneli için bu az sayıda durak demek; panel sözleşmesi bunu bilerek
   yazılmalı. Kaynağın kendi GoP'u neyse duraklar o.

## Doğrulama

| Kontrol | Sonuç |
|---|---|
| `dotnet build VidShrink.sln -c Release` | 0 uyarı, 0 hata |
| `dotnet test VidShrink.sln -c Release` | 256 başarılı, 0 başarısız, 6 atlanan |

Atlanan 6 test T32'den önce de atlanıyordu (`VIDSHRINK_LIVE_SOURCE` isteyen canlı ölçümler).

## Ölçülemeyenler

- **Kalıcı ffmpeg süreci / boru tabanlı çekim ölçülmedi.** Yukarıdaki 2. maddenin önerdiği
  yol denenmedi; kapıyı taşıyıp taşımayacağı bilinmiyor.
- **Süreç açılışı durakalmasının kaynağı bulunmadı.** Virüs taraması, dosya sistemi filtresi
  ya da zamanlayıcı olabilir; ayrıştırılmadı. Yalnız "kod çözmeden bağımsız" olduğu ölçüldü.
- **İşletim sistemi dosya önbelleği** T30'daki gibi güvenilir biçimde boşaltılamadı; "soğuk"
  yine gerçekten soğuk disk demek değil. Soğuk/sıcak farkı ölçüm gürültüsü içinde kaldı.
- **Gerçek kamera kaydıyla tekrarlanmadı.** `testsrc2` yüksek entropili; anahtar kare aralığı
  gerçek dosyalarda da 250 kare civarı olsa da içerik farkı ölçülmedi.
- **Donanım hızlandırmalı kod çözme (`-hwaccel`) denenmedi.** Kare çekme maliyetini
  düşürebilir, ölçülmedi.
