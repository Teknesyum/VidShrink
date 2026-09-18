# E6 — Altyazı Varsayılan Bayrağı ve İz Adı: Ölçüm

Tarih 2026-09-18. Kaynak `.calisma/e6/kaynak.mkv`: 2 sn testsrc + sine, ses izinin
başlığı "Ana Ses", gömülü srt'nin başlığı "Zorunlu TR" ve `forced` bayrağı açık.

Kaynak (`ffprobe`):

```
stream|index=0|codec_type=video|disposition:default=0|disposition:forced=0
stream|index=1|codec_type=audio|disposition:default=0|disposition:forced=0|tag:title=Ana Ses
stream|index=2|codec_type=subtitle|disposition:default=0|disposition:forced=1|tag:title=Zorunlu TR
```

`vidshrink kucult --hedef 0.01MB --cikti cikti2.mkv` (MKV, kodlamalı kol):

```
stream|index=0|codec_type=video|disposition:default=0|disposition:forced=0
stream|index=1|codec_type=audio|disposition:default=1|disposition:forced=0|tag:title=Ana Ses
stream|index=2|codec_type=subtitle|disposition:default=0|disposition:forced=1|tag:title=Zorunlu TR
```

`vidshrink kucult --hedef 0.01MB --cikti cikti.mp4` (MP4, kodlamalı kol):

```
stream|index=0|codec_type=video|disposition:default=1|disposition:forced=0|tag:language=und
stream|index=1|codec_type=audio|disposition:default=1|disposition:forced=0|tag:language=und
stream|index=2|codec_type=subtitle|disposition:default=1|disposition:forced=1|tag:language=und
```

Okunan üç şey:

1. **İz adı yalnız MP4'te düşüyor.** MKV çıktı iki başlığı da taşıyor; MP4 çıktıda
   `tag:title` hiç yok. `-map_metadata 0` genel etiketi taşıyor, iz başlığını değil.
2. **MP4'te üç iz de `default=1`.** Bunu biz istemiyoruz: `-disposition:a:0 default`
   yalnız sese yazılıyor (`StreamMapping.cs:85`), altyazının `default=1` olması mp4
   muxer'ının kendi varsayılanı. MKV'de altyazı `default=0` kalıyor.
3. **`forced` iki kapta da korunuyor.** Kaynağın `IsForced` alanı okunuyor ama hiçbir
   karara girmiyor; bayrak ffmpeg'in iz kopyalamasıyla kendiliğinden geçiyor.

Pass-through kolu bu ölçümün dışında: kaynak zaten hedefin altındaysa dosya kopyalanıyor
ve uzantı kaynağınkine çevriliyor (`EncodeRunner.cs:477-480`) — kap kararı orada
kasıtlı olarak kaynağındır.

## Düzeltmeden Sonra

`-metadata:s:a:0 title=` / `-metadata:s:s:N title=` ve `-disposition:s:N` yazıldıktan sonra
aynı kaynak, aynı komut:

MKV çıktı — istenen davranışın tamamı:

```
1 audio Ana Ses {'default': 1}
2 subtitle Zorunlu TR {'forced': 1}
```

MP4 çıktı — başlık taşınıyor, ama mp4'te `title` etiketi `name` adıyla duruyor:

```
1 audio {'language': 'und', 'handler_name': 'SoundHandler', 'name': 'Ana Ses'} {'default': 1}
2 subtitle {'language': 'und', 'handler_name': 'SubtitleHandler', 'name': 'Zorunlu TR'} {'default': 1, 'forced': 1}
```

**Ölçülen sınır:** MP4'te altyazının `default=1` olması düzeltilemiyor. ffmpeg 9.0'ın mp4
muxer'ı `-disposition:s:0 0` verilse bile her ize `default` yazıyor; doğrudan ffmpeg ile
kurulan negatif kontrol de aynı sonucu veriyor:

```
ffmpeg -i kaynak.mkv -map 0 -c:v libx264 -crf 40 -c:a aac -c:s mov_text -disposition:s:0 0 t0.mp4
0 video {'default': 1}
1 audio {'default': 1}
2 subtitle {'default': 1}
```

Bayrak yine de açık yazılıyor: MKV'de karar bizimdir ve kaynağın `IsDefault`/`IsForced`
alanı oraya birebir geçer; MP4'te muxer eziyor, ama yazmamak bu ezmeyi kaldırmıyor.
