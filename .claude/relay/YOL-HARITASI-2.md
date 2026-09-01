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

## Sonraki basamak

1. T98 GOP aralığını `main`e getirir. **Açığın bilinen en büyük tek kalemi bu** —
   T102 tek değişkenle %24,5 boyut kazancı ölçtü, puan da yükseldi.
2. T99 T95 beklenmeden açıldı (gerekçe sözleşmesinde): tek değişken yerleşim
   olduğu için aletin adillik kapıları gerekmiyor. **T94 hâlâ T95'te** — HDR
   hizası iki farklı aracı karşılaştırıyor, orada kapı gerçekten lazım.
3. T100 + T101 bitince T103 açılır (örnekleme, üçüncü kaldıraç).
4. `SceneMap` `PlanCalculator`a bağlanır (T99 mühürlendikten sonra).
5. Kodlayıcı seçim kuralı ölçülen veriye göre yeniden yazılır — kuyruk
   açığının ana sahibi (aday A, p10'da +13,76) orada.

## Ölçü penceresi sahneye bağlanmadı (T104, 2026-09-02)

Denemesi yapıldı, kazanmadı: sahne sınırlarına oturan ölçüm penceresi sabit 2
saniyelik pencereden **daha iyi ayırmıyor**. Sinyal/gürültü tablosu 24 hücrede
bağımsız yeniden hesaplandı, dördü birebir tuttu. Sabit 2 sn kaldı — ölçüldü,
değiştirilmedi.

Turun asıl kazancı başka yerde: en kötü blok seçilirken kısa kuyruk bloğu
atılıyordu ve bu **8,24 puanlık** bir kör nokta üretiyordu (p3'te gerçek
84,1481, kuralın gördüğü 92,3877). Kapatıldı.

Ders, yol haritasının kendisi için: **sahne haritası her ölçüye yaramıyor.**
Bit bütçesi sahneye göre bölünür (T98), ama kalite ölçüsü bölünmüyor. Haritayı
yeni bir yere bağlamadan önce o yerde kazandığı ölçülür.

## Değişmeyen kurallar

- Sabit karşılaştıran ölçü davranış ölçmez.
- Ölçmediğin şey için "ölçülmedi" yazılır, iddia edilmez.
- Paralel koşumda **iş parçacığı sabitlenir**; süre sayısı damgalanır (aşağıda).
- Mühürden önce `gh run list`.
- `main`e yalnız T0 birleştirir.
