# alinti-denetimi

Belgede "birebir" diye verilen dizgeyi kaynağından doğrular. Python 3, bağımlılık yok.

    python tools/alinti-denetimi/alinti-denetimi.py docs
    python tools/alinti-denetimi/alinti-denetimi.py --self-test

Çıkış kodu: bulgu varsa 1, yoksa 0. `--atlananlar` denetlenemeyeni sebebiyle listeler.

Denetlenen iddia biçimi: `dosya:N` künyesini bir ayıraç (`—`, `-`, `:`) **ya da** bulunma
eki (`'deki`, `'de`) ve hemen ardından ters tırnaklı bir dizge izliyorsa; veya künyeyi bir
çit bloğu izliyorsa. **Künye satır numarası taşımak zorunda.** Boşluk ve satır sonu
normalleştirilir, `...` ile kırpılan alıntı parça parça aranır.

Bulgu: `KAYMA` dizge dosyada hiç yok. `SATIR KAYDI` dizge var ama künyedeki aralıkta değil.

`--supheli` (blok kapsamı) **deneysel ve kapalı** — ölçülen yanlış pozitif oranı 20/20.
Gürültüsü rapordadır, üretimde kullanma.

Kapsam yalnız `docs/`. Sözleşme klasörü uygun değil: sözleşmeler henüz yazılmamış kodu
alıntılar, 19 iddianın 16'sı bulgu verir.

Ölçüm ve oranlar: `docs/olcumler/alinti-denetimi.md`.
