# VidShrink.MpvBench

Oynatıcı 0. dalga adım 1: libmpv yazılım render yolunun (`MPV_RENDER_API_TYPE_SW`, BGRA)
hız, arama, başlangıç, bellek ve işlemci ölçümü. Sayılar `docs/olcumler/libmpv-sw-render.md`.

    VidShrink.MpvBench suite --media <dir> --out <dir> [--reps 3]   # tüm matris, alt süreçlerle
    VidShrink.MpvBench fps|timed --file <f> --hwdec no|auto-copy    # tek koşu, JSON satırı
    VidShrink.MpvBench summarize --in runs.jsonl --out summary.md

libmpv yolu `--lib` ya da `VIDSHRINK_LIBMPV`, DLL depoya girmez; imzalar `client.h`/`render.h`'den.

**Sessiz makine kapısı.** Suite her koşudan önce sistem meşguliyeti ≤%10 olana dek bekler;
koşunun kendi `sysBusy` değeri fps'te %30'u, timed'da %15'i aşarsa koşuyu atıp yineler.
Atılanlar `rejected.jsonl`'e yazılır. Bu makinede MurphyEngine/stockfish yığınları dolaşıyor.

**İşlemci tik tabanlı okunmaz.** `GetProcessTimes` 15,625 ms tikle kısa render patlamalarını
~20 kat eksik sayar; birincil değer kalibre edilmiş `QueryProcessCycleTime`.

**Arama bitişi**: `MPV_EVENT_SEEK`'ten sonra render'ı başlayan ilk yeni (REDRAW olmayan)
kare; öncesinde render edilen eski kare `staleFrames` sayılır.
