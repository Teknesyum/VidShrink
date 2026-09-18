# Fluent Simge Ölçülerinin Mutasyon Kaydı

18 Eylül 2026, `t0/fluent-simgeler`. Ölçüm makinesi: Windows 11, .NET 8, Debug derleme,
her mutasyondan sonra `dotnet build VidShrink.sln -c Debug -m:2 --no-incremental`.

**Bu belge bir düzeltmedir.** Yapıcının raporundaki iki sayı yeniden üretilmiyordu: "30,30"
mutasyonu için "1 kırmızı / 27", `IconPlay` boşaltması için "2 / 56" yazılmıştı. İkisi de
kapsamını adıyla söylemiyordu; kapsam adlandırılınca sayılar tutmuyor. Aşağıdaki her satır
kapsamın **filtresini**, koşan **test sayısını** ve kırmızıların **adını** taşır.

## Kapsam

```
dotnet test tests/VidShrink.Tests/VidShrink.Tests.csproj -c Debug --no-build \
  --filter "FullyQualifiedName~IconsTests|FullyQualifiedName~IkonKutusuTests|FullyQualifiedName~IkonImzaTests"
```

Temel (mutasyonsuz): **106 test, 0 kırmızı.** Dağılımı: `IconsTests` 52, `IkonKutusuTests` 27,
`IkonImzaTests` 27. Kataloğda 25 simge var (`IconCamera` ve `IconMenu` ölü oldukları için
düşürüldü, bkz. `docs/tasarim/fluent-simge-eslemesi.md`).

## M1 — gövdeyi kutunun dışına taşır

`Icons.axaml`, `IconStop`: `M4.75 3C` → `M4.75 30C`.

**3 kırmızı / 106.** Her sınıftan bir tane:

```
IconsTests.UcOlcektenBirindeKirpilmiyor(ad: "IconStop")
IkonImzaTests.SekilKimligiPimiyleAyni(ad: "IconStop")
IkonKutusuTests.MurekkepOrtada(ad: "IconStop")
```

## M2 — gövdeyi boşaltır

`Icons.axaml`, `IconPlay`: sabitleyiciden sonraki gövdenin tamamı silinir
(`F1 M 0,0 M 24,24 ` kalır).

**4 kırmızı / 106:**

```
IconsTests.HicbirGeometriBosDegil(ad: "IconPlay")
IconsTests.UcOlcektenBirindeKirpilmiyor(ad: "IconPlay")
IkonImzaTests.SekilKimligiPimiyleAyni(ad: "IconPlay")
IkonKutusuTests.MurekkepOrtada(ad: "IconPlay")
```

## M3 — iki gövdeyi takas eder (şekil kimliği ölçüsünün varlık sebebi)

`IconStop` ile `IconMaximize` gövdeleri yer değiştirir. İkisinin sınır kutusu aynı
(3,00–21,00 × 3,00–21,00), biri dolu kare biri çerçeve.

| Kapsam | Test | Kırmızı |
|---|---|---|
| `FullyQualifiedName~IkonKutusuTests` | 27 | **0** |
| `FullyQualifiedName~IconsTests` | 52 | **0** |
| `FullyQualifiedName~IkonImzaTests` | 27 | **2** |

Ham kırmızı:

```
IconStop: gövde pimlenen şekil değil, 100 bit ayrışıyor.
ölçülen 000000001ff8300c20042004200420042004200420042004300c1ff800000000
pim      000000003ffc3ffc3ffc3ffc3ffc3ffc3ffc3ffc3ffc3ffc3ffc3ffc00000000
IconMaximize: gövde pimlenen şekil değil, 100 bit ayrışıyor.
ölçülen 000000003ffc3ffc3ffc3ffc3ffc3ffc3ffc3ffc3ffc3ffc3ffc3ffc00000000
pim      000000001ff8300c20042004200420042004200420042004300c1ff800000000
```

Ölçünün söylediği şey budur: sınır kutusu ve kırpılma ölçüleri takasa **kör**, şekil kimliği
değil. Bu yüzden `IkonImzaTests` eklendi.

## Kenar payı toleransı

`IkonKutusuTests.Margin` 1e-3 iken 25 simgenin 4'ü düşüyor:

| Simge | Kutunun dışına taşan kenar |
|---|---|
| `IconShrink` | sol 1,9961 · alt 22,0091 |
| `IconAbout` | üst 1,9990 · sağ 22,0031 · alt 22,0021 |
| `IconConvert` | üst 1,9980 |
| `IconRecorder` | sağ 22,0017 |

Gereken en küçük tolerans **0,0091**, yani yürürlükteki 0,01 gerçekten gerekli ve 0,005'e
sıkmak `IconShrink`'i düşürür. Sayı seçilmedi, ölçüldü.
