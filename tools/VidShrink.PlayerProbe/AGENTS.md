# VidShrink.PlayerProbe

Oynatici hatti karari icin K1-K4 sayilarini ureten olcum araci. T167'de kuruldu.
Sayilar `docs/olcumler/oynatici-hatti.md` icinde; burada onlari **ureten** kod var.

    VidShrink.PlayerProbe.exe gen-sync-clip <cikis.mkv> <sureSn>   # K3'un sentetik klibi
    VidShrink.PlayerProbe.exe k1-own <kaynak> <aralikSn> <tekrar>  # ffmpeg pipe seek
    VidShrink.PlayerProbe.exe k1-vlc <kaynak> <aralikSn> <tekrar>  # LibVLCSharp seek
    VidShrink.PlayerProbe.exe k3-own <senkronKlibi> <sureSn>       # NAudio + video pipe
    VidShrink.PlayerProbe.exe k3-vlc <senkronKlibi> <sureSn>       # LibVLC ham callback

`.sln`e eklenmedi (owns disi); dogrudan `dotnet build/publish tools/VidShrink.PlayerProbe/...` ile calisir.

K2/K4 icin `NAudio`, `LibVLCSharp`, `VideoLAN.LibVLC.Windows` paketleri **birlikte**
referans; K2'nin ayri boyut olcumu her paket tek basinayken yapildi (ham cikti belgede).

k3-vlc'deki audio pts'i `vlc_tick_now()` epoch'unda mutlak saat — ilk gozlenen deger
sifir noktasi alinip video pts eksenine (medya basi = 0) tasinir.

Kaynak dosyalar `.calisma/kaynak/` ve `.calisma/kaynak-genis/` altinda, mutlak yolla
verilir; **oku, yazma, silme**. Cikti `.calisma/T167/` altina.
