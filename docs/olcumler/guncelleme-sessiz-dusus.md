# Masaüstündeki Kurulum Neden Kendini Güncellemedi

Tarih: 2026-09-29. Makine: geliştiricinin Windows 11 masaüstü.

## Durum

- Kısayol: `Desktop\VidShrink.lnk` → `%LOCALAPPDATA%\Programs\VidShrink\VidShrink.exe`
- Kurulu başlatıcı ve uygulama: `0.2.5+18a7fe0` (kurulum 2026-09-05). Yayındaki son sürüm: `1.0.0`.
- `settings.json`: `"autoUpdate": true`. Ayar açık, yol hiç çalışmamış.
- Yayında 0.2.5'in beklediği bütün varlıklar duruyor: `manifest-win-x64.json`,
  `vidshrink-win-x64.zip`, `vidshrink-launcher-win-x64.zip`. Sorun adres değil.

## Kök Neden

0.2.5'te güncelleme başlatıcının açılış yolundaydı ve manifest çekimi
`UpdateCheck.ManifestTimeout = 800 ms` ile kesiliyordu. Zaman aşımı `null` döndürüyor,
`Updater.Run` bunu "güncelleme yok" sayıp sessizce çıkıyordu. GitHub'ın
`releases/latest/download/...` adresi başka bir sunucuya yönlendiriyor; temiz bir süreçte
iki TLS el sıkışması 800 ms'ye sığmıyor.

Ölçüm, kurulu `VidShrink.Core.dll` (0.2.5) doğrudan çağrılarak, beş ayrı temiz süreçte
(`UpdateCheck.FetchManifestAsync(LatestAssetUrl("manifest-win-x64.json"))`):

```
835 ms  NULL (sessizce vazgecildi)  auto=True
837 ms  NULL (sessizce vazgecildi)  auto=True
822 ms  NULL (sessizce vazgecildi)  auto=True
827 ms  NULL (sessizce vazgecildi)  auto=True
836 ms  NULL (sessizce vazgecildi)  auto=True
```

Aynı dosya PowerShell ile: soğuk 1245 ms, ılık 407 ms ve 403 ms. Her açılış yeni süreç, yani
her açılış soğuk: beşte beş düşüş.

İkinci kusur bunu görünmez yaptı: otomatik güncelleme açıkken uygulama kendi denetimini hiç
yapmıyor ve rozeti gizliyordu (`if (UpdateCheck.AutoUpdateEnabled()) return;`). Başlatıcı
düşünce kimse bir şey söylemedi.

## Bugün Hâlâ Geçerli Olan

Zaman aşımı 0.4.2 işinde (ilk yayın v0.4.4) 5 sn'ye çıktı ve denetim açılıştan sonraya taşındı; 800 ms kusuru
yeni sürümlerde yok. Ama sınıf yaşıyordu: sessiz sahneleme hatayı yutar
(`SessizIndirmeBitti` sahne yoksa susar), otomatik güncelleme açıkken `CheckForUpdateAsync`
yine erken döner. Yarın başka bir sebep (yönlendirme, özet uyuşmazlığı, kilit, kapanışta
düşen kurulum) aynı sessiz takılmayı yeniden kurar.

## Çözüm

- `Core/UpdateHealth`: art arda düşüş sayacı, ayar dosyasının yanında `update-health.json`.
  Sessiz sahneleme "ulaşılamadı" ya da hata ile biterse, kapanışta kurulum düşerse sayılır;
  "zaten güncel" sıfırlar. Kilit başkasındaysa ya da iptal edildiyse sayılmaz.
- Üç düşüşte (`FailureLimit`) uygulama sessiz yola güvenmeyi bırakır: `CheckForUpdateAsync`
  yedek kipte bir kez koşar, yeni sürüm varsa rozet ve panel görünür, kullanıcı kurar.
  Sessiz yol da denenmeye devam eder.
- Sebep kayda yazılır (`lastError`): bir sonraki araştırma tahminle başlamaz.

## Eski Kurulumlar

v0.4.4 öncesi kurulumlar 800 ms kapısını taşıyor, kendilerini hiç güncelleyemez; kodları uzaktan
değiştirilemez. Onlar için tek yol yeniden kurulum: yayındaki `VidShrink-Setup.exe`.

## Kısayol Simgesi

Kısayolun simgesi `VidShrink.exe,0`; exe değişince Windows simge önbelleği eskiyi göstermeye
devam eder. `ShortcutIcons` yalnız kurulum ve güncelleme sonunda koşuyordu. Artık uygulama
açılıştan sonra, düşük öncelikte, sürüm başına bir kez (`.shortcut-icon-version` işareti
kurulum kökünde) masaüstü ve Başlat menüsü kısayollarının simgesini yeniden yazar ve kabuğa
haber verir. Hedefi kurulu başlatıcı olmayan kısayola dokunulmaz.
