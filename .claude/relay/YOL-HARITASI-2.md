# Yol haritası 2 — HandBrake'i geçmek

2026-09-02. Birinci yol haritası (T1–T94) motoru ve ölçüm disiplinini kurdu.
Ürün hedefi hâlâ karşılanmadı, bu yüzden harita yeniden çiziliyor.

## Nerede duruyoruz

Eş boyutta: HandBrake VMAF-NEG **48,96**, VidShrink **40,17**. **8,79 puan
gerideyiz.** Kaynak `docs/olcumler/handbrake-acigi.md`.

Aynı belgede bizi önde gösteren eski tablo **GEÇERSİZ DÜZENEK** damgalı.
Bu, haritanın birinci maddesini belirliyor: önce alet, sonra rakam.

## Elimizdeki düzenek (2026-09-02 doğrulandı)

- `.calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4` — 1920x1080 hevc yuv420p10le,
  bt2020nc / smpte2084 / bt2020, 60 fps, 1036,17 sn, 1.729.085.563 bayt.
- `ffmpeg`, `ffprobe`, `HandBrakeCLI` PATH'te.
- Kodlayıcılar: libsvtav1, libx265, av1_nvenc, av1_qsv, av1_amf,
  hevc_nvenc, hevc_amf, h264_nvenc, h264_qsv, h264_amf.

Belgede "kaynak yoktu" diye park edilmiş her ölçüm artık koşulabilir.

## Açığın aday kaynakları

8,79 **ortalamadır**. Harmonikte açık 10,18, p10'da 14,60 — tek sayı kuyruk
hasarını olduğundan küçük gösteriyor. Hedef ortalamayı değil kuyruğu kapatmak.

| # | Aday | Ölçülen | Durum |
|---|---|---|---|
| A | Kodlayıcı — libx265 slow 2-pass, aynı yerleşimde | ort **-0,63**, harmonik **+8,65**, p10 **+13,76**; süre 216 → 1599 sn | kuyruk açığının ana sahibi |
| B | Çözünürlük tabanı — 882x496 seçiliyor, 720p60 hiç aday olmuyor | ölçülmedi | **T99** |
| C | Tepe/VBV tavanı — 1,02x → 1,50x | ort **+1,69**, harmonik **+5,87**, p10 **+7,22**, süre ~0 | **T98** |
| D | GOP — biz 2 sn (`-g fps*2`), HandBrake tarafı çok daha uzun | ölçülmedi | **T98** |
| E | Sahne bazlı bit dağıtımı yok | harita var, kestirimi 0,976/0,929; **28 kesimin 10'unu buluyor** | T96 mühürlü, T101 turda |
| F | psy-rd / AQ | ort +0,095 | kapandı (T87, T92) |
| G | HDR yıkımı | karşılaştırma dışı; iki taraf da tonemap-hizalı puanlandı | kod düzeltildi (`28637a4`, `main`'de) |

Payların doğrulanması T95'in aletine bağlı. Sıralama tahmindir, ölçümle değişir.

## Düzeltilen öncüller (2026-09-02)

Üç varsayımım kaynağa bakınca çürüdü. Yazıyorum ki tekrar edilmesin:

- **HandBrake çıktısı 1080p değil, 1280x720@60.** Çözünürlük açığı 720p'ye
  karşı hesaplanmalı.
- **Varsayılanımız donanım değil.** `SpeedMode` varsayılanı Quality
  (`PlanCalculator.cs:16`), Compatible → libx264. `av1_nvenc` yalnız Fast
  kutusundan ve WhatsApp yolundan geliyor; şikâyet koşusu `--speed fast`'ti.
- **HDR sessiz düşüşü kod tarafında zaten düzeltilmiş** — `28637a4`,
  `main`'de. Kalan soru "neden düşüyor" değil, "düzeltme gerçek kaynakta
  çalışıyor mu ve ne kazandırdı".

## Taban neden yanlış yerde duruyor

`CodecModel.FloorBppf` av1 = 0,020, donanımda x1,25 = **0,025**
(`CodecModel.cs:58-67`). `PlanCalculator.cs:612` bu tabanın altındaki
yerleşimleri eliyor. 790k'da 720p60 = 0,0143 bppf → elenir; 882x496 = 0,030
→ geçer. **HandBrake'in kazanan dosyası 0,0116 bppf'te koşuyor.** Yani
tabanımız, rakibin kazandığı yerleşimi aramaya bile başlamadan dışlıyor.

## Dinamiklik ilkesi (kullanıcı, 2026-09-02)

Ürünün farkı **dinamikliktir.** Sabit iki saniyelik pencere gibi statik
yöntemler yerine içeriğe göre boyutlanan bir işleyiş. Bir sabiti başka bir
sabitle değiştirmek bu ilkeyi karşılamaz.

İkinci yarısı da bağlayıcı: **hiçbir ayar bilmeyen kullanıcı auto modumuzda
uzmana yakın sonuç almalı.** Ölçüsü T102'de.

Her sözleşmede sınanacak soru: bu sayı içerikten mi geliyor, yoksa biz mi
koyduk? Biz koyduysak neye göre?

## HandBrake kaynağı okundu (2026-09-02)

Kullanıcı kaynağı incelemeye yetki verdi; okundu ve karşılaştırıldı.

| Nokta | Onlar | Biz |
|---|---|---|
| Anahtar kare | `min-keyint=fps`, `keyint=10*fps`, yeri **sahne kesimi** belirler; eşik `scenecutThreshold=40`, bias GOP yaşıyla büyür | `-g fps*2` sabit (`FfmpegArguments.cs:151`) |
| CRF'te VBV | dosya kodlamasında **boş** (`encx265.c:514-522`) | sabit 2x maxrate / 4x bufsize (`:143`) |
| Lookahead | `rc-lookahead` 5→60 kare, **kayan**; SVT lookahead'i minigop yapısından türetir | sabit 2 sn pencere, en çok 3 (`ComplexityProbe.cs:14,18`) |
| Preset | kullanıcıya yalnız preset adı sorulur; **hedef boyut modu yok** | hedef boyut birincil — bu bizim gerçek farkımız |
| Çözünürlük | hedef boyuta göre düşürme yok, preset tavanı statik | ölçülmüş üstellerle tam yerleşim araması (`PlanCalculator.cs:622-658`) — **burada öndeyiz** |

Üç kaldıraç çıktı: GOP'u aralığa çevirmek, CRF'te maxrate'i gevşetmek,
örneklemeyi paket profiline bağlamak. İlk ikisi T98'de, üçüncüsü açılacak.

## Sözleşmeler

| # | İş | Durum |
|---|---|---|
| T89 | plan hesabı, ölçülen kaliteyle + K13 | **mühürlendi** |
| T92 | yanlışlanamayan ölçü | **mühürlendi** |
| T96 | sahne haritası ve kestirim değeri | **mühürlendi** (Spearman 0,976) |
| T97 | algı ölçüsünün doğruluğu | **mühürlendi** (kelepçe kaldırıldı) |
| T95 | A/B ölçüm düzeneği (`tools/VidShrink.Ab`) | koşuyor |
| T98 | sabit GOP ve CRF tavanı → dinamikliğe geçiş | koşuyor |
| T100 | ölçülen kalitenin kazancı kullanıcıya ulaşmıyor | koşuyor |
| T101 | haritanın kodlayıcı aktarımı ve kaçırılan kesim | koşuyor |
| T102 | auto mod — bilmeyen kullanıcı ne alıyor | koşuyor |
| T104 | ölçü penceresi de sahneden gelsin | koşuyor |
| T94 | HDR düzeltmesinin gerçek kaynakta doğrulanması | `depends: [T89, T95]` |
| T99 | bppf tabanı kazanan yerleşimi aramıyor | `depends: [T89, T95]` |
| T103 | içeriğe bağlı örnekleme (üçüncü kaldıraç) | `depends: [T96, T100, T101]` |

## GOP: iki bağımsız kaynaktan doğrulandı

T102 ölçtü (2026-09-02), auto mod tabanına karşı tek değişkenle:

| satır | boyut | ortalama | p10 |
|---|---|---|---|
| auto (`-g fps*2` = 120) | 15,04 MiB | 94,462 | 94,534 |
| preset 4 tek başına | 13,97 MiB (−%7,1) | 94,420 | 94,241 |
| **`-g 300` tek başına** | **11,35 MiB (−%24,5)** | **94,617** | **94,868** |

Dosya dörtte bir küçülürken puan **yükseliyor**. Her iki eksende de kazanç
olduğu için bu sonuç boyut eşitliği tartışmasından bağımsız.

HandBrake kaynağı aynı şeyi söylüyordu: `min-keyint=fps`, `keyint=10*fps`,
yerleşim sahne kesiminden. Sabit `-g fps*2`, scenecut zaten açıkken sahne
**olmayan** yerlere I-kare basıyor ve bütçeyi yiyor.

Sabit `FfmpegArguments.cs:162`'de ve T98'in `owns`'unda. T102 ölçtü,
düzeltmiyor.

## T97'nin çıkardığı ölçü hatası

`NormalizeVmafCeiling` (`score >= 99.8 ? 100.0 : score`) yüksek kalitede
A/B farkını **siliyordu**: özdeş kopya (99,8712) ile crf 10 yeniden kodlama
(99,8392) ikisi de 100,0000 raporlanıyordu. Kaldırıldı. Denetimde.

8,79 puanlık açık 99,8'in çok altında ölçüldüğü için etkilenmemiş olmalı —
denetçiye bunu doğrulatıyorum, varsaymıyorum.

## T89'un çıkardığı kopukluk (T100)

Plan, HDR kaynakta 40 MB bütçenin 15,5 MB'ını bilerek harcamadan bıraktı
(crf 22 → 24,483 MB). Teslim edilen dosya yine de 38,404 MB oldu: `EncodeRunner`
çıktıyı band altı sanıp yeniden kodluyor ve bütçeyi dolduruyor. Planlayıcının
"burada durmak daha iyi" kararını koşucu kaza sayıyor.

İki sonucu var: durdurma kısıtının teslim edilen dosyaya etkisi sıfıra yakın,
ve T89'un ölçtüğü %78–%193 süre artışının tamamı bu gereksiz yeniden
denemelerden geliyor. Ayrıca `MainWindow.axaml.cs` ölçülen kaliteyi çağırıp
atıyor — ölçülen yol uygulamada uyuyor.

## Harita az bölüyor (T101, 2026-09-02)

Gözle doğrulanmış pencerede (144,2–333,3 sn): **28 gerçek kesim, harita 10
üretti, 18 kaçtı.** Yanlış pozitif **sıfır** — harita ne diyorsa doğru,
söylemediği çok.

Kaçan 18'in 18'i de tek elekte düştü: `SceneMap.DefaultThreshold = 0.2`,
skorları 0,112–0,199. Öteki iki eleğin payı sıfır — `BaseThreshold = 0.05`
bugünkü ayarda hiçbir gerçek kesimi düşüremez (0,2 ≥ 0,05), `DefaultMinSceneSeconds`
yalnız zaten kesilmiş geçişin ikinci yakalanmasını eliyor.

Bu **tek parametrelik** bir sorun ve yönü belli: 0,2 fazla yüksek. Ama
düşürmenin bedeli ölçülmedi — yanlış pozitif bugün sıfır ve eşik düşünce
sıfır kalmayabilir. Eşiği ölçüsüz oynatmak, ölçüsüz koymakla aynı hata.

Kodlayıcı aktarımı da ölçüldü: libx264 0,976, libx265 **0,929**, libsvtav1
**0,929**. Kayıp tek sahneden geliyor ve HEVC ile AV1 birbirine 1,000 —
sapma sistematik, kodlayıcıya özgü değil. n=8'de bu fark tek sıra takası,
istatistiksel olarak ayrılamaz.

**Sonraki tur:** T105 — eşik eğrisi üç pencerede ölçülür, değer ölçüye göre
konur, müşterilere (T98 aralık, T104 pencere) eski/yeni sahne sayısı bildirilir.

### Sondayı ilk geçişle birleştirmek — sahipsiz iş

T101'in K6 yargısı: sonda maliyetinin (%10,36) ezici çoğunluğu **ikinci bir
çözme**. 107 sn'nin ~92 sn'si çözme; `select` filtre grafiğinde çalıştığı
için kare atlatma çözmeyi atlayamıyor — ölçüldü, atlatmalı sonda 131,4 sn,
çıplak çözme 91,8 sn, taban maliyetin altına inilemiyor. Kare atlatma
**reddedildi**.

Seçilen yol: sondayı asıl kodlamanın **ilk geçişiyle** birleştirmek. İki
kazanç birden var ve ikincisi beklenmedik:

- Çözme tümüyle kalkar (~%86).
- İlk geçiş istatistikleri **hedef kodlayıcının kendi kare boyutları**
  olduğu için, K1'deki 0,929'luk aktarım sapması kökünden kalkar — sonda
  artık vekil bir kodlayıcı değil, kodlayıcının kendisi olur.

Bedeli planı iki aşamaya bölmek. `EncodeRunner` (T100) ve `PlanCalculator`
(T99) işi, ikisi de şu an başka turda. **Sahipsiz; T99 ve T100 mühürlendikten
sonra sözleşmeye çevrilecek.** Buraya yazıldı ki mühürde buharlaşmasın.

## Makine paylaşımı — hangi sayı bozulur, hangisi bozulmaz

Altı ajan koşuyor, dört ffmpeg aynı anda. "Aynı anda tek ağır kodlama" kuralı bu
ölçekte uygulanamıyor ve ajanlar onu **bekleme gerekçesi** yapmaya başladı. Kural
keskinleşiyor: yükü beklemek yok, yükü doğru okumak var.

| Sayı | Yükten etkilenir mi | Ne yapılır |
|---|---|---|
| Duvar saati / süre | **Evet, doğrudan** | Rapora "makine paylaşımlıydı, N ffmpeg" damgası basılır |
| Spearman, sıra korelasyonu | Hayır — sıra ölçüsü | Damga basma; yersiz çekince okuru yanıltır |
| VMAF / boyut, **iş parçacığı sabitken** | Hayır | Damga basma |
| VMAF / boyut, **iş parçacığı sabit değilken** | **Evet** | Ölçüm geçersiz; sabitleyip tekrarla |

Son satır esas olan. Kodlayıcı iş parçacığı sayısını boştaki çekirdeğe göre seçerse
bölümleme koşumdan koşuma değişir ve çıktı da değişir — T87 turundaki kararsızlık
buradan geliyordu, "paralellik" kendisinden değil. Karşılaştırma koşumlarında
`-threads N` (x265'te `pools`) **komut satırında yazılır**.

Bir ajanın yükü fark etmesi doğru; yükü bekleme gerekçesi yapması değil.

## Ölçü aracı sanıkta (T106, 2026-09-02)

T102 auto modu ölçerken aletin kendisinde kusur buldu. VMAF-NEG, SVT-AV1
çıktısında **1 puanın altına düşen bir kare kümesi** üretiyor (auto'da 26 kare,
25'i tam 0); iki x265 koşumunda hiç üretmiyor (en düşük 74,67). O karelerde
PSNR 46-49 dB, HandBrake 98-100 alıyor — yani görüntü aslında temiz.

Bench'in harmonik ortalaması `n / Σ(1/max(x,1))`. `max(x,1)` yüzünden 0 ile
0,13 aynı etkiyi yapıyor ve tek bir düşük kare sütunu çökertiyor: bugünkü
haliyle AV1'i x265'e karşı **39,4 puan** geride gösteriyor.

Bu yüzden T106, kodlayıcı seçim kuralından **önce** gelir. O kural p10 ile
harmonik ortalamaya dayanıyor; ikisinden biri yalan söylüyorsa yanlış
kodlayıcıyı seçeriz. T106 ayrıca `docs/olcumler/` altında kirlenmiş satırların
listesini çıkaracak; sahiplerine T0 dağıtacak.

**Bu arada kural:** harmonik sütuna yaslanan yeni bir sonuç yazılmaz.
Ortalama ve p10 sağlam.

### Cevap geldi: kusur ölçüde değil, hizalamada (T106, 2026-09-02)

Yukarıdaki teşhis **yanlıştı** ve sayı doğruydu. Harmonik ortalama sağlam,
`max(x,1)` kelepçesi suçlu değil. Suçlu **ölçüm boru hattımızın kare
eşlemesi**: karşılaştırmayı zaman damgasıyla yapıyorduk, kaynağın video akışı
`0,020000 s`'de başlıyor, bizim ffmpeg çağrımız çıktıyı `0,016667 s`'ye
taşıyor. Kare başına −3,3 ms; ffmpeg framesync "damgası küçük-eşit son
referans" seçtiği için **her kare komşusuyla** karşılaştırıldı. Yarım kareden
küçük bir kayma tam bir kare kaydırma üretti.

Kareler indekse kilitlenince aynı dosyalarda PSNR 17,2 → **44,74 dB**.

| | min | <1 | harmonik |
|---|---:|---:|---:|
| AV1 eski | 0,000 | 26 | 56,313 |
| AV1 yeni | 92,391 | 0 | **95,655** |
| x265 yeni | 76,219 | 0 | 95,793 |

**AV1 ↔ x265 harmonik farkı 39,446 → 0,138.** İki kodlayıcı bu kaynakta başa
baş. Kodlayıcı seçim kuralı (5. basamak) artık açılabilir — ama 39,4 değil
**0,14** puanla; yani o basamağın gerekçesi buharlaştı, yeniden gerekçe
gerekiyor.

Kusur **AV1'e özgü değil**: `libsvtav1`, `libx264`, `libx265` üçü de
`start_time`'ı düşürüyor, HandBrake düşürmüyor. AV1 yalnızca T102'de ölçülen
taraf olduğu için suçlu göründü. `docs/olcumler/algi-olcusu.md:171` bir **x264**
koşumunda `VMAF-NEG min 0,0000` gösteriyor — bağımsız teyit.

**Ayıklama testi, herkese:**
`ffprobe -v error -select_streams v:0 -show_entries stream=start_time -of csv=p=0 <kaynak>`
`0` dönerse o satır temiz.

**Ders — bu projenin en pahalı sınıfı, altıncı kez:** sayı doğruydu, sayıyı
açıklayan cümle yanlıştı. "Harmonik ortalama yalan söylüyor" tanısı üç
sözleşmenin sınırına yazıldı ve hiçbirinde ölçülmedi. Bir tanının kendisi de
ölçülür.

## Alet kendi adlandirdigi kusuru tekrarladi (T106 denetimi, 2026-09-02)

T106 denetimden **KALDI** dondu — ama teshis onaylandi. Duseren sey su:

T106'nin K4 listesi `tools/auto-mod-olcumu/tablolar.py:24`'u "sutun adi
saydigiyla uyusmuyor" diye isaretledi. **Ayni commit'te bench'in kendi
ciktisinda ayni kusuru uretti:** `peak-curve` tablosunun baslik satiri 10 sutun,
veri satiri 11 hucre. Markdown isleyicisi fazlayi atar, yani T106'nin ekledigi
kelepce uyarisi aletin rapora sayi ureten asil isinde sessizce kaybolur.

Denetci bunu iddiadan degil **gercek kosumdan** cikardi. Ders bu:

**Bir kusuru adlandirmak ondan bagisiklik vermez.** Cikti bicimini degistiren her
commit, ciktinin kendisine sorulmadan kapanmaz. "Uc yerin ucu de artik damgayi
yaziyor" cumlesi kod okunarak yazilmisti; alet kosturulsaydi bir satirda
gorunurdu.

Ayrica denetci iki olcunun **davranis degil dizgi** olctugunu buldu:
`Assert.Contains("settb=AVTB,setpts=N", …)` — `setpts=N+1` mutasyonu 10/10 yesil
geciyor. Yani **bir kare kaydiran kilit**, yani T106'nin kapattigi kusurun tam
kendisi, olcuye takilmiyor. Bu projenin adi konmus kusuru: sabit karsilastiran
test davranis olcmez.

### Ayni acik uretim kodunda duruyor — T110

`src/VidShrink.Ffmpeg/QualityMeter.cs:278` grafiginde kare kilidi yok ve
`--measured-quality` ile **kalibrasyon cipalari** o yoldan geciyor. Alet duzeldi,
urun duzelmedi; ikisi ayrisirsa bir sonraki yanlis karar oradan cikar.

**T110 acildi.** Kalibrasyon cipalarinin hepsi kaymis olcuyle konmus olabilir —
o zaman hepsi gecersiz. `docs/olcumler/algi-olcusu.md:171` bir x264 kosumunda
`VMAF-NEG min 0,0000` gosteriyor; belge de kirlenmis.

## Aynı kusuru iki tur birden düzeltiyor (T98 × T105)

T98 anahtar kare üst sınırını `SceneMap` ortalamasından türetiyor ve
**2,8'e bölüyor**. Bölen T101'in yer gerçeğinden geliyor: 28 gerçek kesime
karşı harita 10 sahne buluyor.

Ama T105 tam o kusuru düzeltiyor — eşik ölçüye göre yeniden konuyor ve harita
daha çok sahne bulacak. İkisi de `main`e girerse **düzeltme iki kez uygulanır**
ve üst sınır olması gerekenin yarısına iner.

Genel kural, bundan sonrası için: **bir sabit başka bir turun düzelttiği
kusurun telafisiyse, o sabit koda yazılmaz.** Ya kusurun ölçüsünden türetilir,
ya da onu doğuran değere bakan bir ölçüyle bağlanır ki değer değişince
ölçü kırılsın. Sessizce doğru kalan telafi sabiti yoktur.

## Kapanan açık, kapanmayan sınıf (T100, 2026-09-02)

Arayüz kullanıcıya "burada durur, kalanı harcamaz" derken teslim edilen dosya
39,14 MB oluyordu — vaat 8,7 MB'tı. İki kapı ayrışmıştı: koşucu
`actualMb >= HardFloorMb` istiyor, arayüz istemiyordu. Kapandı, tek yükleme
bağlandı, mutasyonla doğrulandı.

**Ama sınıf kapanmadı.** Arayüz `note.Mb`'yi vuruyor — bu planın *tahmini*.
Koşucu `actualMb`'yi vuruyor — bu *gerçek*. Üç kaynakta ikisi tabanın aynı
yanına düştü, o yüzden cümle tutuyor. Tahmin tabanın öbür yanına düşen bir
kaynakta yine ayrışır, ve **bugünkü ölçü bunu yakalayamaz**: test koşucuya
`note.Mb ≈ actual` olan bir plan veriyor.

Gerçek çözüm, kararın planlayıcıda saklanması — `StopsShortOfBandOnPurpose`
bugün türetilen bir işaret, `PlanCalculator` koşulu zaten biliyor ama
yazmıyor. T99 o dosyayı tuttuğu için bu turda taşınamadı. **T99 mühürlenince
açılacak ilk küçük iş budur.**

## Taban indi, plan hala kaybedeni seciyor (T99, 2026-09-02)

T99 av1 bppf tabanini 0,020'den 0,0095'e indirdi ve donanim carpanini 1,25'ten
1,52'ye tasidi; ikisi de olcumden. Taban artik kazanan yerlesimi elemiyor.

Ama sikayet kapanmadi. Olculen kazanan `1280x720@60`, `882x496@60`'in **6,39 puan**
onunde — iki pencerede de ayni yonde. Plan hala `882x496@60` seciyor. **Eleyen taban
degil, skor.** `PlanCalculator.LayoutScore` (`PlanCalculator.cs:618`) iki tahmin
egrisinin farki: dusuk cozunurlukte `rate` firliyor, `ScalePenalty` onu 6,39 puan
eksik dengeliyor. Iki yarisi da olculen kaliteye karsi hic kalibre edilmedi.

Ders: **tabani indirmek kazanani secmeye yetmiyor.** Elek ile secici ayri seylerdir;
biri duzeltilince oteki kendiliginde duzelmiyor. T107 bunu olcuye oturtuyor.

Yan etki, T99'un kendi bildirdigi: carpan yukselince `hevc_nvenc` tabani da
0,02196'dan 0,02671'e cikti ve **o taban olculmedi**. Donanim kolunda
`UsableBitrateK` (706k = 0,01277 bppf) yeni tabandan siki, yani taban orada atil ve
HandBrake'in 0,0116'lik noktasi hala disarida.

## Esik olcuye oturdu, T101 iki yerde yanildi (T105, 2026-09-02)

`SceneMap.DefaultThreshold` 0,2 → **0,105**. Olcut yazili: uc pencerenin
birlesiminde F2 (β=2) tepesi. F1 secilseydi 0,115 cikardi; aradaki fark
asimetrinin kendisi — kacan kesim hatayi sahne boyunca tasiyor, yanlis kesim
yerel.

T101'in iki sonucu **yanlis cikti**:

- **"Yanlis pozitif sifir"** yalniz 0,2 icin dogruymus. 0,05'te uc pencerede
  toplam **46 yanlis kesim** var.
- **`DefaultMinSceneSeconds = 1.0`'in payi sifir degil.** P2'de 334,000 her
  esikte kaciyor cunku 333,300'de kesim var. 0,5'e cekince P2 6/7 → 7/7,
  F2 0,899 → 0,922. Sabit yine de degistirilmedi: bedeli T98'in anahtar kare
  araliginda ve T104'un penceresinde, ikisi de olculmedi.

**En iyi esik pencereden pencereye kayiyor** — P1 0,105–0,110, P2 ≤0,08,
P3 ≥0,115. Durgun ve hareketli **ters yone** cekiyor, ~0,035 aci. Bu, urunun
dinamiklik ilkesinin dogrudan kaniti: tek sabit esik dogru cevap degil,
icerikten turetilebiliyorsa turetilir. Sonraki basamak adayi.

Musteriye: 24 sahne / ort 43,17 / medyan 14,03 → **77 sahne / 13,46 / 5,62**.
Dagilim saga carpik; **aralik secen taraf medyani kullanmali**, ortalamayi degil.

## Iki tur ayni sabite bagli — carpisma kontrollu (T98 × T105)

T98 2,8 bolenini koda sabit olarak yazmadi: olculdugu iki sayiya ayirdi
(`SceneMapGroundTruthCuts = 28`, `SceneMapReportedScenes = 10`) ve
`SceneMapThresholdOfRecord = 0.2` ile `SceneMap.DefaultThreshold` ayrisirsa
olcu kirmiziya donuyor.

T105 esigi 0,105 yapti. **Yani tuzak kuruldugu gibi calisacak:** T105 `main`e
girince T98'in olcusu kirmizi doner ve duzeltme yeniden olculmeden gecemez.
Sira: once T105, sonra T98 yeni sayilarla hizalanir.

Ders: **telafi sabitini silmek tek yol degil — olculdugu kosula baglamak da
calisiyor.** Sabit kalir ama sessizce yanlislasamaz.


## CI motorun dortte birini gormuyor (T106 tur 2, 2026-09-02)

T106 yapicisi kendi kusurunu bildirdi ve bildirdigi sey sozlesmesinden buyuktu:
uc yeni olcu `[FfmpegFact]` tasiyor, dolayisiyla kare kilidini tutan pim
**yerelde koruyor, CI'da atlaniyor.**

Sayilar: T99 CI 1030 gecti / **95 atlandi**; T98 CI 1046 / **97**; T106 yerel
tam suit 1106 / **17**. Aradaki ~80 olcu ffmpeg isteyenler ve on bir dosyaya
yayilmis — `ComplexityProbeTests`, `EncodeRunnerTests`, `FpsDropTests`,
`FrameGrabberTests`, `PanelHostTests`, `PerformanceCheckTests`,
`PreviewSyncTests`, `QualityMeterTests`, `QualityTargetTests`, `SceneMapTests`,
`SegmentEncoderTests`.

Yani **motoru olcen her sey** CI'nin kor noktasinda. "Teslimden once tam suit
kos" kurali bunu kapatmiyor; kural yerel kosumu buyutuyor, CI'nin gordugunu
degil. **T115** acildi ve dagitildi.

Ikinci ders ayni turdan: **artimli derleme mutasyon olcumunu zehirliyor.**
Yapici bir sabiti bozup "kirilmadi" okudu; `--no-incremental` ile tekrarlayinca
tablo degisti. Mutasyon kaniti bu projede kabul kriterinin kendisi — yanlis
okunan bir "kirilmadi" tum kaniti curutur. **Kural: her mutasyondan sonra
`dotnet build VidShrink.sln -c Release --no-incremental`, sonra `--no-build`
ile kos. Denetci istemine de yazilir.**

Ucuncu ders: **esdeger mutasyon olcu acigi degildir.** T0 `setpts=N+1`
mutasyonunu onerdi; yapici olctu ve sabitin iki dalda birden kullanildigini,
her iki akisi ayni miktarda kaydirdigini gosterdi. Ortada davranis farki yok,
hicbir davranissal olcu yakalayamaz. Mutasyon secerken once sabitin kac yerde
kullanildigina bak.

## Sonraki basamak

1. **T106** — ölçü aracının geçerliliği. Kodlayıcı seçim kuralından önce gelir;
   o kuralın kanıtı p10 ve harmonik ortalamadan geliyor.
2. **T105** önce girer — eşik ölçüye oturdu (0,105), CI yeşil, denetimde.
3. **T98** ardından hizalanır: T105 girince tel tuzağı kırmızıya döner ve
   bölen yeni eşikte yeniden ölçülür. GOP aralığı da o zaman `main`e iner.
4. **T99** taban kararını verdi (denetimde). Mühürlenince iki iş birden açılır:
   **T107** yerleşim skorunu ölçüye oturtur (asıl şikâyet orada), **T103**
   örneklemeyi alır.
5. **T95** teslim edince T94 açılır — HDR hizası iki farklı aracı
   karşılaştırıyor, aletin adillik kapısı orada gerçekten lazım.
6. `SceneMap` tüketime bağlanır — harita hâlâ okunmuyor. İki ayrı iş:
   **T113** anahtar kare kararını bağlar (üç çağrı yeri `scenes: null`
   geçiyor), **T114** bit dağıtımını sorar. T114'ün asıl sorusu "kodlayıcı
   zaten yapıyor mu" — cevabı hayırsa ürünün en büyük kolu, evetse iş biter.
7. Kodlayıcı seçim kuralı ölçülen veriye göre yeniden yazılır. **T106'dan
   sonra.**
8. **T108** — tepe eğrisi. T98 ölçtü ve eğrinin şekli ölçümle ters göründü:
   aşma kanıtının geldiği ~11,4×'te geniş, açmanın +3,665 puan kazançlı
   ölçüldüğü ~4,6×'te 1,02'ye kilitli. T98 mühürlenince açılır.
9. **T111** — T102'nin sekiz sayısı kaymış eşlemeyle ölçüldü. Uzman açığı
   (+0,400) ayakta, HandBrake karşılaştırması değil. T110 kilidi inince
   yeniden ölçülür.
10. **Eşik içerikten türetilir.** T105 ölçtü: durgun ve hareketli pencere ters
   yöne çekiyor, sabit tek eşik üçünün hiçbirinde en iyi değil. Ürünün
   dinamiklik ilkesinin en somut adayı.

## Kaynak okundu: bir oncul dogrulandi, bir oncul curudu (T112, muhurlendi)

`docs/inceleme/handbrake-motoru.md` — HandBrake `1d2135bc`, denetim GEÇTİ.

### Ayakta kalan fark: kapalı çevrim hedef boyut

**HandBrake'in hedef boyut yolu yok.** `TargetSize` tüm klonda tek kez geçiyor —
`VideoEncodeRateType.cs:17`, enum tanımı. **Okuyan yok.** Karar veren iki yer
yalnız CRF ve ABR tanıyor. Hedef aşımında yapılan tek şey `muxcommon.c:564-568`
içinde bir `hb_deep_log` satırı; geri besleme yok.

İki yoldan doğrulandı — denetçi ayrıca kendi ölçüm belgemizi buldu
(`docs/olcumler/auto-mod.md:361-364`): HandBrakeCLI 1.11.2 `--help` çıktısında
hedef boyut seçeneği yok.

Bizde var: `EncodeRunner.cs:88-165` + `PlanCalculator.cs:525-551`. **Ürünün
ayırt edici özelliği burasıdır ve ölçülmüş bir gerçektir**, iddia değil.

Yan bulgu: yerleşik preset'ler `VideoQualityType: 2` (CRF) ile geliyor ve
`preset.c:2209` kapısı CRF'de `MultiPass`/`Turbo`'yu işe hiç kopyalamıyor —
preset'lerdeki `VideoAvgBitrate` ve `VideoMultiPass` alanları kutudan çıkan
hâlde **ölü.**

### Çürüyen öncül: "biz dinamiğiz, o statik"

Kullanıcının statik yöntem örneği olarak verdiği **2 saniyelik pencere bugün
bizim yaptığımız şey.** `ComplexityProbe.cs:14` `WindowSeconds = 2.0`,
`:17` `MaxWindows = 3`, `:131-143` tavan üç. Denetçi `main`de doğruladı.

Nüans, denetçinin kendi bulgusu: `ComplexityProbe.cs:159` `ScanPoints()`
kaynağın **tamamına** ~40 nokta × 1 sn serpiyor ve `:304` üzerinden pencere
yanlılığına besliyor. Yani "bir saatlik videoyu altı saniyeden planlıyoruz"
cümlesi fazla sert — ama sonuç değişmiyor: **40 da sabit bir sayıdır.**
Örnekleme sabit ölçekli, içerikten türemiyor.

Ve karşı taraf: HandBrake'in **kendi kodunda** kare başına kararı var —
`comb_detect.c:1029-1048` kare başına NONE/LIGHT/HEAVY, `vfr.c:158-171` pencere
içi hareket ölçüsüyle kare seçimi. Kodlayıcıya devrettikleri ayrı; onları
`encx264.c:1363` `hb_x264_param_unparse` içinde ayırt ettik, o karar değil
görüntüleme.

**Sonuç: dinamiklik iddiası bugün karşılıksız.** Karşılığını koymak
**T103'ün** işi ve T103 artık en yüksek öncelikli üründür — sıra 4'ten öne
alınıyor. Ayırt edici özelliğimiz hedef boyut; dinamiklik ise **kazanılacak**
bir fark, sahip olduğumuz bir fark değil.

### Kronik kusurun beşinci tekrarı

Denetim dört sayma hatası buldu ve dördü de aynı kalıpta: **künye ve olgu
doğru, onu özetleyen sayı yanlış.** Preset farkı 7 iken "5", `preset.c` 29
satır iken "26", `work.c` iki satır iken "tek satır".

T98 tur 2 denetimi aynı gün aynı kalıbın dört örneğini daha buldu (oran 1,88×
iken "2,6 katına", fark 0,506 iken "0,1 mertebesinde"). T95 denetimi
manşetin kendisinde buldu.

**Bu artık tekil hata değil, aletin sistematik davranışı.** Kural yetmiyor —
üç sözleşmede de kural yazılıydı. Sayan ile özetleyen aynı geçişte olduğu
sürece tekrar edecek: **özet cümlesi tabloya karşı ayrı bir geçişte okunmalı.**

## Ilk adil A/B: geridiyiz (T95, 2026-09-02, denetimde)

Aylardır HandBrake'i çıktısından tanıyorduk. T95 ilk **adillik kapılı** A/B
aletini kurdu ve altı çift ölçtü. Sonuç ürünün hedefine ters:

| Hedef | HandBrake | VidShrink |
|---|---:|---:|
| 60 MB (harmonik) | 28,70 | 18,98 |
| 600 MB (harmonik) | 67,96 | 58,83 |

**Altı çiftin beşinde HandBrake önde.** Bu sayıya güvenmeden önce üç çekince var
ve üçü de denetimde:

1. **Manşet ölçüt harmonik ortalama** ve harmonik ortalama T106'da soruşturma
   altında. Ajanın kendi tablosunda bir satırda kare minimumu **0,00** duruyor —
   matematiksel olarak tek sıfır harmonik ortalamayı sıfıra çeker, demek ki bir
   taban kıskacı var. Kıskacın manşeti ne kadar biçimlendirdiği ölçülmedi.
2. **60 MB satırları kendi eş boyut kapısını geçmiyor.** Kapı ±%2, VidShrink'in
   üç satırı da −%3,13 / −%8,61 / −%3,17. Kapı kendi kuralına göre o
   karşılaştırmayı geçersiz sayar.
3. **Parça kipi iyimser.** 600 MB'da parça kestirimi 58,83, tam koşum 47,78 —
   sapma **+11,05**. K6'nın sayıları parça kipinden geliyorsa mutlak iddia
   kurulamaz.

Yine de **yön haber değeri taşıyor** ve ölçüm iki gerçek tuzağı yakaladıktan
sonra alındı: HandBrake preset'inin otomatik kırpması 1920x1072 üretiyordu (aynı
satır düzeltilince 20,27 → 93,70), ve parçaların biri ses taşırken öteki
taşımıyordu — bütçe eşitsiz bölünüyordu. İkisi de ölçüyü haksız yapıyordu, ikisi
de ajanın kendi bulgusu.

**Bütçeyi bitirmiyoruz.** 60 MB hedefinde üç satırın üçü de hedefin altında
kalıyor. Daha az bit harcayıp daha düşük puan almak, kalite açığının bir kısmını
tek başına açıklayabilir — ama **bu bir hipotez, ölçülmedi.** Nedensel bağı
kurmak için eş boyutta yeniden ölçmek gerekir.

Bu bulgu T112'yle kavuşuyor: bir taraf HandBrake'in **çıktısını** ölçüyor, öteki
**kaynağını** okuyor. Açığın büyüklüğünü T95, sebebini T112 verecek.

## Duzenek tools'a ulasamiyor — ikinci tekrar (T105, T98)

`AGENTS.md` diyor ki: ölçümü üreten düzenek `tools/` altına taşınır. Ama `owns`
disiplini diyor ki: sahiplenmediğin yola yazamazsın. **İki sözleşme arka arkaya
bu iki kuralın arasında sıkıştı** — T105 sahne yer gerçeği üretecini, T98 atlama
ölçüm düzeneğini `tools/`a koyamadı. İkisi de `.calisma/` altında kaldı;
`.calisma/` gitignore'lu, yani **düzenekler depoda yok.**

Sonuç: rapora giren sayı kalıcı, onu üreten alet değil. Sayı bir gün
sorgulandığında yeniden üretilemez. T111'in varlık sebebi tam olarak budur —
T102'nin sayıları sorgulandı ve arşivi olduğu için kurtarılabildi.

**Kural: her sözleşmenin `owns` satırı kendi düzenek yolunu içerir.**
`tools/<is-adi>/**` biçiminde, sözleşme yazılırken. Sonradan eklenmez; ajan
duvara çarptığında iş zaten bitmiştir. T111 bunu taşıyor
(`tools/auto-mod-olcumu/**`), T112'nin düzeneği yok.

**Sahipsiz kalan iki düzenek** — bir sahip bulunana kadar borç:
- T105'in sahne yer gerçeği üreteci (`.calisma/` altında, T109'un ağacında)
- T98'in atlama ölçüm düzeneği (`.calisma/t98/atlama/`, ham veri 840+120 satır)

Genel biçimi: **bir kural ihlali iki ayrı kuralın kesişiminden doğuyorsa,
suçlu ajan değil kesişimdir.** İkinci tekrarda ajanı uyarmak değil, sözleşme
şablonunu düzeltmek gerekir.

## Muhurlenmis bir olcum kirli cikti (T102 x T106, 2026-09-02)

T102 mühürlendi ve iyi iş yaptı. **Ama ölçüldüğü boru hattı kaymış** ve bunu
ölçen T106'ydı — T102 kapandıktan sonra.

T106 denetçisi T102'nin ham verisinden doğrudan saydı (`.calisma/t102/vmaf/*.json`):
**altı AV1 koşumunun `<1,0` kümesi birebir özdeş, iki x265/HandBrake koşumunda boş.**
Sebep T110'un konusu: kaynağın video akışı `0,020000 s`'de başlıyor, bizim ffmpeg
çağrımız çıktıyı `0,016667 s`'ye taşıyor, framesync her kareyi komşusuyla eşliyor.
HandBrake `start_time`'ı düşürmüyor — o yüzden temiz.

Yani T102'nin tablosunda **iki ayrı sınıf koşum yan yana duruyor**:

| Karşılaştırma | Durum |
|---|---|
| auto ↔ uzman-biz (ikisi de AV1) | muhtemelen sağlam — yanlılık iki tarafta da var |
| y1 ↔ y2 ↔ y3 (üçü de AV1) | muhtemelen sağlam — aynı gerekçe |
| auto ↔ uzman-handbrake | **sağlam değil** — bir taraf kaymış, öteki değil |

Bu, iki sonucu ayırıyor. **+0,400'lük uzman açığı ayakta** — AV1 ile AV1
karşılaştırılıyor, ceza iki tarafta da aynı. Ama **auto 94,462 ↔ uzman-hb2 95,731**
karşılaştırması ayakta değil; o 1,269 puanın içinde yalnız bizim tarafın ödediği
bir ceza var ve **düzeltmenin büyüklüğü ölçülmedi.** İddia edilmez.

"Muhtemelen" kelimesi bilerek duruyor: kümenin özdeş olması yanlılığın **aynı
yönde** olduğunu gösterir, **aynı büyüklükte** olduğunu göstermez. **T111** bunu
ölçüyor.

Kural: **bir ölçümün mührü, ölçen aletin geçerliliğini mühürlemez.** T102 kendi
kabul kriterlerinin hepsini geçti; kusur kriterlerde değil, hepsinin altındaki
alettteydi. Alet sonradan sorgulandığında **mühürlenmiş sayılar yeniden açılır** —
mühür geriye dönük bir doğruluk garantisi değil, o günkü kanıtın kaydıdır.

## Değişmeyen kurallar

- Sabit karşılaştıran ölçü davranış ölçmez.
- Ölçmediğin şey için "ölçülmedi" yazılır, iddia edilmez.
- Paralel koşumda **iş parçacığı sabitlenir**; süre sayısı damgalanır (aşağıda).
- Harmonik ortalamaya yaslanma (T106 soruşturuyor); ortalama ve p10 sağlam.
- Telafi sabiti koda yazılmaz — ölçüden türetilir ya da **ölçüldüğü koşula bağlanır**
  (T98'in tel tuzağı: koşul kayarsa ölçü kırmızıya döner).
- Mühürden önce `gh run list`.
- `main`e yalnız T0 birleştirir.
