# Later'dan Kapanan Kararlar

Sahibin hayır dediği ya da bekletme kararı verdiği başlıklar docs/later.md'den buraya taşınır; gerekçe başlığın altındaki Karar satırındadır.

## 117. Taşınabilir Kip Ve Güncelleme Kanalı

13 Ekim 2026 · "bulduklarını later a eklersin" · AYR-P8 · Ayarlar Ve Kabuk · iş büyük · öncelik 1 · karar sahibin

Karar (21 Ekim 2026, sahip): hayır, tek kanal (ön sürüm kanalı). Gerekçe: düzenli ön sürüm etiketi ve iki yayın hattı bakımı ister, kullanıcı tabanı yetmiyor.

- Satırlar (AYR-P8): AYR-05 Taşınabilir Kip; AYR-09 Güncelleme Kanalı Seçimi. [Tablo](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.App/MainWindow.axaml, .github/workflows/, docs/kurulum.md
- Sahibin kararı: Yayına iki yeni biçim eklensin mi: ayarını yanında taşıyan kurulumsuz paket ve gönüllülere açık ön sürüm kanalı?
- Plan: Önce AYR-05: exe'nin yanındaki bir işaret dosyası ayar ve günlük yolunu uygulama klasörüne çevirir; bu kipte güncellemenin nasıl davranacağı sahibin kararına göre kurulur. Sonra AYR-09: Güncelleme kartına kararlı ve ön sürüm seçimi eklenir, denetim seçilen kanalın yayınlarına bakar. İki satır dağıtım tablosundaki DAG-10 ve DAG-24 ile aynı iştir; orada DAG-24 kendi paketinde, DAG-10 ise DAG-11 ve DAG-20 ile aynı pakette duruyor, birleştirmede bu paketlerle tek işe indirilmelidir. Doğrulama: işaret dosyasıyla ayar yolunun testi ve kanal seçiminin sürüm karşılaştırma testi.

## 123. VVC Kodlama Desteği

13 Ekim 2026 · "bulduklarını later a eklersin" · SIK-P12 · Küçült · iş orta · öncelik 2 · karar sahibin

Karar (21 Ekim 2026, sahip): hayır, raf. Gerekçe: ayrı ffmpeg derlemesi (boyut, bakım, lisans) ister, neredeyse hiçbir aygıt oynatmıyor.

- Satırlar (SIK-P12): SIK-16 VVC (H.266) kodlama. [Tablo](piyasa/2026-10-13/kucult-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/PlanParser.cs, src/VidShrink.Core/FfmpegArguments.cs, src/VidShrink.Core/PlanCalculator.cs, src/VidShrink.App/MainWindow.axaml (kodek seçimi), src/VidShrink.App/Locales/
- Sahibin kararı: libvvenc içeren bir ffmpeg derlemesi dağıtılsın mı ve oynatma desteği bu kadar darken VVC kodek listesine girsin mi?
- Plan: Önce dağıtılan ffmpeg'de libvvenc (VVC kodlayan açık kaynak kitaplık) var mı diye yoklanır; yoksa iş derleme kararında bekler. Varsa kodek izin listesine eklenir (`src/VidShrink.Core/PlanParser.cs:13`), hız ön ayarları ve boyut verimi ölçülerek yazılır, kodek seçiminde çoğu aygıtta oynamadığı uyarısıyla gösterilir. Doğrulama: argüman testi ve ölçüm aracıyla hedef boyut bandının tuttuğunun ölçülmesi. Dönüştür sekmesindeki DON-33 ile aynı iştir.

## 133. Linux'ta Kendini Güncelleme

13 Ekim 2026 · "bulduklarını later a eklersin" · PLT-P6 · Platform Eşitliği · iş orta · öncelik 2 · karar sahibin

Karar (21 Ekim 2026, sahip): hayır; 118 (Flathub/AppImage) kararına kadar kapalı.

- Satırlar (PLT-P6): PLT-20 Linux'ta kendini güncelleme. [Tablo](piyasa/2026-10-13/platform-esitligi-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.Core/InPlaceUpdate.cs, src/VidShrink.App/YerindeGuncelleme.cs, .github/workflows/release.yml, docs/kurulum.md
- Sahibin kararı: Linux'ta bilerek kapalı tutulan uygulama içi güncelleme açılsın mı
- Plan: Kullanıcıya ait `~/.local/share/vidshrink` kurulumunda yerinde takas açılır; yazılamayan yolda kapalı kalır ve bugünkü gibi yalnız haber verilir. Linux için güncelleme paketi bugün üretilmiyor (release.yml:492-500); ya yayına Linux paketi eklenir ya da uygulama arşivi kullanılır. DAG-25 ile aynı iş, DAG paketlerinde yeri yok; DAG-P8 (Flatpak ya da AppImage) onaylanırsa güncellemeyi mağaza üstlenir ve bu paket yalnız betikle kurulanlara kalır. Takas, geri alma ve düşüş sayımı testle ölçülür.

## 152. Betik Ve Eklenti Arabirimi

13 Ekim 2026 · "bulduklarını later a eklersin" · DAG-P12 · Dağıtım Ve Otomasyon · iş büyük · öncelik 2 · karar sahibin

Karar (21 Ekim 2026, sahip): hayır. Gerekçe: ControlChannel dış süreçten kumanda ve otomasyonu karşılıyor; arabirim sözü her sürümde bakım borcu.

- Satırlar (DAG-P12): DAG-36 Betik ve eklenti arabirimi. [Tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/, src/VidShrink.App/, yeni docs/ sayfası
- Sahibin kararı: uygulamaya betik motoru (yeni bağımlılık) eklensin ve sürümler arası korunacak bir eklenti arabirimi sözü verilsin mi.
- Plan: DAG-P4'ten önce başlanmaz: dış süreçten gelen komut kanalı, eklenti isteklerinin kumanda ve otomasyon kısmını bağımlılık eklemeden karşılar. Evet denirse ilk adım dar tutulur: kullanıcı klasöründeki betikler oynatıcı ve kuyruk olaylarına abone olur ve komut kanalındaki komutları çağırır; arayüze parça ekleyen eklenti sonraya kalır. Betik dili ve motoru kararın parçasıdır. Doğrulama: örnek betikle olay ve komut testi, betik hatasının uygulamayı düşürmediğinin testi.
