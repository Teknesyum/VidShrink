# Temayı Elle Boyamak

Programdaki bütün renkler tek bir dosyadan çıkar:

```
src/VidShrink.App/Themes/Palette/seeds.json
```

Her tema burada Teknesyum UI standardının **rolleriyle** duruyor. Ölçüler (boşluk, yuvarlaklık,
yazı boyu) `Themes/Theme.axaml` içinde ve palete dokunmuyor — renk değiştirince yerleşim yerinde kalır.

Bugün **36 palet** var: projenin 26'sı, standardın 9 teması ve varsayılan `Teknesyum`
(birleşimin kararları `docs/netlestirme/027-palet-birlesimi.md`). Ayarlardan seçilen palet
**çalışırken** yürürlüğe girer, yeniden başlatma istemez (`PaletteCatalog.Use`).

Açık/koyu ayrımı elle tutulmuyor: zemin renginin sRGB parlaklığı 0,5'in üstündeyse palet
açık sayılır. Bunun iki sonucu var — Fluent'in kendi açılır listeleri ve kaydırma
çubukları için `RequestedThemeVariant` Light'a geçer, ve başlık şeridi gövde yazısından
uzaklaşmak için koyulaşmak yerine açılır (`PaletteGen`'deki `Dim`, aynı 0,78 büyüklüğü
ters yönde).

İsteğe bağlı `atmos` rolü: verilirse zemin tonları ve parıltı katmanları
(`AtmosHot/Mid/Edge`) kor üçlüsü yerine bu renkten türer; parlaklık ember karışımınınkine
indirilir, gövde yazısı kontrastı düşmez. Yalnız Neon kullanıyor (yeşil, `docs/tasarim/fable-neon-yesil-2026-09-23.md`).
Vurgu gradyanı `AccentGradient` (accent1 → accent3) paletten değil `Theme.axaml`dan gelir.

## Roller

| Rol | Anahtar | Nerede görünür |
| --- | --- | --- |
| `black` | `AppBg` | Pencere zemini |
| `surface` | `SurfaceTone` | Kart ve panel yüzeyi |
| `renk-1` | `NeonBlue` | Birincil vurgu: düğme kenarı, odak halkası, ilerleme |
| `renk-2` | `NeonPink` | İkincil vurgu |
| `renk-3` | `NeonPurple` | Üçüncü vurgu |
| `renk-2-text` | `PinkText` | Hata yazısı |
| `success` | `NeonSuccess` | "Bitti" yeşili |
| `warning` | `EmberBlaze` | Uyarı, ateşin parlak ucu |
| `danger` (yoksa `renk-2`) | `NeonEmber` | Ateşin koyu ucu; beyaza, siyaha, zemine ve yüzeye karşı 3:1'i tutana dek en küçük adımla koyulaşır ya da açılır |
| `flame` (yoksa danger ile warning'in ortası) | `EmberFlame` | Ateşin ortası |
| `text` | `TextBody` | Ana yazı |
| `disabled` | `TextDisabled` | Sönük yazı, ipucu, devre dışı |

`renk-3-text` ve `glass-base` tohumda durur, anahtara inmez. `baslik` verilirse listede o
görünür (`Kagit` → `Sıcak Kâğıt`); palet adları çevrilmez. Geri kalan anahtarlar bunlardan
türetilir: dolgular `renk-1`'in `1A`/`33`/`4D` alfalı hâlleri, parıltılar `40` alfalı, perde
zeminin `CC` alfalısı, ateş şeridi zemin ile `danger`ın az oranlı karışımı. Aynı değer iki
anahtara düşerse sonraki anahtarın mavi kanalı bir birim kayar (`PaletteBuilder.Distinct`).

## Kendi Temanı Eklemek

1. `seeds.json`'a bir nesne ekle. `name` tek kelime, ASCII, baş harfi büyük
   (`BenimTema` listede `Benim Tema` görünür).
2. Aracı çalıştır:

```bash
dotnet run --project tools/VidShrink.PaletteGen
```

3. Adı `src/VidShrink.App/Themes/PaletteCatalog.cs` içindeki `Names` listesine, `baslik`
   verdiysen `Titles`'a yaz. Unutursan `PaletteTests` kırmızı yanar ve söyler.
4. `dotnet test --filter PaletteTests`.

Standardın temaları değişince tohumlar oradan yeniden alınır; projenin kendi tohumları
(`kaynak: vidshrink`) korunur:

```bash
dotnet run --project tools/VidShrink.PaletteGen -- import <temalar-klasörü> <benim.tokens.json>
```

Araç `Palette/<Ad>/Theme.axaml` dosyalarının hepsini silip yeniden yazar; o dosyaları elle
düzenleme, düzenlemen çekirdekte kalıcı olur.

Palet adı klasör adında, dosya adı her palette `Theme.axaml`: UI kılavuzunun `raw-colour`
kuralı ham rengi yalnız belirteç dosyası adına muaf tutuyor, kümesi kodda sabit ve
muafiyet yolu yok. Adres bu yüzden
`avares://VidShrink.App/Themes/Palette/<Ad>/Theme.axaml`.

## Yalnız Bir Rengi Denemek

Hızlı bakmak için palet dosyasındaki bir hex'i değiştirip programı başlatmak yeter;
ama bir dahaki üretimde silinir. Kalıcı olmasını istiyorsan `seeds.json`'a yaz.
