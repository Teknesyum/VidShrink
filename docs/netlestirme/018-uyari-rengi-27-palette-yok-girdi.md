# Danışma Girdisi — Uyarı Rengi 27 Palette Yok (18 Eylül 2026)

Danışılan: fable. Aşağıdaki metin birebir gönderildi.

---

VidShrink (.NET 8 + Avalonia) arayüzünde bir renk boşluğu var; tek bir karar istiyorum, kod yazma.

Durum:
- Renk yalnız `src/VidShrink.App/Themes/Palette/<Ad>/Theme.axaml` dosyalarından gelir. 27 palet var (Ayu, Cobalt, Dracula, Monokai, Neon, Nord, RosePine, Synthwave, Gruvbox vb.).
- Her palet aynı anahtar kümesini taşır: `NeonSuccessColor`, `NeonPinkColor`, `NeonEmberColor`, `NeonBlueColor`, `NeonPurpleColor`, `OnNeonColor`, `TextBodyColor`, `AppBgColor` …
- `Themes/Controls.axaml` iki durum teması tanımlar: `StatusSuccess` → `NeonSuccess`, `StatusError` → `NeonPink`. **Uyarı durumu yok.**
- `NeonEmberColor` adı kehribar çağrıştırıyor ama 27 paletteki gerçek değerler kırmızı/pembe ailesinde: #FF9D0006, #FFCC241D, #FFDC322F, #FFF07178, #FFFF5555, #FFEB6F92 … Yani ember uyarı rengi olarak kullanılamaz, hatadan ayırt edilmez.
- Somut kurban: `Recorder/RecorderView.axaml:180` `TxtWarning`, "kayıt yarım kaldı" (`recorder.output.partial`) iletisini `StatusError` ile, yani hata rengiyle yazıyor. Yarım kayıt bir uyarı; dosya duruyor, oynatılabiliyor.

Proje kuralı: "renk uydurma". 27 palete elle 27 yeni kehribar seçmek tam da renk uydurmak olur ve her paletin kimliğini bozma riski taşır.

Üç yol görüyorum:
A) Her palete `NeonWarningColor` eklemek — 27 renk seçimi, paletlerin asıl kaynağına (Ayu/Nord/Dracula'nın yayımlanmış renk listeleri) bakarak. Doğru ama 27 dosya ve dış kaynak araştırması.
B) Uyarıyı renkten değil biçimden ayırmak: aynı gövde rengi + uyarı simgesi + farklı ağırlık; hata kırmızı kalır, uyarı nötr olur. Renk uydurulmaz.
C) Var olan bir anahtarı uyarıya atamak (ör. `NeonBlue` ya da `NeonPurple`) ve `StatusWarning` temasını ona bağlamak. Ucuz ama anlamsal olarak zayıf: mavi "bilgi" demek, "dikkat" demez.

Sorum: hangisi? Tek bir yol seç ve neden onun diğer ikisinden iyi olduğunu söyle. Seçtiğin yol A ise, 27 rengi nereden alacağımı (her paletin yayımlanmış kaynağındaki hangi rol) tek cümlede söyle. Seçtiğin yol B ise, uyarının hatadan nasıl ayrılacağını söyle — kullanıcı kolaylığı ön planda.

Türkçe yanıtla, kısa tut: karar + gerekçe + uygulama tarifi. Toplam 15 satırı geçme.
