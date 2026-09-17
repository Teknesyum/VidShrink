# Hipersürüş H Dalgası Ölçümleri

Makine DESKTOP-0J80KVV, 17 Eylül 2026. Araç `tools/acilis-hizi/EkranSaati`, ayrı Win32
masaüstünde, her süreçte `KayitKalkani` (her koşumda `kalkan=True`), ayar `.calisma` altında.
Eşleşik ölçüm, sıcak kip, zaman aşımı yok, yük testi yok. Klip `klip1080.mp4` (1080p, 6,8 MB).
Saat ms, uygulamanın kendi izi (`VIDSHRINK_ACILIS_IZI`); sıfır noktası `Process.StartTime`.

Dal `t0/hipersurus-h`, taban G (`cef5d94c`).

## H1: Tembel Sekme — Ölçüldü, Uygulanmadı

`AcilisIsareti.Ad` bir öznitelik; XamlIl öznitelikleri içerikten önce kurar. Her `xaml-X`
işareti X sekmesinin **başında** düşer, iki işaret arasındaki fark bir önceki sekmenin
içeriğidir. Kaydedicinin (E'de tembel) 0,6 ms'lik satırı bu okumayı doğruluyor.

Dosyayla açılış, `ed69629a`, 10 koşum, ardışık fark ortancası:

| içerik | ms |
|---|---:|
| TabControl.Tag (bildirimler, MatrixRain) | 22,3 |
| oynatıcı (PlayerView) | 29,8 |
| küçültme | 37,9 |
| dönüştürme | 8,7 |
| hakkında | 2,8 |
| kaydedici (zaten tembel) | 0,6 |
| gelişmiş | 1,9 |
| ayarlar | 6,9 |

Tembelleşebilir tavan 8,7 + 2,8 + 1,9 + 6,9 = **20,3 ms**. Makinenin eşli gürültüsü ±15-20 ms.
Dönüştürme (90 başvuru) ve ayarlar (94 başvuru) kaydını denetimden okuyor; kurulmamış sekmeden
kayıt kullanıcı ayarını varsayılanla ezer. Karar: uygulanmadı.

**G düzeltmesi:** G raporunun §6 dağılımı bir satır kaymış. "xaml-kucultme 31,5" PlayerView'dı,
"donusturme 48,2" küçültmeydi, "hakkinda 9,3" dönüştürmeydi.

Açık kalan iki aday:
- `TabControl.Tag` bloğu: 22,3'ün ne kadarı ilk-dokunuş bedeli, belli değil. Önce A/B gerekiyor.
- Boş açılışta PlayerView (29,8): ayrı sözleşme, karar kullanıcının.

## H2: `--bakim` Başlatıcısını Erteleme — Nötr

`ed69629a`. Bakım süreci açılış görüntüsünden sonra, düşük öncelikle açılıyor. Önce `cef5d94c`, 10 çift.

| adım | önce | h2 | eşleşik fark | h2 lehine |
|---|---:|---:|---:|---:|
| dosya: bakim-basladi | 111,5 | 803,0 | +688,8 | 0/10 (beklenen) |
| dosya: pencere-yuklendi | 756,3 | 755,5 | +2,8 | 5/10 |
| dosya: ilk-kare | 844,5 | 845,1 | +13,4 | 4/10 |
| dosya: ilk-boya | 1053,9 | 1062,6 | +7,6 | 4/10 |
| boş: bakim-basladi | 104,9 | 900,5 | +797,7 | 0/10 (beklenen) |
| boş: pencere-yuklendi | 790,0 | 792,0 | +7,4 | 4/10 |
| boş: ilk-boya | 886,1 | 894,9 | +16,6 | 4/10 |

Bakım süreci ilk kareye ölçülebilir CPU payıyla girmiyordu; erteleme gürültü içinde.

## H3: Karşılaştırma Paneli Kök Nedeni

**Kök neden.** G'nin 9-11 sn'si üç parçanın toplamı:
- `ComplexityProbe` (VMAF taraması, zaten `ScanConcurrency` ile paralel): sahne haritasından sonra ≈6,5 sn.
- Kalibrasyon: ≈1,1 sn.
- Son klip kodlaması: ≈1,4-1,7 sn.

Buna iki israf ekleniyordu:
- İlk parça 900 ms'lik debounce bekliyordu.
- `Probed` ara aşaması ekrandaki parçayı iptal edip yeniden kodlatıyordu (`panel-parca-iptal`, ≈9,3 sn'de).

**Düzeltme** (`d1aa900c`, `e9147311`):
- İlk parça ve ölçüm planı 1 ms gecikmeyle sıraya giriyor.
- `Probed` aşaması ekranda parça varken planı erteliyor, ölçüm bitince bir kez uyguluyor.

y3b (`ed69629a`) ve y3c (`e9147311`) karşılaştırması, dosyayla açılış, `--bitis panel-parca-kalibre`, 6 çift:

| adım | önce | h3c | eşleşik fark | en az / en çok | h3c lehine |
|---|---:|---:|---:|---:|---:|
| ilk-kare | 672,2 | 675,1 | -2,4 | -16,4 / +69,4 | 4/6 |
| panel-parca-istek | 1256,1 | 853,5 | -409,5 | -459,9 / -321,8 | 6/6 |
| panel-parca (yaklaşık klip) | 3207,8 | 2943,4 | -320,7 | -546,5 / -82,0 | 6/6 |
| olcum-Probed | 7854,8 | 8237,9 | +115,8 | -499,3 / +949,2 | 2/6 |
| olcum-Calibrated | 9067,6 | 9369,9 | -89,9 | -659,4 / +783,7 | 4/6 |
| panel-parca-kalibre | 10933,6 | 10745,4 | -579,9 | -1030,3 / +262,5 | 5/6 |

Tekrar başına ham (ms, panel-parca-kalibre / panel-parca):

| # | önce | h3c |
|---:|---|---|
| 1 | 12887 / 4006 | 12255 / 3460 |
| 2 | 12189 / 3406 | 11159 / 3024 |
| 3 | 11251 / 3217 | 10612 / 2910 |
| 4 | 10527 / 3199 | 10378 / 2956 |
| 5 | 10566 / 3143 | 10039 / 2809 |
| 6 | 10616 / 3012 | 10879 / 2930 |

Ara sürüm y3 (erteleme var, ölçüm planı henüz 1 ms'ye alınmamış), 6 çift:
- panel-parca-kalibre -181 (6/6), panel-parca -310 (6/6).
- `panel-parca-iptal` h3'te hiç düşmedi.

İlk parça ölçümü (`--bitis panel-parca`, 6 çift):
- panel-parca-istek 1274 → 900, eşli -432 (6/6).
- panel-parca 3226 → 3001, eşli -366 (6/6).
- sahne-haritasi +211; bu, erkene çekilen kodlamayla CPU paylaşımından geliyor.

**Hedef <1 sn tutmadı.** Kalibre sonuç, motorun doğruluk işi olan tarama ile bağlı. Taramayı
kısaltmak motor kararı, bu dalganın kapsamı dışında.

## H4: NativeAOT Fizibilitesi ve ReadyToRun Composite

**AOT uygulanmadı.** ILC analizi iki projede de koştu; günlükler `.calisma/hiper-h/aot/`
altında (silindi, engeller aşağıda).

Başlatıcı ve Core engelleri: yedi JSON çağrısı `JsonSerializerContext` kaynak üretimi istiyor
(IL2026/IL3050):
- `PlanParser.cs:35`
- `PresignedUploadProvider.cs:202`
- `ShareResult.cs:162`, `:209`
- `ShareTargets.cs:113`
- `SingleInstanceChannel.cs:79`, `:84`

Uygulama engelleri:
- `Strings.cs:347`: JSON.
- `Localization/Text.cs:56`: `Binding(string)` yansımalı bağlama. `loc:Text` bütün yerelleştirilmiş metinleri taşıyor.
- `DefaultAppSuggestionBar.cs:91`: `Binding(string)` yansımalı bağlama.
- `PaletteCatalog.cs:102`, `:123`: çalışma anında `ResourceInclude(Uri)`.

Ortam engelleri:
- Yerelde `link.exe` PATH'te yok (vcvars bozuk). ILC geçiyor, bağlama 9009 ile düşüyor.
- AOT `DOTNET_STARTUP_HOOKS` desteklemiyor, `KayitKalkani` AOT ikilisini koruyamaz. Ölçülmeden
  önce ShellMenu, `Core/Setup/ShellRegistration` ve DefaultApp için uygulama içi HKCU kalkanı gerekiyor.

**En iyi R2R yolu: composite.** TieredPGO zaten açık (C1). Taraflar:
- r2r = y3c (`e9147311`).
- kompozit = aynı kaynak + `PublishReadyToRunComposite=true`.

Her iki senaryoda 10 çift koşuldu.

Dosyayla açılış (`--bitis ilk-kare`):

| adım | r2r | kompozit | eşleşik fark | en az / en çok | kompozit lehine |
|---|---:|---:|---:|---:|---:|
| pencere-yuklendi | 651,8 | 527,2 | -111,3 | -153,9 / -65,1 | 10/10 |
| ilk-kare | 730,2 | 595,4 | -119,8 | -171,4 / -54,6 | 10/10 |
| ilk-boya | 917,3 | 749,0 | -158,3 | -214,8 / -115,9 | 10/10 |
| panel-parca-istek | 918,9 | 760,9 | -141,3 | -197,5 / -30,7 | 10/10 |

| # | r2r (ilk-boya / ilk-kare) | kompozit |
|---:|---|---|
| 1 | 1082 / 856 | 958 / 801 |
| 2 | 1035 / 810 | 852 / 693 |
| 3 | 1046 / 818 | 832 / 646 |
| 4 | 924 / 755 | 755 / 609 |
| 5 | 910 / 731 | 716 / 571 |
| 6 | 933 / 729 | 801 / 619 |
| 7 | 874 / 700 | 669 / 533 |
| 8 | 832 / 643 | 684 / 559 |
| 9 | 859 / 704 | 743 / 582 |
| 10 | 807 / 649 | 672 / 558 |

Boş açılış (`--bitis ilk-boya`):

| adım | r2r | kompozit | eşleşik fark | en az / en çok | kompozit lehine |
|---|---:|---:|---:|---:|---:|
| pencere-yuklendi | 760,9 | 635,4 | -115,3 | -184,0 / -58,3 | 10/10 |
| ilk-boya | 857,1 | 928,1 | +14,2 | -402,9 / +144,4 | 5/10 |

| # | r2r (ilk-boya / pencere-yuklendi) | kompozit |
|---:|---|---|
| 1 | 696 / 640 | 688 / 474 |
| 2 | 586 / 537 | 706 / 479 |
| 3 | 880 / 804 | 917 / 620 |
| 4 | 815 / 743 | 960 / 656 |
| 5 | 1096 / 760 | 693 / 639 |
| 6 | 868 / 801 | 939 / 632 |
| 7 | 836 / 760 | 960 / 662 |
| 8 | 1149 / 771 | 969 / 662 |
| 9 | 846 / 769 | 682 / 626 |
| 10 | 1090 / 762 | 970 / 666 |

Boş açılışın ilk-boyası iki kümeli (≈690 / ≈950). Pencere 115 ms erken yükleniyor, ama
boya kareye yuvarlanıyor; ortanca kümeler arası dağılımı gösteriyor, kazancı değil.

Yayın boyutu (win-x64 app klasörü): 557 dosya / 122,6 MB → 558 dosya / 150,3 MB
(`VidShrink.App.r2r.dll` 80,7 MB).

Uygulandı: `VidShrink.App.csproj`, yalnız `win-x64` (`c9afc39a`). Yalnız Windows ölçüldü. CLI
aynı klasöre önce yayınlanıyor, uygulama yayını ortak derlemeleri üzerine yazıyor.

## Teorik Sınıra Kalan Mesafe

| yol | şimdi | sınır | kalan | sınırın dayanağı |
|---|---:|---:|---:|---|
| dosyayla açılış ilk-kare | ≈595 | ≈200 | ≈395 | AOT + yazılım kare yolu. AOT engelleri H4'te. |
| herhangi bir piksel | ≈527 (pencere-yuklendi) | 65 | ≈460 | Win32 pencere + ilk boya tabanı |
| panel yaklaşık klip | 2943 | ≈2800-2900 | ≈50-150 | ilk-boya (~850) + tek 5 sn pencere kodlaması (sol kayıpsız ve sağ plan paralel, ≈2,0 sn) |
| panel kalibre klip | 10745 | tarama sonu + ≈1,5 sn | tarama | ≈6,5 sn VMAF taraması motorun doğruluk işi |

Not: panel satırları composite öncesi (y3c) ölçüldü. Composite ilk-boyayı 158 ms öne çektiği
için yaklaşık klibin de aynı pay kadar öne gelmesi bekleniyor. Bu ölçülmedi.

## Kullanıcı Dosyaları

`%APPDATA%\VidShrink` ve `%LOCALAPPDATA%\Programs\VidShrink` için LastWriteTime + SHA256
anlık görüntüsü alındı. HKCU'nun yedi anahtarı `reg export` ile özetlendi.

| | önce | sonra | fark |
|---|---:|---:|---:|
| dosya | 577 | 577 | 0 |
| kayıt anahtarı özeti | 7 | 7 | 0 |
