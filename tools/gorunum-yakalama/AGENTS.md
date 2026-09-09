# Görünüm yakalama

Koşan `VidShrink.App` penceresini yakalayıp tıklamak için üç betik. Arayüz denetiminde
(`docs/inceleme/ui-denetim-2026-09-08.md`) kullanıldı.

PowerShell yürütme ilkesi bunları doğrudan koşturmaz; **her zaman** şu kalıpla çağır:

    powershell -NoProfile -ExecutionPolicy Bypass -File tools\gorunum-yakalama\ss.ps1 -Name kare1

- `ss.ps1 -Name <ad> [-Wait <sn>]` — pencereyi öne alır, tam pencereyi `<ad>.png` olarak
  betiğin yanına yazar, `ad genişlikxyükseklik` basar.
- `click.ps1 -X <px> -Y <px> [-Wait <sn>]` — koordinat **pencereye göredir**, ekrana göre değil.
- `kirp.ps1 -Src <png> -X -Y -W -H -Out <png> [-Olcek 2]` — kanıt kırpması, en yakın komşu
  büyütme; yollar çalışma dizinine göredir.

Uygulama açık olmalı; yoksa betikler `Get-Process VidShrink.App` üzerinde durur.

Tıklamadan sonra 2-4 saniye bekle: sekme geçişi çapraz geçiş animasyonlu, erken yakalanan
kare iki sekmeyi üst üste gösterir ve kusur sanılır.

Ekran görüntüsü `.calisma/` altına; rapora giren kare `docs/inceleme/<denetim>/ekran/` altına.
