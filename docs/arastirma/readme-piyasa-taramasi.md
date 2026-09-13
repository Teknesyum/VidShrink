# README Piyasa Taraması — Video ve Araç Projeleri

Tarih: 2026-09-13. Yöntem: 15 projenin README kaynağı `raw.githubusercontent.com` üzerinden
indirildi, satır/kelime sayımı ve başlık dökümü betikle yapıldı (`wc`, `grep`), ilk ekranlar
elle okundu. Buradaki her sayı indirilen dosyadan üretilmiştir, göz kararı değildir.

İncelenen sürümler, indirildiği günün varsayılan dalıdır (`master`/`main`/`dev`).

## Kaynaklar

| Proje | README kaynağı |
|---|---|
| HandBrake | https://github.com/HandBrake/HandBrake/blob/master/README.markdown |
| FFmpeg | https://github.com/FFmpeg/FFmpeg/blob/master/README.md |
| Shutter Encoder | https://github.com/paulpacifico/shutter-encoder/blob/master/README.md |
| LosslessCut | https://github.com/mifi/lossless-cut/blob/master/README.md |
| Av1an | https://github.com/master-of-zen/Av1an/blob/master/README.md |
| OBS Studio | https://github.com/obsproject/obs-studio/blob/master/README.rst |
| Upscayl | https://github.com/upscayl/upscayl/blob/main/README.md |
| ImageMagick | https://github.com/ImageMagick/ImageMagick/blob/main/README.md |
| yt-dlp | https://github.com/yt-dlp/yt-dlp/blob/master/README.md |
| Tauri | https://github.com/tauri-apps/tauri/blob/dev/README.md |
| Bruno | https://github.com/usebruno/bruno/blob/main/readme.md |
| Zed | https://github.com/zed-industries/zed/blob/main/README.md |
| bat (ek örnek) | https://github.com/sharkdp/bat/blob/master/README.md |
| Stirling-PDF (ek örnek) | https://github.com/Stirling-Tools/Stirling-PDF/blob/main/README.md |
| LocalSend (ek örnek) | https://github.com/localsend/localsend/blob/main/README.md |

## 1. İlk ekranda ne var, hangi sırayla

Ölçülen sıra, dosyanın ilk 35 satırında gerçekten görünen şeydir.

| Proje | İlk ekran sırası | Rozet | İlk 35 satırda görsel |
|---|---|---|---|
| HandBrake | başlık+rozet (aynı satırda) → 3 paragraf tanım | 3 | 3 (hepsi rozet) |
| FFmpeg | başlık → tek cümle tanım | 0 | 0 |
| Shutter Encoder | başlık → 4 rozet → arayüz ekran görüntüsü → Overview | 4 | 5 |
| LosslessCut | logo → ad → tek cümlelik iddia → rozetler → bağış → içindekiler | 7 | 7 |
| Av1an | başlık → 96 çekirdeği doyuran ekran görüntüsü → rozetler → tek paragraf | 2 | 6 |
| OBS Studio | başlık → 3 rozet → "What is OBS Studio?" tek cümle | 3 | 3 (hepsi rozet) |
| Upscayl | sürüm duyurusu + indirme düğmesi → sponsorlar → ürün görseli → başlık → alt başlık → 2 cümle → video | 1 | 4 |
| ImageMagick | başlık → 3 rozet → logo → tek paragraf tanım | 2 | 4 |
| yt-dlp | banner → 6 rozet → tek cümle tanım → içindekiler | 13 | 7 |
| Tauri | tam genişlik splash görseli → 8 rozet → Introduction | 8 | 9 |
| Bruno | logo → tek satır iddia (H3) → 6 rozet → 20 dil bağlantısı → tanım → indirme → ürün görseli | 7 | 7 |
| Zed | başlık → 2 rozet → tek cümle tanım → ayraç → Installation | 2 | 2 (hepsi rozet) |
| Stirling-PDF | logo → H1 iddia → 2 cümle fayda → rozetler → dashboard ekran görüntüsü | 3 | 6 |
| LocalSend | başlık → 3 rozet → bağlantı şeridi → dil → tek cümle tanım → içindekiler | 4 | 0 (görseller aşağıda) |
| bat | logo → 3 rozet → tek satır iddia → bölüm kısayolları → dil | 4 | 6 |

Ortak kalıp 15 projenin 14'ünde aynı: **ad/logo → rozet şeridi → tek cümlelik iddia**. Tek
istisna FFmpeg; orada rozet hiç yok.

Rozet sayısı medyanı 3. "Rozet duvarı" istisnadır: sekiz ve üzeri rozeti olan yalnız Tauri
(8) ve yt-dlp (13) var, ikisinin de kitlesi geliştirici. Son kullanıcıya satan araçlar 1–4
rozette duruyor (Upscayl 1, Av1an 2, Zed 2, OBS 3, Stirling 3, Shutter 4).

**İndirme düğmesi ilk ekranda yalnız üç projede var:** Upscayl (en üst satır, başlıktan bile
önce), Bruno (tanım paragrafından hemen sonra) ve LosslessCut (rozet biçiminde sürüm
bağlantısı). Geri kalanı indirmeyi "Installation" bölümüne, ikinci ekrana bırakıyor —
HandBrake ise README'de hiç indirme bölümü tutmuyor, siteye yolluyor.

**Ekran görüntüsü/GIF ilk ekranda dört projede var:** Shutter Encoder (arayüz, satır 8),
Av1an (çalışan CPU, satır 3), Upscayl (ürün görseli + otomatik oynayan video), Stirling-PDF
(dashboard). Av1an'ın seçimi öğretici: görsel arayüzü değil **iddianın kanıtını** gösteriyor,
96 çekirdeğin tamamı dolu.

## 2. İlk somut fayda cümlesine kaç kelime sonra varılıyor

"Görünür kelime" = rozet, bağlantı hedefi ve HTML etiketi ayıklandıktan sonra kalan metin.

| Proje | İlk somut fayda cümlesi | Ondan önceki görünür kelime |
|---|---|---|
| bat | "A cat(1) clone with syntax highlighting and Git integration." | 0 |
| Bruno | "Opensource IDE for exploring and testing APIs" (H3 başlık) | 0 |
| Av1an | encoding hızını artırır, CPU kullanımını iyileştirir | 2 |
| ImageMagick | "create, edit, compose, or convert bitmap images" | 2 |
| Tauri | "tiny, blazingly fast binaries for all major operating systems" | 2 |
| Zed | "high-performance, multiplayer code editor" | 2 |
| FFmpeg | "collection of libraries and tools to process multimedia content" | 3 |
| Shutter Encoder | "free and open-source media transcoding… built on FFmpeg" | 5 |
| Stirling-PDF | "Run it as a desktop app, in the browser, or on your own servers" | 7 |
| LocalSend | "securely share files… without needing an internet connection" | 13 |
| HandBrake | "makes new ones that work on your phone, tablet, TV…" | 24 |
| yt-dlp | "feature-rich downloader with support for thousands of sites" | 26 |
| OBS Studio | "capturing, compositing, encoding, recording, and streaming" | 42 |
| Upscayl | "enlarge and enhance low-resolution images… without losing quality" | 53 |
| LosslessCut | "The swiss army knife of lossless video/audio editing" (iddia) | 1 |
| LosslessCut | ilk somut özellik satırı (Features listesi) | 163 |

Medyan **5 kelime**. On beş projenin on biri otuz kelimeden önce faydayı söylüyor.

Geciken üçünün nedeni de tek: Upscayl'da sponsor blokları (53), OBS'te rozet ve lisans metni
(42), LosslessCut'ta içindekiler tablosu (163) araya giriyor.

Tek cümlelik iddia neredeyse her yerde **aynı gramerde**: "X, şunu yapan bir araçtır." Sıfat
kullanılıyorsa ölçülebilir olanı seçiliyor ("tiny", "high-performance", "lossless");
pazarlama sıfatı ("powerful", "amazing") yalnız Stirling-PDF ve ImageMagick'te geçiyor.

## 3. Karşılaştırma tabloları

En çarpıcı bulgu bu: **incelenen 15 README'nin hiçbirinde rakiple karşılaştırma tablosu yok.**

Tablo kullanan dört proje var, hepsi başka iş için:

| Proje | Tablo | Ne için | Satır |
|---|---|---|---|
| LocalSend | platform × paket yöneticisi | indirme yolları | 9 |
| LocalSend | platform × asgari sürüm | uyumluluk | 8 |
| Tauri | platform × desteklenen sürüm | uyumluluk | 7 |
| bat | stil bileşeni × açıklama | seçenek referansı | 16 |
| Bruno | (tablo değil, dil bağlantısı listesi) | çeviri | — |

Rakip adı geçtiğinde bile tabloya girmiyor, tek cümleye giriyor ve iddia niteliksel kalıyor:
Bruno "Postman ve benzerlerinin temsil ettiği statükoyu devirmeyi hedefliyor" diyor, sayı
vermiyor; Zed kendini "Atom'un yaratıcılarından" diye konumluyor; Shutter Encoder "FFmpeg
üzerine kurulu" diyerek rakibi değil temeli anıyor.

Karşılaştırma yapan iki proje da onu **README dışına** çıkarmış:

- bat → "Project goals and alternatives" bölümü 13 satır, karşılaştırmanın kendisi
  `doc/alternatives.md` dosyasında.
- Upscayl → "Results" bölümü 3 satır, öncesi/sonrası karşılaştırmaları `COMPARISONS.MD`
  dosyasında.

Metodoloji hiçbir projenin README'sinde yok. Sayı veren tek proje Av1an, o da sayıyı metne
değil görsele koymuş; ölçüm anlatısı `rust-av.github.io/Av1an` belgelerine bırakılmış.

**Çıkarım:** olgun projelerde README iddianın *duyurulduğu* yer; kanıt ayrı dosyada yaşıyor
ve README'den tek satırla bağlanıyor.

## 4. Bölümler: ne var, hangi sırayla, ne yok

Sıklık (15 proje üzerinden):

| Bölüm | Kaç projede | Tipik konum |
|---|---|---|
| Kurulum / İndirme | 12 | tanımdan hemen sonra |
| Katkı (Contributing) | 12 | sona yakın |
| Lisans | 11 | en son |
| Özellikler | 9 | kurulumdan önce ya da sonra |
| Belgeler / bağlantılar | 9 | ortada |
| Topluluk / destek | 8 | sona yakın |
| Geliştirme / derleme | 8 | katkıdan önce |
| Bağış / sponsor | 7 | başta (Upscayl) ya da sonda |
| İçindekiler | 5 | ilk ekranın hemen altında |
| Ekran görüntüleri (ayrı bölüm) | 3 | indirmenin üstünde |
| Yol haritası | 2 | sona yakın |
| SSS | 2 | sona yakın |

İki baskın sıra var:

- **Son kullanıcı ürünü:** iddia → özellikler → indir → belgeler → katkı → lisans
  (LosslessCut, Upscayl, Shutter Encoder, Stirling-PDF).
- **Geliştirici aracı:** tanım → başlarken/kurulum → özellikler → geliştirme → katkı → lisans
  (Tauri, Zed, Av1an, bat).

**README'ye konmayanlar** — olgunlaştıkça atılanların listesi:

- **Mimari/tasarım anlatısı.** Tauri tek satırla `ARCHITECTURE.md`'ye yolluyor. Hiçbir
  projede README içinde mimari bölümü yok.
- **Kullanım kılavuzu / seçenek referansı.** HandBrake "indirme, derleme ve kullanım için
  resmî belgelere bakın" deyip README'yi 42 satırda bitiriyor. Tek istisna yt-dlp: README'si
  bilerek man sayfası olarak da derlendiği için 2504 satır.
- **Karşılaştırma ve kıyas verileri** (yukarıdaki bölüm).
- **Sürüm notları / changelog.** Hiçbirinde gömülü değil; Shutter Encoder tek satır bağlantı
  veriyor.
- **Sorun giderme.** Yalnız 3 projede var (bat, LocalSend, Upscayl'ın SSS'i).
- **Yol haritası.** Yalnız 2 projede, ikisinde de birkaç satır ve dış bağlantı.
- **Güvenlik politikası.** Ayrı `SECURITY.md`'ye çıkmış; bat tek başlıkla 4 satır ayırmış.

## 5. Diyagram

**On beş projenin hiçbirinde README'de mermaid, akış şeması ya da mimari diyagramı yok.**
`mermaid` kelimesi taranan dosyaların hiçbirinde geçmiyor.

Görsellerin tamamı üç türden: rozet, logo, ürün ekran görüntüsü/videosu.

| Proje | README'deki toplam görsel | Türü |
|---|---|---|
| Upscayl | 14 | sponsor bannerı, ürün görseli, otomatik video |
| yt-dlp | 14 | banner + rozet |
| LosslessCut | 12 | logo, rozet, mağaza rozetleri |
| Bruno | 12 | logo, rozet, aydınlık/karanlık iki ürün görseli |
| Tauri | 11 | splash + rozet |
| bat | 8 | logo, rozet, terminal ekran görüntüleri |
| Av1an | 6 | kanıt niteliğinde tek ekran görüntüsü + rozet |
| Stirling-PDF | 6 | logo, rozet, dashboard |
| HandBrake | 4 | yalnız rozet |
| FFmpeg | 0 | — |

Anlam taşıyan (rozet ve logo olmayan) görsel medyanı **1**: tek bir ürün ekran görüntüsü.
LosslessCut video demolarını bile README'ye gömmüyor, "Video demos" başlığı altında 7 satırlık
bağlantı listesi veriyor.

## 6. Uzunluk ve okuma süresi

200 kelime/dakika üzerinden.

| Proje | Satır | Kelime | Okuma |
|---|---|---|---|
| HandBrake | 42 | 256 | ~1 dk |
| FFmpeg | 45 | 235 | ~1 dk |
| Zed | 48 | 298 | ~1,5 dk |
| Stirling-PDF | 69 | 307 | ~1,5 dk |
| OBS Studio | 79 | 300 | ~1,5 dk |
| ImageMagick | 88 | 1265 | ~6 dk |
| Tauri | 96 | 569 | ~3 dk |
| Av1an | 101 | 589 | ~3 dk |
| Shutter Encoder | 105 | 498 | ~2,5 dk |
| LosslessCut | 170 | 1427 | ~7 dk |
| Bruno | 224 | 828 | ~4 dk |
| Upscayl | 242 | 1042 | ~5 dk |
| LocalSend | 305 | 1461 | ~7 dk |
| bat | 941 | 4647 | ~23 dk |
| yt-dlp | 2504 | 19289 | ~96 dk |

**Medyan 101 satır, 589 kelime, ~3 dakika.** Beş proje 80 satırın altında. İki uzun örneğin
ikisi de CLI aracı ve README'yi bilerek referans belgesi olarak kullanıyor (yt-dlp man sayfası
üretiyor, bat `--help` yerine geçiyor); GUI'li hiçbir ürün 305 satırı geçmiyor.

VidShrink'in bugünkü README'si **652 satır** — GUI'li en uzun örneğin (LocalSend, 305) iki
katı, medyanın altı katı.

## 7. Damıtılmış "iyi README" ölçütü

1. İlk 30 görünür kelime içinde ne olduğunu ve kime ne kazandırdığını söyle. Piyasa medyanı
   5 kelime.
2. Rozet 1–4 arası; son kullanıcı ürünü rozet duvarı kurmuyor.
3. İlk ekranda en fazla bir anlamlı görsel, o da iddianın kanıtı olsun.
4. GUI ürününde indirme bağlantısı ilk iki ekranda; kurulum talimatı ayrı bölüm.
5. Rakip karşılaştırması ve ölçüm metodolojisi README'de değil, ayrı dosyada; README'den tek
   satır bağlantı.
6. Diyagram README'ye girmez.
7. Kullanım kılavuzu README'ye girmez; belgeler bölümü tek bağlantı şeridi olur.
8. GUI ürünü için hedef 100–200 satır, 3–5 dakika.
9. Kapanış üçlüsü sabittir: Geliştirme → Katkı → Lisans.

## 8. VidShrink'in README'si için öneri iskelet

Hedef: **140–170 satır, ~700 kelime, ~3,5 dakika.** Bugünkü 652 satırdan düşen hiçbir şey
silinmez, `docs/` altına taşınır.

| # | Bölüm | Satır | İçerik |
|---|---|---|---|
| 1 | Başlık + logo | 3 | Ad, tek satır. |
| 2 | Rozet şeridi | 2 | En çok 4: CI, sürüm, lisans, indirme. |
| 3 | Tek cümlelik iddia | 2 | "VidShrink, verdiğin hedef boyuta oturan videoyu ölçerek üretir." Fayda ilk 20 kelimede. |
| 4 | İndirme | 3 | Windows/macOS/Linux, doğrudan release bağlantısı. Kurulum bölümünden önce, ilk ekranda. |
| 5 | Tek ekran görüntüsü | 3 | Kanıt gösteren tek kare: hedef boy girildi, çıkan dosya o boyda. Banner yok. |
| 6 | "Neden ffmpeg yetmiyor" | 8 | Bugünkü 16 satırın yarısı: tek paragraf + üç madde, gerisi `docs/`e. |
| 7 | Özellikler | 18 | Düz madde listesi, her madde tek satır, alt başlık yok. |
| 8 | Yapmadıkları | 8 | Bugünkü bölüm iyi; kısaltılıp korunur, beklentiyi baştan kesiyor. |
| 9 | Kurulum | 25 | Windows tek satır, macOS/Linux tek satır, gereksinimler tablosu. Uzun anlatı `docs/kurulum.md`'ye. |
| 10 | Nasıl çalışır (özet) | 12 | Bugünkü 130+ satırlık anlatı yerine 12 satır: kalibrasyon, iki geçiş, durma ölçütü. Sonunda "Ayrıntı: `docs/motor.md`". |
| 11 | Ölçülen sonuçlar | 6 | Tek satır iddia + tek bağlantı: `docs/olcumler/`. Tablo README'ye girmez, metodoloji hiç girmez. |
| 12 | Belgeler | 8 | Bağlantı şeridi: kullanım, motor, ölçümler, oynatıcı, SSS. |
| 13 | Yol haritası | 5 | Üç madde + `docs/YOL-HARITASI.md` bağlantısı. |
| 14 | Geliştirme | 15 | Klon, `dotnet build`, `dotnet test`, proje ağacı 5 satır. |
| 15 | Katkı | 6 | Dal kuralı tek cümle + `CONTRIBUTING.md`. |
| 16 | Lisans | 4 | Tek paragraf. |
| — | **Toplam** | **~148** | |

README'den çıkıp gidecekler ve gidecekleri yer:

| Bugünkü bölüm | Yaklaşık satır | Nereye |
|---|---|---|
| "Nasıl çalışır"ın yedi alt başlığı (kalibrasyon, durma, HDR, algısal kalite…) | ~150 | `docs/motor.md` |
| "Ölçülen sonuçlar" tabloları | ~27 | `docs/olcumler/` (zaten var) |
| "Kullanımda neye benziyor" + Kaydedici + Kodlayıcılar + sağ tık menüsü | ~160 | `docs/kullanim.md` |
| Güncel kalma / güncelleme akışı | ~84 | `docs/kurulum.md` |
| Geliştirme ayrıntıları | ~80 | `CONTRIBUTING.md` |

Kural olarak: **README duyurur, `docs/` kanıtlar.** İncelenen on beş projenin istisnasız
uyduğu tek davranış buydu.
