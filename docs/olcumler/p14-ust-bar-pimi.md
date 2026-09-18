# P14: Ölçünün Adım 1'i Girdi Gelmese de Geçiyordu

`main` CI koşumu `35321421774` tek testte kırmızıydı:

```
Failed: 1, Passed: 3557, Skipped: 27, Total: 3585, Duration: 28 m 59 s
VidShrink.Tests.OynaticiYolHaritasiTests.P14UstBarAltBarlaAyniKurallaGizlenir
```

## Kusur

Ölçü şu sırayla kuruluyordu:

```csharp
if (!view.IsPlaying) view.Apply(Keymap.PlayPause.ToCommand());

var saat = new ElleSaat();
window.ChromeZone.Clock = saat;
...
Adim("1 fare ustte", () => Hareket(window, view, new Point(640, 2)), true, false);
```

Oynatma **sahte saat takılmadan önce** başlıyor. `ApplyChromeMode` → `Hold(false)` geçişi
gizlenmeyi gerçek `DispatcherHoverClock` üstünde kuruyor, sonra `Clock` yazıcısı eski saati
`Stop()` ediyor ve bekleyen tik düşüyor. Test adım 1'e **bar açık, bekleyen yok** durumunda
giriyor — ki adım 1'in beklediği tam da bu.

Yani adım 1 `Hareket` hiç ulaşmasa da GEÇTİ diyor. Yanlış olumlu; ölçü hiçbir şeyi pimlemiyor.

Ölçüldü: eski sırayla, adım 1'i "fare aşağıda → bekleyen var" yapınca yerelde **KALDI** —
ilk hareket gerçekten hiçbir şey değiştirmiyordu.

```
1 fare asagida, gecikme bekliyor: sekme 0, oynuyor True, bar acik, bekleyen yok KALDI
2 gecikme doldu: sekme 0, oynuyor True, bar acik, bekleyen yok KALDI
3 fare ustte, bar geri geldi: ... GECTI
```

`HoverZone.SetPointer` değer değişmeyince erken dönüyor (`HoverZone.cs:171`); fare hiç üst
banda girmemişken aşağı hareket durumu değiştirmiyor, bu yüzden ilk hareketin ölçülebilir
etkisi yok. Ölçünün pimi bu yüzden farenin **banda girip çıkmasına** dayanmalı.

## Düzeltme

Sahte saat oynatma başlamadan takılıyor ve oynatmanın kendisi 1. adım oluyor; hareketler
bandın iki yakasına düşecek biçimde diziliyor:

```csharp
if (view.IsPlaying) view.Apply(Keymap.PlayPause.ToCommand());

var saat = new ElleSaat();
window.ChromeZone.Clock = saat;
...
Adim("1 oynatma basladi, gecikme bekliyor", () => view.Apply(Keymap.PlayPause.ToCommand()), true, true);
Adim("2 gecikme doldu", () => saat.Ates(), false, false);
Adim("3 fare ustte, bar geri geldi", () => Hareket(window, view, new Point(640, 2)), true, false);
Adim("4 fare asagida, gecikme bekliyor", () => Hareket(window, view, new Point(640, 420)), true, true);
```

Adım 3 bar **gizliyken** koşuyor: barı yalnız ulaşan bir hareket geri getirebilir.
Adım 4 aynı şeyi ters yönde pimliyor. Duraklatma kolu da banda girip çıkacak biçimde
ikiye ayrıldı (adım 7-8), böylece o iki adım da durum değiştiren gerçek hareket oluyor.

## Mutasyon

| # | Kesim | Dosya | Sonuç |
|---|-------|-------|-------|
| M9 | `Hareket` ham olayı hiç göndermiyor (erken dönüş) | `OynaticiYolHaritasiTests.cs` | 1 kırmızı, adım 3 ve 4 KALDI |

```
   kalan adimlar: 3 fare ustte, bar geri geldi, 4 fare asagida, gecikme bekliyor
1 oynatma basladi, gecikme bekliyor: sekme 0, oynuyor True, bar acik, bekleyen 360 ms GECTI
2 gecikme doldu: sekme 0, oynuyor True, bar gizli, bekleyen yok GECTI
3 fare ustte, bar geri geldi: sekme 0, oynuyor True, bar gizli, bekleyen yok KALDI
4 fare asagida, gecikme bekliyor: sekme 0, oynuyor True, bar gizli, bekleyen yok KALDI
Başarısız! - Başarısız:     2, Başarılı:     0, Atlanan:     0, Toplam:     2, Süre: 3 s
```

Eski düzende aynı mutasyon adım 1'i **GEÇTİ** bırakıyordu; ölçünün kazandığı budur.
Mutasyon elle geri yazıldı (`git checkout` kullanılmadı).

Düzeltme yerindeyken:

```
Başarılı!  - Başarısız:     0, Başarılı:     2, Atlanan:     0, Toplam:     2, Süre: 6 s
```

Sınıfın tamamı: `Başarısız: 1, Başarılı: 10, Toplam: 11` — düşen `P28YanindakiAltyazilar
KendiligindenYuklenir`, bu makinedeki libmpv ortam eksiği; P14 yeşil.
