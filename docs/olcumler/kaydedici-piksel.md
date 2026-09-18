# Kaydedici Piksel Ölçümleri

Paket 2b. Makine: DISPLAY1 1024x768, konsol oturumu, ffmpeg 9.0 gyan full build.

## VP9 Profili ve Piksel Biçimi

Komut: `ffmpeg -f lavfi -i testsrc2=s=64x64:r=5:d=1 -c:v libvpx-vp9 -deadline realtime -cpu-used 8 -threads 1 -pix_fmt <biçim> -profile:v <profil>`, çıktı ffprobe ile okundu.

| Profil | Biçim | Çıkış kodu | ffprobe |
|---|---|---|---|
| 0 | yuv420p | 0 | Profile 0, yuv420p |
| 1 | yuv420p | -1094995529 | okunamaz; "Error encoding frame: Invalid parameter" |
| 1 | yuv444p | 0 | Profile 1, yuv444p |
| 2 | yuv420p | -22 | dosya yok |
| 2 | yuv420p10le | 0 | Profile 2, yuv420p10le |
| 3 | yuv444p10le | 0 | Profile 3, yuv444p10le |
| 0 | yuv444p | -1094995529 | okunamaz; "Error encoding frame: Invalid parameter" |
| 3 | yuv420p | -22 | dosya yok |
| 4 | yuv420p | -22 | dosya yok |
| main | yuv420p | -22 | "Unable to parse profile option value main" |

Sonuç: `-profile:v` 0..3 tamsayı; profil biçimden çıkar (`RecorderArguments.Vp9ProfileFor`), uyuşmayan çift `Validate`'te reddedilir. Ölçü `KayitFfmpegKoluTests.Vp9ProfilUyusmazligiFfmpegdeDeDuser` aynı komutu koşar.

## Renk Aralığı Adları

`ffmpeg -h full` ilk `-color_range` bloğu: `unknown`, `tv`, `pc`, `unspecified`, `mpeg`, `jpeg`, `limited`, `full`. Kaydedici kümesi `unknown`/`unspecified` dışındaki altı ad; eşitlik `KayitFfmpegKoluTests.RenkAraligiKumesiFfmpeginAdlariylaAyni` ile her koşumda ffmpeg'den okunur.

## Kamera Arka Planı: Anahtar Eşiği

Ölçü `KaydediciArkaPlanTests.AnahtarlananKameraKaresiPikseldeArkaPlaniBirakir`: 320x240 kırmızı
taban, 160x120 yeşil kamera, 0,5 sn'de beliren mavi kutu, 0,9 sn karesinin pikselleri (ffmpeg 9.0).

| Kip | Kutu | Kamera arka planı | Kamera dışı |
|---|---|---|---|
| Keep | mavi | yeşil | kırmızı |
| Static (backgroundkey 0,8), küçük kutu | mavi | kırmızı | kırmızı |
| Green (chromakey 0x00FF00, 0,15) | mavi | kırmızı | kırmızı |
| Static 0,8, kameranın dörtte üçü | mavi | kırmızı | kırmızı |
| Static 0,08 (ffmpeg varsayılanı), dörtte üç | kırmızı | kırmızı | kırmızı |
| Static 0,5, dörtte üç | kırmızı | kırmızı | kırmızı |

`backgroundkey`'in `threshold`'u sahne değişimi eşiği: aşılınca o kare yeni arka plan olur ve
beliren nesne de silinir. Kadraja giren kişi büyük alan kapladığı için 0,8 seçildi.

## Bindirmeler: gfxcapture Pikselleri

Araç `tools/kaydedici-piksel`, ölçüm 17 Eylül 2026. Ölçek 1, birincil ekran 0,0 1024x768.
Desen penceresi (100,100) konumunda 480x240: sol üst kırmızı, sağ üst yeşil, sol alt mavi, sağ alt beyaz.
Bindirmeler uygulamanın gerçek pencere türleri, Neon paleti (`NeonEmber` #FF0033). Her yakalama:
`ffmpeg -filter_complex "gfxcapture=monitor_idx=0:capture_cursor=0:max_framerate=20,hwdownload,format=bgra,crop=520:420:0:0,split[a][b]"`,
1,5 sn, çıkış kodu 0, 27-30 kare; piksel son kareden okunur, tolerans kanal başına ±40. Ham çıktı
`.calisma/paket-2b/piksel/` (`sonuc.tsv`, kare başına `.bgra`, son kare `.png`, ffmpeg günlüğü).

| Durum | Nokta (ekran px) | Beklenen | Okunan |
|---|---|---|---|
| Yalnız desen | (140,140) / (380,140) / (140,300) / (380,300) | #FF0000 / #00FF00 / #0000FF / #FFFFFF | aynı dördü |
| Çerçeve, affinity 0x11 (`CaptureExcluded` True) | sol kenar (139,160) | desen #FF0000 | #FF0000 |
| aynı | üst kenar (320,129) | desen #00FF00 | #00FF00 |
| aynı | iç (200,160) | #FF0000 | #FF0000 |
| Negatif: çerçeve `ExcludeFromCapture=false` (`CaptureExcluded` False) | sol kenar (139,160) | #FF0033 | #FF0033 |
| aynı | üst kenar (320,129) | #FF0033 | #FF0033 |
| aynı | iç (200,160) | #FF0000 | #FF0000 |
| Negatif: çerçeve yok | sol / üst / iç | #FF0000 / #00FF00 / #FF0000 | aynı üçü |
| Halka (`RecorderClickRing`, tıklama 180,160; pencere 156,136 48x48) | sol çizgi (158,160) | #FF0033 | #FF0033 |
| aynı | üst çizgi (180,138) | #FF0033 | #FF0033 |
| aynı | merkez (180,160) | desen #FF0000 | #FF0000 |
| Negatif: halka yok | sol / üst / merkez | #FF0000 | #FF0000 üçü |
| Halka `RecorderInputOverlay.Ring` yolu, 2,5 sn yakalama | sol çizgi (158,160), 48 kare | halka birkaç karede, sonra söner | #FF0033 8 karede (15-22), son kare #FF0000; pencere 379 ms sonra gizlendi |
| Büyüteç (`RecorderMagnifier.Follow(250,160)`, kaynak 214,124 72x72, pencere 310,220 144x144, `LastGrab` True) | kenar (311,292) | #FF0033 | #FF0033 |
| aynı | mercek sol (350,292), kaynağı kırmızı | #FF0000 | #FF0000 |
| aynı | mercek sağ (440,292), kaynağı yeşil | #00FF00 | #00FF00 |
| Negatif: büyüteç yok | kenar / mercek sol / mercek sağ | desen #FFFFFF | #FFFFFF üçü |

Sonuç: affinity 0x11 çerçeveyi kayıttan çıkarıyor, aynı pencere affinity'siz kayda giriyor; halka ve
büyüteç kayda giriyor, kapalıyken desen okunuyor. Büyütecin `BitBlt` kopyası bu makinede deseni
görüyor (gdigrab'ın siyah masaüstünden farklı olarak).

Ölçülmeyen: affinity'li çerçevenin ekranda göründüğü (tanımı gereği yakalamada görünmez; aynı kodun
affinity'siz kolu çizildiğini gösteriyor). Büyüteç `RecorderMagnifierHost` üzerinden değil, `Follow`
sabit noktayla çağrılarak ölçüldü (kullanıcının imleci oynatılmadı). Tek ekran, ölçek 1; bu tablonun bindirme pikselleri çoklu ekranda ve ölçek ≠ 1'de ölçülmedi. Yakalama dikdörtgeninin çoklu ekran ve ölçek ≠ 1 hesabı ayrı ölçüldü: `kaydedici-coklu-ekran.md`.

## Tıklama Sesi

Varsayılan çıkış uç noktasının `IAudioMeterInformation.GetPeakValue` değeri 2 ms arayla 500 ms
okundu (Stereo Mix yok, dshow listesinde yalnız mikrofonlar var; `waveOutGetNumDevs` 6). Her satırdan
önce 150 ms taban tepe okundu, hepsinde 0,0000.

| Çağrı | Dönüş | Tepe | Tepe > 0,01 örnek | İlk ses (ms) |
|---|---|---|---|---|
| Sessizlik 1 | — | 0,0000 | 0/172 | — |
| `PlaySoundW(ClickTone.Wave(), SND_ASYNC\|SND_NODEFAULT\|SND_MEMORY)` | True | 0,4936 | 18/177 | 68 |
| Sessizlik 2 | — | 0,0000 | 0/174 | — |
| `Win32ClickSound.Play()` (dönüşü void) | — | 0,4936 | 28/174 | 32 |
| Negatif: `NoClickSound.Play()` (ses kapalı yolu) | — | 0,0000 | 0/178 | — |
| Negatif: olmayan WAV, `SND_ASYNC\|SND_NODEFAULT\|SND_FILENAME` | True | 0,0000 | 0/177 | — |
| Negatif: olmayan WAV, senkron `SND_NODEFAULT\|SND_FILENAME` | False | 0,0000 | 0/172 | — |
| Sessizlik 3 | — | 0,0000 | 0/175 | — |

Sonuç: tıklama tonu varsayılan çıkışa ulaşıyor; tepe 0,49, tonun 0,5 genliğiyle uyumlu. `SND_ASYNC`
ile `PlaySoundW` yalnız kuyruğa almayı bildirir: olmayan dosyada da True döner, sessizliği tepe ölçer
gösterir. Uygulamanın `Win32ClickSound.Play` dönüşü atıyor, çalmadığını kendisi bilemez.

Ölçülmeyen: hoparlörden fiziksel çıkış (tepe ölçer karıştırıcıya giren akışı okur) ve kaydın ses
izine girip girmediği (sistem sesi yakalaması bu ölçüde yok).
