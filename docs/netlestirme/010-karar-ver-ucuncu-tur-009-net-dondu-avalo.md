# Netleştirme: Karar ver (ucuncu tur, 009 net dondu): Avalonia 12 gecisi, major yuku, v1 kapsam

- tarih: 2026-09-11
- girdi: 010-karar-ver-ucuncu-tur-009-net-dondu-avalo-girdi.md
- maliyet: 64381 token, 110 sn

## Dönen

Karar turu; araştırma yok, eldeki olgulardan beş maddeyi tablolarla döndürüyorum.

## Varsayımlar (tek satır her biri)

- Avalonia 11.x EOL tarihi yok sayılır: 12 çıkalı 5 ay, 11.3 güvenlik yaması alsa da yeni kütüphaneler (LibMpv.Avalonia 0.10.1) 12'ye yazılıyor; fiilî destek ufku ≤12 ay kabul.
- OpenGlControlBase 12'de duruyor ama imzası değişmiş olabilir; oynatıcı yüzeyi buna bağlanmaz, WriteableBitmap yoluyla motor-bağımsız tutulur.
- macOS/Linux paket yolları: libmpv macOS'ta app bundle'a dylib gömme el işi; LibVLC macOS NuGet ile bundle'a girer; Linux'ta ikisi de sistem paketi.

## 1. Avalonia 12 geçişi

| Soru | Karar |
|---|---|
| Nitelik | **Paket güncellemesi**, baştan inşa değil. Gerekçe: code-behind arayüz, {Binding} 0, x:DataType 0; resmi kırılma listesinin depo karşılığı 1 dosya/1 satır + doğrulanamayan 3 madde (Clipboard, Headless options, SystemDecorations). |
| Zahmet tahmini | 1 sözleşme, P50 2 tur, P90 4 tur; duvar saati P50 1 gün, P90 3 gün (4. madde kalibrasyonuyla). |
| Zaman | **Şimdi**, 0. dalgadan önce (plan.md sırası doğru). Oynatıcı yüzeyi 12'nin üstüne kurulacak; 11'e kurup sonra taşımak iki kez ödemek. |
| Sonda | Dal `t0/avalonia-12`; 4 projede 12.1.2; `dotnet build` hata sayısı; tam süit; 4 RID `publish`; Windows'ta uygulama aç + bir dosya sıkıştır; Shot/PresentBench derlenir. |
| Kabul ölçütü | Derleme hatası ≤15 dosya; tam süit main ile aynı yeşil sayısı (yeni kırmızı 0); 4 RID publish çıkar; ≤3 turda kapanır. |
| Tutmazsa | 11.3.20'de kal, dal duruyor. Oynatıcı yüzeyi bitmap tabanlı tasarlanır (Avalonia sürümünden bağımsız); 12 geçişi v1 sonrasına, ilk fırsat 12.2 ya da 0. dalga bitişi. Kırılma nedeni Headless ise test AppHost'u ayrı sözleşmede düzeltilir, uygulama 12'ye geçer. |

## 2. Gelecek major'lar

| Soru | Karar |
|---|---|
| Sürekli yük mü | Hayır. Major aralığı ~2,75 yıl; her major için bütçe **≤1 sözleşme**. Yüzey küçük kaldıkça bu bütçe tutar. |
| Geçiş zamanı kuralı | Yeni major'ın **.1.x'i** çıkınca ve seçilen oynatıcı kütüphanesi o major'ı destekleyince; .0'a geçilmez. Geçiş yalnız bir sürüm penceresinin başında (özellik dalı açıkken değil). |
| Kaçınılacak API | Avalonia `internal`/`Unstable` işaretli her şey; `OpenGlControlBase`/`ICustomDrawOperation` doğrudan arayüzde (gerekirse tek adaptör dosyasında sar); tema iç stil anahtarları (Fluent'in `x:Key` iç isimleri); Diagnostics; reflection tabanlı Binding. |
| Sürdürülecek | Code-behind + ölçü belirteçleri; platform çağrıları Launcher/UpdateCheck/FileAssociation'da toplu (17 dosyada 65 çağrı, daha da toplanır). |

## 3. Açgözlü hedefe karşı gerçekçi kıyas

| Senaryo | Kapsam | Tahmin (tur / duvar gün) | Bedel |
|---|---|---|---|
| (a) Her şey | Standart 30 + Gelişmiş 13, 4 RID, hepsi sınanmış | 007 motor tahmini ~14 tur + 4 RID paketleme/CI 6-10 tur + macOS/Linux test altyapısı 4-6 tur ≈ 24-30 tur / 12-20 gün | v1 en az 3-4 hafta kayar; macOS/Linux'ta hiç test koşmadığından ilk gerçek hata raporu kullanıcıdan gelir. |
| (b) v1 kesiti (önerilen) | win-x64: 30+13 tam. osx-arm64: Standart 30 + Gelişmiş'ten motor-bağımsız olanlar. osx-x64 ve linux-x64: derlenir, "deneysel" etiketiyle yayımlanır, sınanmaz. | Motor ~14 tur + Windows paketleme 2 tur + macOS bundle 3-4 tur ≈ 19-20 tur / 8-12 gün | macOS Gelişmiş 13 v1.1'e; linux/osx-x64 kullanıcısı hata bulursa kendi bildirir. |

Ertelenenler ve gerekçe:

| Ertelenen | Gerekçe | Ne zaman |
|---|---|---|
| macOS'ta Gelişmiş 13 | Gelişmiş özelliklerin çoğu motor yüzeyine dayanır; macOS'ta motor paketleme (dylib gömme/imza) doğrulanmamış | v1.1, macOS CI koşumu eklendikten sonra |
| linux-x64 / osx-x64 sınanmış destek | CI'da hiç test koşmuyor; sınamak için runner + ffmpeg + libmpv kurulumu ayrı iş | v1.1-v1.2 |
| macOS CI testi | release.yml ubuntu'da çapraz derliyor; testsiz "destekliyoruz" demek sahte | v1'den hemen sonra ilk altyapı işi |

Motor seçimine etkisi: Windows-öncelikli v1'de libmpv sıralaması (115) değişmez. Ama **macOS koşulu** eklenir: 0. dalga adım 1'e "osx-arm64 bundle'a libmpv gömme 1 turda çalışmalı" maddesi girer; çalışmazsa macOS v1'de oynatıcı **LibVLC** ile (NuGet bundle'a girer, +106-293 MB bedeli kabul) ya da "sistem mpv varsa etkin" düşer. İki motor bir arada değil; macOS için LibVLC'ye düşülürse Windows'ta da LibVLC (tek motor kuralı, kıyas farkı 115 vs 109 küçük).

## 4. Tahmin yöntemi

| Soru | Karar |
|---|---|
| Birim düzeltmesi | Evet. 007'nin "1 tur = 3 gün"ü çöp. Gerçek: Avalonia 4,5 gün/7 tur = **0,64 gün/tur**, boru 2,2/8 = **0,28**. Standart: **0,5 gün/tur (P50), 0,7 (P90)**. |
| Tur şişmesi | T193 tek örnek ×3. İki örnek olana kadar 007 tur tahminleri **×1,5 (P50), ×3 (P90)** ile okunur. |
| Pilot zorunlu mu | Evet ama ayrı iş açılmaz: **Avalonia 12 sondası pilot 1**, **0. dalga libmpv SW ölçümü pilot 2**. İkisi de zaten koşulacak. |
| Alınacak ölçüler | Her sözleşme açılışında önceden yazılır: tahmin tur / gün / token. Kapanışta: gerçek tur, KALDI turu sayısı, duvar saati, değişen dosya/satır, token (kayıt bugün başlar, T193'te yoktu). `docs/olcumler/tahmin-isabet.md` tablosu. |
| Kalan hesabı | Kalan iş tahmini = 007 tur × (pilot gerçek tur / pilot tahmin tur medyanı) × 0,5 gün/tur; aralık P50/P90. İki pilottan sonra katsayı yenilenir; ondan sonra her 5 sözleşmede bir. |

## 5. Kendi kütüphane tetiği

"Kendi kütüphane" bu depoda yeni bir şey değil: T167/T175/T182 borusunun oynatıcıya genişletilmesi (007: ~22 tur → düzeltmeyle 16-33 gün). Tetik, aşağıdakilerden **biri** ölçülüp yazılınca açılır; aksi hâlde kapalı.

| # | Tetik | Ölçüm | Eşik |
|---|---|---|---|
| T1 | Performans | 0. dalga adım 1, SW render | libmpv **ve** LibVLC ikisi de 1080p <55 ya da 4K <24 kare/sn; ve HW yolu 1 sözleşmede kapanmaz |
| T2 | Özellik boşluğu | v1 zorunlu listesine karşı API dökümü | Zorunlu özelliklerden ≥2'si iki kütüphanede de yapılamaz (yazılı kanıt: API yok ya da issue kapalı-red) |
| T3 | Paketleme | osx-arm64 bundle denemesi | İki kütüphane de zorunlu RID'de (win-x64, osx-arm64) 2 turda paketlenemez |
| T4 | Lisans/bakım | Depo etkinliği, lisans metni | AGPL uyumsuzluğu ya da seçilen kütüphanenin 12 ay commit'siz kalması |
| T5 | Bellek | T167 ölçüsü türü | Kurulum maliyeti >300 MB **ve** kullanıcı şikâyeti (v1 sonrası) |

Tetik açılırsa sıra: önce boru genişletme pilotu (1 sözleşme: tek codec, tek platform, arama + senkron ölçüsü), sonra 4. madde katsayısıyla kalan hesaplanır; kararı yine kullanıcı verir.

Sıra: önce 1. maddedeki sonda, sonucu 4. maddenin tablosuna ilk satır olarak yazılır; sonra 0. dalga.

Yok

result: Karar verildi: Avalonia 12 paket güncellemesi (≤3 tur, şimdi, sonda kabul/dönüş ölçütlü), major bütçesi ≤1 sözleşme, v1 = Windows tam + macOS Standart, tahmin birimi 0,5 gün/tur ve iki pilot, kendi kütüphane 5 ölçülebilir tetikle.
