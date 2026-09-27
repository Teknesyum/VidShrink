# Plan: Tek Paketli Güncelleme, Uc'nin Tamamı

2026-09-27. İstek: "update kısmında panel açılıyor dosyalar tek tek iniyor; tek bir update dosyası
inip PC'de açılıp update yapılmalı. Kullanıcı dosyaları yalnız yüklenirken ya da ilk kurulumda
görmeli, indirmede yalnız ilerleme çubuğu. Çok kısaysa kurulum sürecini göstermeyebilirsin."
Ve: "uc ne diyorsa yap, son halini göreyim, gerekirse geri alırım."

## Başlangıç Ölçümü

- Kurulu uygulama eski renklerle açılıyor: `%APPDATA%\VidShrink\settings.json` `"theme": "Neon"`.
  0.10.0 planının "kayıtlı seçim korunur" kararı varsayılan değişikliğini kurulu kullanıcıya
  ulaştırmadı. UI eklentisine raporlandı.
- Uc 2 raporunun açıkları: 43 yazı 14 px (düzen en az 16), düğme parıltısı kırpılıyor,
  eski paletlerde AyuLight 1,65, Monokai/Solarized PinkText, 15 vurgu dolgusu, 8 düşürülmüş taban.
- Kısayol simgeleri güncellenmedi.

## Karar

- "Kayıtlı seçim korunur" geri alınır: tek seferlik göç, eski varsayılan `Neon` kayıtlıysa
  `Teknesyum` olur. Kullanıcı sonra elle seçerse seçimi kalır.
- Uc'nin sahibe bıraktığı her açık uygulanır; sahip son hâli görüp gerekirse geri alır.
- Güncelleme tek arşiv olarak iner, yerelde açılır, yerinde takasla kurulur.

## İşler

| # | İş | Sahip | Bağımlılık |
|---|---|---|---|
| 1 | Tek paketli güncelleme | ajan (opus) | — |
| 4 | Uc'nin tamamı | ajan (opus) | — |
| 5 | Kısayol simgeleri | T0 | 6 |
| 6 | Birleşim, CI, sürüm | T0 | 1, 4 |
