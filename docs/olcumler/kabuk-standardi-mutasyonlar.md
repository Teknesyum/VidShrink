# Kabuk standardı — mutasyon kanıtı

Her kural kaynakta bozuldu, `--no-incremental` ile yeniden derlendi, kendi testi koşuldu.

## M1 — palet dışı düz onaltılık renk

Bozulan dosya: `src/VidShrink.App/MainWindow.axaml satır 19` — test: `RenkYalnizPaletten`

```
    0 Hata

Geçen Süre 00:00:05.52
C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)
Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:05.82]     VidShrink.Tests.KabukStandardiTests.RenkYalnizPaletten [FAIL]
  Başarısız VidShrink.Tests.KabukStandardiTests.RenkYalnizPaletten [21 ms]
  Hata İletisi:
   Assert.Empty() Failure: Collection was not empty
Collection: ["src/VidShrink.App/MainWindow.axaml:19: Foreground="···]
  Yığın İzleme:
     at VidShrink.Tests.KabukStandardiTests.RenkYalnizPaletten() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\KabukStandardiTests.cs:line 42
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: 21 ms - VidShrink.Tests.dll (net8.0)
```

## M2 — palete info renk belirteci

Bozulan dosya: `src/VidShrink.App/Themes/Palette/Neon/Theme.axaml` — test: `InfoRengiYoktur`

```
    0 Hata

Geçen Süre 00:00:03.89
C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)
Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:05.70]     VidShrink.Tests.KabukStandardiTests.InfoRengiYoktur [FAIL]
  Başarısız VidShrink.Tests.KabukStandardiTests.InfoRengiYoktur [15 ms]
  Hata İletisi:
   Assert.Empty() Failure: Collection was not empty
Collection: ["src/VidShrink.App/Themes/Palette/Neon/Theme.axaml:"···]
  Yığın İzleme:
     at VidShrink.Tests.KabukStandardiTests.InfoRengiYoktur() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\KabukStandardiTests.cs:line 59
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: 15 ms - VidShrink.Tests.dll (net8.0)
```

## M3 — TextBlock'a parıltı

Bozulan dosya: `src/VidShrink.App/MainWindow.axaml TxtAppliedLead` — test: `PariltiYaziyaKonmaz`

```
    0 Hata

Geçen Süre 00:00:03.89
C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)
Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:06.22]     VidShrink.Tests.KabukStandardiTests.PariltiYaziyaKonmaz [FAIL]
  Başarısız VidShrink.Tests.KabukStandardiTests.PariltiYaziyaKonmaz [21 ms]
  Hata İletisi:
   Assert.Empty() Failure: Collection was not empty
Collection: ["src/VidShrink.App/MainWindow.axaml: <TextBlock.Eff"···]
  Yığın İzleme:
     at VidShrink.Tests.KabukStandardiTests.PariltiYaziyaKonmaz() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\KabukStandardiTests.cs:line 83
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: 21 ms - VidShrink.Tests.dll (net8.0)
```

## M4 — liste satırına parıltı kolu

Bozulan dosya: `src/VidShrink.App/Themes/Controls.axaml ListBoxItem` — test: `PariltiKapsayicidadir`

```
    0 Hata

Geçen Süre 00:00:04.01
C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)
Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:06.84]     VidShrink.Tests.KabukStandardiTests.PariltiKapsayicidadir [FAIL]
  Başarısız VidShrink.Tests.KabukStandardiTests.PariltiKapsayicidadir [22 ms]
  Hata İletisi:
   Assert.Empty() Failure: Collection was not empty
Collection: ["src/VidShrink.App/Themes/Controls.axaml:1429: List"···, "src/VidShrink.App/Themes/Controls.axaml:1429: List"···]
  Yığın İzleme:
     at VidShrink.Tests.KabukStandardiTests.PariltiKapsayicidadir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\KabukStandardiTests.cs:line 122
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: 22 ms - VidShrink.Tests.dll (net8.0)
```

## M5 — giriş alanına yer tutucu metin

Bozulan dosya: `src/VidShrink.App/MainWindow.axaml satır 285` — test: `YerTutucuMetinYok`

```
    0 Hata

Geçen Süre 00:00:04.05
C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)
Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:06.46]     VidShrink.Tests.KabukStandardiTests.YerTutucuMetinYok [FAIL]
  Başarısız VidShrink.Tests.KabukStandardiTests.YerTutucuMetinYok [29 ms]
  Hata İletisi:
   Assert.Empty() Failure: Collection was not empty
Collection: ["src/VidShrink.App/MainWindow.axaml:285: <TextBox x"···]
  Yığın İzleme:
     at VidShrink.Tests.KabukStandardiTests.YerTutucuMetinYok() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\KabukStandardiTests.cs:line 160
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: 29 ms - VidShrink.Tests.dll (net8.0)
```

## M6 — bildirim ömrü 6 sn değil

Bozulan dosya: `src/VidShrink.App/ShrinkJobWindow.axaml.cs` — test: `HataBildirimiKendiKapanmaz`

```
    0 Hata

Geçen Süre 00:00:04.01
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:06.57]     VidShrink.Tests.KabukStandardiTests.HataBildirimiKendiKapanmaz [FAIL]
  Başarısız VidShrink.Tests.KabukStandardiTests.HataBildirimiKendiKapanmaz [3 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
           ↓ (pos 0)
Expected: "6"
Actual:   "8"
           ↑ (pos 0)
  Yığın İzleme:
     at VidShrink.Tests.KabukStandardiTests.HataBildirimiKendiKapanmaz() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\KabukStandardiTests.cs:line 170
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: 3 ms - VidShrink.Tests.dll (net8.0)
```

## M7 — kendi çubuğu çizilirken sistem kenarlığı geri verilmiyor

Bozulan dosya: `src/VidShrink.App/ShrinkJobWindow.axaml` — test: `KendiBaslikCubugu`

```
    0 Hata

Geçen Süre 00:00:05.28
C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)
Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:06.20]     VidShrink.Tests.KabukStandardiTests.KendiBaslikCubugu [FAIL]
  Başarısız VidShrink.Tests.KabukStandardiTests.KendiBaslikCubugu [20 ms]
  Hata İletisi:
   Assert.Empty() Failure: Collection was not empty
Collection: ["src/VidShrink.App/ShrinkJobWindow.axaml: WindowDec"···]
  Yığın İzleme:
     at VidShrink.Tests.KabukStandardiTests.KendiBaslikCubugu() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\KabukStandardiTests.cs:line 216
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: 20 ms - VidShrink.Tests.dll (net8.0)
```

## M8 — görsel tema kitaplığı bağımlılığı

Bozulan dosya: `src/VidShrink.App/VidShrink.App.csproj` — test: `GorselTemaKitapligiYok`

```
    0 Hata

Geçen Süre 00:00:04.43
C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)
Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:07.52]     VidShrink.Tests.KabukStandardiTests.GorselTemaKitapligiYok [FAIL]
  Başarısız VidShrink.Tests.KabukStandardiTests.GorselTemaKitapligiYok [28 ms]
  Hata İletisi:
   Assert.Empty() Failure: Collection was not empty
Collection: ["src/VidShrink.App/VidShrink.App.csproj: Semi.Avalo"···]
  Yığın İzleme:
     at VidShrink.Tests.KabukStandardiTests.GorselTemaKitapligiYok() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\wt-kabuk\tests\VidShrink.Tests\KabukStandardiTests.cs:line 245
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: 28 ms - VidShrink.Tests.dll (net8.0)
```

