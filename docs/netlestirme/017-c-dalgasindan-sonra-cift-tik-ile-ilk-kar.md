# Netleştirme: C dalgasindan sonra cift tik ile ilk kare arasi 4064 ms; bunun 2520 ms'i pencere

- tarih: 2026-09-16
- girdi: 017-c-dalgasindan-sonra-cift-tik-ile-ilk-kar-girdi.md
- maliyet: 60.453 belirteç, 23 sn

## Dönen

Belirsizlikler:

1. "Hedef 100 ms" hangi ölçüye ait: dış saat kabuk→ilk-kare toplamı mı (4064 → 100, yani 40 kat), yoksa yalnız perde-sonrası "görünür bir şey" mi; 100 ms fiziksel olarak süreç doğumu + CLR + Avalonia için ulaşılamazsa "maksimum yakınlık" için kabul edilebilir eşik ne (ör. 500 ms, 1000 ms)?

2. Sekmelerin tembelleştirilmesinde açılış sekmesi hangisi sayılacak: kabuktan dosya gelince oynatıcı, boş açılışta küçültme — iki senaryo ayrı mı ölçülecek, yoksa yalnız çift-tık-dosya senaryosu mu hedef?

3. `hwdec`/`vd-lavc-fast`/`demuxer` değişiklikleri karşılaştırma panelinin bayt-bayt eşleme testini bozarsa kabul edilir mi: bu anahtarlar yalnız oynatıcı sekmesinin ilk açılışına mı uygulanacak (karşılaştırma motoru varsayılanda kalır), yoksa tüm motor örneklerine mi?

4. "İznimi isteme" dalga içi işler için mi geçerli, yoksa kullanıcıya görünen davranış değişiklikleri (ör. hwdec varsayılanını `AutoCopy` yapmak, ilk karede düşük kaliteli çözme) de sormadan yapılabilir mi?

5. Bu makine oturumlar arası ~3,6 kat dalgalı koşuyor; kabul ölçüsü eşleşik fark ortancası mı (kaç tekrar, hangi klip), yoksa mutlak ms mi?
