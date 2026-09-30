# Düzenleyicide Metin Katmanı: Maliyet Raporu

Tarih: 2026-09-30. Karar kullanıcıda; bu belge yalnız ne gerektiğini ölçer.

## Program Boyutu

**Artış: 0 MB.** Kurulu ffmpeg (`tools/ffmpeg/ffmpeg.exe`) şu bileşenlerle derlenmiş:
`enable-libass`, `enable-libfreetype`, `enable-libharfbuzz`, `enable-libfribidi`.
Filtre listesinde `ass`, `subtitles`, `drawtext` var. libmpv de ASS altyazıyı kendi
içinde çiziyor; `MpvEngine` zaten `sub-add` çağırabiliyor (`src/VidShrink.Player/MpvEngine.cs:338`).
Yeni kütüphane, yeni indirme yok.

## Önerilen Yol: ASS Dosyası

Metin katmanları zaman çizelgesinde tutulur (metin, yazı tipi, boyut, renk, başlangıç,
bitiş, konum anahtar kareleri). Hem önizleme hem dışa aktarma aynı `.ass` dosyasından beslenir:

- **Önizleme:** libmpv dosyayı `sub-add` ile yükler; çizen libass, dışa aktarmadaki ile aynı
  çizici. Ekranda görülen, dosyaya yazılanla aynı olur.
- **Dışa aktarma:** tam kodlama grafiğinin (`EditExport.cs:170-175`) sonuna `ass=` süzgeci eklenir.
- **Zamana bağlı konum (Premiere'deki gibi):** iki anahtar kare arası ASS `\move` ile doğrusal;
  çok anahtar kare her aralık için ayrı satır olur; yumuşak geçiş `\t` ve `\fad` ile.
- **Düzenleme:** program monitörünün üstünde sürüklenebilir bir çerçeve. Oynatma başı neredeyse
  o anda anahtar kare yazılır; dosya yeniden üretilir ve `sub-reload` edilir (milisaniyeler).
- **Zaman çizelgesi:** V1'in üstünde T1 izi; metin klibi video klibi gibi taşınır ve kırpılır.

`drawtext` elendi: anahtar kareli hareket ancak ifade yazarak olur, önizlemede aynısı çizilemez.

## Maliyet

| Parça | İş |
|---|---|
| Model + ASS yazıcı + testler (Core) | 1 ajan turu |
| T1 izi: ekle, taşı, kırp, sil | 1 tur |
| Monitör çerçevesi, sürükleme, anahtar kare, özellik paneli | 1–2 tur |
| Dışa aktarma bağlantısı, yazı tipi, uçtan uca test | 1 tur |

Toplam 4–5 ajan turu, 2–3 dalga. Metin yokken çalışma zamanına ek yük yok.

## Bilinen Sınırlar

- **Kayıpsız kip metin yakamaz.** `-c copy` yolları (`EditExport.cs:205, 263, 277`) kareyi
  yeniden çizmez; metin varsa dışa aktarma tam kodlamaya geçer. Yalnız metinli bölümü yeniden
  kodlamak (akıllı kesim) ikinci aşama.
- **Yazı tipi eşleşmesi.** libmpv ile ffmpeg farklı yazı tipi bulabilir. Önlem: seçilen yazı
  tipi dosyasının klasörü iki tarafa da açıkça verilir (`fontsdir`, `sub-fonts-dir`).
