# Netleştirme: Karar ver: pilot 2 libmpv SW render olcumu sonrasi motor libmpv mi kalsin LibVLC

- tarih: 2026-09-11
- girdi: 011-karar-ver-pilot-2-libmpv-sw-render-olcum-girdi.md
- maliyet: 59023 token, 55 sn

## Dönen

Olgular "karar turu" diyor; netleştirme sorusu değil karar döndürüyorum.

## Karar: motor libmpv kalır. Ek ölçüm karar için gerekmez.

**Gerekçe**

1. Boyut kolu gerçekte berabere. Eşik "<106 MB" LibVLC'nin doğrulanmamış win-x64 tahminine göre kurulmuştu; libmpv 120 MB ile ondan 14 MB büyük — 214 MB'lık publish'in %7'si. Doğrulanmış tek LibVLC sayısı ise 293 MB (3 mimari). Boyut hiçbir yönde karar verdirmiyor; 007'deki "~34 MB" girdisi yanlıştı, puanı düşer ama sıra değişmez.

2. 4K aramanın kalması motorun değil kod çözücünün sınırı. `exact` arama önceki anahtar kareden hedefe kadar çözer; GOP 2 sn, 4K HEVC azami çözme ~280 kare/sn → en kötü ~215 ms, medyan ~110 ms. Ölçülen 172/281 ms bu fiziğe oturuyor. LibVLC aynı dosyada aynı anahtar kareden aynı kareleri çözecek; farklı motor bu sayıyı 60 ms'ye indiremez. Bu yüzden (a) LibVLC'yi aynı dört dosyada ölçmek kararı çevirmez, bir tur harcanmaz.

3. Karar verdiren kolonlar libmpv lehine ve temiz: SW render her dosyada eşiğin 10-25 katı, timed oynatmada 0 düşme, işlemci %3-8, mpv hata 0, 1080p H.264 arama 36 ms. LibVLC'nin T167 sayıları (39-57 ms) farklı dosya ve yöntemle geldi; aynı sınıfta, üstün değil.

4. Kirli 2.-3. tekrar geçersiz sayılır, temiz 1. tekrar yeterli kanıt; (b) temiz makinede tekrar ayrı tur açmaz. HEVC 1080p'nin sınırdaki 61,7 ms'si 0. dalganın kendi ölçüm adımında aynı düzenekle yeniden alınır, ayrı tur değil.

5. (c) kırpılmış libmpv derlemesi v1 kapısı değil; boyut karar kolonu olmaktan çıktığı için sonraya kalır.

**4K arama >60 ms v1 için kabul.** Eşik kod çözücüye göre ayrılır: 1080p medyan ≤60 ms kalır; 4K için `exact` arama medyanı ≤ GOP'un tam çözüm süresi (bu düzenekte ≤200 ms) yeterli. GOM paritesi sürükleme sırasında anahtar kare aramasıyla (`seek absolute+keyframes`), bırakınca `exact` ile sağlanır; bu 0. dalganın tasarım notu, ölçüm değil. hwdec auto-copy arama süresini iyileştirmediği için varsayılan `hwdec=no`, 4K'da bellek isteniyorsa auto-copy seçenek.

**Koşul (010'dan devam):** osx-arm64 bundle'a libmpv 1 turda gömülemezse tek motor kuralı işler, her platform LibVLC. Bu karar o kapıyı kaldırmaz.

Yok

result: Motor libmpv kalır; boyut kolu berabere, 4K arama kaybı kod çözücü sınırı olduğundan LibVLC ölçümü kararı çevirmez, ek tur açılmaz; 4K eşiği GOP çözüm süresine göre ayrılır, osx-arm64 gömme koşulu 010'dan sürer.
