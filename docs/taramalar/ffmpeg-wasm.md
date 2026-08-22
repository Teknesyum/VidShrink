Tema: ffmpeg sarmalayici · kaynak: ffmpeg-sarmalayici.md

# ffmpeg sarmalayıcıları — tarama

Bakılan: argüman kurma sırası, filtre zinciri, ilerleme okuma, iptal/temizlik, hata anlamlandırma.
Künye rakamları `gh api` ile 2026-08-22'de alındı.

## ffmpegwasm/ffmpeg.wasm
**Künye:** MIT · 17.745 yıldız · 422 açık issue · son push 2026-02-01 · son sürüm v12.15 (2025-01-07).
Depo canlı ama açık issue sayısı yüksek.
**Ne yapıyor:** ffmpeg'i WebAssembly'ye derleyip web worker içinde çalıştırıyor. Süreç yerine mesaj
kanalı var: `exec(args, timeout)`, `on("log")`, `on("progress")`.
**Alınacak fikir:** **İptali başarısızlıktan ayrı bir yol yap.** `terminate()` bekleyen tüm işleri tek
bir `ERROR_TERMINATED` işaretiyle reddediyor, worker'ı yok ediyor, `loaded` bayrağını düşürüyor —
yeniden kullanmadan önce `load()` şart. Sonuç: iptal, hatayla karışmayan ayrı bir son durum ve nesne
yarı ölü kalmıyor. VidShrink iptalde zaten `Kill(entireProcessTree)` + kısmi dosya silme yapıyor;
eksik olan, iptalin arayüze hata gibi değil iptal gibi ulaşması ve motor durumunun açıkça sıfırlanması.
**Alınmayacak:** wasm/worker mimarisinin tamamı ve tek çıkış kodu (`ret`) ile yetinmek. Kendi belgeleri
ilerlemenin "yalnızca girdi ve çıktı uzunlukları eşitken doğru" olduğunu söylüyor — VidShrink kırpma
yaptığında bu varsayım bozulur; mevcut `EffectiveDuration` hesabı daha doğru, geri adım atma.
**Nereye dokunur:** `src/VidShrink.Ffmpeg/EncodeRunner.cs` (118-126 ve 161-162. satırlardaki `catch`
blokları), `src/VidShrink.Ffmpeg/TempCleanup.cs`, `src/VidShrink.App/MainWindow.xaml.cs`.

## Kaynaklar
`gh api repos/<owner>/<repo>` ve `.../releases/latest`. Kod iddiaları depoların `master`/`main`
dalındaki `_run.py`, `ffmpeg_writer.py`, `ffmpeg_reader.py`, `classes.ts`, `worker.ts`
dosyalarından okundu. Üçüncü taraf kaynak kullanılmadı, performans/kullanım iddiası aktarılmadı.
