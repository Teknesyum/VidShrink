# K8 Borcu 5 — Paylaşım Hatasının Kullanıcıya Ulaşmayan Yarısı

19 Eylül 2026. Ölçüm: `tests/VidShrink.Tests/PaylasimTekrarDenemeTests.cs`,
mutasyon düzeneği `.calisma/paylasim-tekrar/mutasyon.py`.

## Borcun öncülü yanlıştı

Borç "sınıflandırma fazla ayrıntılı, üyeler ölü" diye yazılmıştı. Ölçülünce tersi çıktı:
kullanıcıya görünen ayrım enum'dan **daha ince** yapılıyor. 11 `ShareFailure` üyesine karşılık
22 yerelleştirme anahtarı var ve hepsi 42 dilde pimli; iki hata türü aynı anahtarı paylaşmıyor
(`IkiHataTuruAyniAnahtariPaylasmiyor`).

Gerçekten ölü olan şey başkaydı: `ShareDiagnosis.RetryAfter` ve `SuggestedTargetId` Core'da
hesaplanıyor, üç gösterim yüzeyinin üçü de atıyordu. Sunucu "42 saniye sonra dene" diyor,
kullanıcı bunu hiç görmüyordu.

## Karar kuralı ve idempotanlık

Ölçü hatanın adı değil, **baytların işlenip işlenmediği**. `ShareRetry.IsRetryable` tek gövde:

| Hata | Kural |
|---|---|
| `RateLimited` | her adımda yeniden denenebilir (istek baştan reddedildi) |
| `NetworkFailure`, `ServiceError` | yalnız `Prepare` ve `Init` — baytlar akmadan önce |
| kalan 8 üye | hiçbir adımda denenmez |

Yükleme ortasında gelen ağ hatasında dosyanın karşıya geçip geçmediği bilinmez; düğmeyi orada
açmak kullanıcıya ikinci bir kopya yükletir. Bu yüzden `ShareStep` tanıya taşındı
(`ShareDiagnosis.Step`, `ShareResult.Step`).

Negatif kontrol: kalan 9 üye × 6 adım = 54 bileşimin hiçbiri yeniden denenmiyor
(`KalanUyelerHicbirAdimdaYenidenDenenmiyor`).

## Mutasyon dökümü

Taban 0/99, geri 0/99. Beş kesim, beşi kırmızı.

| Kesim | Kırmızı |
|---|---|
| M1 adım şartı kalktı (her adımda yeniden dene) | 4 |
| M2 küme genişledi (`FileTooLarge` da denenebilir) | 2 |
| M3 önerilen hedef kolu düştü | 1 |
| M4 geri sayım yok sayıldı | 1 |
| M5 adım tanıya taşınmıyor | 1 |

## Ölü üye tarayıcısının kör noktası

`ShareRetryPrompt` ilk yazımında alanını `TargetId` diye adlandırdı. Tarayıcı üye adını
**türden bağımsız** eşleştirdiği için `prompt.TargetId` okumasını `ShareLink.TargetId`'ye
yazdı ve o pim sessizce "canlandı" — gerçek bir bağlanma yokken küme eşitliği kırmızıya döndü.

Pim silinmedi; belirsizlik silindi. Alan `RetryTargetId` oldu, `OluUye` süiti 17/17 yeşile
döndü. Aynı adı iki türde kullanan her yeni üye bu tuzağa düşecektir.
