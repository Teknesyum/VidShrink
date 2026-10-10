# Oynatıcıda Adresten Oynatma

Durum: uygulandı (2026-10-10). Ölçü: `tests/VidShrink.Tests/OynaticiAdresTests.cs`.

## Zaten Vardı

- "Adres aç…" (Ctrl+U), Araçlar menüsü satırı ve adres kutusu.
- Kabul edilen şemalar: http, https, rtsp, rtmp, srt, udp (`MpvEngine.RemoteSchemes`).
- Motor adresi olduğu gibi `loadfile`'a verir; yerel sunucudan uçtan uca ölçü
  (`OynaticiAracTests.HttpAdresiAcilirYerelYolAdresSayilmaz`).
- m3u/pls/asx listelerindeki adres girdileri ve kuyruktan oynatma.
- Adres son açılanlara ve Küçült sekmesine girmez.
- Açılmayan adres oynatıcının hata satırında (`TxtStall`) görünür.

## Eklenen

- Ctrl+V ve Araçlar menüsünde "Adresi yapıştır": panodaki ilk adres satırı açılır,
  adres yoksa "Panoda adres yok" denir.
- Tarayıcıdan sürüklenen adres metni oynatıcıya bırakılınca açılır.
- Komut satırı: var olan dosya yoksa ilk adres argümanı açılış hedefidir
  (`Program.StartupTarget`). ffprobe adresi yoklamaz.
- Motorda `ytdl=no`: PATH'teki yt-dlp hiçbir koşulda çağrılmaz. Site bağlantısı çözülmez.
- Ortak kural tek yerde: `VidShrink.Core/MediaAddress`. Player Core'a bağlı olmadığı için
  şema kümesinin motorla eşitliği testle pimli.

## Karar: Sorgu Diske Yazılmaz

Adresin `?…` sorgusu, `#…` parçası ve `ad:parola@` bölümü çoğu zaman erişim anahtarıdır.
Motora giden adres olduğu gibidir; kayda ve ekrana giden biçim `MediaAddress.WithoutQuery`'dir.

| Yer | Yazılan |
| --- | --- |
| `player-tools.json` (`LastUrl`, kutunun ön dolgusu) | sorgusuz |
| `history.json` anahtarı (konum, yer imi) | sorgusuz; aynı adres tek kayıt |
| Başlık adı, ipucu, liste satırı | ad yolun son parçası, ipucu sorgusuz |
| Ekran görüntüsü dosya adı | yolun son parçası |
| "Yolu kopyala" | adres olduğu gibi (kullanıcının açık isteği) |
| Kullanıcının kaydettiği liste (m3u8) | adres olduğu gibi, yoksa liste yeniden açılmaz |

Bedeli: anahtarı sorguda taşıyan adres, kutunun ön dolgusundan yeniden açılamaz;
kullanıcı adresi yeniden yapıştırır.

## Yola Bağlı Özellikler

| Özellik | Adreste |
| --- | --- |
| Klip kaydet, GIF kaydet | satır pasif, ipucunda neden; kısayolda bildirim |
| Düzenle (menü, şerit düğmesi, E) | pasif, ipucunda neden; kısayolda bildirim |
| Dosya konumunu aç | pasif, ipucunda neden |
| Altyazı indir | bildirim, sağlayıcıya gidilmez |
| Yan altyazı arama | yapılmaz |
| Arama çubuğu küçük resmi | yalnız saat; ikinci bağlantı açılmaz |
| Klasörde sonraki/önceki dosya | liste boş, "başka dosya yok" bildirimi |
| Ekran görüntüsü, kareyi kopyala | çalışır (motordan) |

## Bırakılan

- Site bağlantısı çözümleme (yt-dlp): kapsam dışı.
- Adresin son açılanlara girmesi: mevcut karar korundu.
- Adreste kaldığı yerden sürdürme: kayıt dosya izine (boy, tarih) bağlı, adreste iz yok.
- Ölçülmedi: https, HLS, zaman aşımı ve yavaş sunucu. Yerel ölçü yalnız http 200/206 ve 404.
