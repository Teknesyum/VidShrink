# Kurulum Süresi: Betik Ve VidShrink-Setup.exe

Makine: DESKTOP-0J80KVV, Windows 11 22631. Kurulan sürüm: v0.8.2 (son yayın). FFmpeg PATH'te
yüklü (ayrıca belirtilen koşum hariç). Tarih: 16 Eylül 2026.

Kullanıcının gerçek kurulumuna dokunulmadı. Kurulum kökü `LOCALAPPDATA`, `TMP` ve `TEMP`
ortam değişkenleriyle `.calisma/kurulum-olcum/kok` altına, kayıt kökü
`HKCU:\Software\VidShrinkOlcum\Classes` test anahtarına yönlendirildi. Yapay yük yok.

## Düzenek

- `tools/kurulum-olcum/olc-betik.ps1` — `Install-VidShrink.ps1`'i aynı kum havuzunda koşturur,
  her çıktı satırını geçen milisaniyeyle günlüğe yazar.
- `tools/kurulum-olcum/olc-exe.ps1` — yayınlanmış `VidShrink-Setup.exe`'yi aynı kum havuzunda
  `--timings` ile koşturur; dış saat düzenekten, iç adımlar exe'den.
- `tools/kurulum-olcum/olc-adim.ps1` — betiğin indirme, sağlama, açma ve kopya adımlarını ayrı zamanlar.
- `tools/kurulum-olcum/mutasyon.ps1` — negatif kontrol; beş mutasyonu tek tek uygular, testi koşturur, geri alır.

Günlükler `.calisma/kurulum-olcum/` altına düşer.

## Önce: Betik

| Koşum | Toplam |
| --- | --- |
| İlk kurulum (`-SkipShortcuts -NoLaunch`) | 12.271 ms |
| Yeniden kurulum | 9.100 ms |
| Yeniden kurulum, bu oturumda tekrar | 9.495 ms |
| `-ShellMenuOnly` test anahtarına | 4.478 ms (kayıt kısmı ~3,3 sn) |

Yeniden kurulumun dağılımı: indirme ~2,06 sn, sağlama + `Expand-Archive` ~3,8 sn, kopya ~1,4 sn.

## Sonra: VidShrink-Setup.exe

Yayınlanan tek dosya: 11.757.968 bayt (win-x64, self-contained, trimmed, sıkıştırılmış).

`--skip-shortcuts --no-launch`, adımlar kümülatif ms:

| Koşum | Dış | İç | etiket | yayin-indirildi | dosyalar-yerinde | bitti |
| --- | --- | --- | --- | --- | --- | --- |
| İlk (libmpv indirildi) | 6.127 | 5.912 | 626 | 4.445 | 5.885 | 5.895 |
| Yeniden kurulum | 5.664 | 5.466 | 475 | 3.705 | 5.333 | 5.453 |
| Üçüncü | 4.525 | 4.340 | 488 | 2.952 | 4.207 | 4.327 |

- Kısayol + menü + ilişkilendirme test anahtarına (`--shortcut-dir`, `--menu-language tr`):
  dış 5.167 ms, iç 4.997 ms; `kabuk` adımı 119 ms (4.752 → 4.871). Betikte aynı iş ~3,3 sn.
- `--download-ffmpeg` ile ilk kurulum (FFmpeg zip'inden aralıklı okuma + libmpv indirme):
  dış 11.862 ms, iç 11.667 ms. Çıkan dosyaların sha256 başları `05F4251BCE92` / `51E0780CD881`, sabitlerle aynı.
- Aynı anda `curl.exe` ile yalnız uygulama zip'i: 1.557 ms. İndirme adımı bant genişliğine bağlı;
  paralel indirme burada az kazandırıyor.

## Denklik

- Dosya düzeni: betik 569, exe 569 dosya. Tek fark sürüm işaret dosyaları: betik UTF-8 BOM'lu (8 bayt), exe BOM'suz (5 bayt).
- Kısayol WScript.Shell ile okundu: hedef, çalışma klasörü ve `...VidShrink.exe,0` simgesi aynı.
- Kayıt ağacı: `KurucuExeTests.BetikleAyniAnahtarlariYaziyorVeAyniSekildeSiliyor` betik ve motorun yazdığı
  ağaçları (kök yolu `<kok>` ile) birebir karşılaştırır; silme kolunda da.
- Kum havuzunda kaldırma: menü 24, paket 0, ilişkilendirme 24 anahtar silindi, kök ve kısayollar gitti.
  Boş kalan `kisayol\Programs` klasörü ve boş `SystemFileAssociations` anahtarı duruyor (betik de bırakıyor).

## Negatif Kontrol

`KurucuExeTests` 13/13 yeşil. Her mutasyon tek testi kırdı, geri alınınca 13/13 yeşil:

| Mutasyon | Kırılan test |
| --- | --- |
| M1 küçült fiili `MultiSelectModel` Player → Document | BetikleAyniAnahtarlariYaziyorVeAyniSekildeSiliyor |
| M2 tutan tur eşiği 2 → 3 | IkiTurTutanVarsaKapatilipYenidenDeneniyor |
| M3 uygulama zip sağlaması atlandı | SaglamaTutmazsaEskiKurulumaDokunulmuyor |
| M4 yarım kurulumda eski klasörü geri koyma yok | YarimKalanKurulumEskiKlasoruGeriKoyuyor |
| M5 libmpv dll sabiti bir hane değişti | SabitlerBetikleAyni |

Kayıt kapısı (`ShellRegistration.WriteAllowed`) mutasyona sokulmadı: kırılsa gerçek HKCU'ya yazardı.
