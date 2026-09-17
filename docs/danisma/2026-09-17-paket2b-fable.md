# Paket 2b — Fable Danışması: Yükleme ve Arka Plan Ayırma

Tarih: 17 Eylül 2026. Model: fable (Agent aracı), 47.331 token, 26,7 sn, araç kullanmadı.

## Gönderilen (kelimesi kelimesine)

````
Aşağıdaki soruya yalnız metinle, Türkçe yanıt ver. Dosya okuma veya araç kullanma gerekmiyor; olgular metinde. Her soru için tek karar ve bir-iki cümle gerekçe.

# Paket 2b — Kaydedici T13: Yükleme ve Arka Plan Ayırma Kararı

VidShrink (.NET 8 + Avalonia + ffmpeg 9.0 gyan full build) masaüstü video aracı. Kaydedici sekmesi var. Kullanıcı
OBS/Bandicam/Cap düzeyi istiyor. Kalan T13 kalemlerinden ikisi dış bağımlılık veya dış servis gerektiriyor; iki karar
istiyorum. Ölçüt sırası: (1) kullanıcı kolaylığı, (2) hesap/şifre istemeyen yol, (3) kullanıcı verisi rıza olmadan
dışarı gitmez, (4) paket boyutu ve lisans.

## Olgular (depodan okundu)

- Paylaşım altyapısı zaten var: `Core/Share` (`IShareProvider`, `MultipartUploadProvider`, `PresignedUploadProvider`),
  hedef tablosu `paylasim-hedefleri.json`: varsayılan `storage.to` (hesapsız, 25 GiB tavan, 1-7 gün saklama, varsayılan
  3 gün, silinebilir, tarayıcıda oynar), yedek `uguu.se` (hesapsız, 128 MiB, 3 saat, silinemez). Commit b235fa6e Drive
  yolunu bu iki anonim sağlayıcıyla değiştirdi.
- Kaydedicinin sonuç panelinde bugün "Paylaş" düğmesi var: kullanıcı basınca varsayılan hedefe yükler, ilerleme çubuğu,
  iptal, bağlantı satırı ve "kopyala" düğmesi çıkar. Küçültme sekmesi de aynı yolu kullanıyor.
- Plandaki C8 maddesi (Cap'in Instant Mode'u): "kaydederken yükle, durunca bağlantı".
- Webcam bindirmesi var: Windows'ta dshow girdisi, `overlay` ile köşeye, genişlik 160/240/320/480.
- ffmpeg 9.0 derlemesinde `backgroundkey` ("static background into transparency", ilk kareyi arka plan sayar,
  threshold/similarity/blend), `chromakey`, `colorkey` var. `dnn_processing` / `dnn_detect` filtreleri bu derlemede YOK.
  Yani model tabanlı segmentasyon ffmpeg içinde yapılamaz; ayrı bir çıkarım çalışma zamanı gerekir.
- Model yolu seçenekleri (kendi bilgim, ölçülmedi): MediaPipe Selfie Segmentation (Apache-2.0, ~250 KB tflite) veya
  ONNX'e çevrilmiş hali; çalıştırmak için ONNX Runtime (MIT, win-x64 native ~10-12 MB) ya da yerel C# ile her kare
  ffmpeg'den ham kare alıp maske üretip geri boru. Bu, kayıt sırasında kare başına CPU çıkarımı ve iki boru demek.
  Kullanıcının makinesi tam yükte iki kez kapandı; yerelde yük testi yasak, ağır ölçüm yalnız CI'da.
- RVM (Robust Video Matting, GPL-3.0) ve MODNet (Apache-2.0, ~25 MB) daha ağır.

## Soru 1 — Yükleme

a) Kayıt bitince otomatik yükleme (Cap Instant) olsun mu, yoksa bugünkü "Paylaş" düğmesi yeter mi? Olacaksa rıza nasıl
   alınır: kapalı varsayılanlı ayar + ilk açılışta tek seferlik onay metni mi, her kayıtta onay mı?
b) "Kaydederken yükle" (akışla, parça parça) mı, "durunca yükle" mi? storage.to presigned tek dosya yüklemesi.
c) Servis: storage.to varsayılan kalsın mı? Kişisel ekran kaydında 3 gün saklama ve silme bağlantısı yeterli mi,
   varsayılan saklama kısaltılmalı mı?

## Soru 2 — Arka plan ayırma

a) Model mi (ONNX Runtime + küçük segmentasyon modeli, ~12 MB ek, kayıt sırasında CPU yükü), yoksa ffmpeg'in
   modelsiz `backgroundkey` (sabit arka plan) + `chromakey` (yeşil perde) kolları mı?
b) Model seçilirse: pakete mi girer, yoksa libmpv gibi isteğe bağlı sha256'lı indirme mi?
c) Modelsiz seçilirse arayüzde bu sınırlama kullanıcıya nasıl söylenir (tek cümle öner)?

Her soruya tek karar ve bir-iki cümle gerekçe ver; seçenek listesi değil karar istiyorum.
````

## Dönen (kelimesi kelimesine)

````
## Soru 1 — Yükleme

**a) Otomatik yükleme yok; bugünkü "Paylaş" düğmesi kalır.** Ekran kaydı çoğu zaman özel içerik taşır; ölçüt 3 (rıza olmadan veri dışarı gitmez) tek tıklık açık bir eylemle en temiz karşılanır, kapalı varsayılanlı ayar bile "bir kere açtım, unuttum" riskini taşır. Cap'in Instant Mode'u kendi hesaplı bulutuna dayanıyor; anonim bir üçüncü tarafa aynı otomatikliği vermek ürünün kendi ölçütüyle çelişir.

**b) Durunca yükle.** storage.to presigned tek dosya kabul ediyor, parçalı akış için altyapı yok; kayıt sırasında ikinci bir ağ borusu açmak ayrıca kayıt karelerine rakip olur. Otomatik yükleme yokken "kaydederken yükle"nin kazandıracağı saniyeler zaten anlamsız.

**c) storage.to varsayılan kalır, varsayılan saklama 3 günden 1 güne iner.** Kişisel ekran kaydı "şimdi gönder, karşı taraf bugün baksın" kullanımıdır; en kısa izinli süre + silme bağlantısı, ölçüt 3'ü hiç ayar dokunmadan sağlar. Uzun süre isteyen kullanıcı paylaşım panelinden 7 güne çıkarır.

## Soru 2 — Arka plan ayırma

**a) Modelsiz: `backgroundkey` + `chromakey`.** Kayıt sırasında kare başına CPU çıkarımı ve iki ham boru, iki kez kapanmış bir makinede yerelde ölçülemeyecek bir yük demek; ölçülemeyen özellik teslim edilemez. ~12 MB native + model ve isteğe bağlı indirme karmaşası da ölçüt 4'e karşı, oysa iki ffmpeg filtresi paket boyutuna sıfır ekliyor ve webcam bindirme yolu (`overlay`) olduğu gibi kullanılıyor.

**b) Düşer** (model seçilmedi). İleride açılırsa yol libmpv'ninki olur: sha256'lı isteğe bağlı indirme, pakete girmez.

**c) Tek cümle:** "Arka plan kaldırma sabit arka plan veya yeşil perde ile çalışır; kayda başlamadan kadrajdan çekilin ki ilk kare boş arka planı görsün."

Yok
````
