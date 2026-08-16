# VidShrink Claude Teslim Raporu

Son güncelleme: 17 Ağustos 2026  
Depo: `C:\Users\Administrator\Desktop\Projeler\vidshrink`  
Dal: `main`

## Amaç

VidShrink, Windows için .NET 8 WPF ile geliştirilmiş, çevrimdışı çalışan bir medya küçültme ve dönüştürme uygulamasıdır. Temel ürün hedefleri:

- Videoyu kullanıcının belirlediği hedef dosya boyutuna küçültmek.
- Hedef boyut zorunluluğu olmadan medya biçimi, kodek, çözünürlük, kare hızı ve ses ayarlarını dönüştürmek.
- Teknik bilgisi olmayan kullanıcıya ayarların etkisini bağlamsal `?` balonlarıyla anlatmak.
- Gömülü AI veya API anahtarı gerektirmeden otomatik planlama yapmak.
- İsteğe bağlı olarak herhangi bir sohbet AI'ına istem üretmek ve dönen JSON planını doğrulamak.

## Çözüm Yapısı

- `src/VidShrink.Core`: Planlama, doğrulama, hedef boyut hesabı ve ffmpeg argüman üretimi.
- `src/VidShrink.Ffmpeg`: ffprobe okuma, ffmpeg süreç yönetimi, ilerleme, iptal ve çıktı düzeltme turları.
- `src/VidShrink.App`: WPF arayüzü, dil geçişi, özel pencere kromu ve tema.
- `docs/implementation-report.md`: Teknik uygulama ve ölçülmüş doğrulama geçmişi.
- `docs/ui-requirements-history.md`: Kullanıcının kalıcı UI talepleri. Çelişkide en yeni talep geçerlidir.
- `Launch-VidShrink.ps1`: Geliştirme amaçlı yeniden derleyerek başlatma yardımcısı.

## Mevcut İşlevler

### Küçült

- Kabul kararı dosya uzantısıyla değil ffprobe video akışıyla verilir.
- Ses bütçesi hedef toplam bit hızından ayrılır.
- Piksel başına bit tablosu CRF, iki geçişli VBR, kare hızı düşürme ve çözünürlük merdiveni kararlarını verir.
- Büyük çıktı ölçülerek en fazla üç denemede bit hızı düzeltilir.
- Sessiz video, VFR, döndürme metadata'sı ve yaygın olmayan çözülebilir kapsayıcılar desteklenir.
- AI planı isteğe bağlıdır; bozuk, eskimiş veya uyumsuz JSON otomatik plana geri düşer.

### Dönüştür

- Çıktılar: MP4, MKV, WebM, MOV, AVI, GIF, MP3, M4A ve WAV.
- Video: H.264, H.265, VP9, AV1 ve gerçek stream copy.
- Kalite: CRF veya Sabit Bit Hızı.
- Çözünürlük ve FPS: Kaynak, hazır değerler veya özel değer.
- Ses: kodlama, Kopyala veya At.
- İsteğe bağlı başlangıç/bitiş kırpması.
- GIF için `palettegen` + `paletteuse`.
- Uyumsuz container/kodek copy kombinasyonları çalıştırılmadan engellenir.
- Çalıştırılacak tam ffmpeg komutu görünürdür.

## Kritik Teknik Davranışlar

- İptal, ffmpeg süreç ağacını öldürür ve kısmi çıktıyı siler.
- stderr kuyruğu eşzamanlı güvenli biçimde toplanır.
- Çıktı adları çakışmaya dayanıklıdır.
- CRF için sahte kesin dosya boyutu gösterilmez.
- Yeniden kodlama presetleri encoder ailesine göre doğrulanır.
- Dil değiştirmek yüklü medyayı veya seçili planı sıfırlamaz.
- Türkçe varsayılan dildir; TR/EN geçişi statik ve dinamik metinleri kapsar.

## Güncel UI Sistemi

- Font: gömülü `Atkinson Hyperlegible Next`; mono değerler Consolas/Cascadia Mono.
- Normal metin alt sınırı 16 DIP; yalnızca ikincil yardım metni 14 DIP olabilir.
- Kısa UI metinleri Her Kelimenin İlk Harfi Büyük; uzun açıklamalar doğal cümle biçimindedir.
- Nötr metin tam beyazdır. Başlıklar ve alan etiketleri neon mavidir.
- Pembe/mor kalıcı metin rengi değildir; hover, odak, seçim ve ince vurgu için kullanılır.
- Metinlerde glow yoktur.
- Genel köşe yarıçapı 6 DIP'tir; yalnızca işlevsel daireler istisnadır.
- Birleşik görünen komşu kontroller kabul edilmez; sekmeler arasında 8 DIP boşluk vardır.
- Panel ve sekme anahatları dört kenarda tam kapanır; layout rounding ve pixel snapping aktiftir.
- Bir hücre yeniden boyutlandırılırken nesne, stroke, DPI yuvarlaması ve her iki eksende en az 2 DIP güvenlik payı birlikte hesaplanır; 20×20 checkbox 24×24 DIP hücrede ortalanır.
- Tüm uygulamanın dışında 1 DIP neon mavi anahat vardır.
- Yüzey rengi `Themes/Theme.xaml` içindeki merkezi `SurfaceToneColor` kaynağından gelir.
- Üst bar gradienti `TitleBarBackground`, alt şerit gradienti `SecondaryBarBackground` kaynağındadır.
- Üst bar 38 DIP; pencere düğmeleri 42×30 DIP; simge 26 DIP.
- Küçült simgesi 10×2 DIP çizilmiş kısa çizgidir.
- Sponsor bağlantıları üst bardadır: `Buy Me A Coffee`, `GitHub / By Teknesyum`, ardından pencere düğmeleri.
- Hedef slider dolu kısmı pembe/mavi anahatlı; thumb mavi/pembe anahatlıdır ve hover tepki verir.
- Ana hedef değeri neon mavi ve kalın; preset chip metinleri beyazdır.
- Çıktı paneli kompakt `254 DIP` yüksekliğindedir; toplam sütun yüksekliğine bağlanan döngüsel yerleşim kullanılmaz.
- Scrollbar tamamen özel temalıdır: 10 DIP koyu yol, mavi thumb, pembe hover ve mor sürükleme tepkisi.
- `?` rozetleri 12×12 DIP, metinden 12 DIP uzakta üst simge konumundadır; hover yalnızca işaret rengini pembeye çevirir ve iki dilde ayrıntılı tooltip gösterir.

## İkon ve Masaüstü Kısayolu

- PNG şeffaf arka planlıdır.
- ICO içinde 16, 20, 24, 32, 40, 48, 64, 128 ve 256 px katmanları bulunur.
- `ApplicationIcon` proje dosyasında tanımlıdır ve pencere ikonu ayrıca pack URI ile atanır.
- Masaüstü `VidShrink.lnk`, proje içindeki sabit Debug `.exe` çıktısını hedefler; PowerShell hedefi kullanılmaz çünkü görev çubuğu simge eşleşmesini bozar.

## Çalışma ve Doğrulama

Bu ortamda `AGENTS.md` ve `C:\Users\Administrator\.codex\RTK.md` geçerlidir. Shell komutları `rtk` ile başlamalıdır.

```powershell
rtk dotnet build VidShrink.sln
rtk dotnet test VidShrink.sln --no-build
rtk git diff --check
```

UI değişikliğinde derleme tek başına yeterli değildir:

1. Yalnızca bu depo içindeki açık `VidShrink.App.exe` işlemini kapat.
2. Debug çözümünü derle.
3. Masaüstündeki `VidShrink.lnk` ile uygulamayı gerçekten aç.
4. İşlemin açık ve yanıt verir olduğunu doğrula.
5. Türkçe başlangıç, İngilizce geçiş, üç sekme, hover/focus, dar ve normal pencere boyutlarını görsel kontrol et.
6. Bir piksellik kenar veya yükseklik farkını hata kabul et.

## Değişiklik Disiplini

- Kullanıcının ilgisiz değişikliklerine dokunma.
- Sabit renk ve ölçüyü farklı kontrollere kopyalama; semantik tema tokenı oluştur.
- WPF native kontrol görünümünü geri getirme.
- Türkçe ve İngilizce anahtarları birlikte güncelle.
- `LanguageCatalog` ters sözlüğünde aynı Türkçe değeri iki İngilizce anahtara verme; çalışma zamanında çökebilir.
- UI değişikliğini `docs/ui-requirements-history.md` ve `docs/implementation-report.md` içine işle.
- Derleme, gerçek açılış ve görsel doğrulama yapılmadan tamamlandı deme.
- Tamamlanan değişiklikleri açık kapsamlı commit ile `main` dalına gönder.

## Bilinen Belge Notu

`implementation-report.md` kronolojik bir geçmiş olduğu için eski paragraflarda artık geçerli olmayan önceki renk veya yerleşim kararları görülebilir. Güncel karar için önce `ui-requirements-history.md` içindeki en yeni maddeye, sonra mevcut XAML kaynaklarına bakılmalıdır.

## Kopyalanabilir Promptlar

- Projeyi devralacak ajan: `docs/claude-project-agent-prompt.md`
- UI tasarımını yönetecek ajan: `docs/claude-ui-agent-prompt.md`
