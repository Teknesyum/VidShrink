# Hızlı Güncelleme: Yerinde Yer Değiştirme

Kaynak desen: AmeliyatListe `sync.rs`. Koşan dosya `<ad>.old` olur, sahnedeki doğrulanmış dosya
adını alır, uygulama kendini yeniden açar, `.old` bir sonraki açılışta sessizce silinir. Kod
`src/VidShrink.Core/InPlaceUpdate.cs`, `src/VidShrink.App/YerindeGuncelleme.cs`.

Ölçen testler `tests/VidShrink.Tests/BaslaticiPanelsizTests.Yerinde.cs`. Düzenek `.calisma/yol-d/`
altında sahte kurulum: gerçek başlatıcı, `tools/VidShrink.SahteUygulama`, yerel sahte yayın
(`VIDSHRINK_UPDATE_SOURCE`, üç dosya v1 → v2). Ağa çıkılmaz. Süre "Yükle"den yeni sürecin
`acildi` olayına kadar.

## Önce ve Sonra

Koşul: sahne hazır, yuva ve güncelleme kilidi boş, 2026-09-25, Release, bu makine. Arka planda
bekleyen başlatıcıyla gerçek koşul aşağıda ayrı tabloda.

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

Bu koşul (yuva **ve** indirme kilidi tutuluyor: arka plan başlatıcısı henüz indiriyor)
hızlı yolda da eski yola düşer, çünkü güncelleme kilidi sıfır beklemeyle alınamıyor. Yani
3,1 sn bu dar pencerede (arka plan indirmesi sürerken "Yükle") duruyor; hızlı yol onu
kaldırmıyor.

## Gerçek Koşul: Arka Plan Başlatıcısı Kapıda Beklerken

Güncellemenin olduğu tipik an: başlatıcı uygulamayı açmış, arka planda sahneyi indirmiş ve
kurmak için uygulamanın kapanmasını bekliyor (`KurulumBekleyeni.Kur` → `BosalincaAl`, 7 gün).
Bekleyen yuvası onun elinde; kapıyı ise beklerken bırakıyor. İlk sürüm hızlı yolu yuvaya
bağlıyordu, bu yüzden tam bu anda hep düşüyordu. Şimdi hızlı yol yuvayı almıyor; yalnız kapı
ve güncelleme kilidi, ikisi de sıfır beklemeyle.

Test `ArkaPlanBaslaticisiBeklerkenYukleHizliYoldanAcar`: gerçek başlatıcı ayarda
`autoUpdate` açık, sahte yayından indirir ve kapıda bekler. Sahte uygulama (`VIDSHRINK_SAHTE_YUKLE=1`)
yuvanın dolu, mührün yazılmış, indirme kilidinin boş olmasını bekler, sonra uygulamanın
`HizliYukle`sini taklit eder: `Uygula` + `YeniSurumuAc`, olmazsa `--update-now`. Süre
`yukle-basladi` olayından yeni sürecin `acildi` olayına. Her turda basış anında yuva dolu
(`yuva-dolu`) doğrulandı.

| Sürüm | Kol | n | Ortanca | Aralık |
|---|---|---|---|---|
| Önce (`HEAD` `YerindeGuncelleme`, yuvayı ister) | 10/10 eski | 10 | 114 ms | 104-145 ms |
| Sonra (yuvasız) | 10/10 hızlı | 10 | 68 ms | 57-73 ms |

Önce kolu 3,1 sn değil: eski uygulama çıkınca kapıda bekleyen arka plan başlatıcısı hemen
kurar ve yuvayı bırakır, `--update-now` başlatıcısı yuvayı 3 sn dolmadan alır. Denetim notundaki
"yuva dolu → 3,1 sn" beklentisi bu koşulda ölçülmedi; kazanç 114 → 68 ms ve kurulumun
uygulamanın kendi sürecinde yapılması.

Sonra arka plan başlatıcısı: eski süreç yeni süreci açıp çıkar, başlatıcı yeni süreç koştuğu
için beklemeye devam eder (kapıyı alamaması doğru), yeni süreç kapanınca kapı ve kilidi alır,
`Kurulmus` sürüm işaretini 9.9.9 görür ve kurmadan çekilir. Ölçülen: üç dosya v2, yazılma
anları yeni sürüm açıldığı andakiyle aynı, sürüm işareti 9.9.9, hata işareti yok, sahne yok,
takas günlüğü yok, `SweepRetired` üç `.old` siler, başlatıcı 60 sn içinde çıkar.

## Başlatıcının Kendi Dosyası Sahnede

`ArkaPlanBaslaticisiKendiDosyasiSahnedeykenGecisOnunCikisindaTamamlanir`: yayın `launcher`
alanında `VidShrink.exe` taşıyor (gerçek başlatıcı + bir bayt) ve arka plan başlatıcısı tam o
dosyadan koşuyor. Hızlı yol 75 ms, kol hızlı. `InPlaceUpdate.Apply` yeni başlatıcıyı
`VidShrink.new.exe`'ye koyar, günlüğü kurar, koşan ikilinin üstüne yazamaz (`Commit` sıfır
pencereyle düşer), `GecisiBaslat` geçişi yapan süreci pid'siz açar (30 sn pencere). Arka plan
başlatıcısı çekilip çıkınca geçiş oturur: `VidShrink.exe` yeni özette, başlatıcı sürüm
işareti 9.9.9, günlük ve `.new` yok. Geçti.

Uygulama 30 sn'den uzun açık kalırsa geçiş süreci vazgeçer; bu yıkıcı değil (`Commit` başarısızlıkta
hiçbir şeye dokunmaz), günlük ve `.new` kalır, başlatıcının bir sonraki açılışında `Repair`
geçişi pid'li süreçle yeniden kurar. Bu kol ayrıca ölçülmedi; mevcut `LauncherUpdate` testlerinin alanı.

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

İkinci mutasyon arka plan başlatıcısının çekilme kolunda: `KurulumBekleyeni.Kur`'daki
`if (Kurulmus(appDirectory, staged)) return false;` silindi. İki gerçek koşul testi de kırmızı
(`HepsiYeniSurum` içinde `Assert.False`: başlatıcı silinmiş sahneden yeniden kurmaya kalkıp
hata işareti yazdı). Satır geri kondu; `BaslaticiPanelsizTests`, `OluUyeTests`, `InPlace`,
`KurulumBekleyeni` süzgeci 59/59 yeşil.

## Sahne Mührü

`SahneMuhruDegismeyenDosyayiYenidenOzetlemez`: mühre giren dosya aynı boy ve aynı yazma
zamanıyla değiştirilirse güvenilir sayılır (ölçünün bilinen sınırı, mühür yalnız
hızlandırır); zaman 2 sn kayınca ya da mühür silinince tutmazlık bulunur.
