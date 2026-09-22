# Kapalı Defter Denetimi 2026-09-23-A

Kapsam: `.claude/kapali.md` satır 6-79 (`## 2026-09-22` ve `## 2026-09-19` bölümleri).
Yalnız ana ağaçtan okundu, dosya değiştirilmedi. Test koşturulmadı, derleme yapılmadı.

## Sayım

- Toplam denetlenen satır: 68
- DOĞRULANDI: 49
- ŞÜPHELİ: 5
- YOKLANAMAZ: 14

## Tablo

| Satır | İddianın kısası | Hüküm | Kanıt |
|---|---|---|---|
| 8 | cb09221e — Hakkında Ayarlar'a taşındı | DOĞRULANDI | `git merge-base --is-ancestor cb09221e origin/main` → 0 |
| 9 | ajan denetim raporu (7 bulgu) | YOKLANAMAZ | Satırın kendisi "eser dosyası yok, iddia doğrulanamaz" diyor |
| 10 | ajan denetim raporu (jobs.md) | YOKLANAMAZ | Satırın kendisi "eser dosyası yok, iddia doğrulanamaz" diyor |
| 11 | 62eac5eb — animasyon | DOĞRULANDI | `git merge-base --is-ancestor 62eac5eb origin/main` → 0 |
| 12 | ajan: kapali.md 8-95 denetimi | YOKLANAMAZ | Satır "eser dosyası yok" diyor |
| 13 | ajan: kapali.md 96-183 denetimi | YOKLANAMAZ | Satır "eser dosyası yok" diyor |
| 14 | ajan: kapali.md 184-270 denetimi | YOKLANAMAZ | Satır "eser dosyası yok" diyor |
| 15 | feb55507 — PresetProfile.Container/fable B | DOĞRULANDI | Hash ana dalda; `src/VidShrink.Core/StreamMapping.cs` içinde `ContainerFor` var |
| 16 | feb55507 — fable önayar kap kararı | DOĞRULANDI | Aynı hash; `docs/danisma/010-fable-onayar-kap.md` var |
| 17 | 0666989b — HandBrake içe aktarım CLI/pencere | DOĞRULANDI | Hash ana dalda; `src/VidShrink.Cli/CliRequest.cs` içinde `--profil-dosyasi` var |
| 18 | a94d3b48 — NVENC hedef altı teslim ölçümü | DOĞRULANDI | Hash ana dalda; `docs/olcumler/nvenc-4-teslim.md` var |
| 19 | a94d3b48 — bench CI beklerken koştu | DOĞRULANDI | Aynı hash, aynı kanıt |
| 20 | 9c16cbb5 — B1 ses/altyazı eksikleri | DOĞRULANDI | Hash ana dalda; `StreamMapping.cs`'de flac/loudnorm/forced geçiyor; `docs/olcumler/b1-kalan-mutasyonlar.md` var |
| 21 | 2aef863e — CLI plan metni iz notu | DOĞRULANDI | Hash ana dalda; `tests/VidShrink.Tests/CliIzNotuTests.cs` var |
| 22 | Arayüz kesilen/üst üste binen taraması | YOKLANAMAZ | Commit/test/dosya izi yok, salt anlatı |
| 23 | ajan: arayüz taşma taraması | YOKLANAMAZ | "sonucu aktarılacak" — ayrıntı yok |
| 24 | ajan: kapali.md 271-350 denetimi | ŞÜPHELİ | `docs/olcumler/kapali-defter-denetimi/dilim-271-350.md` yok (glob boş); satırın kendi notu da bunu söylüyor |
| 25 | ajan: C1-2/4 çeviri betiği | YOKLANAMAZ | "sonucu aktarılacak" — ayrıntı yok |
| 26 | ajan: kapalı defter satır denetimi (4 dilim) | ŞÜPHELİ | dilim-1-125.md, dilim-126-270.md, dilim-351-son.md var; dilim-271-350.md yok — satırın "toplam 0 gerçek şüpheli" özeti eksik dilimle çelişiyor |
| 27 | ajan: 1-125 denetimi, 112 satır 0 şüpheli | DOĞRULANDI | `docs/olcumler/kapali-defter-denetimi/dilim-1-125.md` mevcut, tablo dolu |
| 28 | ajan: 126-270 denetimi, 139 satır 0 şüpheli | ŞÜPHELİ | Dosya var ama satırın kendisi "kesin pim değil" diyerek FfmpegArgumentsTests.cs:640 pimini YOKLANAMAZ'a çeviriyor |
| 29 | ajan: 351-son, AudioCodec kusuru PlanCalculator.cs:504 | ŞÜPHELİ | ad7ddaea ana dalda ve commit mesajı "AudioCodec" kusurunu doğruluyor, ama bugün `PlanCalculator.cs:504` `AllowResolutionDrop` satırı — pim kaymış (AudioCodec kullanımları 86/87/492/511/850/926/1122'de) |
| 30 | ajan: C1-5 filtre anahtarları 42 dil | DOĞRULANDI | `SuzgecPaneliTests.AnahtarlarButunDillerde` mevcut; Locales altında 42 dil klasörü var |
| 31 | 7a6286e6 — B1 arayüz yüzeyi (dış altyazı, yakma, loudnorm) | DOĞRULANDI | Hash ana dalda; `tests/VidShrink.Tests/IzPaneliTests.cs` var |
| 32 | 7a6286e6 — C1-6 ses/altyazı anahtarları 42 dil | DOĞRULANDI | Aynı hash; `IzPaneliTests.AnahtarlarButunDillerde` satır 219'da |
| 33 | ajan: kapalı defter satırlarını doğrula (17/6/2) | YOKLANAMAZ | Ayrı bir denetim eseri yok, kendi kendine referans |
| 37 | Biçim gövdeleri Core/Bicim ve Core/Saat'e indi | DOĞRULANDI | `tests/VidShrink.Tests/BicimDisiYazimTests.cs` var; önceki denetim (dilim-1-125.md satır 25) de "Doğrulandı" diyor |
| 38 | Kullanıcı ekranı kusuru + kanıt görüntüsü | DOĞRULANDI | `.claude/kanit/onizleme-hatasi-2026-09-19.jpg` ana ağaçta duruyor (7,98 MB) |
| 39 | ajan: Paylaşım anahtarları çevirisi 1 | YOKLANAMAZ | Hangi 8 dil/37 anahtar olduğu belirtilmemiş, ölçülebilir iz yok |
| 40 | ajan: Paylaşım anahtarları çevirisi 2 | YOKLANAMAZ | Aynı sebep |
| 41 | ajan: Paylaşım anahtarları çevirisi 3 | YOKLANAMAZ | Aynı sebep |
| 42 | ajan: Paylaşım anahtarları çevirisi 4 | YOKLANAMAZ | Aynı sebep |
| 43 | ajan: Paylaşım anahtarları çevirisi 5 | YOKLANAMAZ | Aynı sebep |
| 44 | Bit hızı birimi üç ayrı yazımla kaldı (ayrı iş) | DOĞRULANDI | `en/main.json:598` hâlâ `main.unit.kbps-value` ayrı anahtar taşıyor, birleştirilmemiş |
| 45 | KaydediciHedefTests sınır/bekleme güncellendi | DOĞRULANDI | `tests/VidShrink.Tests/KaydediciHedefTests.cs` içinde test adı geçiyor |
| 46 | 89c6182a — Core Türkçe cümleleri ayıklama (a) | DOĞRULANDI | Hash ana dalda |
| 47 | Alt ajan raporu doğrulanmadan iş alınmaz kuralı | DOĞRULANDI | `AGENTS.md` "Alt ajanlar" bölümünde birebir metin var |
| 48 | Paralel alt ajanlar `.calisma/<dal>` kuralı | DOĞRULANDI | `AGENTS.md` "Alt ajanlar" bölümünde birebir metin var |
| 49 | ShareDiagnosis.Detail düşürüldü | DOĞRULANDI | `src/VidShrink.Core/Share/ShareErrorClassifier.cs`'de `Detail` alanı yok |
| 50 | fable A — kurucu dili gömülü tablo | DOĞRULANDI | `docs/plan.md:777-787` `SetupText.cs` tr/en tablo planını anlatıyor, kod bununla uyumlu |
| 51 | HandBrake E3/E7/E8/E9 boşluk ölçümü | DOĞRULANDI | `docs/plan.md:903-1071` E3/E7/E8/E9 başlıkları var |
| 52 | b7a6e209 — 42 dilde output-folder.unusable anahtarı | DOĞRULANDI | Hash ana dalda; anahtar 42/42 dosyada |
| 53 | b7b485c4 — 42 dilde modül anahtarları | DOĞRULANDI | Hash ana dalda |
| 54 | 12d34835 — CI kırmızı AyarYuzeyiTests | DOĞRULANDI | Hash ana dalda; `tests/VidShrink.Tests/AyarYuzeyiTests.cs` var |
| 55 | 12d34835 — CI kırmızı SaatTests.IkinciGovdeYok | DOĞRULANDI | Aynı hash; `SaatTests.cs` var |
| 56 | ShareRetry — borç 5 kapandı | DOĞRULANDI | `docs/inceleme/kod-borclari-2026-09-18.md` var |
| 57 | dadbb044 — preview rozeti main.preview.temsili 43 dilde | ŞÜPHELİ | Hash ana dalda ama anahtar 42 dosyada geçiyor (toplam dil sayısı da 42), "43 dilde" fazla |
| 58 | 3cdacd93 — Borç 20 bayat paragraf | DOĞRULANDI | Hash ana dalda |
| 59 | 0512c10a — Borç 4, MainWindow.axaml.cs 5081→4481 | DOĞRULANDI | Hash ana dalda; `docs/olcumler/k8-pencere-boyu-2026-09-19.md` var |
| 60 | 05c19eb8 — BaslikKapsamiTests pimleri | DOĞRULANDI | Hash ana dalda; `BaslikKapsamiTests.cs` var |
| 61 | db6d46ea — parça süresi sınırı 3,5→4,0 | DOĞRULANDI | Hash ana dalda; `docs/olcumler/kayit-bolme-parca-siniri.md` var |
| 62 | WhatsApp karanlık ölçümü | DOĞRULANDI | `docs/olcumler/whatsapp-karanlik.md` var |
| 63 | Yol haritası denetimi kapandı | DOĞRULANDI | `docs/YOL-HARITASI.md` var; 8cf3ca92 ana dalda |
| 64 | Açılış 1 sn — hipersürüş H | DOĞRULANDI | `docs/olcumler/hipersurus-h.md` var; 0a27d7dd ana dalda |
| 65 | P28 altyazı indirme (Yol A) | DOĞRULANDI | `AltyaziOturumTests.cs` (~21+ ölçü) ve `AltyaziIndirmeTests.cs` mevcut |
| 66 | Fluent simgeler + K4 | DOĞRULANDI | 7258466d ve 8cf3ca92 ana dalda; `IkonImzaTests.cs` var |
| 67 | K19 Editör D0 CurrentMedia | DOĞRULANDI | f3da7000/2a20e14a/64aab1b5 ana dalda; `src/VidShrink.App/CurrentMedia.cs` var |
| 68 | Trace recorder persistence guard | DOĞRULANDI | `KaydediciAyarYalitimTests.cs` ve `AyarKaliciligiTests.cs` var |
| 69 | Oturumda denetim erişilebilirliği | DOĞRULANDI | `tests/VidShrink.Tests/OynaticiDenetimTests.cs:398` `AutomationProperties.GetName` beş şerit düğmesinde birebir |
| 70 | Yalnız koyu tema kuralı geçersiz kararı | DOĞRULANDI | Altı açık palet klasörü mevcut (AyuLight, CatppuccinLatte, GithubLight, GruvboxLight, RosePineDawn, SolarizedLight); `PaletteCatalog.cs:106-108` parlaklık eşiğiyle Light/Dark seçiyor |
| 71 | CI geçmişinde şerit testi | DOĞRULANDI | `UstSeritTikTests.SeritSekmeleriYutmuyor` (satır 112) ve `OynaticiDenetimTests.cs:390` şerit düğmesi kolu var |
| 72 | Core sabit Türkçe metin (a) kullanıcıya çıkan | DOĞRULANDI | `Core/Setup/SetupText.cs` gömülü tr/en tablo taşıyor |
| 73 | Bütçe doldurma x265 ikinci tam kodlama ölçümü | DOĞRULANDI | `docs/olcumler/butce-ikinci-kodlama.md` var |
| 74 | B5 hız kapısı x265 (aynı ölçüm, Soru 1) | DOĞRULANDI | Aynı belge |
| 75 | HandBrake kalan iş denetimi | DOĞRULANDI | `docs/handbrake/acik-durumu-2026-09-17.md` var |
| 76 | 8f09dc60 — B1a ses kodlayıcı kararı | DOĞRULANDI | Hash ana dalda |
| 77 | NVENC kalite kolları taraması | DOĞRULANDI | `docs/olcumler/nvenc-kalite-kollari.md` var |
| 78 | fable danışma: NVENC kolları | DOĞRULANDI | `docs/danisma/2026-09-19-fable-nvenc-kalite-kollari.md` var |

## Not

Satır 9/10/12/13/14 zaten kendi metninde "eser dosyası yok, iddia doğrulanamaz" diyerek
önceki denetimi tekrarlıyor — bu denetim onu YOKLANAMAZ olarak aynen taşıdı, yeni bir
çelişki eklemedi. Satır 24/26/28 birbirine bağlı dört-dilim iddiasının eksik kalan
parçası (271-350) hâlâ dosya sistemine hiç yazılmamış; "toplam 0 gerçek şüpheli" özeti bu
yüzden doğrulanamaz durumda kalıyor. Satır 29 ve 57 sayı/pim kaymasına örnek: iş gerçek
ama iddianın taşıdığı iz (satır no, dil sayısı) bugünün koduyla uyuşmuyor.
