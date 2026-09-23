# İpucu satır genişlikleri

Bu dosyayı `TipOverflowTests` üretir, elle yazılmaz. Yeniden üretmek için:

```
dotnet test VidShrink.sln -c Release --filter TipOverflowTests
```

Ölçüm uygulamanın kendi yazı tipiyle yapılır (Atkinson Hyperlegible Next,
16 px). Tavan `Themes/Theme.axaml` belirteçlerinden
hesaplanır: `TooltipMaxWidth` eksi iki yanın dolgusu ve kenarlığı = **746 px**.

Ölçülen satır: **192** · tavanı aşan: **14** ·
tek kelimeyle aşan: **0**

| Dil | İpucu | Satır | Genişlik | Taşma | Görsel satır | Alt satır | Tek kelime |
| --- | --- | ---: | ---: | ---: | ---: | --- | :-: |
| EN | main.chip.whatsapp.tip · WhatsApp re-encodes in-chat video with its o… | 1 | 960 | 214 | 2 | VidShrink's quality, not WhatsApp's. |  |
| TR | main.chip.whatsapp.tip · WhatsApp re-encodes in-chat video with its o… | 1 | 957 | 211 | 2 | değil VidShrink'in kalitesi olur. |  |
| TR | main.codec.tip · H.264 plays on nearly every device and is wh… | 0 | 919 | 173 | 2 | çalışması gerekiyorsa bunu seçin. |  |
| EN | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 0 | 1116 | 370 | 2 | from this machine and this file, not from a preset table. |  |
| EN | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 1 | 1057 | 311 | 2 | second, and how much less is not measured. |  |
| TR | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 0 | 1115 | 369 | 2 | tablosundan değil, bu makineden ve bu dosyadan gelir. |  |
| TR | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 1 | 1019 | 273 | 2 | olur, ne kadar ucuz olduğu ise ölçülmez. |  |
| EN | main.output.estimated-output.tip · Before planning, VidShrink encodes short sam… | 6 | 847 | 101 | 2 | you could see. |  |
| EN | main.convert.container.tip · The container is the file type. | 1 | 851 | 105 | 2 | phone opens it. |  |
| TR | main.convert.container.tip · The container is the file type. | 1 | 943 | 197 | 2 | tek biçimdir — her telefon açar. |  |
| EN | main.convert.video-codec.tip · H.264 plays on nearly every device and is wh… | 2 | 786 | 40 | 2 | encode it. |  |
| TR | main.convert.video-codec.tip · H.264 plays on nearly every device and is wh… | 0 | 919 | 173 | 2 | çalışması gerekiyorsa bunu seçin. |  |
| TR | main.convert.crf-label.tip · In CRF mode, a lower number means higher qua… | 1 | 803 | 57 | 2 | dosya verir. |  |
| EN | settings.share.tip · The share target is the service a finished f… | 2 | 912 | 166 | 2 | can close the link early. |  |
