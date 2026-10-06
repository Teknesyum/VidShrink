# Akıllı Kesme: Kodek Kapısının Ölçümü

Tarih: 2026-10-06. ffmpeg 9.0 (full build, Windows). Kapı: `src/VidShrink.Core/Editing/SmartCutCodec.cs`.

## Soru

Akıllı kesme kenardaki GOP'u (iki anahtar kare arası kare grubu) yeniden kodlar, gövdeyi
kopyalar. Kapı yalnız H.264 ve HEVC'yi geçiriyordu ve bu sınırın ölçülmüş bir gerekçesi yoktu:
`EditExport.MatchingEncoders` iki satırlık bir tabloydu, yanında ölçüm belgesi yoktu.

Ölçüm iki şeyi gösterdi. Eski yol kendi geçirdiği H.264'te bile B kareli kaynakta dört ölçütü
tutmuyordu. Ve doğru kurulan yol dört kodekte tutuyor.

## Dört Ölçüt

1. `ffprobe` hatasız okur, süre beklenenden en çok bir kare sapar.
2. `ffmpeg -v error -i cikti -f null -` stderr'i boş.
3. Paket ve kare sayısı beklenenle aynı (eksik ya da fazla kare yok).
4. Baş ile gövde aynı akışta çözülür: çıktının kodeği, profili ve piksel biçimi kaynakla aynı,
   gövde kareleri kaynakla bayt bayt aynı (PSNR sonsuz), kenar kareleri bozuk değil.

## Düzenek

Kaynak 6 sn, 320x240, 24 kare/sn, her 24 karede anahtar kare, AAC ses. Kesim 0,5–4,5 sn:
beklenen 4,000 sn, 96 kare, 72 gövde karesi. Her koşum `-threads 2`, tek süreç, sırayla.

```
ffmpeg -f lavfi -i testsrc2=s=320x240:r=24:d=6 -f lavfi -i sine=f=440:d=6 -threads 2 <kodlayıcı> -g 24 -c:a aac -shortest kaynak.<kap>
ffprobe -v error -select_streams v:0 -show_packets -show_entries packet=pts_time,flags -of csv=p=0 kaynak.<kap>
ffmpeg -ss 0.5 -t 0.5 -i kaynak -map 0:v:0 -an -vf setpts=PTS-STARTPTS <kenar kodlayıcı> -f <ara> v0
ffmpeg -ss <1 + pay> -i kaynak -t 3 -map 0:v:0 -an -c copy -frames:v 72 -avoid_negative_ts make_zero -f <ara> v1
ffmpeg -ss 4 -t 0.5 -i kaynak -map 0:v:0 -an -vf setpts=PTS-STARTPTS <kenar kodlayıcı> -f <ara> v2
ffmpeg -ss 0.5 -i kaynak -ss 0 -t 4 -map 0:a:0 -vn -c copy -avoid_negative_ts make_zero -f mpegts a0.ts
ffmpeg -f concat -safe 0 -i v.ffconcat -f concat -safe 0 -i a.ffconcat -map 0:v:0 -map 1:a:0 -c copy [-tag:v hvc1] cikti
```

Doğrulama:

```
ffprobe -v error -select_streams v:0 -count_packets -count_frames -show_entries stream=codec_name,profile,pix_fmt,codec_tag_string,nb_read_packets,nb_read_frames:format=duration cikti
ffmpeg -v error -threads 2 -i cikti -f null -
ffmpeg -v error -threads 2 -i cikti -i kaynak -lavfi "[0:v]select=lt(n\,96),settb=AVTB,setpts=N/24/TB[a];[1:v]select=between(n\,12\,107),settb=AVTB,setpts=N/24/TB[b];[a][b]psnr=stats_file=psnr.log" -an -f null -
```

Elle kurulan düzenek silindi; aynı komutları `tests/VidShrink.Tests/DuzenleyiciAkilliKodekTests.cs`
üretim kodunun planından koşturur.

## Sonuç

| Kol | 1 Süre | 2 Çözme stderr | 3 Paket/kare | 4 Birebir gövde / en düşük PSNR | Sonuç |
|---|---|---|---|---|---|
| h264 mp4 High 8 bit (B kareli) | 4,000 | 0 bayt | 96/96 | 72 / 46,12 | geçti |
| h264 mp4 High 10 | 4,000 | 0 | 96/96 | 72 / 46,94 | geçti |
| h264 açık GOP, naif sınır | 4,000 | 6398 bayt | pts bozuk | 0 / 11,76 | kaldı |
| hevc mp4 `hvc1` | 4,000 | 0 | 96/96 | 72 / 46,54 | geçti (`-tag:v hvc1` ile) |
| hevc mp4 `hev1` | 4,000 | 0 | 96/96 | 72 / 46,54 | geçti |
| hevc mkv (büyük arama payı) | 4,000 | 0 | 96/96 | 72 / 46,54 | geçti |
| hevc Main 10 | 4,000 | 0 | 96/96 | 72 / 46,55 | geçti |
| hevc açık GOP, naif sınır | 4,000 | 359 bayt | 96/94 | 69 / 18,57 | kaldı |
| av1 mp4 (libsvtav1) | 4,000 | 0 | 96/96 | 72 / 45,18 | geçti |
| av1 mkv | 4,000 | 0 | 96/96 | 72 / 45,18 | geçti |
| av1 10 bit | 4,000 | 0 | 96/96 | 72 / 45,21 | geçti |
| av1, kaynak libaom | 4,000 | 0 | 96/96 | 72 / 42,90 | geçti |
| vp9 mp4 Profile 0 | 4,000 | 0 | 96/96 | 72 / 48,63 | geçti |
| vp9 mkv | 4,000 | 0 | 96/96 | 72 / 48,63 | geçti |
| vp9 mkv Profile 2 (10 bit) | 4,000 | 0 | 96/96 | 72 / 49,19 | geçti |
| mpeg4 mp4 (B karesiz) | 4,000 | 0 | 96/96 | 72 / 48,65 | sayı geçti, gövde parçası "unknown profile" |
| mpeg4 `-bf 2` mkv | 4,000 | 5722 bayt | 96/96 | 24 / 17,00 | kaldı |
| mpeg4 libxvid mp4 | 4,000 | 0 | 93/93 | 0 / 16,99 | kaldı |
| mpeg4 libxvid qpel mkv | 3,993 | 0 | 88/88 | 0 / 14,98 | kaldı |
| mpeg2 `-bf 2` mp4 / mkv | 4,000 | 242 / 156 bayt | 96/96 | 0 / 18,35 | kaldı |
| mpeg2 `-bf 0` mp4 | 4,000 | 0 | 96/96 | 72 / 46,50 | geçti |

## Kapı Kararı

**Girer:** `h264`, `hevc`, `av1`, `vp9` — 8 bit `yuv420p` ve 10 bit `yuv420p10le`.

**Girmez, Tam kipe düşer:**

- MPEG-4 Part 2. Kenarı kodlayan `mpeg4` ile kaynağı yazan kodlayıcının (özellikle libxvid)
  akış başlığı tutmuyor; çözücü gövdeyi kenarın başlığıyla çözüp bozuk kare veriyor, B kareli
  kolda hata basıyor. Tek geçen kol B karesiz ve aynı kodlayıcıdan çıkan kaynak; kaynağın
  hangi kodlayıcıdan çıktığı dosyadan güvenle okunamaz.
- MPEG-2. Yalnız B karesiz kol geçti; B kareli iki kolda çözme hatası ve bozuk gövde.
- Ölçülmeyen her kodek (ProRes, uydurma ad, boş ad).
- 4:2:2, 4:4:4, 12 bit, `yuvj420p`, taramalı kaynak ve profili piksel biçimiyle uyuşmayan
  kaynak (örn. `High` + 10 bit). 10 bit hiçbir kolda 8 bite indirilmez.
- Kodlayıcısı bu ffmpeg derlemesinde olmayan kodek (`libx264`, `libx265`, `libsvtav1`,
  `libvpx-vp9`). Arayüzde mevcut "Tam kipe düşüldü" notu çıkar, yeni metin yok.
- Sesi AAC olmayan kaynak (ses kopyası kapısı değişmedi).

## Yolun Dayandığı Dört Kural

1. **Açık GOP anahtar karesi sınır olamaz.** Anahtar kareden sonra çözülüp ondan önce
   gösterilen paket varsa gövde oradan başlatılmaz (tablodaki iki "naif sınır" kolu). Temiz
   sınırı kalmayan klip tümüyle kaynağın kodeğiyle kodlanır.
2. **Gövde kare sayısıyla kesilir** (`-frames:v`). Kopyada `-t` çözme damgasına bakar, B
   kareli akışta bir-iki kare eksik ya da fazla bırakır.
3. **Arama payı kaba göre.** mp4 ailesi dışındaki kapta B kareli akışta ffmpeg arama noktasını
   3/23 sn geri çeker; pay 3/23 sn + 1 ms olur. Bu pay bir sonraki anahtar kareyi aşıyorsa
   klipte gövde kopyalanmaz.
4. **Görüntü ve ses ayrı listeden birleşir.** Görüntü parçaları sessiz yazılır ve her birinin
   süresi listeye yazılır; ses tek kopya olarak ikinci girdiden gelir. mp4 ara parçalar
   (`av1`, `vp9`) ortak 90000 zaman ölçeği alır; ölçek ayrıyken VP9 çıktısında kare sayısı bozuldu.

## Mutasyonlar

Taban 47/47 yeşil. Her satırda üretim kodu bozuldu, kırmızı görüldü, elle geri alındı.

| Bozulan | Kırmızı |
|---|---|
| `mpeg4` kapıya eklendi | 1/47 |
| `High` profili 10 bit kaynağa da eşlendi | 1/47 |
| Kodlayıcı yoklaması yok sayıldı | 1/47 |
| Açık GOP sınır kuralı kaldırıldı | 2/47 (biri canlı HEVC açık GOP kolu) |
| Arama payı hep 1 ms | 3/47 (biri canlı HEVC mkv kolu) |
| mp4 ara parçanın ortak zaman ölçeği kaldırıldı | 2/47 (biri canlı VP9 kolu; canlı AV1 kolu yeşil kaldı) |

## Doğrulanmayanlar

- Kare sınırına denk gelmeyen kesim başlangıcında bir kareden kısa kayma.
- Bütünüyle açık GOP'lu kaynakta klip baştan sona kodlanır ama kip "Akıllı" görünür.
- Renk meta verisinin (birincil renkler, aktarım eğrisi, HDR yan verisi) baş ile gövdede eşitliği.
- `hvc1` etiketli çıktının Apple oynatıcılarındaki davranışı; bant içi dizi başlığı taşımayan AV1.
- ffmpeg 9.0'dan eski sürümler; `.ts` kaynakta arama sezgisi.
- Ses dikişinde AAC paket çözünürlüğü kadar (yaklaşık 21 ms) kayma.
- Opus sesli VP9/AV1 (WebM) kaynak: ses kapısı AAC'de kaldığı için Tam kipe düşer, ölçülmedi.
