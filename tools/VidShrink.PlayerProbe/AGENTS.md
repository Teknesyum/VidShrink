# VidShrink.PlayerProbe

Oynatici hatti karari icin K1-K4 sayilarini ureten olcum araci. T167'de kuruldu,
tur 3'te K1-vlc ve K3-own yontemleri duzeltildi.
Sayilar `docs/olcumler/oynatici-hatti.md` icinde; burada onlari **ureten** kod var.

    VidShrink.PlayerProbe.exe gen-sync-clip <cikis.mkv> <sureSn>   # K3'un sentetik klibi
    VidShrink.PlayerProbe.exe k1-own <kaynak> <aralikSn> <tekrar>  # ffmpeg pipe seek
    VidShrink.PlayerProbe.exe k1-vlc <kaynak> <aralikSn> <tekrar>  # LibVLCSharp seek
    VidShrink.PlayerProbe.exe k1-taban <tekrar>                    # ffmpeg surec baslatma bedeli
    VidShrink.PlayerProbe.exe k3-own <senkronKlibi> <sureSn>       # NAudio + "-re" video pipe
    VidShrink.PlayerProbe.exe k3-vlc <senkronKlibi> <sureSn>       # LibVLC ham callback

`.sln`e eklenmedi (owns disi); `dotnet build/publish tools/VidShrink.PlayerProbe/...`
ile dogrudan calisir. `.sln`e eklemeyin: CI'a LibVLC/NAudio paketlerini tasir.

**k1-vlc saat okumaz.** `SetVideoFormat`+`SetVideoCallbacks` ile gelen karenin parmak
izini, ffmpeg'in actigi "hedeften once" / "hedeften sonra" referans kumeleriyle
karsilastirir; sonra kumesi kazandiginda durur. `TimeChanged` kullanmayin — o input'un
~250 ms periyotlu saat raporudur, kare teslimi degil.

**k3-own uretim gibi paceler**: video borusunda `-re` var (`ComparisonGraph.cs:87`,
`PanelHost.cs:460`). Uretimin vsync drain + bayat kare dusurme katmani burada yok.
k3-vlc'deki audio pts'i `vlc_tick_now()` epoch'unda mutlak saat — ilk gozlenen deger
sifir noktasi alinir, o yuzden ilk orneklem raporda atilir.

Kaynak dosyalar `.calisma/kaynak/` ve `.calisma/kaynak-genis/` altinda, mutlak yolla
verilir; **oku, yazma, silme**. Cikti `.calisma/T167/` altina.
