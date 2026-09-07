# İpucu satır genişlikleri

Bu dosyayı `TipOverflowTests` üretir, elle yazılmaz. Yeniden üretmek için:

```
dotnet test VidShrink.sln -c Release --filter TipOverflowTests
```

Ölçüm uygulamanın kendi yazı tipiyle yapılır (Atkinson Hyperlegible Next,
16 px). Tavan `Themes/Theme.axaml` belirteçlerinden
hesaplanır: `TooltipMaxWidth` eksi iki yanın dolgusu ve kenarlığı = **746 px**.

Ölçülen satır: **172** · tavanı aşan: **20** ·
tek kelimeyle aşan: **0**

| Dil | İpucu | Satır | Genişlik | Taşma | Görsel satır | Alt satır | Tek kelime |
| --- | --- | ---: | ---: | ---: | ---: | --- | :-: |
| EN | main.chip.whatsapp.tip · WhatsApp re-encodes in-chat video with its o… | 1 | 960 | 214 | 2 | VidShrink's quality, not WhatsApp's. |  |
| TR | main.chip.whatsapp.tip · WhatsApp re-encodes in-chat video with its o… | 1 | 957 | 211 | 2 | değil VidShrink'in kalitesi olur. |  |
| EN | main.fast-gpu.tip · Graphics cards encode many times faster than… | 1 | 1158 | 412 | 2 | software encoder's quality at about seven times the speed. |  |
| TR | main.fast-gpu.tip · Graphics cards encode many times faster than… | 1 | 1116 | 370 | 2 | kodlayıcısıyla neredeyse aynı kaliteyi yaklaşık yedi kat hızlı verir. |  |
| EN | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 0 | 1116 | 370 | 2 | from this machine and this file, not from a preset table. |  |
| EN | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 1 | 1057 | 311 | 2 | second, and how much less is not measured. |  |
| TR | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 0 | 1115 | 369 | 2 | tablosundan değil, bu makineden ve bu dosyadan gelir. |  |
| TR | main.output.estimated-time.tip · VidShrink times the sample encodes it alread… | 1 | 1019 | 273 | 2 | olur, ne kadar ucuz olduğu ise ölçülmez. |  |
| EN | main.output.estimated-output.tip · Before planning, VidShrink encodes short sam… | 0 | 1154 | 408 | 2 | many bits it needs — it does not guess from the source bitrate. |  |
| EN | main.output.estimated-output.tip · Before planning, VidShrink encodes short sam… | 2 | 1134 | 388 | 2 | because spending the rest would buy nothing you could see. |  |
| TR | main.output.estimated-output.tip · Before planning, VidShrink encodes short sam… | 0 | 1181 | 435 | 2 | kaç bit gerektiğini ölçer — kaynak bit hızından tahmin yürütmez. |  |
| TR | main.output.estimated-output.tip · Before planning, VidShrink encodes short sam… | 2 | 1038 | 292 | 2 | harcamak gözle görülür bir şey satın almaz. |  |
| EN | main.convert.container.tip · The container is the file type. | 1 | 851 | 105 | 2 | phone opens it. |  |
| TR | main.convert.container.tip · The container is the file type. | 1 | 943 | 197 | 2 | tek biçimdir — her telefon açar. |  |
| EN | main.convert.video-codec.tip · H.264 plays on nearly every device and is wh… | 2 | 786 | 40 | 2 | encode it. |  |
| TR | main.convert.video-codec.tip · H.264 plays on nearly every device and is wh… | 0 | 919 | 173 | 2 | çalışması gerekiyorsa bunu seçin. |  |
| TR | main.convert.crf-label.tip · In CRF mode, a lower number means higher qua… | 1 | 803 | 57 | 2 | dosya verir. |  |
| EN | settings.share.tip · The share target is the service a finished f… | 2 | 912 | 166 | 2 | can close the link early. |  |
| EN | settings.update.auto-effect · When this is off, VidShrink does not update… | 0 | 915 | 169 | 2 | command that installs it. |  |
| EN | settings.update.no-self-effect · VidShrink does not update itself on this sys… | 0 | 905 | 159 | 2 | command that installs it. |  |
