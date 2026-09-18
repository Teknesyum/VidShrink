# Kanit Klasoru Kapanisi: Arayuz Obegi (3. Obek)

Dal `t0/kanit-arayuz`. Kural: kanit dosyasini silen cagri **son asertten sonra** durur.
Yesil kosum kendi biraktigini siler; kirmizi kosum kanitini korur, cunku dusen asert o satira gelmez.
Klasor bosalinca o da gider.

Ortam: `VIDSHRINK_LIBMPV=C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\libmpv\libmpv-2.dll`

## Derleme

```
dotnet build VidShrink.sln -c Release -warnaserror -m:2
...
Olusturma basarili oldu.
    0 Uyari
    0 Hata
```

## 1. Kosum: yesil (13 sinifin filtresi)

Filtre:

```
FullyQualifiedName~VidShrink.Tests.AltyaziIndirmeTests|FullyQualifiedName~VidShrink.Tests.AltyaziOturumTests|FullyQualifiedName~VidShrink.Tests.WindowLayoutTests|FullyQualifiedName~VidShrink.Tests.QualityTargetUiTests|FullyQualifiedName~VidShrink.Tests.QualityTargetTests|FullyQualifiedName~VidShrink.Tests.PerformanceCheckTests|FullyQualifiedName~VidShrink.Tests.PaletteApplyTests|FullyQualifiedName~VidShrink.Tests.UstSeritTikTests|FullyQualifiedName~VidShrink.Tests.MiniKipOlcusuTests|FullyQualifiedName~VidShrink.Tests.TestAyarYoluTests|FullyQualifiedName~VidShrink.Tests.BiciminTests|FullyQualifiedName~VidShrink.Tests.FiltreYoklamaTests|FullyQualifiedName~VidShrink.Tests.StreamMappingTests
```

Ham cikti:

```
C:\Users\Administrator\Desktop\Projeler\VidShrink\.claude\worktrees\agent-af267e8dabd2b1c86\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)

Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.

Başarılı!  - Başarısız:     0, Başarılı:   239, Atlanan:     0, Toplam:   239, Süre: 4 m 2 s - VidShrink.Tests.dll (net8.0)
```

--- klasor: yesil kosumdan sonra

```
.calisma\p28-altyazi yok
.calisma\s20     yok
.calisma\t61     yok
.calisma\t57     yok
.calisma\t63     yok
.calisma\tema    yok
.calisma\serit-tik yok
.calisma\mini-olcu yok
.calisma\ayar-yolu yok
.calisma\a1\filtre yok
.calisma\a1      yok
.calisma\hb-1c-test yok
.calisma\kodek-etiketi yok
```

## 2. Kosum: kirmizi (MiniKipOlcusuTests son asert kasten bozuldu)

Mutasyon: `Assert.True(belirtec - olculen < adim, ...)` -> `Assert.True(belirtec - olculen < 0, ...)`. Kosumdan sonra geri alindi.

```
C:\Users\Administrator\Desktop\Projeler\VidShrink\.claude\worktrees\agent-af267e8dabd2b1c86\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)

Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:06.38]     VidShrink.Tests.MiniKipOlcusuTests.SayacGenisligiBelirtectenBuyukDegil [FAIL]
  Başarısız VidShrink.Tests.MiniKipOlcusuTests.SayacGenisligiBelirtectenBuyukDegil [367 ms]
  Hata İletisi:
   belirtec olculenden bir SpaceMd'den fazla genis: 72 vs 71
  Yığın İzleme:
     at VidShrink.Tests.MiniKipOlcusuTests.SayacGenisligiBelirtectenBuyukDegil() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.claude\worktrees\agent-af267e8dabd2b1c86\tests\VidShrink.Tests\MiniKipOlcusuTests.cs:line 51
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)

Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: 367 ms - VidShrink.Tests.dll (net8.0)
```

--- klasor: kirmizi kosumdan sonra

```
.calisma\mini-olcu   VAR: sayac.txt (74 bayt)

sayac.txt icerigi:
MonoValue "00:00:00" olculen=71
RecorderMiniReadoutWidth=72
SpaceMd=12
```

Mutasyon geri alindiktan sonraki kosum:

```
Basarili!  - Basarisiz:     0, Basarili:     1, Atlanan:     0, Toplam:     1, Sure: 338 ms - VidShrink.Tests.dll (net8.0)

.calisma\mini-olcu   yok
```

## CI

```
gh run list --branch t0/kanit-arayuz
35324099960  ci  t0/kanit-arayuz  push  success

gh run view 35324099960 --json jobs
test           completed success
kaydedici-x11  completed success
```

## Denetim Sonrası: İki Kusur (T0)

Bağımsız denetim bu öbeği **KALDI** verdi. İki bulgu ölçülerek doğrulandı ve düzeltildi.

### 1. `PerformanceCheckTests` paylaşılan kanıt dosyası (KRİTİK)

Sınıfın on bir ölçüsü tek bir `.calisma/t63/olcum.txt`'ye yazıyor, `Kapat()` ise onu koşulsuz
siliyordu. Kırmızı düşen bir ölçü kanıdını yazdıktan sonra, **aynı koşumda yeşil biten başka
bir ölçünün** `Kapat()`'ı o kanıtı yok ediyordu. Denetçinin ölçümü:

```
Başarısız! - Başarısız: 1, Başarılı: 23, Atlanan: 0, Toplam: 24
=== t63 durumu ===
/usr/bin/ls: cannot access '.calisma/t63/': No such file or directory
```

Düzeltme: kanıt dosyası artık **test başına** ayrı. xUnit her ölçü için yeni bir örnek kurduğu
için ad `ITestOutputHelper`'ın taşıdığı testten okunuyor; okunamazsa örnek sayacına düşülür.
`Kapat()` yalnız kendi dosyasını siliyor, klasörü boşalınca kaldırıyor. `Kos` yardımcısı
`Log` örnek metoduna dokunduğu için statiklikten çıktı.

Aynı mutasyon (`Assert.True(result.SoftwareRealtimeCores > 0, ...)` → `< 0`), aynı geniş filtre:

```
  Başarısız VidShrink.Tests.PerformanceCheckTests.BuMakinedeKodlamaNereyeDusuyor [6 s]
   MUTASYON
Başarısız! - Başarısız:     1, Başarılı:    23, Atlanan:     0, Toplam:    24, Süre: 1 m 45 s
=== t63 ===
BuMakinedeKodlamaNereyeDusuyor.txt
```

Kanıt artık yerinde ve **düşen ölçünün adıyla**. Mutasyon elle geri yazıldı
(`git checkout` kullanılmadı).

### 2. `KareYerlesimTests` `.calisma/t194` sızıntısı

`KaynakBilgiEtiketleriKendiHucresindeKalir` dört dosya bırakıyor, `Kapat` çağrısı yoktu —
aynı sınıfın başka kolu kapatılmışken bu atlanmıştı. Son asertten sonra `Kapat(klasor, ad)`
eklendi.

### Düzeltmelerden sonra, geniş filtreyle yeşil koşum

```
Başarılı!  - Başarısız:     0, Başarılı:    48, Atlanan:     0, Toplam:    48, Süre: 1 m 53 s
=== klasorler ===
t63 YOK
t194 YOK
```

### Kalan borç

Denetimin diğer maddeleri (yeşil filtrenin `KareYerlesimTests`'i kapsamaması, kırmızı kolun
`Toplam: 1` ile alınmış olması, öbek başına tek `Kapat` yerine 12 kopya, plan–dosya adı
sapması) `.claude/acik.md`'ye gerekçeleriyle yazıldı.

## Yeniden Ölçüm — İki Kol da Geniş Filtreyle (18 Eylül 2026)

Yukarıdaki iki kol dar filtreyle alınmıştı: yeşil 13 sınıfı kapsıyor ama `KareYerlesimTests`'i
kapsamıyordu (239 test), kırmızı ise tek testle (`Toplam: 1`) koşulmuştu. Bu haliyle "yeşil
koşum kendi bıraktığını siler, kırmızı kanıtını korur" iddiası aynı kümede sınanmış olmuyor.
İki kol da aynı geniş filtreyle yeniden alındı.

Filtre (14 sınıf; eskisinin 13'ü + `KareYerlesimTests`):

```
FullyQualifiedName~VidShrink.Tests.AltyaziIndirmeTests|FullyQualifiedName~VidShrink.Tests.AltyaziOturumTests|FullyQualifiedName~VidShrink.Tests.WindowLayoutTests|FullyQualifiedName~VidShrink.Tests.QualityTargetUiTests|FullyQualifiedName~VidShrink.Tests.QualityTargetTests|FullyQualifiedName~VidShrink.Tests.PerformanceCheckTests|FullyQualifiedName~VidShrink.Tests.PaletteApplyTests|FullyQualifiedName~VidShrink.Tests.UstSeritTikTests|FullyQualifiedName~VidShrink.Tests.MiniKipOlcusuTests|FullyQualifiedName~VidShrink.Tests.TestAyarYoluTests|FullyQualifiedName~VidShrink.Tests.BiciminTests|FullyQualifiedName~VidShrink.Tests.FiltreYoklamaTests|FullyQualifiedName~VidShrink.Tests.StreamMappingTests|FullyQualifiedName~VidShrink.Tests.KareYerlesimTests
```

Her kolun öncesinde 14 kanıt klasörü silindi; döküm `tools/kanit-dokumu-arayuz.sh` ile alındı.
Koşum `VIDSHRINK_LIBMPV`'siz açılınca `TestAyarYoluTests` libmpv yokluğundan düşüyor (262/263) —
kusur değil ortam eksiği, ölçü o haliyle atıldı ve motor verilerek tekrarlandı.

### Yeşil kol

```
Başarılı!  - Başarısız:     0, Başarılı:   263, Atlanan:     0, Toplam:   263, Süre: 4 m 10 s
EXITCODE=0

--- klasor: .calisma/p28-altyazi -> YOK
--- klasor: .calisma/s20 -> YOK
--- klasor: .calisma/t61 -> YOK
--- klasor: .calisma/t57 -> YOK
--- klasor: .calisma/t63 -> YOK
--- klasor: .calisma/t194 -> YOK
--- klasor: .calisma/tema -> YOK
--- klasor: .calisma/serit-tik -> YOK
--- klasor: .calisma/mini-olcu -> YOK
--- klasor: .calisma/ayar-yolu -> YOK
--- klasor: .calisma/a1/filtre -> YOK
--- klasor: .calisma/a1 -> YOK
--- klasor: .calisma/hb-1c-test -> YOK
--- klasor: .calisma/kodek-etiketi -> YOK
```

263 testin hiçbiri arkasında klasör bırakmadı. Eski yeşil kol 239 testti; aradaki 24 test
`KareYerlesimTests` ve dalın kendi eklediği kodek etiketi ölçüleri.

### Kırmızı kol (aynı filtre, aynı 263 test)

Mutasyon: `MiniKipOlcusuTests.cs:51`, `Assert.True(belirtec - olculen < adim, ...)` →
`Assert.True(belirtec - olculen < 0, ...)`. Koşumdan sonra **elle** geri yazıldı.

```
[xUnit.net 00:01:52.02]     VidShrink.Tests.MiniKipOlcusuTests.SayacGenisligiBelirtectenBuyukDegil [FAIL]
  Hata İletisi:
   belirtec olculenden bir SpaceMd'den fazla genis: 72 vs 71
  Yığın İzleme:
     at VidShrink.Tests.MiniKipOlcusuTests.SayacGenisligiBelirtectenBuyukDegil() in ...\MiniKipOlcusuTests.cs:line 51

Başarısız! - Başarısız:     1, Başarılı:   262, Atlanan:     0, Toplam:   263, Süre: 3 m 51 s
EXITCODE=1

--- klasor: .calisma/p28-altyazi -> YOK
--- klasor: .calisma/s20 -> YOK
--- klasor: .calisma/t61 -> YOK
--- klasor: .calisma/t57 -> YOK
--- klasor: .calisma/t63 -> YOK
--- klasor: .calisma/t194 -> YOK
--- klasor: .calisma/tema -> YOK
--- klasor: .calisma/serit-tik -> YOK
--- klasor: .calisma/mini-olcu
mini-olcu\sayac.txt
--- klasor: .calisma/ayar-yolu -> YOK
--- klasor: .calisma/a1/filtre -> YOK
--- klasor: .calisma/a1 -> YOK
--- klasor: .calisma/hb-1c-test -> YOK
--- klasor: .calisma/kodek-etiketi -> YOK
```

```
$ cat .calisma/mini-olcu/sayac.txt
MonoValue "00:00:00" olculen=71
RecorderMiniReadoutWidth=72
SpaceMd=12
```

İki dökümün farkı tek satır: `mini-olcu\sayac.txt`. Düşen testin kanıtı duruyor, aynı koşumdaki
262 testin kanıtı silinmiş, diğer 13 klasör iki kolda da yok. Kural artık aynı küme üstünde,
iki kolda da ölçülmüş oldu.
