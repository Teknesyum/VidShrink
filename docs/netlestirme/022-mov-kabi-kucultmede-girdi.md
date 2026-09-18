# 022 — Küçültmede MOV Kabı: Danışma Girdisi

Fable'a verilen metin, birebir:

---

VidShrink'in **küçültme** kolu üç kap tanıyor: MP4, MKV, WebM
(`OutputContainer { Mp4, Mkv, WebM }`, `StreamMapping.cs:7`). Kap kullanıcıya
sorulmuyor; `ContainerFor` istekten türetiyor (tüm izler korunacaksa MKV, değilse MP4) ve
`ContainerOf` çıktı yolunun uzantısından okuyor. Küçültme çıktısının adı çoğunlukla
kendiliğinden üretiliyor, yani kullanıcı pratikte kap seçmiyor.

Ayrı bir **dönüştür** sekmesi var; orada kap açık bir açılır kutu ve MOV zaten listede
(`MainWindow.axaml:874`), AVI/GIF/MP3/M4A/WAV ile birlikte.

HandBrake 1.11.0 `av_mov`'u ekledi; kurulu 1.11.2 dört kap listeliyor
(av_mp4/av_mov/av_mkv/av_webm). Bizim küçültme kolumuzda MOV yok — envanterde E2 açığı
olarak duruyor.

MOV, MP4 ailesinden: aynı muxer soyu, `+faststart` çalışıyor, altyazı `mov_text`. Ayrıldığı
yer ses kopyalama: MP4'te kopyalanabilir saydığımız `opus` ve `flac` MOV'da geçmiyor;
MOV'da aac/ac3/eac3/mp3 (ve alac) var.

Bugün `.mov` uzantılı bir çıktı yolu verilirse `ContainerOf` onu sessizce MP4 sayıyor:
ffmpeg uzantıdan mov muxer'ı seçiyor ama biz MP4 kurallarıyla iz haritası kuruyoruz —
opus kopyalama kararı MOV'da patlayabilir.

Sorular:

1. Küçültme koluna MOV eklenmeli mi, yoksa "MOV dönüştür sekmesinin işi" deyip
   `ContainerOf`'un `.mov`'u sessizce MP4 sayması mı düzeltilmeli (örneğin reddetmek)?
2. Eklenecekse kullanıcı MOV'u nereden seçiyor? Küçültmede kap kutusu yok; yeni bir kutu
   açmak mı, çıktı uzantısından türetmek mi, CLI'da bir bayrak mı?
3. MOV'un MP4'ten ayrıldığı ses kuralı (opus/flac kopyalanamaz) nasıl ele alınsın —
   sessizce yeniden kodlamak mı, kullanıcıya söylemek mi?

Kısıt: kullanıcı kolaylığı ön planda. Uydurma renk/ölçü yok. Yanıt Türkçe, kısa.
