# Açılış: Çok Çekirdekli JIT Profili

Tarih 2026-09-30, dal `worktree-agent-aa27aa1fa5054091f`, taban `7b1c797a`. Makine DESKTOP-0J80KVV,
Windows 11 23H2. Düzenek `tools/acilis-hizi/EkranSaati` (ayrı masaüstü, `KayitKalkani` kancası),
iki yapı da `dotnet publish -c Release -r win-x64 --self-contained` (R2R composite), giriş
doğrudan `app\VidShrink.App.exe`, sıcak kip, 10 eşli tekrar, sıra tekrardan tekrara döner.
Hüküm eşli farkın ortancası ve kaç çiftin aynı yöne baktığıyla verilir; ortanca sütunları
oturumlar arası kaymayı taşır (bu oturumda başka ajanların derlemesi yüzünden p95 3 sn'ye çıktı).

## Uygulanan: `ProfileOptimization` (MultiCoreJit)

`src/VidShrink.App/JitProfili.cs`, çağrı `Program.RunMain` içinde tek örnek sahipliği belli
olduktan sonra (`JitProfili.Baslat(AcilisBitti.Task)`). Profil `acilis.jitprofile` ayar
klasörüne yazılır: `VIDSHRINK_SETTINGS_PATH` verilmişse onun klasörü, yoksa
`%LOCALAPPDATA%\VidShrink`. Kayıt açılış görüntüsünden (`AcilisBitti`, yedek 10 sn) 3 sn sonra
`StartProfile(null)` ile kapanır ve diske iner (~140 KB); profil yalnız açılışı kapsar. İleten
ikinci süreç profil başlatmaz, yoksa çıkarken iyi profili kendi kısa kaydıyla ezerdi. Sonraki
açılışta çalışma zamanı profildeki yöntemleri arka plan çekirdeklerinde önden derler.

Yoklama (geçici deney yapısı, `JitInfo`): R2R composite'e rağmen ilk kareye kadar 602 yöntem
JIT'leniyor, JIT süresi ortanca 59 ms; 2 gen0, 0 gen2 GC. Kazanç XAML aşamasında
(`app-init→xaml` ~199 → ~129 ms): JIT ve tür yükleme arayüz iş parçacığından arka plana kayıyor.

### Taban → Yeni, Dosyayla Açılış (720p h264, 6,2 MB), Kalıcı Ayar Klasörü

| adım | taban ortanca | yeni ortanca | eşli fark ortancası | lehine |
|---|---|---|---|---|
| app-init | 296,7 | 278,6 | −20,9 | 8/10 |
| xaml | 532,0 | 415,7 | −112,3 | 10/10 |
| pencere-yuklendi | 638,0 | 520,2 | −125,1 | 10/10 |
| ilk-kare | 695,3 | 554,5 | **−144,4** | 10/10 |
| ilk-boya | 738,0 | 555,6 | −188,6 | 10/10 |

### Taban → Yeni, Boş Açılış (`--klip -`, bitiş `ilk-boya`)

| adım | taban ortanca | yeni ortanca | eşli fark ortancası | lehine |
|---|---|---|---|---|
| xaml | 462,7 | 387,3 | −75,1 | 10/10 |
| pencere-yuklendi | 586,6 | 495,2 | −85,7 | 10/10 |
| ilk-boya | 777,8 | 634,0 | **−128,5** | 10/10 |

### Profilsiz İlk Açılış (Her Koşum Yeni Ayar Klasörü)

Kurulumdan sonraki ilk açılış: profil yok, yalnız kayıt yükü var.

| adım | taban ortanca | yeni ortanca | eşli fark ortancası | lehine |
|---|---|---|---|---|
| ilk-kare | 615,8 | 611,5 | −5,0 | 7/10 |
| ilk-boya | 701,8 | 626,5 | −53,3 | 8/10 |

İlk açılış yavaşlamıyor; fark gürültü içinde.

Ölçüm notu: düzenek her koşuma yeni ayar klasörü verir, profil orada birikmez. Kalıcı durum
iki tarafa `--ortam-a/-b VIDSHRINK_SETTINGS_PATH=<sabit>\settings.json` verilerek ve yeni yapı bir
kez `--bitis jit-profili-yazildi` ile koşturulup profil üretilerek ölçüldü.

## Denenip Reddedilenler

Hepsi aynı deney yapısında, ortam değişkeniyle açılan geçici kollarla, 10 eşli tekrar;
kollar teslimden önce söküldü.

| deney | ilk-kare eşli fark | lehine | hüküm |
|---|---|---|---|
| Dosya türü kaydını (`RegisterFileTypes`) ilk karenin arkasına almak | — | — | `palet→kayit` arası ~1 ms; `FileAssociationSetup` aynı yol için zaten yazmıyor. Kazanç yok. |
| Arka planda D3D11 aygıtı + `av_libglesv2`/Skia/HarfBuzz DLL ısıtma | −25,5 | 7/10 | Sınırda. Arka plandaki aygıt da ~155 ms sürüyor, sürücü başlatması iki iş parçacığında kilitleniyor; atılan bir GPU aygıtı için kalıcı kod değmez. |
| `Win32CompositionMode.DirectComposition` (varsayılan WinUIComposition) | +11,3 | 3/10 | Kazanç yok. |

Platform kurulumu (`tek-ornek→platform`, `AfterPlatformServicesSetup` işareti) ~200 ms ve yolun
en büyük tek dilimi; içindeki pay ANGLE/D3D11 aygıtı. Yazılım çizimi bunu siler ama çizim CPU'su
%60 artıyor (`hipersurus-g.md`), kapalı karar.

## Poster (İlk Kareyi Resim Olarak Önden Göstermek)

Uygulanmadı. Pencerenin ilk boyası (`ilk-boya`) her klipte ilk video karesiyle aynı anda ya da
ondan sonra geliyor: 720p'de ilk-kare 554,5 / ilk-boya 555,6; 4K h264 ve 1080p60 HEVC'de (3'er
koşum, yüklü makine) `kare-kaynagi` ile `ilk-kare` arası ≤2 ms, `ilk-boya` ≥ `ilk-kare`.
Kullanıcının gördüğü ilk karede video zaten var; poster gösterecek boş bir aralık yok. Ağır
klipte `pencere-yuklendi→ilk-kare` 60–135 ms, ama bu aralıkta pencere de henüz boyanmamış.
Poster ayrıca yalnız daha önce açılmış dosyada (önbellekten) mümkün; ilk açılışta kareyi başka
yoldan çıkarmanın maliyeti ölçülmedi.

## Önerilen, Ölçülmeyen

- `DOTNET_GCgen0size` ile gen0 büyütmek: yalnız 2 gen0 GC var, tavan birkaç ms; ölçüm gürültüsü
  içinde kalır, denenmedi.
- Kalan en büyük dilim platform kurulumu (~200 ms, GPU). Arayüz iş parçacığından alınamıyor;
  Avalonia'nın kurulum sırası değişmeden dokunulacak yer yok.
