# İpucu satır genişlikleri

Bu dosyayı `TipOverflowTests` üretir, elle yazılmaz. Yeniden üretmek için:

```
dotnet test VidShrink.sln -c Release --filter TipOverflowTests
```

Ölçüm uygulamanın kendi yazı tipiyle yapılır (Atkinson Hyperlegible Next,
16 px). Tavan `Themes/Theme.axaml` belirteçlerinden
hesaplanır: `TooltipMaxWidth` eksi iki yanın dolgusu ve kenarlığı = **426 px**.

Ölçülen satır: **210** · tavanı aşan: **140** ·
tek kelimeyle aşan: **0**

| Dil | İpucu | Satır | Genişlik | Taşma | Görsel satır | Alt satır | Tek kelime |
| --- | --- | ---: | ---: | ---: | ---: | --- | :-: |
| EN | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 0 | 580 | 154 | 2 | File Larger Than This. |  |
| EN | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 1 | 992 | 566 | 3 | Weaker Than This One. |  |
| EN | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 2 | 550 | 124 | 2 | Instead Of WhatsApp's. |  |
| EN | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 3 | 554 | 128 | 2 | Forums And Chat Apps. |  |
| TR | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 0 | 517 | 91 | 2 | Dosya Vermez. |  |
| TR | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 1 | 1111 | 685 | 3 | Kodlayıcısı Buradakinden Çok Daha Zayıftır. |  |
| TR | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 2 | 515 | 89 | 2 | Kalitesini Korur. |  |
| TR | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 3 | 643 | 217 | 2 | ve Sohbet Uygulamalarına Uyar. |  |
| EN | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 0 | 514 | 88 | 2 | bitrate Encoder. |  |
| EN | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 1 | 1007 | 581 | 3 | Quality, Not WhatsApp's. |  |
| EN | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 2 | 628 | 202 | 2 | Document Instead Of As A Video. |  |
| TR | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 0 | 586 | 160 | 2 | Kodlayıcısıyla Yeniden Kodlar. |  |
| TR | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 1 | 988 | 562 | 3 | VidShrink'in Kalitesi Olur. |  |
| TR | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 2 | 672 | 246 | 2 | Olarak Değil Belge Olarak Gönderin. |  |
| EN | MainWindow.axaml · 8 MB fits Discord without Nitro, older forum… | 0 | 528 | 102 | 2 | mail Gateways. |  |
| TR | MainWindow.axaml · 8 MB fits Discord without Nitro, older forum… | 0 | 541 | 115 | 2 | Geçitlerine Uyar. |  |
| EN | MainWindow.axaml · 25 MB fits Gmail attachments, Discord Nitro… | 0 | 543 | 117 | 2 | Most Ticket Systems. |  |
| TR | MainWindow.axaml · 25 MB fits Gmail attachments, Discord Nitro… | 0 | 506 | 80 | 2 | Sistemine Uyar. |  |
| EN | MainWindow.axaml · 100 MB suits archiving and uploads where qua… | 0 | 603 | 177 | 2 | More Than Transfer Time. |  |
| TR | MainWindow.axaml · 100 MB suits archiving and uploads where qua… | 0 | 602 | 176 | 2 | ve Yüklemelere Uygundur. |  |
| EN | MainWindow.axaml · 128 MiB is the measured ceiling of uguu.se,… | 0 | 712 | 286 | 2 | Anonymous Share Target With The Smallest Limit. |  |
| EN | MainWindow.axaml · 128 MiB is the measured ceiling of uguu.se,… | 1 | 648 | 222 | 2 | Share Target Without Being Refused. |  |
| EN | MainWindow.axaml · 128 MiB is the measured ceiling of uguu.se,… | 2 | 641 | 215 | 2 | Chip Is The Safe Number For Both. |  |
| TR | MainWindow.axaml · 128 MiB is the measured ceiling of uguu.se,… | 0 | 600 | 174 | 2 | uguu.se'nin Ölçülmüş Tavanıdır. |  |
| TR | MainWindow.axaml · 128 MiB is the measured ceiling of uguu.se,… | 1 | 637 | 211 | 2 | de Geri Çevrilmeden Verilebilir. |  |
| TR | MainWindow.axaml · 128 MiB is the measured ceiling of uguu.se,… | 2 | 694 | 268 | 2 | Bu Yonga İkisi İçin de Güvenli Sayıdır. |  |
| EN | MainWindow.axaml · WhatsApp allows 16 MB for media sent in chat… | 0 | 797 | 371 | 2 | For A Document, And Publishes No Limit In Between. |  |
| EN | MainWindow.axaml · WhatsApp allows 16 MB for media sent in chat… | 1 | 763 | 337 | 2 | Goes Through Only When It Is Sent As A Document. |  |
| EN | MainWindow.axaml · WhatsApp allows 16 MB for media sent in chat… | 2 | 711 | 285 | 2 | Opens A File Instead Of Playing A Video. |  |
| TR | MainWindow.axaml · WhatsApp allows 16 MB for media sent in chat… | 0 | 810 | 384 | 2 | GB İzin Verir ve İkisinin Arasında Bir Sınır Yayımlamaz. |  |
| TR | MainWindow.axaml · WhatsApp allows 16 MB for media sent in chat… | 1 | 710 | 284 | 2 | Yalnız Belge Olarak Gönderildiğinde Geçer. |  |
| TR | MainWindow.axaml · WhatsApp allows 16 MB for media sent in chat… | 2 | 727 | 301 | 2 | Karşı Taraf Video Oynatmak Yerine Dosya Açar. |  |
| EN | MainWindow.axaml · Half of the source size. | 1 | 621 | 195 | 2 | Resolution And Frame Rate. |  |
| TR | MainWindow.axaml · Half of the source size. | 1 | 632 | 206 | 2 | Çözünürlüğü ve Kare Hızını Korur. |  |
| EN | MainWindow.axaml · Intent sets how early the engine stops spend… | 1 | 660 | 234 | 2 | Leave A Lot Of The Target Unused. |  |
| EN | MainWindow.axaml · Intent sets how early the engine stops spend… | 2 | 726 | 300 | 2 | Noticing — The Right Choice For WhatsApp. |  |
| EN | MainWindow.axaml · Intent sets how early the engine stops spend… | 3 | 620 | 194 | 2 | Re-encode The File Anyway. |  |
| TR | MainWindow.axaml · Intent sets how early the engine stops spend… | 1 | 736 | 310 | 2 | Hedefin Büyük Kısmını Kullanmadan Bırakabilir. |  |
| TR | MainWindow.axaml · Intent sets how early the engine stops spend… | 2 | 772 | 346 | 2 | Noktada Durur — WhatsApp İçin Doğru Seçim Budur. |  |
| TR | MainWindow.axaml · Intent sets how early the engine stops spend… | 3 | 638 | 212 | 2 | Dosyayı Zaten Yeniden Kodlayacaktır. |  |
| EN | MainWindow.axaml · H.264 is universal: every phone made in the… | 0 | 781 | 355 | 2 | Years Plays It, And WhatsApp Never Re-encodes It. |  |
| EN | MainWindow.axaml · H.264 is universal: every phone made in the… | 1 | 1213 | 787 | 3 | Older Android Phones And Some Web Players Refuse It. |  |
| EN | MainWindow.axaml · H.264 is universal: every phone made in the… | 2 | 1004 | 578 | 3 | The Compatibility Risk. |  |
| EN | MainWindow.axaml · H.264 is universal: every phone made in the… | 3 | 751 | 325 | 2 | (GPU) Below To Let The Graphics Card Encode. |  |
| TR | MainWindow.axaml · H.264 is universal: every phone made in the… | 0 | 740 | 314 | 2 | Oynatır ve WhatsApp Onu Yeniden Kodlamaz. |  |
| TR | MainWindow.axaml · H.264 is universal: every phone made in the… | 1 | 1218 | 792 | 3 | Android Telefonlar ve Bazı Web Oynatıcılar Kabul Etmez. |  |
| TR | MainWindow.axaml · H.264 is universal: every phone made in the… | 2 | 693 | 267 | 2 | Uyumluluk Riskini Aştığında H.265'i Seçer. |  |
| TR | MainWindow.axaml · H.264 is universal: every phone made in the… | 3 | 877 | 451 | 3 | Seçeneğini Açın. |  |
| EN | MainWindow.axaml · When the target is tight, fewer pixels encod… | 0 | 958 | 532 | 3 | Than Blocking. |  |
| EN | MainWindow.axaml · When the target is tight, fewer pixels encod… | 1 | 1100 | 674 | 3 | Resolution The Budget Can Still Hold Cleanly. |  |
| EN | MainWindow.axaml · When the target is tight, fewer pixels encod… | 2 | 910 | 484 | 3 | Always The Better Trade. |  |
| TR | MainWindow.axaml · When the target is tight, fewer pixels encod… | 0 | 935 | 509 | 3 | Rahatsız Eder. |  |
| TR | MainWindow.axaml · When the target is tight, fewer pixels encod… | 1 | 1026 | 600 | 3 | Taşıyabileceği En Büyük Çözünürlüğü Seçer. |  |
| TR | MainWindow.axaml · When the target is tight, fewer pixels encod… | 2 | 922 | 496 | 3 | İyi Bir Takastır. |  |
| EN | MainWindow.axaml · Halving the frame rate frees bits for the fr… | 1 | 767 | 341 | 2 | Drops Below A Level Where Motion Starts To Stutter. |  |
| EN | MainWindow.axaml · Halving the frame rate frees bits for the fr… | 2 | 727 | 301 | 2 | The Cost Is Smoothness, Not Compatibility. |  |
| TR | MainWindow.axaml · Halving the frame rate frees bits for the fr… | 1 | 746 | 320 | 2 | Takılmaya Başladığı Seviyenin Altına Asla İnmez. |  |
| TR | MainWindow.axaml · Halving the frame rate frees bits for the fr… | 2 | 591 | 165 | 2 | Akıcılıktır, Uyumluluk Değil. |  |
| EN | MainWindow.axaml · Graphics cards encode many times faster than… | 1 | 1208 | 782 | 3 | Encoder's Quality At About Seven Times The Speed. |  |
| EN | MainWindow.axaml · Graphics cards encode many times faster than… | 2 | 579 | 153 | 2 | Quality Per Megabyte. |  |
| TR | MainWindow.axaml · Graphics cards encode many times faster than… | 1 | 1147 | 721 | 3 | Neredeyse Aynı Kaliteyi Yaklaşık Yedi Kat Hızlı Verir. |  |
| TR | MainWindow.axaml · Graphics cards encode many times faster than… | 2 | 600 | 174 | 2 | Başına Bir Miktar Kalitedir. |  |
| EN | MainWindow.axaml · Fill target lands close to the target size a… | 0 | 697 | 271 | 2 | Out The Best Quality The Budget Allows. |  |
| EN | MainWindow.axaml · Fill target lands close to the target size a… | 1 | 780 | 354 | 2 | Improving: No Padding, But The File Can Come Out Smaller. |  |
| TR | MainWindow.axaml · Fill target lands close to the target size a… | 0 | 608 | 182 | 2 | Verdiği En İyi Kaliteyi Sıkar. |  |
| TR | MainWindow.axaml · Fill target lands close to the target size a… | 1 | 789 | 363 | 2 | Dosyayı Şişirmez Ama Belirgin Biçimde Küçük Kalabilir. |  |
| EN | MainWindow.axaml · Preserving HDR keeps the source's wider colo… | 0 | 1088 | 662 | 3 | Recent Devices And Apps Play It Correctly. |  |
| EN | MainWindow.axaml · Preserving HDR keeps the source's wider colo… | 1 | 767 | 341 | 2 | Range — Smaller, Safe On WhatsApp And Any Phone. |  |
| TR | MainWindow.axaml · Preserving HDR keeps the source's wider colo… | 0 | 1172 | 746 | 3 | Yalnızca Yeni Cihazlar ve Uygulamalar Doğru Oynatır. |  |
| TR | MainWindow.axaml · Preserving HDR keeps the source's wider colo… | 1 | 822 | 396 | 2 | Eder — Daha Küçük ve WhatsApp ile Her Telefonda Güvenli. |  |
| EN | MainWindow.axaml · VidShrink times the sample encodes it alread… | 0 | 1185 | 759 | 3 | This Machine And This File, Not From A Preset Table. |  |
| EN | MainWindow.axaml · VidShrink times the sample encodes it alread… | 1 | 1117 | 691 | 3 | And How Much Less Is Not Measured. |  |
| EN | MainWindow.axaml · VidShrink times the sample encodes it alread… | 2 | 831 | 405 | 2 | Encoded With, The Time Is Left Blank Instead Of Guessed. |  |
| TR | MainWindow.axaml · VidShrink times the sample encodes it alread… | 0 | 1150 | 724 | 3 | Tablosundan Değil, Bu Makineden ve Bu Dosyadan Gelir. |  |
| TR | MainWindow.axaml · VidShrink times the sample encodes it alread… | 1 | 1060 | 634 | 3 | Ne Kadar Ucuz Olduğu İse Ölçülmez. |  |
| TR | MainWindow.axaml · VidShrink times the sample encodes it alread… | 2 | 615 | 189 | 2 | Tahmin Edilmez, Boş Bırakılır. |  |
| EN | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 0 | 1202 | 776 | 3 | Needs — It Does Not Guess From The Source Bitrate. |  |
| EN | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 1 | 728 | 302 | 2 | Narrow Range Rather Than A Rule Of Thumb. |  |
| EN | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 2 | 1194 | 768 | 3 | Spending The Rest Would Buy Nothing You Could See. |  |
| TR | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 0 | 1223 | 797 | 3 | Gerektiğini Ölçer — Kaynak Bit Hızından Tahmin Yürütmez. |  |
| TR | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 1 | 638 | 212 | 2 | Bir Sayı Olmasının Sebebi Budur. |  |
| TR | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 2 | 1083 | 657 | 3 | Harcamak Gözle Görülür Bir Şey Satın Almaz. |  |
| EN | MainWindow.axaml · The container is the file type. | 1 | 890 | 464 | 3 | Phone Opens It. |  |
| EN | MainWindow.axaml · The container is the file type. | 2 | 716 | 290 | 2 | Most Phone Galleries Treat It As A Document. |  |
| EN | MainWindow.axaml · The container is the file type. | 4 | 587 | 161 | 2 | Android Support Is Uneven. |  |
| TR | MainWindow.axaml · The container is the file type. | 1 | 971 | 545 | 3 | Biçimdir — Her Telefon Açar. |  |
| TR | MainWindow.axaml · The container is the file type. | 2 | 670 | 244 | 2 | Çoğu Telefon Galerisi Onu Belge Sayar. |  |
| TR | MainWindow.axaml · The container is the file type. | 4 | 626 | 200 | 2 | Android Desteği İse Düzensizdir. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device made and… | 0 | 808 | 382 | 2 | WhatsApp Expects; Pick It When The File Must Just Work. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device made and… | 1 | 787 | 361 | 2 | And Every Phone Since 2016 Decodes It In Hardware. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device made and… | 2 | 818 | 392 | 2 | Players Will Not Open It, And WhatsApp May Re-encode It. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device made and… | 3 | 620 | 194 | 2 | Apps But Rarely In The Gallery. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device made and… | 4 | 638 | 212 | 2 | Only Recent Phones Decode It. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device made and… | 5 | 801 | 375 | 2 | Loss, No Waiting — Whenever The Container Accepts It. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device made and… | 0 | 944 | 518 | 3 | Gerekiyorsa Bunu Seçin. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device made and… | 1 | 747 | 321 | 2 | ve 2016 Sonrası Her Telefon Donanımda Çözer. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device made and… | 2 | 785 | 359 | 2 | Oynatıcı Açmaz, WhatsApp da Yeniden Kodlayabilir. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device made and… | 3 | 661 | 235 | 2 | Uygulamalarda Oynatır Ama Galeride Nadiren. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device made and… | 4 | 565 | 139 | 2 | Yeni Telefonlar Çözer. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device made and… | 5 | 795 | 369 | 2 | Kalite Kaybı ve Bekleme Olmadan Olduğu Gibi Korur. |  |
| EN | MainWindow.axaml · CRF targets visual quality, so final size ca… | 2 | 674 | 248 | 2 | When Size Or Bandwidth Matters More. |  |
| TR | MainWindow.axaml · CRF targets visual quality, so final size ca… | 1 | 598 | 172 | 2 | Daha Öngörülebilir Yapar. |  |
| TR | MainWindow.axaml · CRF targets visual quality, so final size ca… | 2 | 700 | 274 | 2 | Daha Önemliyse Sabit Bit Hızı Kullanın. |  |
| EN | MainWindow.axaml · In CRF mode, a lower number means higher qua… | 0 | 788 | 362 | 2 | A Larger File; 23 Is A Common H.264 Starting Point. |  |
| EN | MainWindow.axaml · In CRF mode, a lower number means higher qua… | 1 | 728 | 302 | 2 | Value Gives More Quality And A Larger File. |  |
| TR | MainWindow.axaml · In CRF mode, a lower number means higher qua… | 0 | 801 | 375 | 2 | Büyük Dosya Demektir; 23, H.264 İçin Yaygın Başlangıçtır. |  |
| TR | MainWindow.axaml · In CRF mode, a lower number means higher qua… | 1 | 830 | 404 | 2 | Yüksek Değer Daha Fazla Kalite ve Daha Büyük Dosya Verir. |  |
| EN | MainWindow.axaml · Source keeps the original dimensions. | 1 | 522 | 96 | 2 | Aspect Ratio. |  |
| TR | MainWindow.axaml · Source keeps the original dimensions. | 1 | 494 | 68 | 2 | Yüksekliğini Sınırlar. |  |
| EN | MainWindow.axaml · Used only when frame rate is custom. | 2 | 524 | 98 | 2 | Motion Detail. |  |
| TR | MainWindow.axaml · Used only when frame rate is custom. | 2 | 536 | 110 | 2 | Ayrıntısı Eklemez. |  |
| EN | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 0 | 810 | 384 | 2 | Codec Every Phone And WhatsApp Take Without Complaint. |  |
| EN | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 1 | 744 | 318 | 2 | Inside MP4 It Will Not Play On Many Phones. |  |
| EN | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 4 | 642 | 216 | 2 | When The Container Supports It. |  |
| EN | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 5 | 688 | 262 | 2 | When You Are Squeezing A Silent Clip. |  |
| TR | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 0 | 745 | 319 | 2 | WhatsApp'ın Sorunsuz İşlediği Tek Ses Kodeğidir. |  |
| TR | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 1 | 735 | 309 | 2 | WebM'dir; MP4 İçinde Birçok Telefonda Oynamaz. |  |
| TR | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 4 | 541 | 115 | 2 | Kodlamadan Korur. |  |
| TR | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 5 | 532 | 106 | 2 | Doğru Seçim Budur. |  |
| EN | MainWindow.axaml · Audio data rate in kilobits per second. | 1 | 700 | 274 | 2 | Detail, And 256 Or 320 Is Useful For Music. |  |
| TR | MainWindow.axaml · Audio data rate in kilobits per second. | 1 | 683 | 257 | 2 | 256 veya 320 Müzik İçin Kullanışlıdır. |  |
| TR | MainWindow.axaml · Audio data rate in kilobits per second. | 2 | 528 | 102 | 2 | Değer Kullanılmaz. |  |
| EN | MainWindow.axaml · The share target is the service a finished f… | 0 | 459 | 33 | 2 | Uploaded To. |  |
| EN | MainWindow.axaml · The share target is the service a finished f… | 1 | 751 | 325 | 2 | The File Again, So A Link Can Be Closed Early. |  |
| EN | MainWindow.axaml · The share target is the service a finished f… | 2 | 960 | 534 | 3 | Close The Link Early. |  |
| TR | MainWindow.axaml · The share target is the service a finished f… | 1 | 769 | 343 | 2 | Silmesine İzin Verir, Bağlantı Erken Kapatılabilir. |  |
| TR | MainWindow.axaml · The share target is the service a finished f… | 2 | 796 | 370 | 2 | Ama Silme Jetonu Vermez; Bağlantı Erken Kapatılamaz. |  |
| EN | Theme.axaml/AiHintText · This step is optional. | 1 | 530 | 104 | 2 | Its JSON Answer. |  |
| TR | Theme.axaml/AiHintText · This step is optional. | 1 | 653 | 227 | 2 | JSON Yanıtını Yapıştırıp Doğrulayın. |  |
| EN | MainWindow.axaml.cs/AutoUpdateEffectEnglish · When this is off, VidShrink does not update… | 0 | 967 | 541 | 3 | Command That Installs It. |  |
| TR | MainWindow.axaml.cs/AutoUpdateEffectEnglish · When this is off, VidShrink does not update… | 0 | 804 | 378 | 2 | Bir Sürüm Olduğunu Söyler ve Kuran Komutu Gösterir. |  |
| EN | MainWindow.axaml.cs/NoSelfUpdateEffectEnglish · VidShrink does not update itself on this sys… | 0 | 956 | 530 | 3 | Command That Installs It. |  |
| TR | MainWindow.axaml.cs/NoSelfUpdateEffectEnglish · VidShrink does not update itself on this sys… | 0 | 794 | 368 | 2 | Bir Sürüm Olduğunu Söyler ve Kuran Komutu Gösterir. |  |
| EN | MainWindow.axaml.cs/HardwareTipEnglish · Graphics cards encode many times faster than… | 1 | 1208 | 782 | 3 | Encoder's Quality At About Seven Times The Speed. |  |
| EN | MainWindow.axaml.cs/HardwareTipEnglish · Graphics cards encode many times faster than… | 2 | 579 | 153 | 2 | Quality Per Megabyte. |  |
| TR | MainWindow.axaml.cs/HardwareTipEnglish · Graphics cards encode many times faster than… | 1 | 1147 | 721 | 3 | Neredeyse Aynı Kaliteyi Yaklaşık Yedi Kat Hızlı Verir. |  |
| TR | MainWindow.axaml.cs/HardwareTipEnglish · Graphics cards encode many times faster than… | 2 | 600 | 174 | 2 | Başına Bir Miktar Kalitedir. |  |
| EN | MainWindow.axaml.cs/NoHardwareTipEnglish · No usable hardware encoder was found on this… | 0 | 649 | 223 | 2 | Computer, So Fast Shrink Is Unavailable. |  |
| EN | MainWindow.axaml.cs/NoHardwareTipEnglish · No usable hardware encoder was found on this… | 1 | 557 | 131 | 2 | Faster Than The CPU. |  |
| TR | MainWindow.axaml.cs/NoHardwareTipEnglish · No usable hardware encoder was found on this… | 0 | 749 | 323 | 2 | Bulunamadı, Bu Yüzden Hızlı Düşürme Kullanılamıyor. |  |
