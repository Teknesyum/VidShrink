# 021 — `izle` Harf Çarpışması: Danışma Girdisi

Fable'a verilen metin, birebir:

---

VidShrink'in `izle` komutu bir klasörü izliyor, kararlı hale gelen videoyu küçültüyor.
`--bir-kez` kipinde bekleyen dosya kalmayınca çıkıyor.

Kusur (Linux gibi harf duyarlı bir dosya sisteminde): aynı klasörde `Klip.mp4` ve
`klip.mp4` varsa, dosya adının kıyaslandığı her yer harfi yok sayıyor. İkisi bekleyenler
tablosunda tek satıra düşüyor, her taramada öbürünün boyutu görülüp kararlılık sayacı
sıfırlanıyor. Sonuç: ikisi de hiç küçültülmüyor ve tablo hiç boşalmadığı için
`--bir-kez` hiç çıkmıyor — kullanıcı sonsuza kadar bekliyor. Bugün bu davranış yalnız
iki README'de yazılı; kodda kapatılmadı.

Üç yol görünüyor:

1. **Ayrı tut.** Bekleyenler tablosunu ve işlenmiş kaydını Linux'ta harf duyarlı yap.
   İkisi de küçültülür, ama ikisinin çıktısı aynı ada indiği için bu sefer çıktı
   çakışması doğar; çıktı adını da ayırmak gerekir.
2. **Atla ve söyle.** Bir taramada aynı ada inen ikinci dosyayı bekleyenlerden düşür,
   "harf varyantı çakıştı, atlandı" diye günlüğe yaz. `--bir-kez` çıkar, kullanıcı
   dosyayı yeniden adlandırınca ikincisi de küçülür.
3. **Hata ver.** Çakışma görülünce koşuyu hemen hatayla bitir; kullanıcı klasörü
   düzeltsin.

Sorular:

1. Hangisi? Gerekçesiyle.
2. Seçilen yolda kullanıcı ne görüyor — hangi satır, hangi anda, hangi çıkış kodu?
3. Windows ve macOS'ta (dosya sistemi zaten harf duyarsız) davranış değişmeli mi?

Kısıt: kullanıcı kolaylığı ön planda. Uydurma renk/ölçü yok. Yanıt Türkçe, kısa.
