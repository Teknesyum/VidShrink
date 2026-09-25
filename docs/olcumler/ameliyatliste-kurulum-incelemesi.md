# AmeliyatListe Kurulum İncelemesi

Kaynak: `Projeler\!Arşivlendi\AmeliyatListe`. Satırlar T0 tarafından `Installer.tsx:15-21`, `:182-259` ve `app.css:550-556` üzerinde yerinde doğrulandı.

## Alınacak (Arayüz ve Akış)
- Uygulama exe'si adında "kurulum" geçince kurulum kipinde açılıyor (`kurulum.rs:26-32`). Kenarlıksız pencere 720x540 (`lib.rs:32-38`).
- Beş adım listesi, her adım ✓ / ! / sıra numarası taşıyor (`Installer.tsx:15-21, 141-151`).
- Adım renkleri: süren mavi ve parıltılı, biten yeşil, hatalı pembe (`app.css:550-555`).
- Gradyan ilerleme çubuğu ve yüzde; hareket azaltma tercihine uyuyor (`app.css:556-577, 614-616`).
- Günlük tek aralıklı yazıyla, en çok 300 satır, üstü solarak kayboluyor, son satır parlak (`Installer.tsx:66, 99-105`).
- Kurulum yeri satırında "Değiştir" düğmesi (`Installer.tsx` yer-ad).
- Düğme sırası: Kur → pasif "Kuruluyor" → "Kapat" + "Programı aç"; hatada "Yeniden dene" (`Installer.tsx:227-259`).
- Hata iletileri Türkçe ve anlaşılır: internet yok, erişim reddedildi (`kurulum.rs:76-108`).
- Yazma izni önceden deneniyor; yönetici yetkisi gerekmiyor, kurulum kullanıcı profiline (`kurulum.rs:174-177`).
- `AL_OTOMATIK` sessiz kip ve `AL_PROVA` prova kipi (`kurulum.rs:49-51, 345-364`).

## Alınmayacak
- Özel depo + gömülü salt okunur SSH anahtarı + MinGit ile çekme (`kurulum.rs:9-13, 200-268`). Anahtar README'de açık duruyor; kullanıcı kuralı gereği bu desen taşınmaz. VidShrink GitHub Releases + sha256 ile kalır.
- Sürüm seçimi yok, hep `main` (`kurulum.rs:228`); SHA doğrulaması yok.
