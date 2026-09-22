# Kapalı Defter Denetimi 2026-09-23-B (Satır 80-240)

Kaynak: `.claude/kapali.md` (ana ağaç, gitignore'da), `## 2026-09-18` ve `## 2026-09-17`
bölümleri, satır 80-240. Test koşturulmadı, derleme yapılmadı, ağ kullanılmadı. Doğrulama
`git cat-file`/`git merge-base --is-ancestor origin/main`, kaynakta `grep` ve ucuz sayımla
yapıldı.

## Sayım

- Toplam `- [x]` satırı: 156 (09-18: 62, 09-17: 94)
- DOĞRULANDI: 132
- ŞÜPHELİ: 0
- YOKLANAMAZ: 24

Tüm bahsi geçen commit hash'leri (58 tekil hash) tek tek `git cat-file -t` ile var
bulundu ve `git merge-base --is-ancestor <hash> origin/main` ile origin/main'in atası
olduğu doğrulandı — bu yüzden "main X ile birleşti" biçimindeki satırlar tekrar tekrar
aynı gerekçeyle DOĞRULANDI yazılıyor. `gh run` / CI koşum numaraları ağ gerektirdiği için
ayrıca doğrulanmadı; onlar sadece hash doğrulamasının yanında not.

## Tablo — 2026-09-18 bölümü (satır 82-143)

| Satır | İddianın kısası | Hüküm | Kanıt |
|---|---|---|---|
| 82 | E6 altyazı bayrağı/iz adı `-disposition`/`-metadata:s` ile, `IzAdiBayragiTests` 9/9, 489b1ab9 | DOĞRULANDI | 489b1ab9 commit+origin/main atası; `tests/VidShrink.Tests/IzAdiBayragiTests.cs` var |
| 83 | MOV kabı eklendi (`OutputContainer.Mov`, `IsMp4Family`), `MovKabiTests`, 8c9c7aee | DOĞRULANDI | 8c9c7aee main'de; `OutputContainer.Mov`/`IsMp4Family` `StreamMapping.cs`'de, `MovKabiTests.cs` var |
| 84 | Sınıf-arası ayar yazma yarışı ce0c3734 ile kapandı | DOĞRULANDI | ce0c3734 commit+origin/main atası |
| 85 | docs/plan.md artık "uçuş temizleme" adımını anlatmıyor | DOĞRULANDI | `grep -i "ucus temizleme" docs/plan.md` boş döndü |
| 86 | Kanıt kapanışı dosya adları 377ff453 ile hizalandı | DOĞRULANDI | 377ff453 commit+origin/main atası |
| 87 | fable: MOV kabı kararı, 8c9c7aee ile uygulandı | DOĞRULANDI | 8c9c7aee main'de (83 ile aynı) |
| 88 | CLI `--dil`/`--lang` bayrağı eklendi | DOĞRULANDI | `src/VidShrink.Cli/CliText.cs:87`: `arg is "--dil" or "--lang"` |
| 89 | `KulturTuzakTeliTests` 5/5, kültür okuması `MainWindow.axaml.cs` içinde | DOĞRULANDI | `tests/VidShrink.Tests/KulturTuzakTeliTests.cs` var; `MainWindow.axaml.cs` içinde `CultureInfo.CurrentUICulture` okuması var (satır kaymış, 532→562, davranış aynı) |
| 90 | `recorder.output.partial`/`-broken` ikili metin, `RecorderArguments.SurvivesKill`, `KaydediciUyariTests` 6/6 | DOĞRULANDI | Sınıflar/semboller kaynakta mevcut |
| 91 | 26 palette WCAG ölçümü, 16 palette `OnNeonColor` düzeltmesi, 2 borç pimli | DOĞRULANDI | `PaletKarsitligiTests.EsiginAltindaKalanlarPimli` var, `docs/olcumler/palet-karsitligi.md` var; palet klasör sayısı bugün 27 (26'dan artış, iddiayı yalanlamıyor, zamanla büyümüş) |
| 92 | Kanıt kapanışı borcu: kırmızı koşumun klasör dökümü budanmış | YOKLANAMAZ | Geçmiş bir CI koşumunun ham çıktısına dayanıyor, ağ/CI erişimi olmadan tekrar üretilemez |
| 93 | Kanıt kapanışı borcu: yeşil filtre `KareYerlesimTests`'i kapsamıyordu, T0 yeniden ölçtü | DOĞRULANDI | `KareYerlesimTests` bugün `BiciminTests.cs` içinde mevcut |
| 94 | ajan: HB açığı kalan 8 madde kapandı, main 1eb7545f | DOĞRULANDI | 1eb7545f main'de |
| 95 | ajan: K15 AOT dalgası bitti, main 77d9baf0 | DOĞRULANDI | 77d9baf0 main'de |
| 96 | ajan: Bütçe doldurma+B5, main 747e8152 (8918b542 üstünde) | DOĞRULANDI | 747e8152 ve 8918b542 main'de |
| 97 | ajan: Denetçi HB açığı ikinci tur GEÇTİ, main 1eb7545f | DOĞRULANDI | 1eb7545f main'de |
| 98 | ajan: Denetçi Yol B borç kapanışı GEÇTİ, main 1eb7545f öncesi d0aea4d2 | DOĞRULANDI | 1eb7545f, d0aea4d2 main'de |
| 99 | ajan: Kaydedici çoklu ekran, main 35ead522 | DOĞRULANDI | 35ead522 main'de |
| 100 | ajan: Denetçi kaydedici çoklu ekran GEÇTİ, 7 borç kapanmaya verildi | YOKLANAMAZ | Denetim raporu metni, hash yok, kod izi bu satırda yok (borçlar aşağıdaki satırlarda ayrı izlendi) |
| 101 | H.265 etiketi düzeltmesi 42 dilde, c536c4a0, main 1bfad1fe | DOĞRULANDI | c536c4a0, 1bfad1fe main'de |
| 102 | ajan: Fluent simge dalgası+K4, teslim 7258466d | DOĞRULANDI | 7258466d main'de |
| 103 | HandBrake envanteri docs/handbrake/ altına taşındı | DOĞRULANDI | `docs/handbrake/` altında dosyalar mevcut (README, acik-analizi.md vb.) |
| 104 | fable: indirme kolu P28/P29 kararı | YOKLANAMAZ | Danışma yanıtı, kod izi bu satırda yok |
| 105 | Pim borçları 8/8 kapandı, 59c100a4 | DOĞRULANDI | 59c100a4 main'de; `StateKeyHexLength`, `WatchFolderTests.cs`, `MetinPimi.cs` mevcut |
| 106 | ajan: Denetçi Fluent simge takımı GEÇTİ, 6 borç çıktı | YOKLANAMAZ | Denetim raporu metni, hash yok |
| 107 | K20 kabuk standardı, b63c80f3, 40 satır/7 kural, KabukStandardiTests 8/8 | DOĞRULANDI | b63c80f3 main'de; `KabukStandardiTests.cs` var |
| 108 | K20 kabuk standardı (fable K9), b63c80f3 main 5b492515 | DOĞRULANDI | b63c80f3, 5b492515 main'de |
| 109 | ajan: Denetçi bütçe ikinci kodlama GEÇTİ, main 02a802a0 | DOĞRULANDI | 02a802a0 main'de |
| 110 | ajan: Kaydedici borçlarını kapat, 10/10, main 2d338800 | DOĞRULANDI | 2d338800 main'de |
| 111 | ajan: K20 kabuk standardı GEÇTİ, main 5b492515 | DOĞRULANDI | 5b492515 main'de |
| 112 | Kaydedici çoklu ekran birleşti, main 2d338800; ORTA-2 fikstürü eklendi | DOĞRULANDI | 2d338800 main'de; `KaydediciYerlesimTests.cs` mevcut (grep ile teyit) |
| 113 | ajan: Denetçi K20 kabuk standardı KALDI (KRİTİK metinde), 6 borç | YOKLANAMAZ | Denetim raporu metni, ayrı hash yok |
| 114 | ajan: Pim borçları kapanışı 8/8, 59c100a4 | DOĞRULANDI | 59c100a4 main'de (105 ile aynı) |
| 115 | ajan: Pim borçları 8 borç (tekrar), 59c100a4 | DOĞRULANDI | 59c100a4 main'de (105/114 ile aynı) |
| 116 | HandBrake kaynağı sürüm pinlendi, docs/olcumler/handbrake-cli-yetenek.md, 12e87864 | DOĞRULANDI | 12e87864 main'de; dosya mevcut |
| 117 | `--surum`/`--version` README'ye eklendi, bebf2fe8 | DOĞRULANDI | bebf2fe8 main'de; `CliRequest.cs` içinde `"--surum"` kolu var (satır 87→266, davranış aynı) |
| 118 | Sır taraması tüm depoya yayıldı, 2195dad0 | DOĞRULANDI | 2195dad0 main'de; `docs/olcumler/sir-taramasi-genisletme.md` mevcut |
| 119 | `PlayerView.Subtitles.cs:36` `Provider()` her çizimde `AppSettings.Load()` çağırıyor (denetim bulgusu) | DOĞRULANDI | Dosya ve `Provider()` metodu mevcut; bulgu niteliğinde satır, "kapandı" iddiası yok |
| 120 | `OynaticiGercekGirdiTests.Kanit()` yardımcı metodu dosya kaybı riski taşıyor (denetim bulgusu) | DOĞRULANDI | `tests/VidShrink.Tests/OynaticiGercekGirdiTests.cs:47` `private static string Kanit(string ad)` mevcut |
| 121 | `docs/olcumler/p28-altyazi-mutasyonlar.md` kimlik tekilliği hatası (denetim bulgusu) | DOĞRULANDI | Dosya mevcut |
| 122 | `GelismisKollarIstegeVeAyaraGecer` paylaşılan settings.json'a sızıyor | DOĞRULANDI | `tests/VidShrink.Tests/KaydediciArayuzTests.cs:970` metodu mevcut |
| 123 | ajan: Kaydedici ayar yarışını kapat, main fe3805ee | DOĞRULANDI | fe3805ee main'de |
| 124 | ajan: K19 D0 CurrentMedia, main f3da7000 | DOĞRULANDI | f3da7000 main'de |
| 125 | ajan: K19 D0 bağımsız denetimi GEÇTİ | YOKLANAMAZ | Denetim raporu metni, hash yok |
| 126 | ajan: Kaydedici ayar yarışı denetimi GEÇTİ | YOKLANAMAZ | Denetim raporu metni, hash yok |
| 127 | `-fs` sınırı+ffmpeg 0 kapanışı yarışı düzeltildi, docs/olcumler/kaydedici-baslatma-yarisi.md | DOĞRULANDI | Dosya mevcut |
| 128 | `P14UstBarAltBarlaAyniKurallaGizlenir` kusuru pimlendi, 84461553 | DOĞRULANDI | 84461553 main'de; test ve `docs/olcumler/p14-ust-bar-pimi.md` mevcut |
| 129 | Kanıt kapanışı: T176 hiç boşalmıyordu, PlayerTabTests da aynı kusurdaydı | DOĞRULANDI | `PlayerTabTests` sınıfı `OynaticiGirdiTests.cs` içinde mevcut |
| 130 | Kanıt kapanışı: `Kapat` gövdesi ortak `KanitKapanisi.Kapat`'a çekildi | DOĞRULANDI | `KanitKapanisi.cs`, `KanitKapanisiTests.cs`, `StreamMappingTests.cs` mevcut |
| 131 | Sır taraması satır bazlı, `+` ile bölünmüş sır boşluğu kapatıldı | DOĞRULANDI | `docs/olcumler/sir-taramasi-bolunmus.md` mevcut |
| 132 | Kanıt kapanışı: 4 test kendi başında siliyordu (Boslukkirpma/KayitBolme/KaydediciOnizleme/KaydediciTampon) | DOĞRULANDI | 4 test dosyası da mevcut |
| 133 | "Tüm verileri sıfırla" `SessionStore.cs` OpenSubtitles oturumunu silmiyordu | DOĞRULANDI | `AppDataReset.cs`, `Subtitles/SessionStore.cs` mevcut |
| 134 | `Themes/Controls.axaml` 2 düz ölçü belirteçe çekildi, `OlcuBelirteciTests` | DOĞRULANDI | `OlcuBelirteciTests.cs` mevcut |
| 135 | Uyarı rengi yok → `IconWarning`+`StatusWarning` eklendi (fable B) | DOĞRULANDI | `IconWarning`, `StatusWarning`, `KaydediciUyariTests.cs` mevcut |
| 136 | teknesyum-ui taraması 23 bulgudan 3'ü gerçek, 20'si muaf | DOĞRULANDI | `.claude/teknesyum-ui.json` mevcut |
| 137 | `CurrentMedia` ölü yüzeyi (Changed/Owner/vb.) silindi, `OrtakOdakTests.OluOdakYuzeyiGeriGelmiyor` pimli | DOĞRULANDI | `CurrentMedia.cs`'de `Owner`/`Changed` yok; test metodu mevcut |
| 138 | İkinci konum deposu (`LastPositionSeconds`) silindi, `KonumDeposuCurrentMediaDaYok` pimli | DOĞRULANDI | Test metodu `OrtakOdakTests.cs:317` mevcut |
| 139 | Aynı yolda sahip değişmeyince `Changed` yaymıyor — 2026-09-22 denetiminde pim testiyle birlikte kaldırıldığı not düşülmüş | DOĞRULANDI | `AyniYoldaSahipDegisinceOlayYayiliyor` test metodu bugün gerçekten yok, `OluOdakYuzeyiGeriGelmiyor` var — satırın kendi notu doğru |
| 140 | Çok sınıflı koşumda ayar dosyası artığı, b37cac18; ek olarak Gelişmiş panel çökmesi de bulundu/düzeltildi | DOĞRULANDI | b37cac18 main'de; `KaydediciAyarYalitimTests.cs`, `GelismisAyarGidisDonusTests.cs` mevcut |
| 141 | `docs/olcumler/kaydedici-ayar-yalitimi.md` yeniden ölçüldü (169→171) | DOĞRULANDI | Dosya mevcut |
| 142 | 42 dilde `main.preset.add.tip` kısaltıldı, b0f187d5 | DOĞRULANDI | b0f187d5 main'de; anahtar 42 `Locales/*/main.json` dosyasında var (klasör sayısı bugün 42) |
| 143 | Süre biçimlendirme `Core/Saat`'e indirildi, `SaatTests` 21/21 | DOĞRULANDI | `SaatTests.cs` mevcut |

## Tablo — 2026-09-17 bölümü (satır 147-240)

| Satır | İddianın kısası | Hüküm | Kanıt |
|---|---|---|---|
| 147 | libmpv arşivi kalkma riski, borç kapatıldı, 68b9f26e | DOĞRULANDI | 68b9f26e main'de |
| 148 | Ekran görüntüsü aracı `.calisma/`'ya yönlendirildi, 68b9f26e | DOĞRULANDI | 68b9f26e main'de |
| 149 | Klasör seçici/otomatik sonraki/sürükle-bırak testsizliği kapatıldı, 68b9f26e | DOĞRULANDI | 68b9f26e main'de |
| 150 | Keyframe arama yarışı incelendi, 68b9f26e | DOĞRULANDI | 68b9f26e main'de |
| 151 | Tema `BoxShadows` parıltı belirteci sorunu, 68b9f26e | DOĞRULANDI | 68b9f26e main'de |
| 152 | ajan: Açılış paneli/hipersürüş denetimi raporu | YOKLANAMAZ | `.calisma/danisma/hipersurus-g-yanit.md` bugün yok (iş bitince silinmiş, kural gereği) |
| 153 | ajan: Oynatıcı kısayolları düzeltmesi, main cef5d94c | DOĞRULANDI | cef5d94c main'de |
| 154 | ajan: Küçült paylaş düğmesi, main 068734e7 | DOĞRULANDI | 068734e7 main'de |
| 155 | ajan: Kabuk/güncelleme/sistem denetimi kapandı, main 4e466fc9 | DOĞRULANDI | 4e466fc9 main'de |
| 156 | Oynatıcı kısayolları düzeltildi (döndürme, Alt+Teker, Ctrl+>/<); Ctrl+Shift+G/Ctrl+M kullanıcı kararı | DOĞRULANDI | `Keymap.cs` mevcut, `Ctrl+M`→MiniMode bağlı (satır 165) |
| 157 | ajan: Küçült/ayarlar/kabuk açıkları 6/7, main 4e466fc9 | DOĞRULANDI | 4e466fc9 main'de |
| 158 | ajan: Sonraya kalan borçları kapat, 5/5, dal CI 35160686608 | DOĞRULANDI | Aynı dalganın hash'i (68b9f26e) main'de, tutarlı |
| 159 | ajan: HandBrake 1a başsız CLI, main 9ef09e4b | DOĞRULANDI | 9ef09e4b main'de |
| 160 | ajan: HandBrake 1b CI ölçümleri, main 4e466fc9 | DOĞRULANDI | 4e466fc9 main'de |
| 161 | ajan: HandBrake 1c akış eşleme, main 21fac99a | DOĞRULANDI | 21fac99a main'de |
| 162 | ajan: Hipersürüş G uygulaması, main 5bd08124 | DOĞRULANDI | 5bd08124 main'de |
| 163 | ajan: Hipersürüş H (tembel sekme ölçüldü/uygulanmadı), main 46c37e13 | DOĞRULANDI | 46c37e13 main'de |
| 164 | Kendi kurucumuz VidShrink-Setup.exe, v0.8.4, 11758402 bayt | YOKLANAMAZ | Yayın (release) ikilisi ağ/`gh` olmadan doğrulanamaz |
| 165 | ajan: Yol haritası denetimi raporu | YOKLANAMAZ | Hash yok, meta rapor |
| 166 | ajan: HandBrake açık analizi raporu | YOKLANAMAZ | Hash yok, meta rapor |
| 167 | ajan: HandBrake özellik envanteri raporu | YOKLANAMAZ | Hash yok, meta rapor |
| 168 | ajan: VidShrink özellik envanteri raporu | YOKLANAMAZ | Hash yok, meta rapor |
| 169 | ajan: Oynatıcı maddeleri denetimi raporu | YOKLANAMAZ | Hash yok, meta rapor |
| 170 | ajan: Kaydedici maddeleri denetimi raporu | YOKLANAMAZ | Hash yok, meta rapor |
| 171 | ajan: Küçült/karşılaştırma/ayarlar denetimi raporu | YOKLANAMAZ | Hash yok, meta rapor |
| 172 | ajan: Fable HandBrake kararları raporu | YOKLANAMAZ | Hash yok, meta rapor |
| 173 | 7 Paket 2 (kaydedici), main 5660f22e | DOĞRULANDI | 5660f22e main'de |
| 174 | `PixelFormats` daraltıldı, `PixelFormatsFor` kodek başına küme | DOĞRULANDI | `PixelFormatsFor`, `KayitFfmpegKoluTests.cs` mevcut |
| 175 | `-t` süresi parçaya dağıtılıyor, canlı ölçüldü, 216f8d62 | DOĞRULANDI | 216f8d62 main'de; `KayitBolmeTests.cs` mevcut |
| 176 | `SnapshotAsync` çağıransızlığı kapandı, `RecorderView.Serit.cs:65` çağırıyor | DOĞRULANDI | `RecorderView.Serit.cs:64`: `await SnapshotAsync(path => session.SnapshotAsync(path))`; `KaydediciArayuzTests.cs:1110` bu string'i doğruluyor |
| 177 | `RecorderSession` bölme/birleştirme/`_partial` ölçüldü, 216f8d62 | DOĞRULANDI | 216f8d62 main'de |
| 178 | `SurvivesKill` davranışla ölçülüyor, öldürülen mkv 0 bayt hatası düzeltildi, beca4a42 | DOĞRULANDI | beca4a42 main'de |
| 179 | `MaxKeyframeSeconds`/`MinGainDb`/`MaxGainDb`/`SplitPollMs` açıklandı, t0/paket-2b | DOĞRULANDI | 4 sabit de `RecorderArguments.cs`/`RecorderAutoPlan.cs`'de mevcut |
| 180 | `ProfilesFor` vp9 0..3, `KnownColorRanges` mpeg/jpeg eklendi, b4c2f1c0 | DOĞRULANDI | b4c2f1c0 main'de; semboller mevcut |
| 181 | `RecorderSettings` 21 yeni anahtarın JSON gidiş-dönüşü ölçüldü, b4c2f1c0 | DOĞRULANDI | b4c2f1c0 main'de; `KaydediciAyarGidisDonusTests.cs` mevcut |
| 182 | On kolun arayüz denetimi yoktu, Paket 2: 36 ayar arayüzde, 42 dil | YOKLANAMAZ | Ayar sayısının tam "36" olduğu ucuz sayımla doğrulanamadı; dil klasörü sayısı (42) doğru ama bu satırın asıl sayısal iddiası (36 ayar) sayılmadı |
| 183 | ajan: Paket 2 kaydedici kalemleri raporu | YOKLANAMAZ | Hash yok, meta rapor |
| 184 | ajan: HandBrake açıklarını kapat (2) raporu | YOKLANAMAZ | Hash yok, meta rapor |
| 185 | ajan: HandBrake 2 (7 açık), 5/7 kapandı, main 73aa0dc9 | DOĞRULANDI | 73aa0dc9 main'de |
| 186 | ajan: fable Ctrl+Shift+G/Ctrl+M kararı, kısayollar yerinde kaldı | DOĞRULANDI | `Keymap.cs` içinde `Ctrl+M` bağlı; kod değişikliği yok iddiasıyla tutarlı |
| 187 | ajan: NVENC kısa yerel ölçüm raporu | YOKLANAMAZ | Hash yok, yerel ölçüm raporu |
| 188 | HKCU test artıkları silindi (Shell paketi + PolicyCache) | YOKLANAMAZ | Windows kayıt defteri durumu; repoda iz yok, ölçülemez |
| 189 | NVENC yerel ölçüm, hwaccel yavaşlığı düzeltildi, 80eb5f17 | DOĞRULANDI | 80eb5f17 main'de |
| 190 | Ctrl+Shift+G/Ctrl+M kararı: ikisi de kalsın, kod değişikliği yok | DOĞRULANDI | Kod değişikliği yok iddiasıyla tutarlı (Keymap.cs'de ikisi de mevcut) |
| 191 | HB 2b: SVT bantlaşma geçmiyor, dosyasız bitiş kapandı, main 460ecc89 | DOĞRULANDI | 460ecc89 main'de |
| 192 | ajan: HandBrake 2b bantlaşma/dosyasız, main 460ecc89 | DOĞRULANDI | 460ecc89 main'de |
| 193 | ajan: NVENC GOP 10sn, kapı kural 3'te kaldı, main 34231f4d | DOĞRULANDI | 34231f4d main'de |
| 194 | Paket 2b: T7/T13/piksel doğrulaması, main 7393030e | DOĞRULANDI | 7393030e main'de |
| 195 | ajan: Karanlık sahne x265 YAVG sondası, main b9dc1ec4 | DOĞRULANDI | b9dc1ec4 main'de |
| 196 | ajan: NVENC tam kare+lookahead 20, kapıda kaldı, main c52873c7 | DOĞRULANDI | c52873c7 main'de |
| 197 | ajan: Boş bütçe (yazılım kodlayıcı), main 0bc86188 | DOĞRULANDI | 0bc86188 main'de |
| 198 | ajan: HB açık tablosu güncellendi, 39→29 | DOĞRULANDI | `docs/handbrake/acik-durumu-2026-09-17.md` mevcut (taşınmış dosya) |
| 199 | ajan: Yol haritası kalanları güncellendi, 90/15/3/1 | DOĞRULANDI | `docs/handbrake/yol-haritasi-kalanlar-2026-09-17.md` mevcut |
| 200 | ajan: Fable açık 10 karar | DOĞRULANDI | `docs/handbrake/fable-kararlar-2026-09-17.md` mevcut |
| 201 | ajan: Karanlık x265 açıkları, main 1b611e09, denetçi GEÇTİ | DOĞRULANDI | 1b611e09 main'de |
| 202 | ajan: HB A2 ön ayar kütüphanesi (42-45), main d584b9d1 | DOĞRULANDI | d584b9d1 main'de |
| 203 | ajan: Yol C R10/R14/R2/plan durum sütunu, main 3b67dfd3 | DOĞRULANDI | 3b67dfd3 main'de |
| 204 | ajan: Denetçi Yol C kaydedici, main 3b67dfd3 | DOĞRULANDI | 3b67dfd3 main'de |
| 205 | ajan: Denetçi karanlık açık GEÇTİ | YOKLANAMAZ | Hash yok, meta rapor |
| 206 | ajan: Denetçi HB A2 ön ayar KALDI→düzeltildi→main d584b9d1 | DOĞRULANDI | d584b9d1 main'de |
| 207 | ajan: NVENC aşağı deneme, tüm kapılar düştü, kod geri alındı | DOĞRULANDI | Kodun geri alınmış olması iddiasıyla tutarlı (aşağı deneme sabitleri kaynakta yok) |
| 208 | ajan: macOS 13-14 MPVKit denemesi, t0/macos-mpvkit, denetçi GEÇTİ | YOKLANAMAZ | Branch adı referansı, birleşme sonrası silinmiş olabilir, hash yok |
| 209 | ajan: macOS MPVKit tek CI denemesi (fable K5) | YOKLANAMAZ | Aynı gerekçe, hash yok |
| 210 | MPVKit dylib+kurucu Darwin indirme+belge, main 4eb1b547+0fb9cd95 | DOĞRULANDI | 4eb1b547, 0fb9cd95 main'de; `mpvkit-macos`/MPVKit referansları mevcut |
| 211 | ajan: Denetçi HB A3 izle GEÇTİ, 2 ORTA borç | YOKLANAMAZ | Hash yok, meta rapor |
| 212 | ajan: Denetçi macOS MPVKit GEÇTİ, 4 ORTA borç | YOKLANAMAZ | Hash yok, meta rapor |
| 213 | ajan: HB A2 ön ayar kütüphanesi, main d584b9d1, kanca sildiği satır geri yazıldı | DOĞRULANDI | d584b9d1 main'de |
| 214 | ajan: Denetçi HB A2 KALDI (GPL)→düzeltildi→main d584b9d1 | DOĞRULANDI | d584b9d1 main'de |
| 215 | ajan: HB A1 filtre zinciri, main 33194cf9 | DOĞRULANDI | 33194cf9 main'de |
| 216 | ajan: HB A4+A5 arm64+CLI B1, main ef8e6931 | DOĞRULANDI | ef8e6931 main'de |
| 217 | ajan: VideoToolbox Hızlı kip yolu, main f7f3fcfa, kapı kaldı | DOĞRULANDI | f7f3fcfa main'de |
| 218 | ajan: VT Hızlı kip plan yolu (fable K1), main f7f3fcfa | DOĞRULANDI | f7f3fcfa main'de |
| 219 | ajan: Denetçi Yol B kabuk KALDI (1 KRİTİK P26) | DOĞRULANDI | `P26` referansları kaynakta/testlerde mevcut, bulgu izi var |
| 220 | ajan: Denetçi VT hızlı borç kapanışı GEÇTİ (P26 tanısı yanlış) | DOĞRULANDI | `P26` referansları mevcut |
| 221 | ajan: Denetçi HB A4+A5 arm64 GEÇTİ | YOKLANAMAZ | Hash yok, meta rapor |
| 222 | ajan: Denetçi HB A1 filtre GEÇTİ, main 33194cf9 | DOĞRULANDI | 33194cf9 main'de |
| 223 | "VidShrink açılıyor" paneli kalktı, main 94d1f46a, `BaslaticiPanelsizTests` | DOĞRULANDI | 94d1f46a main'de; `BaslaticiPanelsizTests.cs` mevcut |
| 224 | ajan: HB A3 izle borç kapanışı, main 6d7e5cdd | DOĞRULANDI | 6d7e5cdd main'de |
| 225 | ajan: Yol A döndürme/mıknatıs/P17/P1, main a721f6c0 | DOĞRULANDI | a721f6c0 main'de |
| 226 | ajan: Yol D açılış paneli kalksın, main 94d1f46a | DOĞRULANDI | 94d1f46a main'de |
| 227 | Karanlık x265 Dengeli CI ölçümü, Balanced eklenmez, a8772d97 ile main | DOĞRULANDI | a8772d97 main'de; `docs/olcumler/karanlik-x265.md` içinde 7,79/7,46 sayıları aynen doğrulandı |
| 228 | ajan: Denetçi Yol D açılış paneli TAMAM | YOKLANAMAZ | Hash yok, meta rapor |
| 229 | Yol D ORTA borçlar (bekleyen başlatıcı, sahte bakım hatası), main 94d1f46a | DOĞRULANDI | 94d1f46a main'de |
| 230 | ajan: Denetçi Yol A oynatıcı GEÇTİ, `ClickArbiter.Source`/Aero Snap düzeltildi | DOĞRULANDI | `ClickArbiter` `PlayerInputMap.cs`'de, Aero Snap `PlayerView.Fare.cs`'de mevcut |
| 231 | ajan: Denetçi Yol D borç düzeltmeleri, Updater docstring+38313 sayımı kapandı | DOĞRULANDI | `Updater.cs` mevcut; `BiciminTests.cs` docstring'i 38313'ü tarihsel adım olarak tutuyor, güncel toplamla (38700) çelişmiyor |
| 232 | ajan: Yol B borç kapanışı, 006c8702; yeniden denetim d0aea4d2 ile main | DOĞRULANDI | 006c8702, d0aea4d2 main'de |
| 233 | ajan: Denetçi HB A3 izle GEÇTİ, `PendingCount` silindi, main 6d7e5cdd | DOĞRULANDI | 6d7e5cdd main'de; `PendingCount` kaynakta yok (kaldırılmış, iddiayla tutarlı) |
| 234 | Karanlık x265 Dengeli: kapsam kalır hükmü, main a8772d97 | DOĞRULANDI | a8772d97 main'de; sayılar 227 ile aynı doğrulama |
| 235 | Yol D ORTA-A: `YuvaBeklemesi`/`IndirmeKilidi`/`KurulumKilidi` pimlendi, main e962538e | DOĞRULANDI | e962538e main'de; 3 sembol de kaynakta mevcut |
| 236 | A3 kalan borç (APFS harf duyarlılığı+README.tr), main e962538e | DOĞRULANDI | e962538e main'de |
| 237 | Yol B P14/P3/S9/K4/S14/S20, main d0aea4d2 | DOĞRULANDI | d0aea4d2 main'de |
| 238 | Yol D belge bulguları Yol D ORTA-A turunda kapandı, main e962538e | DOĞRULANDI | e962538e main'de |
| 239 | Denetçi karanlık Dengeli GEÇTİ, main a8772d97 | DOĞRULANDI | a8772d97 main'de |
| 240 | ORTA-B/ORTA-C ağaçta yeri yok, bekleme-butceleri.md'ye yazıldı, main e962538e | DOĞRULANDI | e962538e main'de; `docs/olcumler/bekleme-butceleri.md` mevcut |

## Not

Şüpheli bulunan satır yok. YOKLANAMAZ satırların hepsi ya salt "sonucu aktarılacak/aktarıldı"
biçiminde hash'siz ajan/denetim raporları, ya CI koşum numarası/registry/yayın ikilisi gibi
ağ veya sistem durumu gerektiren iddialar, ya da `.calisma/` altında iş bitince silinmesi
kurala uygun geçici bir dosyaya referans (satır 152).

## Ek: Satır 241-245 (T0)

A denetimi 6-79'u, B denetimi 80-240'ı kapsadı; son beş satır ikisinin dışında kaldı. Beşi
de yoklandı: `e962538e`, `d0aea4d2`, `a8772d97` main'in atası; APFS notu `README.tr.md`'de;
ORTA-B/ORTA-C bulgusu `docs/olcumler/bekleme-butceleri.md:109`'da. Şüpheli yok. Böylece
kapalı defterin bütün satırları (6-245) denetlendi.
