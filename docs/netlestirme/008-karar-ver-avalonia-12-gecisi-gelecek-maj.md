# Netleştirme: Karar ver: Avalonia 12 gecisi, gelecek major yuku, acgozlu hedefe (en hizli + tu

- tarih: 2026-09-11
- girdi: 008-karar-ver-avalonia-12-gecisi-gelecek-maj-girdi.md
- maliyet: 58108 token, 21 sn

## Dönen

Soruyu keskinleştirme turu; görüş değil yalnız belirsiz noktalar.

1. "En hızlı yöntem" neyin hızı: **ilk yayımlanabilir v1'e ulaşma süresi mi**, yoksa yıllar boyunca toplam bakım yükü mü? İkisi Avalonia 12 kararında zıt yöne çekebilir.
2. "Tüm platformlar" kümesi: **win-x64, osx-arm64, osx-x64, linux-x64 (mevcut 4 RID) mi**, yoksa iOS/Android/WASM da dahil mi? Avalonia 12'nin mobil için .NET 10 zorunluluğu yalnız ikinci durumda kıyasa girer.
3. "Tüm özellikler" derken Standart 30 + Gelişmiş 13'ün **hepsi v1'de mi şart**, yoksa açgözlülük hedefin tamamı için olup v1'de ertelenmesine izin var mı? T0 yorumu ertelemeye kapı açıyor, kullanıcı cümlesi açmıyor.
4. Avalonia 12 sondası (dalda 12.1.2'ye çek, derle, tam süit) **bu karardan önce koşulsun mu**, yoksa karar sonda sonucu olmadan mı verilsin? Şu an "NuGet indirme izni bekliyor"; karar bu ölçüm olmadan tahmine dayanır.
5. "Geçmişteki incelemelerimiz ne kadar tuttu" hesabında **kütüphane/motor tahminleri mi** (007'nin libmpv ~14 tur vb., hiç sınanmadı) yoksa genel iş tahminleri mi (tek örnek T193) esas alınacak; ikincisiyse tek örnekten kalibrasyon kabul mü, pilot zorunlu mu?

result: Netleştirme 008 için beş belirsizlik döndürüldü: hız ölçütü (v1 mi bakım mı), platform kümesi (4 RID mi mobil dahil mi), v1'de tüm özellik şartı, Avalonia 12 sondasının karardan önce koşulup koşulmayacağı, geçmiş isabet hesabının veri tabanı (motor tahminleri mi tek T193 örneği mi).
