# Kanıt Kapanışı: Fare Ölçülerinde Kanıt Hiç Yazılmıyordu

`OynaticiFareTests`'in altı ölçüsü kanıt gövdesini `StringBuilder`'da biriktirip **son
asertten sonra** diske yazıyordu. Kapanış kuralı doğruydu (`Kapat` en sonda), ama yazma da
oradaydı: düşen asert `Write`'a hiç gelmiyor, kırmızı koşum kanıtsız kalıyordu.

"Kırmızı koşum kanıtını korur" sözü bu altı ölçü için hiç geçerli değildi. `main`'de de
böyleydi; süpürme düzeltmesinin gerilemesi değil, baştan beri açık bir borç.

## Düzeltme

`FareKanit.Defter` satırı eklediği anda diske yazıyor:

```csharp
internal sealed class Defter(string ad)
{
    private readonly StringBuilder govde = new();

    internal void Satir(string metin)
    {
        govde.AppendLine(metin);
        Write(ad, govde.ToString());
    }

    internal string Metin => govde.ToString();
}
```

Beş ölçü `new StringBuilder()` yerine `new FareKanit.Defter("<ad>")` kuruyor,
`AppendLine` → `Satir` oluyor; testin dışındaki `FareKanit.Write` çağrısı kalktı. Altıncı
ölçü (`AyarlarAltMenusuSekmeyeGoturmezKisayolSekmeyiAcar`) gövdeyi tek seferde kurduğu için
`Write` çağrısı asertlerin **üstüne** taşındı. `Kapat` altısında da yerinde, son asertten sonra.

## Mutasyon

| # | Kesim | Dosya | Sonuç |
|---|-------|-------|-------|
| M14 | `f4`'ün son aserti `pan.LimitX + 1` bekliyor | `OynaticiGirdiTests.cs` | 1 kırmızı, kanıt korundu |

```
[xUnit.net 00:00:05.72]     VidShrink.Tests.OynaticiFareTests.MerkezMiknatisiEsikIcindeOrtalarDisindaOrtalamaz [FAIL]
   Assert.Equal() Failure: Values differ
Actual:   200
Başarısız! - Başarısız:     1, Başarılı:     5, Atlanan:     0, Toplam:     6, Süre: 618 ms
```

Kırmızı koşumdan sonra kanıt yerinde ve düşen değeri gösteriyor — eski düzende bu dosya hiç
yazılmıyordu:

```
$ cat .calisma/dalga7a/f4-miknatis.txt
sinir: 200 x 200 dip, miknatis 12 dip
esik icinde surukleme (10 dip): X 0 Y 0 ortada True
esik disinda surukleme (24 dip): X 24 Y 0 ortada False
merkeze donus: X 0 Y 0 ortada True
sinir disina surukleme: X 200 Y 200 (sinir 200,200)
```

Mutasyon elle geri yazıldı (`git checkout` yok).

Düzeltme yerindeyken:

```
dotnet test ... --filter "OynaticiFareTests|OynaticiGirdiTests|KanitKapanisiTests"
Başarılı!  - Başarısız:     0, Başarılı:    25, Atlanan:     0, Toplam:    25, Süre: 1 s
```

Koşumdan sonra `.calisma/dalga7a` yok.
