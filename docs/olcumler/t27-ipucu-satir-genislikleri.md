# İpucu satır genişlikleri

Bu dosyayı `TipOverflowTests` üretir, elle yazılmaz. Yeniden üretmek için:

```
dotnet test VidShrink.sln -c Release --filter TipOverflowTests
```

Ölçüm uygulamanın kendi yazı tipiyle yapılır (Atkinson Hyperlegible Next,
16 px). Tavan `Themes/Theme.axaml` belirteçlerinden
hesaplanır: `TooltipMaxWidth` eksi iki yanın dolgusu ve kenarlığı = **746 px**.

Ölçülen satır: **210** · tavanı aşan: **60** ·
tek kelimeyle aşan: **18**

| Dil | İpucu | Satır | Genişlik | Taşma | Görsel satır | Alt satır | Tek kelime |
| --- | --- | ---: | ---: | ---: | ---: | --- | :-: |
| EN | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 1 | 992 | 246 | 2 | Encoder Is Far Weaker Than This One. |  |
| TR | MainWindow.axaml · The target is a hard ceiling: VidShrink neve… | 1 | 1111 | 365 | 2 | ve Kendi Kodlayıcısı Buradakinden Çok Daha Zayıftır. |  |
| EN | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 1 | 1007 | 261 | 2 | Is VidShrink's Quality, Not WhatsApp's. |  |
| TR | MainWindow.axaml · WhatsApp re-encodes in-chat video with its o… | 1 | 988 | 242 | 2 | WhatsApp'ın Değil VidShrink'in Kalitesi Olur. |  |
| TR | MainWindow.axaml · Intent sets how early the engine stops spend… | 2 | 772 | 26 | 2 | Budur. | evet |
| EN | MainWindow.axaml · H.264 is universal: every phone made in the… | 0 | 781 | 35 | 2 | encodes It. |  |
| EN | MainWindow.axaml · H.264 is universal: every phone made in the… | 1 | 1213 | 467 | 2 | But Some Older Android Phones And Some Web Players Refuse It. |  |
| EN | MainWindow.axaml · H.264 is universal: every phone made in the… | 2 | 1004 | 258 | 2 | Gain Outweighs The Compatibility Risk. |  |
| EN | MainWindow.axaml · H.264 is universal: every phone made in the… | 3 | 751 | 5 | 2 | Encode. | evet |
| TR | MainWindow.axaml · H.264 is universal: every phone made in the… | 1 | 1218 | 472 | 2 | Ancak Bazı Eski Android Telefonlar ve Bazı Web Oynatıcılar Kabul Etmez. |  |
| TR | MainWindow.axaml · H.264 is universal: every phone made in the… | 3 | 877 | 131 | 2 | (GPU) Seçeneğini Açın. |  |
| EN | MainWindow.axaml · When the target is tight, fewer pixels encod… | 0 | 958 | 212 | 2 | Less Objectionable Than Blocking. |  |
| EN | MainWindow.axaml · When the target is tight, fewer pixels encod… | 1 | 1100 | 354 | 2 | Largest Resolution The Budget Can Still Hold Cleanly. |  |
| EN | MainWindow.axaml · When the target is tight, fewer pixels encod… | 2 | 910 | 164 | 2 | Always The Better Trade. |  |
| TR | MainWindow.axaml · When the target is tight, fewer pixels encod… | 0 | 935 | 189 | 2 | Çok Daha Az Rahatsız Eder. |  |
| TR | MainWindow.axaml · When the target is tight, fewer pixels encod… | 1 | 1026 | 280 | 2 | Taşıyabileceği En Büyük Çözünürlüğü Seçer. |  |
| TR | MainWindow.axaml · When the target is tight, fewer pixels encod… | 2 | 922 | 176 | 2 | Zaman Daha İyi Bir Takastır. |  |
| EN | MainWindow.axaml · Halving the frame rate frees bits for the fr… | 1 | 767 | 21 | 2 | Stutter. | evet |
| EN | MainWindow.axaml · Graphics cards encode many times faster than… | 1 | 1208 | 462 | 2 | The Software Encoder's Quality At About Seven Times The Speed. |  |
| TR | MainWindow.axaml · Graphics cards encode many times faster than… | 1 | 1147 | 401 | 2 | Kodlayıcısıyla Neredeyse Aynı Kaliteyi Yaklaşık Yedi Kat Hızlı Verir. |  |
| EN | MainWindow.axaml · Fill target lands close to the target size a… | 1 | 780 | 34 | 2 | Smaller. | evet |
| TR | MainWindow.axaml · Fill target lands close to the target size a… | 1 | 789 | 43 | 2 | Kalabilir. | evet |
| EN | MainWindow.axaml · Preserving HDR keeps the source's wider colo… | 0 | 1088 | 342 | 2 | And Only Recent Devices And Apps Play It Correctly. |  |
| EN | MainWindow.axaml · Preserving HDR keeps the source's wider colo… | 1 | 767 | 21 | 2 | Phone. | evet |
| TR | MainWindow.axaml · Preserving HDR keeps the source's wider colo… | 0 | 1172 | 426 | 2 | Olur ve Yalnızca Yeni Cihazlar ve Uygulamalar Doğru Oynatır. |  |
| TR | MainWindow.axaml · Preserving HDR keeps the source's wider colo… | 1 | 822 | 76 | 2 | Telefonda Güvenli. |  |
| EN | MainWindow.axaml · VidShrink times the sample encodes it alread… | 0 | 1185 | 439 | 2 | Comes From This Machine And This File, Not From A Preset Table. |  |
| EN | MainWindow.axaml · VidShrink times the sample encodes it alread… | 1 | 1117 | 371 | 2 | Than The Second, And How Much Less Is Not Measured. |  |
| EN | MainWindow.axaml · VidShrink times the sample encodes it alread… | 2 | 831 | 85 | 2 | Of Guessed. |  |
| TR | MainWindow.axaml · VidShrink times the sample encodes it alread… | 0 | 1150 | 404 | 2 | Ayar Tablosundan Değil, Bu Makineden ve Bu Dosyadan Gelir. |  |
| TR | MainWindow.axaml · VidShrink times the sample encodes it alread… | 1 | 1060 | 314 | 2 | Mal Olur, Ne Kadar Ucuz Olduğu İse Ölçülmez. |  |
| EN | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 0 | 1202 | 456 | 2 | Many Bits It Needs — It Does Not Guess From The Source Bitrate. |  |
| EN | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 2 | 1194 | 448 | 2 | Because Spending The Rest Would Buy Nothing You Could See. |  |
| TR | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 0 | 1223 | 477 | 2 | Gerçekte Kaç Bit Gerektiğini Ölçer — Kaynak Bit Hızından Tahmin Yürütmez. |  |
| TR | MainWindow.axaml · Before planning, VidShrink encodes short sam… | 2 | 1083 | 337 | 2 | Kalanı Harcamak Gözle Görülür Bir Şey Satın Almaz. |  |
| EN | MainWindow.axaml · The container is the file type. | 1 | 890 | 144 | 2 | Every Phone Opens It. |  |
| TR | MainWindow.axaml · The container is the file type. | 1 | 971 | 225 | 2 | Tek Biçimdir — Her Telefon Açar. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device made and… | 0 | 808 | 62 | 2 | Just Work. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device made and… | 1 | 787 | 41 | 2 | Hardware. | evet |
| EN | MainWindow.axaml · H.264 plays on nearly every device made and… | 2 | 818 | 72 | 2 | Re-encode It. |  |
| EN | MainWindow.axaml · H.264 plays on nearly every device made and… | 5 | 801 | 55 | 2 | Accepts It. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device made and… | 0 | 944 | 198 | 2 | Çalışması Gerekiyorsa Bunu Seçin. |  |
| TR | MainWindow.axaml · H.264 plays on nearly every device made and… | 1 | 747 | 1 | 2 | Çözer. | evet |
| TR | MainWindow.axaml · H.264 plays on nearly every device made and… | 2 | 785 | 39 | 2 | Kodlayabilir. | evet |
| TR | MainWindow.axaml · H.264 plays on nearly every device made and… | 5 | 795 | 49 | 2 | Gibi Korur. |  |
| EN | MainWindow.axaml · In CRF mode, a lower number means higher qua… | 0 | 788 | 42 | 2 | Point. | evet |
| TR | MainWindow.axaml · In CRF mode, a lower number means higher qua… | 0 | 801 | 55 | 2 | Başlangıçtır. | evet |
| TR | MainWindow.axaml · In CRF mode, a lower number means higher qua… | 1 | 830 | 84 | 2 | Dosya Verir. |  |
| EN | MainWindow.axaml · AAC is the safe pairing for MP4 and the only… | 0 | 810 | 64 | 2 | Complaint. | evet |
| EN | MainWindow.axaml · The share target is the service a finished f… | 1 | 751 | 5 | 2 | Early. | evet |
| EN | MainWindow.axaml · The share target is the service a finished f… | 2 | 960 | 214 | 2 | Nobody Can Close The Link Early. |  |
| TR | MainWindow.axaml · The share target is the service a finished f… | 1 | 769 | 23 | 2 | Kapatılabilir. | evet |
| TR | MainWindow.axaml · The share target is the service a finished f… | 2 | 796 | 50 | 2 | Kapatılamaz. | evet |
| EN | MainWindow.axaml.cs/AutoUpdateEffectEnglish · When this is off, VidShrink does not update… | 0 | 967 | 221 | 2 | Shows The Command That Installs It. |  |
| TR | MainWindow.axaml.cs/AutoUpdateEffectEnglish · When this is off, VidShrink does not update… | 0 | 804 | 58 | 2 | Gösterir. | evet |
| EN | MainWindow.axaml.cs/NoSelfUpdateEffectEnglish · VidShrink does not update itself on this sys… | 0 | 956 | 210 | 2 | The Command That Installs It. |  |
| TR | MainWindow.axaml.cs/NoSelfUpdateEffectEnglish · VidShrink does not update itself on this sys… | 0 | 794 | 48 | 2 | Gösterir. | evet |
| EN | MainWindow.axaml.cs/HardwareTipEnglish · Graphics cards encode many times faster than… | 1 | 1208 | 462 | 2 | The Software Encoder's Quality At About Seven Times The Speed. |  |
| TR | MainWindow.axaml.cs/HardwareTipEnglish · Graphics cards encode many times faster than… | 1 | 1147 | 401 | 2 | Kodlayıcısıyla Neredeyse Aynı Kaliteyi Yaklaşık Yedi Kat Hızlı Verir. |  |
| TR | MainWindow.axaml.cs/NoHardwareTipEnglish · No usable hardware encoder was found on this… | 0 | 749 | 3 | 2 | Kullanılamıyor. | evet |
