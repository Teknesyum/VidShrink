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
