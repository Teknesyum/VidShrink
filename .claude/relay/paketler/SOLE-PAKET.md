# Sole — iki iş, sırayla. Aralarında bana dönme.

Kural: birincisini bitirip ittikten sonra **beklemeden** ikincisine geç. Denetimi ben
paralel koşturacağım; denetim sonucu sana ayrıca gelir, onu beklemek zorunda değilsin.

---

## İş 1 — T88 düzeltme turu 2 (elindeki iş)

Sözleşme: `.claude/relay/contracts/T88.md`, sondaki **"# Düzeltme turu 2"** bölümü.
`main`de güncel; önce `git pull` at.

Bağımsız denetim tur 1'e **KALDI** verdi. Senin raporun "Plan kararları bu sözleşmede
değişmedi" diyordu, denetim bunun yanlış olduğunu ölçtü:

**E1 (KRİTİK)** — `ComplexityProbe.cs:437,452` tam ölçek örneğini `-f matroska`ya aldı
ama `halfBytes` ham h264 kaldı (`:438,484`). Konteyner ek yükü yalnız bir tarafa biniyor.
Ölçülen: düşük karmaşıklıklı içerikte 5,3K → 6,6K, **+%24,5**. Bu doğrudan
`ComplexityProfile.FromProbe`a giriyor; `DetailExponent` düşük bit hızlı pencerelerde
~+0,32 kayıyor. İki taraf aynı birimi ölçecek.

E2–E5 sözleşmede yazılı: `RunAsync` varsayılanı, ölçülmeyen kriter 3, kendi sayacından
beslenen assertion, veriden güçlü rapor cümleleri.

Dal aynı: `T88-ornekte-kalite-olcumu`. Bitince it, `main`e **birleştirme**.

---

## İş 2 — T85 düzeltme turu 2 (sıradaki iş, İş 1 biter bitmez başla)

Sözleşme: `.claude/relay/contracts/T85.md`, sondaki **"# Düzeltme turu 2"** bölümü.

Tur 1 gerçek iş yaptı — pencere sızıntısı ölçüldü ve kapatıldı, sınıf %35 hızlandı —
ama sözleşmenin asıl hedefi karşılanmadı: **çökme hiç üretilemedi**, dolayısıyla
"düzeltildi" denemez. Raporun kendisi bunu yazıyor.

Turun taşıyan maddesi **F2**: `dotnet test` kilitlendiğinde

    Etkin test çalıştırması iptal edildi. Nedeni: Test ana işlemi kilitlendi
    Başarısız: 0

basıp **çıkış kodu 0** dönüyor. Bu yüzden bu projede dört kez yanlış yeşil okundu ve
her sözleşmenin `verify:` satırı bugün bu duruma kör. `tools/kosum-kapisi/` altına
kesinti satırını, `Başarısız: 0`'ı ve `Toplam:` alt sınırını birlikte denetleyen bir
kapı yazacaksın; Türkçe ve İngilizce çıktının ikisini de tanıyacak.

Dal: `t85-suit-esszamanli`. Bitince it, `main`e birleştirme.

---

## İkisi için de geçerli

- Kendi dalında çalış, paylaşılan çalışma ağacına (`Desktop/Projeler/Vidshrink`) yazma.
- Hiçbir assertion gevşetilmez, hiçbir test `Skip`e alınmaz, hiçbir beklenti ölçümün
  kendi çıktısından türetilmez.
- Her düzeltme için mutasyon; sonucu `docs/olcumler/` altına yaz.
- Tam süit bir kez koşulur. Çıktıda kesinti satırı varsa çıkış kodu 0 ve `Başarısız: 0`
  olsa bile koşum yarımdır — raporda toplam test sayısını yaz.
- Ölçmediğin şey için "ölçülmedi" yaz. Kazanç iddiası yazma.
