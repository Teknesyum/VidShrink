# Yol Haritası

Sıra kullanıcının. Biten madde silinmez, `[x]` ile işaretlenir ve nedeni satırda kalır.

## Sıra

- [x] 8. dalga — ekran kaydı motoru, ses girdisi, Kaydedici sekmesi (0.4.1)
- [x] Güncelleme senkronunun yakınsaması — kurulu 0.3.0 kendi senkronuyla ilerleyebiliyor (0.4.2)
- [x] Ekran kaydedici modülünün tamamlanması — ffmpeg kolu (9c) ve otomatik kip (9d) girdi
- [ ] WhatsApp'a özel azami kalite — karanlık videoda törpüleme ölçümü, `.claude/sonra.md`
- [x] Başlık çubuğu düğmelerinin keskin köşesi — Kesit E anahat dilini tek sözleşmeye bağladı (`RadiusSquare`, `HoverRing`)
- [ ] Simge takımının dolgu diline geçmesi — `docs/arastirma/ikon-estetigi.md` ikinci tavsiyesi (Fluent), karar kullanıcının

## Kararlar

### 12 işlik turun kesit sırası (12 Eylül 2026)

Kullanıcının verdiği 12 iş dört kesite bölündü ve bu sırayla kuruluyor:
**A** simge takımı ve Ayarlar sekmesi → **B** oynatıcı yerleşimi →
**C** kaydedicinin ffmpeg kolu (worktree ajanı) → **D** kaydedicinin otomatik kipi.
A ve B arayüzün aynı belirteçlerine dokunduğu için ardışık; C kendi dalında koştuğu
için B ile çakışmıyor; D, C'nin ürettiği ayar yüzeyi olmadan ölçülemediği için sonda.

### 15 işlik turun kesit sırası (13 Eylül 2026)

Kullanıcının verdiği 15 iş dört kesite bölündü: **E** anahat dili → **F** simge takımı →
**G** oynatıcı barları, **H** güncelleme paneli ve geliştirici sekmesi bağımsız.
E, F ve G aynı belirteçlere dokunduğu için ardışık; H arayüzün başka bir köşesinde.

### Simge ölçüleri araştırmadan gelir, gözden değil (13 Eylül 2026)

`IconStroke` 1.5'ten **2**'ye, pencere düğmesi üçlüsü 14:16:16'dan **12:18:14**'e,
kaymış merkezler 12'ye çekildi. Üç sayı da `docs/arastirma/ikon-estetigi.md`'de ölçülüp
sektör kaynaklarıyla (Lucide, Material) karşılaştırıldı; hiçbiri gözle seçilmedi.
`IkonKutusuTests` 26 yolu ayrıştırıp kenar payını ve merkezi pimliyor, böylece bir daha
kayan simge sessizce giremez. Tek muafiyet `IconPlay`: üçgen kütlesi tabanda toplandığı
için sektör onu bilerek sağa kaydırıyor, ölçü orada +0.5..+1.5 aralığını sınıyor.

### Otomatik kipte eşik yok, ölçüm var (12 Eylül 2026)

Kaydedicinin otomatik kipi "kabul edilebilir düşen kare" sayısı **uydurmuyor**: bu depoda
kayıt için ölçülmüş böyle bir sayı yok. Karar iki parçaya bölündü — `RecorderAutoPlan`
(Core, saf) adayları sıralıyor, `RecorderAutoProbe` (Ffmpeg) aday başına 3 saniyelik gerçek
kayıt alıp `RecordProgress.DroppedFrames` okuyor. Kare düşürmeyen ilk aday kısa devre
ediyor; hiçbiri sıfır değilse oranı en küçük olan, eşitlikte merdiven sırası kazanıyor.
Sabit bir eşik istenirse önce `tools/VidShrink.Bench` ölçer, sonra yazılır.

### Tek sürüm numarası, üç işletim sistemi (12 Eylül 2026)

Windows, macOS ve Linux ayrı sürüm numarası taşımaz. `Directory.Build.props` içindeki tek
`<Version>` üçünü de etiketler; `release.yml` aynı etiketten `win-x64`, `osx-x64`,
`osx-arm64` ve `linux-x64` üretir. Bir platformda bir iş eksik kalıyorsa sürüm numarası
değil o işin kendisi geride kalır.

### macOS alt sürümü: 15 (12 Eylül 2026)

osx-arm64 paketinin libmpv'si Homebrew şişelerinden geliyor ve 48 dylib'in 47'si
`minos=15.0` taşıyor (`docs/olcumler/libmpv-macos-gomme.md:160`, librubberband 11.0). Alt sürümü 13'e indirmek libmpv'yi kendimiz
derlemek ya da MPVKit'e geçmek demek; ikisi de kendi yayın zincirini getiriyor. Oynatıcı
libmpv'ye bağlı olduğu için alt sürüm macOS 15 ilan edilir. Daha düşük bir taban istenirse
iş `.claude/sonra.md`'deki libmpv derleme maddesinden açılır.
