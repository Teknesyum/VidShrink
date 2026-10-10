# Altyazı Yakmanın Üç Ölçülmemiş Hali — 10 Ekim 2026

Dış altyazı dosyasını görüntüye yakmanın (`--yak-srt`, `--yak-ass`) üç hali ölçüldü: UTF-8
olmayan dosya, sıra dışı yol, libass'sız ffmpeg. ffmpeg 9.0 (gyan full), Windows 11, aynı anda
tek süreç, `-threads 2`, libx264 ultrafast, CRF 30.

Ölçen sınıf `tests/VidShrink.Tests/AltyaziYakmaKodlamaYolTests.cs` (28 kol) ve
`IzPaneliTests.LibasssizFfmpegdeMetinIziYakmaListesindeYok`.

## Kaynak Ve Ölçü

Kaynak `color=c=black:size=320x240:rate=25` + 440 Hz sinüs, 2 sn. Altyazı tek satır:
`Çığ şöğüş İı` (Türkçe'ye özgü harfler; Windows-1254'te tek bayt, UTF-8'de iki bayt).

Çıktının 1. saniyesindeki kare gri tonlu ham bayta çevrilir (76 800 bayt). Alt yarıda parlaklığı
200'ün üstündeki piksel sayılır ve karenin tamamı UTF-8 dosyayla yakılmış referans kareyle bayt
bayt karşılaştırılır. Siyah karede parlak piksel yalnız yazıdan gelir.

Referans (UTF-8, BOM'suz): çıkış 0, alt yarıda 214 parlak piksel, md5 `945942a548ce…`.

## 1. UTF-8 Olmayan Dosya

İddia: "bozuk harf sessizce çıkıyor olabilir." Ölçülen: bozuk harf çıkmıyor, ama ham ffmpeg
hatası çıkıyor. İkisi de kusur; bulunan ikincisi.

Düzeltmeden önce, dosya süzgece olduğu gibi verilince:

| Dosya | ffmpeg çıkışı | Kare |
| --- | --- | --- |
| UTF-8 | 0 | referans |
| UTF-8 + BOM | 0 | referansla aynı |
| Windows-1254 | -1094995529 (kabukta 183) | 0 bayt |
| UTF-16 LE + BOM | -1094995529 | 0 bayt |
| UTF-16 BE + BOM | -1094995529 | 0 bayt |

Üç kırmızı satırda ffmpeg'in yazdığı:

```
[srt @ ...] Invalid UTF-8 in decoded subtitles text; maybe missing -sub_charenc option
[Parsed_subtitles_0 @ ...] Error decoding: Invalid data found when processing input (ignoring)
```

Süzgecin kendi `charenc` seçeneği de denendi: `charenc=CP1254` referansla aynı kareyi veriyor,
`charenc=UTF-16` ise çıkış 0 ile yanlış kare (28 parlak piksel) veriyor. Sessiz bozulma tam
olarak bu yolda; o yüzden `charenc` kullanılmadı.

### Düzeltme

`Core/SubtitleCharset.Read` dosyayı okur: BOM'a bakar, BOM'suz UTF-16'yı sıfır baytların
yerinden tanır, geçerli UTF-8 ise dokunmaz, değilse sistemin ANSI kod sayfasıyla (Türkçe
Windows'ta 1254) çözer. CLI çözülen metni `vidshrink_altyazi_<guid>.srt` adıyla UTF-8 yazar,
onu yakar, iş bitince siler ve varsayımı söyler:

```
Yakılan altyazı: dosya UTF-8 değil, Windows-1254 olarak okundu. Harfler yanlış çıkarsa dosyayı UTF-8 olarak kaydedin.
```

Kodlama çıkarılamazsa (sıfır baytlı ama UTF-16'ya benzemeyen dosya, ANSI sayfası UTF-8 ya da
çok baytlı olan sistem) iş başlamadan reddedilir, çıkış 64:

```
Yakılacak altyazı dosyasının karakter kodlaması anlaşılamadı; dosyayı UTF-8 olarak kaydedip yeniden deneyin: <yol>
```

Düzeltmeden sonra (canlı kol `CanliUtf8OlmayanSrtUtf8IleAyniYakiliyor`):

| Dosya | Okunan | ffmpeg çıkışı | Kare |
| --- | --- | --- | --- |
| Windows-1254 | Windows-1254 | 0 | referansla aynı, 214 piksel |
| UTF-16 LE + BOM | UTF-16LE | 0 | referansla aynı, 214 piksel |
| UTF-16 BE + BOM | UTF-16BE | 0 | referansla aynı, 214 piksel |

## 2. Yol

İddia: "UNC, 260'tan uzun ve ayraçlı yol bozuluyor olabilir." Ölçülen: bozulmuyor. Kod
değişmedi, davranış pimlendi.

`VideoFilterChain.FilterPath`: `\` → `/`, `:` → `\:`, `'` → `'\\\''`; tamamı `filename='...'`
içinde. Saf kol `YolSuzgecKacisi` yedi yolu tam metinle pimler:

| Yol | Süzgece giden |
| --- | --- |
| `\\sunucu\paylasim\alt klasor\a.srt` | `//sunucu/paylasim/alt klasor/a.srt` |
| `\\localhost\C$\Videolar\a.srt` | `//localhost/C$/Videolar/a.srt` |
| `\\?\C:\Videolar\a.srt` | `//?/C\:/Videolar/a.srt` |
| `\\?\UNC\sunucu\paylasim\a.srt` | `//?/UNC/sunucu/paylasim/a.srt` |
| `C:\a[1], b; c=d %e #f\x.srt` | `C\:/a[1], b; c=d %e #f/x.srt` |
| `C:\it's\'a'.srt` | `C\:/it'\\\''s/'\\\''a'\\\''.srt` |
| `D:\a:b\c.ass` | `D\:/a\:b/c.ass` |

`UzunYolKisaltilmadanKacisli` 260'tan uzun yolun kısaltılmadan geçtiğini ölçer (kaçışlı metin
yalnız sürücü harfinin `\`'ı kadar, bir karakter uzun).

Canlı (`CanliUzunVeAyracliYoldanYakiliyor`); klasör adı `uzun [a], b'c; d=e %f #g`, yani ayraçlı
ve uzun yol aynı kolda:

| Yol | Uzunluk | ffmpeg çıkışı | Kare |
| --- | --- | --- | --- |
| Uzun ve ayraçlı, düz yazım | 350 karakter (elle ölçümde 421) | 0 | referansla aynı |
| Aynı yol `\\?\` önekiyle | 354 | 0 | referansla aynı |
| Kısa yol `\\?\` önekiyle | — | 0 | referansla aynı |

Bu makinede `LongPathsEnabled=1`. Kapalı olan makinede düz yazımlı 260 üstü yol ölçülmedi.

UNC canlı kolu (`CanliUncYolundanYakiliyor`) `\\localhost\C$` üstünden çalışır. Bu makinede
yönetim paylaşımı kapalı (yalnız `IPC$` var), kol kendini atladı:

```
\\localhost\C$ acilmiyor (yonetim paylasimi kapali); UNC canli olculmedi.
```

## 3. libass'sız ffmpeg

İddia: "seçenek sunuluyor ve ham ffmpeg hatası çıkıyor olabilir." CLI'da yanlış, arayüzde doğru.

CLI zaten koruyordu: yoklamada `subtitles` süzgeci yoksa iş başlamadan `error.no-libass` ile
reddediyor ("Bu ffmpeg derlemesinde subtitles süzgeci yok (libass'sız derlenmiş); altyazı
görüntüye yakılamaz."). Değişmedi.

Arayüz korumuyordu: yakma listesi kaynağın metin izlerini yoklamaya bakmadan sunuyordu, seçilince
iş ffmpeg'in "No such filter: 'subtitles'" hatasıyla düşerdi. Düzeltme: `RefreshBurnChoices`
metin izini yalnız `TextBurnAvailable` iken listeye alır; yoklama geldiğinde liste yenilenir.
Görüntü izi (PGS, VOBSUB, DVB) `overlay` ile yakıldığı için listede kalır. Yoklama okunamadıysa
metin izi sunulur; CLI'daki kuralla aynı.

Ölçü sahte yoklama çıktısıyla (`EncoderCapabilities.Parse`, süzgeç listesinde `subtitles`
satırı var ya da yok); gerçek libass'sız ikili indirilmedi.

| Yoklama | Liste (kapalı + izler) | Seçim |
| --- | --- | --- |
| `subtitles` var | 4 | korunur |
| `subtitles` yok | 2 (kapalı + PGS) | metin izi seçimi düşer |
| Yoklama yok | 4 | — |

## Mutasyon

Her biri tek satır, 87 kollu süzgeçle (`AltyaziYakmaKodlamaYolTests|DisAltyaziYakmaTests|IzPaneliTests`)
koşuldu, elle geri alındı.

| # | Bozulan | Kırmızı |
| --- | --- | --- |
| M0 | `SubtitleCharset`: ANSI sayfası ≤ 0 / 65001 kapısı kaldırıldı | 1 |
| M1 | CLI: UTF-8 olmayan dosya çevrilmeden geçti | 5 |
| M2 | CLI: çıkarılamayan kodlama reddedilmedi | 1 |
| M3 | CLI: geçici kopya silinmedi | 2 (ikincisi kalan kopyanın bulaşması) |
| M4 | Arayüz: `TextBurnAvailable` hep doğru | 1 |
| M5 | `FilterPath`: `\` → `/` kaldırıldı | 16 |
| M6 | Arayüz: yoklama gelince liste yenilenmedi | 1 |

Mutasyonsuz: 87 kol, 86 geçti, 1 atlandı (UNC).

## Ölçülmedi

- Gerçek UNC yolundan yakma: bu makinede paylaşım yok. Kaçış saf kolla pimli, ffmpeg'in
  `//sunucu/pay/…` yolunu açtığı ölçülmedi.
- `LongPathsEnabled=0` olan makinede uzun yol.
- Gerçek libass'sız ffmpeg ikilisi; yalnız yoklama çıktısı sahtelendi.
- ANSI kod sayfası bir tahmindir (`CultureInfo.CurrentCulture`). Türkçe sistemde Windows-1252
  dosya 1254 diye okunur; çoğu harf aynıdır, birkaçı değildir. Kodlamayı elle söyleyen bayrak
  yok. Shift-JIS, GBK gibi çok baytlı sayfalar reddedilir, çevrilmez.
- Arayüzde dış dosya yakma yok; 1. madde yalnız CLI'yı ilgilendirir. Kaynağın kendi metin izi
  kapsayıcıdan UTF-8 çıkar.
- Düzenleyicinin yazı bindirmesi (`Core/Editing/EditExport.cs`, `ass=` süzgeci) libass'a bakmıyor.
- Altyazı taraması durum satırı, libass'sız derlemede artık sunulmayan bir metin izini anabilir.
- macOS ve Linux.
