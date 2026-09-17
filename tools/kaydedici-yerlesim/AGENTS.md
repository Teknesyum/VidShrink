# Kaydedici Yerleşim

Kaydedicinin çoklu ekran ve ölçek ≠ 1 piksel hesabını ölçer. Tablo
`docs/olcumler/kaydedici-coklu-ekran.md`.

    dotnet build tools\kaydedici-yerlesim\VidShrink.KaydediciYerlesim.csproj -m:2
    tools\kaydedici-yerlesim\bin\Debug\net8.0\VidShrink.KaydediciYerlesim.exe [çıktı klasörü]

- Varsayılan çıktı `.calisma/yerlesim/yerlesim.txt`; konsola da aynısı yazılır.
- Gerçek ekran **istemez**: monitör yerleşimi ve ölçek çarpanı programın içinde enjekte edilir.
  Beş yerleşim: tek ekran 1,0 / 1,25 / 1,5, aynı ölçekli iki ekran (ikincisi negatif X'te),
  farklı ölçekli iki ekran.
- Her yerleşimde üç şey ölçülür: ekran hedefinin ürettiği `gdigrab` yakalama dikdörtgeni
  (eski kol ile yan yana), bölge çizim örtüsünün fiziksel kaplaması (eski formül ile yan yana),
  monitör boşluğuna düşen bölgenin `Validate` dönüşü.
- Yalnız `VidShrink.Core`'a başvurur; süreç çalıştırmaz, saniyeler sürer, artık bırakmaz.
- `gdigrab`'ın fiziksel piksel konuştuğu ayrı bir ölçü: ffmpeg sürecinin
  `GetProcessDpiAwareness` değeri. Bu araçta değil, belgenin 1. bölümünde.
