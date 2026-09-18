# Kanıt Klasörü Kapanışı: Kaydedici Öbeği (2. Öbek)

Kural: kanıt dosyasını silen çağrı **son asertten sonra** durur. Yeşil koşum kendi bıraktığını
siler; kırmızı koşum kanıtını korur, çünkü düşen asert o satıra gelmez. Klasör boşalınca o da
gider. Ortak gövde `tests/VidShrink.Tests/KanitKapanisi.cs`, her kanıt yardımcısında tek satırlık
`Kapat` sarmalayıcısı.

Dal: `t0/kanit-kaydedici`. Makine: Windows 11, gdigrab var, **OBS Virtual Camera var** — `[KameraFact]`
kolu gerçek aygıtla koştu (`ffmpeg -list_devices` çıktısında `"OBS Virtual Camera" (video)`), atlanan
tek ölçü X11 kolu. Denetim düzeltmesi: bu satır önce "aygıt yok, sahte kaynakla koştu" diyordu;
koşumun kendi `Atlanan: 1` sayısı bunu yalanlıyor, aygıt atlansaydı 2 olurdu.

Ortam: `VIDSHRINK_LIBMPV=C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\hiper\taban\tools\libmpv\libmpv-2.dll`
(worktree'de `tools/libmpv` yok; `KayitOdakTakibiTests` oynatıcıyı bu kütüphaneyle yüklüyor).

Derleme:

```
dotnet build VidShrink.sln -c Release -warnaserror -m:2
```

```
    0 Uyarı
    0 Hata

Geçen Süre 00:00:02.06
```

Filtre:

```
FullyQualifiedName~KayitMotoruTests|FullyQualifiedName~KaydediciSeciciTests|FullyQualifiedName~KaydediciCerceveTests|FullyQualifiedName~KaydediciGirdiTests|FullyQualifiedName~KaydediciKameraTests|FullyQualifiedName~KaydediciHedefTests|FullyQualifiedName~KaydediciArkaPlanTests|FullyQualifiedName~KaydediciOnizlemeTests|FullyQualifiedName~KaydediciTamponTests|FullyQualifiedName~KayitBolmeTests|FullyQualifiedName~BoslukKirpmaTests|FullyQualifiedName~KayitOdakTakibiTests|FullyQualifiedName~KayitSonucVurguTests|FullyQualifiedName~KaydediciPencereTests|FullyQualifiedName~SesGirisiTests|FullyQualifiedName~SesliKayitTests
```

## Yeşil Koşum

```
dotnet test tests/VidShrink.Tests/VidShrink.Tests.csproj -c Release --no-build --filter "<yukarıdaki filtre>"
```

```
C:\Users\Administrator\Desktop\Projeler\VidShrink\.claude\worktrees\agent-addc133288b3df395\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)

Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:49.53]     VidShrink.Tests.KaydediciPencereTests.X11PenceresiListedenBulunupIkiSaniyeKaydedilir [SKIP]
  Atlandı VidShrink.Tests.KaydediciPencereTests.X11PenceresiListedenBulunupIkiSaniyeKaydedilir [1 ms]

Başarılı!  - Başarısız:     0, Başarılı:   115, Atlanan:     1, Toplam:   116, Süre: 2 m 17 s - VidShrink.Tests.dll (net8.0)
```

--- klasor: koşumdan sonra `.calisma` (yedi kanıt klasörünün hiçbiri yok)

```
$ ls -1 .calisma
a1/
ayar-yolu/
dalga3/
hb-1c-test/
mini-olcu/
oynatici-motor/
p28-altyazi/
s20/
serit-tik/
t57/
t61/
t63/
tema/
test-ciktilari/

$ for d in paket-2 paket-2b dalga8a dalga8c dalga8d kaydedici-pencere kayit-sonuc-vurgu; ...
YOK: paket-2
YOK: paket-2b
YOK: dalga8a
YOK: dalga8c
YOK: dalga8d
YOK: kaydedici-pencere
YOK: kayit-sonuc-vurgu
```

## Kırmızı Koşum (bilerek bozulan son asert)

Mutasyon: `SesGirisiTests.MikrofonVeSistemSesiBirlikteAmixIleBirlesir` son asertinin beklenen
değeri `"audio=Stereo Mix (Realtek High Definition Audio)"` yerine `"audio=BOZUK MUTASYON"`.
Koşumdan sonra geri alındı.

```
dotnet test tests/VidShrink.Tests/VidShrink.Tests.csproj -c Release --no-build --filter "<yukarıdaki filtre>"
```

```
C:\Users\Administrator\Desktop\Projeler\VidShrink\.claude\worktrees\agent-addc133288b3df395\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)

Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:01:16.29]     VidShrink.Tests.SesGirisiTests.MikrofonVeSistemSesiBirlikteAmixIleBirlesir [FAIL]
  Başarısız VidShrink.Tests.SesGirisiTests.MikrofonVeSistemSesiBirlikteAmixIleBirlesir [1 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
                 ↓ (pos 6)
Expected: "audio=BOZUK MUTASYON"
Actual:   "audio=Stereo Mix (Realtek High Definition"···
                 ↑ (pos 6)
  Yığın İzleme:
     at VidShrink.Tests.SesGirisiTests.MikrofonVeSistemSesiBirlikteAmixIleBirlesir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.claude\worktrees\agent-addc133288b3df395\tests\VidShrink.Tests\SesGirisiTests.cs:line 279
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
[xUnit.net 00:01:51.97]     VidShrink.Tests.KaydediciPencereTests.X11PenceresiListedenBulunupIkiSaniyeKaydedilir [SKIP]
  Atlandı VidShrink.Tests.KaydediciPencereTests.X11PenceresiListedenBulunupIkiSaniyeKaydedilir [1 ms]

Başarısız! - Başarısız:     1, Başarılı:   114, Atlanan:     1, Toplam:   116, Süre: 1 m 57 s - VidShrink.Tests.dll (net8.0)
```

--- klasor: koşumdan sonra yalnız düşen testin kanıtı duruyor

```
$ ls -la .calisma/dalga8c
644  k5-amix.txt  235B

$ find .calisma/paket-2 .calisma/paket-2b .calisma/dalga8a .calisma/dalga8d .calisma/kaydedici-pencere .calisma/kayit-sonuc-vurgu
(hiçbiri yok)
```
