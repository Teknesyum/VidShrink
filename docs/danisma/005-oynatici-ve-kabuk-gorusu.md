# Oynatici ve kabuk gorusu

- soran: T0
- danisilan: fable
- tarih: 2026-09-05

## Sorulan

C:\Users\Teknesyum\.claude\plugins\cache\teknesyum\teknesyum-core\0.15.0\roles\advisor.md dosyasini oku ve onu uygula.
Soran opus kosuyor. Turkce yaz.

Proje koku: C:\Users\Teknesyum\Desktop\Projeler\VidShrink
VidShrink: hedef boyuta sikistiran video araci. .NET 8 + Avalonia + ffmpeg, Windows.

## Kullanicinin istegi, kendi cumlesiyle

*"vidshrinke video oynayici sekmesi de ekle direk videolarimizi oynatabilecegimiz bir
modulu de olacak farenin ileri tekerlegi 1sn ileri saracak geri tekerlek 1 sn geri saricak
ctrl ile tekerlek 10 sn shift tekerlek 60 sn ctrl shift tekerlek 300sn olacak sag klik ve
space play pause orta click fullscreen ile onceki hali arasinda gecis yapicak sol click
suan icin bos alt ile fare tekerlegi local zoom yapicak .mp4 gibi video dosyalarinda sag
menude VidShrink ile Ac secenegi olacak altinda VidShrink ile Kucult secenegi de olacak
burda hizli kucultme seceneklerini secebilecek kullanici winrar in klasore ayiklasi gibi
kullanisli olacak programi acmadan kullanicinin ayarlarini program hatirlayacak"*

## Uc dugumde gorus istiyorum

**A. Oynatici hangi boruyu kullanmali?** Depoda zaten calisan bir oynatma hatti var ama o
**iki kaynagi yan yana** karsilastirmak icin kurulmus. Tek video oynatan bir sekme icin o
hatti mi genisletmeli, yanina ikinci bir tek-kaynak hatti mi kurmali, yoksa bambaska bir
sey mi (LibVLCSharp gibi hazir bir oynatici) dogru olur? Maliyeti ve kirilma noktalarini
soyle.

**B. Kabuk menusu ne kadar ileri gitmeli?** "VidShrink ile Ac" tek bir giris, ama "VidShrink
ile Kucult" altinda **hizli kucultme secenekleri** olacak ve kullanici *"winrar'in klasore
ayiklasi gibi"* diyor — yani programi acmadan, tek tikla, is bitsin. Bu Windows'ta
`HKCU\Software\Classes` alt menusu (`SubCommands` / `ExtendedSubCommandsKey`) ile mi
yapilmali, yoksa IExplorerCommand COM sunucusu mu gerekir? Windows 11'in yeni baglam
menusu bu ikisini ayni sekilde tasimiyor — hangisi bu proje icin dogru ve neyi feda eder?

**C. "Programi acmadan is bitsin" ne demek olmali?** Kullanici sag menuden "16 MB'a kucult"
dedi. Pencere hic acilmasin mi, kucuk bir ilerleme penceresi mi acilsin, yoksa gorev
cubugunda mi ilerlesin? Hata olursa kullanici nasil ogrenir? Bu bir urun karari ve
kullanicinin cumlesi tek basina cevaplamiyor.

## Kanit — olculmus durum, iddia degil

Mevcut oynatma hatti (satir sayilariyla):

```
src/VidShrink.Ffmpeg/Playback/PipeComparisonFrameSource.cs   494   ffmpeg borusu, kare kaynagi
src/VidShrink.Ffmpeg/Playback/ComparisonGraph.cs             170   filtre grafigi (hstack)
src/VidShrink.App/Playback/PanelHost.cs                      996   durum makinesi
src/VidShrink.App/Playback/ComparisonPanel.axaml.cs          938   panel
src/VidShrink.App/Playback/ComparisonSurface.cs              468   cizim yuzeyi
src/VidShrink.App/Playback/ControlStrip.axaml.cs             408   oynat/duraklat/sar seridi
src/VidShrink.App/Playback/SegmentEncoder.cs                 408   parca kodlayici
src/VidShrink.App/Playback/ZoomGesture.cs                    290   tekerlek yakinlastirma
src/VidShrink.App/Playback/HoverZone.cs                      250
```

`ComparisonGraph` **iki girdiyi `hstack` ile yan yana koyuyor** — hat tasarim geregi
karsilastirma icin. `PanelHost` "parca modu"nda calisiyor: boru yalniz **2 saniyelik
pencereyi** taniyor, atlama demek o ana yeni bir pencere kodlamak demek. Yani hat
`SegmentEncoder` ile kisa parcalar kodlayip oynatiyor, tam dosyayi bastan sona akitmiyor.

`ZoomGesture` zaten var ve `Wheel(double notches, double anchorX, double anchorY)`
imzasiyla capa noktasindan yakinlastiriyor — kullanicinin istedigi "alt + tekerlek local
zoom" buna oturabilir.

Sekmeler bugun (`src/VidShrink.App/MainWindow.axaml`): Kucult, Donustur, Ayarlar,
Hakkinda, Gelismis. Oynatici altinci sekme olacak.

Kabuk yuzeyi bugun tek dosyada (`src/VidShrink.Core/ShellIntegration.cs`, 50 satir):
24 uzantilik `MediaExtensions` listesi ve `ResolveStartupPath` — uygulamaya argumanla
gelen yolu cozuyor. **Kayit defterine hicbir sey yazmiyor**; docstring "kayit defteri
girdileri de buradan beslenir" diyor ama o kod henuz yok.

Kalici ayar mekanizmasi var: `MainWindow.axaml.cs:791 SaveSettings()` ve `:173-187`
`Watch(...)` baglantilari kontrolleri izleyip kaydediyor.

Kurulum betikleri: `Install-VidShrink.ps1` (22 KB), `install-vidshrink.sh` (10 KB),
`Launch-VidShrink.ps1`. Uygulama `src/VidShrink.Launcher` ile `net8.0-windows` `win-x64`
olarak da paketleniyor.

Su an ayni anda iki sozlesme kosuyor ve ikisi de motorun karar merkezine dokunuyor
(kodek kilidi ve elle gecersiz kilma); oynatici isi onlardan **sonra** baslayacak.

## Bu depoda seni baglayan kurallar

- Renk ve olcu yalniz `src/VidShrink.App/Themes/Theme.axaml` belirteclerinden; uydurma yasak.
- `teknesyum-ui` arayuz standardi bu makinede **kurulu degil**.
- Kod yorumu yazilmaz.
- Yeni bagimlilik eklemek ucretsiz degil: depo bugun ffmpeg disinda harici oynatici
  kullanmiyor.

## Ne istiyorum

advisor.md'nin uc basligi, en fazla 20 satir. Plan yazma, kod yazma, dosya olusturma.
A, B ve C dugumlerinin **ucune de** deg; birini secip otekileri atlama.

## Donen

## Call
A: Karşılaştırma hattını genişletme; onun yanına ffmpeg tabanlı **ikinci, tek-kaynak, tam-akış** bir kaynak kur (`SingleFrameSource`, hstack yok, parça modu yok). LibVLCSharp'ı ancak ses + sar gecikmesi ölçümde başarısız çıkarsa aç. `ZoomGesture` ve `ControlStrip` aynen paylaşılır.
B: `HKCU\Software\Classes\SystemFileAssociations\.mp4\shell\VidShrink` + `SubCommands` ile kayıt defteri; COM sunucusu yok.
C: Pencere açılmasın; görev çubuğunda küçük, kapatılabilir ilerleme penceresi (tray değil). Hata: pencere kalır, tek satır neden + "Uygulamada aç" düğmesi.

## Why
A: Mevcut hat 2 sn'lik pencere kodluyor; 1/10/60/300 sn atlamalar seek-tabanlı tam akış ister, ayrıca ses hiç yok — o hattı bükmek `PanelHost`'un 996 satırlık durum makinesini ikinci bir moda zorlar. LibVLC 60+ MB native yük ve depoda ilk harici oynatıcı olur; bedeli önce ölçülmeden ödenmez.
B: Windows 11 yeni menüde her iki yol da "Daha fazla seçenek" altına düşer; IExplorerCommand yalnız MSIX paketle üst menüye çıkar, bu depo betikle kurulum yapıyor. Kayıt defteri yolu `Install-VidShrink.ps1`'e 30 satır, geri alınabilir, imza gerektirmez.
C: WinRAR'ın kendisi de ilerleme penceresi açar; "programı açmadan" kullanıcı için "ana pencereyi kurcalamadan" demek. Tamamen sessiz kodlama, hata durumunda kullanıcıya görünmez.

## Missed
- Ses: mevcut boru yalnız kare veriyor; tek-kaynak oynatıcıda ses çıkışı (NAudio/WASAPI) yeni bağımlılık — LibVLC kararının asıl terazisi budur, önce sor.
- Alt seçenekler (16 MB, 25 MB, 50 MB…) kayıt defterinde sabit yazılır; kullanıcı ayarları değiştirince kayıt defteri de yeniden yazılmalı — `SaveSettings()` bu senkronu tetiklemeli.
- Aynı anda 5 dosya seçilip "Küçült" denince 5 süreç mi, tek kuyruk mu? `MultiSelectModel=Player` yoksa çakışır.
- Orta tık tam ekran + tekerlek bağlamaları Avalonia'da `PointerWheelChanged` modifier'larıyla çözülür; ancak Alt+tekerlek Windows'ta pencere menüsünü çalabilir, test edilmeli.
- Kayıt defteri temizliği: kaldırma betiğine karşı girdi (`Uninstall`) yoksa çöp kalır.
