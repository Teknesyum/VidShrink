# Video Düzenleme: ffmpeg ve mpv Teknikleri

VidShrink'e video düzenleme sekmesi eklemek için yapılan tarama. Kapsanan yetenekler: kesme,
birleştirme, hız değiştirme, parça kaldırma, geri oynatma, önizleme, küçük resim ve dalga biçimi.

Tarih: 2026-09-15. Hedef yığın: .NET 8 + Avalonia + ffmpeg + libmpv.

## Ölçüm Düzeneği ve Dürüstlük Notu

Bu raporun bir kısmı **bu makinede ölçüldü**, kalanı belge ve kaynak kodu okumasıdır.
Ayrım her tabloda `Ölçüldü` / `Doğrulanmadı` sütunuyla verilir. Başkasının ölçümü olan
sayılar "kaynağın iddiası" diye işaretlendi; hiçbiri bizim ölçümümüz gibi sunulmadı.

| Öğe | Değer |
| --- | --- |
| Makine | Windows 11 Pro 22631 |
| ffmpeg | `9.0-full_build` (gyan.dev), libx264 / SVT-AV1 / AAC |
| libmpv | `v0.41.0-1023-g69e63f425` (`.calisma/libmpv/libmpv-2.dll`) |
| Malzeme | `testsrc2` 1920x1080@30, 300 sn, libx264 ultrafast, `-g 250`, AAC. 584 MB |
| Anahtar kare aralığı | 8,3333 sn (36 anahtar kare) |
| Tekrar | **Tek koşum, tekrar yok.** Varyans ölçülmedi |

İki uyarı. Kaynak **sentetik** (`testsrc2`); gerçek kamera görüntüsünde kodlama süreleri
daha uzun, kod çözme benzer olur. Ses **saf sinüs**; `showwavespic` PNG boyutu (1336 bayt)
gerçek müzikte çok daha büyük olacaktır, o sayıyı genellemeyin.

mpv çalışma zamanı iddiaları (EDL, `play-direction`, `lavfi-complex`) **koşturulmadı** —
makinede `mpv` CLI yok, libmpv yalnız DLL olarak var. Hepsi belge okumasıdır.

Ham ölçüm kütüğü: [`docs/olcumler/video-duzenleme-olcum-kutugu-2026-09-15.txt`](../olcumler/video-duzenleme-olcum-kutugu-2026-09-15.txt)

**Projenin önceki ölçümüyle çapraz denetim.** `docs/olcumler/T32-anahtar-kare-olcumleri.md`
(24.08.2026, başka makine) anahtar kare dizinini aynı yöntemle çıkarıyor
(`-show_entries packet=pts_time,flags`) ve 60 sn'lik 1080p klipte **81,1 ms** buluyor.
Bu rapor 300 sn'lik klipte **201 ms** ölçtü — aynı büyüklük mertebesi, dosya uzunluğuyla
tutarlı yön. İki bağımsız ölçüm çelişmiyor. T32'nin "tekrar çağrı ucuzlamıyor, bir kere
çıkarılıp saklanmalı" hükmü bu rapor için de geçerlidir.

## ffmpeg 9 Kırılması

`-vsync` **kaldırıldı**. Ölçüm sırasında bu hatayla karşılaşıldı:

```
Unrecognized option 'vsync'.
```

Yerine `-fps_mode` (`passthrough` / `cfr` / `vfr`) kullanılır. Ayrıca `-r` ile CFR olmayan
bir `-fps_mode` birlikte verilemez: *"One of -r/-fpsmax was specified together a non-CFR
-vsync/-fps_mode. This is contradictory."* İnternetteki küçük resim tariflerinin çoğu hâlâ
`-vsync 0` yazıyor; VidShrink'in üreteceği argümanlarda bu geçmemeli.

---

# 1. Kesme

## 1.1 Ölçülen Fark

Aynı istek — 104,0 → 126,0 sn, yani **22,000 sn** — üç yöntemle:

| Yöntem | Süre | Çıktı uzunluğu | Hata | Kanıt |
| --- | --- | --- | --- | --- |
| `-c copy` (kayıpsız) | **65 ms** | 26,030 sn | **+4,030 sn** | Ölçüldü |
| Smart cut (kenar GOP) | **1111 ms** | 22,052 sn | +0,052 sn | Ölçüldü |
| Tam yeniden kodlama | **2550 ms** | 22,000 sn | 0 | Ölçüldü |

`preset medium`, 1080p30. Aynı iş `ultrafast` ile: smart 545 ms, tam 770 ms.

Kayıpsız kesmenin 4 saniyelik hatası tesadüf değil: istenen 104,0 sn'den geriye en yakın
anahtar kare 100,0 sn'de, fark tam 4,0 sn. Kesme noktası anahtar kareye denk gelirse hata
sıfırdır — ölçümün ilk turunda 100,0 sn seçilmiş ve yanlışlıkla "hatasız" görünmüştü.

Bu, kullanıcıya gösterilmesi gereken davranıştır: **kayıpsız kesmede çıktı, istenenden
anahtar kare aralığı kadar uzun olabilir.** Bizim malzemede bu 0–8,3 sn arası.

## 1.2 `-ss` / `-to` Semantiği — Ölçülmüş Tuzak

Bu, doğrudan yanlış çıktı üreten bir tuzak. Dördü de aynı niyetle yazıldı (100. sn'den
30 sn al), sonuçlar farklı:

| Komut | Çıktı | Ölçüldü |
| --- | --- | --- |
| `-ss 100 -i in -to 130` | **130,000 sn** | Evet |
| `-ss 100 -i in -to 130 -copyts` | 30,000 sn | Evet |
| `-ss 100 -i in -to 130 -copyts -start_at_zero` | 30,000 sn | Evet |
| `-ss 100 -i in -t 30` | 30,000 sn | Evet |

`-ss` girdi tarafındayken (`-i`'den önce) zaman sıfırlanır; `-to` **yeni sıfıra göre**
sayılır ve 130 saniyelik çıktı verir. `-copyts` mutlak semantiği geri getirir.

**Hüküm: VidShrink `-to` kullanmasın, `-t <süre>` kullansın.** Süre, çizelgede zaten
hesaplı; `-t` yerleşimden bağımsız olarak doğru çalışan tek biçimdir.

Belge (`doc/ffmpeg.texi`, `-ss`): *"When used as an input option (before `-i`), seeks in
this input file to position. Note that in most formats it is not possible to seek exactly,
so ffmpeg will seek to the closest seek point before position. When transcoding and
`-accurate_seek` is enabled (the default), this extra segment between the seek point and
position will be decoded and discarded. When doing stream copy or when `-noaccurate_seek`
is used, it will be preserved."*

Son cümle baştaki bozuk kare sorununun kaynağıdır: kopyalama sırasında istenen andan
önceki kareler atılamaz, çünkü atmak için çözmek gerekir.

`-avoid_negative_ts` değerleri: `auto` (-1, varsayılan), `disabled`, `make_non_negative`,
`make_zero`. concat demuxer'a girecek parçalarda `make_non_negative`, tek başına teslim
edilecek parçada `make_zero` yaygın seçim. Kaynak:
[lossless-cut#1874](https://github.com/mifi/lossless-cut/discussions/1874).

## 1.3 Smart Cut — Nasıl Çalışır, Kim Gerçekten Yapıyor

Fikir: kesme noktası ile bir sonraki anahtar kare arasını **yeniden kodla**, ortadaki
anahtar kare hizalı bloğu **kopyala**, sonda aynısını yap, üçünü birleştir.

Bu raporda kurulup ölçülen düzenek:

```bash
S=104; E=126; KA=108.333333; KB=125.000000   # KA: S'den sonraki ilk anahtar kare
                                              # KB: E'den onceki son anahtar kare
ffmpeg -y -ss $S  -to $KA -i in.mp4 -c:v libx264 -preset medium -pix_fmt yuv420p -c:a aac head.mp4
ffmpeg -y -ss $KA -to $KB -i in.mp4 -c copy -avoid_negative_ts make_zero mid.mp4
ffmpeg -y -ss $KB -to $E  -i in.mp4 -c:v libx264 -preset medium -pix_fmt yuv420p -c:a aac tail.mp4

printf "file 'head.mp4'\nfile 'mid.mp4'\nfile 'tail.mp4'\n" > parts.txt
ffmpeg -y -f concat -safe 0 -i parts.txt -c copy out.mp4
```

Dikiş bütünlüğü ölçüldü: çıktı **660 kare** (tam kodlamayla birebir aynı), ardışık kareler
arasında boşluk/atlama **yok**, `ffmpeg -i out.mp4 -f null -` **hatasız**. Yani bu düzenek
bizim malzemede çalışıyor.

Kazanç, yeniden kodlanan payla doğru orantılı. Burada 22 sn'nin 5,33 sn'si (%24) kodlandı
ve 2550 → 1111 ms (2,3x) kazanıldı. **Uzun kesitlerde oran çok daha iyi olur** — 10 dakikalık
bir kesitte kodlanan pay yine ~8 sn kalır, kopyalanan kısım büyür. Kısa kesitte smart cut'ın
anlamı azalır; 30 saniyenin altında doğrudan tam kodlama daha basit ve öngörülebilir.

### Anahtar kare listesini çıkarmak (ölçüldü)

| Yöntem | Süre (5 dk 1080p) | Not |
| --- | --- | --- |
| `-show_packets` + `flags` süzme | **201 ms** | Kod çözme yok, en ucuz |
| `-skip_frame nokey -show_entries frame=` | **308 ms** | Çözücüyü devreye sokar |

İkisi de 36 anahtar kare buldu, aynı zamanlar. Paket yolu tercih edilmeli:

```bash
ffprobe -v error -select_streams v:0 -show_packets \
  -show_entries packet=pts_time,flags -of csv=p=0 in.mp4 | grep ',K'
```

Çıktı `8.333333,K__` biçiminde; `flags` alanının ilk karakteri `K` ise anahtar kare.
Büyük dosyada tüm dosyayı taramak yerine `-read_intervals` ile hedefin etrafında pencere
açılır (LosslessCut'ın `readFramesAroundTime`'ı bunu yapıyor).

### Smart cut yapan projeler

| Proje | Dil / Lisans | Durum | Smart cut | Kaynak |
| --- | --- | --- | --- | --- |
| [LosslessCut](https://github.com/mifi/lossless-cut) | TS/Electron, GPL | Aktif | **Evet**, v3.44+, hâlâ "deneysel" | `src/renderer/src/smartcut.ts` |
| [smartcut](https://github.com/skeskinen/smartcut) | Python/PyAV, MIT | **Bakım dışı** (2026) | **Evet**, NAL çözümleme | `README`, ticari ürüne taşındı |
| [avcut](https://github.com/anyc/avcut) | C, GPL-2.0 | **Terk edilmiş** | **Evet**, GOP tamponlama | Bakımcı lossless-cut öneriyor |
| [SmartCut (DeepRegular)](https://github.com/DeepRegular/SmartCut) | Python+Rust, GPL-3.0 | Aktif | **Evet**, MPEG-2 TS odaklı | Japon yayın kaydı reklam kesimi |
| [VidCutter](https://github.com/ozmartian/vidcutter) | Python/PyQt | Yavaş | **Evet**, tercihlerden açılır | `CHANGELOG` |
| [ffmpeg-smart-trim](https://github.com/valkjsaaa/ffmpeg-smart-trim) | Python, GPL-3.0 | Ölü | Evet, kısa referans | 13 commit |
| Avidemux | C++, GPL | Aktif | **Hayır** — kesimi anahtar kareye kaydırır | [admWiki](https://www.avidemux.org/admWiki/doku.php?id=using:cutting) |
| mkvmerge | C++, GPL | Aktif | **Hayır** — kodlayıcı yok | [Splitting-imprecise](https://codeberg.org/mbunkus/mkvtoolnix/wiki/Splitting-imprecise) |
| Shotcut | C++, GPL | Aktif | **Hayır** (geliştirici: "No") | [forum](https://forum.shotcut.org/t/is-smart-rendering-a-feature-of-shortcut/18023) |
| SolveigMM Video Splitter | Ticari | Aktif | Evet, AV1 dahil | Üretici iddiası |
| TMPGEnc Smart Renderer 6 | Ticari, 69,95 USD | Aktif | Evet | Üretici iddiası |
| VideoReDo | Ticari | **Kapandı** (2024) | — | Geliştirici vefat etti |

**Değerlendirme.** Smart cut'ın zor kısmı kesme ya da birleştirme değil, kenar parçayı
kaynağın kodek/profil/seviye/renk uzayı/SAR/bit hızı ile **uyumlu** kodlamaktır. LosslessCut'ın
yıllardır "deneysel" demesi, avcut'ın terk edilmesi ve skeskinen'in projeyi ticari ürüne
taşıması aynı sebebi gösteriyor. LosslessCut kaynak bit hızını algılayıp **1,2x** çarpanla
kodluyor; tek video akışı şart, esas olarak H.264'te güvenilir, H.265'te kısmi.

Çağrılabilir bir kütüphane **yok**. .NET tarafında en okunabilir referans smartcut
(Python/PyAV, MIT — mantık taşınabilir); avcut GPL-2.0 ve libav* doğrudan kullanıyor,
lisansı yayılıcı.

---

# 2. Birleştirme

## 2.1 Demuxer mi, Filtre mi

| Özellik | concat **demuxer** | concat **filtresi** |
| --- | --- | --- |
| Yeniden kodlama | Gerekmez (`-c copy`) | **Her zaman gerekir** |
| Parametre uyuşmazlığı | **Sessizce geçer** | Hata verir |
| Girdi biçimi | Metin dosyası | `filter_complex` |
| Hız | Çok hızlı | Kodlama hızında |
| Farklı çözünürlük | Bozuk çıktı | `scale` ile çözülür |

Belge, demuxer (`doc/demuxers.texi`): *"All files must have the same streams (same codecs,
same time base, etc.)."*

Belge, filtre (`doc/filters.texi`): *"All corresponding streams must have the same parameters
in all segments; the filtering system will automatically select a common pixel format for
video streams, and a common sample format, sample rate and channel layout for audio streams,
but other settings, such as resolution, must be converted explicitly by the user."*

Ayrıca filtre için: *"For this filter to work correctly, all segments must start at timestamp 0."*
ve *"Different frame rates are acceptable but will result in variable frame rate at output."*

## 2.2 Uyuşmazlıkta Ne Oluyor — Ölçüldü

720p ve 1080p iki parça birleştirildi:

| Yol | Sonuç | Ölçüldü |
| --- | --- | --- |
| Demuxer + `-c copy` | **Sessizce geçti.** 6 sn çıktı, kapsayıcı `1280x720` etiketli | Evet |
| Filtre, `scale` yok | **Hata verdi**, çıktı üretilmedi | Evet |
| Filtre, `scale` ile | Doğru: 6 sn, 1920x1080 | Evet |

Filtrenin hata metni açık:

```
[Parsed_concat_0] Input link in0:v0 parameters (size 1920x1080, SAR 1:1) do not match
the corresponding output link in0:v0 parameters (1280x720, SAR 1:1)
[Parsed_concat_0] Failed to configure output pad on Parsed_concat_0
```

**Bu, raporun en tehlikeli bulgusudur.** Demuxer hata vermez, uyarı da vermez; kapsayıcıya
ilk parçanın çözünürlüğünü yazar ve ikinci parça o etiketin altında oynatılır. Oynatıcıya
göre bozuk görüntü, yanlış ölçek ya da çökme çıkar. VidShrink parçaları birleştirmeden önce
**kendisi doğrulamalı**: çözünürlük, piksel biçimi, kare hızı, SAR, ses örnekleme hızı ve
kanal düzeni. Uyuşmuyorsa ya filtre yoluna geç ya da kullanıcıyı uyar.

Doğru filtre kullanımı:

```bash
ffmpeg -i p1.mp4 -i p2.mp4 -filter_complex \
 "[0:v]scale=1920:1080,setsar=1[a];[1:v]scale=1920:1080,setsar=1[b];\
  [a][b]concat=n=2:v=1:a=0[v]" -map "[v]" -c:v libx264 out.mp4
```

`setsar=1` unutulmamalı — SAR uyuşmazlığı da aynı hatayı verir.

## 2.3 Ses/Video Kayması — Ölçüldü

Belge uyarıyor: *"Related streams do not always have exactly the same duration... related
synchronized streams (e.g. a video and its audio track) should be concatenated at once."*

Kayma parça sayısıyla birikiyor mu diye ölçüldü (düz kesitler, hız/geri yok):

| Parça sayısı | Beklenen | Video | Ses | Kayma |
| --- | --- | --- | --- | --- |
| 2 | 6,000 | 6,000000 | 6,000000 | **0** |
| 5 | 15,000 | 15,000000 | 15,000000 | **0** |
| 10 | 30,000 | 30,000000 | 30,000000 | **0** |

Düz kesitlerde kayma **birikmiyor**. Ancak hız ve geri filtreleri karışınca kayma çıkıyor
(bölüm 3.3). `concat` filtresi video ve sesi birlikte aldığı sürece (`v=1:a=1`) hizayı kendisi
koruyor.

---

# 3. Hız

## 3.1 `atempo` Sınırı — Yaygın Bilgi Yanlış

İnternette ve birçok projede `atempo`'nun sınırı **0.5–2.0** diye geçer ve bu yüzden
zincirleme önerilir. ffmpeg 9.0 belgesi (`doc/filters.texi`) başka söylüyor:

> *"The filter accepts exactly one parameter, the audio tempo. If not specified then the
> filter will assume nominal 1.0 tempo. Tempo must be in the **[0.5, 100.0]** range.*
>
> *Note that tempo greater than 2 will skip some samples rather than blend them in. If for
> any reason this is a concern it is always possible to daisy-chain several instances of
> atempo to achieve the desired product tempo."*

Bu makinede sınandı:

| Değer | Sonuç | Ölçüldü |
| --- | --- | --- |
| `atempo=0.4` | **Reddedildi** — `Error applying option 'tempo'... Result too large` | Evet |
| `atempo=2.0` | Kabul | Evet |
| `atempo=4.0` | **Kabul** (tek kademe yeter) | Evet |
| `atempo=2,atempo=2` | Kabul, aynı süre | Evet |
| `atempo=150` | **Reddedildi** | Evet |

**Hüküm.** Zincirleme *çalışmak için* gerekmiyor; **kalite için** gerekiyor. 2'nin üstünde
tek kademe örnek atlar (`skip`), zincir harmanlar (`blend`). VidShrink 2'ye kadar tek kademe,
üstünde `n`'inci kök ile zincir kursun:

```bash
# 4x: tek kademe calisir ama ornek atlar
-filter_complex "[0:v]setpts=0.25*PTS[v];[0:a]atempo=2,atempo=2[a]"

# 3x, belgenin kendi ornegi
atempo=sqrt(3),atempo=sqrt(3)
```

0,5'in altı için de zincir şart: `atempo=0.5,atempo=0.5` = 0,25x.

## 3.2 `setpts` ile Video Hızı

Belge: `setpts=0.5*PTS` hızlandırır, `setpts=2.0*PTS` yavaşlatır. Değişkenler `PTS`, `N`,
`STARTPTS`, `TB`, `FRAME_RATE`, `T`. Video hızı ile ses tempo'su **birbirinin tersidir** —
bu, hata yapmanın en kolay yeri:

| İstenen hız | Video | Ses |
| --- | --- | --- |
| 2x hızlı | `setpts=0.5*PTS` | `atempo=2.0` |
| 4x hızlı | `setpts=0.25*PTS` | `atempo=2,atempo=2` |
| 0,5x yavaş | `setpts=2.0*PTS` | `atempo=0.5` |

Yani video için çarpan `1/hız`, ses için `hız`.

Ölçüldü: 22,000 sn girdi, 2x → **11,067 sn**; 4x → **5,567 sn**. Beklenen 11,0 ve 5,5;
fark kare yuvarlamasından.

## 3.3 Kare Hızı ve Kare Düşürme

`setpts` **kare atmaz**, yalnız zaman damgasını değiştirir. 2x hızlandırılan 30 fps video
60 fps'lik bir akışa dönüşür; kapsayıcı bunu kabul eder ama dosya gereksiz büyür ve bazı
oynatıcılar zorlanır. İki seçenek:

```bash
# A) Kare hizini koru, kare dusur (dosya kucuk, hareket daha kesik)
[0:v]setpts=0.5*PTS,fps=30[v]

# B) Tum kareleri tut (akici ama 60 fps cikti)
[0:v]setpts=0.5*PTS[v]
```

`fps` filtresi belgede: *"changes the frame rate by interpolating or dropping frames as
necessary."* Yavaşlatmada tersi olur — 0,5x video 15 fps'e düşer, `fps=30` ile kareler
**çoğaltılır** (tekrarlanır, yeni kare üretilmez). Gerçek ara kare için `minterpolate`
gerekir ki çok pahalıdır ve bu turda ölçülmedi.

**Öneri:** VidShrink hedef kare hızını çizelgenin tamamı için sabitlesin ve her hızlı
parçadan sonra `fps=<hedef>` koysun. Aksi halde `concat` değişken kare hızlı çıktı üretir
(belge bunu açıkça söylüyor).

## 3.4 Sesin Hız Değişiminde Durumu

`atempo` **perdeyi korur** (belge: *"Adjust audio tempo"*, `asetrate`'ten farkı budur).
Perdeyi kasten kaydırmak istenirse `asetrate=44100*2,aresample=44100` kullanılır — sincap
sesi etkisi. VidShrink'in varsayılanı `atempo` olmalı; perde kaydırma ayrı bir seçenek.

Çok yüksek hızda (8x üstü) ses genelde anlamsızlaşır; birçok araç eşik üstünde sesi
tamamen düşürür. Bu bir ürün kararıdır, teknik zorunluluk değil.

---

# 4. Geri Oynatma

## 4.1 `reverse` Filtresinin Bedeli — Ölçüldü

Belge tek cümleyle uyarıyor (`doc/filters.texi`):

> *"Reverse a video clip. **Warning: This filter requires memory to buffer the entire clip,
> so trimming is suggested.**"*

`areverse` için birebir aynı uyarı. "Entire clip" ne kadar tuttuğu ölçüldü:

| Girdi süresi | Tepe bellek | Süre | Ölçüldü |
| --- | --- | --- | --- |
| 5 sn | **727 MB** | 349 ms | Evet |
| 10 sn | **1 193 MB** | 504 ms | Evet |
| 20 sn | **2 101 MB** | 1 143 ms | Evet |
| 40 sn | **3 967 MB** | 1 957 ms | Evet |

Doğrusal: **~99 MB / saniye** (1080p30). Kuram da bunu söylüyor:
1920 × 1080 × 1,5 bayt (yuv420p) = 3,11 MB/kare × 30 = **93 MB/sn**. Ölçüm ve hesap uyuşuyor,
yani bu sayı malzemeden bağımsız ve güvenle genellenebilir.

Sonuçları acı: **1 dakika ≈ 5,9 GB, 5 dakika ≈ 29,7 GB.** 4K'da dört katı.
Tüm videoyu `reverse` ile ters çevirmek **mümkün değil**.

## 4.2 Parça Parça Geri Alma — Ölçüldü

Çözüm: kesitlere böl, her kesiti ayrı ters çevir, **ters sırada** birleştir.

```bash
# 60 sn'yi 6 x 10 sn parcaya bol
for i in 0 1 2 3 4 5; do
  ffmpeg -y -ss $((i*10)) -t 10 -i in.mp4 -vf reverse -af areverse \
    -c:v libx264 -preset ultrafast rv_$i.mp4
done
# TERS sirada birlestir
printf "file 'rv_5.mp4'\nfile 'rv_4.mp4'\nfile 'rv_3.mp4'\nfile 'rv_2.mp4'\nfile 'rv_1.mp4'\nfile 'rv_0.mp4'\n" > rv.txt
ffmpeg -y -f concat -safe 0 -i rv.txt -c copy reversed.mp4
```

Ölçüldü: 60 sn, **6 226 ms**, parça başına tepe bellek **1 325 MB**, çıktı 60,007 sn.
Tek parça yapılsaydı ~5,9 GB gerekecekti. Kesit boyu doğrudan bellek tavanını belirler;
10 sn iyi bir varsayılan (≈1,3 GB), düşük bellekli makinede 5 sn.

## 4.3 mpv Gerçek Zamanlı Geri Oynatabiliyor mu

**Kısmen — ve mpv'nin kendisi güvenmemeyi öneriyor.**

Önce "-100 hız" sorusu. mpv `DOCS/man/options.rst`:

```
``--speed=<0.01-100>``
    Slow down or speed up playback by the factor given as parameter.
```

Aralık **0,01–100** ve **negatif değer yok**. `speed=-100` seçenek doğrulamasından geçmez.
Geri oynatma `speed` ile değil, ayrı bir mekanizmayla yapılır:

```
``--play-direction=<forward|+|backward|->``
    Control the playback direction (default: forward). Setting ``backward``
    will attempt to play the file in reverse direction...
```

mpv'nin kendi uyarısı, aynen:

> *"Backward playback is extremely fragile. It may not always work, is much slower than
> forward playback, and breaks certain other features. How well it works depends mainly on
> the file being played. Generally, it will show good results (or results at all) only if
> the stars align."*

Bilinen sorunlardan bizi ilgilendirenler: bazı kapsayıcı/kodek birleşimleri desteklenmiyor
ve **oynatıcı bunu algılamıyor**; altyazıda geri demux yok; donanım çözmeyle
*"will probably exhaust all your GPU memory and then crash a thing or two"*; `frame-step`
komutları yer değiştiriyor.

Ayarlar: `--video-reversal-buffer`, `--audio-reversal-buffer`, `--demuxer-max-bytes`
(`--cache=yes` şart), `--demuxer-backward-playback-step` (varsayılan 60; çok yüksek değer
*"quadratic runtime behavior"* getiriyor).

**mpv'nin kendi tavsiyesi bizim senaryomuz için belirleyici:**

> *"If you just want to quickly go backward through the video and just show 'keyframes',
> just use forward playback, and hold down the left cursor key (which on CLI with default
> config sends many small relative seek commands)."*

## 4.4 -100 Hız: Taklit Şart, ve Ucuz — Ölçüldü

30 fps kaynakta -100x demek, ekranın saniyede **3000 kaynak karesi** göstermesi demek.
Hiçbir ekran bunu yapamaz; -100x zaten **kare atlamalı bir taklittir**, her 100 kareden
biri gösterilir. Bunu bilerek yapmak, `reverse` ile zorlamaktan hem doğru hem ucuzdur.

Dışa aktarma tarafında ölçüldü (5 dakikalık dosyanın tamamı):

| Yöntem | Süre | Kare | Bellek | Ölçüldü |
| --- | --- | --- | --- | --- |
| `select='not(mod(n,100))',reverse` | 2 260 ms | 90 | düşük | Evet |
| `-skip_frame nokey` + `reverse` | **389 ms** | 36 | önemsiz | Evet |
| Tam `reverse` | — | 9000 | **~29,7 GB** | Denenmedi |

```bash
# -100x geri, yalniz anahtar karelerden: 5 dk dosya 389 ms
ffmpeg -skip_frame nokey -i in.mp4 -vf "reverse,setpts=N/30/TB" \
  -an -r 30 -c:v libx264 -preset ultrafast out.mp4
```

`-skip_frame nokey` çözücüye anahtar kare dışını hiç açtırmadığı için hem bellek hem süre
önemsizleşiyor. Uç hızlarda zaten yalnız anahtar kareler görülecek; kalite kaybı yok.

Oynatıcı tarafında (libmpv) karşılığı: periyodik `seek <-adım> relative+keyframes`.
thumbfast'in kazıma deseni de budur — hareket sürerken `keyframes`, durunca `exact`.

**Hüküm.** VidShrink'te "geri oynatma" iki ayrı özelliktir ve karıştırılmamalıdır:
- **Geri sarma (gezinme)**: periyodik `seek ... relative+keyframes`. Ucuz, her dosyada çalışır.
- **Geri parça (dışa aktarma)**: kesit kesit `reverse`, ters sırada `concat`. Kesit boyu sınırlı.

`--play-direction=backward` üçüncü bir şeydir ve mpv'nin kendi değerlendirmesi ("extremely
fragile", hwdec ile çökme) göz önüne alınınca **VidShrink'in varsayılanı olmamalı**.

---

# 5. Önizleme: Yeniden Kodlamadan Oynatma

## 5.1 mpv EDL Protokolü

Kaynak: [`DOCS/edl-mpv.rst`](https://github.com/mpv-player/mpv/blob/master/DOCS/edl-mpv.rst).
Amaç birebir bizim ihtiyacımız: *"EDL files basically concatenate ranges of video/audio from
multiple source files into a single continuous virtual file."* Ve playlist'ten farkı:
*"the virtual EDL file appears as a virtual timeline (like a single file), instead as a playlist."*

```
# mpv EDL v0
!no_chapters
kaynak.mp4,0,12.5
kaynak.mp4,40,7.25,title=Ikinci Parca
%22%ad,virgullu,dosya.mkv,3,length=9
```

İlk satır **zorunlu** (`# mpv EDL v0`), UNIX satır sonu şart, fazladan boşluk yasak.
Parametreler `file`, `start` (sn, varsayılan 0), `length` (sn, varsayılan kalan).
Değer `,;\n!` içeriyorsa uzunluk kaçışı: `%18%dosya,adi.mkv`.

URI biçiminde satır sonu yerine `;`:

```
edl://kaynak.mp4,0,12.5;kaynak.mp4,40,7.25
```

`!new_stream` ayrı dosyalardan paralel iz bağlar (video bir dosyadan, ses başkasından) ve
*"this will use a unified cache for all streams"*.

**Sınırları.** EDL saf bir sanal zaman çizelgesidir — **yeniden kodlama yapmaz, çıktı
üretmez**, yalnız oynatır. Teslim yine ffmpeg'e düşer. Karışık kodek kabul ediliyor ama
*"mpv will apply an arbitrary heuristic"*; eksik iz olursa *"there will be a 'hole', and
bad behavior may result"*. Biçim dondurulmuş değil: *"which is not frozen yet and may change
any time."* Ayrıca güvenlik kapısı var — göreli/mutlak yollar engellenebilir,
`--load-unsafe-playlists` gerekebilir.

**libmpv'den kullanılır.** `loadfile` bir playlist işlemidir, `loadfile "edl://..."` geçerlidir.
VidShrink'te `MpvEngine.cs:227` zaten `Command(OpenTag, "loadfile", Target(path))` çağırıyor;
`Target`'ın `edl://` için kaçışı doğru yapması yeterli. Uzun çizelgede URI yerine `.edl`
dosyası yazmak daha güvenli (`,;` kaçışıyla uğraşılmaz).

**Doğrulanmadı:** bu makinede mpv CLI yok, EDL koşturulmadı.

## 5.2 `--lavfi-complex`

`DOCS/man/options.rst`:

> *"Set a 'complex' libavfilter filter, which means a single filter graph can take input
> from multiple source audio and video tracks... A label of the form `aidN` selects audio
> track N as input. A label of the form `vidN` selects video track N as input. A label named
> `ao` will be connected to the audio output. A label named `vo` will be connected to the
> video output. Each label can be used only once."*

Ve çalışma zamanı:

> *"It's not possible to change the tracks connected to the filter at runtime, unless you
> explicitly change the `lavfi-complex` property and set new track assignments."*

Yani **özellik olarak yazılabilir** — `mpv_set_property_string("lavfi-complex", ...)`.
VidShrink'te `MpvEngine.SetProperty(string,string)` (satır 211) zaten var, ek altyapı gerekmiyor.

```
--lavfi-complex='[vid1] [vid2] hstack [vo]'
--lavfi-complex='[aid1] [aid2] amix [ao]'
```

İkinci bir dosyayı grafiğe sokmak için `--external-file=other.mkv` gerekir.
`hstack`/`vstack` boyut eşitliği ister, önüne `scale` konmalı.

**Karşılaştırma paneli için değerli**, ama **çizelge önizlemesi için değil**: tek `[vo]`,
tek `[ao]`, her etiket bir kez. Kesitleri `trim` ile dizip `concat` etmek burada işlemez,
çünkü lavfi-complex tek dosyanın izleri üzerinde çalışır. Çizelge önizlemesinin doğru
aracı **EDL**'dir.

## 5.3 Karar

| İhtiyaç | Araç | Neden |
| --- | --- | --- |
| Kesilmiş/sıralanmış çizelge önizlemesi | **EDL** | Kesintisiz sanal zaman çizelgesi, tek `loadfile` |
| A/B karşılaştırma paneli | **`lavfi-complex`** | İki kaynağı tek çıkışta birleştirir |
| Hız önizlemesi | `speed` özelliği | 0,01–100, EDL ile birlikte çalışır |
| Geri önizleme | periyodik `seek` | `play-direction` kırılgan |
| Teslim | ffmpeg | mpv çıktı üretmez |

**Önemli sınır:** EDL parça sıralar ve keser, ama **hız ve geri uygulayamaz**. Hızlı bir
parçanın önizlemesi EDL ile yapılamaz; o parçaya gelince `speed` özelliğini değiştirmek
gerekir (çizelge konumunu izleyip elle sürmek). Geri parçanın gerçek önizlemesi ise ancak
o kesit önceden `reverse` ile üretilip EDL'e **geçici dosya** olarak konursa mümkündür.
Bu, `.calisma/` altında önbelleklenecek bir iştir.

---

# 6. Küçük Resim Şeridi ve Dalga Biçimi

## 6.1 Küçük Resim — Ölçüldü

5 dakikalık 1080p dosyadan şerit üretmenin üç yolu:

| Yöntem | Süre | Kare | Çıktı | Ölçüldü |
| --- | --- | --- | --- | --- |
| `-skip_frame nokey` tek geçiş | **369 ms** | 36 | 148 KB | Evet |
| `fps=1/10` tek geçiş | 2 122 ms | 30 | — | Evet |
| 30 ayrı `-ss` süreci | 2 618 ms | 30 | — | Evet |

`-skip_frame nokey` **5,7x** daha hızlı, çünkü P/B karelerini hiç çözmüyor:

```bash
ffmpeg -skip_frame nokey -i in.mp4 -fps_mode passthrough \
  -vf scale=160:-1 -q:v 5 th_%03d.jpg
```

(`-vsync 0` değil — ffmpeg 9'da kaldırıldı.)

**Ama bir bedeli var:** anahtar kareler **düzenli aralıklı değildir**. Encoder sahne
değişiminde sıklaştırır, durgun sahnede seyreltir. Bizim sentetik malzemede 8,3333 sn'de bir
düzenli çıktı, gerçek videoda çıkmaz. Şerit çizilirken "n'inci kutu = n × aralık"
varsayılamaz; **her karenin gerçek zamanı yanında saklanmalı** ve şerit zaman eşlemeli çizilmeli.

LosslessCut'ın kararı (`src/renderer/src/ffmpeg.ts`, `renderThumbnail`): `-ss` girdiden önce,
`scale=-2:200`, JPEG `-q:v 10`, stdout'a. `renderThumbnails` **tüm video için değil, görünen
zoom penceresi için 10 adet** üretiyor, `concurrency: 2`, hook'ta **300 ms debounce**.
Bu, video uzunluğundan bağımsız sabit maliyet demek — ölçeklenen tek yaklaşım.

libmpv'den `screenshot-raw` mümkün (`MPV_FORMAT_NODE_MAP`, `w`/`h`/`stride`/`format`/`data`;
stride negatif olabilir) ama **ana oynatıcı örneğini bozar** (seek gerekir) ve 1080p'de kare
başına ~8,3 MB ham kopya demektir. thumbfast'in çözümü ayrı, gizli, duraklatılmış bir ikinci
mpv örneği + `--ovc=rawvideo --of=image2 --ofopts=update=1`; VidShrink libmpv'yi zaten
gömdüğü için ikinci bir `mpv_create()` ile IPC'siz kurulabilir.

## 6.2 Dalga Biçimi — Ölçüldü

| Yöntem | Süre (5 dk) | Çıktı | Ölçüldü |
| --- | --- | --- | --- |
| `showwavespic` → PNG | 263 ms | 1 336 bayt* | Evet |
| `-f s16le` 8 kHz mono | 275 ms | 4,58 MB | Evet |
| `-f s16le` 1 kHz mono | 309 ms | 600 KB | Evet |
| `-vn` önce + 8 kHz | **251 ms** | 4,58 MB | Evet |

\* Saf sinüs olduğu için anlamsız derecede küçük; gerçek müzikte çok büyük olur. Genellemeyin.

Hepsi ucuz — 5 dakikalık dosyada çeyrek saniye. `-vn`'i **girdiden önce** koymak video
akışını hiç açmıyor ve en hızlısı.

```bash
# Ham PCM cikar, zarfi C# tarafinda hesapla
ffmpeg -vn -i in.mp4 -ac 1 -ar 8000 -f s16le -c:a pcm_s16le out.raw
```

**`showwavespic` PNG'si tavsiye edilmez**, çünkü çıktı bir *resimdir*: zoom değişince ffmpeg
yeniden koşmak gerekir. Düzenleme çizelgesi sürekli zoom'lanır.

Doğru model: bir kez ham PCM oku, C# tarafında **pencere başına min/max** hesapla, `short[]`
sakla, çizimde yeniden özetle. İki tuzak:

1. **`-ar` ile downsample ederek zarf çıkarma.** `aresample` ortalama/filtre uygular, tepe
   değil — zarfı düzleştirir, kısa tepeler kaybolur. Tam örnekleme hızında oku, decimation'ı
   min/max ile kendin yap.
2. **Zoom'da alt-örnekleme.** Piksel başına düşen N noktadan birini seçmek aliasing üretir;
   min/max'ı **yeniden özetlemek** gerekir. Kdenlive 25.04'te düzelttiği hata tam buydu
   ([Étienne Paul André](https://etiand.re/posts/2025/01/audio-waveforms-in-kdenlive-technical-upgrades-for-speed-precision-and-better-ux/)).

Veri hacmi önemsiz: 200 nokta/sn, 1 saatlik video, 16-bit stereo ≈ **2,9 MB**.

[bbc/audiowaveform](https://github.com/bbc/audiowaveform) **dağıtılmamalı**: MP4/MKV okumuyor
(MP3, WAV, FLAC, Ogg, Opus), yani önce ffmpeg ile ses çıkarmak gerekir — ffmpeg zaten elimizde
ve min/max döngüsü C#'ta otuz satır. İkinci bir yerel ikili taşımanın karşılığı yok.

## 6.3 Bilinen Kilitlenme

Ham PCM'i `pipe:1` ile okurken **stderr mutlaka boşaltılmalı**. Projenin hafızasındaki
`bosaltilmayan-stderr-sureci-kilitliyor` kaydı tam bu boru hattında ısırıyor: 4096 baytlık
boru dolunca ffmpeg bloke oluyor.

---

# 7. Tam Çizelge Boru Hattı — Ölçüldü

Kullanıcının istediği beş yeteneğin hepsi **tek ffmpeg geçişinde** kuruldu ve doğrulandı:
kesme, parça kaldırma (10-20 ve 40-50 alındı, arası atıldı), hız (2x), geri (`reverse`),
birleştirme (`concat`).

```bash
ffmpeg -i in.mp4 -filter_complex "
[0:v]trim=10:20,setpts=PTS-STARTPTS[v0];
[0:a]atrim=10:20,asetpts=PTS-STARTPTS[a0];
[0:v]trim=40:50,setpts=(PTS-STARTPTS)/2[v1];
[0:a]atrim=40:50,asetpts=PTS-STARTPTS,atempo=2.0[a1];
[0:v]trim=70:80,setpts=PTS-STARTPTS,reverse[v2];
[0:a]atrim=70:80,asetpts=PTS-STARTPTS,areverse[a2];
[v0][a0][v1][a1][v2][a2]concat=n=3:v=1:a=1[v][a]" \
 -map "[v]" -map "[a]" -c:v libx264 -preset ultrafast -c:a aac out.mp4
```

Sonuç: **1 357 ms**, çıktı 25,033 sn (beklenen 10 + 5 + 10 = 25,000). Video 25,033 / ses 25,007
— **26 ms ses/video kayması**. Düz kesitlerde kayma sıfırdı (bölüm 2.3); bu kayma `atempo` ve
`areverse`'ün kare/örnek yuvarlamasından geliyor.

Üç kural bu örnekten çıkıyor:

1. Her `trim`'den sonra `setpts=PTS-STARTPTS` **şart** — `trim` zaman damgasını değiştirmez
   (belge: *"this filter does not modify the timestamps"*) ve `concat` filtresi her segmentin
   0'dan başlamasını ister.
2. Hız için `setpts` bölmesi `atrim`'den **sonra**, `atempo` ise `asetpts`'ten sonra gelmeli.
3. Video ve ses **aynı `concat` çağrısında** birleştirilmeli (`v=1:a=1`), ayrı ayrı değil.

Bu boru hattı `reverse` içerdiği için bölüm 4.1'deki bellek sınırına tabidir: geri çevrilen
kesit ne kadar uzunsa o kadar RAM. Çizelgede uzun bir geri parça varsa kesit kesit üretilip
`concat` demuxer ile dikilmeli.

---

# 8. Çizelge → ffmpeg Derleyicileri ve NLE'ler

## 8.1 Otomatik Düzenleme ve Analiz

| Proje | Dil / Lisans | Durum | Çizelgeyi nasıl kuruyor |
| --- | --- | --- | --- |
| [auto-editor](https://github.com/WyattBlue/auto-editor) | Nim, Unlicense | Çok canlı, 5,2k | **v3 JSON çizelgesi**, etiket→segment→render |
| [moviepy](https://github.com/Zulko/moviepy) | Python, MIT | Bakımcı arıyor | Klip = `frame(t)`, ffmpeg'e ham rgb24 borusu |
| [PySceneDetect](https://github.com/Breakthrough/PySceneDetect) | Python, BSD-3 | v0.7.1 (Tem 2026) | Sahne algılama, üç bölme kipi |
| [ffmpeg-normalize](https://github.com/slhck/ffmpeg-normalize) | Python, MIT | v1.42 (Eyl 2026) | İki geçişli EBU R128 `loudnorm` |

**auto-editor'ın v3 şeması bu taramanın en değerli bulgusudur** — küçük, okunur, sürümlenmiş:

```
version "3" · timebase "30000/1001" · resolution [w,h] · samplerate
v: [ [clip, clip...], ... ]     // video katmanlari, dizi sirasi = z duzeni
a: [ [clip, ...], ... ]
clip: { src, start, dur, offset, stream, effects[] }
```

`start`/`dur` **çizelge** üzerinde, `offset` **kaynak** içinde — mutlak zaman kutusu modeli.
`timebase` rasyonel (`30000/1001`), kayan noktada tutulmuyor.

`ffmpeg-normalize`'in `--threshold` refleksi VidShrink'in kendi ilkesiyle aynı: zaten hedefteyse
hiç işleme sokma.

## 8.2 JSON / Deklaratif Derleyiciler

| Proje | Lisans | Durum | ffmpeg'i nasıl kullanıyor |
| --- | --- | --- | --- |
| [Editly](https://github.com/mifi/editly) | MIT | **Yarı durgun** (May 2025) | filter_complex **üretmiyor** — RGBA borusu, ffmpeg yalnız kodlayıcı |
| [ffmpeg-concat](https://github.com/transitive-bullshit/ffmpeg-concat) | MIT (npm) | **Terk** (2022) | Geçiş karelerini diske açıp GL shader uyguluyor |
| [fluent-ffmpeg](https://github.com/fluent-ffmpeg/node-fluent-ffmpeg) | MIT | **Arşivlendi** (May 2025) | Argüman kurucu, çizelge modeli yok |
| [ffmpeg.wasm](https://github.com/ffmpegwasm/ffmpeg.wasm) | MIT sarmalayıcı | "Experimental" | CLI argümanları, **~2 GB WASM tavanı** |
| [Remotion](https://github.com/remotion-dev/remotion) | **Ticari lisans** | Çok canlı, 59k | React ağacı → headless Chrome → ffmpeg |
| [Shotstack](https://shotstack.io/docs/) | Kapalı SaaS | Canlı | Şema açık, motor kapalı |
| [Revideo](https://github.com/redotvideo/revideo) | MIT | Tempo düşmüş | Canvas, **paralel worker render** |

`fluent-ffmpeg`'in arşivlenme notu doğrudan bizi ilgilendiriyor: *"This library is no longer
maintained and no longer works properly with recent ffmpeg versions."* **Negatif ders:** ffmpeg
argüman üretimini üçüncü parti kütüphaneye emanet etmenin sonu budur. VidShrink'in bunu
`VidShrink.Core`'da kendi tutması doğru karardır ve öyle kalmalıdır.

**Shotstack'in şeması bize en yakın olanı:**

```json
{"timeline":{"tracks":[{"clips":[
  {"asset":{"type":"video","src":"a.mp4","trim":2,"volume":0.5},
   "start":4,"length":6}
]}]},"output":{"format":"mp4","resolution":"HD"}}
```

`track` = katman (dizi sırası = z düzeni), `clip` = mutlak zaman kutusu (`start` + `length`),
`trim` = kaynak içi ofset. Editly'nin sıralı `duration` modelinden üstün: bir parçanın
uzunluğu değişince sonrakiler kaymaz.

**Remotion'un ilkesi ise mimari olarak en önemlisi:** önizleme ve render **aynı koddan**
beslenir, bu yüzden "önizlemede farklı görünüyordu" hatası yapısal olarak imkânsızdır.

## 8.3 Çerçeve Tabanlı NLE'ler

Aşağıdaki sürüm ve tarihler depo sürüm sayfalarından okundu (15.09.2026 taraması); yerelde **derlenip denenmedi**.

| Proje | Lisans | Durum | Çekirdek |
| --- | --- | --- | --- |
| [MLT](https://github.com/mltframework/mlt) | LGPL-2.1 / GPL | v7.40.0 (25.06.2026), canlı | producer/playlist/tractor, MLT XML |
| [Kdenlive](https://invent.kde.org/multimedia/kdenlive) | GPL | 26.08.1 | MLT 7.38 önyüzü |
| [Shotcut](https://github.com/mltframework/shotcut) | GPLv3 | v26.9.6 (06.09.2026) | Proje dosyası doğrudan `.mlt` |
| [Olive](https://github.com/olive-editor/olive) | GPL | **Durmuş** — son commit 05.12.2024, son kararlı 0.1.2 (2019) | Kendi node graph'ı |
| [Flowblade](https://github.com/jliljebl/flowblade) | GPL-3.0+ | 2.24 | MLT, Avid tarzı insert |
| [Pitivi / GES](https://github.com/pitivi/gst-editing-services) | LGPL | Canlı | GStreamer Editing Services |
| [Vidiot](https://sourceforge.net/projects/vidiot/) | GPLv3 | Durgun | wxWidgets |
| [HandBrake](https://github.com/HandBrake/HandBrake) | GPL-2.0 | 1.11.2 | **Çizelge yok** — tek title→tek job |

MLT tek başına hem önizleme hem render verebiliyor, **aynı XML'den**:

```bash
melt proje.mlt                                    # sdl2 consumer, onizleme
melt proje.mlt -consumer avformat:out.mp4 vcodec=libx264
```

**MLT ↔ .NET — kapı P/Invoke değil, `melt.exe`.** Depodaki resmî C# SWIG bağlayıcısı
(`src/swig/csharp`) Mono dönemine ait: `.so` üretiyor (Windows `.dll` değil), `mcs` ile derliyor
(`csc`/`dotnet` değil), CI'da bu kol koşmuyor. NuGet paketi yok. Ayrıca `MLT_REPOSITORY` ortam
değişkeni **Windows'ta okunmuyor** — `mlt_factory.c` içinde o dal `#if !defined(_WIN32)` koruması
altında; modül dizini host sürecin exe konumundan türetiliyor, yani `VidShrink.App.exe`'nin yanında
`lib\mlt-7\` aranır. Tek temiz çıkış `mlt_factory_init(dizin)`'e dizini açıkça vermek.

Buna karşılık **MLT XML üretip `melt.exe`'yi alt süreç olarak koşturmak** mevcut
`VidShrink.Ffmpeg` süreç-çağrısı mimarisine birebir oturur; Kdenlive bile render'ı ayrı süreçte
`melt`'e veriyor. P/Invoke ancak kare düzeyinde denetim gerekirse değer kazanır — ki VidShrink'te
böyle bir ihtiyaç yok. GES tarafı daha kötü: `gstreamer-sharp` kaynakta canlı ama yayınlanmış
paketler bayat (`GstSharp` 1.18 / .NET Framework 4.5), .NET 8 için kendin derlemen gerekir.

## 8.4 OpenTimelineIO

[OpenTimelineIO](https://github.com/AcademySoftwareFoundation/OpenTimelineIO) (Apache-2.0,
C++ çekirdek). Model: `Timeline / Stack / Track / Clip / Gap / Transition`, `RationalTime` ve
`TimeRange`. **1.0 çıkmadı.**

- **.NET bağlayıcısı yok.** Resmî bağlayıcılar C, Java, Swift. P/Invoke için teorik taban
  [C bağlayıcısı](https://github.com/OpenTimelineIO/OpenTimelineIO-C-Bindings) ("Public Beta").
  NuGet paketi ve topluluk C# sarmalayıcısı **bulunamadı**.
- **OTIO → ffmpeg doğrudan renderer yok.** OTIO kasıtlı olarak render etmez, yalnız tarif eder.
  Tek pratik yol [otio-mlt-adapter](https://github.com/apetrynet/otio-mlt-adapter) ile MLT'ye
  çevirip `melt` koşmak — ve o adaptör **yalnız yazma yönlü**.

**Alınacak tek ders `RationalTime`'dır:** `30000/1001` gibi bir zaman tabanı kayan noktada
tutulmaz. VidShrink'in çizelge nesnesi de rasyonel tutmalı.

## 8.5 mpv Kesme Script'leri

| Script | Ürettiği komut | Alınacak fikir |
| --- | --- | --- |
| [mpv-cut](https://github.com/familyfriendlymikey/mpv-cut) | `-ss X -t D -i in -c copy -avoid_negative_ts make_zero` | İki eylem: COPY / ENCODE |
| [occivink/encode.lua](https://github.com/occivink/mpv-scripts) | `-ss` + `-to` göreli, `-map`, `-filter:v` | **Oynatıcıdaki `vf` zincirini ffmpeg'e taşıma** |
| [Kagami/mpv_slicing](https://github.com/Kagami/mpv_slicing) | Ham AVI ara ürün | **`scale=in_color_matrix=` ile renk matrisi taşıma** |
| [videoclip](https://github.com/Ajatt-Tools/videoclip) | mp4/webm, altyazı gömer | Profil tabanlı ayar |
| [mpv_sponsorblock](https://github.com/po5/mpv_sponsorblock) | ffmpeg üretmez | **`chapter-list` özelliği yazılabilir** |
| [thumbfast](https://github.com/po5/thumbfast) | İkinci mpv örneği | İki kademeli `keyframes`/`exact` seek |

İki tanesi doğrudan VidShrink'e değer katıyor. `mpv_slicing`'in **renk matrisini mpv'den okuyup
`scale=in_color_matrix=` ile ffmpeg'e vermesi**, A/B karşılaştırmasında renk kaymasını önler —
projenin hafızasındaki "ortak ölçüm parçaları sessizce farklı" kaydının kardeşi bir tuzak.
`mpv_sponsorblock`'un `chapter-list` özelliğini yazması ise kesim noktalarını oynatıcı şeridinde
göstermenin en ucuz yoludur.

## 8.6 Bir Çelişki ve Çözümü

Tarama sırasında LosslessCut'ın kesme komutu `-ss X -i in -to Y -c copy` biçiminde aktarıldı.
Bu raporun 1.2 bölümündeki **ölçüm bu yazımın yanlış sonuç verdiğini gösteriyor**: `-ss`
girdi tarafındayken `-to` çıktı tarafında göreli sayılıyor ve 30 sn yerine 130 sn üretiyor.
Doğru yazım `-t <süre>`'dir. VidShrink bu kalıbı kaynak projelerden **kopyalamamalı**.

---

# 9. Hüküm

## 9.1 Ölçüm Özeti

| Soru | Ölçülen cevap |
| --- | --- |
| Kayıpsız vs yeniden kodlama | 65 ms / +4,030 sn hata **vs** 2550 ms / hatasız |
| Smart cut işe yarıyor mu | **Evet** — 1111 ms, +0,052 sn, 660 kare, dikiş temiz |
| `atempo` sınırı | **[0,5 – 100,0]** (0,4 ve 150 reddedildi), 2,0 üstü örnek atlar |
| `reverse` bellek | **~99 MB/sn** (1080p30) → 1 dk ≈ 5,9 GB |
| Parçalı geri (60 sn) | 6 226 ms, parça başına 1,3 GB |
| -100x geri | **389 ms** kare atlamalı, 5 dk dosyanın tamamı |
| Küçük resim | `-skip_frame nokey` **369 ms** vs `fps=1/10` 2 122 ms |
| Dalga biçimi | **~250 ms** (5 dk), yöntemden bağımsız ucuz |
| concat uyuşmazlığı | Demuxer **sessizce geçiyor**, filtre hata veriyor |
| Tam çizelge | 1 357 ms, 26 ms A/V kayması |

## 9.2 Öneriler

**Kesme — üç kip, açıkça etiketli.** "Hızlı (anahtar kareye oturur)", "kare hassas (yeniden
kodlar)", "melez (uçları kodlar, gövdeyi kopyalar)". Tek "böl" düğmesi yalan söyler;
PySceneDetect ve Avidemux ikisi de bu ayrımı arayüzde yapıyor. Hızlı kipte kullanıcıya
**beklenen sapma** (anahtar kare aralığı) gösterilmeli.

**`-to` yasak, `-t` kullan.** Ölçülmüş tuzak; kaynak projelerden kopyalanmamalı.

**Birleştirmeden önce parametreleri doğrula.** Demuxer sessizce bozuk çıktı üretiyor.
Çözünürlük, piksel biçimi, kare hızı, SAR, örnekleme hızı, kanal düzeni denetlenmeli;
uyuşmuyorsa filtre yoluna geçilmeli (`scale` + `setsar=1`).

**Hızda video `1/hız`, ses `hız`.** 2'ye kadar tek `atempo`, üstünde kök alarak zincir.
Her hızlı parçadan sonra `fps=<hedef>` ile kare hızını sabitle.

**Geri oynatmayı ikiye ayır.** Gezinme = periyodik `seek ... relative+keyframes`.
Dışa aktarma = kesit kesit `reverse` + ters `concat`, kesit 10 sn. `--play-direction=backward`
varsayılan olmasın — mpv'nin kendi değerlendirmesi "extremely fragile" ve hwdec ile çökme riski
belgeli. **"-100 hız" mpv'de yoktur** (`speed` 0,01–100, negatif yok); kare atlamalı taklit şart
ve zaten ucuz.

**Önizleme = EDL.** Kesim/sıralama planı doğrudan `edl://` URI'sine çevrilebilir, libmpv'den
tek `loadfile` ile oynar. `MpvEngine` bunun için hazır (`Command(OpenTag, "loadfile", ...)`,
`SetProperty`). Sınırı bilinmeli: EDL **hız ve geri uygulayamaz**; onlar `speed` özelliği ve
önceden üretilmiş geçici kesitlerle çözülür. Karşılaştırma paneli `lavfi-complex` ile.

**Önizleme ve teslim aynı plan nesnesinden beslensin** (Remotion ilkesi). Ayrı yol açılırsa
"önizlemede başka görünüyordu" hatası kaçınılmaz olur.

**Küçük resim: `-skip_frame nokey`, ama zaman damgasıyla birlikte sakla.** Anahtar kareler
düzenli aralıklı değildir; "n'inci kutu = n × aralık" varsayımı gerçek videoda kırılır.
LosslessCut'ın modeli (görünen pencere için sabit 10 adet, concurrency 2, 300 ms debounce)
video uzunluğundan bağımsız maliyet verir.

**Dalga biçimi: ham PCM + C# tarafında min/max.** `showwavespic` PNG'si zoom'da yeniden
koşmak gerektirir. `-ar` ile downsample ederek zarf çıkarma — düzleştirir.
`audiowaveform` dağıtılmasın (MP4/MKV okumuyor). **stderr boşaltılsın** — bilinen kilitlenme.

**Çerçeve kullanma, doğrudan ffmpeg üret.** MLT'nin C# bağlayıcısı Mono/Linux dönemine ait,
GES'in .NET tarafı donmuş, OTIO'nun .NET bağlayıcısı yok. Kopyalanacak olan kod değil **model**:
`start`/`length` mutlak zaman kutusu + kaynak içi `offset` + rasyonel `timebase` + sürüm alanı.

**Parça parça derle, tek dev `filter_complex` kurma.** Taranan hiçbir proje karmaşık işi tek
grafa derlemiyor. Kazanç: paralellik, kısmi yeniden koşum (hedef boyu tutmayan parça tek başına
tekrarlanır — VidShrink'in iki geçişli sıkıştırmasına doğrudan uyar), ara ürünlerin `.calisma/`
altında incelenebilmesi.

**VidShrink'in kimsede olmayan avantajı: `-force_key_frames`.** Zaten kodluyoruz. Kendi
çıktımızın anahtar kare aralığını bilinçli seçmek, o dosyaların **sonradan kayıpsız
kesilebilirliğini** bir tasarım kararı hâline getirir. Hiçbir transkoder bunu satmıyor.

## 9.3 Doğrulanmamış Kalanlar

- mpv EDL, `--play-direction`, `--lavfi-complex` **koşturulmadı** (makinede mpv CLI yok).
- Gerçek kamera görüntüsünde kodlama süreleri ölçülmedi; malzeme sentetik.
- Varyans ölçülmedi — her sayı tek koşum.
- `minterpolate` ile gerçek ara kare üretimi ölçülmedi.
- H.265 ve AV1'de smart cut sınırları ölçülmedi; yalnız H.264 sınandı.
- Ticari araçların (SolveigMM, TMPGEnc) smart render iddiaları üretici beyanıdır.
