# NVENC Aşağı Deneme: Yüksek İlk Nişan, Aşınca Bir Aşağı

Dal: `t0/nvenc-asagi` (`origin/main` 0bc86188 üstü). Karar: `fable-kararlar-2026-09-17.md` soru 2, aday 3.
Önceki iş: `butce-doldur.md` (aday 1 kaldı, aday 2 yalnız yazılımda kaldı), hücreler `nvenc-2.md`.

## Kural (Ölçümden Önce Yazıldı)

Kodlayıcı NVENC ise (`CodecModel.Vendor(codec) == Nvenc`) planın ilk nişanı bant merkezi (hedef < 10 MB'de
hedefin %96'sı) değil `BudgetFill.Aim` (hedefin %98,5'i). İlk deneme tavanı aşarsa var olan düzeltme kolu
(`PlanCalculator.Correct`, ölçülmüş verimle bant merkezine) bir aşağı deneme koşar; tavan bekçisi ve en küçük tavan
üstü teslim değişmez. Yazılım, QSV, AMF ve VideoToolbox ilk nişanı bugünkü gibi kalır (QSV/AMF bu makinede
ölçülemiyor, iddia NVENC'le sınırlı). Yazılımın yukarı denemesi (`BudgetFill`) değişmez; testle pimli.

## Kapı

Her hücre iki kol: **önce** (`origin/main` 0bc86188 Bench) ve **sonra** (bu dal Bench), aynı oturumda, aynı ölçerle,
sırayla, makinede her an tek ffmpeg. Düzenek `tools/butce-doldur/kos.ps1`.

| Kapı | Küme | Koşul |
|---|---|---|
| K1 dolum | 26 hücre | sonra kolunda ortalama teslim/hedef sapması ≥ −%2,5 (nvenc-2'de −%4,8) |
| K2 taşma | 26 + tutma | sonra kolunda hiçbir teslim hedefi aşmaz |
| K3 deneme | 26 hücre | sonra kolunda ortalama deneme ≤ 2,0 |
| K4 kalite | 26 + tutma | hiçbir hücrede sonra VMAF-NEG ort − önce VMAF-NEG ort < −0,3 değil |
| K5 tutma dolum | tutma | sonra ortalama sapma ≥ önce ortalama sapma |
| K6 negatif | yazılım | libx264 `parlak` 1000 ve libx265 `orta` 2000 çıktıları önce/sonra md5 aynı |

Hücreler: `butce-doldur.md`'nin 26 NVENC hücresi. Tutma: nvenc-2'nin kalan 12 NVENC hücresi (\|sapma\| ≤ %3, bütçe
işlerinde kullanılmadı): hareketli av1 2000/3500, karanlik h264 2000, karanlik hevc 1000, orta av1 2000/3500,
orta h264 1000, orta hevc 600, parlak av1 2000/3500, parlak h264 2000, parlak hevc 3500.

Hepsi geçerse kod kalır. Biri kalırsa kod geri alınır, `butce-doldur.md` "donanımın basamaklı yanıtı, kapalı" yazar ve
bütçe konusu bir daha açılmaz. Kural sonuçtan sonra gevşetilmez.
