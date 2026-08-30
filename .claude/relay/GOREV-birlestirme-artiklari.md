# Görev paketi — birleştirmeden kalan iki artık

Sole için yazıldı. Depo dalı: `main` (kendi dalını aç: `sole/birlestirme-artiklari`).

## Nereden geliyor

T83 (metinlerin anahtara taşınması) ve T84 (ayar kalıcılığı) aynı dosyalara paralel
yazdı, birleştirme elle çözüldü. Mac'te uygulamayı ilk kez çalıştıran ajan iki artık
bildirdi. Süit yeşil — yani ikisini de **hiçbir ölçü tutmuyor**; asıl iş onları ölçüye
bağlamak.

## Artık 1 — ekranda eksik metin

Bildirilen: **sekme yazısı ve plan satırı** yerelleştirme birleştirmesinden ötürü eksik.
Anahtar eşitliği ölçüsü geçiyor (iki dilde aynı anahtar kümesi), yani eksik olan anahtar
değil **bağ**: bir denetim anahtara hiç bağlanmamış ya da boş anahtara bağlanmış.

Kabul kriteri:

1. Eksik olan her metni bul ve bağla. Kaçının eksik olduğunu say, raporda tek tek yaz.
2. Bunu yakalayan bir ölçü ekle: pencere kurulduktan sonra, kullanıcıya görünen her
   metin düğümü **boş olmayan** bir değer taşıyor. Boş olması meşru olan düğüm varsa
   (henüz video yüklenmemişken dolan alanlar gibi) ölçüde **adıyla** muaf tutulsun,
   genel bir desen yazma.
3. Ölçü mutasyonla sınansın: bağladığın bağlardan birini kaldır, ölçünün kırmızıya
   döndüğünü göster, geri al.

## Artık 2 — pencerede beklenmeyen saydamlık

Bildirilen: `main`de pencerenin **%10,4'ü** saydam piksel taşıyor, yayınlanmış `v0.2.4`te
%0'dı. Ölçüm macOS'ta yapıldı.

Önce **doğrula, sonra düzelt**. Bilinenler:

- `MainWindow.axaml`de `Background="Transparent"` ve `TransparencyLevelHint="Transparent"`
  satırları v0.2.4'ten beri **değişmedi** — karşılaştırdım, birebir aynı.
- `PhoenixOpacity` 0,30 → **0,08** düştü (anka arka planı ortama gömüldü).
- T82 karşılaştırma panelinin bant hâlini `PanelSurface` / `PanelSurfaceOpacity` (0,90)
  belirtecine bağladı; bu **kullanıcının istediği** davranış, panel öteki panellerle eşit
  saydamlıkta olacaktı.

Yani %10 saydamlığın bir kısmı istenen şey olabilir. Kabul kriteri:

1. Saydam piksellerin **nereden** geldiğini katman katman söyle: pencere mi, çalışma
   alanı zemini mi, paneller mi, anka mı. Ölçüm Windows'ta yapılsın — kullanıcının
   makinesi Windows.
2. İstenen saydamlık ile kaza sonucu olanı ayır. İstenen: panellerin öteki panellerle
   eşit, hafif saydam olması. İstenmeyen: pencerenin arkasındaki masaüstünün görünmesi.
3. İstenmeyen varsa düzelt; yoksa "hepsi istenen" de ve **düzeltme**. Hangi sonuca
   vardığını sayıyla göster.
4. Vardığın sonucu bir ölçüye bağla ki bir daha kaymasın.

## Sınırlar

- `dotnet test -c Release` tamamı yeşil — `PerformanceCheckTests` dahil, `--no-build` yok.
  Bugünkü taban: 939 ölçü, 922 geçiyor, 17 atlanıyor, 0 başarısız.
- Yeni renk ya da ölçü uydurma; `Theme.axaml` belirteçlerinden çık.
- Yorum yazma; mevcut yorumları koru.
- Kendi dalında çalış, bitince **it**. `main`e sen birleştirme.
