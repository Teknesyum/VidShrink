# Netleştirme: Rakip oynatıcıların (GOM öncelikli) özellikleri VidShrink oynatıcısına nasıl sın

- tarih: 2026-09-11
- girdi: 003-rakip-oynaticilarin-gom-oncelikli-ozelli-girdi.md
- maliyet: 55500 token, 18 sn

## Dönen

1. "Standart / Gelişmiş / Alınmayacak" üçlüsünde Standart'ın kabul sınırı ne — sıkıştırma aracının oynatıcısında "işe yarayan" ölçütü tek dosyayı doğrulamak mı (kısaltılmış kullanım), yoksa günlük medya oynatıcı olmak mı? Bu, hız/altyazı/çalma listesi gibi orta bantı iki yöne çekiyor.

2. Üç oynatıcı yolunun birleştirilmesi (ortak çekirdek) bu işin kapsamında mı, yoksa yalnız PlayerView sekmesi hedefleniyor ve karşılaştırma paneli/önizleme sesi dokunulmadan kalıyor mu?

3. Sesi ayrı süreç + NAudio'dan çıkarıp tek ffmpeg süreci ya da ortak saat üzerinden A/V senkrona geçmek kabul edilebilir bir yeniden yazım mı, yoksa mevcut iki-boru mimarisi korunarak (yalnız `-re` kaldırılıp saat eklenerek) mi ilerlenmeli?

4. Dalga kabul ölçütleri mevcut test düzenine (gerçek Avalonia + gerçek ffmpeg, kanıt dosyası) bağlı olacak mı; her dalga için `dotnet test` yeşil + kanıt dosyası yeterli mi, yoksa Bench ölçümü (arama gecikmesi, hız doğruluğu) de isteniyor mu?

5. 43 dil kısıtı özellik başına yeni metin ekleme maliyetini büyütüyor — kısayol tablosu ve Gelişmiş menü etiketleri için 43 dilde çeviri zorunlu mu, yoksa ilk dalgada Türkçe+İngilizce ile başlayıp diğerleri sonra doldurulabilir mi?
