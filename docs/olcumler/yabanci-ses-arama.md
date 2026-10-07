# Yabancı Ses Araması (`--altyazi-tara`)

Tarih 2026-10-07, dal `worktree-agent-a56b0b45fa9b372c7`, ffmpeg 9.0.
HandBrake'in "Foreign Audio Search"unun (`--subtitle scan` + `--subtitle-forced`) karşılığı:
filmin yalnız yabancı dilde konuşulan yerlerini çeviren altyazıyı bulup görüntüye yakar.

## Nasıl Karar Veriyor

1. Kaynağın kendi `forced` bayrağı okunur (ffprobe `disposition.forced`). Bayraklı iz varsa o seçilir;
   birden çoksa önce tercih edilen dildeki, sonra sesin dilindeki, yoksa ilki. Sayım yapılmaz.
2. Bayrak yoksa altyazı paketleri sayılır (`ffprobe -select_streams s -show_entries packet=stream_index,size`).
   Kodlama ve kod çözme yok; yalnız kap okunur.
3. İzler dil ve türe (metin / resim) göre kümelenir. Bir kümede paketi, kümenin en dolu izinin
   **%10**'unu geçmeyen ve sıfır olmayan tek iz adaydır (`ForeignAudioSearch.SparseShare`).
4. Tek aday varsa seçilir. Birden çok kümede aday varsa tercih edilen dil, sonra sesin dili ayırır.
5. Bulunan iz `--yak N`'in alanına yazılır; ikinci bir yakma yolu yok.

## İz Seçilmeyen Durumlar

Her biri bir satırla söylenir, kodlama yakmasız sürer.

| Sonuç | Ne zaman | Anahtar |
|---|---|---|
| altyazı yok | yakılabilir (metin ya da resim) iz yok | `result.subtitle-scan.no-subtitles` |
| sayılamadı | ffprobe sayımı düştü | `result.subtitle-scan.not-measured` |
| aday yok | kümede tek iz var, iz boş, ya da hiçbiri eşiğin altında değil | `result.subtitle-scan.no-candidate` |
| belirsiz | aynı kümede iki seyrek iz, ya da dilin ayıramadığı iki küme | `result.subtitle-scan.ambiguous` |

`--altyazi-tara` ile `--yak`, `--yak-srt`, `--yak-ass` birlikte verilemez (`error.scan-burn-conflict`);
`izle` komutu bayrağı almaz.

## Eşik

Eşiğin iki yakası sahte sayımla pimli: en dolu iz 100 paketken 10 ve 1 paketlik iz seçilir, 11 ve
100 paketlik seçilmez, 0 paketlik (boş) iz seçilmez.

Oran neden kümenin içinde: bir Fransızca tam altyazı ile bir İngilizce tam altyazı farklı sayıda
paket taşır, ama ikisi de tam altyazıdır. Seyreklik yalnız aynı dilin aynı türdeki dolu iziyle
kıyaslanınca anlam taşır. Resim altyazı paketi metin altyazı paketiyle de kıyaslanmaz (PGS bir
satır için birden çok paket yazar).

**%10 gerçek film üstünde ölçülmedi.** Bu makinede zorunlu altyazılı bir film yok, ağ ve örnek
indirme kural gereği kapalı. Sayı, yalnız yabancı konuşmaları çeviren bir izin tam altyazının
küçük bir kesri olması beklentisine dayanıyor; HandBrake'in kendi oranı kaynağından doğrulanmadı.
Eşik yanlış yöne kayarsa sonuç yanlış iz değil "aday yok" olur: kural, emin olunamayan yerde
seçmemek üzerine kurulu.

## Canlı Ölçüm

`AltyaziTaramaTests.CanliPaketSayimi`, 3 sn 320x240 lavfi klibi, `-threads 2`, iki subrip izi:

| Klip | 1. iz | 2. iz | Karar |
|---|---|---|---|
| 12 satır + 1 satır | 12 paket | 1 paket | `Sparse`, 2. iz |
| 12 satır + 12 satır | 12 paket | 12 paket | `NoCandidate` |
| olmayan dosya | — | — | sayım `null` |

ffprobe satır başına bir paket sayıyor; sayım sözlüğünde yalnız iki altyazı akışı var.

## Mutasyonlar

`AltyaziTaramaTests` 19 kol. Her mutasyon tek yeri değiştirdi, Release derlendi, yalnız bu sınıf
koşuldu, dosya elle geri yazıldı.

| Kod | Mutasyon | Kırmızı |
|---|---|---|
| F1 | eşik %50 | 1 |
| F2 | `forced` bayrağı okunmaz | 1 |
| F3 | yakma seçenekleriyle çakışma denetimi yok | 1 |
| F4 | bulunan iz yakmaya beslenmez | 2 |
| F5 | küme dile göre ayrılmaz | 2 |
| F6 | küme türe göre ayrılmaz | 1 |
| F7 | boş iz de aday | 2 |
| F8 | belirsizde ilk aday seçilir | 1 |
| F9 | paket yerine bayt sayılır | 1 |
| F10 | `izle` bayrağı reddetmez | 1 |
| F11 | "kümede en az iki iz" satırı kaldırılır | 0 |
| F12 | bayraklı izlerde dil sırası yok | 1 |

On ikinin on biri kırmızı. F11 ölü koldu: tek izli kümede iz kendi en dolusu olduğundan %10'un
altında kalamıyor. Satır kaynaktan kaldırıldı.

## Ölçülmeyenler

- Gerçek film, gerçek zorunlu altyazı; yalnız sentetik klip.
- Resim altyazıda (PGS, VOBSUB) paket sayısının satır sayısına oranı.
- Büyük dosyada sayımın süresi; ffprobe bütün kabı okur.
- Arayüz: "Yabancı dil altyazısını bul" düğmesi aynı `Flagged`/`Decide` yolunu çağırır
  (`MainWindow.AltyaziTarama.cs`, `IzPaneliTests`); gerçek pencerede ve gerçek sayımla denenmedi,
  testte sayaç sahte.
