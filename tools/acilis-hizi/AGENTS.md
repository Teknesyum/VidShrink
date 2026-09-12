# Acilis hizi olcumu

Cift tiktan ilk kareye kadar gecen sureyi olcer (7. dalga, 7c kolu). Sayilar
`docs/olcumler/acilis-hizi.md` icinde; burada onlari **ureten** duzenek var.

    pwsh -File tools/acilis-hizi/olcum.ps1 -Exe <VidShrink.App.exe> -Klip <video> `
         -Cikti .calisma/dalga7c -Kip sicak -Tekrar 12 -Libmpv <libmpv-2.dll> -Etiket once-sicak

- `-Kip sicak` — ayni yayin klasorunden art arda acilis.
- `-Kip soguk` — her tekrarda yayin klasoru yeni bir yola kopyalanir; **disk onbellegi
  temizlenmez**, sogukluk surec ve yol sogukluguydur, sayfa onbellegi degil.
- Cikti: `ham-<etiket>.csv` (her tekrarin butun adimlari) ve `ozet-<etiket>.txt`
  (adim basina n, en az, ortanca, p95, en cok). p95 en yakin sira yontemi: `ceil(0.95*n)`.

**Saat surecin kendisinde.** Uygulama `VIDSHRINK_ACILIS_IZI` doluyken her adimi
`adim<TAB>ms` olarak o dosyaya yazar; sifir noktasi `Process.StartTime`, yani surecin
isletim sistemince yaratildigi an. Betik saat okumaz, dosyayi bekler. Kabugun
`CreateProcess`'ten onceki kendi payi bu sayiya **girmez**.

Iz kancasi uretimde bedelsiz: `AcilisIzi.Yaz` degisken bos oldugunda tek bir ortam
degiskeni okumasidir, dosya acmaz. Ilk kareyi bekleyen 1 ms'lik saat (`IlkKareyiBekle`)
yalniz degisken doluyken kurulur.

**Her kosum kendi tek ornek kanalini alir** (`VIDSHRINK_INSTANCE_CHANNEL`): yoksa ikinci
acilis yolu koşan surece iletip hemen cikar ve olcum bos doner. Surec her tekrarda
`Stop-Process` ile kapatilir ve kapandigi dogrulanir.

Adimlar sirayla: `main`, `tek-ornek`, `cerceve`, `gecici-temizlik`, `palet`,
`pencere-kuruldu`, `pencere-yuklendi`, `ayarlar`, `varsayilan-oneri`,
`giris-canlandirmasi`, `sekme`, `kare-kaynagi`, `ilk-kare`, `motor-acildi`,
`kucultme-yuklendi`.

Klipler `.calisma/dalga7c/klip/` altinda; olcum ciktisi da `.calisma/dalga7c/` altina.
`.sln`e eklenmedi — betik, CI'da kosmaz.
