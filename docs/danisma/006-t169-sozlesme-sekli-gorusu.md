# T169 sozlesme sekli gorusu

- soran: T0
- danisilan: fable
- tarih: 2026-09-05

## Sorulan

C:\Users\Teknesyum\.claude\plugins\cache\teknesyum\teknesyum-core\0.15.0\roles\advisor.md dosyasini oku ve onu uygula.
Soran opus kosuyor. Turkce yaz.

Proje koku: C:\Users\Teknesyum\Desktop\Projeler\VidShrink
Sozlesme: `.claude/relay/contracts/T169.md` — okuyabilirsin.

## Soru

T169 iki turdur teslim edemiyor ve **ikisi de ayni sekilde dustu**: ajan testi arka planda
baslatiyor, sonra "testlerin bitmesini bekliyorum" deyip **hicbir sey yapmadan** turu
kapatiyor. Bu sozlesmede toplam ~490 bin token harcandi, teslim sifir.

Ayni kusur bu depoda baska bir sozlesmede de (T161) iki tur ust uste oldu ve orada
uctuncu turda duzeldi — ama orada da modeli buyutmek gerekti.

**Sana sordugum sey modelin buyuklugu degil. Sozlesmenin sekli.** Bu sozlesme bir ajanin
tek turda bitirebilecegi bir is mi, yoksa bolunmesi mi gerekiyor? Bolunecekse hangi
cizgiden?

## Kanit

T169'un istedigi is: Windows sag tik menusune ikinci bir girdi ("VidShrink ile Kucult") ve
altinda hizli hedef boyutlarindan olusan bir alt menu.

Sekiz kabul kriteri var: K1 girdi agaci, K2 alt menu + liste esitligi, K3 geri alma
simetrisi, K4 arguman cozumu, K5 coklu secim tek kuyruk, K6 gercek koke dokunmama,
K7 uc mutasyon, K8 kol sayisi.

**Isin teknik sekli sira disi:** urun kodu **PowerShell** (`Install-VidShrink.ps1`),
olcu ise **xUnit/C#**. Yani test, PowerShell betigini bir alt surec olarak kosturup
kayit defterine ne yazdigina bakiyor. Bu desen depoda zaten var ve calisiyor
(`tests/VidShrink.Tests/ShellMenuTests.cs`, T68) — ama her olcu bir surec dogurmak
demek ve `dotnet test` cagrisi yavas.

Iki turun agaclarinda birakilan is:

```
tur 2 (kesildi):  Install-VidShrink.ps1 degismis
                  src/VidShrink.Core/ShellIntegration.cs degismis
                  tests/VidShrink.Tests/ShellShrinkMenuTests.cs acilmis
                  docs/olcumler/kabuk-menusu.md YOK
                  commit yok
```

Depodaki ilgili sayilar:
- `dotnet test` filtresiz **25 dakika** suruyor; ajanlara yalniz kendi filtreleriyle
  kosmalari soylendi.
- T169'un `verify` listesinde **dort** ayri `dotnet test` cagrisi var (kendi kolu +
  bozmadigini gostermesi gereken iki eski kol) + bir manset denetimi + bir build.
- K7 uc mutasyon istiyor ve **her mutasyondan once** `dotnet build -c Release
  --no-incremental` sart (`--no-build` yasak). Yani en az uc tam yeniden derleme.
- Makinede es zamanli baska iki ajan kosuyor.

Ayni depoda tek turda basariyla kapanan sozlesmeler de var (T162: 14 kol, iki mutasyon;
T166: iki bulgu, iki mutasyon). Onlar saf C#'ti ve alt surec dogurmuyordu.

## Bu depoda seni baglayan kurallar

- Bir kriteri karsilayamayan ajan "uydurma ve gecistirme, bildir" diyor.
- Bos olcu (`Assert.True(true)`, kalici dogru VEYA) bu depoda uc sozlesmede bes kez cikti
  ve hepsi bagimsiz denetimden dondu; kriterler bu yuzden sert.
- `main`e yalniz T0 birlestirir; her sozlesme kendi dalinda calisir.

## Ne istiyorum

advisor.md'nin uc basligi, en fazla 20 satir. Plan yazma, kod yazma, dosya olusturma.

Somut cevap ver: **bolunsun mu, bolunmesin mi**; bolunecekse cizgi nerede; ve
bolunmeyecekse iki turdur ayni yerde dusen seyin sebebi sozlesmede mi baska yerde mi.

## Donen

_cevap bekleniyor_
