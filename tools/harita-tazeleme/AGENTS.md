# harita-tazeleme

`docs/inceleme/handbrake-motoru.md` § 6'daki toplu hüküm cümlelerini veriden üretir.
Python 3, bağımlılık yok. T135'te yazıldı.

    python tools/harita-tazeleme/harita-tazeleme.py tara      # bayat sayı adayları
    python tools/harita-tazeleme/harita-tazeleme.py bayat     # satır x eski x yeni x çapa
    python tools/harita-tazeleme/harita-tazeleme.py sira      # eski/yeni sıralama + gerekçe
    python tools/harita-tazeleme/harita-tazeleme.py hukum     # belgeye giren hüküm cümleleri
    python tools/harita-tazeleme/harita-tazeleme.py dogrula   # bulgu varsa çıkış kodu 1

**Belgedeki hüküm cümlesi elle yazılmaz.** "Şu kadar açık var", "en büyüğü şu",
"sırası şu" türünden her cümle `hukum` çıktısından birebir kopyalanır; `dogrula`
her cümlenin belgede durduğunu ve her düzeltilmiş sayının kaynağında
(`docs/olcumler/auto-mod.md`) bulunduğunu denetler. Boşluk normalleştirilir, yani
belgede 75 sütuna sarılmış olması sorun değil.

Sayı değişince **veri tablosu düzeltilir, cümle değil**: `ACIK_YENI`, `MADDELER`,
`BAYAT`. Sonra `hukum` çıktısı belgeye yeniden yapıştırılır ve `dogrula` koşulur.

`BAYAT` içindeki satır numaraları haritanın T135 öncesi hâline (`890af6e`) aittir;
çapa bölüm başlığı + tablo satırının ilk hücresidir, satır numarası değil.
