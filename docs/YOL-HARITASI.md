# Yol Haritası

Sıra kullanıcının. Biten madde silinmez, `[x]` ile işaretlenir ve nedeni satırda kalır.

## Sıra

- [x] 8. dalga — ekran kaydı motoru, ses girdisi, Kaydedici sekmesi (0.4.1)
- [x] Güncelleme senkronunun yakınsaması — kurulu 0.3.0 kendi senkronuyla ilerleyebiliyor (0.4.2)
- [x] Ekran kaydedici modülünün tamamlanması — ffmpeg kolu (9c) ve otomatik kip (9d) girdi
- [x] WhatsApp'a özel azami kalite — Paket 3 ölçtü: `aq-mode=3` karanlık PSNR'ı 8 satırda −0,01 ile +0,07 dB oynattı, kod değişmedi (`docs/olcumler/whatsapp-karanlik.md`)
- [x] Başlık çubuğu düğmelerinin keskin köşesi — Kesit E anahat dilini tek sözleşmeye bağladı (`RadiusSquare`, `HoverRing`)
- [ ] Simge takımının dolgu diline geçmesi — `docs/arastirma/ikon-estetigi.md` ikinci tavsiyesi (Fluent), karar kullanıcının
- [ ] **HandBrake algı tarafında da geçilecek** — bugün eşit boyutta (±%2) HandBrake'in x265 ön ayarı
  8,79 VMAF-NEG, 2,60 dB XPSNR ve 0,0299 SSIM önde (`docs/olcumler/handbrake-acigi.md`). Hedef boyuta
  oturtmayı biz kazanıyoruz; kalan açık psy-rd, psy-rdoq ve uyarlamalı niceleme anahtarlarının
  argümanlarımıza girmemesinden geliyor. Ölçüt: aynı düzenekte aynı kaynakta fark **0'a** insin,
  sonra artıya geçsin. Bu koşullarda ölçülmemiş bir kazanım rapora girmez.
  Paket 3 (yazılım yolu, `docs/olcumler/handbrake-acigi-yazilim.md`): XPSNR'da 6/6 satır önde; VMAF-NEG'de
  `karanlik` kesitinde SVT-AV1 −0,65 ve −0,61 geride. Donanım yolu ölçülmedi.

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
`IkonKutusuTests` `Icons.axaml`'daki yolların hepsini ayrıştırıp kenar payını ve merkezi
pimliyor — sayıyı elle yazmıyor, dosyadan okuyor — böylece bir daha
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

### macOS alt sürümü: 14 (17 Eylül 2026)

İlk karar 15'ti: osx-arm64 paketinin libmpv'si Homebrew şişelerinden geliyordu ve 48
dylib'in 47'si `minos=15.0` taşıyordu (`docs/olcumler/libmpv-macos-gomme.md`, librubberband
11.0). MPVKit yolu denendi ve tuttu: MPVKit 1.0.0'ın LGPL statik arşivlerinden bağlanan
evrensel `libmpv.2.dylib` `minos=13.0` taşıyor, kurucu onu `deps-libmpv-macos-mpvkit-1.0.0`
önsürümünden sha256 doğrulamalı indiriyor, Homebrew yalnız yedek. macos-14 ve macos-15
koşucularında başsız kare testi geçtiği için alt sürüm **macOS 14** ilan edilir; 13
koşucusu artık yok, o yüzden 13.0 yalnız yükleme komutlarının tabanı, koşulmuş bir sürüm
değil.

### Kaydedicinin kullanışlılık sırası araştırmadan gelir (13 Eylül 2026)

On iki kaydedici okundu; üç rapor `docs/arastirma/` altında duruyor:
`kaydedici-kullanislilik.md` (833 satır, sekiz başlık), `kaydedici-bolge-ve-cerceve.md`
(bölge seçimi ve çerçeve, sayılar ShareX ve OBS kaynağından), `kaydedici-otomatik-ayar-ve-hedef-boyut.md`
(OBS sihirbazının bit hızı formülü, hedef boyut, lisans yükü).

Sıra kullanıcının değil, iki ölçüte göre dizildi: **kaydedici o özellik olmadan kullanılmıyor mu**,
ve **bizi rakipten ayırıyor mu**. Numaralar aşağıda; her biri kendi kesitidir.

1. Sürükleyerek bölge seçimi + son bölgeyi hatırlama — kapı. Zorluk orta (çok monitör, DPI).
2. Genel kısayol: başlat/durdur, duraklat, iptal; `RegisterHotKey` çakışırsa kullanıcıya söylenir.
3. Tepsi simgesi: üç durumlu renk, canlı ipuçta süre **ve anlık MB** — anlık boyut bizim farkımız.
4. Bitiş penceresi: **Sıkıştır** / Klasörü Aç / Önizle. Kaydedici ile sıkıştırıcının aynı
   programda olması tek gerçek üstünlüğümüz; "Sıkıştır" birinci düğmedir.
5. Otomatik bitirme: süre sınırı ve dosya boyutu sınırı, 30 saniye kala uyarı.
6. Kayıt çerçevesi: üstte kalan, tıklamayı geçiren, kısayolla gizlenen. Zorluk yüksek.
7. Geri sayım 3-2-1 (0/3/5/10 saniye seçenekli).
8. Oran kilidi ve hazır boyutlar (16:9, 4:3; 1920x1080, 1280x720, dikey 1080x1920).
9. Otomatik kodlayıcı seçimi — **bu turda yapıldı**, aşağıya bakın.
10. Tıklama halkası ve imleç gizleme.
11. Piksel piksel klavye ayarı (ok 1 px, Shift+ok 10 px) ve büyüteç.
12. Tuş gösterimi; parola kutusunda otomatik susma. Zorluk yüksek, kitle dar.

Tam ekranda üstte duran çerçeve konusunda bir düzeltme kayda geçti: **Bandicam'de
"yanıp sönen dikdörtgen" diye belgelenmiş bir ayar yok**; yeşilden kırmızıya dönen şey
FPS bindirmesidir, dikdörtgen penceresi kayıt sırasında düz kırmızıdır. Çerçevenin kayda
karışmaması için iki yol var: `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` — Windows 10
2004 altında işe yaramaz — ve ShareX'in ucuz numarası, kayıt dikdörtgenini her yönden
1 px içeri almak. İkincisi P/Invoke istemiyor, önce o denenir.

WGC'nin sarı kenarlığı kapatılamaz: `IsBorderRequired = false` manifest yeteneği istiyor,
o da MSIX paketi demek. Paketsiz exe'de bu yol kapalı; OBS aynı isteği "planlanmadı" diye
kapattı. Pencere yakalamada sarı kenarlık bilinen ve kabul edilen kısıttır.

### Kaydedicide en iyi ayarı program seçer, kullanıcı isterse üstüne yazar (13 Eylül 2026)

Otomatik kip artık **varsayılan**. Onay kutusu kalktı; kullanıcı hiçbir şey seçmezse
`RecorderAutoPlan` merdiveni koşuyor. Elle ayar yüzeyi kaybolmuyor, "Kendim ayarlayacağım"
denince açılıyor — sıkıştırma tarafındaki davranışın aynısı.

Hedef süre ve hedef MB **isteğe bağlı**: boş bırakılırsa otomatik en iyi sonuç, doldurulursa
bit hızı `(hedef_MB × 8 × 1024 × 1024 / 1000) / süre_sn − ses_kbps` ile hesaplanıp aday
merdiveninin üstüne biniyor. Katsayı seçimi belgeye yazıldı: OBS'in `×1000/8/1024/1024`
yazımı ile yaygın `×8192` yazımı arasında %2,4 fark var, biz OBS'in yazımını kullanıyoruz.

HandBrake'in "hedef boyut kötü fikirdir" tezi biliniyor ve bilerek reddediliyor: VidShrink'in
varlık sebebi hedef boyuta oturtmak. Ama tez raporda duruyor, çünkü hedef boyut kolunun neden
iki geçiş ya da tavanlı CRF gerektirdiğini o açıklıyor.
