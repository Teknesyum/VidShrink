# Plan — Tüm Diller, Dil Seçici Ve Yirmi Hazır Tema

Kullanıcının isteği (2026-09-09): tüm diller; üstteki EN/TR kısayolu kalsın, sağına bir
ayar tekerleği gelsin ve ayarlardaki dil bölümüne götürsün; oradan istenen dil seçilsin.
Ayrıca yirmi hazır tema ayarlardan seçilebilsin, tema özellikleri dil gibi ayrı bir yerde
saklansın.

## Sıra Ve Gerekçesi

Kullanıcı sırayı dilden başlatıyor, ama üst şeritteki düğme listesi bugün
`Locales` altındaki **her** dil için bir düğme basıyor. Yüz dil klasörü eklendiği anda üst
şeride yüz düğme dizilir. Bu yüzden seçici önce yazılır; dil klasörleri ondan sonra dolar.

1. **A — Dil seçici.** Üst şeritte yalnız EN/TR + tekerlek. Tekerlek Ayarlar sekmesine
   geçip dil bölümünü gösterir. Ayarlarda tam liste açılır kutuda.
2. **B — Yirmi tema.** Palet dosyaları, ayarda seçim, seçimin kalıcılığı, çalışırken değişim.
3. **C — Diller.** Küme küme çeviri, her küme kendi commit'i.

## A — Dil Seçici

| Dosya | Değişiklik |
|---|---|
| `MainWindow.axaml:179` | `LangSwitch` yalnız kısayol dilleri + tekerlek düğmesi |
| `MainWindow.axaml:1042` | `SettingsLangSwitch` yerine tam liste açılır kutusu |
| `MainWindow.axaml.cs:574` | `BuildLanguageSwitch` ikiye ayrılır: kısayol / tam liste |
| `Locales/*/settings-tab.json` | tekerlek için ad, açılır kutu için etiket |

Kısayol kümesi kodda sabit iki ad değil: `Strings.ShortcutLanguages` — bugün `en`, `tr`.

## B — Yirmi Tema

Palet bugün `Themes/Palette/Neon.axaml`, `App.axaml` hangisinin yürürlükte olduğunu
bildiriyor. Yirmi palet aynı klasöre girer, aynı 32 anahtarı taşır.

- Seçim `AppSettings.Theme` alanında saklanır (dil `settings.json`'da; tema da orada).
- Çalışırken değişim: `App.Current.Resources.MergedDictionaries` içindeki palet sözlüğü
  yenisiyle değiştirilir. Ölçüler ve fırça tanımları yerinde kalır.
- Açılış görüntüsü `App.axaml`'deki **varsayılan** paletten üretilmeye devam eder.
- Ölçü: her palet aynı anahtar kümesini taşır (eksik anahtar = boş ekran), ve hiçbir
  paletin metni ekrana çıkmaz.

## C — Diller

Bir dil = `Locales/<kod>/` altında dört dosya, 475 anahtar, ~4976 sözcük.
Ölçüm: `docs/olcumler/dil-ekleme-fiyati.md`.

Küme küme yazılır; her küme kendi commit'inde ve `dotnet test` yeşil kalır. Sığdırma
turu (`TipOverflowTests`) bugün yalnız EN/TR ölçüyor; yeni dillerde balon taşması
göründükçe metin kısaltılır, ölçü genişletilir.

## Kalan İki Dilli Dikiş

`Playback/PlayerView.axaml.cs:305` — `fault.ReasonTr` / `ReasonEn`. Motor iletisi
anahtara taşınacak; C adımından önce kapanır.
