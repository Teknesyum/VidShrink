# İpucu satır genişlikleri

Bu dosyayı `TipOverflowTests` üretir, elle yazılmaz. Yeniden üretmek için:

```
dotnet test VidShrink.sln -c Release --filter TipOverflowTests
```

Ölçüm uygulamanın kendi yazı tipiyle yapılır (Atkinson Hyperlegible Next,
16 px). Tavan `Themes/Theme.axaml` belirteçlerinden
hesaplanır: `TooltipMaxWidth` eksi iki yanın dolgusu ve kenarlığı = **426 px**.

Ölçülen satır: **204** · tavanı aşan: **134** ·
tek kelimeyle aşan: **0**

| Dil | İpucu | Satır | Genişlik | Taşma | Görsel satır | Alt satır | Tek kelime |
| --- | --- | ---: | ---: | ---: | ---: | --- | :-: |
| EN | main.target.tip · The target is a hard ceiling: VidShrink neve… | 0 | 580 | 154 | 2 | File Larger Than This. |  |
| EN | main.target.tip · The target is a hard ceiling: VidShrink neve… | 1 | 992 | 566 | 3 | Weaker Than This One. |  |
| EN | main.target.tip · The target is a hard ceiling: VidShrink neve… | 2 | 550 | 124 | 2 | Instead Of WhatsApp's. |  |
| EN | main.target.tip · The target is a hard ceiling: VidShrink neve… | 3 | 554 | 128 | 2 | Forums And Chat Apps. |  |
| TR | main.target.tip · The target is a hard ceiling: VidShrink neve… | 0 | 517 | 91 | 2 | Dosya Vermez. |  |
| TR | main.target.tip · The target is a hard ceiling: VidShrink neve… | 1 | 1111 | 685 | 3 | Kodlayıcısı Buradakinden Çok Daha Zayıftır. |  |
| TR | main.target.tip · The target is a hard ceiling: VidShrink neve… | 2 | 515 | 89 | 2 | Kalitesini Korur. |  |
| TR | main.target.tip · The target is a hard ceiling: VidShrink neve… | 3 | 643 | 217 | 2 | ve Sohbet Uygulamalarına Uyar. |  |
| EN | main.chip.whatsapp.tip · WhatsApp re-encodes in-chat video with its o… | 0 | 514 | 88 | 2 | bitrate Encoder. |  |
| EN | main.chip.whatsapp.tip · WhatsApp re-encodes in-chat video with its o… | 1 | 1007 | 581 | 3 | Quality, Not WhatsApp's. |  |
| EN | main.chip.whatsapp.tip · WhatsApp re-encodes in-chat video with its o… | 2 | 628 | 202 | 2 | Document Instead Of As A Video. |  |
| TR | main.chip.whatsapp.tip · WhatsApp re-encodes in-chat video with its o… | 0 | 586 | 160 | 2 | Kodlayıcısıyla Yeniden Kodlar. |  |
| TR | main.chip.whatsapp.tip · WhatsApp re-encodes in-chat video with its o… | 1 | 988 | 562 | 3 | VidShrink'in Kalitesi Olur. |  |
| TR | main.chip.whatsapp.tip · WhatsApp re-encodes in-chat video with its o… | 2 | 672 | 246 | 2 | Olarak Değil Belge Olarak Gönderin. |  |
| EN | main.chip.8.tip · 8 MB fits Discord without Nitro, older forum… | 0 | 528 | 102 | 2 | mail Gateways. |  |
| TR | main.chip.8.tip · 8 MB fits Discord without Nitro, older forum… | 0 | 541 | 115 | 2 | Geçitlerine Uyar. |  |
| EN | main.chip.25.tip · 25 MB fits Gmail attachments, Discord Nitro… | 0 | 543 | 117 | 2 | Most Ticket Systems. |  |
| TR | main.chip.25.tip · 25 MB fits Gmail attachments, Discord Nitro… | 0 | 506 | 80 | 2 | Sistemine Uyar. |  |
| EN | main.chip.100.tip · 100 MB suits archiving and uploads where qua… | 0 | 603 | 177 | 2 | More Than Transfer Time. |  |
| TR | main.chip.100.tip · 100 MB suits archiving and uploads where qua… | 0 | 602 | 176 | 2 | ve Yüklemelere Uygundur. |  |
| EN | main.chip.128.tip · 128 MiB is the measured ceiling of uguu.se,… | 0 | 712 | 286 | 2 | Anonymous Share Target With The Smallest Limit. |  |
| EN | main.chip.128.tip · 128 MiB is the measured ceiling of uguu.se,… | 1 | 648 | 222 | 2 | Share Target Without Being Refused. |  |
| EN | main.chip.128.tip · 128 MiB is the measured ceiling of uguu.se,… | 2 | 641 | 215 | 2 | Chip Is The Safe Number For Both. |  |
| TR | main.chip.128.tip · 128 MiB is the measured ceiling of uguu.se,… | 0 | 600 | 174 | 2 | uguu.se'nin Ölçülmüş Tavanıdır. |  |
| TR | main.chip.128.tip · 128 MiB is the measured ceiling of uguu.se,… | 1 | 637 | 211 | 2 | de Geri Çevrilmeden Verilebilir. |  |
| TR | main.chip.128.tip · 128 MiB is the measured ceiling of uguu.se,… | 2 | 694 | 268 | 2 | Bu Yonga İkisi İçin de Güvenli Sayıdır. |  |
| EN | main.chip.180.tip · On the phone: 16 MB in chat, 2 GB as a docum… | 1 | 666 | 240 | 2 | This, WhatsApp Does Not Publish It. |  |
| EN | main.chip.180.tip · On the phone: 16 MB in chat, 2 GB as a docum… | 2 | 475 | 49 | 2 | As It Is. |  |
| TR | main.chip.180.tip · On the phone: 16 MB in chat, 2 GB as a docum… | 1 | 619 | 193 | 2 | Bildirimi, WhatsApp Yayımlamıyor. |  |
| TR | main.chip.180.tip · On the phone: 16 MB in chat, 2 GB as a docum… | 2 | 488 | 62 | 2 | Gibi Geçer. |  |
| EN | main.chip.half.tip · Half of the source size. | 1 | 621 | 195 | 2 | Resolution And Frame Rate. |  |
| TR | main.chip.half.tip · Half of the source size. | 1 | 632 | 206 | 2 | Çözünürlüğü ve Kare Hızını Korur. |  |
| EN | main.intent.tip · Intent sets how early the engine stops spend… | 1 | 660 | 234 | 2 | Leave A Lot Of The Target Unused. |  |
| EN | main.intent.tip · Intent sets how early the engine stops spend… | 2 | 726 | 300 | 2 | Noticing — The Right Choice For WhatsApp. |  |
| EN | main.intent.tip · Intent sets how early the engine stops spend… | 3 | 620 | 194 | 2 | Re-encode The File Anyway. |  |
| TR | main.intent.tip · Intent sets how early the engine stops spend… | 1 | 736 | 310 | 2 | Hedefin Büyük Kısmını Kullanmadan Bırakabilir. |  |
| TR | main.intent.tip · Intent sets how early the engine stops spend… | 2 | 772 | 346 | 2 | Noktada Durur — WhatsApp İçin Doğru Seçim Budur. |  |
| TR | main.intent.tip · Intent sets how early the engine stops spend… | 3 | 638 | 212 | 2 | Dosyayı Zaten Yeniden Kodlayacaktır. |  |
| EN | main.codec.tip · H.264 is universal: every phone made in the… | 0 | 781 | 355 | 2 | Years Plays It, And WhatsApp Never Re-encodes It. |  |
| EN | main.codec.tip · H.264 is universal: every phone made in the… | 1 | 1213 | 787 | 3 | Older Android Phones And Some Web Players Refuse It. |  |
| EN | main.codec.tip · H.264 is universal: every phone made in the… | 2 | 1004 | 578 | 3 | The Compatibility Risk. |  |
| EN | main.codec.tip · H.264 is universal: every phone made in the… | 3 | 751 | 325 | 2 | (GPU) Below To Let The Graphics Card Encode. |  |
| TR | main.codec.tip · H.264 is universal: every phone made in the… | 0 | 740 | 314 | 2 | Oynatır ve WhatsApp Onu Yeniden Kodlamaz. |  |
| TR | main.codec.tip · H.264 is universal: every phone made in the… | 1 | 1218 | 792 | 3 | Android Telefonlar ve Bazı Web Oynatıcılar Kabul Etmez. |  |
| TR | main.codec.tip · H.264 is universal: every phone made in the… | 2 | 693 | 267 | 2 | Uyumluluk Riskini Aştığında H.265'i Seçer. |  |
| TR | main.codec.tip · H.264 is universal: every phone made in the… | 3 | 877 | 451 | 3 | Seçeneğini Açın. |  |
| EN | main.allow.resolution.tip · When the target is tight, fewer pixels encod… | 0 | 958 | 532 | 3 | Than Blocking. |  |
| EN | main.allow.resolution.tip · When the target is tight, fewer pixels encod… | 1 | 1100 | 674 | 3 | Resolution The Budget Can Still Hold Cleanly. |  |
| EN | main.allow.resolution.tip · When the target is tight, fewer pixels encod… | 2 | 910 | 484 | 3 | Always The Better Trade. |  |
| TR | main.allow.resolution.tip · When the target is tight, fewer pixels encod… | 0 | 935 | 509 | 3 | Rahatsız Eder. |  |
| TR | main.allow.resolution.tip · When the target is tight, fewer pixels encod… | 1 | 1026 | 600 | 3 | Taşıyabileceği En Büyük Çözünürlüğü Seçer. |  |
| TR | main.allow.resolution.tip · When the target is tight, fewer pixels encod… | 2 | 922 | 496 | 3 | İyi Bir Takastır. |  |
| EN | main.allow.fps.tip · Halving the frame rate frees bits for the fr… | 1 | 767 | 341 | 2 | Drops Below A Level Where Motion Starts To Stutter. |  |
| EN | main.allow.fps.tip · Halving the frame rate frees bits for the fr… | 2 | 727 | 301 | 2 | The Cost Is Smoothness, Not Compatibility. |  |
| TR | main.allow.fps.tip · Halving the frame rate frees bits for the fr… | 1 | 746 | 320 | 2 | Takılmaya Başladığı Seviyenin Altına Asla İnmez. |  |
| TR | main.allow.fps.tip · Halving the frame rate frees bits for the fr… | 2 | 591 | 165 | 2 | Akıcılıktır, Uyumluluk Değil. |  |
| EN | main.fast-gpu.tip · Graphics cards encode many times faster than… | 1 | 1208 | 782 | 3 | Encoder's Quality At About Seven Times The Speed. |  |
| EN | main.fast-gpu.tip · Graphics cards encode many times faster than… | 2 | 579 | 153 | 2 | Quality Per Megabyte. |  |
| TR | main.fast-gpu.tip · Graphics cards encode many times faster than… | 1 | 1147 | 721 | 3 | Neredeyse Aynı Kaliteyi Yaklaşık Yedi Kat Hızlı Verir. |  |
| TR | main.fast-gpu.tip · Graphics cards encode many times faster than… | 2 | 600 | 174 | 2 | Başına Bir Miktar Kalitedir. |  |
| EN | main.fill.tip · Fill target lands close to the target size a… | 0 | 697 | 271 | 2 | Out The Best Quality The Budget Allows. |  |
| EN | main.fill.tip · Fill target lands close to the target size a… | 1 | 780 | 354 | 2 | Improving: No Padding, But The File Can Come Out Smaller. |  |
| TR | main.fill.tip · Fill target lands close to the target size a… | 0 | 608 | 182 | 2 | Verdiği En İyi Kaliteyi Sıkar. |  |
| TR | main.fill.tip · Fill target lands close to the target size a… | 1 | 789 | 363 | 2 | Dosyayı Şişirmez Ama Belirgin Biçimde Küçük Kalabilir. |  |
| EN | main.hdr.tip · Preserving HDR keeps the source's wider colo… | 0 | 1088 | 662 | 3 | Recent Devices And Apps Play It Correctly. |  |
| EN | main.hdr.tip · Preserving HDR keeps the source's wider colo… | 1 | 767 | 341 | 2 | Range — Smaller, Safe On WhatsApp And Any Phone. |  |
| TR | main.hdr.tip · Preserving HDR keeps the source's wider colo… | 0 | 1172 | 746 | 3 | Yalnızca Yeni Cihazlar ve Uygulamalar Doğru Oynatır. |  |
| TR | main.hdr.tip · Preserving HDR keeps the source's wider colo… | 1 | 822 | 396 | 2 | Eder — Daha Küçük ve WhatsApp ile Her Telefonda Güvenli. |  |
| EN | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 0 | 1185 | 759 | 3 | This Machine And This File, Not From A Preset Table. |  |
| EN | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 1 | 1117 | 691 | 3 | And How Much Less Is Not Measured. |  |
| EN | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 2 | 831 | 405 | 2 | Encoded With, The Time Is Left Blank Instead Of Guessed. |  |
| TR | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 0 | 1150 | 724 | 3 | Tablosundan Değil, Bu Makineden ve Bu Dosyadan Gelir. |  |
| TR | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 1 | 1060 | 634 | 3 | Ne Kadar Ucuz Olduğu İse Ölçülmez. |  |
| TR | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 2 | 615 | 189 | 2 | Tahmin Edilmez, Boş Bırakılır. |  |
| EN | main.output.estimated-output.tip · Before planning, VidShrink encodes short sam… | 0 | 1202 | 776 | 3 | Needs — It Does Not Guess From The Source Bitrate. |  |
| EN | main.output.estimated-output.tip · Before planning, VidShrink encodes short sam… | 1 | 728 | 302 | 2 | Narrow Range Rather Than A Rule Of Thumb. |  |
| EN | main.output.estimated-output.tip · Before planning, VidShrink encodes short sam… | 2 | 1194 | 768 | 3 | Spending The Rest Would Buy Nothing You Could See. |  |
| TR | main.output.estimated-output.tip · Before planning, VidShrink encodes short sam… | 0 | 1223 | 797 | 3 | Gerektiğini Ölçer — Kaynak Bit Hızından Tahmin Yürütmez. |  |
| TR | main.output.estimated-output.tip · Before planning, VidShrink encodes short sam… | 1 | 638 | 212 | 2 | Bir Sayı Olmasının Sebebi Budur. |  |
| TR | main.output.estimated-output.tip · Before planning, VidShrink encodes short sam… | 2 | 1083 | 657 | 3 | Harcamak Gözle Görülür Bir Şey Satın Almaz. |  |
| EN | main.convert.container.tip · The container is the file type. | 1 | 890 | 464 | 3 | Phone Opens It. |  |
| EN | main.convert.container.tip · The container is the file type. | 2 | 716 | 290 | 2 | Most Phone Galleries Treat It As A Document. |  |
| EN | main.convert.container.tip · The container is the file type. | 4 | 587 | 161 | 2 | Android Support Is Uneven. |  |
| TR | main.convert.container.tip · The container is the file type. | 1 | 971 | 545 | 3 | Biçimdir — Her Telefon Açar. |  |
| TR | main.convert.container.tip · The container is the file type. | 2 | 670 | 244 | 2 | Çoğu Telefon Galerisi Onu Belge Sayar. |  |
| TR | main.convert.container.tip · The container is the file type. | 4 | 626 | 200 | 2 | Android Desteği İse Düzensizdir. |  |
| EN | main.convert.video-codec.tip · H.264 plays on nearly every device made and… | 0 | 808 | 382 | 2 | WhatsApp Expects; Pick It When The File Must Just Work. |  |
| EN | main.convert.video-codec.tip · H.264 plays on nearly every device made and… | 1 | 787 | 361 | 2 | And Every Phone Since 2016 Decodes It In Hardware. |  |
| EN | main.convert.video-codec.tip · H.264 plays on nearly every device made and… | 2 | 818 | 392 | 2 | Players Will Not Open It, And WhatsApp May Re-encode It. |  |
| EN | main.convert.video-codec.tip · H.264 plays on nearly every device made and… | 3 | 620 | 194 | 2 | Apps But Rarely In The Gallery. |  |
| EN | main.convert.video-codec.tip · H.264 plays on nearly every device made and… | 4 | 638 | 212 | 2 | Only Recent Phones Decode It. |  |
| EN | main.convert.video-codec.tip · H.264 plays on nearly every device made and… | 5 | 801 | 375 | 2 | Loss, No Waiting — Whenever The Container Accepts It. |  |
| TR | main.convert.video-codec.tip · H.264 plays on nearly every device made and… | 0 | 944 | 518 | 3 | Gerekiyorsa Bunu Seçin. |  |
| TR | main.convert.video-codec.tip · H.264 plays on nearly every device made and… | 1 | 747 | 321 | 2 | ve 2016 Sonrası Her Telefon Donanımda Çözer. |  |
| TR | main.convert.video-codec.tip · H.264 plays on nearly every device made and… | 2 | 785 | 359 | 2 | Oynatıcı Açmaz, WhatsApp da Yeniden Kodlayabilir. |  |
| TR | main.convert.video-codec.tip · H.264 plays on nearly every device made and… | 3 | 661 | 235 | 2 | Uygulamalarda Oynatır Ama Galeride Nadiren. |  |
| TR | main.convert.video-codec.tip · H.264 plays on nearly every device made and… | 4 | 565 | 139 | 2 | Yeni Telefonlar Çözer. |  |
| TR | main.convert.video-codec.tip · H.264 plays on nearly every device made and… | 5 | 795 | 369 | 2 | Kalite Kaybı ve Bekleme Olmadan Olduğu Gibi Korur. |  |
| EN | main.convert.quality-mode.tip · CRF targets visual quality, so final size ca… | 2 | 674 | 248 | 2 | When Size Or Bandwidth Matters More. |  |
| TR | main.convert.quality-mode.tip · CRF targets visual quality, so final size ca… | 1 | 598 | 172 | 2 | Daha Öngörülebilir Yapar. |  |
| TR | main.convert.quality-mode.tip · CRF targets visual quality, so final size ca… | 2 | 700 | 274 | 2 | Daha Önemliyse Sabit Bit Hızı Kullanın. |  |
| EN | main.convert.crf-label.tip · In CRF mode, a lower number means higher qua… | 0 | 788 | 362 | 2 | A Larger File; 23 Is A Common H.264 Starting Point. |  |
| EN | main.convert.crf-label.tip · In CRF mode, a lower number means higher qua… | 1 | 728 | 302 | 2 | Value Gives More Quality And A Larger File. |  |
| TR | main.convert.crf-label.tip · In CRF mode, a lower number means higher qua… | 0 | 801 | 375 | 2 | Büyük Dosya Demektir; 23, H.264 İçin Yaygın Başlangıçtır. |  |
| TR | main.convert.crf-label.tip · In CRF mode, a lower number means higher qua… | 1 | 830 | 404 | 2 | Yüksek Değer Daha Fazla Kalite ve Daha Büyük Dosya Verir. |  |
| EN | main.convert.resolution.tip · Source keeps the original dimensions. | 1 | 522 | 96 | 2 | Aspect Ratio. |  |
| TR | main.convert.resolution.tip · Source keeps the original dimensions. | 1 | 494 | 68 | 2 | Yüksekliğini Sınırlar. |  |
| EN | main.convert.custom-fps.tip · Used only when frame rate is custom. | 2 | 524 | 98 | 2 | Motion Detail. |  |
| TR | main.convert.custom-fps.tip · Used only when frame rate is custom. | 2 | 536 | 110 | 2 | Ayrıntısı Eklemez. |  |
| EN | main.convert.audio.tip · AAC is the safe pairing for MP4 and the only… | 0 | 810 | 384 | 2 | Codec Every Phone And WhatsApp Take Without Complaint. |  |
| EN | main.convert.audio.tip · AAC is the safe pairing for MP4 and the only… | 1 | 744 | 318 | 2 | Inside MP4 It Will Not Play On Many Phones. |  |
| EN | main.convert.audio.tip · AAC is the safe pairing for MP4 and the only… | 4 | 642 | 216 | 2 | When The Container Supports It. |  |
| EN | main.convert.audio.tip · AAC is the safe pairing for MP4 and the only… | 5 | 688 | 262 | 2 | When You Are Squeezing A Silent Clip. |  |
| TR | main.convert.audio.tip · AAC is the safe pairing for MP4 and the only… | 0 | 745 | 319 | 2 | WhatsApp'ın Sorunsuz İşlediği Tek Ses Kodeğidir. |  |
| TR | main.convert.audio.tip · AAC is the safe pairing for MP4 and the only… | 1 | 735 | 309 | 2 | WebM'dir; MP4 İçinde Birçok Telefonda Oynamaz. |  |
| TR | main.convert.audio.tip · AAC is the safe pairing for MP4 and the only… | 4 | 541 | 115 | 2 | Kodlamadan Korur. |  |
| TR | main.convert.audio.tip · AAC is the safe pairing for MP4 and the only… | 5 | 532 | 106 | 2 | Doğru Seçim Budur. |  |
| EN | main.convert.audio-bitrate.tip · Audio data rate in kilobits per second. | 1 | 700 | 274 | 2 | Detail, And 256 Or 320 Is Useful For Music. |  |
| TR | main.convert.audio-bitrate.tip · Audio data rate in kilobits per second. | 1 | 683 | 257 | 2 | 256 veya 320 Müzik İçin Kullanışlıdır. |  |
| TR | main.convert.audio-bitrate.tip · Audio data rate in kilobits per second. | 2 | 528 | 102 | 2 | Değer Kullanılmaz. |  |
| EN | settings.share.tip · The share target is the service a finished f… | 0 | 459 | 33 | 2 | Uploaded To. |  |
| EN | settings.share.tip · The share target is the service a finished f… | 1 | 751 | 325 | 2 | The File Again, So A Link Can Be Closed Early. |  |
| EN | settings.share.tip · The share target is the service a finished f… | 2 | 960 | 534 | 3 | Close The Link Early. |  |
| TR | settings.share.tip · The share target is the service a finished f… | 1 | 769 | 343 | 2 | Silmesine İzin Verir, Bağlantı Erken Kapatılabilir. |  |
| TR | settings.share.tip · The share target is the service a finished f… | 2 | 796 | 370 | 2 | Ama Silme Jetonu Vermez; Bağlantı Erken Kapatılamaz. |  |
| EN | main.ai.tip · This step is optional. | 1 | 530 | 104 | 2 | Its JSON Answer. |  |
| TR | main.ai.tip · This step is optional. | 1 | 653 | 227 | 2 | JSON Yanıtını Yapıştırıp Doğrulayın. |  |
| EN | main.fast-gpu.tip-missing · No usable hardware encoder was found on this… | 0 | 649 | 223 | 2 | Computer, So Fast Shrink Is Unavailable. |  |
| EN | main.fast-gpu.tip-missing · No usable hardware encoder was found on this… | 1 | 557 | 131 | 2 | Faster Than The CPU. |  |
| TR | main.fast-gpu.tip-missing · No usable hardware encoder was found on this… | 0 | 749 | 323 | 2 | Bulunamadı, Bu Yüzden Hızlı Düşürme Kullanılamıyor. |  |
| EN | settings.update.auto-effect · When this is off, VidShrink does not update… | 0 | 967 | 541 | 3 | Command That Installs It. |  |
| TR | settings.update.auto-effect · When this is off, VidShrink does not update… | 0 | 804 | 378 | 2 | Bir Sürüm Olduğunu Söyler ve Kuran Komutu Gösterir. |  |
| EN | settings.update.no-self-effect · VidShrink does not update itself on this sys… | 0 | 956 | 530 | 3 | Command That Installs It. |  |
| TR | settings.update.no-self-effect · VidShrink does not update itself on this sys… | 0 | 794 | 368 | 2 | Bir Sürüm Olduğunu Söyler ve Kuran Komutu Gösterir. |  |
