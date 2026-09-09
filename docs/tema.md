# Temayı Elle Boyamak

Programdaki bütün renkler tek bir dosyadan çıkar:

```
src/VidShrink.App/Themes/Palette/seeds.json
```

Her tema burada **on bir hex** ile duruyor. Ölçüler (boşluk, yuvarlaklık, yazı boyu)
`Themes/Theme.axaml` içinde ve palete dokunmuyor — renk değiştirince yerleşim yerinde kalır.

## On Bir Çekirdek

| Alan | Nerede görünür |
| --- | --- |
| `bg` | Pencere zemini, en koyu ton |
| `surface` | Kart ve panel yüzeyi |
| `accent1` | Birincil vurgu: düğme kenarı, odak halkası, ilerleme |
| `accent2` | İkincil vurgu |
| `accent3` | Üçüncü vurgu |
| `success` | "Bitti" yeşili |
| `ember` | Sıkıştırma ateşinin koyu ucu |
| `flame` | Ateşin ortası |
| `blaze` | Ateşin parlak ucu |
| `textBody` | Ana yazı |
| `textDim` | Sönük yazı, ipucu, devre dışı |

Geri kalan 21 anahtar bunlardan türetilir: dolgular `accent1`'in `1A`/`33`/`4D` alfalı
hâlleri, parıltılar `40` alfalı, perde `bg`'nin `CC` alfalısı, ateş şeridi `bg` ile
`ember`in az oranlı karışımı.

## Kendi Temanı Eklemek

1. `seeds.json`'a bir nesne ekle. `name` tek kelime, ASCII, baş harfi büyük
   (`BenimTema` listede `Benim Tema` görünür).
2. Aracı çalıştır:

```bash
dotnet run --project tools/VidShrink.PaletteGen
```

3. Adı `src/VidShrink.App/Themes/PaletteCatalog.cs` içindeki `Names` listesine yaz.
   Unutursan `PaletteTests` kırmızı yanar ve söyler.
4. `dotnet test --filter PaletteTests`.

Araç `Palette/*.axaml` dosyalarının hepsini silip yeniden yazar; o dosyaları elle
düzenleme, düzenlemen çekirdekte kalıcı olur.

## Yalnız Bir Rengi Denemek

Hızlı bakmak için palet dosyasındaki bir hex'i değiştirip programı başlatmak yeter;
ama bir dahaki üretimde silinir. Kalıcı olmasını istiyorsan `seeds.json`'a yaz.
