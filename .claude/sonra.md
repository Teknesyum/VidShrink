# Sonra

- osx-arm64 libmpv: Homebrew dylib'lerinin 47/48'i minos=15.0 → paket macOS 13-14'te açılmaz. Daha düşük deployment target'lı derleme (kendi derleme ya da MPVKit) — v1 macOS alt sürümü kararı yayın öncesi.
- osx-arm64 libmpv: Developer ID imza + hardened runtime + notarization denenmedi.
- osx-arm64 libmpv: oynatma-yalnız LGPL derlemesi, kodlayıcılar 13,3 MB (x265, SVT-AV1, x264, lame, vmaf) düşer.
- osx-arm64 libmpv: yayında Homebrew şişe adresi + sha256 sabitlenir; 48 kitaplığın lisans metni ve kaynak teklifi pakete.
- osx-x64: ayrı Intel libmpv derlemesi (universal değil).

- CI ve release'deki sabit libmpv arşivi (shinchiro) büyük olasılıkla Aralık 2026 civarı kalkar; o gün indirme adımı kırmızı olur. Önce yeni sürüme sabitle ya da arşivi depo sürümüne kopyala (B ajanı raporu, 11 Eylül 2026).

- 3. dalga: son açılanları çeken ekran görüntüsü aracı gerçek AppData'daki `player-recent.json`'a yazıyor; araç `.calisma/` altına yönlendirilmeli.
- 3. dalga: ekran görüntüsü klasörü seçicisi, gerçek dosya sonu otomatik sonraki ve gerçek sürükle-bırak olayı testsiz (sentetik olayla sınandı).
- Keyframe arama yarışı dalı: açılışta ilk kare zaman aşımı (OynaticiMotorTests satır ~252) incelenmedi.
