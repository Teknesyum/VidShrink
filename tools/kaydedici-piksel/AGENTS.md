# Kaydedici Piksel

Kaydedicinin üç ekran bindirmesini (çerçeve, tıklama halkası, büyüteç) `gfxcapture` karelerinde,
tıklama sesini varsayılan çıkışın tepe ölçeriyle doğrular. Tablo `docs/olcumler/kaydedici-piksel.md`.

    dotnet build tools\kaydedici-piksel\VidShrink.KaydediciPiksel.csproj -m:2
    tools\kaydedici-piksel\bin\Debug\net8.0\VidShrink.KaydediciPiksel.exe [çıktı klasörü]

- Varsayılan çıktı `.calisma/paket-2b/piksel/`: `sonuc.tsv`, yakalama başına `.bgra`, son kare `.png`, ffmpeg günlüğü.
- Gerçek masaüstü ister: (100,100)'de 480x240 desen penceresi açar, bindirmeleri sırayla gösterir. ~40 sn.
- Ölçek 1 değilse koordinatlar geçersiz sayılır, araç 2 ile çıkar.
- Her ffmpeg çağrısı 15 sn bekçili. `ddagrab` bu makinede asılı kalıyor, kullanılmaz; gdigrab siyah verir.
- Ses kısmı kısa tıklama tonu çalar (duyulur). İmleç oynatılmaz; büyüteç sabit noktayla `Follow` edilir.
- Ayar yolu `VIDSHRINK_SETTINGS_PATH` ile çıktı klasörüne yönlenir; App'in `App` sınıfı açılmaz (HKCU'ya yazmaz).
- App'in internal türleri için `LanguageCatalog.cs`'te `InternalsVisibleTo("VidShrink.KaydediciPiksel")`.
