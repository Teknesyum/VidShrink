# Dış Altyazı Dosyasını Görüntüye Yakma — 7 Ekim 2026

`--yak-srt` (HandBrake `--srt-burn`) ve `--yak-ass` (`--ssa-burn`) için `subtitles` süzgecinin
(ffmpeg'in libass ile çizdiği altyazı süzgeci) Windows yolunu nasıl okuduğu ölçüldü.
ffmpeg 9.0 (gyan full), aynı anda tek süreç, `-threads 2`, libx264 ultrafast, CRF 30.

Ölçen sınıf `tests/VidShrink.Tests/DisAltyaziYakmaTests.cs`; sayılar onun canlı kollarının
yazdığı satırlardan.

## Kaynak Ve Ölçü

Kaynak `color=c=black:size=320x240:rate=25` (tam siyah kare) + 440 Hz sinüs, 2 sn (kesit
kolunda 14 sn). Çıktının 1. saniyesindeki kare gri tonlu ham bayta çevrilir, üst ve alt yarının
en parlak pikseli okunur (0 siyah, 255 beyaz). Siyah karede parlak piksel yalnız yazıdan gelir.

## Yol

Altyazı şu klasöre yazıldı, çıktı da aynı klasöre:

`.calisma\dis-altyazi-yakma\Çığ şöğü [1080p], it's; x=1\alt yazı (türkçe).srt`

Türkçe harf, boşluk, köşeli ve yuvarlak parantez, virgül, noktalı virgül, eşittir ve tek tırnak
bir arada. Süzgece giden metin `VideoFilterChain.FilterPath`'ten geçer: `\` → `/`, `:` → `\:`,
`'` → `'\\\''`; tamamı `filename='...'` içinde.

## Sonuç

| Kol | Üst yarı | Alt yarı |
|---|---|---|
| SRT, zor yol | 0 | 255 |
| Aynı kaynak, yakmasız (olumsuz kontrol) | 0 | 0 |
| ASS, biçem `Alignment 8` (üstte), zor klasör | 255 | 0 |
| SRT, kesit 11,5-14 sn; yazı dosyada 12-14 sn | 0 | 255 |

- SRT altta, ASS biçeminin dediği yerde (üstte) çizildi: `--yak-ass` biçemi koruyor.
- Yakılan çıktıda altyazı izi yok (ffprobe).
- Kesitte yazı dosyanın kendi saatiyle okundu: çıktının 1. saniyesi kaynağın 12,5. saniyesi.
  Süzgeç `setpts=PTS+1.5/TB,subtitles=...,setpts=PTS-1.5/TB` ile sarılı.

## Sıra

Zincirde yakma kırpma ve döndürmeden sonra, ölçeklemeden önce: `crop` < `subtitles` < `scale`.
Yazı kaynak çözünürlüğünde çizilip görüntüyle birlikte küçülür; kaynak izini yakan `--yak` ile
aynı yer. İki geçişin `-vf` metni aynı (test pimli). HandBrake'in sırası kaynağından
doğrulanmadı: bu turda ağ çağrısı yoktu.

## Mutasyonlar

Koşum süzgeci `DisAltyaziYakmaTests|AltyaziYakmaTests`, 39 kol. Her mutasyon elle geri alındı.

| Mutasyon | Kırmızı |
|---|---|
| Tek tırnak kaçışını kaldırmak | 6 (üç canlı kol dahil) |
| `:` kaçışını kaldırmak | 9 (dört canlı kol dahil) |
| `BurnFile`'ı `ChangesPicture`'dan çıkarmak (kopya yolu açık kalır) | 1 |
| libass denetiminden dış dosyayı çıkarmak | 2 |
| Kesitte `setpts` sarmasını dosya yolunda kaldırmak | 2 (canlı kesit kolu dahil) |
| `--yak` ile dosya yakmanın çakışma denetimini kaldırmak | 2 |

## Ölçülmedi

- UTF-8 dışı kodlamalı SRT (`charenc`): seçenek yok, dosya UTF-8 kabul edilir.
- UNC yolu (`\\sunucu\paylasim\...`) ve 260 karakteri aşan yol.
- libass'sız derlenmiş gerçek bir ffmpeg: elde yok. Denetim sahte yoklamayla ölçüldü
  (`CliServices.HasFilter`).
- ASS'in istediği yazı tipi sistemde yokken libass'ın seçtiği yedek.
- macOS ve Linux yolları; CI'da ffmpeg yoksa canlı kollar atlanır.
