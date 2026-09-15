# Netleştirme: Bir videoya cift tiklandigi andan ilk karenin ekranda gorunmesine kadar gecen su

- tarih: 2026-09-15
- girdi: 014-bir-videoya-cift-tiklandigi-andan-ilk-ka-girdi.md
- maliyet: 62443 token, 15 sn

## Dönen

Belirsiz kalan yerler:

1. Hedef eşik nedir: "milisaniyeler mertebesi" 100 ms altı mı, 300 ms altı mı, yoksa "bugünkünün yarısı" gibi bir oran mı?
2. Sıfır noktası hangisi olacak: ölçü şimdi app'in `Process.StartTime`'ından başlıyor, başlatıcının süresi dışarıda; tasarı çift tıktan (başlatıcı dahil, Explorer'ın `CreateProcess` anından) mı ölçülsün?
3. "İlk kare" tanımı: `Frame.Source = _bitmap` işareti mi (bugünkü), yoksa video karesi gelmeden önce gösterilecek bir ara görüntü (poster/küçük resim/boş oynatıcı penceresi) de sayılır mı?
4. Sınırlar: `hwdec`, `vo`, `pause=yes`, kaldığı yere arama gibi kullanıcıya görünür davranışlar ve iki süreçli yapı değiştirilebilir mi, yoksa yalnız sıralama/erteleme/paralelleştirme mi?
5. Doğrulama zemini: tasarının her adımının kazancı `tools/acilis-hizi/olcum.ps1` ile (20 adımlık iz, ortanca) mı ölçülecek, yoksa yeni bir test/ölçü düzeneği (`.sln`'e ekleme, CI pimi) de tasarının parçası mı?
