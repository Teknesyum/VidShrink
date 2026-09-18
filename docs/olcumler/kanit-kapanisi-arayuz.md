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
