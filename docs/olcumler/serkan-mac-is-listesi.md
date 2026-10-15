# Serkan'ın Mac İş Listesi

Tarih: 15 Ekim 2026 · Karar: `docs/piyasa/2026-10-13/karar-sorulari.md`, "Sahibin Cevabı" 2. madde (Mac işleri Serkan'a verilir).
Kaynak paketler: `docs/later.md` 111 (EKA-P12), 112 (EKA-P13), 131 (PLT-P4), 134 (PLT-P7), 146 (EKB-P17).

Bu belge yalnız ölçüm içindir. Kod yazma işleri ayrı bir ajan turunda yapılır; burada Serkan'ın Mac'te yalnız
**gözle ve komutla doğrulayacağı** şeyler var. Önceki Mac turları (`docs/olcumler/macos-gercek-kosum.md`,
`docs/olcumler/videotoolbox.md`) aynı düzenekle yapıldı.

## Önce Okuyacağın Üç Kural

1. Her iş kendi dalında: `git switch -c serkan/<iş>`. `main`e yalnız T0 birleştirir (kök `AGENTS.md`, "Dallar").
2. Geçici her şey `.calisma/serkan-<iş>/` altına. İş bitince kendi bıraktığını sil; sayılar `docs/olcumler/` altındaki belgeye girer.
3. Uzun ya da tüm çekirdekleri dolduran yük yok. Aşağıdaki her komut birkaç dakikayı geçmez. Kodlama döngüsü gerektiren
   ölçüm (iş 4'ün kapısı) GitHub Actions'ta koşar, senin Mac'inde değil.

## Makine Şartı

| Şart | Değer | Neden |
|---|---|---|
| İşlemci | Apple Silicon (M1 ve üstü) şart; Intel Mac da varsa iş 1 ve iş 5 ayrıca Intel'de tekrarlanır | Yayında `osx-arm64` ve `osx-x64` var (`install-vidshrink.sh`) |
| macOS | 14 ya da üstü (önceki tur 26.6.2 idi) | MPVKit libmpv'si macOS 14+ (`tools/mpvkit-macos/AGENTS.md`) |
| Bellek ve disk | 8 GB bellek yeter; 10 GB boş disk (tahmin) | önceki tur 8 GB M1'de koştu |
| Araçlar | Homebrew, git, .NET 8 SDK, ffmpeg | aşağıdaki kurulum |
| Ekran | Gerçek ekran ve açık kullanıcı oturumu | Ekran Kaydı izni uzaktan verilemez |

## Depoyu Kurma

Her adım tek komut. Terminal uygulamasında sırayla:

```
brew install git dotnet@8
```

```
brew install ffmpeg
```

```
git clone https://github.com/Teknesyum/VidShrink.git
```

```
cd VidShrink
```

```
dotnet build -c Release
```

ffmpeg'in gereken yeteneklerini doğrula (iki komutta da bir satır çıkmalı):

```
ffmpeg -hide_banner -devices | grep avfoundation
```

```
ffmpeg -hide_banner -encoders | grep videotoolbox
```

Önceki turda Homebrew ffmpeg'inde `zscale` yoktu; kalite ölçümü isteyen işlerde bu önemli.
Ayrıntı: `docs/olcumler/macos-gercek-kosum.md`, "Düzeltme" bölümü.

Libmpv isteyen testler için kurulu dylib'in yolunu ver (dosya adı `ls tools/libmpv` ile görülür; aşağıdaki ad doğrulanmadı):

```
export VIDSHRINK_LIBMPV="$PWD/tools/libmpv/libmpv.2.dylib"
```

## Ekran İzinleri

macOS izni **terminal uygulamasına** verir (Terminal, iTerm ya da VS Code); ffmpeg ondan miras alır.

| İzin | Nereden | Hangi iş |
|---|---|---|
| Ekran Kaydı | Sistem Ayarları > Gizlilik ve Güvenlik > Ekran ve Sistem Sesi Kaydı | iş 1, 2, 3, 5 |
| Mikrofon | Sistem Ayarları > Gizlilik ve Güvenlik > Mikrofon | iş 1, 2 |
| Girdi İzleme | Sistem Ayarları > Gizlilik ve Güvenlik > Girdi İzleme | yalnız iş 5 |

Menü adları macOS sürümüne göre değişir; adı tutmazsa Sistem Ayarları'nda "Gizlilik" yaz ve ara.
İzni verdikten sonra terminal uygulamasını kapatıp yeniden aç; macOS izni açık oturuma uygulamaz.
**İzin verilmemiş hali de bir ölçümdür** (iş 1): önce izinsiz koş, sonra ver.

## İş 1: Mikrofon Ve Ekran Kaydı İzin Akışı (PLT-P4, later 131)

Soru (`docs/piyasa/2026-10-13/platform-esitligi-dogrulanmis.md`, "Doğrulanamadı" PLT-04): **Info.plist'te mikrofon açıklaması
olmayan pakette macOS, ffmpeg çocuk sürecine mikrofonu veriyor mu?** Vermiyorsa satır çekirdek eksiktir ve önceliği 1 olur.

Dal: `serkan/macos-izin-akisi` · Sonuç: `docs/olcumler/macos-izin-akisi.md`

1. Uygulamayı bu ağaçtan kur (paket `macos-app-bundle.sh` ile üretilir, bugün izin anahtarı taşımaz):
   ```
   sh install-vidshrink.sh
   ```
2. Uygulamayı aç, kaydedicide yalnız mikrofonla 5 saniyelik kayıt yap. Yaz: dosya oluştu mu, sessiz mi, izin penceresi çıktı mı, hangi uygulama adıyla.
3. Aynı şeyi ekran kaydıyla yap (izinsiz): dosya siyah mı, hata satırı çıkıyor mu.
4. Karşılaştırma için çıplak ffmpeg, terminalden, mikrofon izniyle:
   ```
   ffmpeg -f avfoundation -i ":0" -t 5 .calisma/serkan-izin/mikrofon.wav
   ```
5. Paketin izin anahtarı sayısı (bugün 0 beklenir). Yol `install-vidshrink.sh` çıktısında yazan paket yoludur:
   ```
   grep -c "UsageDescription" "<paket yolu>/Contents/Info.plist"
   ```

Belgeye tablo: koşum (izinsiz/izinli) × yol (uygulama/çıplak ffmpeg) × sonuç (dosya baytı, süre, ekranda gördüğün).

## İş 2: Sistem Sesi (EKA-P12, later 111)

Durum: ScreenCaptureKit yardımcı ikilisi **henüz yazılmadı**. Bugünkü pay taban ölçümüdür; yardımcı bir dalda gelince
aynı belgenin ikinci bölümü doldurulur.

Dal: `serkan/macos-sistem-sesi` · Sonuç: `docs/olcumler/macos-sistem-sesi.md`

Bugünkü taban:

1. Aygıtları listele, çıktıyı belgeye aynen yapıştır:
   ```
   ffmpeg -hide_banner -f avfoundation -list_devices true -i ""
   ```
2. Sanal ses aygıtı (BlackHole gibi) kurulu mu yaz. Kurulu değilse uygulamanın sistem sesi kaynağı sunmadığını ekran görüntüsüyle göster.
3. Müzik çalarken 5 saniye kaydet; sessiz olması beklenir:
   ```
   ffmpeg -f avfoundation -i ":0" -t 5 .calisma/serkan-sistem-sesi/taban.wav
   ```

Yardımcı gelince (adı ve dalı belgeye yazılır):

1. Müzik çalarken yardımcıyla 10 saniye kayıt al, sonra seviyeyi ölç:
   ```
   ffmpeg -i .calisma/serkan-sistem-sesi/yardimci.wav -af volumedetect -f null - 2>&1 | grep -E "mean_volume|max_volume"
   ```
2. Sürücü kurmadan (BlackHole yok) çalıştığını doğrula. Sonra Ekran Kaydı iznini kapat-aç, davranışı yaz.

## İş 3: Pencere Yakalama (EKA-P13, later 112)

Bağımlılık: iş 2'nin yardımcı ikilisi gelmeden yeni davranış ölçülemez (later 112: EKA-P12 bitmeden başlamaz). Taban bugün alınır.

Dal: `serkan/macos-pencere` · Sonuç: `docs/olcumler/macos-pencere.md`

Bugünkü taban (kod pencereyi ekran kırpması olarak kaydediyor, `RecorderWindows.cs:93`):

1. Küçük bir pencere (örneğin Notlar) aç, uygulamada pencere hedefini seç, 10 saniyelik kayıt başlat.
2. Kayıt sürerken pencereyi sürükle, sonra üstüne başka pencere getir.
3. Kaydı durdur, videoyu aç. Beklenen: taşınınca kırpma boş masaüstünü, örtülünce örten pencereyi gösterir.
   İki durumun ekran görüntüsünü `docs/olcumler/macos-pencere/` altına koy.

Yardımcı gelince üç adımı tekrarla. Geçme ölçütü: pencere taşınınca ve örtülünce kayıt bozulmaz.

## İş 4: VideoToolbox Hızlı Kip (PLT-P7, later 134)

Kapı ölçümü kodlama döngüsüdür. `tools/kalite-paketi-3/hb.ps1` yalnız GitHub Actions'ta koşar (betiğin 31. satırı:
"kullanicinin makinesinde kodlama dongusu yasak"). Sekiz hücreyi Serkan'ın Mac'i değil `macos-15` koşucusu (Apple M1 sanal) koşar.
Kalite ölçeği kodu yazıldıktan sonra T0 tetikler:

```
gh workflow run handbrake-kiyas.yml -f isler=vthizli
```

Serkan'ın payı kısa bir gerçek-donanım doğrulamasıdır.

Dal: `serkan/macos-vt-dogrulama` · Sonuç: `docs/olcumler/macos-vt-dogrulama.md`

1. Uydurma bayrak reddediliyor mu (kapının N1 ölçütü). Beklenen: çıkış kodu 0'dan farklı ve `Unrecognized option 'foo'`:
   ```
   ffmpeg -hide_banner -f lavfi -i testsrc2=s=320x240:d=0.2 -c:v hevc_videotoolbox -foo 1 -f null -
   ```
   Hemen ardından çıkış kodunu yaz:
   ```
   echo $?
   ```
2. Planın hangi kodeki seçtiğini gör (kaynağı sen seç, 30 saniyeyi geçmesin, dosya adını belgeye yaz). `--plan-only` kodlamaz:
   ```
   dotnet run --project tools/VidShrink.Bench -c Release -- shrink KAYNAK.mp4 5 --out .calisma/serkan-vt/ --speed fast --plan-only
   ```
   Bugün yazılım kodeki beklenir; kalite ölçeği kodu gelince `hevc_videotoolbox` görünmelidir.
3. Gözle: kod gelince çıkan kısa videoyu aç, ekran kesitinde blok ya da donma var mı yaz (VMAF bunu göstermez).

## İş 5: Girdi Efektleri macOS'ta (EKB-P17, later 146)

Dinleyiciler henüz yazılmadı (`RecorderView.Girdi.cs:14-22` boş). Bugünkü pay izin davranışıdır; efektin kendisi kod gelince denenir.

Dal: `serkan/macos-girdi-izni` · Sonuç: `docs/olcumler/macos-girdi-izni.md`

1. Girdi İzleme izni yokken tıklama halkası kutusunu aç, kayıt başlat, tıkla. Halka görünüyor mu, hata satırı var mı yaz.
2. İzni ver, terminali yeniden aç, tekrarla.
3. Tablo: izin (yok/var) × efekt (halka, tuş gösterimi, büyüteç) × görüldü mü.

## Sonucu Teslim Etme

Her iş için sırayla:

```
git add docs/olcumler
```

```
git commit -m "Mac olcumu: <is adi>"
```

```
git push -u origin serkan/<is>
```

Teslim şartı: belgede ortam tablosu (`sysctl -n machdep.cpu.brand_string`, `sw_vers`, `ffmpeg -version | head -2` çıktıları aynen),
her komutun ham çıktısı ve "Ölçülmeyenler" listesi olsun. Ölçmediğini ölçtüm diye yazma; biçim `docs/olcumler/videotoolbox.md` ile aynı.
