# Eski Sürümden Güncelleme: GitHub 429

Tarih: 2026-10-17. Kaynak: Serkan'ın Windows makinesi, kurulu sürüm 0.9.5.

## Ölçüm

0.9.5'in kendi `VidShrink.Core`'u (etiket `v0.9.5`) kaynaktan derlendi; deneme klasöründe
`UpdateStaging.StageAsync(..., lanes: 6, ...)` ve ardından `InPlaceUpdate.Apply` gerçek
1.2.23 yayınına karşı koşuldu. Ayar dosyası `VIDSHRINK_SETTINGS_PATH` ile deneme klasöründe.

| Deneme | Sahnede dosya | Sonuç |
|---|---|---|
| 1 | 106 | `github.com` 429 (aralık isteği), düştü |
| 2 | 106 | 429, düştü |
| 3 | 598 | 429, düştü |
| 4-6 | 598 | manifest isteği düştü (`sahne=null`) |
| 7 | 655 / 655 | sahne tamam, takas başarılı, sürüm işareti 1.2.23 |

## Hüküm

- 0.9.5 dosya başına bir aralık isteği atıyor (655 dosya); GitHub'ın yönlendirme ucu birkaç yüz
  istekten sonra 429 dönüyor. Sahne denemeler arasında korunduğu için her "Yükle" ilerletiyor.
- 0.9.5'te otomatik güncelleme varsayılan kapalı (`UpdateSettings.AutoUpdate = false`); 0.9.6'da açıldı.
  Ayarı açmamış kullanıcı kendiliğinden hiç güncellenmez.
- Güncel sürümler tek paket indiriyor (`StagePackageAsync`); bu kırılma yalnız eski kurulumlarda.
- Sunucu tarafında düzeltilemez. Yol: `VidShrink-Setup.exe`'yi yeniden çalıştırmak, ya da "Yükle"ye birkaç kez basmak.
