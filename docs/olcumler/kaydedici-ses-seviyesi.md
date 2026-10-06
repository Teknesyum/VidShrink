# Kaydedici Ses Seviyesi Göstergesi

Tarih 2026-10-06. Makine: Windows 11, ffmpeg 9.0 (Gyan full_build). Cihaz: "Mikrofon (Arctis Nova Pro Wireless)",
dshow. Düzenek `tools/kaydedici-ses-seviyesi/olc.ps1`; ham satırlar aşağıda aynen.

## Seçilen Yol

Cihaz başına ayrı, kısa ömürlü bir ffmpeg süreci: girdiyi tek kanal 8 kHz ham örneğe (`s16le`) çevirip
standart çıktıya yazar, tepe değeri C# tarafında 400 örneklik (50 ms) pencerelerde hesaplanır
(`Core/AudioLevel`, `Ffmpeg/AudioLevelSource`).

Neden bu yol:

- Kayıt sürecine dokunmuyor. Kaydın kendi ffmpeg'ine `astats`/`ebur128` eklemek her kaydın süzgeç grafiğini
  değiştirirdi ve kayıttan önce yine ayrı süreç gerekirdi.
- stderr metni ayrıştırılmıyor; ffmpeg sürümüyle değişen bir yazıma bağlanılmıyor. Düzenleyicinin dalga
  tepeleri (`AudioPeaks`) aynı ham örnek yolunu kullanıyor.
- Boru kapanınca ffmpeg kendiliğinden çıkıyor; yine de `Dispose` süreci ağacıyla öldürüyor.
- Süreç `BelowNormal`, `-threads 1`, stderr ayrı görevde boşaltılıyor.

Kayıt başlarken okuyucu kapatılır, kayıt süreci açıldıktan sonra yeniden açılır, kayıt bitince yeniden
başlatılır. Aşağıdaki ölçüm bozulma göstermedi; sıra yine de kaydın cihazı ilk açan olmasını sağlıyor.

## Aynı Cihazı İkinci Süreçle Açmak Kaydı Bozuyor Mu

Kayıt kolu: 6 sn `pcm_s16le` wav, cihazın kendi biçimi (44100 Hz, 2 kanal). Ölçer kolu: göstergenin
argümanlarıyla aynı girdi (`-audio_buffer_size 50`, tek kanal 8 kHz `s16le`). Dört koşum sırayla.

```
A-yalniz            kayit_cikis=0 duvar_ms=7309 wav_bayt=1055302 sure_sn=5.981995 akis=44100,2 uyari=[]
B-kayit-sonra-olcer kayit_cikis=0 duvar_ms=7276 wav_bayt=1054066 sure_sn=5.974989 uyari=[] olcer_cikis=0 olcer_bayt=31920 (2 sn)
C-olcer-sonra-kayit kayit_cikis=0 duvar_ms=7253 wav_bayt=1057774 sure_sn=5.996009 uyari=[] olcer_cikis=0 olcer_bayt=127680 (8 sn)
D-yalniz-tekrar     kayit_cikis=0 duvar_ms=7276 wav_bayt=1052834 sure_sn=5.968005 uyari=[]
```

| Koşum | Kayıt Süresi (sn) | Yalnız Ortalamasına Fark |
|---|---|---|
| A, yalnız | 5,982 | +0,007 |
| B, kayıt sürerken ölçer açıldı | 5,975 | 0,000 |
| C, ölçer açıkken kayıt başladı | 5,996 | +0,021 |
| D, yalnız (tekrar) | 5,968 | −0,007 |

Yalnız iki koşumun ortalaması 5,975 sn, aralarındaki fark 0,014 sn. Ölçerli iki koşum bu ortalamadan en çok
0,021 sn (%0,35) sapıyor; ikisi de çıkış kodu 0 ile bitti ve ffmpeg hiçbir uyarı yazmadı (tampon taşması,
kesinti, "busy"). Ölçer de iki koşumda beklenen baytı verdi (2 sn → 31 920, 8 sn → 127 680).

Hüküm: bu cihazda ikinci süreç kaydı bozmuyor. Windows'un paylaşımlı ses kipi aynı cihazı iki okuyucuya
veriyor.

## Ölçülmeyenler

- Tek cihaz, tek makine, koşum başına bir tekrar. Yalnız Windows dshow.
- Sistem sesi cihazı (bu makinede kurulu değil), macOS avfoundation ve Linux pulse ölçülmedi.
- Özel (exclusive) kipte açılan cihaz ölçülmedi; orada ikinci açılış reddedilebilir. Gösterge o durumda
  açılamayan kaynağı görür ve çubuğu gizler, kayda dokunmaz.
- Kaydın ses içeriği (örnek örnek) karşılaştırılmadı; ölçülen süre, bayt, çıkış kodu ve uyarı satırı.
- Gösterge gerçek pencerede gözle denenmedi.

## Mutasyonlar

`KaydediciSesSeviyesiTests`, taban 30/30 yeşil. Her mutasyon kaynakta elle yapıldı, kırmızı görüldü, elle
geri alındı.

| No | Kesim | Kırmızı |
|---|---|---|
| M1 | `AudioLevel.Fraction`: sessizlik eşiği dalını kaldırmak | 1/30 |
| M2 | `AudioLevel.Advance`: tutma süresini kaldırmak | 2/30 |
| M3 | `CloseLevel`: kaynağı `Dispose` etmemek | 10/30 |
| M4 | `DeactivateLevels`: sekmeden çıkınca canlı bırakmak | 6/30 |
| M5 | `AudioLevelSource.Dispose`: süreci öldürmemek | 1/30 |
| M6 | `RefreshLevels`: aynı cihaz korumasını kaldırmak | 7/30 |

M5 yalnız canlı kolda (lavfi `sine`) görünür; koşumdan sonra artık ffmpeg süreci kalmadığı elle bakıldı.
