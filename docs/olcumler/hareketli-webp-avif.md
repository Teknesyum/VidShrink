# Hareketli WebP Ve AVIF

Tarih: 2026-10-05. Dönüştür sekmesine iki kap eklendi: hareketli WebP ve hareketli AVIF.
Bu belge ön koşul ölçümünü ve GIF ile boyut kıyasını taşır. Test:
`tests/VidShrink.Tests/HareketliWebpAvifTests.cs`.

## Ön Koşul: Paketteki Ffmpeg Bu İkisini Taşıyor Mu

Yerel ffmpeg `ffmpeg version 9.0-full_build-www.gyan.dev`; `ffmpeg.exe` sha256'sı
`05F4251BCE9293C2AB492CB17CA7724A0FFD0D06C881BA2EE83B82A89C2FC740`. Bu, `Install-VidShrink.ps1`
ve `Core/Setup/SetupModel.cs` içindeki x64 piniyle aynı özet; `ci.yml` ve `release.yml` aynı
arşivi (`GyanD/codexffmpeg 9.0 full_build`) indiriyor. Yani yerel, CI ve Windows x64 kurucusu
aynı ikiliyi koşuyor.

```
ffmpeg -hide_banner -encoders | findstr "webp av1"
 V....D libaom-av1           libaom AV1 (codec av1)
 V....D librav1e             librav1e AV1 (codec av1)
 V..... libsvtav1            SVT-AV1(Scalable Video Technology for AV1) encoder (codec av1)
 V....D libwebp_anim         libwebp WebP image (codec webp)
 V....D libwebp              libwebp WebP image (codec webp)

ffmpeg -hide_banner -muxers | findstr "webp avif gif"
  E  avif            AVIF
  E  gif             CompuServe Graphics Interchange Format (GIF)
  E  webp            WebP
```

1 sn'lik 160x90 deneme (`-threads 2`, kaynak `testsrc2=size=160x90:rate=15:duration=1`):

| Komutun Kodlayıcı Kısmı | Çıkış Kodu | Bayt | ffprobe |
|---|---|---|---|
| `-c:v libwebp_anim -loop 0` → `.webp` | 0 | 33972 | `webp_anim`, 15 kare |
| `-c:v libsvtav1 -crf 35 -pix_fmt yuv420p -loop 0 -f avif` | 0 | 10951 | `av1`, iki akış: 1 kare (kapak öğesi) + 15 kare |
| `-c:v libaom-av1 -crf 35 -cpu-used 8 -f avif` | 0 | 6917 | `av1`, aynı yapı |
| `-c:v libwebp_uydurma` (negatif kontrol) | -1129203192 | — | `Unknown encoder 'libwebp_uydurma'` |
| `-f avif_uydurma` (negatif kontrol) | -22 | — | `Requested output format 'avif_uydurma' is not known.` |

Hüküm: ikisi de var, ikisi de yapıldı. AVIF için `libsvtav1` seçildi: uygulamanın AV1 kodlayıcısı
zaten o, hız ve kalite kolları (`-preset 8`, `-crf`) `CodecModel`'de hazır.

Hareketli AVIF dosyasında iki video akışı var: ilk kare durağan kapak öğesi, ikincisi
hareketli iz. `ffprobe -select_streams v:0` yalnız kapağı sayar ve 1 kare döner; kare sayısı
ikinci akıştan okunur.

Ölçülmeyen: arm64 kurucusunun BtbN `n9.0.1 winarm64-gpl` ikilisi, macOS (brew) ve Linux
(dağıtım paketi) ffmpeg'i. Bu makinede yoklar. Kodlayıcı orada yoksa dönüşüm ffmpeg'in
`Unknown encoder` hatasıyla düşer; kap seçeneği gizlenmez.

## Döngü Ve Ses

| Bayrak | Kaldırılınca Ne Oluyor (Ölçüldü) |
|---|---|
| `-loop 0` (WebP) | `ANIM` parçasındaki döngü sayısı 0'dan 1'e döner: canlandırma bir kez oynar, durur |
| `-loop 0` (AVIF) | Muxer varsayılanı zaten 0 (sonsuz); bayrak açık yazılıyor |
| `-an` | İki muxer de sesi kendiliğinden düşürüyor (çıkış kodu 0, ses akışı yok); bayrak niyeti açık yazıyor |

## Boyut Kıyası

Kaynak `testsrc2`, 2 sn, `libx264 -crf 12`. Her çevirme `-threads 2` ile, tek tek.
Uygulamanın varsayılanı (H.264 seçili, CRF 23) WebP'de `-quality 63`, AVIF'te `-crf 32` üretir;
en iyi kalite ucu (CRF 10) `-quality 100` ve `-crf 18`.

```
ffmpeg -hide_banner -y -nostdin -threads 2 -i k.mp4 -vf palettegen=stats_mode=diff palet.png
ffmpeg -hide_banner -y -nostdin -threads 2 -i k.mp4 -i palet.png -lavfi "[0:v][1:v]paletteuse=dither=sierra2_4a" k.gif
ffmpeg -hide_banner -y -nostdin -i k.mp4 -c:v libwebp_anim -quality 63 -an -loop 0 -f webp -threads 2 k.webp
ffmpeg -hide_banner -y -nostdin -i k.mp4 -c:v libsvtav1 -preset 8 -crf 32 -pix_fmt yuv420p -an -loop 0 -f avif -threads 2 k.avif
```

| Çıktı | 160x90, 15 Kare/Sn (30 Kare) | GIF'e Oran | 480x270, 30 Kare/Sn (60 Kare) | GIF'e Oran |
|---|---|---|---|---|
| Kaynak mp4 | 41411 | — | 276001 | — |
| GIF | 125495 | 1,00 | 858496 | 1,00 |
| WebP `-quality 63` (varsayılan) | 63550 | 0,51 | 336538 | 0,39 |
| WebP `-quality 100` | 142044 | 1,13 | 834456 | 0,97 |
| AVIF `-crf 32` (varsayılan) | 24735 | 0,20 | 160721 | 0,19 |
| AVIF `-crf 18` | 47009 | 0,37 | 342558 | 0,40 |

Sayılar bayt. Varsayılan kalitede WebP GIF'in yarısı ile beşte ikisi arasında, AVIF beşte biri.
En iyi kalite ucunda WebP GIF'le aynı boyda kalıyor (küçük klipte %13 daha büyük).

Sınır: `testsrc2` yapay bir desen; gerçek çekimde oranlar değişir. Görsel kalite ölçülmedi,
yalnız boyut. GIF 256 renge inerken öbür ikisi inmiyor; eşit kalitede kıyas değil.

## Tasarım Notları

- Kalite için yeni denetim yok. Kalite kutusundaki sayı seçili video kodeğinin CRF aralığında
  okunur (`CodecModel.CrfRange`), aralıktaki konumu WebP'de 100 → 0, AVIF'te 18 → 55'e çevrilir.
- Bit hızı kipinde AVIF `-b:v` alır. WebP'nin bit hızı kipi yok; o kipte `-quality` yazılmaz ve
  libwebp varsayılanı (75) kalır.
- Ses seçimi ne olursa olsun `-an` yazılır ve doğrulama ses yüzünden düşmez. Akış kopyası (video)
  reddedilir.
- İki kap listenin **sonunda**, GIF'in yanında değil: ayar dosyası kabı sıra numarasıyla saklıyor
  (`settings.Container`); araya eklemek kayıtlı MP3/M4A/WAV/FLAC seçimini kaydırırdı.
- HDR kaynak 8 bit `yuv420p`'ye ton eşlemesiz iner (GIF yolu gibi). Ölçülmedi.

## Mutasyonlar

| Mutasyon | Kırmızı |
|---|---|
| `-loop 0` kaldırıldı | 3/13 (iki argüman ölçüsü + canlı kol) |
| WebP kalitesi ters çevrildi (`share * 100`) | 6/13 |
| Ses kodlayıcı doğrulamasındaki muafiyet kaldırıldı | 10/13 |
| `libwebp_anim` yerine durağan `libwebp` | 2/13 |

Her mutasyon elle geri alındı; sonra 13/13 yeşil.
