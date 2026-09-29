# Danışma 016 — Fable: Tasarım Güncellemesinin Değerlendirmesi

Girdi: `016-fable-tasarim-guncellemesi-girdi.md`. Cevap aynen:

---

Bulgular yerinde: "madde madde listelenir." metni `main.plan.empty`, yani Küçült sekmesinin "Yapılacak İşlem" boş metni; Düzenleyici karesinde videonun altından sızıyor. `Watermark` hiçbir .axaml'de yok; Dönüştür'deki gri "1280x720" gerçek `Text` değeri.

Sağlama: 18 karenin tamamı açıldı, CHANGELOG'un iki bölümü, kabuk standardı, tema belgesi ve 015 okundu; kare tarihi ile dört commit'in sırası `git log`la, kare boyutu PNG başlığından, yer tutucu ve sızan metin `rg` ile kodda doğrulandı. Hiçbir dosya yazılmadı.

## Danışma 016 — Cevap

### Önce bir uyarı: kanıt takımı değişikliğin öncesinden

`docs/gorseller/T200-*.png` **06:57**'de commit'lenmiş (`c8213cd9`). Değerlendirilmesi istenen dört değişiklik daha sonra gelmiş: tampon Gelişmiş'e (10:14), sekme renkleri ters ve duraklatma mavisi (13:11), duraklatma çubuk açmıyor (13:20), 1024 px Gelişmiş (13:26). Karelerde hâlâ eski hâl var: seçili sekme beyaz, diğerleri mavi; "Kayıt Tamponu 30 sn" ilk kartta. Kareler ayrıca **1600×1000**, 1920×1080 değil. Bu yüzden Unreleased'daki oynatıcı/sekme/kaydedici-gelişmiş kalemleri hakkında kareden hüküm veremiyorum; aşağıda yalnız gördüğümü yazdım. Öneri 1 bu yüzden.

### Öneriler (önem sırasıyla)

1. **Kanıt takımını yenile — tüm ekranlar.** Kare 06:57, dört değişiklik öğleden sonra; boyut 1600×1000. "1080'de kaydırmasız" iddiası bu takımla ölçülemez. Öner: T201 takımını 1920×1080 %100'de, Unreleased birleştikten sonra çek; oynatıcıya "duraklatılmış + fare ortada" ve "1/3 pencere" kareleri ekle. Dosya: gorsel ölçüm betiği (`tools/`, doğrulanmalı hangi betik). İş: küçük.

2. **Düzenleyici — Küçült sekmesinin içeriği videonun altından sızıyor.** Karede video alanının altında kesik "…madde listelenir." satırı ve iki kart kenarı görünüyor; metin `Locales/tr/main.json:209 main.plan.empty`, yani Küçült'ün "Yapılacak İşlem" paneli. Sekme değişince eski sekmenin içeriği ya gizlenmiyor ya da düzenleyici video yüzeyi diğer sekmelerin üstüne çiziliyor. Öner: sekme geçişinde pasif içerik `IsVisible=false`; video yüzeyi kendi sekme kapsayıcısında kalsın. Dosya: `MainWindow.axaml` sekme sunucusu, düzenleyici görünümü. İş: orta. (Nedeni doğrulanmalı; belirti kesin.)

3. **Hakkında kartı — üstte ~600 px boşluk.** Her iki dilde kart, içerik kartın en altına yığılmış, üstü boş; TR'de orta sütunda, EN'de sol sütunda. Sütun akışı dile göre kartı başka sütuna atıyor ve kart sütun boyuna geriliyor. Öner: `SutunIzgara`'da kart `VerticalAlignment=Top`, gerilme yok; Hakkında'yı her dilde aynı sütuna sabitle. Dosya: `SutunIzgara.cs`, `MainWindow.axaml`. İş: küçük. (Boşluğun gerilmeden mi yoksa çekim anında henüz çizilmemiş bir açılır bölümden mi geldiği doğrulanmalı.)

4. **Ayarlar — 1600×1000'de kaydırıyor.** Sağda kaydırma çubuğu, "Paylaşım Hedefi" ve "Hakkında" kesik. 1080'de sığıyorsa 1000'de 80 px için taşıyor demektir; sınır çok dar. Öner: sol sütundaki "Güncelleme" ve "Paylaşım Hedefi" açıklama metinlerini tooltip'e alın (Kaydedici'de yapılan aynı hamle), "Tanı Günlüğü" tek satırlık kart olsun. Dosya: `MainWindow.axaml` ayarlar bölümü. İş: küçük.

5. **Dönüştür — sağ sütun %60 boş, sol form tek uzun kart.** Form 12 alan iki kolonda 700 px; sağda FFmpeg + İlerleme 350 px, altı zemin. Öner: formu üç kolona ya da "Temel / Kırpma / Ses" üç karta bölüp `SutunIzgara`'ya ver; Küçült'teki akış deseninin aynısı. Dosya: `MainWindow.axaml` dönüştür bölümü, `KucultSutunlari.cs` benzeri bir sütun kuralı. İş: orta.

6. **Dönüştür — "1280x720" ve "25" yer tutucu gibi gri.** Kabuk standardı yer tutucuyu yasaklıyor; kodda `Watermark` yok, bunlar gerçek `Text` değerleri (`MainWindow.axaml:1205`) ama soluk çiziliyor, kullanıcı "boş" okur. `MainWindow.axaml.cs:4738`'deki yorum da bu değerlerin çıktıya katkısı olmadığını söylüyor. Öner: alan kapalıyken (`Çözünürlük=Kaynak`) kutuyu gizle ya da devre dışı bırak, açılınca boş ve tam renkli gelsin. Dosya: `MainWindow.axaml`, ilgili `.cs`. İş: küçük.

7. **Kaydedici — Kamera kartı ipuçları hâlâ kartta.** Karede iki paragraf açıklama Kamera kartını 400 px'e çıkarıyor. Unreleased "ipuçları tooltip'e" diyor; bu kart o kapsamda mı doğrulanmalı. Öner: aynı kurala bağla; kart "Kamera Yok" iken alt üç alan devre dışı olduğu gibi kalsın. Dosya: `Recorder/RecorderView.axaml`. İş: küçük.

8. **Küçült — hızlı hedef düğmeleri iki satıra kırılıyor, dilde farklı yerden.** TR: 100 ilk satırda; EN: 100 ikinci satırda. Öner: sekiz çip + "+" için `WrapPanel` yerine sabit iki satırlı ızgara (5+4), her dilde aynı görünüm. Dosya: Küçült Hedef kartı, ölçü `Theme.axaml` boşluk belirteci. İş: küçük.

9. **Oynatıcı — video pencerenin tepesinden değil başlığın altından başlıyor.** Karede üst 30 px'lik koyu bant ve video onun altında; 015'in "başlık overlay olur, video tepeden başlar" önerisi uygulanmamış görünüyor. Öner: 015 S2. Dosya: oynatıcı sekmesi `TopOverlay`. İş: orta. (Kare eski; sonraki commit'lerde değiştiyse düşer — doğrulanmalı.)

10. **Karşılaştırma paneli — büyük harf etiketler ve rozetler.** "ORİJİNAL / İŞLENMİŞ / CRF 30 / %100" tamamı büyük harf; uygulamanın geri kalanı Title Case. Öner: "Orijinal / İşlenmiş", rozetler aynı yazı ağırlığı. Dosya: karşılaştırma paneli görünümü. İş: küçük.

### İyi olan, dokunma

- **Küçült üç sütun düzeni:** Kaynak → Hedef/Kalite → Karşılaştırma → Çıktı/Ne Çıkacak okuma sırası doğru; "Yapılacak İşlem" tablosu ve "Neden Böyle · 6" katlanır satırı olduğu gibi kalsın.
- **Kaydedici'nin dört sıkı kartı ve üst şerit:** Boşta/Geçen Süre/Kare/Düşen Kare tek satır, Başlat mavi tek birincil düğme; Kamera Yok iken alt alanların devre dışı grileşmesi doğru davranış.
- **Oynatıcı alt şeridi:** 015'in 52 px hedefine inmiş, tek satır, scrim üstünde; zaman/ses solda, ileri-geri ortada, hız/kes/tam ekran sağda — gruplama doğru.
