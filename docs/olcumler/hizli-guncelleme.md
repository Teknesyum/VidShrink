# Hızlı Güncelleme: Yerinde Yer Değiştirme

Kaynak desen: AmeliyatListe `sync.rs`. Koşan dosya `<ad>.old` olur, sahnedeki doğrulanmış dosya
adını alır, uygulama kendini yeniden açar, `.old` bir sonraki açılışta sessizce silinir. Kod
`src/VidShrink.Core/InPlaceUpdate.cs`, `src/VidShrink.App/YerindeGuncelleme.cs`.

Ölçen testler `tests/VidShrink.Tests/BaslaticiPanelsizTests.Yerinde.cs`. Düzenek `.calisma/yol-d/`
altında sahte kurulum: gerçek başlatıcı, `tools/VidShrink.SahteUygulama`, yerel sahte yayın
(`VIDSHRINK_UPDATE_SOURCE`, üç dosya v1 → v2). Ağa çıkılmaz. Süre "Yükle"den yeni sürecin
`acildi` olayına kadar.

## Önce ve Sonra

Aynı koşul: sahne hazır, yuva ve güncelleme kilidi boş, 2026-09-25, Release, bu makine.

| Yol | Koşum | n | Ortanca | Aralık |
|---|---|---|---|---|
| Yeni: yerinde takas + doğrudan açılış | 1 | 10 | 70 ms | 63-77 ms |
| Yeni: yerinde takas + doğrudan açılış | 2 | 10 | 70 ms | 63-89 ms |
| Eski: başlatıcı `--update-now` | 1 | 10 | 158 ms | 146-207 ms |
| Eski: başlatıcı `--update-now` | 2 | 10 | 179 ms | 155-209 ms |

Hedef 1 sn'nin altı; `YerindeYukleUygulamayiBirSaniyeninAltindaAcar` ortancayı `< 1000 ms` ile
pimler. Her turda üç dosya v2, günlük yok, sahne silinmiş, `SweepRetired` üç `.old` siler.

Sahte kurulumdaki dosyalar iki bayt. Gerçek kurulumda eski yol başlatıcı sürecini açar,
uygulamanın çıkmasını bekler ve sahnenin bütününü yeniden özetler. Yeni yolda bu üçü yok:
başlatıcı açılmaz, eski süreç yeni süreci açtıktan sonra kapanır (tek örnek kanalı
`VIDSHRINK_ONCEKI_SUREC` ile eskinin çıkmasını bekler), mühre uyan dosya yeniden özetlenmez.

## Eski Referans: 3133 ms

`docs/olcumler/bekleme-butceleri.md`: n=15, ortanca 3133 ms, aralık 3098-3308. Bu ölçüm
yuva ve güncelleme kilidi **dışarıdan tutulurken** alındı (`ElleYukleArkaPlanIndirirkenAcilisiGeciktirmez`),
süre 3 sn'lik elle yuva bütçesinin vazgeçmesidir. Bu turda aynı test bir kez: 3163 ms.

Hızlı yol üç kilidi sıfır beklemeyle alır; biri tutuluyorsa denemez ve eski yola düşer. Yani
arka planda kurulumu bekleyen bir başlatıcı yuvayı tutarken "Yükle" hâlâ bu 3,1 sn'yi öder.
Hızlı yol bu koşulu değiştirmiyor.

## Olumsuz Kontrol: Kilitli Dosya

`KilitliDosyaGeriAlinirEskiYolYineKurar`: `z.txt` `FileShare.Read` ile açık (silme paylaşımı
yok, ad değişmez). `Uygula` yanlış döner; üç dosya v1, hiç `.old` yok, günlük yok, sürüm
işareti yazılmamış, sahne `FindMismatch` ile hâlâ geçerli. Kilit kalkınca başlatıcı
`--update-now` aynı sahneyle v2 kurar. Geçti.

`KilitliDosyadaYerDegistirmeGeriAlinir` aynı şeyi süreç içinde: `b.dll` kilitliyken `Swap`
`IOException` atar, `a.txt` geri döner (`a.txt=v1,b.dll=v1`), sonraki `Swap` üçünü de koyar.
`YarimKalanYerDegistirmeGeriAlinir`: elle yazılmış günlükle yarım takas; günlük varken
`SweepRetired` 0 siler, `Recover` v1'i ve sahneyi geri getirir.

## Mutasyon

`InPlaceUpdate.Swap`'ın `catch` kolundaki `if (Undo(done)) ClearJournal(appDirectory);`
satırı `_ = done;` yapıldı. Sonuç 4 testin 2'si kırmızı:

- `KilitliDosyadaYerDegistirmeGeriAlinir` — koleksiyonlar farklı (`a.txt` v2'de kaldı).
- `KilitliDosyaGeriAlinirEskiYolYineKurar` — metinler farklı.

Satır geri kondu, `BaslaticiPanelsizTests` 37/37 yeşil.

## Sahne Mührü

`SahneMuhruDegismeyenDosyayiYenidenOzetlemez`: mühre giren dosya aynı boy ve aynı yazma
zamanıyla değiştirilirse güvenilir sayılır (ölçünün bilinen sınırı, mühür yalnız
hızlandırır); zaman 2 sn kayınca ya da mühür silinince tutmazlık bulunur.
