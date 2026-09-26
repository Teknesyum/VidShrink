# 027 — Palet Birleşimi: 26 + 10 = 36

Projenin 26 paleti ile Teknesyum UI standardının temaları tek şemada birleşti. Hiçbir palet
silinmedi. Yeni varsayılan `Teknesyum`; ayarında palet yazılı kullanıcı kendi seçiminde kalır.

## Sayılar

- **36 palet**: projenin 26'sı + standardın 9 teması (`templates/temalar/*.json`) + `Teknesyum`
  (`teknesyum-private/teknesyum-ui/benim.tokens.json`).
- **Eski 26 palette görünen renk farkı: 0.** Birleşimden önceki 26 dosyanın 910 renk satırı
  yeniden üretilenlerle tek tek karşılaştırıldı; değişen değer yok.
- **Standarttan gelen 10 palette metin kontrastı en düşük 8,41:1** (eşik 7:1). Ölçülen çiftler
  gövde, hata (`renk-2-text`), uyarı, başarı ve `renk-1` etiketi × zemin ve yüzey.
- **Dolgu üstündeki yazı (`OnNeon`) 15 dolguda 7:1'in altında**, en düşüğü 4,69:1 (Buz, `renk-2`).
  Hepsi AA 4,5'in üstünde; projenin AA borç listesi (CatppuccinLatte, GruvboxLight) değişmedi.

## Tek Şema

Her palet `seeds.json`'da standardın rolleriyle durur. Projenin 31 renk anahtarı bunlardan
`tools/VidShrink.PaletteGen` ile türer; eşleme `PaletteBuilder.cs:7-13`'te.

| Standart rol | Proje anahtarı |
| --- | --- |
| `renk-1` / `renk-2` / `renk-3` | `NeonBlue` / `NeonPink` / `NeonPurple` |
| `renk-2-text` | `PinkText` |
| `black` | `AppBg` |
| `surface` | `SurfaceTone` |
| `text` / `disabled` | `TextBody` / `TextDisabled` |
| `success` / `warning` | `NeonSuccess` / `EmberBlaze` |
| `danger` (yoksa `renk-2`) | `NeonEmber` |
| `flame` (yoksa danger ile warning'in ortası) | `EmberFlame` |

Kalan anahtarlar (dolgu, kenar, atmosfer, ateş şeridi, perde, parıltı) bunların alfalı ya da
karışık hâlleri. `renk-3-text` ve `glass-base` tohumda durur, anahtara inmez. Projenin eski
paletleri `danger`, `flame` ve (yalnız Neon) `atmos` alanlarını açıkça taşır; eski renkleri bu
yüzden birebir kaldı.

## Kararlar

1. **Ham değer değişmedi.** Standart temaların rolleri olduğu gibi alındı. `renk-2` ve `renk-3`
   dolguları bazı temalarda siyahla da beyazla da 7:1'e ulaşmıyor; standardın kuralı böyle
   dolguya `on: null` der (yazı taşımaz). Bu 15 dolgu `PaletKarsitligiTests.YaziTasimayanDolgular`
   listesinde adıyla pimli: listeye yeni ad düşmesi de, listedekinin düzelmesi de kırmızı.
2. **Aynı değer iki anahtarda olmaz.** Palet değişimi eski rengi yeni renge değerle eşler; aynı
   değeri taşıyan iki anahtar belirsiz sayılıp boyanmaz. Üreteç çakışan sonraki anahtarın mavi
   kanalını bir birim kaydırır (`PaletteBuilder.Distinct`). Kural yeni değil: eski 26 dosyada da
   aynı yerlerde duruyordu (dolgu kenarı, atmosfer, ember), renk farkının 0 çıkması bunu da
   doğrular. 36 paletin hepsinde en az bir kayma var. Çekirdek rolün kendisinin kaydığı yerler:
   zemin Teknesyum ve Keskin'de (`surface` = `black`); uyarı yedi palette (Nord, Gruvbox,
   GruvboxLight, Solarized, SolarizedLight, Everforest, Synthwave; `warning` = `renk-2`); başarı
   beş palette (AyuLight, GithubLight, MaterialOcean, OneDark, RosePine); `OnNeon` beyazı
   GithubLight, Kar, Keskin ve Kırık'ta, siyahı Teknesyum'da. Kayma en çok 2 birim, göze görünmez.
3. **Varsayılan Teknesyum.** `App.axaml` onu birleştirir, `PaletteCatalog.Default` onu söyler.
   Ayar dosyasında `theme` yazılıysa o açılır; boş ya da dosya yoksa Teknesyum
   (`PaletteTests.KayitliSecimKorunurBosAyarVarsayilanaDuser`).
4. **Palet adları çevrilmez.** Özel ad sayıldı. Klasör adı ASCII (`Kagit`), görünen ad
   `seeds.json`'daki `baslik` (`Sıcak Kâğıt`). Arka kod taraması (`LanguageTests.ArkaKoddaCumleKalmadi`)
   bu başlıkları muaf tutar; muafiyet `seeds.json`'dan okunur, yalnız `PaletteCatalog.cs`'te ve
   yalnız tam eşleşmede geçer.
5. **Standardın "yalnız koyu tema" kuralı uygulanmadı.** Projede açık zeminli 6 palet vardı ve
   silinmiyor; standardın açık temaları da (Buz, Kağıt, Kar, Keskin, Kırık) alındı. Sıra:
   varsayılan, koyu paletler, açık paletler; iki grupta önce projenin, sonra standardın.
6. **Kontrast gediği kapatılmadı.** AyuLight'ın 2,65:1'lik gediğine dokunulmadı.

## Bulgu

Neon'a özgü iki ölçü (alev rampasının kararma sırası, iki sıcak tonun kırmızıdan türemesi, yeşil
atmosfer) varsayılanı okuyordu. Varsayılan Teknesyum olunca Neon'u adıyla okuyor. Kararma sırası
36 paletin 22'sinde tutmuyor (Teknesyum dahil): atmosferin kenarı sıcak tabandan açık. Bu bir
tasarım kuralı değil, Neon'un kendi tasarımıydı.

## Yeniden Üretim

```powershell
dotnet run --project tools/VidShrink.PaletteGen
dotnet run --project tools/VidShrink.PaletteGen -- import <temalar-klasörü> <benim.tokens.json>
```

`PaletteTests.PaletDosyalariTohumdanYenidenUretilir` 36 dosyanın üreteçle bayt bayt aynı
olduğunu sınar (satır sonu normalleştirilir).
