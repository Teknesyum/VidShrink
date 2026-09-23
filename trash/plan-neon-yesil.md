# Plan — Neon Teması: Yeşil Zemin, Mor Gradyan Vurgu

İstek: Neon paletinde arka plan yeşil; camgöbeği vurgu #00F3FF'ten mora giden
gradyan, güncelleme panelindeki gibi. Danışma kaydı
`docs/tasarim/fable-neon-yesil-2026-09-23.md`.

## Adımlar

1. `seeds.json`'a isteğe bağlı `atmos` çekirdeği. Neon'da `#34D399`
   (teknesyum-ui `neon-success`); öteki 25 palette yok, onlarda atmosfer eskisi
   gibi ember/flame/blaze.
2. `PaletteGen`: üç yeni anahtar `AtmosHotColor`/`AtmosMidColor`/`AtmosEdgeColor`.
   `atmos` varsa beyaza %35, kendisi, zemine %40; yoksa blaze/flame/ember.
   `EmberDeep/Mid/Edge` ve `EmberBar*` artık atmosfer tabanından karışır.
3. `Theme.axaml`: anka ve atmosfer fırçaları `Atmos*` anahtarlarından; yeni
   `AccentGradient` (NeonBlue → NeonPurple).
4. `Controls.axaml`: birincil düğme ve ilerleme çubuğu dolgusu `AccentGradient`;
   `MainWindow.axaml` güncelleme çubuğu sarı yerine aynı gradyan.
5. Kayıt kırmızısı (`NeonEmber`) değişmez: kaydedici onu kullanıyor.
6. Testler: `ThemeTokenTests` rampa kuralı tondan parlaklığa (sıcak taban = en
   açık durak), `LanguageTests` muafiyet listesine üç anahtar, yeni ölçü: Neon'un
   atmosferi yeşil, öteki paletlerde atmosfer = ember (olumsuz kontrol).
7. Paletleri üret, derle, dokunulan testler, görsel kare, kullanıcıya göster.
