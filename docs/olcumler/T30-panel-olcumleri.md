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

<!-- kayıt noktası: Ö1, Ö4, Ö5 bitti; Ö3 ve Ö2 kaldı -->
