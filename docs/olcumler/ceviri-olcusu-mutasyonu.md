# T90 — Çeviri ölçüsü mutasyonları

Durum: **tamamlandı — 2026-09-01.**

## C# interpolasyon sabit parçası

`ControlStrip.axaml.cs` içindeki zaman göstergesi interpolasyonuna geçici olarak `Embedded mutation text` sabit parçası eklendi. `LanguageTests.ArkaKoddaCumleKalmadi` kırmızı oldu ve şu ihlali verdi:

`Playback\ControlStrip.axaml.cs içinde anahtardan gelmeyen cümle var: Embedded mutation text /`

Mutasyon geri alındı. Bu sonuç, taramanın interpolasyonun yalnız ifadelerini değil kullanıcıya çıkan sabit parçalarını da gördüğünü kanıtlıyor.

## XAML öğe gövdesi

`MainWindow.axaml` içindeki `TaglineLead` geçici olarak öznitelik bağından çıkarılıp `<TextBlock>Embedded body mutation</TextBlock>` biçiminde düz gövde metnine çevrildi. `LanguageTests.BicimlemedeKullaniciyaGorunenDuzMetinKalmadi` kırmızı oldu ve şu ihlali verdi:

`MainWindow.axaml içinde anahtardan gelmeyen metin var: >Embedded body mutation<`

Mutasyon geri alındı. Ölçü artık hem kullanıcı metni taşıyan XAML özniteliklerini hem öğe gövdelerini tarıyor.

## Tanınmayan girdinin kararı

`LanguageCatalog.Validation` tanınmayan motor iletisini artık sessizce İngilizce göstermiyor; `main.validation.untranslated-engine` anahtarıyla açıkça etiketliyor. Aşama satırındaki tanınmayan motor/ffmpeg metni ise teşhis değerini kaybetmemesi için ham biçimde korunuyor; `TaninmayanAsamaHamMotorMetniOlarakKorunur` testi bu bilinçli düşürme yolunu sabitliyor.

## Doğrulama

Sözleşme filtresi:

`LocalizationTests|LanguageTests|PlaybackTests: 37 başarılı, 0 başarısız`

İlk tam süit, T90 kapsamı dışındaki ve T86 tur 2 sözleşmesinde açıkça kayıtlı süreçler arası geçici-dizin yarışında kırmızı oldu:

`Başarısız: 1, Başarılı: 976, Atlanan: 23, Toplam: 1000, Süre: 19 m 25 s`

Kırılan `PerformanceCheckTests.OlcumArtikBirakmiyor` ölçüsü tek başına art arda üç kez çalıştırıldı ve `1 başarılı, 0 başarısız` × 3 verdi. Makine sakin durumdayken tam süit yeniden kapıdan geçirildi:

`Başarısız: 0, Başarılı: 977, Atlanan: 23, Toplam: 1000, Süre: 18 m 28 s`

Kapı sonucu: `başarısız=0 toplam=1000 alt-sınır=974`.
