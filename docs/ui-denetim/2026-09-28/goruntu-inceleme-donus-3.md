**Bulgular** (basit ile gelişmiş arasında 2, 3 ve 6. sekmeler piksel piksel aynı; `*` iki kipi ve iki dili birlikte kapsar)

| # | Dosya | Yer (px) | Ne görüldü | Önem |
|---|---|---|---|---|
| 1 | `*-100-2`, `*-125-2`, `tr-gelismis-150-2` | Karşılaştırma Paneli, 650-1260 × 125-640 | Yaklaşık 610×515'lik kutu neredeyse tümüyle boş. Kaynakta dosya yüklü olduğu halde "İki tarafı görmek için bir dosya yükleyin" yazıyor. | yüksek |
| 2 | `*-100-2` | Tahmini Çıktı (1297,395) ile Tahmini Boyut (821,928) | Aynı 15,6 MB değeri iki kartta ayrı ayrı yazılı. | orta |
| 3 | `*-100-2` | Hedef özeti (49,595), Ne Çıkacak › Kalite ve Uyumluluk (1338,691) | "Otomatik · Hedefi Doldur" iki yerde yinelenmiş. "Hedef 16 MB" de hemen üstteki 16 MB kutusunu tekrarlıyor. | düşük |
| 4 | `*-100-2` | Çıktı kartı (1297,474), Kalite kutusu (510,675) | "Öngörülen Kalite 49,9/100" Kalite alanındaki değeri tekrarlıyor. | düşük |
| 5 | `*-100-3`, `*-125-3`, `tr-gelismis-150-3` | Özel Genişlik × Yükseklik (491-916,490), Özel Kare Hızı (491-916,575) | Çözünürlük ve Kare Hızı "Kaynak" seçiliyken 1280x720 ve 25 kutuları etkin ve dolu görünüyor. | orta |
| 6 | `*-100-3`, `en-gelismis-125-3` | FFmpeg Komutu kutusu (1790-1822,205-224; 125'te 1700-1015,256-279) | Satır kırılması bayrağı değerinden ayırıyor: "-crf / 23", "-c:v / libx264". | düşük |
| 7 | `tr-gelismis-100-4`, `en-gelismis-100-4` | Çıkış Klasörü kutusu (530-812,778 / 530-802,799) | Yol üç nokta olmadan harf ortasından kesiliyor ("…\VidShr", "…\VidSh"). | orta |
| 8 | `en-gelismis-100-4`, `en-gelismis-125-4` | Approximate Length (S) (517,409-430); Time Limit / Split Every (S) (1194,329) | Etiket iki satıra bölünüyor. Saniye birimi "(S)" olarak büyük harfle yazılmış, oysa aynı ekranda "30 s" var. | düşük |
| 9 | `tr-gelismis-125-4`, `en-gelismis-125-4` | Kamera kartı, Köşe (1446,508) ile Kamera Arka Planı (1646,481-507) | Sağdaki etiket iki satıra kaydığı için Köşe etiketi ve iki açılır kutu yataydan kaymış. İki paragraf arasında ~30 px fazladan boşluk var (660-690). | düşük |
| 10 | `tr-gelismis-150-4` | Geri Sayım (73,731) ile Kayıt Tamponu (265,700-731) | "Kayıt Tamponu" iki satır; iki etiket aynı hizada değil. "Ayarları Program Seçsin" üç satıra bölünüyor (580,457-519). | düşük |
| 11 | `tr-gelismis-150-3` | CRF satırı (493-887,551-741) | "23" kutusu kaydırıcının altına iniyor, solunda ~250 px boşluk kalıyor. Kalite Modu etiketi (73,583) yandaki etiketle (493,551) hizasız. | orta |
| 12 | `*-125-6`, `tr-gelismis-150-6` | Sağ sütunun en üstü (1266-1862,157-267) | "Tüm Ayarları Sıfırla" kartı Çıktı kartının üstüne çıkmış. 100'de sol sütunun en altında duruyor; sıra tutarsız. | düşük |
| 13 | `*-100-6`, `*-125-6` | Gelişmiş başlığı (518-723,953) | "Gelişmiş" büyük başlık, yanındaki "dokunulmadı" / "untouched" küçük düz metin; taban çizgileri ve ağırlıkları uyumsuz. | düşük |
| 14 | `*-100-6` | Sağ Tık Menüsü › Varsayılan Yap (519-661,805) ve üst şerit (1509,68) | Aynı Windows "Varsayılan uygulamalar" eylemi aynı ekranda iki düğmeyle sunuluyor. | düşük |
| 15 | `*-100-6` | OpenSubtitles Hesabı etiketi (987-1297,287-308) | Etiket iki satıra bölünmüş; "?" simgesi (1319,298) metinden kopuk, iki satırın ortasında asılı kalıyor. | düşük |
| 16 | `*-100-2` | Karşılaştırma Paneli (953,368) ve Ne Çıkacak (1297,628) | Bu iki başlık küçük, eşaralıklı yazıyla; öteki kart başlıkları (Kaynak, Çıktı, Yapılacak İşlem) büyük düz yazıyla. Başlık düzeni tutarsız. | düşük |
| 17 | `en-gelismis-125-2` | Output kartı (1294,420-446) | "…on this machine / yet." — son sözcük tek başına ikinci satıra düşüyor. | düşük |
| 18 | 28 karenin tümü | Sağ kenar, x≈1898-1920, y≈740-1040 | Arka plan görseli keskin dikey bir çizgiyle bitiyor; 22 px'lik düz şerit kalıyor. | düşük |
| — | `tr-basit-100-4`, `en-basit-100-4` | — | 0 bulgu. | — |

**Şüpheli**

| # | Dosya | Yer (px) | Ne görüldü | Önem |
|---|---|---|---|---|
| Ş1 | `*-100-6` | Güncelleme › Kendiliğinden Güncelle (987,592) | Kutu kapalı. Windows'ta varsayılan açıksa varsayılan ayar hâlâ yüklenmiyor olabilir. | orta |
| Ş2 | `*-100-2` | Kalite kaydırıcısı (60-440,676) | Plan "Otomatik · Hedefi Doldur" iken Kalite kaydırıcısı ve kutusu etkin görünüyor; seçimle çelişiyor olabilir. | orta |
| Ş3 | `tr-gelismis-150-*` | Başlık şeridi (1493-1706,18) | 150'de "Destek Ol" ve "Teknesyum" hiç görünmüyor. Kademe tasarımı mı, yoksa kaybolan içerik mi, belli değil. | düşük |
| Ş4 | `*-gelismis-100-4` | Kayıt Seçenekleri, Tahmini Süre ve Tahmini Boyut kutuları (517-922,448) | "Ayarları Program Seçsin" seçiliyken iki kutu etkin görünüyor. | düşük |
