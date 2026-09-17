# HandBrake A1 — Filtre Zinciri Ölçümü (B9)

Düzenek: `tools/kalite-paketi-3/hb.ps1 -Is filtre`, iş akışı `handbrake-kiyas.yml` (`isler=filtre`).
Yalnız GitHub Actions koşucusunda çalışır; yerelde kodlama yok.

## Kural (Ölçümden Önce Yazıldı, Sonra Gevşetilmez)

Girdi: `karanlik`, `parlak`, `hareketli` kesitleri (Sintel, 10 sn, ffv1). Her kesit önce
`libx264 -crf 4 -preset veryfast` ile h264 ara dosyaya çevrilir; kaynak ve VMAF referansı bu dosyadır.
Ürün yolu `bench shrink <ara> <2000 kbit karşılığı MB> --speed quality --no-resolution-drop --no-fps-drop --no-measure`.

**Progressive kesitler.** Üç kol, iki tekrar, sıra dönüşümlü (1. tekrar kapali → otomatik → acik,
2. tekrar acik → otomatik → kapali):

- `kapali`: `--filters deinterlace=off`
- `otomatik`: filtre bayrağı yok (varsayılan zincir; yoklama gerekiyorsa idet çalışır)
- `acik`: `--filters deinterlace=on` (idet+bwdif zinciri progressive içerikte çalışır)

Hüküm, kesit başına, `otomatik` ve `acik` kollarının her biri için `kapali`ya karşı:

1. |ΔVMAF-NEG ortalama| < **0,1** (iki tekrarın ortalaması)
2. Süre: kol başına iki tekrarın en küçük **deneme başına kodlama süresi**
   (`kodlama_sn / deneme`); (kol − kapali) / kapali < **%5**

Eşik %5 ölçümden önce yazıldığı gibi duruyor; değişen yalnız neyin ölçüldüğü. İlk koşumda
ölçüt `toplam_sn`'in en küçüğüydü ve filtreyi ölçmüyordu: `toplam_sn`'in yaklaşık %45'i
karmaşıklık yoklaması ve ölçüm, üstüne bütçe döngüsünün deneme sayısı kollar arasında
±1 oynuyor ve tek bir fazladan deneme toplamı ~%35 büyütüyor. Yani %5'lik eşiği filtre
değil deneme sayısı belirliyordu. Düzeltilen ölçüt deneme sayısından bağımsızdır; satırda
`deneme` ve `kodlama_sn` alanları zaten vardı, hüküm satırına da kol başına deneme sayısı
(`<kol>_deneme`) ve `<kol>_kodlama_sn_deneme_basi` yazılıyor.

**Taramalı sentetik negatif kontrol.** `hareketli` kesitinden `tinterlace=mode=interleave_top,setfield=tff`
ile taramalı ara dosya (`-flags +ilme+ildct`; ffmpeg 8+ `-top` seçeneğini reddettiği için ilk koşum
35248878850'de düşürüldü; taramalı 4:2:0 x264 yüksekliğin 4'e bölünmesini istediği için 35252046279'da 1920x818
kodlanamadı, ara dosya ve referans `crop=iw:trunc(ih/4)*4:0:0` ile 1920x816'ya kırpılır), referans aynı kesitin çift
kareleri (`select=not(mod(n\,2))`). Kollar `kapali` ve `otomatik`, birer tekrar. Hüküm:

1. ffprobe `field_order` progressive **değil** (satırda `alan_progressive_degil`)
2. `otomatik` komutunda `bwdif` var, `kapali` komutunda yok
3. VMAF-NEG ortalama: otomatik − kapali ≥ **1,0**

**Belirsiz `field_order` kolu (idet yoklamasının kendisi).** Yukarıdaki taramalı kol
`field_order=tb` döndürdüğü için üründe `IsInterlaced=true` olur ve idet yoklaması hiç
koşmaz: `NeedsInterlaceProbe` yalnız alan sırası **belirsizken** (`null` ya da `unknown`)
ve kodlayıcı h264/mpeg2video/dvvideo iken doğrudur. Bu yüzden ayrı bir kesit: aynı
`hareketli` kaynağından 720x576 PAL, `tinterlace=mode=interleave_top,setfield=tff`,
`-c:v dvvideo` ile DV ara dosya — ffprobe `field_order=unknown` döner, gerçek dünyada
DV kaset sayımlarının hali. Referans yine çift kareler. Kollar `kapali` ve `otomatik`,
birer tekrar. Hüküm:

1. ffprobe `field_order` boş ya da `unknown` (satırda `alan_belirsiz`)
2. `otomatik` kolunun günlüğündeki `yoklama:` satırı `idet=kostu` diyor (satırda `idet_kostu`)
3. `otomatik` komutunda `bwdif` var, `kapali` komutunda yok
4. VMAF-NEG ortalama: otomatik − kapali ≥ **1,0**

Kural sayıları bu commit'te sabitlenmiştir; sonuç tablosu aşağıya ölçümden sonra eklenir.

## Deneme Sayıları (Koşum 35254572887, Ham Veri)

Bütçe döngüsünün deneme sayısı kollar arasında eşit değil; süre kıyasının neden deneme
sayısına oturtulması gerektiği buradan okunuyor:

| kesit | kol | tekrar | deneme | kodlama_sn | toplam_sn | kodlama_sn/deneme |
| --- | --- | --- | --- | --- | --- | --- |
| karanlik | kapali | 1 | 2 | 99,1 | 151,7 | 49,55 |
| karanlik | kapali | 2 | 2 | 98,0 | 148,8 | 49,00 |
| karanlik | otomatik | 1 | 2 | 108,8 | 159,8 | 54,40 |
| karanlik | otomatik | 2 | 2 | 96,5 | 149,3 | 48,25 |
| karanlik | acik | 1 | 2 | 100,4 | 151,9 | 50,20 |
| karanlik | acik | 2 | 2 | 97,9 | 149,4 | 48,95 |
| parlak | kapali | 1 | **4** | 52,7 | 100,8 | 13,18 |
| parlak | kapali | 2 | **3** | 37,2 | 81,8 | 12,40 |
| parlak | otomatik | 1 | 3 | 36,7 | 82,9 | 12,23 |
| parlak | otomatik | 2 | 3 | 37,2 | 82,6 | 12,40 |
| parlak | acik | 1 | **4** | 56,6 | 102,6 | 14,15 |
| parlak | acik | 2 | **4** | 50,4 | 100,3 | 12,60 |
| hareketli | kapali | 1 | 2 | 50,1 | 93,3 | 25,05 |
| hareketli | kapali | 2 | 2 | 51,6 | 92,2 | 25,80 |
| hareketli | otomatik | 1 | 2 | 49,7 | 90,6 | 24,85 |
| hareketli | otomatik | 2 | 2 | 49,9 | 90,7 | 24,95 |
| hareketli | acik | 1 | 2 | 50,0 | 90,8 | 25,00 |
| hareketli | acik | 2 | 2 | 53,4 | 109,3 | 26,70 |
| hareketli | taramali-kapali | 1 | 2 | 28,0 | 60,4 | 14,00 |
| hareketli | taramali-otomatik | 1 | 2 | 25,5 | 57,8 | 12,75 |

`parlak` kesitinde `kapali` kolunun bir tekrarı 4, öteki 3 deneme; `acik` kolunun iki
tekrarı da 4 deneme. Eski ölçüt `kapali`nın 3 denemelik 81,8 sn'sini `acik`ın 4 denemelik
100,3 sn'siyle karşılaştırıyordu.

## Sonuç (Yeni Ölçüt, Koşum 35254572887 Ham Verisiyle Yeniden Hesaplandı)

Koşum 35254572887 (`handbrake-kiyas`, `isler=filtre`, `vt=false`), windows koşucusu, 2000 kbit.
Aynı ham veri, düzeltilmiş süre ölçütü (`kodlama_sn/deneme`):

| kesit | kol | ΔVMAF-NEG | kapali (sn/deneme) | kol (sn/deneme) | süre farkı | hüküm |
| --- | --- | --- | --- | --- | --- | --- |
| karanlik | otomatik | +0,0026 | 49,00 | 48,25 | −1,53 % | geçti |
| karanlik | acik | +0,0008 | 49,00 | 48,95 | −0,10 % | geçti |
| parlak | otomatik | −0,0124 | 12,40 | 12,23 | −1,34 % | geçti |
| parlak | acik | +0,0571 | 12,40 | 12,60 | **+1,61 %** | **geçti** |
| hareketli | otomatik | −0,0098 | 25,05 | 24,85 | −0,80 % | geçti |
| hareketli | acik | +0,0112 | 25,05 | 25,00 | −0,20 % | geçti |

Eski ölçütle kalan tek kol (`parlak` / `acik`, +%22,62) yeni ölçütle +%1,61 ile geçiyor.
Bir önceki sürümde bu satır "elle açılan `idet,bwdif` zinciri kodlamayı %22,6 yavaşlattı"
diye yazılmıştı; **ham veri bunu doğrulamıyor.** Aynı kesitte boş zincirli `kapali` kolu
4 denemede 100,8 sn, `idet,bwdif`li `acik` kolu 4 denemede 100,3 sn sürdü — yani eşit
deneme sayısında bwdif'li kol daha hızlı. Fark filtreden değil, fazladan bir bütçe
denemesinden geliyordu. Deneme başına kodlama süresinde bwdif'in maliyeti %2'nin
altındadır; beş kolun hiçbiri eşiği zorlamıyor.

Otomatik kip üç kesitte de geçti: bayraksız koşumda zincir boş kaldı (`deinterlace=Auto zincir=`),
kalite ve süre kapali koluyla aynı.

Taramalı sentetik negatif kontrol (`hareketli`, ffprobe `field_order=tb`):

| kol | zincir | VMAF-NEG ort | hüküm |
| --- | --- | --- | --- |
| taramali-kapali | yok | 23,87 | — |
| taramali-otomatik | `idet,bwdif=mode=send_frame:parity=auto:deint=interlaced` | 98,78 | geçti |

ΔVMAF-NEG = +74,92 (eşik 1,0); bwdif yalnız otomatik kolun komutunda. Taramalı kaynakta
zincirin kurulması ayrıca süreyi düşürdü (deneme başına 14,00 → 12,75 sn). Bu kolda
`field_order=tb` olduğu için ürün dosyayı doğrudan taramalı sayar; **idet yoklaması bu
kolda koşmaz**, kararı ffprobe verir. Yoklamanın kendisi aşağıdaki belirsiz alan kolunda
ölçülür.

## Belirsiz `field_order` Kolu (Koşum 35265321818, sha f38586f1)

DV ara dosya (720x576 PAL, `-c:v dvvideo`) ffprobe'ta `field_order=unknown` döndü, yani
üründe `IsInterlaced=false` ve alan sırası belirsiz — `NeedsInterlaceProbe`'un tam koşulu.
Yoklamanın koştuğu bench günlüğünün `yoklama:` satırından okunuyor:

```
belirsiz-kapali    yoklama: idet=kosmadi alan=- taramali=False
belirsiz-otomatik  yoklama: idet=kostu alan=- taramali=False tff=140 bff=0 progressive=110 belirsiz=0 toplam=250
```

| kol | zincir | deneme | VMAF-NEG ort | hüküm |
| --- | --- | --- | --- | --- |
| belirsiz-kapali | yok | 4 | 62,69 | — |
| belirsiz-otomatik | `idet,bwdif=mode=send_frame:parity=auto:deint=interlaced` | 2 | 90,80 | geçti |

ΔVMAF-NEG = +28,11 (eşik 1,0); `alan_belirsiz=true`, `idet_kostu=true`, bwdif yalnız
otomatik komutta. idet 250 karede 140 tff / 0 bff saydı: parite baskın (`IdetParityDominance=10`),
taramalı payı 140/250 = 0,56 > `IdetDominantShare=0,25`, karar `Deinterlace=On`.

**İlk koşumun hatası (35261244666) ve düzeltmesi.** Bu kol ilk kurulduğunda hüküm `kaldi`
çıktı: Δ yalnız +0,76 idi. Neden filtre değil, düzenekti — referans çift kareler kaynağın
kendi kare hızından türetiliyordu, DV ara dosya ise 25 fps PAL'e oturuyor; iki taraf
zamanda kaymış olduğu için VMAF ikisini de düşük puanlıyordu (bwdif'li 1,32 / bwdif'siz
2,08). `hb.ps1`'de DV ara dosya ve referans artık aynı `fps=50` tabanından kuruluyor
(commit `f38586f1`). Eşik gevşetilmedi, ölçüm düzeltildi; yerelde ≤3 sn'lik lavfi denemesi
düzeltmeden sonra bwdif'li 95,99 / bwdif'siz 66,34 verdi, CI koşumu da +28,11 ile aynı yöne
çıktı.

## Doğrulama Koşumu 35265321818 — Progressive Kollar

Aynı koşumda progressive kollar yeni ölçütle ikinci kez ölçüldü (kodlama_sn/deneme):

| kesit | kol | ΔVMAF-NEG | kapali (sn/deneme) | kol (sn/deneme) | süre farkı | hüküm |
| --- | --- | --- | --- | --- | --- | --- |
| karanlik | otomatik | +0,0029 | 66,75 | 66,65 | −0,15 % | geçti |
| karanlik | acik | +0,0015 | 66,75 | 66,85 | +0,15 % | geçti |
| parlak | otomatik | +0,0752 | 21,80 | 22,52 | +3,33 % | geçti |
| parlak | acik | +0,0459 | 21,80 | 22,90 | **+5,05 %** | **kaldı** |
| hareketli | otomatik | +0,0117 | 18,55 | 18,50 | −0,27 % | geçti |
| hareketli | acik | +0,0196 | 18,55 | 18,60 | +0,27 % | geçti |
| hareketli | taramali-otomatik | +74,88 | — | — | — | geçti |
| hareketli | belirsiz-otomatik | +28,11 | — | — | — | geçti |

`parlak` / `acik` kolu eşiğin 0,05 puan üstünde kaldı ve **kural gevşetilmedi.** Aynı kol
35254572887 ham verisinde +1,61 %, bu koşumda +5,05 %; ikisinde de sorun ölçütün kendisi
değil `parlak` kesitinin bütçe döngüsü — bu kesit tek kesit olarak deneme sayısını 3 ile 4
arasında oynatıyor ve "kol başına iki tekrarın en küçüğü" kuralı `kapali` için 3 denemelik
tekrarı (21,80), `acik` için 4 denemelik tekrarı (22,90) seçiyor. Deneme başına süre
karşılaştırması bunu tamamen sıfırlamıyor: 3 denemeli koşum bütçe döngüsünün ısınma
payını daha az taşıyor. Tek koldaki bu kalan açık borç olarak duruyor; çözümü ya kesit
başına deneme sayısını sabitlemek (bench'te `--no-budget-fill`) ya da tekrar sayısını
artırıp medyan almak — ikisi de ölçüm düzeneği değişikliği, kural değişikliği değil.

## Mutasyonla Kırmızıya Dönme Kaydı

Düzenek `.calisma/a1/mutasyon.py`: her mutant kaynağa tek metin değişikliği olarak uygulanır,
test projesi yeniden derlenir (artımlı derlemenin mutasyonu taşımaması riski için derleme
ayrı komut), sonra dokunulan sınıfların filtresi koşulur
(`VideoFilterChainTests|PlanCalculatorTests|OluUyeTests|FfmpegArgumentsTests|EncodeRunnerTests|ManualOverrideTests`),
ardından dosya geri yazılır. 37 mutant koşuldu, **37'si kırmızı** (ilk turda 34 kırmızı,
2 hayatta kalan iki yeni savla kapatıldı, 1 mutantın deseni düzeltildi ve o da kırmızı):

| # | mutasyon | kırmızıya dönen test |
| --- | --- | --- |
| M1 | `bwdif` `mode=send_frame` → `send_field` | TaramaliKaynakOtomatikteIdetBwdifAlirProgressiveAlmaz, TaramaliKaynakOtomatiktePassthroughaDusmezProgressiveDuser |
| M2 | `parity=auto` → `parity=tff` | aynı iki test |
| M3 | `deint=interlaced` → `deint=all` | aynı iki test |
| M4 | zincirden `idet,` öneki düşürüldü | aynı iki test |
| M5 | `IdetDominantShare` 0,25 → 0,0 | IdetKarariParitesiBaskinTaramaliyiSecerKarisikParityiReddeder, YoklamaSonucuOtomatigiAcikYaDaKapaliyaCevirirElleSecimeDokunmaz |
| M6 | `IdetDominantShare` 0,25 → 0,5 | IdetKarariParitesiBaskinTaramaliyiSecerKarisikParityiReddeder |
| M7 | `IdetParityDominance` 10 → 1 | IdetKarariParitesiBaskinTaramaliyiSecerKarisikParityiReddeder |
| M8 | `IdetParityDominance` 10 → 100 | IdetKarariParitesiBaskinTaramaliyiSecerKarisikParityiReddeder |
| M9 | `NeedsInterlaceProbe`ta `Deinterlace == Auto` koşulu düşürüldü | IdetYoklamasiYalnizBelirsizAlanVeTaramaliKodeklerdeIstenir |
| M10 | `!Detelecine` koşulu düşürüldü | IdetYoklamasiYalnizBelirsizAlanVeTaramaliKodeklerdeIstenir |
| M11 | `!info.IsInterlaced` koşulu düşürüldü | IdetYoklamasiYalnizBelirsizAlanVeTaramaliKodeklerdeIstenir |
| M12 | `!FieldOrderSaysProgressive` koşulu düşürüldü | IdetYoklamasiYalnizBelirsizAlanVeTaramaliKodeklerdeIstenir, OluOzellikYuzeyiPimlenenKume |
| M13 | yoklama kodek listesinden `dvvideo` çıkarıldı | IdetYoklamasiYalnizBelirsizAlanVeTaramaliKodeklerdeIstenir |
| M14 | `FieldOrderSaysProgressive` `"progressive"` → `"unknown"` | IdetYoklamasiYalnizBelirsizAlanVeTaramaliKodeklerdeIstenir |
| M15 | `ResolveInterlace` idet kararını yok sayıp hep `Off` | YoklamaSonucuOtomatigiAcikYaDaKapaliyaCevirirElleSecimeDokunmaz |
| M16 | `Deinterlaces` varsayılan kolu `info.IsInterlaced` → `false` | TaramaliKaynakOtomatikteIdetBwdifAlirProgressiveAlmaz, TaramaliKaynakOtomatiktePassthroughaDusmezProgressiveDuser |
| M17 | `ChangesPictureFor`dan `Deinterlaces` çağrısı düşürüldü | TaramaliKaynakOtomatiktePassthroughaDusmezProgressiveDuser |
| M18 | `deblock=filter=strong` → `weak` | DeblockVeDebandTekFiltreYazarKapalidaYazmaz, TumFiltrelerSabitSiradaDizilirFpsEnSonda |
| M19 | `hue=s=0` → `hue=s=1` | FfmpegArgumanlariZinciriVfIcindeVeRenkEtiketiniTasir, PadVeGriSonaYazilir, TumFiltrelerSabitSiradaDizilirFpsEnSonda |
| M20 | `deband` → `deband=1thr=0.02` | DeblockVeDebandTekFiltreYazarKapalidaYazmaz, TumFiltrelerSabitSiradaDizilirFpsEnSonda |
| M21 | `hqdn3d=3:2:2:3` → `3:2:2:4` | GurultuAzaltmaSecilenFiltreVeGucuYazar, TumFiltrelerSabitSiradaDizilirFpsEnSonda |
| M22 | `nlmeans=s=2.0` → `s=3.0` | GurultuAzaltmaSecilenFiltreVeGucuYazar |
| M23 | zincirden deinterlace adımı tümüyle çıkarıldı | TaramaliKaynakOtomatikteIdetBwdifAlirProgressiveAlmaz, TaramaliKaynakOtomatiktePassthroughaDusmezProgressiveDuser, TumFiltrelerSabitSiradaDizilirFpsEnSonda |
| M24 | zincirden `crop` adımı çıkarıldı | KirpmaDikdortgeniYazilirVeOlceklemeKirpikBoyuttanHesaplanir, PlanKirpmaOnerisiniTasirAmaKirpmayiUygulamaz, TumFiltrelerSabitSiradaDizilirFpsEnSonda |
| M25 | zincirden `deblock` adımı çıkarıldı | DeblockVeDebandTekFiltreYazarKapalidaYazmaz, OluOzellikYuzeyiPimlenenKume, TumFiltrelerSabitSiradaDizilirFpsEnSonda |
| M26 | `ChangesPicture`tan `Crop is not null` düşürüldü | AcikFiltrePassthroughuEngeller (ilk turda **hayatta kaldı**, kırpma-only passthrough savı eklendi) |
| M27 | `WithCrop` kimlik işlevine çevrildi | PlanKirpmaOnerisiniTasirAmaKirpmayiUygulamaz |
| M28 | `DetelecineFpsFactor` 4/5 → 1 | DetelecineZinciriFpsyiBesteDordeIndirirVeDeinterlaceYerineGecer |
| M29 | `Validate`ta kırpma boyutunun çift olma denetimi düşürüldü | GecersizKirpmaVePadBildirilir |
| M30 | `CropProbe.SamplePoints` 10 → 3 | KirpmaYoklamasiSinirYirmiDortVeOnNoktayaYayilir |
| M31 | `CropProbe.Limit` 24 → 16 | KirpmaYoklamasiSinirYirmiDortVeOnNoktayaYayilir |
| M32 | `CropProbe.FramesPerPoint` 2 → 1 | KirpmaYoklamasiSinirYirmiDortVeOnNoktayaYayilir (ilk turda **hayatta kaldı**, argüman savı eklendi) |
| M33 | `cropdetect` `round=2` → `round=16` | KirpmaYoklamasiSinirYirmiDortVeOnNoktayaYayilir |
| M34 | mod kararı `OrderByDescending` → `OrderBy` (en seyrek örnek) | KirpmaKarariOrneklerinModunuAlirBirlesimiDegil |
| M35 | mod kaynak çerçeveye eşitse `null` dönme kuralı düşürüldü | KirpmaKarariOrneklerinModunuAlirBirlesimiDegil |
| M36 | passthrough kapısındaki `ChangesPictureFor(info)` → `false` | AcikFiltrePassthroughuEngeller, TaramaliKaynakOtomatiktePassthroughaDusmezProgressiveDuser |
| M37 | `OzellikScan` yalnız `VidShrink.Core`a daraltıldı | K8OluYuzeyiGerekceliBorcOlarakDuruyor, OluOzellikYuzeyiPimlenenKume |

Hayatta kalan mutant yok. İlk turun ham çıktısı ve JSON kaydı `.calisma/a1/` altındaydı,
iş bitince silindi; tablo o çıktıdan aktarıldı.

## K8 (cropdetect): Motor Hazır, Kullanıcı Yolu C1'de

`CropProbe` (limit=24, 10 nokta, mod kararı), `EncodePlan.SuggestedCrop` ve
`PlanOptions.DetectedCrop` çalışıyor ve testli; **ama üretimde tüketicisi yok.** Kırpma
varsayılan kapalı, yoklama sonucu yalnız öneri olarak plana yazılıyor; öneriyi kullanıcıya
gösteren ve tek tıkla uygulayan yüzey C1'in (arayüz) işi. Bu yüzden K8 bu turda
**kullanıcıya teslim edilmedi**: motor hazır, kullanıcı yolu C1'de.

Bu ölü yüzey artık ölçülüyor: `OluUyeTests` içindeki `OzellikScan` taraması `VidShrink.Core`
ve `VidShrink.Ffmpeg` ad alanlarının public özelliklerini de sayıyor (912 özellik, bugün
152'si işaretli) ve K8'e ait altı satır gerekçeli borç olarak pimlendi
(`EncodePlan.SuggestedCrop`, `CropDetection.Rect`, `CropDetection.Samples`,
`IdetCounts.Progressive`, `IdetCounts.Undetermined`, `VideoFilterOptions.ChangesPicture`).
