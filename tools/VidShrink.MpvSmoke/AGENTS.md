# VidShrink.MpvSmoke

Oynatıcı 0. dalga kapı A: paketlenmiş libmpv'nin yüklenip SW render ile kare çizdiği duman.
libmpv'yi yükler, dosyayı açar, BGRA'ya (stride 4*w, 64 bayt hizalı) kare çizer, sıfır
olmayan pikselleri sayar, mpv/ffmpeg sürümünü basar. Geçerse `smoke: PASS`, çıkış 0.

    VidShrink.MpvSmoke --lib <libmpv.2.dylib|libmpv-2.dll> --file <video> [--timeout 30]

P/Invoke imzaları `../VidShrink.MpvBench/Native.cs`'den bağlantıyla gelir; kopya yok.

`bundle-macos.py <libmpv.2.dylib> <hedef>`: libmpv ve `/opt/homebrew`, `/usr/local`
altındaki tüm bağımlılıklarını hedefe kopyalar, başvuruları `@loader_path/`, kimliği
`@rpath/` yapar, yabancı LC_RPATH'leri siler, ad-hoc imzalar.

Koşum: `.github/workflows/macos-libmpv-gate.yml`. Sonuç `docs/olcumler/libmpv-macos-gomme.md`.
