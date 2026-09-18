# Kanıt Kapanışı: Ortak Gövde Joker Eşlemeyi Kaybetmişti

38 çağrı yerini tek gövdeye toplayan `KanitKapanisi.Kapat`, eski `AltyaziKanit.Kapat`'ın
`Directory.GetFileSystemEntries(Kok, ad)` ile yaptığı **joker eşlemeyi** kaybetti; tam ad
eşliyordu. Bugün hiçbir çağrı yerinde joker yok, o yüzden kimse kırmızıya dönmedi — ama
ileride `"donu*-trimmed*"` verilse gövde sessizce hiçbir şey silmezdi.

Sessiz kusurun ölçüsü de yoktu: kapanış yanlış çalıştığında hiçbir test kırmızıya dönmez,
yalnız artık birikir ya da kardeşin kanıtı kaybolur.

## Düzeltme

`Onceki` ve `Kapat` ortak bir `Sil` üstünden geçiyor; ad `*` ya da `?` taşıyorsa eşleşen
her girdi siliniyor, taşımıyorsa yol doğrudan kuruluyor:

```csharp
private static void Sil(string klasor, string ad)
{
    if (ad.IndexOfAny(Jokerler) >= 0)
    {
        if (!Directory.Exists(klasor)) return;
        foreach (var eslesen in Directory.GetFileSystemEntries(klasor, ad)) SilYolu(eslesen);
        return;
    }

    SilYolu(Path.Combine(klasor, ad));
}
```

`KanitKapanisiTests` dört kolu pimliyor: joker eşleşeni siler eşleşmeyeni bırakır, jokersiz
ad yalnız kendini siler (`olcu.txt` silinirken `olcu.txt.eski` kalır), **eşleşmeyen joker
klasörü süpürmez**, boş üst klasörler gider ama `.calisma` kalır.

## Mutasyon

| # | Kesim | Dosya | Sonuç |
|---|-------|-------|-------|
| M13 | joker kolu hiç girilmiyor (`ad.Length < 0`) | `KanitKapanisi.cs` | 1 kırmızı |

```
[xUnit.net 00:00:05.76]     VidShrink.Tests.KanitKapanisiTests.JokerEslesenleriSilerEslesmeyeniBirakir [FAIL]
   Assert.Equal() Failure: Collections differ
Başarısız! - Başarısız:     1, Başarılı:     3, Atlanan:     0, Toplam:     4, Süre: 20 ms
```

Kırmızı koşumdan sonra kanıt klasörü yerinde durdu — kuralın kendisi de ölçüldü:

```
$ ls .calisma/test-ciktilari/kanit-kapanisi-olcu/
joker/
```

İlk denemede mutasyon `if (false)` yazılmıştı; CS0162 ile derleme düştü ve `--no-build`
eski ikiliyi koşturup **4/4 yeşil** verdi. Sahte yeşil buradan geliyor, mutasyon derlenebilir
olmalı. Mutasyon elle geri yazıldı (`git checkout` yok).

Düzeltme yerindeyken:

```
dotnet test ... --filter "KanitKapanisiTests|AltyaziIndirmeTests|BoslukKirpmaTests"
Başarılı!  - Başarısız:     0, Başarılı:    76, Atlanan:     0, Toplam:    76, Süre: 5 s
```

Koşumdan sonra `.calisma/test-ciktilari/kanit-kapanisi-olcu` yok.
