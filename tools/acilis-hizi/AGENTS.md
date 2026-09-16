# Acilis hizi olcumu

Cift tiktan ilk kareye gecen sureyi olcer (7. dalga, 7c kolu). Sayilar
`docs/olcumler/acilis-hizi.md` icinde; burada onlari **ureten** duzenek var.

    pwsh -File tools/acilis-hizi/olcum.ps1 -Exe <taban.exe> -ExeB <yeni.exe> `
         -Etiket taban -EtiketB iyilestirme -Klip <video> -Cikti .calisma/dalga7c/eslesik `
         -Kip sicak -Tekrar 12 -Libmpv <libmpv-2.dll>

**`-ExeB` olmadan olcum alma.** Bu makine oturumlar arasinda 1,7 kata kadar kayiyor:
ayri oturumlarda alinan once/sonra sayilari kaymayi iyilesme sanma tuzagidir, bir kez
dusuldu. `-ExeB` verilince her tekrarda iki yapi da kosar ve sira tekrardan tekrara
doner; hukum `eslesik fark` tablosundan, farkin ortancasi ve kac tekrarda ayni yone
baktigi okunarak verilir. Ortanca sutunlari kaymayi tasir, hukum vermez.

- `-Kip sicak` — ayni yayin klasorunden art arda acilis.
- `-Kip soguk` — yayin klasoru her tekrarda yeni yola kopyalanir; **disk onbellegi
  temizlenmez**, sogukluk surec ve yol sogukluguydur. p95: `ceil(0.95*n)`.
- Cikti: `ham-*.csv` (her tekrarin butun adimlari), `ozet-*.txt` (n/en az/ortanca/p95).

**Saat surecin kendisinde.** Uygulama `VIDSHRINK_ACILIS_IZI` doluyken her adimi
`adim<TAB>ms` yazar; sifir noktasi `Process.StartTime`. Kabugun `CreateProcess`
oncesi payi bu sayiya **girmez**. Kanca uretimde bedelsiz: degisken bosken
`AcilisIzi.Yaz` tek bir ortam degiskeni okumasidir, `IlkKareyiBekle` hic kurulmaz.

**Olculen sey `VidShrink.exe`, uygulama degil.** Baslatici uygulamayi doguruyor ve kendi
cikiyor; bu yuzden surecin olmesi kosumun bittigi anlamina gelmez. Dongu izi bekler,
oldurulen sey de baslatici degil olcum klasorunden kosan uygulamadir. Kullanicinin kurulu
VidShrink'i ayni adi tasiyor; oldurmeden once yolun olcum klasorunun altinda oldugu
dogrulanir.

**Perde yok.** Hipersurus F dalgasindan beri olagan acilista panel cizilmiyor; kurulum
paneli yalniz 400 ms'yi asan bakim isinde gorunur. `perde` adimi listeden cikti.

**EkranSaati** (`EkranSaati/`) ayni isi kullanicinin ekranina dokunmadan yapar: ayri bir
Win32 masaustu (`WinSta0\vidshrink-olcum`) acar, uygulamayi orada dogurur, iz saatini
okur; ekran okunmaz. Her surece `KayitKalkani` baslangic kancasi yuklenir: HKCU ozel
kovana yonlenir, hata olursa kanca sessizce 97 ile cikar (FailFast yok, hata kutusu yok).
Her kosumdan once ve sonra sag tik menusu, etiketler ve iliskilendirme karsilastirilir;
fark varsa geri yazar ve durur. `--yuk <sistem-izleme.log>`: cpu/gpu 60 ustuyse kosum
baslamaz, kosum sonrasi cpu 80 ustuyse olcum durur.

**Her kosum kendi tek ornek kanalini alir** (`VIDSHRINK_INSTANCE_CHANNEL`): yoksa ikinci
acilis yolu kosan surece iletip cikar ve olcum bos doner.

Isaretler birikimlidir: bir adimin farki kendinden oncekileri de icerir, pay cikarmak
icin ardisik iki isaretin farki alinir. `varsayilan-oneri` bilerek ilk karenin arkasina
alindi; buyuk gorunmesi yavaslama degil, yer degistirmedir.

Klipler ve cikti `.calisma/dalga7c/` altinda. `.sln`e eklenmedi, CI'da kosmaz.
