# İpucu satır genişlikleri

Bu dosyayı `TipOverflowTests` üretir, elle yazılmaz. Yeniden üretmek için:

```
dotnet test VidShrink.sln -c Release --filter TipOverflowTests
```

Ölçüm uygulamanın kendi yazı tipiyle yapılır (Atkinson Hyperlegible Next,
16 px). Tavan `Themes/Theme.axaml` belirteçlerinden
hesaplanır: `TooltipMaxWidth` eksi iki yanın dolgusu ve kenarlığı = **426 px**.

Ölçülen satır: **190** · tavanı aşan: **121** ·
tek kelimeyle aşan: **0**

| Dil | İpucu | Satır | Genişlik | Taşma | Görsel satır | Alt satır | Tek kelime |
| --- | --- | ---: | ---: | ---: | ---: | --- | :-: |
| EN | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 0 | 548 | 122 | 2 | file larger than this. |  |
| EN | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 1 | 939 | 513 | 3 | weaker than this one. |  |
| EN | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 2 | 535 | 109 | 2 | instead of WhatsApp's. |  |
| EN | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 3 | 533 | 107 | 2 | and chat apps. |  |
| TR | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 0 | 504 | 78 | 2 | dosya vermez. |  |
| TR | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 1 | 1080 | 654 | 3 | kodlayıcısı buradakinden çok daha zayıftır. |  |
| TR | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 2 | 505 | 79 | 2 | kalitesini korur. |  |
| TR | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 3 | 629 | 203 | 2 | ve sohbet uygulamalarına uyar. |  |
| EN | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 0 | 493 | 67 | 2 | bitrate encoder. |  |
| EN | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 1 | 960 | 534 | 3 | quality, not WhatsApp's. |  |
| EN | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 2 | 599 | 173 | 2 | document instead of as a video. |  |
| TR | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 0 | 571 | 145 | 2 | kodlayıcısıyla yeniden kodlar. |  |
| TR | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 1 | 957 | 531 | 3 | VidShrink'in kalitesi olur. |  |
| TR | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 2 | 650 | 224 | 2 | olarak değil belge olarak gönderin. |  |
| EN | MainWindow.axaml · 8 MB fits Discord without Nitro, older forum… | 0 | 509 | 83 | 2 | mail gateways. |  |
| TR | MainWindow.axaml · 8 MB fits Discord without Nitro, older forum… | 0 | 527 | 101 | 2 | geçitlerine uyar. |  |
| EN | MainWindow.axaml · 25 MB fits Gmail attachments, Discord Nitro… | 0 | 532 | 106 | 2 | ticket systems. |  |
| TR | MainWindow.axaml · 25 MB fits Gmail attachments, Discord Nitro… | 0 | 496 | 70 | 2 | sistemine uyar. |  |
| EN | MainWindow.axaml · 100 MB suits archiving and uploads where qua… | 0 | 582 | 156 | 2 | more than transfer time. |  |
| TR | MainWindow.axaml · 100 MB suits archiving and uploads where qua… | 0 | 586 | 160 | 2 | yüklemelere uygundur. |  |
| EN | MainWindow.axaml · Half of the source size. | 1 | 587 | 161 | 2 | resolution and frame rate. |  |
| TR | MainWindow.axaml · Half of the source size. | 1 | 610 | 184 | 2 | çözünürlüğü ve kare hızını korur. |  |
| EN | MainWindow.axaml · Intent sets how early the engine stops spend… | 1 | 618 | 192 | 2 | leave a lot of the target unused. |  |
| EN | MainWindow.axaml · Intent sets how early the engine stops spend… | 2 | 693 | 267 | 2 | noticing — the right choice for WhatsApp. |  |
| EN | MainWindow.axaml · Intent sets how early the engine stops spend… | 3 | 597 | 171 | 2 | re-encode the file anyway. |  |
| TR | MainWindow.axaml · Intent sets how early the engine stops spend… | 1 | 717 | 291 | 2 | hedefin büyük kısmını kullanmadan bırakabilir. |  |
| TR | MainWindow.axaml · Intent sets how early the engine stops spend… | 2 | 750 | 324 | 2 | noktada durur — WhatsApp için doğru seçim budur. |  |
| TR | MainWindow.axaml · Intent sets how early the engine stops spend… | 3 | 624 | 198 | 2 | zaten yeniden kodlayacaktır. |  |
| EN | MainWindow.axaml · H.264 is universal: every phone made in the… | 0 | 749 | 323 | 2 | years plays it, and WhatsApp never re-encodes it. |  |
| EN | MainWindow.axaml · H.264 is universal: every phone made in the… | 1 | 1155 | 729 | 3 | older Android phones and some web players refuse it. |  |
| EN | MainWindow.axaml · H.264 is universal: every phone made in the… | 2 | 957 | 531 | 3 | compatibility risk. |  |
| EN | MainWindow.axaml · H.264 is universal: every phone made in the… | 3 | 713 | 287 | 2 | (GPU) below to let the graphics card encode. |  |
| TR | MainWindow.axaml · H.264 is universal: every phone made in the… | 0 | 717 | 291 | 2 | oynatır ve WhatsApp onu yeniden kodlamaz. |  |
| TR | MainWindow.axaml · H.264 is universal: every phone made in the… | 1 | 1179 | 753 | 3 | Android telefonlar ve bazı web oynatıcılar kabul etmez. |  |
| TR | MainWindow.axaml · H.264 is universal: every phone made in the… | 2 | 675 | 249 | 2 | uyumluluk riskini aştığında H.265'i seçer. |  |
| TR | MainWindow.axaml · H.264 is universal: every phone made in the… | 3 | 856 | 430 | 3 | seçeneğini açın. |  |
| EN | MainWindow.axaml · When the target is tight, fewer pixels encod… | 0 | 918 | 492 | 3 | than blocking. |  |
| EN | MainWindow.axaml · When the target is tight, fewer pixels encod… | 1 | 1056 | 630 | 3 | resolution the budget can still hold cleanly. |  |
| EN | MainWindow.axaml · When the target is tight, fewer pixels encod… | 2 | 871 | 445 | 3 | always the better trade. |  |
| TR | MainWindow.axaml · When the target is tight, fewer pixels encod… | 0 | 906 | 480 | 3 | rahatsız eder. |  |
| TR | MainWindow.axaml · When the target is tight, fewer pixels encod… | 1 | 993 | 567 | 3 | büyük çözünürlüğü seçer. |  |
| TR | MainWindow.axaml · When the target is tight, fewer pixels encod… | 2 | 899 | 473 | 3 | bir takastır. |  |
| EN | MainWindow.axaml · Halving the frame rate frees bits for the re… | 1 | 732 | 306 | 2 | below a level where motion starts to stutter. |  |
| EN | MainWindow.axaml · Halving the frame rate frees bits for the re… | 2 | 693 | 267 | 2 | cost is smoothness, not compatibility. |  |
| TR | MainWindow.axaml · Halving the frame rate frees bits for the re… | 1 | 726 | 300 | 2 | takılmaya başladığı seviyenin altına asla inmez. |  |
| TR | MainWindow.axaml · Halving the frame rate frees bits for the re… | 2 | 572 | 146 | 2 | akıcılıktır, uyumluluk değil. |  |
| EN | MainWindow.axaml · Graphics cards encode many times faster than… | 1 | 1158 | 732 | 3 | encoder's quality at about seven times the speed. |  |
| EN | MainWindow.axaml · Graphics cards encode many times faster than… | 2 | 555 | 129 | 2 | quality per megabyte. |  |
| TR | MainWindow.axaml · Graphics cards encode many times faster than… | 1 | 1116 | 690 | 3 | neredeyse aynı kaliteyi yaklaşık yedi kat hızlı verir. |  |
| TR | MainWindow.axaml · Graphics cards encode many times faster than… | 2 | 586 | 160 | 2 | başına bir miktar kalitedir. |  |
| EN | MainWindow.axaml · Fill target lands close to the target size a… | 0 | 660 | 234 | 2 | the best quality the budget allows. |  |
| EN | MainWindow.axaml · Fill target lands close to the target size a… | 1 | 802 | 376 | 2 | no padding, but the file can end up noticeably smaller. |  |
| TR | MainWindow.axaml · Fill target lands close to the target size a… | 0 | 590 | 164 | 2 | verdiği en iyi kaliteyi sıkar. |  |
| TR | MainWindow.axaml · Fill target lands close to the target size a… | 1 | 768 | 342 | 2 | dosyayı şişirmez ama belirgin biçimde küçük kalabilir. |  |
| EN | MainWindow.axaml · Preserving HDR keeps the source's wider colo… | 0 | 1037 | 611 | 3 | devices and apps play it correctly. |  |
| EN | MainWindow.axaml · Preserving HDR keeps the source's wider colo… | 1 | 807 | 381 | 2 | range — smaller, and safe on WhatsApp and any phone. |  |
| TR | MainWindow.axaml · Preserving HDR keeps the source's wider colo… | 0 | 1136 | 710 | 3 | yalnızca yeni cihazlar ve uygulamalar doğru oynatır. |  |
| TR | MainWindow.axaml · Preserving HDR keeps the source's wider colo… | 1 | 802 | 376 | 2 | eder — daha küçük ve WhatsApp ile her telefonda güvenli. |  |
| EN | MainWindow.axaml · VidShrink times the sample encodes it alread… | 0 | 1193 | 767 | 3 | machine and this file rather than from a table of presets. |  |
| EN | MainWindow.axaml · VidShrink times the sample encodes it alread… | 1 | 1057 | 631 | 3 | and how much less is not measured. |  |
| EN | MainWindow.axaml · VidShrink times the sample encodes it alread… | 2 | 789 | 363 | 2 | encoded with, the time is left blank instead of guessed. |  |
| TR | MainWindow.axaml · VidShrink times the sample encodes it alread… | 0 | 1180 | 754 | 3 | tablosundan değil, bu makineden ve bu dosyadan gelir. |  |
| TR | MainWindow.axaml · VidShrink times the sample encodes it alread… | 1 | 1019 | 593 | 3 | kadar ucuz olduğu ise ölçülmez. |  |
| TR | MainWindow.axaml · VidShrink times the sample encodes it alread… | 2 | 600 | 174 | 2 | tahmin edilmez, boş bırakılır. |  |
| EN | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 0 | 1238 | 812 | 3 | really needs — it does not guess from the source bitrate. |  |
| EN | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 1 | 684 | 258 | 2 | narrow range rather than a rule of thumb. |  |
| EN | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 2 | 1134 | 708 | 3 | spending the rest would buy nothing you could see. |  |
| TR | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 0 | 1181 | 755 | 3 | gerektiğini ölçer — kaynak bit hızından tahmin yürütmez. |  |
| TR | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 1 | 617 | 191 | 2 | sayı olmasının sebebi budur. |  |
| TR | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 2 | 1038 | 612 | 3 | gözle görülür bir şey satın almaz. |  |
| EN | MainWindow.axaml · The container is the file type. | 1 | 851 | 425 | 3 | opens it. |  |
| EN | MainWindow.axaml · The container is the file type. | 2 | 696 | 270 | 2 | phone galleries treat it as a document. |  |
| EN | MainWindow.axaml · The container is the file type. | 4 | 571 | 145 | 2 | Android support is uneven. |  |
| TR | MainWindow.axaml · The container is the file type. | 1 | 943 | 517 | 3 | telefon açar. |  |
| TR | MainWindow.axaml · The container is the file type. | 2 | 650 | 224 | 2 | telefon galerisi onu belge sayar. |  |
| TR | MainWindow.axaml · The container is the file type. | 4 | 612 | 186 | 2 | Android desteği ise düzensizdir. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device ever made… | 0 | 817 | 391 | 2 | WhatsApp expects; pick it when the file has to just work. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device ever made… | 1 | 1650 | 1224 | 5 | encode it. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device ever made… | 2 | 590 | 164 | 2 | but rarely in the gallery. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device ever made… | 3 | 610 | 184 | 2 | only recent phones decode it. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device ever made… | 4 | 762 | 336 | 2 | no waiting — whenever the container accepts it. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device ever made… | 0 | 919 | 493 | 3 | bunu seçin. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device ever made… | 1 | 1551 | 1125 | 4 | açmaz, WhatsApp da yeniden kodlayabilir. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device ever made… | 2 | 644 | 218 | 2 | uygulamalarda oynatır ama galeride nadiren. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device ever made… | 3 | 550 | 124 | 2 | yeni telefonlar çözer. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device ever made… | 4 | 768 | 342 | 2 | kalite kaybı ve bekleme olmadan olduğu gibi korur. |  |
| EN | MainWindow.axaml · CRF targets visual quality, so final size ca… | 2 | 650 | 224 | 2 | size or bandwidth matters more. |  |
| TR | MainWindow.axaml · CRF targets visual quality, so final size ca… | 1 | 580 | 154 | 2 | daha öngörülebilir yapar. |  |
| TR | MainWindow.axaml · CRF targets visual quality, so final size ca… | 2 | 679 | 253 | 2 | daha önemliyse sabit bit hızı kullanın. |  |
| EN | MainWindow.axaml · In CRF mode, a lower number means higher qua… | 0 | 758 | 332 | 2 | larger file; 23 is a common H.264 starting point. |  |
| EN | MainWindow.axaml · In CRF mode, a lower number means higher qua… | 1 | 699 | 273 | 2 | value gives more quality and a larger file. |  |
| TR | MainWindow.axaml · In CRF mode, a lower number means higher qua… | 0 | 806 | 380 | 2 | büyük dosya demektir; 23, H.264 için yaygın bir başlangıçtır. |  |
| TR | MainWindow.axaml · In CRF mode, a lower number means higher qua… | 1 | 803 | 377 | 2 | yüksek değer daha fazla kalite ve daha büyük dosya verir. |  |
| EN | MainWindow.axaml · Source keeps the original dimensions. | 1 | 499 | 73 | 2 | aspect ratio. |  |
| TR | MainWindow.axaml · Source keeps the original dimensions. | 1 | 481 | 55 | 2 | yüksekliğini sınırlar. |  |
| EN | MainWindow.axaml · Used only when frame rate is custom. | 2 | 497 | 71 | 2 | motion detail. |  |
| TR | MainWindow.axaml · Used only when frame rate is custom. | 2 | 523 | 97 | 2 | ayrıntısı eklemez. |  |
| EN | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 0 | 795 | 369 | 2 | every phone and WhatsApp handle without complaint. |  |
| EN | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 1 | 718 | 292 | 2 | inside MP4 it will not play on many phones. |  |
| EN | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 4 | 614 | 188 | 2 | when the container supports it. |  |
| EN | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 5 | 652 | 226 | 2 | when you are squeezing a silent clip. |  |
| TR | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 0 | 725 | 299 | 2 | WhatsApp'ın sorunsuz işlediği tek ses kodeğidir. |  |
| TR | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 1 | 715 | 289 | 2 | WebM'dir; MP4 içinde birçok telefonda oynamaz. |  |
| TR | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 4 | 527 | 101 | 2 | kodlamadan korur. |  |
| TR | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 5 | 514 | 88 | 2 | seçim budur. |  |
| EN | MainWindow.axaml · Audio data rate in kilobits per second. | 1 | 675 | 249 | 2 | and 256 or 320 is useful for music. |  |
| TR | MainWindow.axaml · Audio data rate in kilobits per second. | 1 | 666 | 240 | 2 | 256 veya 320 müzik için kullanışlıdır. |  |
| TR | MainWindow.axaml · Audio data rate in kilobits per second. | 2 | 518 | 92 | 2 | değer kullanılmaz. |  |
| EN | Theme.axaml/AiHintText · This step is optional. | 1 | 511 | 85 | 2 | JSON answer. |  |
| TR | Theme.axaml/AiHintText · This step is optional. | 1 | 641 | 215 | 2 | JSON yanıtını yapıştırıp doğrulayın. |  |
| EN | MainWindow.axaml.cs/AutoUpdateEffectEnglish · When this is off, VidShrink does not update… | 0 | 915 | 489 | 3 | installs it. |  |
| TR | MainWindow.axaml.cs/AutoUpdateEffectEnglish · When this is off, VidShrink does not update… | 0 | 783 | 357 | 2 | bir sürüm olduğunu söyler ve kuran komutu gösterir. |  |
| EN | MainWindow.axaml.cs/NoSelfUpdateEffectEnglish · VidShrink does not update itself on this sys… | 0 | 905 | 479 | 3 | installs it. |  |
| TR | MainWindow.axaml.cs/NoSelfUpdateEffectEnglish · VidShrink does not update itself on this sys… | 0 | 772 | 346 | 2 | sürüm olduğunu söyler ve kuran komutu gösterir. |  |
| EN | MainWindow.axaml.cs/HardwareTipEnglish · Graphics cards encode many times faster than… | 1 | 1158 | 732 | 3 | encoder's quality at about seven times the speed. |  |
| EN | MainWindow.axaml.cs/HardwareTipEnglish · Graphics cards encode many times faster than… | 2 | 555 | 129 | 2 | quality per megabyte. |  |
| TR | MainWindow.axaml.cs/HardwareTipEnglish · Graphics cards encode many times faster than… | 1 | 1116 | 690 | 3 | neredeyse aynı kaliteyi yaklaşık yedi kat hızlı verir. |  |
| TR | MainWindow.axaml.cs/HardwareTipEnglish · Graphics cards encode many times faster than… | 2 | 586 | 160 | 2 | başına bir miktar kalitedir. |  |
| EN | MainWindow.axaml.cs/NoHardwareTipEnglish · No usable hardware encoder was found on this… | 0 | 616 | 190 | 2 | so fast shrink is unavailable. |  |
| EN | MainWindow.axaml.cs/NoHardwareTipEnglish · No usable hardware encoder was found on this… | 1 | 534 | 108 | 2 | faster than the CPU. |  |
| TR | MainWindow.axaml.cs/NoHardwareTipEnglish · No usable hardware encoder was found on this… | 0 | 734 | 308 | 2 | bulunamadı, bu yüzden hızlı düşürme kullanılamıyor. |  |
