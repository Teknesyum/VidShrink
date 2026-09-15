# Netleştirme: A dalgasi olculdu: cift tik -> ilk kare 1796,5 ms'ten 1747,7 ms'e indi, yalnizca

- tarih: 2026-09-15
- girdi: 016-a-dalgasi-olculdu-cift-tik-ilk-kare-1796-girdi.md
- maliyet: 57.371 belirteç, 31 sn

## Dönen

**Belirsiz kalanlar**

1. **Hedefin bittiği işaret:** 100 ms, gerçek `ilk-kare`ye mi, yoksa iskelet arayüzün ekranda göründüğü ana mı sayılacak — iskelet sayılıyorsa gerçek karenin gelmesi için ayrı bir üst sınır var mı (ör. 1 sn)?

2. **İskeletin sahibi:** İskeleti başlatıcı (`VidShrink.exe`, 58,8 ms) kendi Win32/yalın penceresiyle mi çizsin, yoksa yalnız Avalonia içinde mi kalınsın — başlatıcıya çizim işi ya da üçüncü bir süreç eklemek serbest mi?

3. **Başlatıcının işlevi:** Kabuk komutu değişmiyor, ama başlatıcı güncelleme kontrolünü arka plana atıp `App`i hemen doğurabilir mi, yoksa "önce güncelle, sonra aç" sırası korunacak mı?

4. **Teknik sınırlar:** ReadyToRun/AOT (yayın boyutu ve CI değişir), yedi sekmeyi ayrı `UserControl`lere bölme (4601 satırlık dosyada büyük yeniden düzenleme), `hwdec` açma (uyumluluk riski), arka planda yerleşik bekleyen süreç — bunlardan hangileri baştan yasak, hangileri ölçüp karar verilecek?

5. **Başarı ölçüsü:** Aynı protokol mü (eşleşik sıcak, 14 tekrar, ortanca), yoksa p90 ve soğuk açılış da hedefe dahil mi — kabuğun `Start-Process` öncesi payı (Explorer → CreateProcess) dış saate girecek mi?
