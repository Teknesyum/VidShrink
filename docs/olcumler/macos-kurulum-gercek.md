# Kurulum betiği gerçek Mac'te — 23 Ağustos'tan beri açık madde kapandı

İş 3'ün raporu. `install-vidshrink.sh` ilk kez gerçek bir Apple Silicon Mac'te koştu.

## Düzenek

Apple M1 · 8 çekirdek · 8 GB · arm64 · macOS 26.6.2 (25G83) · ffmpeg 9.0.1 (Homebrew)
· dal `serkan/macos-olcum`, taban `53bbc16`.

Bu bölümdeki koşumlar stok `homebrew/core` ffmpeg 9.0.1_1 ile yapıldı. Aynı tur
içinde sonradan `zimg`li derlemeye geçildi (`macos-gercek-kosum.md`, "Düzeltme");
buradaki sayılar o değişiklikten öncesine aittir ve kurulum betiği ffmpeg'e
dokunmuyor.

Yayın: `v0.2.5`, `Teknesyum/VidShrink` — betiğin kendi bulduğu son yayın.

## K1 — Betiğin tam çıktısı ve çıkış kodu

Önce hesap temizlendi (paket, düz kurulum dizini ve kısayol birlikte):

```
sh install-vidshrink.sh --uninstall
```

```
Silindi:
/Users/serkan/Applications/VidShrink.app
/Users/serkan/.local/bin/vidshrink
```
çıkış kodu 0. `~/.local/share/vidshrink` zaten yoktu — macOS'ta artık kullanılmıyor.

Sonra kurulum:

```
sh install-vidshrink.sh
```

```
VidShrink kurulumu hazırlanıyor...
Son yayın aranıyor...
Kurulacak sürüm: 0.2.5
Yayın paketi indiriliyor...
İndirilenler doğrulanıyor...
VidShrink 0.2.5 kuruldu: /Users/serkan/Applications/VidShrink.app
Yeni sürümleri uygulama kendisi kuruyor; Ayarlar altından kapatabilirsiniz.
Kaldırmak için: --uninstall
Çalıştırmak için: vidshrink
```

**Çıkış kodu 0. Hiçbir satırda düşmedi.** Sağlama doğrulaması ("İndirilenler
doğrulanıyor...") geçti, yani `checksums-osx-arm64.txt` ile inen arşiv tuttu.

`LOG.md:25`teki T13 mühür notu — *"betik hâlâ gerçek macOS/Linux makinede
koşturulmadı"* — bu koşumla kapanıyor: macOS/arm64 tarafı koştu. Linux hâlâ
koşturulmadı.

### Kurulumun bıraktığı iz

```
ls -la ~/Applications/VidShrink.app/Contents/
codesign --verify --strict ~/Applications/VidShrink.app
/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" ~/Applications/VidShrink.app/Contents/Info.plist
```

| | |
|---|---|
| Paket | `~/Applications/VidShrink.app` |
| `CFBundleShortVersionString` | `0.2.5` — `Directory.Build.props`taki `<Version>` ile aynı |
| İmza | `codesign --verify --strict` sessiz döndü, geçerli |
| Kısayol | `~/.local/bin/vidshrink` -> `.../VidShrink.app/Contents/MacOS/VidShrink` |
| Düz kurulum dizini | yok — macOS'ta yük yalnız paketin içinde |

## K2 — Motor uçtan uca: bir dosya sıkıştırıldı

**Sınır önce söylensin: bu koşum kurulu `.app`in arayüzünden geçmedi.** Uygulamayı
ekrandan sürmek için istenen izin reddedildi (bkz. "Ölçülemeyenler"). Aşağıdaki
koşum aynı motorun — aynı `VidShrink.Core` / `VidShrink.Ffmpeg` derlemelerinin —
depo yapısındaki başsız yolundan geçiyor.

```
DOTNET_ROOT=$HOME/.dotnet /usr/bin/time -p \
  tools/VidShrink.Bench/bin/Release/net8.0/VidShrink.Bench shrink \
  .calisma/is3/deneme-1080p30.mp4 8 \
  --out .calisma/is3/cikti --results .calisma/is3/shrink.json
```

Girdi, ölçüm için üretilmiş sentetik bir klip (İş 2'nin ortak havuzu değil; burada
ölçülen kalite değil, motorun uçtan uca çalışıp çalışmadığı):

```
ffmpeg -f lavfi -i "testsrc2=size=1920x1080:rate=30:duration=30" \
       -f lavfi -i "sine=frequency=440:duration=30" \
       -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -c:a aac -b:a 128k \
       .calisma/is3/deneme-1080p30.mp4
```

| | |
|---|---|
| Girdi | `deneme-1080p30.mp4`, **33 023 073 bayt**, 1920x1080@30, 30,0 sn |
| Hedef | 8 MB |
| Çıktı | `deneme-1080p30_8mb.mp4`, **8 018 024 bayt** (7,65 MB), doluluk %95,58 |
| Plan | libx264 / 2pass / 2041k, 1920x1080@30, **deneme sayısı 1** |
| Süre — toplam duvar saati | **88,69 sn** (`/usr/bin/time -p`, `real`) |
| Süre — yoklama | 9,19 sn |
| Süre — plan | 3,89 sn |
| Süre — kodlama | 11,82 sn |
| Süre — ölçüm | 63,60 sn |
| VMAF-NEG ort / p10 / min | 90,481 / 88,773 / 86,776 |
| XPSNR | 36,749 |

Motorun ürettiği ffmpeg komutu, olduğu gibi:

```
ffmpeg -hide_banner -y -hwaccel auto -i .calisma/is3/deneme-1080p30.mp4 \
  -c:v libx264 -preset slow -b:v 2041k -maxrate 3061k -bufsize 4082k \
  -pass 2 -passlogfile .calisma/is3/cikti/pass -g 300 -keyint_min 30 \
  -pix_fmt yuv420p -c:a aac -b:a 96k -movflags +faststart \
  .calisma/is3/cikti/deneme-1080p30_8mb.mp4
```

İki şey buradan okunuyor:

- Motor bu Mac'te uçtan uca çalışıyor: hedefi ilk denemede tutturdu, banda girdi.
- **`auto` yine işlemci kodlayıcısı seçti (`libx264`).** İş 1'in `NoHardwareEncoder`
  ölçümüyle aynı yönde; sebebi İş 2'nin K4'üne ait.
- Toplam 88,69 saniyenin 63,60'ı ölçüm. Kullanıcının gördüğü sıkıştırma bunun
  dışında: yoklama + plan + kodlama = 24,90 sn.

## K3 — Güncelleme takası

**Kısmen koştu. Kanıt aşağıda, koşmayan kısım da yazılı.**

### Koşan: takas mantığı, gerçek dosya sistemi üzerinde

`MacUpdate`in üç ölçüsü bu Mac'te gerçekten koştu — Windows'ta erken dönüp
geçiyorlar (İş 1, K2 tablosu satır 5–7). Üçü de gerçek `codesign` çağırıyor ve
takası gerçek `renamex_np(..., RENAME_SWAP)` ile yapıyor:

```
dotnet test -c Release --no-build --filter "FullyQualifiedName~MacUpdateTests"
```

| Ölçü | Ne yaptı |
|---|---|
| `AVerifiedBundleSwapsIntoPlace` | 0.2.5 kurulu paket + 0.2.6 hazırlanmış paket kurdu, `Commit` çağırdı, kurulu paketin sürüm işareti 0.2.6 oldu, imza geçerli kaldı, eski paket hazırlama dizinine geçti |
| `ABrokenSignatureStopsTheSwap` | Mühürden sonra içeriden dosya değiştirdi, takas durdu, kurulu paket el değmemiş kaldı |
| `PreparingFromAReleaseLeavesASignedBundleBesideTheInstalledOne` | Yerel bir yayından arşiv açtı, her dosyanın özetini manifestle karşılaştırdı, yayının kendi `macos-app-bundle.sh`ini koşturdu, imzayı doğruladı; ikinci çağrı (aynı sürüm) `false` döndü |

`MacUpdateTests` 6 ölçünün 6'sı da geçti (`Passed: 16` satırı üç macOS sınıfının
toplamı: `MacOsStartupTests` 6 + `MacOsBundleTests` 4 + `MacUpdateTests` 6).

### Koşmayan: kurulu paketin kendi ömründeki takas

`MacUpdate.Begin()` uygulamanın açılışında, `MacUpdate.Finish()` ömrün `Exit`
olayında koşuyor (`App.axaml.cs:31` ve devamı). Bu yolu ölçmek uygulamayı açıp
kapatmayı gerektiriyor; ekran izni reddedildiği için **kurulu
`~/Applications/VidShrink.app` üzerinde gerçek bir 0.2.5 -> 0.2.6 takası
koşturulmadı.**

Koşturulabilir olduğu ölçüldü: `PrepareAsync` yerel bir kaynağı
`VIDSHRINK_UPDATE_SOURCE` ile kabul ediyor (`UpdateCheck.cs`, `ReadManifestAsync`
/ `ExtractAsync`), yani ağa çıkmadan, elde yapılmış bir 0.2.6 yayınıyla
denenebilir. İzin verilirse tek koşumluk iş.

## K4 — Simge

`macos-paket.md:140` "depoda macOS için `.icns` yok" diyor. **Bu hâlâ doğru ama
kurulan pakette simge var** — betik onu kurulum sırasında üretiyor.

```
ls -la ~/Applications/VidShrink.app/Contents/Resources/
sips -g pixelWidth -g pixelHeight ~/Applications/VidShrink.app/Contents/MacOS/VidShrink.png
iconutil -c iconset ~/Applications/VidShrink.app/Contents/Resources/VidShrink.icns \
  -o .calisma/is3/VidShrink.iconset
```

| | |
|---|---|
| `Contents/Resources/VidShrink.icns` | **903 981 bayt**, var |
| Üretildiği görsel | `Contents/MacOS/VidShrink.png`, 1254x1254, **568 972 bayt** |
| `.icns` içindeki boy | **10 tane**: 16, 32, 128, 256, 512 ve her birinin `@2x`'i |
| `Info.plist` `CFBundleIconFile` | `VidShrink` |

On boyu tek tek saydım, `iconutil -c iconset`in yazdığı dizinden:
`icon_16x16`, `icon_16x16@2x`, `icon_32x32`, `icon_32x32@2x`, `icon_128x128`,
`icon_128x128@2x`, `icon_256x256`, `icon_256x256@2x`, `icon_512x512`,
`icon_512x512@2x`.

Yani `macos-paket.md:147`deki "görselin bulunmadığı bir yayında paket simgesiz
kalıyor" durumu **v0.2.5'te geçerli değil**: `VidShrink.png` arşivin içinde
geliyor, `sips` + `iconutil` zinciri koşuyor ve simge üretiliyor.

Paketin gerçek simgesi (`.icns`ten çıkarılmış 512x512):

![macOS paket simgesi](gorseller/macos-paket-simgesi.png)

**Ekran görüntüsü alınamadı** — Finder/uygulama penceresini görüntülemek için
istenen izin reddedildi. Yukarıdaki görsel ekrandan değil, kurulan paketin
`.icns`inden çıkarıldı; simgenin ne olduğunu gösterir, Dock'ta/Finder'da nasıl
göründüğünü göstermez.

## Ölçülemeyenler

| Ne | Neden |
|---|---|
| `.app`in arayüzünden sıkıştırma (K2) | Ekran denetimi izni reddedildi. Motor başsız yoldan ölçüldü. |
| Kurulu paketin gerçek güncelleme takası (K3) | Aynı sebep — `Begin()`/`Finish()` uygulama ömrüne bağlı. |
| Ekran görüntüsü (K4) | Aynı sebep. Simge `.icns`ten belgelendi. |

İzin iki kez istendi, ikisi de reddedildi: ilki İş 3'ün kendi koşumunda, ikincisi
istek üzerine yeniden. İkinci istek VidShrink ve Finder içindi, dönen cevap
`{"granted":[],"denied":[com.teknesyum.vidshrink, com.apple.finder]}`. Ekranı
başka yoldan (`screencapture`, `osascript`) almaya çalışmadım; reddedilen izin
etrafından dolaşılmaz. Bu üç madde izin verildiği anda tek koşumluk iştir.
| Linux'ta betik | Bu paketin kapsamı dışında; T13 notunun Linux yarısı açık kalıyor. |
