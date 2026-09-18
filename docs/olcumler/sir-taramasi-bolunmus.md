# Sır Taraması: `+` ile İkiye Bölünmüş Sır

`DepodaGomuluAnahtarYok` satır satır tarıyordu. C#'ta bir sır iki parçaya bölünüp
birleştirildiğinde hiçbir satır tek başına sır gibi görünmüyor:

```csharp
const string belirtec = "<JWT BASLIGI>"
    + ".PIM77-belirtec-govdesi.imza";
```

Ölçünün **kendi pozitif kontrolleri** tam bu boşluktan geçirilmişti — yani ölçü, kaçırdığı
biçimi kendi kaynağında kullanıyordu.

## Düzeltme

İkinci bir kol: `Birlestir` bitişik dizi birleştirmelerini (satır sonunu geçenler dahil)
kapatıp metni yeniden satırlara bölüyor, sonra **aynı** tarayıcı koşuyor.

Muafiyet kapalı küme: `PozitifKontrolDosyalari` — bölünmüş yazmayı bilerek kullanan iki
ölçü dosyası. Muafiyetin ölü kalmaması `Assert.All(muafSayilari, sayi => sayi > 0)` ile
pimli: listedeki bir dosya kolu hiç tetiklemiyorsa ölçü kırmızı olur.

## Mutasyon

| # | Kesim | Sonuç |
|---|-------|-------|
| M11 | Muaf olmayan bir belgeye `"7hQ2vN9xLm4TpZ8" + "cRb1WnH5kJ6yEaG0"` satırı | kırmızı; **yalnız** birleştiren kol yakaladı |

```
  Hata İletisi:
   Assert.Empty() Failure: Collection was not empty
Collection: ["docs\olcumler\butce-doldur.md: <!-- M11 Api-Key:"···]
```

```
$ cat .calisma/p28-altyazi/anahtar-taramasi.txt
temiz
pozitif kontrol: True
tel kaydi: True
ayar alani: False
bolunmus kol: docs\olcumler\butce-doldur.md: <!-- M11 Api-Key: "<32 KARAKTERLIK SAHTE ANAHTAR>" -->
bolunmus kolun muaf dosyalarda yakaladigi: 2 1
```

İlk satır **`temiz`**: satır bazlı kol aynı sırrı görmüyor. Ölçünün kazandığı budur.
Mutasyon elle geri yazıldı (`git checkout` yok).

Kolun ilk koşumu ayrıca gerçek bir bulgu verdi: `AltyaziOturumTests.cs:436-437`'deki
bölünmüş sahte JWT. Sır değil, sentetik pim — muafiyet listesine bu yüzden girdi.

## Yeşil Koşum

```
dotnet build tests/VidShrink.Tests/VidShrink.Tests.csproj -c Release -warnaserror -m:2
    0 Hata
```

```
dotnet test ... --filter "FullyQualifiedName~DepodaGomuluAnahtarYok"
Başarılı!  - Başarısız:     0, Başarılı:     1, Atlanan:     0, Toplam:     1, Süre: 586 ms
```

Kanıt dosyası `anahtar-taramasi.txt` yeşil koşumdan sonra yok.

## Belgenin Kendisi Taramaya Takıldı

Bu belge yazıldıktan sonra ölçü kırmızıya döndü: kanıt bölümleri hem M11'in ektiği sahte
anahtarı hem de `AltyaziOturumTests.cs`'teki sentetik JWT başlığını **tırnak içinde**
taşıyordu; yeni kol ikisini de yakaladı.

Ders ölçünün lehine: belgeye verbatim düşen sır de sırdır. İkisi de maskelendi
(`<32 KARAKTERLIK SAHTE ANAHTAR>`, `<JWT BASLIGI>`); satırın yapısı, dosya adı ve bulgunun
kendisi yerinde duruyor. Muafiyet listesine belge **eklenmedi** — muafiyet ölçüyü körleştirirdi.

Yeşil koşum belge yazılmadan önce alınmıştı; ilk turda bu yüzden görülmedi.
