# İpucu satır genişlikleri

Bu dosyayı `TipOverflowTests` üretir, elle yazılmaz. Yeniden üretmek için:

```
dotnet test VidShrink.sln -c Release --filter TipOverflowTests
```

Ölçüm uygulamanın kendi yazı tipiyle yapılır (Atkinson Hyperlegible Next,
16 px). Tavan `Themes/Theme.axaml` belirteçlerinden
hesaplanır: `TooltipMaxWidth` eksi iki yanın dolgusu ve kenarlığı = **746 px**.

Ölçülen satır: **220** · tavanı aşan: **20** ·
tek kelimeyle aşan: **0**

| Dil | İpucu | Satır | Genişlik | Taşma | Görsel satır | Alt satır | Tek kelime |
| --- | --- | ---: | ---: | ---: | ---: | --- | :-: |
| EN | main.chip.whatsapp.tip · WhatsApp re-encodes in-chat video with its o… | 1 | 960 | 214 | 2 | VidShrink's quality, not WhatsApp's. |  |
| TR | main.chip.whatsapp.tip · WhatsApp re-encodes in-chat video with its o… | 1 | 957 | 211 | 2 | değil VidShrink'in kalitesi olur. |  |
| EN | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 0 | 1116 | 370 | 2 | from this machine and this file, not from a preset table. |  |
| EN | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 1 | 1057 | 311 | 2 | second, and how much less is not measured. |  |
| TR | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 0 | 1115 | 369 | 2 | tablosundan değil, bu makineden ve bu dosyadan gelir. |  |
| TR | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 1 | 1019 | 273 | 2 | olur, ne kadar ucuz olduğu ise ölçülmez. |  |
| EN | main.output.estimated-output.tip · Before planning, VidShrink encodes short sam… | 6 | 847 | 101 | 2 | you could see. |  |
| TR | main.codec.tip · H.264 plays on nearly every device and is wh… | 0 | 919 | 173 | 2 | çalışması gerekiyorsa bunu seçin. |  |
| EN | main.convert.container.tip · The container is the file type. | 1 | 851 | 105 | 2 | phone opens it. |  |
| TR | main.convert.container.tip · The container is the file type. | 1 | 943 | 197 | 2 | tek biçimdir — her telefon açar. |  |
| EN | main.convert.video-codec.tip · H.264 plays on nearly every device and is wh… | 2 | 786 | 40 | 2 | encode it. |  |
| TR | main.convert.video-codec.tip · H.264 plays on nearly every device and is wh… | 0 | 919 | 173 | 2 | çalışması gerekiyorsa bunu seçin. |  |
| TR | main.convert.crf-label.tip · In CRF mode, a lower number means higher qua… | 1 | 803 | 57 | 2 | dosya verir. |  |
| EN | settings-tab.ffmpeg-path.hint · Manual uses the ffmpeg you choose instead of… | 0 | 967 | 221 | 2 | ffprobe must be in the same folder. |  |
| TR | settings-tab.ffmpeg-path.hint · Manual uses the ffmpeg you choose instead of… | 0 | 898 | 152 | 2 | ffprobe aynı klasörde olmalı. |  |
| EN | settings-tab.opensubtitles.hint · Subtitle download in the player uses your ow… | 0 | 1153 | 407 | 2 | then paste the key here. Without a key the feature stays off. |  |
| TR | settings-tab.opensubtitles.hint · Subtitle download in the player uses your ow… | 0 | 1282 | 536 | 2 | oluşturun, ardından anahtarı buraya yapıştırın. Anahtar yoksa özellik kapalı kalır. |  |
| EN | settings-tab.opensubtitles.accounthint · The password is only sent to OpenSubtitles t… | 0 | 1452 | 706 | 2 | comes back is stored, protected by Windows; on other systems the session lasts until you close VidShrink. |  |
| TR | settings-tab.opensubtitles.accounthint · The password is only sent to OpenSubtitles t… | 0 | 1341 | 595 | 2 | belirtecidir; Windows onu korur, başka sistemlerde oturum VidShrink kapanınca biter. |  |
| EN | settings.share.tip · The share target is the service a finished f… | 2 | 912 | 166 | 2 | can close the link early. |  |
