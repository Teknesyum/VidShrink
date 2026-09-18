# Kanıt Kapanışı: Koşum Başındaki Süpürme

Dört ölçü kanıt klasörünü **koşumun başında** komple siliyordu:

```csharp
foreach (var eski in Directory.GetFiles(Kanit, "*.mkv")) File.Delete(eski);   // BoslukKirpmaTests.cs:132
foreach (var eski in Directory.GetFiles(Kanit)) File.Delete(eski);            // KayitBolmeTests.cs:74
foreach (var eski in Directory.GetFiles(Kanit)) File.Delete(eski);            // KaydediciOnizlemeTests.cs:118
foreach (var eski in Directory.GetFiles(kok)) File.Delete(eski);              // KaydediciTamponTests.cs:114
```

Klasör sınıf içinde paylaşılıyor. Kardeş ölçü kırmızı düşüp kanıtını koruduğunda, bir
sonraki koşumda bu satır onu siliyordu — düzeltilen `PerformanceCheckTests` kusurunun
aynısı, yalnız sınıf içinde.

## Düzeltme

`KanitKapanisi.Onceki(klasor, adlar)`: yalnız o ölçünün **kendi** yazdığı adları siler,
klasörü süpürmez.

Klasör geneline bakan iki asert de kendi adına daraltıldı:

- `Assert.Single(GetFiles(Kanit, "*-trimmed*.mkv"))` → `"donu*-trimmed*.mkv"`.
- `Assert.Single(GetFiles(Kanit, "*.jpg"))` → koşumdan önceki jpg kümesi alınıyor, asert
  **farkın** tam olarak `onizleme.jpg` olduğunu söylüyor (`YeniJpg`). Önizlemesiz kolun jpg
  yazmadığı ölçüsü budur; kardeşin `kaynak.jpg`/`arayuz.jpg` dosyaları artık sayıma girmiyor.

## Mutasyon

| # | Kesim | Sonuç |
|---|-------|-------|
| M10 | `SonucPanelindekiDugmeSonucuKendiSatirindaSoyler` ilk aserti ters çevrildi | 1 kırmızı; `panel.mkv` + `panel.gif` **duruyor** |

```
  Başarılı VidShrink.Tests.BoslukKirpmaTests.CanliKayittaDonukAralikKisalirDonuksuzKayitDokunulmaz [393 ms]
  Başarılı VidShrink.Tests.BoslukKirpmaTests.DonukAraliklarAyrisirSondaAcikKalanSureyeKapanir [3 ms]
  Başarılı VidShrink.Tests.BoslukKirpmaTests.HerDonukAralikSaklanacakPayaKisalir [< 1 ms]
  Başarısız VidShrink.Tests.BoslukKirpmaTests.SonucPanelindekiDugmeSonucuKendiSatirindaSoyler [355 ms]

$ ls .calisma/paket-2b/kirpma
panel.gif  1B
panel.mkv  1B
```

Canlı ölçü aynı koşumda **yeşil** koştu ve kardeşin kanıtına dokunmadı; eski satır `*.mkv`
sildiği için `panel.mkv`'yi yok ederdi. Mutasyon elle geri yazıldı (`git checkout` yok).

## Yeşil Koşum

```
dotnet build VidShrink.sln -c Release -warnaserror -m:2
    0 Uyarı
    0 Hata
```

```
dotnet test tests/VidShrink.Tests/VidShrink.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~BoslukKirpmaTests|FullyQualifiedName~KayitBolmeTests|FullyQualifiedName~KaydediciOnizlemeTests|FullyQualifiedName~KaydediciTamponTests"

Başarılı!  - Başarısız:     0, Başarılı:    14, Atlanan:     0, Toplam:    14, Süre: 44 s
```

```
$ ls .calisma/paket-2b
piksel/
```

Dördünün kanıt klasörü de yeşil koşumdan sonra yok; `piksel/` bu öbeğe ait değil.
