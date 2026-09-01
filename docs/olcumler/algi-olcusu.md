# Algı ölçüsü — normalizasyon, tonemap yolu ve sahne tabanı

T97. Ölçüm makinesi: Windows 11, `ffmpeg 9.0-full_build-www.gyan.dev` (`--enable-libvmaf
--enable-libzimg`). Kaynak: `.calisma/kaynak/parca-1.mkv` — `kaynak-1080p60-hdr-17dk.mp4`
içinden 00:02:00 noktasından 60 sn, `-c copy`. 1920x1080, 60 fps, 3624 kare,
yuv420p10le, bt2020/smpte2084/bt2020nc, `color_range=pc`.

Bu turda süre/hız iddiası yok.


## 1. Mevcut durum — `QualityMeter` bugün ne ölçüyor

Tek bir özel `MeasureAsync` üç genel giriş tarafından çağrılıyor: `MeasureAsync`
(düz), `MeasureTonemappedReferenceAsync` (`tonemapReference: true`) ve iki
`MeasureWindowAsync` aşırı yüklemesi (referans ve test için ayrı `-ss`).

Her metrik **ayrı bir ffmpeg koşumu**. Üçünün de filtre grafiği aynı:

    [0:v]<test-normalizasyonu>[t];[1:v]<tonemap-öneki><referans-normalizasyonu>[r];[t][r]<metrik>

Normalizasyon `zscale` ile açık: giriş uzayı dosyanın etiketlerinden, çıkış uzayı
referanstan alınıyor. `zscale` yoksa ölçüm hata fırlatıyor — sessizce etiketsiz
karşılaştırma yapılmıyor. Referans HDR ise çıkış `yuv420p10le` ve referansın
kendi uzayı; değilse `yuv420p` bt709 limited.

| Metrik | Çağrı | Kaynak | Toplama |
|---|---|---|---|
| VMAF-NEG ortalama | `libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json` | kare başına JSON günlüğü | aritmetik ortalama |
| VMAF-NEG harmonik | aynı koşum | aynı günlük | `n / Σ(1/max(x,1))` — 0 puanlı kare 1 sayılıyor |
| VMAF-NEG p10 | aynı koşum | aynı günlük | sıralı küme üzerinde doğrusal ara değerli 10. yüzdelik |
| VMAF-NEG min | aynı koşum | aynı günlük | tüm kümenin en küçüğü |
| VMAF-NEG en kötü sahne | aynı koşum | aynı günlük | **T97'de eklendi** — 2 sn'lik ardışık pencerelerin en düşük ortalaması |
| XPSNR | `xpsnr` | stderr özet satırı | `(4y + u + v) / 6`, klibin tamamı |
| SSIM | `ssim` | stderr `All:` | klibin tamamı |

Bilinen sınır: XPSNR ve SSIM'in kare başına dökümü okunmuyor, yalnız özet satırı
alınıyor. Bu yüzden p10, min ve sahne tabanı **yalnız VMAF-NEG için** var. Bu
turda değiştirilmedi.

Karşılaştırılabilirlik kapısı: referans ve test'in HDR olup olmadığı ayrışıyorsa
ya da ikisi de HDR ama aktarım/ana renkleri farklıysa ölçüm `Comparable=false`
dönüyor ve hiçbir sayı üretilmiyor. Tek istisna tonemap yolu (§3).


## 2. `NormalizeVmafCeiling` yargısı — **bozuyordu, kaldırıldı**

Kaldırılan kod, kare başına *ve* toplanmış dört değerin her birine ayrı ayrı
uygulanıyordu:

    private static double NormalizeVmafCeiling(double score)
        => score >= 99.8 ? 100.0 : score;

Üstündeki cümle "vmaf_v0.6.1neg özdeş karelerde ~99,87 veriyor" diyordu. O sayı
tek bir içeriğe ait: 320x240 `testsrc2`. Gerçek tavan içeriğe ve çözünürlüğe göre
oynuyor.

**Ölçüm — kelepçenin tavanla ilişkisi yok.** Bit düzeyinde özdeş dosyanın
kendisiyle karşılaştırılması:

| Özdeş çift | ham VMAF-NEG ortalama | kelepçeli | eşiğin altında mı |
|---|---|---|---|
| 320x240 testsrc2 crf 23 | 99,8712 | **100,0000** | hayır, 99,8 üstü → kelepçe çalıştı |
| 1920x1080 60 fps gerçek içerik (10 sn) | 99,6769 | 99,6769 | evet → kelepçe hiç çalışmadı |

Yani sabit 99,8 eşiği asıl hedef içerikte (1080p60) modelin tavanına *ulaşmıyor*:
özdeş kopya 99,68 diye raporlanıyordu. Eşik yalnız küçük sentetik klipte
tetikleniyordu.

**Ölçüm — kare başına kelepçe neredeyse hiç iş yapmıyordu.** VMAF-NEG kareleri
ya tam 100 veriyor ya da 99,8'in belirgin altına düşüyor; `[99,8; 100)` bandında
kare pek kalmıyor:

| Günlük | kare | `[99,8; 100)` | `== 100` |
|---|---|---|---|
| 1080p özdeş | 602 | 0 | 509 |
| 1080p crf 8 | 602 | 2 | 498 |
| 1080p crf 12 | 602 | 7 | 453 |
| 1080p tonemap yüksek | 3624 | 11 | 247 |
| 1080p tonemap düşük | 3624 | 0 | 0 |

Bütün etkiyi **toplanmış değere uygulanan ikinci kelepçe** yapıyordu.

**Ölçüm — A/B sonucunu nasıl bozuyordu.** İki gerçek yarışmacı, aynı referansa
karşı (320x240 crf 23 referans):

| Yarışmacı | ham ortalama | ham harmonik | kelepçeli ortalama | kelepçeli harmonik |
|---|---|---|---|---|
| A: özdeş kopya | 99,8712 | 99,8680 | 100,0000 | 100,0000 |
| B: crf 10 yeniden kodlama | 99,8392 | 99,8341 | 100,0000 | 100,0000 |
| **A − B** | **+0,0320** | **+0,0339** | **0,0000** | **0,0000** |

Hastalık **ölçek kayması değil, tavan çökmesi.** Kelepçe iki yarışmacıyı farklı
ölçeklendirmiyor; `[99,8; 100]` aralığının **tamamını tek bir noktaya indiriyor**.
O aralıkta duran her A/B karşılaştırması berabere raporlanıyordu — kayan değil,
silinen bir sonuç. Hedefin görsel kayıpsıza yakın olduğu durum tam olarak burası.

Orta kalitede etkisi ölçülebilir ama önemsizdi (1080p, aynı referansa karşı
crf 8 / crf 12): ham fark +0,3151, kelepçeli fark +0,3141 — 0,001 VMAF. Yani
sorun her yerde değil, yalnız tavanda; ama tavan da işin asıl bölgesi.

**Yapılan:** `NormalizeVmafCeiling` tamamen kaldırıldı. `VmafNegMean/Harmonic/P10/Min`
artık libvmaf'ın verdiği ham değerler. Özdeş kopya artık 100 değil, modelin o
içerikteki tavanını raporluyor — 1080p60 gerçek içerikte 99,68, 320x240
`testsrc2`'de 99,87. Kullanıcıya gösterilecek "kusursuz" rozeti istenirse sayıyı
bozarak değil ayrı bir alanla verilmeli.

Yan bulgu: özdeş 1080p içerikte kare bazlı **min 97,4256**, p10 97,9241. Yani
`VmafNegMin` özdeş dosyada bile 97,4 diyor — bu bir kalite işareti değil, modelin
kendi gürültüsü. §4'ün gerekçesi bu.


## 3. Tonemap'li referans yolu — **çağrılıyor ve duyarlı**

Yol gerçekten var: `MeasureTonemappedReferenceAsync`'in tek üretim çağıranı
`tools/VidShrink.Bench` (`bench measure-tonemapped`). Uygulama içinden çağıran
yok — HandBrake karşılaştırması bu kapıdan geçiyor.

Ne yapıyor: referansın `MediaInfo`'su SDR gibi yeniden etiketleniyor
(`IsHdr=false`, bt709/bt709/bt709/tv, yuv420p) ki karşılaştırılabilirlik kapısı
HDR/SDR ayrışmasında ölçümü reddetmesin; ardından referans zincirinin başına
`HdrResolver.TonemapFilter` ekleniyor. Böylece referans, çıktının üretildiği
tonemap'in aynısından geçiyor.

**Duyarlılık ölçümü.** Aynı HDR kaynaktan (`parca-1.mkv`, 60 sn) aynı tonemap
filtresiyle iki SDR çıktı; tek fark bit oranı. Ölçüm `bench measure-tonemapped`
ile, üretim kodunun kendi yolundan:

| | yüksek (`-crf 18`, 14 855 kb/s) | düşük (`-b:v 300k`, 202 kb/s) | fark |
|---|---|---|---|
| VMAF-NEG ortalama | **90,5957** | **25,1360** | 65,46 |
| VMAF-NEG harmonik | 86,3932 | 12,5446 | 73,85 |
| VMAF-NEG p10 | 88,2500 | 5,7337 | 82,52 |
| VMAF-NEG min | 0,7867 | 0,0000 | 0,79 |
| VMAF-NEG en kötü sahne (2 sn) | 85,9740 @ 50,0 sn | 5,8757 @ 12,0 sn | 80,10 |
| XPSNR | **40,3535** | **21,3199** | 19,03 |
| SSIM | 0,98478 | 0,825074 | 0,1597 |

Bit oranı yetmiş kat değişirken üç metrik de ayrışıyor.
`docs/olcumler/handbrake-acigi.md`'deki GEÇERSİZ tablodaki hastalık — XPSNR'ın
14,86 / 14,78 / 14,67'de çakılı kalması — **bu yolda yok**: XPSNR 40,35'ten
21,32'ye iniyor.

Dikkat çeken tek şey `VmafNegMin`: iyi çıktıda 0,79. 90,6 ortalamalı bir klipte
0,79 puanlı bir kare kalite olayı değil, sahne kesmesinde tek karelik bir
hizalanma/ani değişim artığı. Kullanılabilir bir taban değil (§4).

**Yan bulgu, düzeltilmedi.** `xpsnr` filtresi bu çiftte uyarı basıyor:
`not matching timebases found between first input: 1/15360 and second input
1/1000`. Her iki zincirin sonuna `settb=AVTB` eklenip ölçüm tekrarlandı; sonuç
kuruşu kuruşuna aynı çıktı (38,9113 / 42,7523 / 43,7238). Uyarı bu girdide
ölçüyü kaydırmıyor, o yüzden üretim zinciri değiştirilmedi. Farklı kare hızlı
çiftte tekrar bakılmalı — bu turda ölçülmedi.


## 4. Sahne tabanı — 2 saniyelik pencere

Sorun: filmin tamamındaki tek en kötü kare kullanıcıyı ilgilendirmiyor, hem de
ölçülemiyor. §2'nin yan bulgusu: özdeş 1080p içerikte min 97,43. Buna karşılık
gerçekten iyi bir kodlamada min 0,79 çıkabiliyor (§3). Aynı sayı hem özdeş
içerikte 97 hem iyi kodlamada 1 diyorsa taban olarak kullanılamaz.

Eklenen: kareler `2 sn × fps` uzunluğunda **ardışık, örtüşmeyen** pencerelere
bölünüyor, her pencerenin ortalaması alınıyor, en düşüğü ve başlangıç saniyesi
raporlanıyor (`VmafNegWorstScene`, `WorstSceneStartSeconds`,
`SceneWindowSeconds`). Başlangıç saniyesi referans zaman çizgisine göre;
pencereli ölçümde `referenceStartSeconds` ekleniyor.

**Pencere uzunluğu seçimi.** Altı uzunluk denendi. Ölçüt: pencere, metriğin kendi
gürültüsünden büyük bir sinyal vermeli. Gürültü = bit düzeyinde özdeş 1080p60
klipte en kötü pencerenin klip ortalamasının (99,677) altına düşme miktarı.
Sinyal = iki gerçek yarışmacının (aynı referansa karşı crf 8 ve crf 12) en kötü
pencereleri arasındaki fark.

| Pencere | özdeş klipte en kötü | gürültü (99,677 − ) | crf8 − crf12 sinyali | sinyal > gürültü |
|---|---|---|---|---|
| 0,5 sn | 97,914 | 1,763 | 1,384 | hayır |
| 1 sn | 98,338 | 1,339 | 1,089 | hayır |
| **2 sn** | **98,987** | **0,690** | **0,715** | **evet** |
| 3 sn | 99,130 | 0,547 | 0,606 | evet |
| 5 sn | 99,360 | 0,317 | 0,516 | evet |
| 10 sn | 99,676 | 0,001 | 0,316 | evet ama yozlaşmış |

2 sn, sinyalin gürültüyü geçtiği **en kısa** uzunluk. Daha kısası tek karelik
model gürültüsüne teslim oluyor: 0,5 sn'de özdeş klip 97,91'e düşüyor, ki bu
kullanılamaz min'in (97,43) neredeyse aynısı. Daha uzunu yerel çöküşü seyreltiyor;
10 sn'lik klipte 10 sn'lik pencere klip ortalamasının kendisi oluyor (99,676 vs
99,677) ve ayrımı 0,316'ya, yani ortalamalar farkının aynısına indiriyor — sahne
tabanı olmaktan çıkıyor.

Seçilen uzunlukta taban, ortalamanın gizlediğini gösteriyor: tonemap yüksek
çıktıda ortalama 90,60 iken en kötü 2 sn penceresi 85,97 (50. saniye); düşük
çıktıda ortalama 25,14 iken en kötü pencere 5,88 (12. saniye). Ayrım 80,10 —
ortalamalar farkının (65,46) belirgin üstünde.

`WindowQualityMeasurement`'a dört alan **sona, varsayılan değerle** eklendi
(`VmafNegMin`, `VmafNegWorstScene`, `WorstSceneStartSeconds`,
`SceneWindowSeconds`). Var olan üye kaldırılmadı, adı değişmedi, sırası bozulmadı.


## 5. Kurulu metrik envanteri

`ffmpeg -filters`, karşılaştırma metrikleriyle sınırlı:

    TS identity          VV->V      Calculate the Identity between two video streams.
    .. libvmaf           VV->V      Calculate the VMAF between two video streams.
    TS msad              VV->V      Calculate the MSAD between two video streams.
    TS psnr              VV->V      Calculate the PSNR between two video streams.
    TS ssim              VV->V      Calculate the SSIM between two video streams.
    .. ssim360           VV->V      Calculate the SSIM between two 360 video streams.
    .. vmafmotion        V->V       Calculate the VMAF Motion score.
    T. xpsnr             VV->V      Calculate the extended perceptually weighted peak
                                    signal-to-noise ratio (XPSNR) between two video streams.

**SSIMULACRA2 ve butteraugli bu derlemede yok.** `libjxl` derlenmiş olsa da
karşılık gelen filtre listede geçmiyor. Kurulu olmadıkları için eklenmediler.

`ffmpeg -h filter=libvmaf`:

    libvmaf AVOptions:
       log_path          <string>     ..FV....... Set the file path to be used to write log.
       log_fmt           <string>     ..FV....... Set the format of the log (csv, json, xml, or sub). (default "xml")
       pool              <string>     ..FV....... Set the pool method to be used for computing vmaf.
       n_threads         <int>        ..FV....... Set number of threads to be used when computing vmaf. (default 0)
       n_subsample       <int>        ..FV....... Set interval for frame subsampling used when computing vmaf. (default 1)
       model             <string>     ..FV....... Set the model to be used for computing vmaf. (default "version=vmaf_v0.6.1")
       feature           <string>     ..FV....... Set the feature to be used for computing vmaf.

    framesync AVOptions:
       eof_action        <int>        (default repeat)   repeat / endall / pass
       shortest          <boolean>    (default false)
       repeatlast        <boolean>    (default true)
       ts_sync_mode      <int>        (default default)  default / nearest

Model yoklaması — aynı 320x240 klip kendisiyle karşılaştırılarak:

| `model=version=` | sonuç |
|---|---|
| `vmaf_v0.6.1` | 99,742838 |
| `vmaf_v0.6.1neg` | 99,742505 — **kullanılan model** |
| `vmaf_4k` | `Error initializing filters` — yok |
| `vmaf_4k_v0.6.1` | 100,000000 |
| `vmaf_float_v0.6.1` | 99,742788 |
| `vmaf_b_v0.6.3` | `Error initializing filters` — yok |

`vmaf_4k` adı kurulu değil; 4K modeli `vmaf_4k_v0.6.1` adıyla var. Bu turda
kullanılmadı: kaynaklar 1080p ve altında, 4K modeli o çözünürlükte fazla iyimser.
`n_subsample` var ve maliyeti düşürebilir; sayıları değiştirdiği için bu turda
açılmadı.

Değer katan ve kurulu olduğu halde eklenmeyen: `psnr` (XPSNR zaten onun
algısal ağırlıklı hali), `identity` ve `msad` (algısal değil), `vmafmotion`
(kalite değil hareket ölçüsü), `ssim360` (360 içerik yok).


## 6. Mutasyon kanıtı

`dotnet test -c Release --filter "QualityMeterTests"` — 11 ölçü, tamamı geçiyor,
atlanan yok. ffmpeg gerektirenler `[FfmpegFact]`, tonemap zinciri gerektiren
`[TonemapFact]`. Ölçünün içinde yetenek yoklayıp sessizce dönen kol yok.

Üç mutasyon tek tek uygulandı, kaynak her seferinde geri alındı:

| Mutasyon | Düşen ölçü |
|---|---|
| `NormalizeVmafCeiling` geri kondu (toplanmış dörtlüye) | `IdenticalClipReportsTheModelCeilingInsteadOfAForcedHundred`, `TwoNearLosslessRivalsKeepTheirOrderAboveTheCeilingBand` |
| Pencere adımı `scores.Count` yapıldı (tek pencere = tüm klip) | `WorstSceneAveragesOverTwoSecondBuckets`, `WorstSceneReportsTheWindowStartOnTheReferenceTimeline`, `WorstSceneFindsTheDamagedSectionTheMeanHides` |
| Tonemap öneki düşürüldü (`referencePrefix = ""`) | `TonemappedReferenceSeparatesTwoSdrQualities` |

`WorstSceneAveragesOverTwoSecondBuckets` pencere uzunluğunu tek başına
sabitliyor: 600 kare @ 60 fps, yalnız 2,0–3,0 sn arası sıfır. 2 sn'lik pencere 50
verir, 1 sn'lik 0, 5 sn'lik 80. Ölçü 50 bekliyor.


## Yeniden üretim

    ffmpeg -ss 00:02:00 -t 60 -i kaynak-1080p60-hdr-17dk.mp4 -map 0:v:0 -c copy parca-1.mkv

    TM=zscale=t=linear:npl=100,tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p
    ffmpeg -i parca-1.mkv -vf "$TM" -c:v libx264 -preset veryfast -crf 18 -an sdr-yuksek.mp4
    ffmpeg -i parca-1.mkv -vf "$TM" -c:v libx264 -preset veryfast -b:v 300k -maxrate 400k -bufsize 800k -an sdr-dusuk.mp4

    dotnet run -c Release --project tools/VidShrink.Bench -- measure-tonemapped parca-1.mkv sdr-yuksek.mp4
    dotnet run -c Release --project tools/VidShrink.Bench -- measure-tonemapped parca-1.mkv sdr-dusuk.mp4

§2 ve §4 tabloları kare başına JSON günlüğünden çıkarıldı; günlüğü doğrudan almak
için filtreye `libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=...`
verilir. `bench measure-tonemapped` çıktısı bu çözümlemeyle birebir uyuştu
(ortalama 90,59568; XPSNR 40,35355) — çözümleme üretim yolunun kendisini ölçüyor.
