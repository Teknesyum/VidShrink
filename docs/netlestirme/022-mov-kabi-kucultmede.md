# 022 — Küçültmede MOV Kabı: Fable'ın Yanıtı

Girdi: [022-mov-kabi-kucultmede-girdi.md](022-mov-kabi-kucultmede-girdi.md). Yanıt birebir:

---

1. **Küçültme koluna MOV eklenmeli; ama "sessiz MP4 sayma" ondan bağımsız olarak bugün düzeltilmeli.** Kullanıcı `.mov` yazınca ffmpeg mov muxer'ı seçiyor, biz MP4 haritası kuruyoruz — bu bir kap desteği eksiği değil, yanlış davranış. Reddetmek kullanıcıyı "dönüştür sekmesine git" diye başka yere sürer ve orada hedef boyut yok; oysa küçültme kolu HandBrake ile eşdeğer olmayı zaten hedefliyor (E2). MP4 ailesinden olduğu için maliyet küçük: `OutputContainer.Mov`, `ContainerOf`'a `.mov`, muxer/faststart/mov_text aynen, yalnız ses kopyalama listesi ayrışıyor.

2. **Yeni kutu açma; uzantıdan türet.** Mevcut sözleşme zaten "kap sorulmaz, `ContainerFor` türetir, `ContainerOf` uzantıdan okur". MOV için kullanıcıya bir açılır kutu sunmak hem tutarsız (MP4/MKV için yok) hem de gereksiz karar yükü. Kullanıcı çıktı adını `.mov` yazarsa MOV; otomatik üretilen adda türetim değişmez (girdi `.mov` olsa bile — Apple ekosisteminde kalmak isteyen `.mov`u kendisi yazar; girdiden miras almak kimseye sormadan opus kopyalamayı bozar). CLI'da ayrı bayrak da açılmaz: çıktı yolu zaten bayrak, uzantı orada; ikinci kaynak çelişki üretir.

3. **Sessizce yeniden kodla, `StatusWarning` ile söyle.** MOV'da opus/flac kopyalanamaz — bu kullanıcının seçemeyeceği bir sabit; sormak ya da reddetmek "MOV istedim, çalışmadı" demektir. Doğru davranış: `ContainerOf == Mov` iken kopyalanabilir küme aac/ac3/eac3/mp3/alac; opus/flac gelirse aac'ye kodla, plan hesabında ses bütçesini buna göre ayır (hedef boy şaşmasın), iş bitince durum metni "iş bitti ama ses yeniden kodlandı" tonunda uyarı — projede bu tam olarak `StatusWarning`in tanımı (simge ve ağırlık, renk yok). Kayıpsız kaynağı (flac) kaybettiği için sessiz geçiştirme olmaz; ama iş de kesilmez.
