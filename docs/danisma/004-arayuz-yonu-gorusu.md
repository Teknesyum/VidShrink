# Arayuz yonu gorusu

- soran: T0
- danisilan: fable
- tarih: 2026-09-04

## Sorulan

C:\Users\Teknesyum\.claude\plugins\cache\teknesyum\teknesyum-core\0.15.0\roles\advisor.md dosyasini oku ve onu uygula.
Soran opus kosuyor. Turkce yaz.

Proje koku: C:\Users\Teknesyum\Desktop\Projeler\VidShrink

## Soru

VidShrink bir video sikistirma araci (.NET 8 + Avalonia + ffmpeg). Kullanici arayuz icin
su iki seyi istedi, kendi cumlesiyle:

1. *"arayuzu eksiksiz hallet ... suanki halindeki kadar basit ayarlar degil daha kompleks
   kullanici kodegine kadar secebilmeli varsayilan oto mod tabi ama yinede bilincli bir
   kullanici ne isterse onun ciktisini alir"*
2. *"onizleme bolumu fazla buyuk onun alanini asagidaki ozellikleri ve nedenlerini sunan
   pencere alsin biraz"*

Iki dugumde gorus istiyorum:

**A. Hangi ayarlar acilmali?** Kodek kesin aciliyor (ayri bir sozlesme onu yaziyor).
Bunun disinda "bilincli kullanici" icin hangi dugmeler acilmali, hangileri otomatik
kalmali? Acilan her dugme motorun kendi kararini gecersiz kilar ve motor bu depoda
olcumle kalibre edildi — yanlis dugmeyi acmak kullaniciyi kendi aracina karsi calistirir.

**B. Onizleme ile "nedenler" paneli arasindaki yer paylasimi.** Kullanici onizlemeyi
kucultup nedenler panelini buyutmek istiyor, "biraz" diyor. Bu bir olcu karari;
kullaniciya sormadan uydurmak bu depoda yasak. Sana sordugum sey olcunun kendisi degil:
**bu yer paylasimi sabit bir orana mi baglanmali, kullanicinin surukleyebilecegi bir
ayirici mi olmali, yoksa panel icerige gore mi buyumeli** — ve neden.

## Kanit — hepsi olculmus durum, iddia degil

Orta sutunun bugunku duzeni (`src/VidShrink.App/MainWindow.axaml:478`):

```xml
<Grid Grid.Column="1" RowDefinitions="*,Auto" RowSpacing="...">
  <playback:ComparisonPanel Grid.Row="0" x:Name="Preview" VerticalAlignment="Stretch"/>
  <Border x:Name="PlanPanel" Grid.Row="1"
          MinHeight="{StaticResource PlanPanelMinHeight}"
          MaxHeight="{StaticResource PlanPanelMaxHeight}">
```

Belirtecler (`src/VidShrink.App/Themes/Theme.axaml:385`):
```
PlanPanelMinHeight  320
PlanPanelMaxHeight  512
```

Pencere: `Width="1560" Height="1060" MinWidth="1040" MinHeight="720"`. Sayfa uc esit
sutun: `ColumnDefinitions="*,*,*"`. Sol sutun kaynak + hedef, orta sutun onizleme + plan
paneli, sag sutun kalan.

Dosyadaki kendi yorumu, bugunku paylasimin **bilerek** boyle oldugunu soyluyor:

> T54/K4: artan yer onizlemeye gider. Onizleme esneyen satirdadir, plan paneli Auto
> satirda — istedigi kadarini alir, kalanini onizlemeye birakir. Eski duzende esneyen
> satir MaxHeight'li plan panelindeydi ve artan yer kimseye gitmiyordu.

Yani kullanicinin simdi geri istedigi sey, gecmiste bir sozlesmenin bilerek verdigi karar.

Plan panelinin bugun tasidigi icerik (`MainWindow.axaml:482-510`): baslik, `PlanFacts`
izgarasi, bir ayirici, `PlanReasonsHead` basligi ve nedenler listesi.

Nedenler zaten yapisal olarak var, serbest metin degil — `src/VidShrink.Core/EncodePlan.cs`:

```
enum ReasonCode {
  ResolutionScaled, FrameRateReduced, ResolutionRestoredAtCeiling, BudgetExceedsCeiling,
  BudgetBelowCeilingTwoPass, PredictedQualityMeasured, PredictedQualityEstimated,
  RetryScaled, EncoderFallback, HdrTonemapped, FillCrfLowered, FillTwoPassBandCenter,
  FillTwoPassBandTooNarrowForCrf, HardwareBitrateBias, SourceAlreadyUnderTarget,
  TargetCappedToSource }
record ReasonNote(ReasonCode Code, int Width, int Height, double Fps, double ScalePercent,
  double Crf, double BudgetCrf, double Mb, double TargetMb, double AudioMb, ...)
```

Ayrica bir tavsiye listesi (`src/VidShrink.Core/CompressionStrategy.cs`):

```
enum AdviceCode {
  BudgetIsGenerous, QualityCeilingReached, CodecUpgradeRecommended,
  HardwareCodecCostsQuality, ResolutionReduced, FrameRateReduced, AudioReduced,
  AudioMono, AudioDropped, TargetEnforcedTwoPass, ExtremeRatioWarning, ContentIsSimple,
  ContentIsComplex, ScaleSavesLittle, ScaleSavesMuch, TargetBelowCodecFloor,
  FrameRateCutForFloor, MotionCutIsCheap, MotionCutIsExpensive, EncoderFallback,
  HdrTonemapped }
```

Bugun kullaniciya acik olan tum ayarlar (`MainWindow.axaml`):

```
SliderTarget          hedef boyut MB, 1-500
SliderQualityTarget   hedef kalite 1-100
CmbIntent             Archive / Sharing / SocialMedia
CmbCodec              Otomatik / Uyumlu / En kucuk        <- niyet soyluyor, kodek degil
ChkResolution         cozunurluk dusurulebilir mi
ChkFps                kare hizi dusurulebilir mi
ChkFastGpu            hizli GPU yolu (bugun IsEnabled=False)
CmbFillPolicy         hedefi doldur / tavana kadar
CmbHdrPolicy          HDR koru / SDR'a esle   (yalniz HDR kaynakta gorunur)
```

Motorun kendi karar verdigi, bugun kullaniciya **hic** acilmayan seyler
(`src/VidShrink.Core/PlanCalculator.cs`, `CodecModel.cs`, `CompressionStrategy.cs`):

```
EncodeMode        Crf / TwoPass / PassThrough      — rejime ve banda gore secilir
CRF degeri        butceden hesaplanir
cozunurluk olcegi RegimeFloors: Aggressive min %20/180p, Extreme min %12/120p
kare hizi tabani  Aggressive 10 fps, Extreme 6 fps
ses butcesi payi  rejime gore %30 / %25 / %18 / %12
ses kanal sayisi  PickAudio icinde dusuruluyor (stereo -> mono -> yok)
preset / speed    SpeedMode Quality|Fast
donanim/yazilim   yoklamayla secilir, EncoderFallbackCause ile gerekcelendirilir
FillBand          hedefin %92-100 / %95-100 / %97,2-100 bandi, hedef boyutuna gore
```

Bu depodaki ilgili kurallar (`DEVIR.md §6`, `AGENTS.md`):

- Renk ve olcu yalniz `Themes/Theme.axaml` belirteclerinden gelir; uydurma yasak.
- `teknesyum-ui` arayuz standardi bu makinede **kurulu degil**.
- Kod yorumu yazilmaz.
- Boyut paritesi ±%2; bandin disindaki satir kiyaslanabilir degil.

## Ne istiyorum

advisor.md'nin uc basligi, en fazla 20 satir. Plan yazma, kod yazma, dosya olusturma —
gorusun mesajla donsun. A ve B dugumlerinin ikisine de deg; birini secip otekini atlama.

## Donen

## Call
A: Aç — kodek, EncodeMode (Auto/CRF/İki geçiş), CRF elle (yalnız CRF modunda, hedef boyutu bilgilendirici yapar), preset/speed, ses hedefi (kbps + kanal: Auto/stereo/mono/yok), çözünürlük ve kare hızı için "en az" tabanı (onay kutusu yerine değer), donanım/yazılım zorlama. Kapalı tut — FillBand, RegimeFloors, ses bütçe payı, EncoderFallback mantığı, retry. Her açılan düğme "Otomatik" seçenekli ve motorun seçtiği değeri yanında gösteren bir alan olsun; plan paneli de o düğmeye tek bir yeni ReasonCode (`UserOverride`) ile gerekçe yazsın.
B: Sürüklenebilir GridSplitter, belirtecli MinHeight/MaxHeight sınırları içinde, konumu ayar olarak kalıcı. Sabit oran değil, içerige göre büyüme hiç değil.

## Why
Açılacak düğmeler ffmpeg'in kendi kavramlarıdır (kodek, CRF, preset, ses kbps); bilinçli kullanıcı bunları başka araçtan bilir ve motor bunları girdi olarak alıp kalanı hâlâ hesaplayabilir. Kapalı kalanlar ise ölçümle kalibre edilmiş iç sabitler; onları açmak kullanıcıya bir anlam vermeden pariteyi bozar.
B'de "biraz" sözcüğü sabit oranın olmadığını söylüyor: kullanıcı ölçüyü bilmiyor, elle bulacak. İçerige göre büyüme nedenler listesiyle her planda zıplar, önizleme titrer; T54/K4 kararı da tam bu yüzden alınmıştı. Ayırıcı her iki kararı da geçerli tutar: varsayılan konum bugünkü paylaşım, kullanıcının çektiği yer onun kararı.

## Missed
- CRF elle girildiğinde hedef boyut kaydırıcısı ile çelişir; hangisinin kazandığı arayüzde tek satırla görünmeli (ör. hedef alanı soluk, "CRF sabit, boyut tahmini X MB").
- Gelişmiş ayarlar ayrı bir "Gelişmiş" katlanır bölümde olmalı; açıldığında sol sütun uzar ve orta sütunun dikey bütçesini dolaylı etkiler — B kararı A'dan bağımsız değil.
- Kullanıcının asıl şikayeti nedenlerin sığmaması olabilir: ReasonCode + AdviceCode toplam 37 kalem, 512 px'e sığmaz. Ayırıcıdan önce listenin kendi içinde kaydırılabilir olup olmadığını ve satır yüksekliğini ölç; belki panel büyümeden sorun çözülür.
- GridSplitter konumu ölçü belirteci değil kullanıcı verisi; Theme.axaml'a değil ayar dosyasına gider, "uydurma yasak" kuralına takılmaz.
